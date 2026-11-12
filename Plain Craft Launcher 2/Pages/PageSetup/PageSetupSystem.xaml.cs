using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageSetupSystem
    {
        private void BtnSystemUpdate_Click(object sender, MouseButtonEventArgs e) => ModBase.RunInBackgroundAsync(() => ModUpdate.CheckAsync(true), "手动检查更新");



        // 非重复加载部分
        private bool _PageSetupSystem_Loaded_IsLoaded = false;

        public PageSetupSystem()
        {
            this.Loaded += PageSetupSystem_Loaded;
            this.InitializeComponent();

        }


        private void PageSetupSystem_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();
            if (_PageSetupSystem_Loaded_IsLoaded)
                return;
            _PageSetupSystem_Loaded_IsLoaded = true;

            ModAnimation.AniControlEnabled += 1;
            Reload();
            SliderLoad();
            ModAnimation.AniControlEnabled -= 1;

        }
        public void Reload()
        {
            SettingService.RefreshSettings(this);
        }
        public void Reset()
        {
            try
            {
                SettingService.ResetSettings(this);
                Logger.Info("已初始化启动器页设置");
                ModMain.Hint("已初始化启动器页设置！", ModMain.HintType.Green, false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "初始化启动器页设置失败", LogBehavior.Alert);
            }
            Reload();
        }

        // 滑动条
        private void SliderLoad()
        {
            this.SliderDownloadThread.GetHintText = new Func<object, object>(v => Coerce.ToInt(v) + 1);
            this.SliderDownloadSpeed.GetHintText = new Func<object, object>(v => { switch (v) { case var @case when Coerce.LessEqual(@case, 14): { return Coerce.Concat((Coerce.ToInt(v) + 1) * 0.1d, " M/s"); } case var case1 when Coerce.LessEqual(case1, 31): { return Coerce.Concat((Coerce.ToInt(v) - 11) * 0.5d, " M/s"); } case var case2 when Coerce.LessEqual(case2, 41): { return Coerce.Concat(Coerce.ToInt(v) - 21, " M/s"); } default: { return "无限制"; } } });
            this.SliderDebugAnim.GetHintText = new Func<object, object>(v => Coerce.Greater(v, 29) ? "关闭" : Coerce.Concat(Coerce.ToInt(v) / 10d + 0.1d, "x"));
        }
        private void SliderDownloadThread_PreviewChange(object sender, ModBase.RouteEventArgs e)
        {
            if (this.SliderDownloadThread.Value < 100)
                return;
            if (!Settings.Get<bool>("HintDownloadThread"))
            {
                Settings.Set("HintDownloadThread", true);
                ModMain.MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" + ModBase.CrLf + "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn: true);
            }
        }

        // 调试模式
        private void CheckDebugMode_Change(object sender, bool e)
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModMain.Hint("部分调试信息将在刷新或启动器重启后切换显示！", Log: false);
        }

        #region 导出 / 导入设置

        /// <summary>
        /// 把可导出的设置写入文件（不含登录信息、本机路径、版本独立设置）。
        /// </summary>
        public static void ExportSettings(string Target)
        {
            var Values = Settings.Entries.Values
                .Where(s => SettingsTransfer.IsTransferable(s.Key, s.Encrypted, s.Source == Settings.Sources.Instance))
                .Select(s => new KeyValuePair<string, object>(s.Key, Settings.Get(s.Key)));
            File.WriteAllText(Target, SettingsTransfer.Export(Values, DateTime.Now), new UTF8Encoding(false));
        }
        /// <summary>
        /// 从文件导入设置，返回导入的项数。文件格式不对时抛出 FormatException。
        /// </summary>
        public static int ImportSettings(string Source)
        {
            var Values = SettingsTransfer.Parse(File.ReadAllText(Source));
            int Count = 0;
            foreach (var Pair in Values)
            {
                if (!Settings.Entries.TryGetValue(Pair.Key, out var Entry))
                    continue; // 新版本的设置项，当前版本不认识
                if (!SettingsTransfer.IsTransferable(Entry.Key, Entry.Encrypted, Entry.Source == Settings.Sources.Instance))
                    continue;
                try
                {
                    Settings.Set(Entry.Key, Coerce.ChangeType(Pair.Value.ToObject<object>(), Entry.Type));
                    Count += 1;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"导入设置项 {Pair.Key} 失败");
                }
            }
            return Count;
        }

        private void BtnSystemSettingExp_Click(object sender, MouseButtonEventArgs e)
        {
            string Target = Dialogs.SaveFile("导出设置", "PCL 设置.json", filter: new[] { ("json", "PCL 设置文件") });
            if (string.IsNullOrEmpty(Target))
                return;
            try
            {
                ExportSettings(Target);
                ModMain.Hint("设置已导出。文件中不包含登录信息，可以放心分享。", ModMain.HintType.Green);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "导出设置失败", LogBehavior.Alert);
            }
        }
        private void BtnSystemSettingImp_Click(object sender, MouseButtonEventArgs e)
        {
            string Source = Dialogs.SelectFile("导入设置", false, filter: new[] { (new[] { "json" }, "PCL 设置文件") }).FirstOrDefault();
            if (string.IsNullOrEmpty(Source))
                return;
            try
            {
                int Count = ImportSettings(Source);
                ModAnimation.AniControlEnabled += 1;
                Reload();
                ModAnimation.AniControlEnabled -= 1;
                ModMain.Hint($"已导入 {Count} 项设置。部分设置需要重启启动器后生效。", ModMain.HintType.Green);
            }
            catch (FormatException ex)
            {
                ModMain.MyMsgBox(ex.Message, "无法导入");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "导入设置失败", LogBehavior.Alert);
            }
        }

        #endregion

    }
}