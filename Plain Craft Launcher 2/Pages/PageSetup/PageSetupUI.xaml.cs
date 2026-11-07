using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf;
using MeloongCore.Wpf.Extensions;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{
    public partial class PageSetupUI
    {

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
                BackgroundRefresh(false, false);

                // 标题栏
                this.CheckLogoLeft.Visibility = this.RadioLogoType0.Checked ? Visibility.Visible : Visibility.Collapsed;
                this.PanLogoText.Visibility = this.RadioLogoType2.Checked ? Visibility.Visible : Visibility.Collapsed;
                this.PanLogoChange.Visibility = this.RadioLogoType3.Checked ? Visibility.Visible : Visibility.Collapsed;

                // 主页
                OnMainPageTypeChanged();
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

        // 背景图片
        private void BtnUIBgOpen_Click(object sender, EventArgs e)
        {
            ModBase.OpenExplorer(Paths.Base + @"PCL\Pictures\");
        }
        private void BtnBackgroundRefresh_Click(object sender, EventArgs e)
        {
            BackgroundRefresh(true, true);
        }
        public void BackgroundRefreshUI(bool Show, int Count)
        {
            if (this.PanBackgroundOpacity == null)
                return;
            if (Show)
            {
                this.PanBackgroundOpacity.Visibility = Visibility.Visible;
                this.PanBackgroundBlur.Visibility = Visibility.Visible;
                this.PanBackgroundSuit.Visibility = Visibility.Visible;
                this.BtnBackgroundClear.Visibility = Visibility.Visible;
                this.CardBackground.Title = "背景图片（" + Count + " 张）";
            }
            else
            {
                this.PanBackgroundOpacity.Visibility = Visibility.Collapsed;
                this.PanBackgroundBlur.Visibility = Visibility.Collapsed;
                this.PanBackgroundSuit.Visibility = Visibility.Collapsed;
                this.BtnBackgroundClear.Visibility = Visibility.Collapsed;
                this.CardBackground.Title = "背景图片";
            }
            this.CardBackground.TriggerForceResize();
        }
        private void BtnBackgroundClear_Click(object sender, EventArgs e)
        {
            if (ModMain.MyMsgBox("即将删除背景图片文件夹中的所有文件。" + Constants.vbCrLf + "此操作不可撤销，是否确定？", "警告", Button2: "取消", IsWarn: true) == 1)
            {
                DirectoryUtils.Delete(Paths.Base + @"PCL\Pictures");
                BackgroundRefresh(false, true);
                ModMain.Hint("背景图片已清空！", ModMain.HintType.Green);
            }
        }
        /// <summary>
    /// 刷新背景图片及设置页 UI。
    /// </summary>
    /// <param name="IsHint">是否显示刷新提示。</param>
    /// <param name="Refresh">是否刷新图片显示。</param>
        public static void BackgroundRefresh(bool IsHint, bool Refresh)
        {
            try
            {

                // 获取可用的图片文件
                DirectoryUtils.Create(Paths.Base + @"PCL\Pictures\");
                var Pic = new List<string>();
                foreach (var File in DirectoryUtils.EnumerateFiles(Paths.Base + @"PCL\Pictures\", true))
                {
                    string Extension = PathUtils.GetExtension(File);
                    if (Extension != "ini" && Extension != "db")
                        Pic.Add(File); // 文件夹可能会被加入 .ini 和 thumbs.db
                }
                // 加载
                if (!Pic.Any())
                {
                    if (Refresh)
                    {
                        if (ModMain.FrmMain.ImgBack.Visibility == Visibility.Collapsed)
                        {
                            if (IsHint)
                                ModMain.Hint("未检测到可用背景图片！", ModMain.HintType.Red);
                        }
                        else
                        {
                            ModMain.FrmMain.ImgBack.Visibility = Visibility.Collapsed;
                            if (IsHint)
                                ModMain.Hint("背景图片已清除！", ModMain.HintType.Green);
                        }
                    }
                    if (!(ModMain.FrmSetupUI == null))
                        ModMain.FrmSetupUI.BackgroundRefreshUI(false, 0);
                }
                else
                {
                    if (Refresh)
                    {
                        string Address = ModBase.RandomOne(Pic);
                        try
                        {
                            Logger.Info($"加载背景图片：{Address}");
                            ModMain.FrmMain.ImgBack.Background = new MyBitmap(Address);
                            ModMain.FrmMain.ImgBack.Visibility = Visibility.Visible;
                            ModMain.FrmMain.UpdateBackgroundAndTitleBar();
                            if (IsHint)
                                ModMain.Hint("背景图片已刷新：" + PathUtils.GetLastPart(Address), ModMain.HintType.Green, false);
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("参数无效"))
                            {
                                Logger.Error($"刷新背景图片失败，该图片文件可能并非标准格式。{Constants.vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{Constants.vbCrLf}文件：{Address}", LogBehavior.Alert);
                            }
                            else
                            {
                                Logger.Error(ex, $"刷新背景图片失败（{Address}）", LogBehavior.Alert);
                            }
                        }
                    }
                    if (!(ModMain.FrmSetupUI == null))
                        ModMain.FrmSetupUI.BackgroundRefreshUI(true, Pic.Count);
                }
            }

            catch (Exception ex)
            {
                Logger.Error(ex, "刷新背景图片时出现未知错误");
            }
        }

        // 顶部栏
        private void BtnLogoChange_Click(object sender, EventArgs e)
        {
            string FileName = Dialogs.SelectFile("选择图片", false, filter: new[] { (new[] { "png", "jpg", "jpeg", "gif", "webp" }, "常用图片文件") }).FirstOrDefault();
            if (string.IsNullOrEmpty(FileName))
                return;
            string TargetPath = Paths.Base + @"PCL\Logo.png";
            try
            {
                // 复制文件
                FileUtils.Copy(FileName, TargetPath);
                // 设置当前显示
                ModMain.FrmMain.ImageTitleLogo.Source = (string)null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
                ModMain.FrmMain.ImageTitleLogo.Source = TargetPath;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("参数无效"))
                {
                    Logger.Error($"改变标题栏图片失败，该图片文件可能并非标准格式。{Constants.vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{Constants.vbCrLf}文件：{TargetPath}", LogBehavior.Alert);
                }
                else
                {
                    Logger.Error(ex, "设置标题栏图片失败", LogBehavior.Alert);
                }
                ModMain.FrmMain.ImageTitleLogo.Source = (string)null;
            }
        }
        private void RadioLogoType3_Check(object sender, ModBase.RouteEventArgs e)
        {
            if (!(ModAnimation.AniControlEnabled == 0 && e.RaiseByMouse))
                return;
            Refresh:
            ;

            // 已有图片则不再选择
            string TargetPath = Paths.Base + @"PCL\Logo.png";
            if (FileUtils.Exists(TargetPath))
            {
                try
                {
                    ModMain.FrmMain.ImageTitleLogo.Source = (string)null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
                    ModMain.FrmMain.ImageTitleLogo.Source = TargetPath;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("参数无效"))
                    {
                        Logger.Error($"改变标题栏图片失败，该图片文件可能并非标准格式。{Constants.vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{Constants.vbCrLf}文件：{TargetPath}", LogBehavior.Alert);
                    }
                    else
                    {
                        Logger.Error(ex, "调整标题栏图片失败", LogBehavior.Alert);
                    }
                    ModMain.FrmMain.ImageTitleLogo.Source = (string)null;
                    e.Handled = true;
                    try
                    {
                        FileUtils.Delete(TargetPath);
                    }
                    catch (Exception exx)
                    {
                        Logger.Error(exx, "清理错误的标题栏图片失败", LogBehavior.Alert);
                    }
                }
                return;
            }
            // 没有图片则要求选择
            string FileName = Dialogs.SelectFile("选择图片", false, filter: new[] { (new[] { "png", "jpeg", "jpg", "gif", "webp" }, "常用图片文件") }).FirstOrDefault();
            if (string.IsNullOrEmpty(FileName))
            {
                ModMain.FrmMain.ImageTitleLogo.Source = (string)null;
                e.Handled = true;
            }
            else
            {
                try
                {
                    FileUtils.Copy(FileName, TargetPath);
                    goto Refresh;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "复制标题栏图片失败", LogBehavior.Alert);
                }
            }
        }
        private void BtnLogoDelete_Click(object sender, EventArgs e)
        {
            try
            {
                FileUtils.Delete(Paths.Base + @"PCL\Logo.png");
                this.RadioLogoType1.SetChecked(true, true);
                ModMain.Hint("标题栏图片已清空！", ModMain.HintType.Green);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "清空标题栏图片失败", LogBehavior.Alert);
            }
        }

        // 主页
        private void BtnCustomFile_Click(object sender, EventArgs e)
        {
            try
            {
                if (FileUtils.Exists(Paths.Base + @"PCL\Custom.xaml"))
                {
                    if (ModMain.MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn: true) == 2)
                        return;
                }
                ModBase.ExtractResources(Paths.Base + @"PCL\Custom.xaml", "Custom");
                ModMain.Hint("教学文件已生成！", ModMain.HintType.Green);
                ModBase.OpenExplorer(Paths.Base + @"PCL\Custom.xaml");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "生成教学文件失败");
            }
        }
        private void BtnCustomRefresh_Click()
        {
            ModMain.FrmLaunchRight.ForceRefresh();
            ModMain.Hint("已刷新主页！", ModMain.HintType.Green);
        }
        private void BtnCustomTutorial_Click(object sender, EventArgs e)
        {
            ModMain.MyMsgBox("1. 点击 生成教学文件 按钮，这会在 PCL 文件夹下生成 Custom.xaml 布局文件。" + Constants.vbCrLf + "2. 使用记事本等工具打开这个文件并进行修改，修改完记得保存。" + Constants.vbCrLf + "3. 点击 刷新主页 按钮，查看主页现在长啥样了。" + Constants.vbCrLf + Constants.vbCrLf + "你可以在生成教学文件后直接刷新主页，对照着进行修改，更有助于理解。" + Constants.vbCrLf + "直接将主页文件拖进 PCL 窗口也可以快捷加载。", "主页自定义教程");
        }
        public static void OnMainPageTypeChanged()
        {
            if (ModMain.FrmSetupUI is null)
                return;
            switch (Settings.Get<int>("UiCustomType"))
            {
                case 0: // 无
                    {
                        ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed;
                        break;
                    }
                case 1: // 本地
                    {
                        ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustomWarn.Visibility = Settings.Get<bool>("HintCustomWarn") ? Visibility.Collapsed : Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustom.Text = $"从 PCL 文件夹下的 Custom.xaml 读取主页内容。{Constants.vbCrLf}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。";
                        CustomEventService.SetEventType(ModMain.FrmSetupUI.HintCustom, CustomEvent.EventType.None);
                        break;
                    }
                case 2: // 联网
                    {
                        ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustomWarn.Visibility = Settings.Get<bool>("HintCustomWarn") ? Visibility.Collapsed : Visibility.Visible;
                        ModMain.FrmSetupUI.HintCustom.Text = $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。{Constants.vbCrLf}如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！";
                        CustomEventService.SetEventType(ModMain.FrmSetupUI.HintCustom, CustomEvent.EventType.打开网页);
                        CustomEventService.SetEventData(ModMain.FrmSetupUI.HintCustom, "https://github.com/Meloong-Git/PCL/discussions/2528");
                        break;
                    }
                case 3: // 预设
                    {
                        ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Visible;
                        ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed;
                        ModMain.FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed;
                        break;
                    }
            }
            ModMain.FrmSetupUI.CardCustom.TriggerForceResize();
        }
        private void BtnCustomFolder_Click(object sender, MouseButtonEventArgs e)
        {
            string CustomPath = Paths.Base + @"PCL\Custom.xaml";
            if (FileUtils.Exists(CustomPath))
            {
                ModBase.OpenExplorer(CustomPath);
            }
            else
            {
                ModBase.OpenExplorer(Paths.Base + @"PCL\");
            }
        }

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
        private void HSL_Change()
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
                if (!HiddenForceShow && Settings.Get<bool>("UiHiddenPageDownload") && Settings.Get<bool>("UiHiddenPageSetup") && Settings.Get<bool>("UiHiddenPageOther"))
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
                    ModMain.FrmMain.BtnTitleSelect4.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenPageOther") ? Visibility.Collapsed : Visibility.Visible;
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
                    ModMain.FrmSetupLeft.PanItem.Visibility = AvaliableCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
                }
                // 更多子页面
                int OtherAvaliableCount = 0;
                if (!Settings.Get<bool>("UiHiddenOtherHelp"))
                    OtherAvaliableCount += 1;
                if (!Settings.Get<bool>("UiHiddenOtherAbout"))
                    OtherAvaliableCount += 1;
                if (!Settings.Get<bool>("UiHiddenOtherTest"))
                    OtherAvaliableCount += 1;
                if (!Settings.Get<bool>("UiHiddenOtherFeedback"))
                    OtherAvaliableCount += 1;
                if (!Settings.Get<bool>("UiHiddenOtherVote"))
                    OtherAvaliableCount += 1;
                if (ModMain.FrmOtherLeft is not null)
                {
                    ModMain.FrmOtherLeft.ItemHelp.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenOtherHelp") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmOtherLeft.ItemFeedback.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenOtherFeedback") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmOtherLeft.ItemVote.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenOtherVote") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmOtherLeft.ItemAbout.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenOtherAbout") ? Visibility.Collapsed : Visibility.Visible;
                    ModMain.FrmOtherLeft.ItemTest.Visibility = !HiddenForceShow && Settings.Get<bool>("UiHiddenOtherTest") ? Visibility.Collapsed : Visibility.Visible;
                    // 隐藏左边选择卡
                    ModMain.FrmOtherLeft.PanItem.Visibility = OtherAvaliableCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
                }
                if (OtherAvaliableCount == 1 && !HiddenForceShow)
                {
                    if (!Settings.Get<bool>("UiHiddenOtherHelp"))
                    {
                        ModMain.FrmMain.BtnTitleSelect4.Text = "帮助";
                    }
                    else if (!Settings.Get<bool>("UiHiddenOtherAbout"))
                    {
                        ModMain.FrmMain.BtnTitleSelect4.Text = "关于";
                    }
                    else
                    {
                        ModMain.FrmMain.BtnTitleSelect4.Text = "百宝箱";
                    }
                }
                else
                {
                    ModMain.FrmMain.BtnTitleSelect4.Text = "更多";
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
        private void HiddenOtherMain()
        {
            // 更多主页面
            if (this.CheckHiddenPageOther.Checked)
            {
                // 开启
                this.CheckHiddenOtherAbout.Checked = true;
                this.CheckHiddenOtherTest.Checked = true;
                this.CheckHiddenOtherFeedback.Checked = true;
                this.CheckHiddenOtherVote.Checked = true;
                this.CheckHiddenOtherHelp.Checked = true;
            }
            // 关闭
            else if (Settings.Get<bool>("UiHiddenOtherHelp") && Settings.Get<bool>("UiHiddenOtherAbout") && Settings.Get<bool>("UiHiddenOtherTest") && Settings.Get<bool>("UiHiddenOtherVote") && Settings.Get<bool>("UiHiddenOtherFeedback"))
            {
                this.CheckHiddenOtherAbout.Checked = false;
                this.CheckHiddenOtherTest.Checked = false;
                this.CheckHiddenOtherFeedback.Checked = false;
                this.CheckHiddenOtherVote.Checked = false;
                this.CheckHiddenOtherHelp.Checked = false;
            }
        }
        private void HiddenOtherSub(object sender, bool user)
        {
            // 更多子页面（有具体内容的）
            if (Settings.Get<bool>("UiHiddenOtherHelp") && Settings.Get<bool>("UiHiddenOtherAbout") && Settings.Get<bool>("UiHiddenOtherTest"))
            {
                // 已被全部隐藏
                this.CheckHiddenPageOther.Checked = true;
            }
            else
            {
                // 未被全部隐藏
                this.CheckHiddenPageOther.Checked = false;
            }
            // 修改无具体内容的项
            if (!user)
                return;
            if (Settings.Get<bool>("UiHiddenOtherHelp") && Settings.Get<bool>("UiHiddenOtherAbout") && Settings.Get<bool>("UiHiddenOtherTest"))
            {
                this.CheckHiddenOtherFeedback.Checked = true;
                this.CheckHiddenOtherVote.Checked = true;
            }
        }
        private void HiddenOtherNet(object sender, bool user)
        {
            // 更多子页面（无具体内容的）
            if (!user)
                return;
            if (Settings.Get<bool>("UiHiddenOtherHelp") && Settings.Get<bool>("UiHiddenOtherAbout") && Settings.Get<bool>("UiHiddenOtherTest") && (!Settings.Get<bool>("UiHiddenOtherFeedback") || !Settings.Get<bool>("UiHiddenOtherVote")))
            {
                this.CheckHiddenOtherAbout.Checked = false;
                this.CheckHiddenOtherTest.Checked = false;
                this.CheckHiddenOtherHelp.Checked = false;
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
            this.SliderLauncherTransparent.GetHintText = new Func<object, object>(v => Math.Round(40 + Conversions.ToDouble(v) * 0.1d) + "%");
            this.SliderLauncherHue.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(v, "°"));
            this.SliderLauncherSat.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(v, "%"));
            this.SliderLauncherDelta.GetHintText = new Func<int, string>((Value) => { if (Value > 90) { return "+" + (Value - 90); } else if (Value == 90) { return 0.ToString(); } else { return (Value - 90).ToString(); } });
            this.SliderLauncherLight.GetHintText = new Func<int, string>((Value) => { if (Value > 20) { return "+" + (Value - 20); } else if (Value == 20) { return 0.ToString(); } else { return (Value - 20).ToString(); } });
            this.SliderBackgroundOpacity.GetHintText = new Func<object, object>(v => Math.Round(Conversions.ToDouble(v) * 0.1d) + "%");
            this.SliderBackgroundBlur.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(v, " 像素"));
        }

    }
}