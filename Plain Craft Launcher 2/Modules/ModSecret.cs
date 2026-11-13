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
                    Req.Headers.TryAddWithoutValidation("User-Agent", $"PCL-Sapling/{ModBase.VersionBaseName} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36");
                }
                else
                {
                    // 产品名/版本号（联系方式）：Modrinth 等 API 要求能识别出是哪个程序发出的请求
                    Req.Headers.TryAddWithoutValidation("User-Agent", $"PCL-Sapling/{ModBase.VersionBaseName} (+{ModBase.RepoUrl})");
                }
            }
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
                // 读取当前主题的配色参数（原开源版在这里是空的：色相固定为蓝色，切换主题与自定义滑块都不会生效）
                ThemeNow = NewTheme >= 0 ? NewTheme : Settings.Get<int>("UiLauncherTheme");
                var Args = ThemePalette.Resolve(ThemeNow, Settings.Get<int>("UiLauncherHue"), Settings.Get<int>("UiLauncherSat"), Settings.Get<int>("UiLauncherDelta"), Settings.Get<int>("UiLauncherLight"));
                ColorHue = Args.Hue;
                ColorSat = Args.Saturation;
                ColorLightAdjust = Args.LightAdjust;
                ColorHueTopbarDelta = Args.TopbarHueDelta;

                if (ModDarkMode.IsDark)
                {
                    // 深色：1 是浅色文字，2~4 是提亮的主题蓝，5~8 是很深的蓝灰底色
                    Color1 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.15d, 88d);
                    Color2 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.85d, 68d + ColorLightAdjust * 0.3d);
                    Color3 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.85d, 62d + ColorLightAdjust * 0.3d);
                    Color4 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.75d, 55d + ColorLightAdjust * 0.3d);
                    Color5 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.45d, 36d);
                    Color6 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.4d, 27d);
                    Color7 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.35d, 22d);
                    Color8 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.3d, 18d);
                }
                else
                {
                    Color1 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.2d, 25d + ColorLightAdjust * 0.3d);
                    Color2 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust);
                    Color3 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust);
                    Color4 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust);
                    Color5 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 80d + ColorLightAdjust * 0.4d);
                    Color6 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 91d + ColorLightAdjust * 0.1d);
                    Color7 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 95d);
                    Color8 = new ModBase.MyColor().FromHSL2(ColorHue, ColorSat, 97d);
                }
                ColorBg0 = ModDarkMode.IsDark ? new ModBase.MyColor().FromHSL2(ColorHue, ColorSat * 0.4d, 40d) : Color4 * 0.4d + Color5 * 0.4d + ColorGray4 * 0.2d; // 输入框、下拉框的边框
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
                ApplySurfaceResources();
                ThemeRefreshMain();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast);
            }
        }
        /// <summary>
        /// 首页等新界面使用的表面颜色（取自 Sapling 的层次：画布 → 卡片 → 凹陷）。都跟随主题色相。
        /// </summary>
        private static void ApplySurfaceResources()
        {
            var Res = System.Windows.Application.Current.Resources;
            Color Hsl(double Hue, double Sat, double Light, byte Alpha = 255)
            {
                Color Result = new ModBase.MyColor().FromHSL2(Hue, Sat, Light);
                Result.A = Alpha;
                return Result;
            }
            SolidColorBrush Solid(Color Value)
            {
                var Brush = new SolidColorBrush(Value);
                Brush.Freeze();
                return Brush;
            }
            bool Dark = ModDarkMode.IsDark;
            Res["ColorBrushSurface"] = Solid(Dark ? Color.FromArgb(242, 0x24, 0x2B, 0x35) : Color.FromArgb(245, 255, 255, 255));
            Res["ColorBrushText"] = Solid(Color1);
            Res["ColorBrushOnAccent"] = Solid(Dark ? Color.FromRgb(0xF2, 0xF6, 0xFA) : (Color)Color8); // 主题色底上的图标与文字
            Res["ColorBrushTextMuted"] = Solid(Dark ? Hsl(ColorHue, 10, 64) : Hsl(ColorHue, 10, 47));
            Res["ColorBrushAccentText"] = Solid(Dark ? Hsl(ColorHue, ColorSat * 0.9, 72) : Hsl(ColorHue, ColorSat, 41 + ColorLightAdjust * 0.5));
            Res["ColorBrushChip"] = Solid(Dark ? Hsl(ColorHue, ColorSat * 0.45, 26) : Hsl(ColorHue, ColorSat * 0.9, 93.5));
            Res["ColorBrushChipText"] = Solid(Dark ? Hsl(ColorHue, ColorSat * 0.9, 80) : Hsl(ColorHue, ColorSat, 37 + ColorLightAdjust * 0.5));
            Color Tile = Dark ? Hsl(ColorHue, ColorSat * 0.22, 20) : Hsl(ColorHue, ColorSat * 0.6, 97.5);
            Color TileBorder = Dark ? Hsl(ColorHue, ColorSat * 0.22, 27) : Hsl(ColorHue, ColorSat * 0.4, 90);
            Res["ColorBrushTile"] = Solid(Tile);
            Res["ColorBrushTileBorder"] = Solid(TileBorder);
            Res["ColorObjectTile"] = Tile;
            Res["ColorObjectTileHover"] = Dark ? Hsl(ColorHue, ColorSat * 0.4, 25) : Hsl(ColorHue, ColorSat * 0.9, 94.5);
            Res["ColorObjectTileBorder"] = TileBorder;
            Res["ColorObjectTileHoverBorder"] = Dark ? Hsl(ColorHue, ColorSat * 0.6, 45) : Hsl(ColorHue, ColorSat * 0.8, 78);
            // 弹窗与阴影
            Res["ColorBrushDialog"] = Solid(Dark ? Color.FromRgb(0x23, 0x2A, 0x33) : Color.FromRgb(0xFB, 0xFB, 0xFB));
            Res["ColorBrushDialogText"] = Solid(Dark ? Color.FromRgb(0xC5, 0xCC, 0xD3) : Color.FromRgb(0x5C, 0x5C, 0x5C));
            Res["ColorObjectShadow"] = Dark ? Color.FromRgb(0, 0, 0) : (Color)Color1;
            // 主角卡顶部很淡的一层主题色，往下渐隐成卡片底色
            Color WashColor = Dark ? Hsl(ColorHue, ColorSat * 0.5, 25) : Hsl(ColorHue, ColorSat * 0.9, 95.5);
            var Wash = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            Wash.GradientStops.Add(new GradientStop(WashColor, 0));
            WashColor.A = 0;
            Wash.GradientStops.Add(new GradientStop(WashColor, 0.55));
            Wash.Freeze();
            Res["ColorBrushHeroWash"] = Wash;
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
            double TitleSat = ModDarkMode.IsDark ? ColorSat * 0.8d : ColorSat, TitleEdge = ModDarkMode.IsDark ? 31 : 48 + ColorLightAdjust, TitleMid = ModDarkMode.IsDark ? 36 : 54 + ColorLightAdjust;
            Brush.GradientStops.Add(new GradientStop() { Offset = 0d, Color = new ModBase.MyColor().FromHSL2(ColorHue + Deltas[0], TitleSat, TitleEdge) });
            Brush.GradientStops.Add(new GradientStop() { Offset = 0.5d, Color = new ModBase.MyColor().FromHSL2(ColorHue + Deltas[1], TitleSat, TitleMid) });
            Brush.GradientStops.Add(new GradientStop() { Offset = 1d, Color = new ModBase.MyColor().FromHSL2(ColorHue + Deltas[2], TitleSat, TitleEdge) });
            ModMain.FrmMain.PanTitle.Background = Brush;
            ModMain.FrmMain.PanTitle.Background.Freeze();
            // 主页面背景：随主题色变化的彩色渐变
            Brush = new LinearGradientBrush() { EndPoint = new Point(0.1d, 1d), StartPoint = new Point(0.9d, 0d) };
            double FormSat = ModDarkMode.IsDark ? ColorSat * 0.3d : ColorSat * 0.8d, FormLight = ModDarkMode.IsDark ? 13d : 91d;
            Brush.GradientStops.Add(new GradientStop() { Offset = -0.1d, Color = new ModBase.MyColor().FromHSL2(ColorHue - 15, FormSat, FormLight) });
            Brush.GradientStops.Add(new GradientStop() { Offset = 0.4d, Color = new ModBase.MyColor().FromHSL2(ColorHue, FormSat, FormLight) });
            Brush.GradientStops.Add(new GradientStop() { Offset = 1.1d, Color = new ModBase.MyColor().FromHSL2(ColorHue + 15, FormSat, FormLight) });
            ModMain.FrmMain.PanForm.Background = Brush;
            ModMain.FrmMain.PanForm.Background.Freeze();
        });
        }

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