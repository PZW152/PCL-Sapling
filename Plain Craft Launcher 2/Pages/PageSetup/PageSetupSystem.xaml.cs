using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MeloongCore;
using MeloongCore.Extensions;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageSetupSystem
    {


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
        private void CheckDebugMode_Change()
        {
            if (ModAnimation.AniControlEnabled == 0)
                ModMain.Hint("部分调试信息将在刷新或启动器重启后切换显示！", Log: false);
        }

        #region 导出 / 导入设置

        private void BtnSystemSettingExp_Click(object sender, MouseButtonEventArgs e)
        {
            ModMain.Hint("该功能尚在开发中！");
        }
        private void BtnSystemSettingImp_Click(object sender, MouseButtonEventArgs e)
        {
            ModMain.Hint("该功能尚在开发中！");
        }

        #endregion

    }
}