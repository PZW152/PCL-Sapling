using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public partial class Application
    {

        // 开始
        /// <summary>
        /// 保留系统的区域格式，但把历法换成公历。
        /// </summary>
        private static void UseGregorianCalendar()
        {
            var Culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone();
            if (Culture.DateTimeFormat.Calendar is System.Globalization.GregorianCalendar)
                return;
            var Gregorian = Culture.OptionalCalendars.OfType<System.Globalization.GregorianCalendar>().FirstOrDefault() ?? new System.Globalization.GregorianCalendar();
            Culture.DateTimeFormat.Calendar = Gregorian;
            System.Globalization.CultureInfo.CurrentCulture = Culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = Culture;
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // 日期一律使用公历：系统使用民国纪年等其他历法时，日期会显示成“115/09/15”
                UseGregorianCalendar();
                // 核心初始化
                MeloongCore.Main.Init("PCL");
                MeloongCore.Wpf.Main.Init();
                if (!(e.Args.Contains("--update") || e.Args.Contains("--gpu") || e.Args.Contains("--memory"))) // 若是通过命令行调用，则不检查单例、不轮换日志（#9137）
                {
                    ModShortcut.AcceptCommandLine(e.Args); // 快捷方式的启动请求：若启动器已打开，由已打开的启动器处理
                    WaitingMutex(e);
                    Logger.Instance = new PclLogger() { logFolder = Paths.BaseThenName, MinLevel = ModBase.ModeDebug ? LogLevel.Trace : LogLevel.Info };
                }
                // 提升主线程优先级
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                // 执行开发版测试
                if (ModBase.BuildType == ModBase.BuildTypes.Debug)
                {
                    try
                    {
                        ModDevelop.Start();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "开发者模式测试出错", LogBehavior.Alert);
                    }
                }
                // 检查 .NET Framework 版本
                try
                {
                    if (Coerce.ToInt(RegistryUtils.TryRead(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", "Release", 999999)) < 528040)
                        OldEnvironmentAssert();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "检查 .NET Framework 版本失败");
                }
                // 修复 WPF 字体加载（#3467）
                try
                {
                    var _unused = new FormattedText("", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First(), 96d, new ModBase.MyColor(), ModBase.DPI);
                }
                catch (UriFormatException)
                {
                    Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVariableTarget.User);
                    var _unused = new FormattedText("", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First(), 96d, new ModBase.MyColor(), ModBase.DPI);
                }
                // 检查参数调用
                if (e.Args.Length > 0)
                {
                    if (e.Args[0] == "--gpu")
                    {
                        // 调整显卡设置
                        try
                        {
                            ModMain.SetGPUPreference(e.Args[1].Trim('"'));
                            Environment.Exit((int)ModBase.ProcessReturnValues.TaskDone);
                        }
                        catch (Exception)
                        {
                            Environment.Exit((int)ModBase.ProcessReturnValues.Fail);
                        }
                    }
                }
                // 初始化文件结构
                try
                {
                    DirectoryUtils.Create(Paths.Base + @"PCL\");
                    ModBase.CheckPermissionWithException(Paths.Base + @"PCL\");
                }
                catch (Exception)
                {
                    MessageBox.Show($@"PCL 没有对当前文件夹的权限（{Paths.Base}PCL\），请尝试：" + ModBase.CrLf + "1. 将 PCL 移动到其他文件夹" + (PathUtils.GetDiskName(Paths.Base) is { } arg1 && arg1 == 'C' ? "，例如 C 盘和桌面以外的其他位置。" : "。") + ModBase.CrLf + "2. 删除当前目录中的 PCL 文件夹，然后再试。" + ModBase.CrLf + "3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。", "运行环境错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit((int)ModBase.ProcessReturnValues.Cancel);
                }

                while (true)
                {
                    try
                    {
                        DirectoryUtils.Create(ModBase.PathTemp);
                        ModBase.CheckPermissionWithException(ModBase.PathTemp);
                        break;
                    }
                    catch (Exception ex)
                    {
                        ModMain.MyMsgBox("PCL 无法访问缓存文件夹，可能导致程序出错或无法正常使用！" + ModBase.CrLf + ModBase.CrLf + "错误原因：" + ex.GetDisplay(true), "缓存文件夹不可用");
                        break;
                    }
                }
                DirectoryUtils.Create(ModBase.PathTemp + @"Cache\");
                DirectoryUtils.Create(Paths.AppDataThenName);
                // 设置 ToolTipService 默认值
                ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(100));
                // 设置网络配置默认值
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.DefaultConnectionLimit = 10000;
                ServicePointManager.UseNagleAlgorithm = false;
                ServicePointManager.EnableDnsRoundRobin = true;
                ServicePointManager.ReusePort = true;
                if (Environment.OSVersion.Version < new Version(6, 2)) // Windows 7 回退（#8519）
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                }
                // 界面语言（必须在任何窗口显示之前决定）
                ModLanguage.Initialize();
                // 设置初始窗口
                ModMain.FrmStart = new SplashScreen(@"Images\icon.ico");
                ModMain.FrmStart.Show(false, true);
                // 基础信息
                Logger.Info($"程序版本：{ModBase.VersionDisplay} ({ModBase.VersionCode}{(string.IsNullOrEmpty(ModBase.CommitHash) ? "" : $"，#{ModBase.CommitHash}")})");
                Logger.Info($"识别码：{ModBase.Identify}");
                Logger.Info($"程序路径：{ModBase.PathExe}");
                Logger.Info($"系统编码：{Encoding.Default.HeaderName} ({Encoding.Default.CodePage}, GBK={ModBase.IsGBKEncoding})");
                Logger.Info($"管理员权限：{WindowsUtils.HasAdminRole()}");
                // 检测异常环境
                if (Paths.Base.Contains(Path.GetTempPath()) || Paths.Base.Contains(@"AppData\Local\Temp\"))
                {
                    ModMain.MyMsgBox("请将 PCL 从压缩包中解压后再使用！" + ModBase.CrLf + "如果不会解压，可以在网上寻找教程。", "需要解压！", "我知道了", IsWarn: true, ForceWait: true);
                    FormMain.EndProgramForce(ModBase.ProcessReturnValues.Cancel);
                }
                if (!Environment.Is64BitOperatingSystem)
                {
                    ModMain.MyMsgBox("PCL 和新版 Minecraft 均不再支持 32 位系统，请重装为 64 位系统后再进行游戏！", "环境警告", "我知道了", IsWarn: true, ForceWait: true);
                    FormMain.EndProgramForce(ModBase.ProcessReturnValues.Cancel);
                }
                // 计时
                Logger.Info($"第一阶段加载用时：{ModBase.GetTimeMs() - ModBase.ApplicationStartTick} ms");
                ModBase.ApplicationStartTick = ModBase.GetTimeMs();
                ModAnimation.AniControlEnabled += 1;
            }
            catch (Exception ex)
            {
                string FilePath = null;
                try
                {
                    FilePath = ModBase.PathExe;
                }
                catch
                {
                }
                MessageBox.Show(ex.GetDisplay(true) + ModBase.CrLf + "PCL 所在路径：" + (string.IsNullOrEmpty(FilePath) ? "获取失败" : FilePath), "PCL 初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FormMain.EndProgramForce(ModBase.ProcessReturnValues.Exception);
            }
        }
        /// <summary>
    /// 要求程序以单例运行。
    /// 如果已在运行，则将其窗口拖出来并播放提示音，然后结束当前进程。
    /// </summary>
        private static void WaitingMutex(StartupEventArgs e)
        {
            if (ModBase.BuildType == ModBase.BuildTypes.Debug)
                return;
            try
            {
                bool MutexCreatedNew;
                // 与原版 PCL 分开，两者可以同时打开；开发中的版本也与正式版分开，可以和正在使用的正式版同时运行
                PclMutex = new Mutex(true, "PCL_Sapling_SingletonMutex" + (ModBase.BuildType == ModBase.BuildTypes.Release ? "" : "_" + ModBase.BuildType), out MutexCreatedNew);
                if (MutexCreatedNew)
                    return;
                Logger.Warn("已有一个 PCL 实例正在运行");
                // 等待已有的 PCL 退出
                if (e.Args.Length > 0 && e.Args[0] == "--wait")
                {
                    try
                    {
                        if (PclMutex.WaitOne(10000))
                            return; // 等待至多 10 秒
                    }
                    catch (AbandonedMutexException)
                    {
                        return; // 已有实例异常退出，继续启动
                    }
                }
                // 将已有的 PCL 窗口拖出来
                var WindowHwnd = ModMain.FindWindow(null, ModBase.WindowTitle);
                if (WindowHwnd != IntPtr.Zero)
                    ModMain.ShowWindowToTop(WindowHwnd);
                // 播放提示音并退出
                System.Media.SystemSounds.Beep.Play();
                Environment.Exit((int)ModBase.ProcessReturnValues.Cancel);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "单例检查失败");
            }
        }
        private static Mutex PclMutex;

        // 结束
        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            ModMain.FrmMain?.EndProgram(false);
        }

        // 异常
        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ;
            // 触发页面的 Dispatcher
            // 原 VB 為 On Error Resume Next：出錯即跳過該語句
            try { if (ModMain.FrmMain?.PageLeft is IDispatcherUnhandledException LeftPage) LeftPage.DispatcherUnhandledException(sender, e); } catch { }
            if (e.Handled)
                return;
            try { if (ModMain.FrmMain?.PageRight is IDispatcherUnhandledException RightPage) RightPage.DispatcherUnhandledException(sender, e); } catch { }
            if (e.Handled)
                return;
            // 正常处理
            e.Handled = true;
            if (ModBase.IsProgramEnding)
                return;
            try { ModMain.FeedbackInfo(); } catch { }
            string Detail = "";
            try { Detail = e.Exception.GetDisplay(true); } catch { }
            if (Detail.Contains("System.Windows.Threading.Dispatcher.Invoke") || Detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") || Detail.Contains("未能加载文件或程序集") || Detail.Contains(".NET Framework")) // “自动错误判断” 的结果分析
            {
                OldEnvironmentAssert(e.Exception);
            }
            else
            {
                Logger.Error(e.Exception, "程序出现未知错误", LogBehavior.AlertThenCrash);
            }
        }
        private static void OldEnvironmentAssert(Exception ex = null)
        {
            ModBase.OpenWebsite("https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/thank-you/net48-offline-installer");
            if (ex is null)
            {
                Logger.Error($"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{ModBase.CrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash);
            }
            else
            {
                Logger.Error(ex, $"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{ModBase.CrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash);
            }
        }
        private string[] _Prefixes = new[] { "Newtonsoft.Json", "Ookii.Dialogs.Wpf", "Imazen.WebP", "ThrottleDebounce", "Microsoft.Win32.Registry", "PCLCS", "MeloongCore.Wpf", "MeloongCore" };
        private ConcurrentDictionary<string, Lazy<Assembly>> _LoadedAssemblies = new ConcurrentDictionary<string, Lazy<Assembly>>(StringComparer.Ordinal); // 缓存

        // 动态 DLL 加载
        private Application() // 这里必须尽早调用，且不能使用任何库，否则加载 MeloongCore 就会导致崩溃
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, Args) =>
        {
            string Prefix = _Prefixes.FirstOrDefault(p => Args.Name.StartsWith(p, StringComparison.Ordinal));
            if (Prefix is null)
                return null;
            var LazyAssembly = _LoadedAssemblies.GetOrAdd(Prefix, p => new Lazy<Assembly>(() =>
        {
                var LoadedAssembly = Assembly.Load((byte[])My.Resources.Resources.ResourceManager.GetObject(p));
                if (p == "Imazen.WebP")
                    ExtractLibwebp(); // WebP 特判
                return LoadedAssembly;
            }, LazyThreadSafetyMode.ExecutionAndPublication));
            return LazyAssembly.Value;
        };
            Startup += Application_Startup;
            SessionEnding += Application_SessionEnding;
            DispatcherUnhandledException += Application_DispatcherUnhandledException;
        }
        private static void ExtractLibwebp() // 这个方法会调用 ModBase，进而调用 MeloongCore，所以不能放在 New 里
        {
            string arglpPathName = PathUtils.RemoveSlashSuffix(ModBase.PathPure.Value);
            Application.SetDllDirectory(arglpPathName);
            try
            {
                ModBase.ExtractResources(ModBase.PathPure.Value + "libwebp.dll", "libwebp64");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "写入 libwebp.dll 失败"); // 防止同时加载多个图片时，同时写入文件导致文件占用，进而导致崩溃
            }
        }
        [DllImport("kernel32", EntryPoint = "SetDllDirectoryA")]
        private static extern bool SetDllDirectory(string lpPathName);

        // 控件模板事件
        private void MyIconButton_Click(object sender, EventArgs e)
        {
            switch (Settings.Get<ModLaunch.McLoginType>("LoginType"))
            {
                case ModLaunch.McLoginType.Ms:
                    {
                        // 微软
                        JObject MsJson = (JObject)Settings.Get<string>("LoginMsJson").DeserializeJson();
                        MsJson.Remove(Coerce.ToStr(sender.Fe().Tag));
                        Settings.Set("LoginMsJson", MsJson.ToString(Formatting.None));
                        if (object.ReferenceEquals(ModMain.FrmLoginMs.ComboAccounts.SelectedItem, sender.ParentFe()))
                            ModMain.FrmLoginMs.ComboAccounts.SelectedIndex = 0;
                        ModMain.FrmLoginMs.ComboAccounts.Items.Remove(sender.ParentFe());
                        break;
                    }
                case ModLaunch.McLoginType.Legacy:
                    {
                        // 离线
                        var Names = new List<string>();
                        Names.AddRange(Settings.Get<string>("LoginLegacyName").ToString().Split("¨"));
                        Names.Remove(Coerce.ToStr(sender.Fe().Tag));
                        Settings.Set("LoginLegacyName", Names.Join('¨'));
                        ModMain.FrmLoginLegacy.ComboName.ItemsSource = Names;
                        ModMain.FrmLoginLegacy.ComboName.Text = Names.Any() ? Names[0] : "";
                        break;
                    }
            }
        }

    }
}