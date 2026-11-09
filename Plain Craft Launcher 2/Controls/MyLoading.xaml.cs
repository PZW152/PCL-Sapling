using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MeloongCore.Extensions;
using Microsoft.VisualBasic.CompilerServices;
using static PCL.MyLoading;

namespace PCL
{

    public partial class MyLoading
    {

        public event IsErrorChangedEventHandler IsErrorChanged;

        public delegate void IsErrorChangedEventHandler(object sender, bool isError);
        public event StateChangedEventHandler StateChanged;

        public delegate void StateChangedEventHandler(object sender, MyLoadingState newState, MyLoadingState oldState);
        public event ClickEventHandler Click;

        public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

        public bool AutoRun { get; set; } = true;
        private int Uuid = ModBase.GetUuid();

        #region 颜色

        public SolidColorBrush Foreground
        {
            get
            {
                return (SolidColorBrush)this.GetValue(ForegroundProperty);
            }
            set
            {
                this.SetValue(ForegroundProperty, value);
            }
        }
        public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register("Foreground", typeof(SolidColorBrush), typeof(MyLoading));
        public MyLoading()
        {
            this.InitializeComponent();
            this.SetResourceReference(ForegroundProperty, "ColorBrush3");
            StateProgressChanged = (_, __) => RefreshText();
            StateLoadingStateChanged = (_, __) => RefreshState();
            IsErrorChanged += (_, __) => RefreshText();
            this.Loaded += (_, __) => RefreshText();
            this.Loaded += (_, __) => InitState();
            this.Loaded += (_, __) => RefreshState();
            this.Unloaded += (_, __) => RefreshState();
            this.MouseLeftButtonUp += Button_MouseUp;
        }

        #endregion

        #region 文本

        private bool _ShowProgress { get; set; } = false;
        public bool ShowProgress
        {
            get
            {
                return _ShowProgress;
            }
            set
            {
                if (_ShowProgress == value)
                    return;
                _ShowProgress = value;
                RefreshText();
            }
        }

        private string _Text = "加载中";
        public string Text
        {
            get
            {
                return _Text;
            }
            set
            {
                _Text = value;
                RefreshText();
            }
        }

        private string _TextError = "加载失败";
        public string TextError
        {
            get
            {
                return _TextError;
            }
            set
            {
                _TextError = value;
                RefreshText();
            }
        }
        /// <summary>
    /// 是否在使用 Loader 时使用 Loader 的错误输出来替换默认的错误文本显示。
    /// </summary>
        public bool TextErrorInherit { get; set; } = true;

        private void RefreshText()
        {
            ModBase.RunInUi(() => { if (InnerState == MyLoadingState.Error) { if (TextErrorInherit && State.IsLoader) { Exception Ex = ((ModLoader.LoaderBase)State).Error; if (Ex is null) { this.LabText.Text = "未知错误"; } else { this.LabText.Text = Ex.GetDisplay(false); } } else { this.LabText.Text = TextError; } } else if (ShowProgress && State.IsLoader) { this.LabText.Text = $"{Text} - {Math.Floor(((ModLoader.LoaderBase)State).Progress * 100)}%"; } else { this.LabText.Text = Text; } });
        }

        #endregion

        #region 状态改变

        // 状态枚举
        public enum MyLoadingState
        {
            Unloaded = -1,
            Run = 0,
            Stop = 1,
            Error = 2
        }

        // 用于外部改变的公开状态
        private ILoadingTrigger __State;
        // 必须保存委托实例：若在 -= 时新建 Lambda，旧的处理器永远不会解绑，
        // 被替换掉的加载器会一直持有本控件，且每次进度变化都重复刷新
        private readonly ILoadingTrigger.ProgressChangedEventHandler StateProgressChanged;
        private readonly ILoadingTrigger.LoadingStateChangedEventHandler StateLoadingStateChanged;

        private ILoadingTrigger _State
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return __State;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (__State != null)
                {
                    __State.ProgressChanged -= StateProgressChanged;
                    __State.LoadingStateChanged -= StateLoadingStateChanged;
                }

                __State = value;
                if (__State != null)
                {
                    __State.ProgressChanged += StateProgressChanged;
                    __State.LoadingStateChanged += StateLoadingStateChanged;
                }
            }
        }
        public ILoadingTrigger State
        {
            get
            {
                InitState();
                return _State;
            }
            set
            {
                _State = value;
                RefreshState();
            }
        }
        private void InitState()
        {
            if (_State is null)
            {
                _State = new MyLoadingStateSimulator();
                if (AutoRun)
                    _State.LoadingState = MyLoadingState.Run;
            }
        }
        private void RefreshState()
        {
            if (_State.LoadingState == MyLoadingState.Run && !this.IsLoaded)
                InnerState = MyLoadingState.Stop;
            InnerState = _State.LoadingState;
            OuterState = _State.LoadingState;
            AniLoop();
        }

        // 用于引发外部事件的状态
        private MyLoadingState _OuterState { get; set; } = MyLoadingState.Unloaded;
        private MyLoadingState OuterState
        {
            get
            {
                return _OuterState;
            }
            set
            {
                if (_OuterState == value)
                    return;
                var OldValue = _OuterState;
                _OuterState = value;
                // 引发事件
                StateChanged?.Invoke(this, value, OldValue);
                if (OldValue == MyLoadingState.Error != (value == MyLoadingState.Error))
                    IsErrorChanged?.Invoke(this, value == MyLoadingState.Error);
            }
        }


        // 用于引发内部动画事件的状态
        private MyLoadingState _InnerState { get; set; } = MyLoadingState.Unloaded;
        private MyLoadingState InnerState
        {
            get
            {
                return _InnerState;
            }
            set
            {
                if (_InnerState == value)
                    return;
                var OldValue = _InnerState;
                _InnerState = value;
                // 引发事件
                AniLoop();
                if (OldValue == MyLoadingState.Error != (value == MyLoadingState.Error))
                    ErrorAnimation(this, value == MyLoadingState.Error);
            }
        }

        #endregion

        #region 动画

        /// <summary>
    /// 是否需要动画。
    /// </summary>
        public bool HasAnimation { get; set; } = true;

        /// <summary>
    /// 主动画循环是否正在运行中。
    /// </summary>
        private bool IsLooping = false;
        private void AniLoop()
        {
            // 这坨循环代码也是老屎坑了，救救.jpg
            if (!HasAnimation || IsLooping || !(InnerState == MyLoadingState.Run) || ModAnimation.AniSpeed > 10d || !this.IsLoaded)
                return;
            IsLooping = true;
            ErrorAnimationWaiting = true;
            ModAnimation.AniStart(new[] {
            ModAnimation.AaRotateTransform(this.PathPickaxe, (double)-20 - ((RotateTransform)this.PathPickaxe.RenderTransform).Angle, 350, 250, new ModAnimation.AniEaseInBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaRotateTransform(this.PathPickaxe, 50d, 900, Ease: new ModAnimation.AniEaseOutFluent(), After: true),
            ModAnimation.AaRotateTransform(this.PathPickaxe, 25d, 900, Ease: new ModAnimation.AniEaseOutElastic(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaCode(() =>
                {
                this.PathLeft.Opacity = 1d;
                this.PathLeft.Margin = new Thickness(7d, 41d, 0d, 0d);
                this.PathRight.Opacity = 1d;
                this.PathRight.Margin = new Thickness(14d, 41d, 0d, 0d);
                ErrorAnimationWaiting = false;
            }),
            ModAnimation.AaOpacity(this.PathLeft, (double)-1, 100, 50),
            ModAnimation.AaX(this.PathLeft, (double)-5, 180, Ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaY(this.PathLeft, (double)-6, 180, Ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(this.PathRight, (double)-1, 100, 50),
            ModAnimation.AaX(this.PathRight, 5d, 180, Ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaY(this.PathRight, (double)-6, 180, Ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaCode(() =>
                {
                IsLooping = false;
                AniLoop();
            }, After: true)
        }, "MyLoader Loop " + Uuid + "/" + ModBase.GetUuid());
        }

        /// <summary>
    /// 镐子是否还没挥下去，要求错误动画等待。
    /// </summary>
        private bool ErrorAnimationWaiting = false;
        private void ErrorAnimation(object sender, bool isError)
        {
            if (isError)
            {
                // 非错误变为错误
                int Wait = ErrorAnimationWaiting ? 400 : 0;
                ModAnimation.AniStart(new[] { ModAnimation.AaColor(this.PanBack, ForegroundProperty, "ColorBrushRedLight", 300), ModAnimation.AaOpacity(this.PathError, 1d - this.PathError.Opacity, 100, 300 + Wait), ModAnimation.AaScaleTransform(this.PathError, 1d - ((ScaleTransform)this.PathError.RenderTransform).ScaleX, 400, 300 + Wait, new ModAnimation.AniEaseOutBack()) }, "MyLoader Error " + Uuid);
            }
            else
            {
                // 错误变为非错误
                ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this.PathError, -this.PathError.Opacity, 100), ModAnimation.AaScaleTransform(this.PathError, 0.5d - ((ScaleTransform)this.PathError.RenderTransform).ScaleX, 200), ModAnimation.AaColor(this.PanBack, ForegroundProperty, "ColorBrush3", 300) }, "MyLoader Error " + Uuid);
            }
        }

        #endregion

        #region 点击事件

        private void Button_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Click?.Invoke(sender, e);
        }

        #endregion

    }

    public interface ILoadingTrigger
    {
        bool IsLoader { get; }
        MyLoadingState LoadingState { get; set; }
        event LoadingStateChangedEventHandler LoadingStateChanged;

        delegate void LoadingStateChangedEventHandler(MyLoadingState NewState, MyLoadingState OldState);
        event ProgressChangedEventHandler ProgressChanged;

        delegate void ProgressChangedEventHandler(double NewProgress, double OldProgress);
    }

    public class MyLoadingStateSimulator : ILoadingTrigger
    {
        private MyLoadingState _LoadingState { get; set; } = MyLoadingState.Unloaded;
        public MyLoadingState LoadingState
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
        public bool IsLoader { get; private set; } = false;

        public event ILoadingTrigger.LoadingStateChangedEventHandler LoadingStateChanged;
#pragma warning disable CS0067 // 由 ILoadingTrigger 规定，但模拟器没有进度概念，故永不引发
        public event ILoadingTrigger.ProgressChangedEventHandler ProgressChanged;
#pragma warning restore CS0067
    }
}