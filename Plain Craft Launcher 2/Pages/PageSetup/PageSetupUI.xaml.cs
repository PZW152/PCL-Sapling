using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf;
using MeloongCore.Wpf.Extensions;

namespace PCL
{
    public partial class PageSetupUI
    {
        // 非重复加载部分
        private bool _PageSetupUI_Loaded_Reloaded = false;

        public PageSetupUI()
        {
            this.Loaded += PageSetupUI_Loaded;
            this.InitializeComponent();

        }

        private void PageSetupUI_Loaded(object sender, RoutedEventArgs e)
        {
            // 重复加载部分
            this.PanBack.ScrollToHome();

            ModAnimation.AniControlEnabled += 1;
            Refresh(); // #4826
            ModAnimation.AniControlEnabled -= 1;
            if (_PageSetupUI_Loaded_Reloaded)
                return;
            _PageSetupUI_Loaded_Reloaded = true;

            SliderLoad();


        }
        public void Refresh()
        {
            try
            {
                SettingService.RefreshSettings(this);
            }
            catch (NullReferenceException ex)
            {
                Logger.Error(ex, "个性化设置项存在异常，已被自动重置", LogBehavior.Alert);
                Reset();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "重载个性化设置时出错");
            }
        }
        public void Reset()
        {
            try
            {
                SettingService.ResetSettings(this);
                Logger.Info("已初始化个性化设置！");
                ModMain.Hint("已初始化个性化设置", ModMain.HintType.Green, false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "初始化个性化设置失败", LogBehavior.Alert);
            }
            Refresh();
        }


        // 顶部栏

        // 主页

        // 主题
        // 主题自定义
        private void RadioLauncherTheme14_Change(object sender, ModBase.RouteEventArgs e)
        {
            if (this.RadioLauncherTheme14.Checked)
            {
                if (this.LabLauncherHue.Visibility == Visibility.Visible)
                    return;
                this.LabLauncherHue.Visibility = Visibility.Visible;
                this.SliderLauncherHue.Visibility = Visibility.Visible;
                this.LabLauncherSat.Visibility = Visibility.Visible;
                this.SliderLauncherSat.Visibility = Visibility.Visible;
                this.LabLauncherDelta.Visibility = Visibility.Visible;
                this.SliderLauncherDelta.Visibility = Visibility.Visible;
                this.LabLauncherLight.Visibility = Visibility.Visible;
                this.SliderLauncherLight.Visibility = Visibility.Visible;
            }
            else
            {
                if (this.LabLauncherHue.Visibility == Visibility.Collapsed)
                    return;
                this.LabLauncherHue.Visibility = Visibility.Collapsed;
                this.SliderLauncherHue.Visibility = Visibility.Collapsed;
                this.LabLauncherSat.Visibility = Visibility.Collapsed;
                this.SliderLauncherSat.Visibility = Visibility.Collapsed;
                this.LabLauncherDelta.Visibility = Visibility.Collapsed;
                this.SliderLauncherDelta.Visibility = Visibility.Collapsed;
                this.LabLauncherLight.Visibility = Visibility.Collapsed;
                this.SliderLauncherLight.Visibility = Visibility.Collapsed;
            }
            this.CardLauncher.TriggerForceResize();
        }
        private void HSL_Change(object sender, bool e)
        {
            if (ModAnimation.AniControlEnabled != 0 || this.SliderLauncherSat is null || !this.SliderLauncherSat.IsLoaded)
                return;
            ModSecret.ThemeRefresh();
        }

        // 滑动条
        private void SliderLoad()
        {
            this.SliderLauncherHue.GetHintText = new Func<object, object>(v => (ThemePalette.BlueHueMin + Coerce.ToInt(v)) + "°"); // 滑块只覆盖蓝色范围
            this.SliderLauncherSat.GetHintText = new Func<object, object>(v => Coerce.Concat(v, "%"));
            this.SliderLauncherDelta.GetHintText = new Func<int, string>(Value => { int Delta = Value - ThemePalette.MaxTopbarDelta; return Delta > 0 ? "+" + Delta : Delta.ToString(); });
            this.SliderLauncherLight.GetHintText = new Func<int, string>((Value) => { if (Value > 20) { return "+" + (Value - 20); } else if (Value == 20) { return 0.ToString(); } else { return (Value - 20).ToString(); } });
        }

    }
}