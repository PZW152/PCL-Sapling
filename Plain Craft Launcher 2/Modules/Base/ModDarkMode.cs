using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using MeloongCore;
using Microsoft.Win32;

namespace PCL
{
    /// <summary>
    /// 深色模式（取自 Sapling：深色只换中性色，主题蓝保持不变）。
    /// 设置 UiDarkMode：0 跟随系统，1 浅色，2 深色。
    /// PCL 的很多颜色在界面加载时就固定了（StaticResource），所以切换后需要重新打开启动器。
    /// </summary>
    public static class ModDarkMode
    {
        /// <summary>
        /// 本次运行是否使用深色。启动时决定，运行中不变。
        /// </summary>
        public static bool IsDark { get; private set; }

        /// <summary>
        /// 系统是否设置为深色应用模式。
        /// </summary>
        public static bool SystemPrefersDark()
        {
            try
            {
                using var Key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return Key?.GetValue("AppsUseLightTheme") is int Light && Light == 0;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取系统深色模式设置失败");
                return false;
            }
        }

        public static bool Resolve(int Setting) => Setting switch
        {
            1 => false,
            2 => true,
            _ => SystemPrefersDark()
        };

        /// <summary>
        /// 在任何窗口创建之前调用：决定深浅色，并替换在界面加载时就会被读取的中性色。
        /// </summary>
        public static void Initialize()
        {
            IsDark = Resolve(Settings.Get<int>("UiDarkMode"));
            Logger.Info($"界面外观：{(IsDark ? "深色" : "浅色")}");
            if (!IsDark)
                return;
            var Res = Application.Current.Resources;
            // 灰阶整体翻转：1 是最深的文字色，8 是最浅的底色
            string[] Grays = { "#D3D8DE", "#A8B0B9", "#8C949D", "#6F7780", "#4B525A", "#363C44", "#30363E", "#2B3139" };
            for (int i = 0; i < Grays.Length; i++)
            {
                var Value = (Color)ColorConverter.ConvertFromString(Grays[i]);
                Res["ColorBrushGray" + (i + 1)] = Frozen(Value);
                Res["ColorObjectGray" + (i + 1)] = Value;
            }
            Res["ColorBrushHalfWhite"] = Frozen(Color.FromArgb(0x55, 0x10, 0x14, 0x1A));
            Res["ColorBrushSemiWhite"] = Frozen(Color.FromArgb(0xBB, 0x1E, 0x24, 0x2C));
            Res["ColorBrushWhite"] = Frozen(Color.FromRgb(0x26, 0x2E, 0x38));
            Res["ColorBrushSemiTransparent"] = Frozen(Color.FromArgb(0x01, 0x20, 0x28, 0x30));
            Res["ColorBrushBackgroundTransparentSidebar"] = Frozen(Color.FromArgb(0xF2, 0x1D, 0x23, 0x2B));
            Res["ColorBrushRedBack"] = Frozen(Color.FromArgb(0x80, 0x5A, 0x24, 0x24));
        }

        private static SolidColorBrush Frozen(Color Value)
        {
            var Brush = new SolidColorBrush(Value);
            Brush.Freeze();
            return Brush;
        }

        /// <summary>
        /// 图标按钮等“黑色”元素在深色模式下改用白色。
        /// </summary>
        public static double Ink => IsDark ? 255d : 0d;

        /// <summary>
        /// 外观设置改变：与本次运行不同时，询问是否重新打开启动器。
        /// </summary>
        public static void OnSettingChanged(int Setting)
        {
            if (ModMain.FrmMain is null || !ModMain.FrmMain.IsLoaded || Resolve(Setting) == IsDark)
                return;
            ModBase.RunInDedicatedThread(() =>
            {
                if (ModMain.MyMsgBox("新的外观需要重新打开启动器后才会生效。", "切换外观", "现在重新打开", "稍后") != 1)
                    return;
                try
                {
                    Process.Start(new ProcessStartInfo(ModBase.PathExe, "--wait") { WorkingDirectory = Path.GetDirectoryName(ModBase.PathExe), UseShellExecute = true });
                    ModBase.RunInUi(() => ModMain.FrmMain.EndProgram(false));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "重新打开启动器失败", LogBehavior.Toast);
                }
            }, "Dark Mode Restart");
        }
    }
}
