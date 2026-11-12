using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MeloongCore;
using MeloongCore.Extensions;

namespace PCL
{
    public partial class PageSetupAbout
    {

        private new bool IsLoaded = false;

        public PageSetupAbout()
        {
            this.Loaded += PageSetupAbout_Loaded;
            this.InitializeComponent();

        }
        private void PageSetupAbout_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();

            // 非重复加载部分
            if (IsLoaded)
                return;
            IsLoaded = true;

            this.ItemAboutPcl.Info = this.ItemAboutPcl.Info.Replace("%UPSTREAM%", ModBase.UpstreamVersion);
            this.ItemAboutEdition.Info = this.ItemAboutEdition.Info.Replace("%VERSION%", ModBase.VersionDisplay + (string.IsNullOrEmpty(ModBase.CommitHash) ? "" : "（" + ModBase.CommitHash.Substring(0, System.Math.Min(7, ModBase.CommitHash.Length)) + "）"));

        }

        #region 反馈与支持（取自 Sapling）

        private void BtnAboutFeedback_Click(object sender, MouseButtonEventArgs e)
        {
            ModBase.ClipboardSet(DiagnosticsText());
            ModMain.Hint("诊断信息已复制，可以粘贴到问题描述中", ModMain.HintType.Green);
            ModBase.OpenWebsite(ModBase.RepoUrl + "/issues/new");
        }
        private void BtnAboutDiagnostics_Click(object sender, MouseButtonEventArgs e)
        {
            ModBase.ClipboardSet(DiagnosticsText());
            ModMain.Hint("诊断信息已复制", ModMain.HintType.Green);
        }
        private void BtnAboutDataFolder_Click(object sender, MouseButtonEventArgs e) => ModBase.OpenExplorer(Paths.Base + @"PCL\");
        private void BtnAboutLicenses_Click(object sender, MouseButtonEventArgs e)
        {
            if (ModMain.MyMsgBox("本修改版基于 PCL（《PCL 分发有限许可》）修改，并使用以下第三方组件：" + ModBase.CrLf + ModBase.CrLf +
                    "· MeloongCore（Apache-2.0）" + ModBase.CrLf +
                    "· Newtonsoft.Json（MIT）" + ModBase.CrLf +
                    "· Ookii.Dialogs.Wpf（BSD-3-Clause）" + ModBase.CrLf +
                    "· ThrottleDebounce（Apache-2.0）" + ModBase.CrLf +
                    "· Imazen.WebP（MIT）与 libwebp（BSD-3-Clause）" + ModBase.CrLf +
                    "· OpenCC 字词资料（Apache-2.0）" + ModBase.CrLf +
                    "· PolySharp（MIT）", "第三方许可", "查看完整说明", "确定") == 1)
                ModBase.OpenWebsite(ModBase.RepoUrl + "/blob/main/THIRD-PARTY-NOTICES.md");
        }

        /// <summary>
        /// 诊断信息：版本、系统与当前选择的版本。用户文件夹路径替换为 %USERPROFILE%，不包含账号信息。
        /// </summary>
        public static string DiagnosticsText()
        {
            var Builder = new StringBuilder();
            void Line(string Name, Func<object> Value)
            {
                string Text;
                try { Text = Value()?.ToString() ?? "无"; } catch (Exception ex) { Text = "获取失败：" + ex.Message; }
                Builder.AppendLine($"{Name}：{Text}");
            }
            Line("启动器", () => $"{ModBase.ProductName} {ModBase.VersionDisplay}" + (string.IsNullOrEmpty(ModBase.CommitHash) ? "" : $"（{ModBase.CommitHash}）"));
            Line("基于", () => "PCL " + ModBase.UpstreamVersion);
            Line("操作系统", () => RuntimeInformation.OSDescription + (Environment.Is64BitOperatingSystem ? "，64 位" : "，32 位"));
            Line(".NET Framework", () => RuntimeInformation.FrameworkDescription);
            Line("内存", () => $"可用 {Math.Floor(SystemInfo.AvailablePhysicalMemory / 1048576d)} MB / 共 {Math.Floor(SystemInfo.TotalPhysicalMemory / 1048576d)} MB");
            Line("缩放", () => Math.Round(ModBase.DPI / 96d * 100d) + "%");
            Line("界面语言", () => UiLanguage.UseTraditional ? "繁体中文" : "简体中文");
            Line("系统区域", () => System.Globalization.CultureInfo.CurrentUICulture.Name);
            Line("游戏文件夹", () => HideUserFolder(ModMinecraft.McFolderSelected));
            Line("选择的版本", () => ModMinecraft.McInstanceSelected is null ? "无" : $"{ModMinecraft.McInstanceSelected.Name}（{ModMinecraft.McInstanceSelected.Info}）");
            Line("启动器位置", () => HideUserFolder(ModBase.PathExe));
            return Builder.ToString().TrimEnd();
        }
        private static string HideUserFolder(string Path)
        {
            string User = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(Path) || string.IsNullOrEmpty(User) ? Path : Path.Replace(User, "%USERPROFILE%");
        }

        #endregion
    }
}
