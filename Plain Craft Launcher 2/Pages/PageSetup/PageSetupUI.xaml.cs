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
        #region 事件

        private void CheckHiddenSetupLaunch_Change(object sender, bool e) => HiddenSetupSub();
        private void CheckHiddenSetupSystem_Change(object sender, bool e) => HiddenSetupSub();
        private void CheckHiddenPageSetup_Change(object sender, bool e)
        {
            HiddenSetupMain();
            HiddenHint(sender, e);
        }
        private void CheckHiddenSetupUI_Change(object sender, bool e)
        {
            HiddenSetupSub();
            HiddenHint(sender, e);
        }

        #endregion


        // 非重复加载部分
        private bool _PageSetupUI_Loaded_Reloaded = false;

        public PageSetupUI()
        {
            this.Loaded += PageSetupUI_Loaded;
            this.Loaded += (_, __) => HiddenRefresh();
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

        #region 功能隐藏

        private static bool _HiddenForceShow = false;
        /// <summary>
    /// 是否强制显示被禁用的功能。
    /// </summary>
        public static bool HiddenForceShow
        {
            get
            {
                return _HiddenForceShow;
            }
            set
            {
                _HiddenForceShow = value;
                HiddenRefresh();
            }
        }

        /// <summary>
    /// 更新功能隐藏带来的显示变化。
    /// </summary>
        public static void HiddenRefresh()
        {
            if (ModMain.FrmMain.PanTitleSelect is null || !ModMain.FrmMain.PanTitleSelect.IsLoaded)
                return;
            try
            {
                // 顶部栏
                if (!HiddenForceShow && Settings.Get<bool>("UiHiddenPageDownload") && Settings.Get<bool>("UiHiddenPageSetup"))
                {
                    // 顶部栏已被全部隐藏
                    ModMain.FrmMain.PanTitleSelect.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 顶部栏未被全部隐藏
                    ModMain.FrmMain.PanTitleSelect.Visibility = Visibility.Visible;
                    ModMain.FrmMain.BtnTitleSelect1.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenPageDownload") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmMain.BtnTitleSelect3.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenPageSetup") ? Visibility.Collapsed : Visibility.Visible;
                }
                // 功能
                ModMain.FrmLaunchLeft.RefreshButtonsUI();
                if (ModMain.FrmSetupUI is not null)
                {
                    ModMain.FrmSetupUI.CardSwitch.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenFunctionHidden") ? Visibility.Collapsed : Visibility.Visible;
                }
                // 版本设置子页面
                if (ModMain.FrmInstanceLeft is not null)
                {
                    ModMain.FrmInstanceLeft.ItemModify.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenFunctionInstanceModify") ? Visibility.Collapsed : Visibility.Visible;
                }
                // 设置子页面
                if (ModMain.FrmSetupLeft is not null)
                {
                    ModMain.FrmSetupLeft.ItemLaunch.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenSetupLaunch") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmSetupLeft.ItemUI.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenSetupUi") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmSetupLeft.ItemSystem.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenSetupSystem") ? Visibility.Collapsed : Visibility.Visible;
                    // 隐藏左边选择卡
                    int AvaliableCount = 0;
                    if (!Settings.Get<bool>("UiHiddenSetupLaunch"))
                        AvaliableCount += 1;
                    if (!Settings.Get<bool>("UiHiddenSetupUi"))
                        AvaliableCount += 1;
                    if (!Settings.Get<bool>("UiHiddenSetupSystem"))
                        AvaliableCount += 1;
                    AvaliableCount += 1; // “关于”始终显示
                    ModMain.FrmSetupLeft.PanItem.Visibility = AvaliableCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
                }
                // 各个页面的入口
                if (ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.InstanceSelect)
                    ModMain.FrmSelectRight.BtnEmptyDownload_Loaded();
                if (ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.Launch)
                    ModMain.FrmLaunchLeft.RefreshButtonsUI();
                if (ModMain.FrmMain.PageCurrent == (FormMain.PageStackData)FormMain.PageType.InstanceSetup && ModMain.FrmInstanceModDisabled is not null)
                    ModMain.FrmInstanceModDisabled.BtnDownload_Loaded();
                // 备注
                if (ModMain.FrmSetupUI is not null)
                    ModMain.FrmSetupUI.CardSwitch.Title = HiddenForceShow ? "功能隐藏（已暂时关闭，按 F12 以重新启用）" : "功能隐藏";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "刷新功能隐藏项目失败");
            }
        }

        // UI 协同改变
        private void HiddenSetupMain()
        {
            // 设置主页面
            if (this.CheckHiddenPageSetup.Checked)
            {
                // 开启
                this.CheckHiddenSetupLaunch.Checked = true;
                this.CheckHiddenSetupSystem.Checked = true;
                this.CheckHiddenSetupUI.Checked = true;
            }
            // 关闭
            else if (Settings.Get<bool>("UiHiddenSetupLaunch") && Settings.Get<bool>("UiHiddenSetupUi") && Settings.Get<bool>("UiHiddenSetupSystem"))
            {
                this.CheckHiddenSetupLaunch.Checked = false;
                this.CheckHiddenSetupSystem.Checked = false;
                this.CheckHiddenSetupUI.Checked = false;
            }
        }
        private void HiddenSetupSub()
        {
            // 设置子页面
            if (Settings.Get<bool>("UiHiddenSetupLaunch") && Settings.Get<bool>("UiHiddenSetupUi") && Settings.Get<bool>("UiHiddenSetupSystem"))
            {
                // 已被全部隐藏
                this.CheckHiddenPageSetup.Checked = true;
            }
            else
            {
                // 未被全部隐藏
                this.CheckHiddenPageSetup.Checked = false;
            }
        }
        // 警告提示
        private void HiddenHint(object sender, bool user)
        {
            if (ModAnimation.AniControlEnabled == 0 && ((MyCheckBox)sender).Checked)
                ModMain.Hint("按 F12 即可暂时关闭功能隐藏设置。千万别忘了，要不然设置就改不回来了……");
        }

        #endregion

        // 滑动条
        private void SliderLoad()
        {
            this.SliderLauncherTransparent.GetHintText = new Func<object, object>(v => Math.Round(40 + Coerce.ToDouble(v) * 0.1d) + "%");
            this.SliderLauncherHue.GetHintText = new Func<object, object>(v => (ThemePalette.BlueHueMin + Coerce.ToInt(v)) + "°"); // 滑块只覆盖蓝色范围
            this.SliderLauncherSat.GetHintText = new Func<object, object>(v => Coerce.Concat(v, "%"));
            this.SliderLauncherDelta.GetHintText = new Func<int, string>(Value => { int Delta = Value - ThemePalette.MaxTopbarDelta; return Delta > 0 ? "+" + Delta : Delta.ToString(); });
            this.SliderLauncherLight.GetHintText = new Func<int, string>((Value) => { if (Value > 20) { return "+" + (Value - 20); } else if (Value == 20) { return 0.ToString(); } else { return (Value - 20).ToString(); } });
        }

    }
}