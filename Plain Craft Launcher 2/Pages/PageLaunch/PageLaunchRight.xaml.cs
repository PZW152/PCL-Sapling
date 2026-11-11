using System;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using MeloongCore;
using MeloongCore.Extensions;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public partial class PageLaunchRight : IRefreshable, IDispatcherUnhandledException
    {
        public PageLaunchRight()
        {
            OnlineLoader = new ModLoader.LoaderTask<(string Address, bool ShouldRefresh), int>("下载主页", OnlineLoaderSub) { ReloadTimeout = 10 * 60 * 1000 };
            this.Loaded += (_, __) => Init();
            this.Loaded += (_, __) => Refresh();
            this.InitializeComponent();
        }

        private void Init()
        {
            this.PanBack.ScrollToHome();
            this.PanLog.Visibility = ModBase.ModeDebug.ToVisibility();
            // 快照版提示
            this.PanHint.Visibility = (ModBase.BuildType != ModBase.BuildTypes.Release && ModSecret.VersionBranchMain == "Official" && !Settings.Get<bool>("HintSnapshot")).ToVisibility();
        }

        #region 主页

        /// <summary>
    /// 刷新主页。
    /// </summary>
        private void Refresh()
        {
            ModBase.RunInThread(() => { try { lock (RefreshLock) RefreshReal(); } catch (Exception ex) { Logger.Error(ex, "加载 PCL 主页自定义信息失败", ModBase.ModeDebug ? LogBehavior.Alert : LogBehavior.Toast); } });
        }
        private void RefreshReal()
        {
            string Content = null;
            string Url = null;
            switch (Settings.Get<int>("UiCustomType"))
            {
                case 1:
                    {
                        // 加载本地文件
                        Logger.Info("主页自定义数据来源：本地文件");
                        Content = FileUtils.TryReadAsString(Paths.Base + @"PCL\Custom.xaml");
                        break;
                    }
                case 2:
                    {
                        // 联网下载
                        Url = Settings.Get<string>("UiCustomNet");
                        break;
                    }
            }
            // 联网下载
            if (!string.IsNullOrWhiteSpace(Url))
            {
                // 替换自定义变量与设置
                Url = ModMain.ArgumentReplace(Url, WebUtility.HtmlEncode);
                if ((Url ?? "") == (Settings.Get<string>("CacheSavedPageUrl") ?? "") && FileUtils.Exists(ModBase.PathTemp + @"Cache\Custom.xaml"))
                {
                    // 缓存可用
                    Logger.Info("主页自定义数据来源：联网缓存文件");
                    Content = FileUtils.TryReadAsString(ModBase.PathTemp + @"Cache\Custom.xaml");
                    // 后台更新缓存
                    OnlineLoader.Start((Url, false));
                }
                else
                {
                    // 缓存不可用
                    Logger.Info("主页自定义数据来源：联网全新下载");
                    ModMain.Hint("正在加载主页……");
                    ModBase.RunInUiWait(() => LoadContent(null)); // 在加载结束前清空页面
                    Settings.Set("CacheSavedPageVersion", "");
                    OnlineLoader.Start((Url, true)); // 下载完成后将会再次触发更新
                    return;
                }
            }
            // 同步到 UI
            ModBase.RunInUi(() => LoadContent(Content));
        }
        private object RefreshLock = new object();

        // 联网获取主页文件
        private ModLoader.LoaderTask<(string Address, bool ShouldRefresh), int> OnlineLoader;
        private void OnlineLoaderSub(ModLoader.LoaderTask<(string Address, bool ShouldRefresh), int> Task)
        {
            string Address = Task.Input.Address; // #3721 中连续触发两次导致内容变化
            bool ShouldRefresh = Task.Input.ShouldRefresh;
            try
            {
                // 获取版本校验地址
                string VersionAddress;
                if (Address.Contains(".xaml"))
                {
                    VersionAddress = Address.Replace(".xaml", ".xaml.ini");
                }
                else
                {
                    VersionAddress = Address.BeforeFirst("?");
                    if (!VersionAddress.EndsWithF("/"))
                        VersionAddress += "/";
                    VersionAddress += "version";
                    if (Address.Contains("?"))
                        VersionAddress += "?" + Address.AfterFirst("?");
                }
                // 校验版本
                string Version = "";
                try
                {
                    Version = ModNet.NetRequestByClientRetry(VersionAddress);
                    if (Version.Length > 1000)
                        throw new Exception($"获取的主页版本过长（{Version.Length} 字符）");
                    string CurrentVersion = Settings.Get<string>("CacheSavedPageVersion");
                    if (!string.IsNullOrEmpty(Version) && !string.IsNullOrEmpty(CurrentVersion) && (Version ?? "") == (CurrentVersion ?? ""))
                    {
                        Logger.Info($"当前缓存的主页已为最新，当前版本：{Version}，检查源：{VersionAddress}");
                        return;
                    }
                    Logger.Info($"需要下载联网主页，当前版本：{Version}，检查源：{VersionAddress}");
                }
                catch (Exception exx)
                {
                    Logger.Warn(exx, $"联网获取主页版本失败");
                    Logger.Info($"无法检查联网主页版本，将直接下载，检查源：{VersionAddress}");
                }
                // 实际下载
                string FileContent = ModNet.NetRequestByClientRetry(Address);
                Logger.Info($"已联网下载主页，内容长度：{FileContent.Length}，来源：{Address}");
                Settings.Set("CacheSavedPageUrl", Address);
                Settings.Set("CacheSavedPageVersion", Version);
                FileUtils.Write(ModBase.PathTemp + @"Cache\Custom.xaml", FileContent);
                // 若内容变更则要求刷新
                if (((FileContent.GetStableHashCode() is var arg1 && LoadedContentHash.HasValue ? LoadedContentHash.Value != arg1 : (bool?)null) is var arg2 && !arg2.HasValue || arg2.Value) && ShouldRefresh && arg2.HasValue)
                    Refresh();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"下载主页失败（{Address}）", ModBase.ModeDebug ? LogBehavior.Alert : LogBehavior.Toast);
            }
        }

        /// <summary>
    /// 立即强制刷新主页。
    /// 必须在 UI 线程调用。
    /// </summary>
        public void ForceRefresh()
        {
            Logger.Info("要求强制刷新主页");
            ClearCache();
            // 实际的刷新
            if (ModMain.FrmMain.PageCurrent.Page == FormMain.PageType.Launch)
            {
                this.PanBack.ScrollToHome();
                Refresh();
            }
            else
            {
                ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Launch);
            }
        }

        void IRefreshable.Refresh() => ForceRefresh();

        /// <summary>
    /// 清空主页缓存信息。
    /// </summary>
        private void ClearCache()
        {
            LoadedContentHash = default;
            OnlineLoader.Input = ("", true);
            Settings.Set("CacheSavedPageUrl", "");
            Settings.Set("CacheSavedPageVersion", "");
            Logger.Info("已清空主页缓存");
        }

        /// <summary>
    /// 从文本内容中加载主页。
    /// 必须在 UI 线程调用。
    /// </summary>
        private void LoadContent(string Content)
        {
            try
            {
                lock (LoadContentLock)
                {
                    // 如果加载目标内容一致则不加载
                    ulong Hash = (Content ?? "").GetStableHashCode();
                    if (LoadedContentHash.HasValue && Hash == LoadedContentHash.Value)
                        return;
                    LoadedContentHash = Hash;
                    // 实际加载内容
                    this.PanCustom.Children.Clear();
                    if (string.IsNullOrWhiteSpace(Content))
                    {
                        Logger.Info($"实例化：清空主页 UI，来源为空");
                        return;
                    }
                    var LoadStartTime = DateTime.Now;
                    // 修改时应同时修改 PageOtherHelpDetail.Init
                    Content = ModMain.ArgumentReplace(Content, StringUtils.XmlEscape);
                    while (Content.Contains("xmlns"))
                        Content = Content.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", ""); // 禁止声明命名空间
                    Content = "<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=mscorlib\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\" xmlns:core=\"clr-namespace:MeloongCore;assembly=MeloongCore\" xmlns:corewpf=\"clr-namespace:MeloongCore.Wpf;assembly=MeloongCore.Wpf\">" + Content + "</StackPanel>";
                    Logger.Info($"实例化：加载主页 UI 开始，最终内容长度：{Content.Count()}");
                    this.PanCustom.Children.Add((UIElement)ModBase.GetObjectFromXML(Content));
                    // 加载计时
                    int LoadCostTime = (DateTime.Now - LoadStartTime).Milliseconds;
                    Logger.Info($"实例化：加载主页 UI 完成，耗时 {LoadCostTime}ms");
                    if (LoadCostTime > 3000)
                        ModMain.Hint($"主页加载过于缓慢（花费了 {Math.Round(LoadCostTime / 1000d, 1)} 秒），请向主页作者反馈此问题，或暂时停止使用该主页");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"加载失败的主页内容：{Constants.vbCrLf}{Content}");
                OnLoadContentFailed(ex);
            }
        }
        /// <summary>
    /// 加载主页失败时调用。
    /// </summary>
        private void OnLoadContentFailed(Exception ex)
        {
            if (ModBase.ModeDebug || Settings.Get<int>("UiCustomType") == 1)
            {
                Logger.Warn(ex, "加载主页失败");
                if (ModMain.MyMsgBox(ex is UnauthorizedAccessException ? ex.Message : $"主页内容编写有误，请根据下列错误信息进行检查：{Constants.vbCrLf}{ex.GetDisplay(false)}", "加载主页失败", "重试", "取消") == 1)
                    ForceRefresh();
            }
            else
            {
                Logger.Error(ex, "加载主页失败", LogBehavior.Toast);
            }
        }
        /// <summary>
    /// 捕获主页在 Measure 和 Arrange 阶段抛出的异常。
    /// </summary>
        private void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (!(e.Exception is System.Windows.Markup.XamlParseException))
                return;
            e.Handled = true;
            LoadContent(null);
            OnLoadContentFailed(e.Exception);
        }

        void IDispatcherUnhandledException.DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) => DispatcherUnhandledException(sender, e);

        private ulong? LoadedContentHash = default;
        private object LoadContentLock = new object();

        #endregion

    }
}