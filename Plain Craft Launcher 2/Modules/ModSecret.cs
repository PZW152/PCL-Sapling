using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf.Extensions;
using Newtonsoft.Json.Linq;

namespace PCL
{
    // 由于包含加解密等安全信息，本文件中的部分代码已被删除

    internal static class ModSecret
    {

        // 标注 PCL 的不同分支，仅用于替换标记
        public const string VersionBranchMain = "OpenSource";
        // 在开源版的注册表与常规版的注册表隔离，以防数据冲突
        public const string RegFolder = "PCLDebug";
        // 用于微软登录的 ClientId
        public static string OAuthClientId = NonEmpty(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID")) ?? BuildSecrets.MsClientId; // 運行時環境變量優先，否則用編譯時注入的值
        // CurseForge API Key
        public static string CurseForgeAPIKey = NonEmpty(Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY")) ?? BuildSecrets.CurseForgeApiKey;
        private static string NonEmpty(string Value) => string.IsNullOrEmpty(Value) ? null : Value;

        #region 网络鉴权

        public const bool RestrictMcDownload = true;
        internal static string SecretCdnSign(string UrlWithMark)
        {
            if (!UrlWithMark.EndsWithF("{CDN}"))
                return UrlWithMark;
            return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20");
        }
        /// <summary>
    /// 设置 Headers 的 UA、Referer。
    /// </summary>
        internal static void SecretHeadersSign(string Url, ref HttpRequestMessage Req, bool SimulateBrowserHeaders = false)
        {
            if (!Req.Headers.UserAgent.Any())
            {
                if (Url.Contains("baidupcs.com") || Url.Contains("baidu.com"))
                {
                    Req.Headers.Add("User-Agent", "LogStatistic");  // #4951
                }
                else if (SimulateBrowserHeaders)
                {
                    Req.Headers.Add("User-Agent", $"PCL2/{ModBase.VersionBaseName}.{(int)ModBase.BuildType} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36");
                }
                else
                {
                    Req.Headers.Add("User-Agent", $"PCL2/{ModBase.VersionBaseName}.{(int)ModBase.BuildType}");
                }
            }
            if (!SimulateBrowserHeaders)
                Req.Headers.Add("Referer", $"http://{ModBase.VersionCode}.open.pcl2.server/");
            if (Url.Contains("api.curseforge.com") || Url.Contains("forgecdn.net"))
                Req.Headers.Add("x-api-key", CurseForgeAPIKey);
        }

        #endregion

        #region 主题

        public static ModBase.MyColor Color1 = new ModBase.MyColor(52d, 61d, 74d);
        public static ModBase.MyColor Color2 = new ModBase.MyColor(11d, 91d, 203d);
        public static ModBase.MyColor Color3 = new ModBase.MyColor(19d, 112d, 243d);
        public static ModBase.MyColor Color4 = new ModBase.MyColor(72d, 144d, 245d);
        public static ModBase.MyColor Color5 = new ModBase.MyColor(150d, 192d, 249d);
        public static ModBase.MyColor Color6 = new ModBase.MyColor(213d, 230d, 253d);
        public static ModBase.MyColor Color7 = new ModBase.MyColor(222d, 236d, 253d);
        public static ModBase.MyColor Color8 = new ModBase.MyColor(234d, 242d, 254d);
        public static ModBase.MyColor ColorBg0 = new ModBase.MyColor(150d, 192d, 249d);
        public static ModBase.MyColor ColorBg1 = new ModBase.MyColor(190d, Color7);
        public static ModBase.MyColor ColorGray1 = new ModBase.MyColor(64d, 64d, 64d);
        public static ModBase.MyColor ColorGray2 = new ModBase.MyColor(115d, 115d, 115d);
        public static ModBase.MyColor ColorGray3 = new ModBase.MyColor(140d, 140d, 140d);
        public static ModBase.MyColor ColorGray4 = new ModBase.MyColor(166d, 166d, 166d);
        public static ModBase.MyColor ColorGray5 = new ModBase.MyColor(204d, 204d, 204d);
        public static ModBase.MyColor ColorGray6 = new ModBase.MyColor(235d, 235d, 235d);
        public static ModBase.MyColor ColorGray7 = new ModBase.MyColor(240d, 240d, 240d);
        public static ModBase.MyColor ColorGray8 = new ModBase.MyColor(245d, 245d, 245d);
        public static ModBase.MyColor ColorSemiTransparent = new ModBase.MyColor(1d, Color8);

        public static int ThemeNow = -1;
        public static int ColorHue = 210;
        public static int ColorSat = 85;
        public static int ColorLightAdjust = 0;
        public static OneOf<int, int[]> ColorHueTopbarDelta = 0;

        public static void ThemeRefresh(int NewTheme = -1)
        {
            try
            {
                if (ThemeNow == NewTheme && NewTheme >= 0)
                    return;
                if (NewTheme >= 0)
                    ThemeNow = NewTheme;

                Color1 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.2d, 25d + ColorLightAdjust * 0.3d);
                Color2 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust);
                Color3 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust);
                Color4 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust);
                Color5 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 80d + ColorLightAdjust * 0.4d);
                Color6 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 91d + ColorLightAdjust * 0.1d);
                Color7 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 95d);
                Color8 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 97d);
                ColorBg0 = Color4 * 0.4d + Color5 * 0.4d + ColorGray4 * 0.2d;
                ColorBg1 = new ModBase.MyColor(190d, Color7);

                ColorSemiTransparent = new ModBase.MyColor(1d, Color8);
                System.Windows.Application.Current.Resources["ColorBrush1"] = new SolidColorBrush(Color1);
                System.Windows.Application.Current.Resources["ColorBrush2"] = new SolidColorBrush(Color2);
                System.Windows.Application.Current.Resources["ColorBrush3"] = new SolidColorBrush(Color3);
                System.Windows.Application.Current.Resources["ColorBrush4"] = new SolidColorBrush(Color4);
                System.Windows.Application.Current.Resources["ColorBrush5"] = new SolidColorBrush(Color5);
                System.Windows.Application.Current.Resources["ColorBrush6"] = new SolidColorBrush(Color6);
                System.Windows.Application.Current.Resources["ColorBrush7"] = new SolidColorBrush(Color7);
                System.Windows.Application.Current.Resources["ColorBrush8"] = new SolidColorBrush(Color8);
                System.Windows.Application.Current.Resources["ColorBrushBg0"] = new SolidColorBrush(ColorBg0);
                System.Windows.Application.Current.Resources["ColorBrushBg1"] = new SolidColorBrush(ColorBg1);
                System.Windows.Application.Current.Resources["ColorObject1"] = (Color)Color1;
                System.Windows.Application.Current.Resources["ColorObject2"] = (Color)Color2;
                System.Windows.Application.Current.Resources["ColorObject3"] = (Color)Color3;
                System.Windows.Application.Current.Resources["ColorObject4"] = (Color)Color4;
                System.Windows.Application.Current.Resources["ColorObject5"] = (Color)Color5;
                System.Windows.Application.Current.Resources["ColorObject6"] = (Color)Color6;
                System.Windows.Application.Current.Resources["ColorObject7"] = (Color)Color7;
                System.Windows.Application.Current.Resources["ColorObject8"] = (Color)Color8;
                System.Windows.Application.Current.Resources["ColorObjectBg0"] = (Color)ColorBg0;
                System.Windows.Application.Current.Resources["ColorObjectBg1"] = (Color)ColorBg1;
                ThemeRefreshMain();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast);
            }
        }
        public static void ThemeRefreshMain()
        {
            ModBase.RunInUi(() =>
        {
            if (!ModMain.FrmMain.IsLoaded)
                return;
            // 顶部条背景
            var Brush = new LinearGradientBrush() { EndPoint = new Point(1d, 0d), StartPoint = new Point(0d, 0d) };
            int[] Deltas = ColorHueTopbarDelta.Switch(d => new int[] { -d, 0, d }, d => d);
            Brush.GradientStops.Add(new GradientStop() { Offset = 0d, Color = new ModBase.MyColor().FromHSL2(ColorHue + Deltas[0], ColorSat, 48 + ColorLightAdjust) });
            Brush.GradientStops.Add(new GradientStop() { Offset = 0.5d, Color = new ModBase.MyColor().FromHSL2(ColorHue + Deltas[1], ColorSat, 54 + ColorLightAdjust) });
            Brush.GradientStops.Add(new GradientStop() { Offset = 1d, Color = new ModBase.MyColor().FromHSL2(ColorHue + Deltas[2], ColorSat, 48 + ColorLightAdjust) });
            ModMain.FrmMain.PanTitle.Background = Brush;
            ModMain.FrmMain.PanTitle.Background.Freeze();
            // 主页面背景
            if (Settings.Get<bool>("UiBackgroundColorful"))
            {
                Brush = new LinearGradientBrush() { EndPoint = new Point(0.1d, 1d), StartPoint = new Point(0.9d, 0d) };
                Brush.GradientStops.Add(new GradientStop() { Offset = -0.1d, Color = new ModBase.MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.8d, 91d) });
                Brush.GradientStops.Add(new GradientStop() { Offset = 0.4d, Color = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.8d, 91d) });
                Brush.GradientStops.Add(new GradientStop() { Offset = 1.1d, Color = new ModBase.MyColor().FromHSL2(ColorHue + 15, ColorSat * 0.8d, 91d) });
                ModMain.FrmMain.PanForm.Background = Brush;
            }
            else
            {
                ModMain.FrmMain.PanForm.Background = new ModBase.MyColor(245d, 245d, 245d);
            }
            ModMain.FrmMain.PanForm.Background.Freeze();
        });
        }

        #endregion

        #region 更新

        internal static void UpdateCheckByButton()
        {
            ModMain.Hint("该版本中不包含更新功能……");
        }

        internal static bool IsUpdateWaitingRestart = false;
        public static void UpdateRestart(bool TriggerRestartAndByEnd)
        {
        }
        public static void UpdateReplace(int ProcessId, string OldFileName, string NewFileName, bool TriggerRestart)
        {
        }

        /// <summary>
    /// 确保 PathTemp/Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    /// 如果不是，则下载一个。
    /// </summary>
        internal static void DownloadLatestPCL(ModLoader.LoaderBase LoaderToSyncProgress = null)
        {
            // 注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
        }

        #endregion

        #region 联网配置

        /// <summary>
    /// 联网获取的配置信息。
    /// 若获取失败或仍在获取中，可能为 Nothing。
    /// </summary>
        public static JObject ServerConfig;

        public static ModLoader.LoaderTask<int, int> ServerLoader = new ModLoader.LoaderTask<int, int>("PCL 配置更新", _ => Logger.Info("该版本中不包含更新通知功能……"), Priority: ThreadPriority.BelowNormal) { ReloadTimeout = 1000 * 60 * 60 }; // 超时 1 小时

        #endregion

        #region 识别码

        /// <summary>
    /// 获取设备识别码。
    /// </summary>
        internal static string GetIdentify()
        {
            return "0000-0000-0000-0000";
        }

        #endregion

    }
}