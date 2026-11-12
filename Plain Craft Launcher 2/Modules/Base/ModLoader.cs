using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf.Extensions;

namespace PCL
{
    public static class ModLoader
    {

        // 各类加载器
        /// <summary>
    /// 加载器的统一基类。
    /// </summary>
        public abstract class LoaderBase : ILoadingTrigger
        {

            public bool IsLoader { get; private set; } = true;

            // 基础属性
            /// <summary>
        /// 加载器的标识编号。
        /// </summary>
            public int Uuid = ModBase.GetUuid();
            /// <summary>
        /// 加载器的名称。
        /// </summary>
            public string Name;
            /// <summary>
        /// 用于状态改变检测的同步锁。
        /// </summary>
            public readonly object LockState = new object();
            /// <summary>
        /// 父加载器。
        /// </summary>
            public LoaderBase Parent = null;
            /// <summary>
        /// 最上级的加载器。
        /// </summary>
            public LoaderBase RealParent
            {
                get
                {
                    LoaderBase RealParentRet = default;
                    try
                    {
                        RealParentRet = Parent;
                        while (RealParentRet is not null && RealParentRet.Parent is not null)
                            RealParentRet = RealParentRet.Parent;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"获取父加载器失败（{Name}）");
                        return null;
                    }

                    return RealParentRet;
                }
            }

            public LoaderBase()
            {
                Name = "未命名任务 " + Uuid + "#";
            }

            public virtual void InitParent(LoaderBase Parent)
            {
                this.Parent = Parent;
            }

            // 事件

            /// <summary>
        /// 当状态改变时，在工作线程触发代码。在添加事件后，必须将 HasOnStateChangedThread 设为 True。
        /// </summary>
            public event OnStateChangedThreadEventHandler OnStateChangedThread;

            public delegate void OnStateChangedThreadEventHandler(LoaderBase Loader, ModBase.LoadState NewState, ModBase.LoadState OldState);
            public bool HasOnStateChangedThread = false;

            /// <summary>
        /// 当状态改变时，在 UI 线程触发代码。
        /// </summary>
            public event OnStateChangedUiEventHandler OnStateChangedUi;

            public delegate void OnStateChangedUiEventHandler(LoaderBase Loader, ModBase.LoadState NewState, ModBase.LoadState OldState);
            /// <summary>
        /// 简易的在 UI 线程添加触发事件的方式。主要用于在新建 Loader 时直接使用 With 绑定事件，以及进行老代码兼容。
        /// </summary>
            public Action<LoaderBase> OnStateChanged
            {
                set
                {
                    OnStateChangedUi += (Loader, NewState, OldState) => value(Loader);
                }
            }

            /// <summary>
        /// 在加载器目标事件执行完成，加载器状态即将变为 Finish 时调用。可以视为扩展加载器目标事件。
        /// </summary>
            public event PreviewFinishEventHandler PreviewFinish;

            public delegate void PreviewFinishEventHandler(LoaderBase Loader);
            protected void RaisePreviewFinish()
            {
                PreviewFinish?.Invoke(this);
            }

            // 状态监控
            /// <summary>
        /// 加载器的状态。
        /// </summary>
            public ModBase.LoadState State
            {
                get
                {
                    return _State;
                }
                set
                {
                    if (_State == value)
                        return;
                    var OldState = _State;
                    if (value == ModBase.LoadState.Finished && Settings.Get<bool>("SystemDebugDelay"))
                        Thread.Sleep(ModBase.RandomInteger(100, 2000));
                    _State = value;
                    // 通知等待者：进入加载时换一个新的信号，离开加载时完成它
                    lock (ExitSignalLock)
                    {
                        if (value == ModBase.LoadState.Loading)
                        {
                            if (ExitSignal.Task.IsCompleted)
                                ExitSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        }
                        else
                        {
                            ExitSignal.TrySetResult(true);
                        }
                    }
                    if (value == ModBase.LoadState.Canceled)
                        CancellationTokenSource?.Cancel();
                    Logger.Log($"加载器 {GetType().Name} {Name} 状态改变：{value}", value == ModBase.LoadState.Failed ? LogLevel.Error : LogLevel.Info);
                    // 实现 ILoadingTrigger 接口与 OnStateChanged 回调
                    ModBase.RunInUi(() =>
        {
            switch (value)
            {
                case ModBase.LoadState.Loading:
                    {
                        LoadingState = MyLoading.MyLoadingState.Run;
                        break;
                    }
                case ModBase.LoadState.Failed:
                    {
                        LoadingState = MyLoading.MyLoadingState.Error;
                        break;
                    }

                default:
                    {
                        LoadingState = MyLoading.MyLoadingState.Stop;
                        break;
                    }
            }
            OnStateChangedUi?.Invoke(this, value, OldState);
        });
                    if (HasOnStateChangedThread)
                        ModBase.RunInThread(() => OnStateChangedThread?.Invoke(this, value, OldState));
                }
            }
            private ModBase.LoadState _State = ModBase.LoadState.Waiting;
            /// <summary>
            /// 在加载器离开加载状态时完成的信号。不在加载中时为已完成的信号。
            /// </summary>
            private TaskCompletionSource<bool> ExitSignal = CompletedSignal();
            private readonly object ExitSignalLock = new object();
            private static TaskCompletionSource<bool> CompletedSignal()
            {
                var Signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Signal.SetResult(true);
                return Signal;
            }
            private Task CurrentExitTask
            {
                get
                {
                    lock (ExitSignalLock)
                        return ExitSignal.Task;
                }
            }
            public MyLoading.MyLoadingState LoadingState
            {
                get
                {
                    return _LoadingState;
                }
                set
                {
                    if (_LoadingState == value)
                        return;
                    var OldState = _LoadingState;
                    _LoadingState = value;
                    LoadingStateChanged?.Invoke(value, OldState);
                }
            }
            private MyLoading.MyLoadingState _LoadingState = MyLoading.MyLoadingState.Stop;
            public event ILoadingTrigger.LoadingStateChangedEventHandler LoadingStateChanged;
            /// <summary>
        /// 若加载器出错，可提供给外部参考的异常。
        /// </summary>
            public Exception Error { get; set; } = null;
            /// <summary>
        /// 使用 LoaderCombo 加载时，该任务是否会阻碍后续任务的进行。
        /// </summary>
            public bool Block = true;
            /// <summary>
        /// 该加载器是否显示在列表中。
        /// </summary>
            public bool Show = true;
            /// <summary>
        /// 当前加载器是否由 IsForceRestart 强制调起。
        /// 这个属性自身不会干任何事，而是提供给加载器执行的函数，使得加载器调用另一个加载器时，可以继承强制重启属性。
        /// </summary>
            public bool IsForceRestarting = false;

            // 进度监控
            /// <summary>
        /// 加载器的执行进度，为 0 至 1 的小数。
        /// </summary>
            public virtual double Progress
            {
                get
                {
                    switch (State)
                    {
                        case ModBase.LoadState.Waiting:
                            {
                                return 0d;
                            }
                        case ModBase.LoadState.Loading:
                            {
                                return _Progress == -1 ? 0d : _Progress;
                            }

                        default:
                            {
                                return 1d;
                            }
                    }
                }
                set
                {
                    if (_Progress == value)
                        return;
                    double OldValue = _Progress;
                    _Progress = value;
                    ProgressChanged?.Invoke(value, OldValue);
                }
            }
            private double _Progress = -1;
            public event ILoadingTrigger.ProgressChangedEventHandler ProgressChanged;
            /// <summary>
        /// 创建一个 ProgressProvider，在其进度改变时，并将其进度单向映射到此加载器上。
        /// 这是 Loader 对新框架的兼容层。
        /// </summary>
            public ProgressProvider CreateSyncProgressProvider(double FromPercentage = 0d, double ToPercentage = 1d)
            {
                var Provider = new ProgressProvider();
                Provider.ProgressChanged += ChangedProgress =>
        {
            var RawProgress = Provider.GetRaw();
            Progress = FromPercentage + (RawProgress.actual + RawProgress.skiped) * (ToPercentage - FromPercentage);
        };
                return Provider;
            }
            /// <summary>
        /// 计算总进度时的权重。它应该为预计时间（秒）。
        /// </summary>
            public double ProgressWeight { get; set; } = 1d;

            // 状态变化
            public abstract void Start(object Input = null, bool IsForceRestart = false);
            public abstract void Cancel();
            public abstract void Failed(Exception Ex);
            private CancellationTokenSource CancellationTokenSource;
            /// <summary>
        /// 创建一个随此加载器取消的取消令牌；令牌取消时也会调用此加载器的 Cancel。
        /// 这是 Loader 对新框架的兼容层。
        /// </summary>
            public CancellationToken CreateCancellationToken()
            {
                lock (LockState)
                {
                    if (CancellationTokenSource is null || CancellationTokenSource.IsCancellationRequested)
                    {
                        CancellationTokenSource = new CancellationTokenSource();
                        CancellationTokenSource.Token.Register(Cancel);
                    }
                    if (State == ModBase.LoadState.Canceled)
                        CancellationTokenSource.Cancel();
                    return CancellationTokenSource.Token;
                }
            }

            // 等待结束
            public const string WaitForExitTimeoutMessage = "等待加载器执行超时。";

            /// <summary>
            /// 无限期地等待加载器完成，直到结束或抛出异常。若加载器尚未开始，则会开始执行。
            /// </summary>
            public void WaitForExit(object Input = null, LoaderBase LoaderToSyncProgress = null, bool IsForceRestart = false, CancellationToken c = default, ProgressProvider p = null)
            {
                Start(Input, IsForceRestart);
                // 需要同步进度时每 50ms 醒来一次，否则一直等到状态改变
                int WakeInterval = LoaderToSyncProgress is null && p is null ? Timeout.Infinite : 50;
                while (State == ModBase.LoadState.Loading)
                {
                    if (LoaderToSyncProgress is not null)
                        LoaderToSyncProgress.Progress = Progress;
                    p?.Set(Progress);
                    c.ThrowIfCancellationRequested();
                    try
                    {
                        CurrentExitTask.Wait(WakeInterval, c);
                    }
                    catch (AggregateException) { } // 信号只会正常完成，不会出错
                }
                ThrowIfNotFinished(KeepOriginalError: false);
            }
            /// <summary>
            /// 等待加载器完成，直到结束、抛出异常或超时。若加载器尚未开始，则会开始执行。
            /// </summary>
            /// <param name="Timeout">等待的超时时间，以毫秒为单位。</param>
            /// <param name="TimeoutMessage">若执行超时，将会抛出的异常信息。</param>
            public void WaitForExitTime(int Timeout, object Input = null, string TimeoutMessage = WaitForExitTimeoutMessage, object LoaderToSyncProgress = null, bool IsForceRestart = false)
            {
                Start(Input, IsForceRestart);
                var Watch = System.Diagnostics.Stopwatch.StartNew();
                while (State == ModBase.LoadState.Loading)
                {
                    if (LoaderToSyncProgress is not null)
                        ((LoaderBase)LoaderToSyncProgress).Progress = Progress;
                    long Left = Timeout - Watch.ElapsedMilliseconds;
                    if (Left < 0)
                        throw new TimeoutException(TimeoutMessage);
                    CurrentExitTask.Wait((int)Math.Min(Left + 1, LoaderToSyncProgress is null ? int.MaxValue : 50));
                }
                ThrowIfNotFinished(KeepOriginalError: true);
            }
            /// <summary>
            /// 异步等待加载器完成，直到结束或抛出异常。若加载器尚未开始，则会开始执行。
            /// </summary>
            public async Task WaitForExitAsync(object Input = null, bool IsForceRestart = false, CancellationToken c = default)
            {
                Start(Input, IsForceRestart);
                while (State == ModBase.LoadState.Loading)
                {
                    await Task.WhenAny(CurrentExitTask, Task.Delay(Timeout.Infinite, c)).ConfigureAwait(false);
                    c.ThrowIfCancellationRequested();
                }
                ThrowIfNotFinished(KeepOriginalError: false);
            }
            /// <summary>
            /// 等待加载器离开加载状态，不会启动加载器。
            /// 若提供 Tick，会在等待期间每隔 TickInterval 毫秒调用一次（例如刷新进度显示）。
            /// </summary>
            public void WaitWhileLoading(int TickInterval = Timeout.Infinite, Action Tick = null, CancellationToken c = default)
            {
                while (State == ModBase.LoadState.Loading)
                {
                    Tick?.Invoke();
                    c.ThrowIfCancellationRequested();
                    try
                    {
                        CurrentExitTask.Wait(Tick is null ? Timeout.Infinite : TickInterval, c);
                    }
                    catch (AggregateException) { }
                }
            }
            /// <summary>
            /// 加载器结束后，若并非成功完成则抛出对应的异常。
            /// </summary>
            /// <param name="KeepOriginalError">直接抛出加载器的原始错误，而不是包装一层以保留调用堆栈。</param>
            private void ThrowIfNotFinished(bool KeepOriginalError)
            {
                switch (State)
                {
                    case ModBase.LoadState.Finished:
                        return;
                    case ModBase.LoadState.Canceled:
                        throw new OperationCanceledException("加载器执行已中断");
                    default:
                        if (Error is null)
                            throw new Exception("未知错误！");
                        if (KeepOriginalError)
                            throw Error;
                        throw new Exception(Error.Message, Error); // 保留调用堆栈，同时不影响信息输出与单元测试
                }
            }

            // 相同重载
            public override bool Equals(object obj)
            {
                LoaderBase @base = obj as LoaderBase;
                return @base is not null && Uuid == @base.Uuid;
            }
            public override int GetHashCode()
            {
                return Uuid;
            }

        }
        /// <summary>
    /// 用于异步执行并监控单一函数的加载器。
    /// </summary>
        /// <summary>
        /// 不依赖泛型参数访问 LoaderTask 的接口，供 LoaderCombo 等在只知道 LoaderBase 时使用。
        /// （原 VB 以晚期绑定调用 ShouldStart，并隐式处理其 ByRef 参数；C# 的 dynamic 不会这样做，会导致绑定失败）
        /// </summary>
        public interface ILoaderTask
        {
            bool ShouldStartBoxed(object Input, bool IsForceRestart = false, bool IgnoreReloadTimeout = false);
            object OutputBoxed { get; }
            object StartGetInputBoxed(object Input, Delegate InputDelegate);
        }
        public class LoaderTask<TIn, TOut> : LoaderBase, ILoaderTask
        {

            // 线程设定
            protected internal ThreadPriority ThreadPriority;
            // 执行事件（二者之一）
            protected internal Action<LoaderTask<TIn, TOut>> LoadDelegate;
            protected internal Func<LoaderTask<TIn, TOut>, Task> LoadDelegateAsync;
            protected internal Func<object> InputDelegate;

            /// <summary>
            /// 加载器的一次执行。重新开始或取消时，旧的执行会被废弃：其取消令牌被触发，同步执行的线程会被打断。
            /// </summary>
            private sealed class LoaderRun
            {
                public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
                /// <summary>同步执行时所在的线程；执行结束后为 null。</summary>
                public Thread Thread;
                public void Stop()
                {
                    try
                    {
                        Cancellation.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "触发加载器取消令牌时出错");
                    }
                    lock (this)
                        Thread?.Interrupt(); // 唤醒正在 Sleep 或等待的同步加载函数
                }
            }
            /// <summary>当前有效的执行。</summary>
            private LoaderRun ActiveRun;
            /// <summary>代码所属的执行，会随异步调用与新线程一起流动。</summary>
            private readonly AsyncLocal<LoaderRun> CurrentRun = new AsyncLocal<LoaderRun>();

            // 状态指示
            /// <summary>
            /// 当前代码所属的执行是否应当中断：已被取消，或加载器已重新开始。
            /// 在加载函数及其开启的线程、异步任务中都可使用；在其他地方调用时总是返回 True。
            /// </summary>
            public bool IsCanceled
            {
                get
                {
                    var Active = ActiveRun;
                    return Active is null || !ReferenceEquals(CurrentRun.Value, Active) || State == ModBase.LoadState.Canceled;
                }
            }
            /// <summary>
            /// 当前代码所属执行的取消令牌：在执行被取消、失败或加载器重新开始时触发。
            /// </summary>
            public CancellationToken RunToken => CurrentRun.Value?.Cancellation.Token ?? new CancellationToken(true);
            /// <summary>
            /// 在输入相同时使用原有结果的超时，单位为毫秒。
            /// </summary>
            public int ReloadTimeout = -1;
            /// <summary>
            /// 上次完成加载时的时间。
            /// </summary>
            public long LastFinishedTime = 0L;
            // 输入输出
            public TIn Input = default;
            public TOut Output = default;

            // 获取输入
            public TIn StartGetInput(TIn Input = default, Func<TIn> InputDelegate = null) // InputDelegate 参数存在匿名调用
            {
                if (InputDelegate is null)
                    InputDelegate = this.InputDelegate is null ? null : () => (TIn)this.InputDelegate();
                TIn NewInput = default; // 若 InputType 不能为 Nothing，则会导致 Input Is Nothing 永远失败，因此需要额外判断
                if ((Input is null || NewInput is not null && Input.Equals(NewInput)) && InputDelegate is not null)
                {
                    ModBase.RunInUiWait(() => Input = InputDelegate());
                }
                return Input;
            }
            // 代码执行
            public bool ShouldStartBoxed(object Input, bool IsForceRestart = false, bool IgnoreReloadTimeout = false)
            {
                TIn Value = Input is null ? default : (TIn)Input;
                return ShouldStart(ref Value, IsForceRestart, IgnoreReloadTimeout);
            }
            public object OutputBoxed => Output;
            public object StartGetInputBoxed(object Input, Delegate InputDelegate) => StartGetInput(Input is null ? default : (TIn)Input, InputDelegate is null ? null : () => (TIn)InputDelegate.DynamicInvoke());
            public bool ShouldStart(ref TIn Input, bool IsForceRestart = false, bool IgnoreReloadTimeout = false)
            {
                // 获取输入
                try
                {
                    Input = StartGetInput(Input);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"加载输入获取失败（{Name}）", LogBehavior.Toast);
                    Error = ex;
                    lock (LockState)
                        State = ModBase.LoadState.Failed;
                }
                // 检验输入以确定情况
                if (IsForceRestart)
                    return true; // 强制要求重启
                if (Input is null != this.Input is null || Input is not null && !Input.Equals(this.Input))
                    return true; // 输入不同

                if ((State == ModBase.LoadState.Loading || State == ModBase.LoadState.Finished) && (IgnoreReloadTimeout || ReloadTimeout == -1 || LastFinishedTime == 0L || ModBase.GetTimeMs() - LastFinishedTime < ReloadTimeout)) // 正在加载或已结束…
                                                                                                                                                                                                                                     // …且没有超时…
                {
                    return false; // …则不重试
                }
                else
                {
                    return true;
                } // 否则需要重启
            }
            public override void Start(object Input = null, bool IsForceRestart = false)
            {
                // 确认是否开始加载
                bool localShouldStart() { TIn argInput = Coerce.To<TIn>(Input); var ret = ShouldStart(ref argInput, IsForceRestart); Input = argInput; return ret; }

                if (localShouldStart())
                {
                    // 输入不同或失败，开始加载
                    if (State == ModBase.LoadState.Loading)
                        TriggerThreadInterrupt();
                    this.Input = Coerce.To<TIn>(Input);
                    lock (LockState)
                    {
                        State = ModBase.LoadState.Loading;
                        Progress = -1;
                    }
                }
                else
                {
                    return;
                }

                // 开始新的执行
                // 如果执行是因为判断到 IsCanceled 而提前中止，则代表已有新执行被启动，此时不应当改为 Canceled
                // 如果执行是在没有 IsCanceled 时手动引发了取消异常，则代表没有重新启动，这通常代表用户手动取消，应当改为 Canceled
                var Run = new LoaderRun();
                ActiveRun = Run; // 必须在开始执行前设置，否则执行可能在此之前就结束，导致误判 IsCanceled
                if (LoadDelegateAsync is not null)
                {
                    Task.Run(async () =>
                    {
                        CurrentRun.Value = Run;
                        OnRunStart(IsForceRestart);
                        try
                        {
                            await LoadDelegateAsync(this).ConfigureAwait(false);
                            OnRunEnd(null);
                        }
                        catch (Exception ex)
                        {
                            OnRunEnd(ex);
                        }
                    });
                }
                else
                {
                    Task.Factory.StartNew(() =>
                    {
                        CurrentRun.Value = Run;
                        var Current = Thread.CurrentThread;
                        Current.Name ??= "L/" + Name;
                        Current.Priority = ThreadPriority;
                        lock (Run)
                            Run.Thread = Current;
                        try
                        {
                            OnRunStart(IsForceRestart);
                            try
                            {
                                LoadDelegate(this);
                                OnRunEnd(null);
                            }
                            catch (Exception ex)
                            {
                                OnRunEnd(ex);
                            }
                        }
                        finally
                        {
                            lock (Run)
                                Run.Thread = null; // 之后不再打断这个线程
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }
            }
            private void OnRunStart(bool IsForceRestart)
            {
                IsForceRestarting = IsForceRestart;
                Logger.Trace(() => $"加载器 {Name} 已{(IsForceRestarting ? "强制" : "")}启动");
            }
            /// <summary>
            /// 一次执行结束时调用。Ex 为 null 表示加载函数正常返回。
            /// </summary>
            private void OnRunEnd(Exception Ex)
            {
                if (Ex is null)
                {
                    if (IsCanceled)
                    {
                        Logger.Warn($"加载器 {Name} 已中断但加载函数正常运行至结束，输出被弃用");
                        return;
                    }
                    Logger.Trace(() => $"加载器 {Name} 已完成");
                    RaisePreviewFinish();
                    State = ModBase.LoadState.Finished;
                    LastFinishedTime = ModBase.GetTimeMs();
                }
                else if (Ex.IsCanceled())
                {
                    Logger.Trace(Ex, $"加载器 {Name} 已取消，当前已完成 {Math.Round(Progress * 100d)}%");
                    if (!IsCanceled)
                        State = ModBase.LoadState.Canceled;
                }
                else
                {
                    Failed(Ex);
                }
            }
            public override void Failed(Exception ex)
            {
                Error = ex;
                lock (LockState)
                {
                    if (IsCanceled || State >= ModBase.LoadState.Finished)
                        return;
                    State = ModBase.LoadState.Failed;
                }
                Logger.Warn(ex, $"加载线程 {Name} 出错，已完成 {Math.Round(Progress * 100d)}%");
                TriggerThreadInterrupt();
            }
            public override void Cancel()
            {
                lock (LockState)
                {
                    if (State != ModBase.LoadState.Loading)
                        return;
                    State = ModBase.LoadState.Canceled;
                }
                TriggerThreadInterrupt();
            }
            /// <summary>
            /// 废弃当前的执行：触发其取消令牌，并打断同步执行的线程。
            /// </summary>
            private void TriggerThreadInterrupt()
            {
                var Run = Interlocked.Exchange(ref ActiveRun, null);
                if (Run is null)
                    return;
                Run.Stop();
                Logger.Trace(() => $"加载器 {Name} 的执行已中断");
            }

            public LoaderTask()
            {
                // 仅仅是为了避免一些智障报错（继承类必须重写 New 的情况）
            }
            public LoaderTask(string Name, Action<LoaderTask<TIn, TOut>> LoadDelegate, Func<TIn> InputDelegate = null, ThreadPriority Priority = ThreadPriority.Normal)
            {
                this.Name = Name;
                this.LoadDelegate = LoadDelegate;
                this.InputDelegate = InputDelegate is null ? null : () => InputDelegate();
                ThreadPriority = Priority;
            }
            /// <summary>
            /// 使用异步加载函数的加载器。加载函数应在等待时传入 RunToken，以便及时响应取消。
            /// </summary>
            public LoaderTask(string Name, Func<LoaderTask<TIn, TOut>, Task> LoadDelegateAsync, Func<TIn> InputDelegate = null)
            {
                this.Name = Name;
                this.LoadDelegateAsync = LoadDelegateAsync;
                this.InputDelegate = InputDelegate is null ? null : () => InputDelegate();
            }

        }

        /// <summary>
    /// 新 Worker 对老 Loader 的兼容包装类。
    /// </summary>
        public class LoaderWorker : LoaderBase
        {
            private IWorker _Worker;

            public virtual IWorker Worker
            {
                [MethodImpl(MethodImplOptions.Synchronized)]
                get
                {
                    return _Worker;
                }

                [MethodImpl(MethodImplOptions.Synchronized)]
                set
                {
                    if (_Worker != null)
                    {
                        _Worker.Succeeded -= Worker_Succeeded;
                        _Worker.Canceled -= Cancel;
                        _Worker.Failed -= Failed;
                    }

                    _Worker = value;
                    if (_Worker != null)
                    {
                        _Worker.Succeeded += Worker_Succeeded;
                        _Worker.Canceled += Cancel;
                        _Worker.Failed += Failed;
                    }
                }
            }
            public LoaderWorker(string Name, IWorker Worker)
            {
                this.Name = Name;
                this.Worker = Worker;
                Worker.Progress.ProgressChanged += Raw => Progress = Raw.actual + Raw.skiped;
            }

            public override void Start(object Input = null, bool IsForceRestart = false)
            {
                lock (LockState)
                    State = ModBase.LoadState.Loading;
                Logger.Trace(() => $"Worker {Name} 已启动");
                IsForceRestarting = IsForceRestart;
                Worker.Start();
            }
            private void Worker_Succeeded()
            {
                Logger.Trace(() => $"Worker {Name} 已完成");
                RaisePreviewFinish();
                lock (LockState)
                    State = ModBase.LoadState.Finished;
            }
            public override void Cancel()
            {
                lock (LockState)
                {
                    if (State != ModBase.LoadState.Loading)
                        return;
                    State = ModBase.LoadState.Canceled;
                }
                Logger.Trace(() => $"Worker {Name} 已取消，当前已完成 {Math.Round(Progress * 100d)}%");
                Worker.Cancel();
            }
            public override void Failed(Exception Ex)
            {
                lock (LockState)
                {
                    if (State >= ModBase.LoadState.Finished)
                        return;
                    State = ModBase.LoadState.Failed;
                }
                Logger.Warn(Ex, $"Worker {Name} 出错，已完成 {Math.Round(Progress * 100d)}%");
                Error = Ex;
                Worker.Cancel();
            }
        }

        /// <summary>
    /// 支持多个加载器连续运作的复合加载器。
    /// </summary>
        /// <summary>
        /// LoaderCombo 的非泛型视图，用于在不知道输入类型时访问子加载器与输入。
        /// </summary>
        public interface ILoaderCombo
        {
            List<LoaderBase> SubLoaders { get; }
            object InputBoxed { get; }
            List<LoaderBase> GetLoaderList(bool RequireShow = true);
        }
        public class LoaderCombo<InputType> : LoaderBase, ILoaderCombo
        {

            public override double Progress
            {
                get
                {
                    switch (State)
                    {
                        case ModBase.LoadState.Waiting:
                            {
                                return 0d;
                            }
                        case ModBase.LoadState.Loading:
                            {
                                double Total = 0d;
                                double Finished = 0d;
                                foreach (var Loader in Loaders)
                                {
                                    Total += Loader.ProgressWeight;
                                    Finished += Loader.ProgressWeight * Loader.Progress;
                                }
                                if (Total == 0d)
                                    return 0d;
                                return Finished / Total;
                            }

                        default:
                            {
                                return 1d;
                            }
                    }
                }
                set
                {
                    throw new Exception("多重加载器不支持设置进度");
                }
            }

            public List<LoaderBase> Loaders = new List<LoaderBase>();
            public InputType Input;
            List<LoaderBase> ILoaderCombo.SubLoaders => Loaders;
            object ILoaderCombo.InputBoxed => Input;
            public LoaderCombo(string Name, IEnumerable<LoaderBase> Loaders)
            {
                this.Loaders.Clear();
                foreach (LoaderBase Loader in Loaders)
                {
                    if (Loader is not null)
                    {
                        this.Loaders.Add(Loader);
                        Loader.OnStateChangedThread += SubTaskStateChanged;
                        Loader.HasOnStateChangedThread = true;
                    }
                }
                InitParent(null);
                this.Name = Name;
            }
            public override void InitParent(LoaderBase Parent)
            {
                this.Parent = Parent;
                foreach (var Loader in Loaders)
                    Loader.InitParent(this);
            }
            public override void Start(object Input = null, bool IsForceRestart = false)
            {
                IsForceRestarting = IsForceRestart;
                // 改变状态
                lock (LockState)
                {
                    if (State == ModBase.LoadState.Loading)
                    {
                        return;
                    }
                    else
                    {
                        State = ModBase.LoadState.Loading;
                    }
                }
                // 启动加载
                this.Input = Coerce.To<InputType>(Input);
                if (IsForceRestart)
                {
                    foreach (var Loader in Loaders)
                        Loader.State = ModBase.LoadState.Waiting;
                }
                ModBase.RunInThread(Update);
            }
            public override void Cancel()
            {
                // 改变状态
                lock (LockState)
                {
                    if (State == ModBase.LoadState.Loading || State == ModBase.LoadState.Waiting)
                    {
                        State = ModBase.LoadState.Canceled;
                    }
                    else
                    {
                        return;
                    }
                }
                // 中断加载器
                ModBase.RunInThread(() => { foreach (var Loader in Loaders) Loader.Cancel(); });
            }
            public override void Failed(Exception Ex)
            {
                Error = Ex; // 先设置错误再调整状态，防止父加载器获取不到异常
                lock (LockState)
                {
                    if (State >= ModBase.LoadState.Finished)
                        return;
                    State = ModBase.LoadState.Failed;
                }
                foreach (var Loader in Loaders)
                    Loader.Cancel();
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            }

            /// <summary>
        /// 子任务状态变更。
        /// </summary>
            private void SubTaskStateChanged(LoaderBase Loader, ModBase.LoadState NewState, ModBase.LoadState OldState)
            {
                switch (NewState)
                {
                    case ModBase.LoadState.Loading:
                        {
                            break;
                        }
                    // 开始，啥都不干
                    case ModBase.LoadState.Waiting:
                        {
                            break;
                        }
                    // 子加载器可能由于外部输入改变而暂时变为 Waiting，之后会立即重新启动
                    // 所以啥都不干就行
                    case ModBase.LoadState.Finished:
                        {
                            // 正常结束，触发刷新
                            Update();
                            break;
                        }
                    case ModBase.LoadState.Canceled:
                        {
                            // 被中断，这个任务也中断
                            Cancel();
                            break;
                        }
                    case ModBase.LoadState.Failed:
                        {
                            // 完蛋，出错了
                            Failed(new Exception(Loader.Name + "失败", Loader.Error));
                            break;
                        }
                }
            }
            /// <summary>
        /// 触发一次更新，以启动新加载器或完成。
        /// </summary>
            private void Update()
            {
                if (State == ModBase.LoadState.Finished || State == ModBase.LoadState.Failed || State == ModBase.LoadState.Canceled)
                    return;
                bool IsFinished = true;
                bool Blocked = false;
                object Input = this.Input;
                foreach (var Loader in Loaders)
                {
                    bool IsTaskLoader = GeneralUtils.IsGenericInstanceOf(Loader, typeof(LoaderTask<,>));
                    object TaskInput() => Input is not null && ReferenceEquals(Loader.GetType().GenericTypeArguments.First(), Input.GetType()) ? Input : null;
                    if (Loader.State == ModBase.LoadState.Finished)
                    {
                        // 已完成：输入条件变化时重启，否则把输出交给下一个加载器
                        if (IsTaskLoader && ((ILoaderTask)Loader).ShouldStartBoxed(TaskInput(), IgnoreReloadTimeout: true))
                        {
                            Logger.Info($"由于输入条件变更，重启已完成的加载器 {Loader.Name}");
                        }
                        else
                        {
                            if (IsTaskLoader)
                                Input = ((ILoaderTask)Loader).OutputBoxed;
                            // 如果不让继续启动，且已有加载器正在加载中，就不继续启动
                            if (Loader.Block && !IsFinished)
                                Blocked = true;
                            continue;
                        }
                    }
                    else if (Loader.State == ModBase.LoadState.Loading)
                    {
                        // 进行中：输入条件变化时重启，否则等待它完成
                        if (IsTaskLoader && ((ILoaderTask)Loader).ShouldStartBoxed(TaskInput(), IgnoreReloadTimeout: true))
                        {
                            Logger.Warn($"由于输入条件变更，重启进行中的加载器 {Loader.Name}");
                        }
                        else
                        {
                            IsFinished = false;
                            Blocked = true;
                            continue;
                        }
                    }
                    // 未启动或需要重启：启动加载器
                    IsFinished = false;
                    if (Blocked)
                        continue;
                    if (Input is not null)
                    {
                        // 若输入类型与下一个加载器相同才继续
                        if (IsTaskLoader || GeneralUtils.IsGenericInstanceOf(Loader, typeof(LoaderCombo<>)))
                        {
                            Loader.Start(ReferenceEquals(Loader.GetType().GenericTypeArguments.First(), Input.GetType()) ? Input : null, IsForceRestarting);
                        }
                        else if (Loader is LoaderWorker)
                        {
                            Loader.Start(IsForceRestart: IsForceRestarting);
                        }
                        else if (Loader is ModNet.LoaderDownload)
                        {
                            Loader.Start(Input is List<ModNet.NetFile> ? Input : null, IsForceRestarting);
                        }
                        else
                        {
                            throw new Exception("未知的加载器类型（" + Loader.GetType().Name + "）");
                        }
                    }
                    else
                    {
                        Loader.Start(IsForceRestart: IsForceRestarting);
                    }
                    // 阻止继续
                    if (Loader.Block)
                        Blocked = true;
                }
                if (IsFinished)
                {
                    // 顺利完成，贼棒
                    RaisePreviewFinish();
                    State = ModBase.LoadState.Finished;
                    ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                }
            }

            /// <summary>
        /// 获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        /// </summary>
            public static void GetLoaderList(object Loader, ref List<LoaderBase> List, bool RequireShow = true)
            {
                foreach (var SubLoader in ((ILoaderCombo)Loader).SubLoaders)
                {
                    if (SubLoader.Show || !RequireShow)
                        List.Add((LoaderBase)SubLoader);
                    if (GeneralUtils.IsGenericInstanceOf(SubLoader, typeof(LoaderCombo<>)))
                        GetLoaderList(SubLoader, ref List);
                }
            }
            /// <summary>
        /// 获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        /// </summary>
            public void GetLoaderList(ref List<LoaderBase> List, bool RequireShow = true)
            {
                GetLoaderList(this, ref List, RequireShow);
            }
            /// <summary>
        /// 获得最底层的，应被显示给用户的加载器列表。
        /// </summary>
            public List<LoaderBase> GetLoaderList(bool RequireShow = true)
            {
                var List = new List<LoaderBase>();
                GetLoaderList(ref List, RequireShow);
                return List;
            }

        }

        // 任务栏进度条
        public static ConcurrentList<LoaderBase> LoaderTaskbar = new ConcurrentList<LoaderBase>();
        public static double LoaderTaskbarProgress = 0d; // 平滑后的进度
        private static System.Windows.Shell.TaskbarItemProgressState LoaderTaskbarProgressLast = System.Windows.Shell.TaskbarItemProgressState.None;

        public static void LoaderTaskbarAdd<T>(LoaderCombo<T> Loader)
        {
            if (ModMain.FrmSpeedLeft is not null)
                ModMain.FrmSpeedLeft.TaskRemove(Loader);
            LoaderTaskbar.Add(Loader);
            Logger.Info($"{Loader.Name} 已加入任务列表");
        }
        public static void LoaderTaskbarProgressRefresh()
        {
            try
            {
                System.Windows.Shell.TaskbarItemProgressState NewState;
                double NewProgress = LoaderTaskbarProgressGet();
                // 若单个任务已中止，或全部任务已完成，则刷新并移除
                foreach (var Task in LoaderTaskbar)
                {
                    if (LoaderTaskbar.All(l => l.State != ModBase.LoadState.Loading) || Task.State == ModBase.LoadState.Waiting || Task.State == ModBase.LoadState.Canceled)
                    {
                        ModMain.FrmSpeedLeft?.TaskRefresh(Task);
                        LoaderTaskbar.Remove(Task);
                        Logger.Info($"{Task.Name} 已移出任务列表");
                    }
                }
                // 更新平滑后的进度
                if (NewProgress <= 0d || NewProgress >= 1d || LoaderTaskbarProgress > NewProgress)
                {
                    LoaderTaskbarProgress = NewProgress;
                }
                else
                {
                    LoaderTaskbarProgress = LoaderTaskbarProgress * 0.9d + NewProgress * 0.1d;
                }
                ModBase.RunInUi(() => ModMain.FrmMain.BtnExtraDownload.Progress = LoaderTaskbarProgress);
                // 更新任务栏信息
                if (!LoaderTaskbar.Any() || LoaderTaskbarProgress == 1d)
                {
                    NewState = System.Windows.Shell.TaskbarItemProgressState.None;
                }
                else if (LoaderTaskbarProgress < 0.015d)
                {
                    NewState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
                }
                else
                {
                    NewState = System.Windows.Shell.TaskbarItemProgressState.Normal;
                    ModMain.FrmMain.TaskbarItemInfo.ProgressValue = LoaderTaskbarProgress;
                }
                if (LoaderTaskbarProgressLast != NewState)
                {
                    LoaderTaskbarProgressLast = NewState;
                    ModMain.FrmMain.TaskbarItemInfo.ProgressState = NewState;
                    ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "刷新任务栏进度显示失败");
            }
        }
        public static double LoaderTaskbarProgressGet()
        {
            try
            {
                if (!LoaderTaskbar.Any())
                    return 1d;
                return LoaderTaskbar.Select(l => l.Progress).Average().Clamp(0d, 1d);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "获取任务栏进度出错");
                return 0.5d;
            }
        }

        // 文件夹刷新类委托
        private static Dictionary<LoaderBase, LoaderFolderDictionaryEntry> LoaderFolderDictionary = new Dictionary<LoaderBase, LoaderFolderDictionaryEntry>();
        private struct LoaderFolderDictionaryEntry
        {
            public DateTime? LastCheckTime;
            public string FolderPath;
            public override bool Equals(object obj)
            {
                if (!(obj is LoaderFolderDictionaryEntry))
                    return false;
                LoaderFolderDictionaryEntry entry = (LoaderFolderDictionaryEntry)obj;
                return EqualityComparer<DateTime?>.Default.Equals(LastCheckTime, entry.LastCheckTime) && (FolderPath ?? "") == (entry.FolderPath ?? "");
            }
            public override int GetHashCode()
            {
                return (LastCheckTime, FolderPath ?? "").GetHashCode();
            }
        }
        public enum LoaderFolderRunType
        {
            RunOnUpdated,
            ForceRun,
            UpdateOnly
        }
        /// <summary>
    /// 执行以文件夹检测作为输入的加载器。加载器需以文件夹路径为输入值。
    /// 返回是否执行了加载器。
    /// </summary>
    /// <param name="ExtraPath">用于检查文件夹修改的额外路径。该路径不会传入加载器。</param>
        public static bool LoaderFolderRun(LoaderBase Loader, string FolderPath, LoaderFolderRunType Type, int MaxDepth = 0, string ExtraPath = "", bool WaitForExit = false)
        {
            var Value = new LoaderFolderDictionaryEntry() { FolderPath = FolderPath + ExtraPath, LastCheckTime = default };
            try
            {
                // 获取数据
                var FolderInfo = DirectoryUtils.GetInfo(FolderPath + ExtraPath);
                Value.LastCheckTime = FolderInfo.Exists ? global::PCL.ModLoader.GetActualLastWriteTimeUtc(FolderInfo, MaxDepth) : null;
                // 如果已经检查过，则跳过
                if (Type == LoaderFolderRunType.RunOnUpdated && LoaderFolderDictionary.ContainsKey(Loader))
                {
                    if (FolderInfo.Exists)
                    {
                        if (LoaderFolderDictionary[Loader].LastCheckTime is not null && Value.Equals(LoaderFolderDictionary[Loader]))
                            return false;
                    }
                    else if (LoaderFolderDictionary[Loader].LastCheckTime is null)
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "文件夹加载器启动检测出错");
            }
            // 写入检查数据
            LoaderFolderDictionary[Loader] = Value;
            // 开始检查
            if (Type == LoaderFolderRunType.UpdateOnly)
                return false;
            if (WaitForExit)
            {
                Loader.WaitForExit(FolderPath, IsForceRestart: true);
            }
            else
            {
                Loader.Start(FolderPath, IsForceRestart: true);
            }
            return true;
        }
        private static DateTime GetActualLastWriteTimeUtc(DirectoryInfo FolderInfo, int MaxDepth)
        {
            var Time = FolderInfo.LastWriteTimeUtc;
            if (MaxDepth > 0)
            {
                foreach (var Folder in FolderInfo.EnumerateDirectories())
                {
                    var FolderTime = GetActualLastWriteTimeUtc(Folder, MaxDepth - 1);
                    if (FolderTime > Time)
                        Time = FolderTime;
                }
            }
            return Time;
        }

    }
}