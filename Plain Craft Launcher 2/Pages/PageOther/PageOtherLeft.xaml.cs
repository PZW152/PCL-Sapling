using System;
using System.Windows;
using System.Windows.Controls;
using MeloongCore;
using Microsoft.VisualBasic;

namespace PCL
{
    public partial class PageOtherLeft
    {

        private bool IsLoad = false;
        private bool IsPageSwitched = false; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次
        private void PageOtherLeft_Loaded(object sender, RoutedEventArgs e)
        {
            // 是否处于隐藏的子页面
            bool IsHiddenPage = false;
            if (this.ItemHelp.Checked && Settings.Get<bool>("UiHiddenOtherHelp"))
                IsHiddenPage = true;
            if (this.ItemAbout.Checked && Settings.Get<bool>("UiHiddenOtherAbout"))
                IsHiddenPage = true;
            if (PageSetupUI.HiddenForceShow)
                IsHiddenPage = false;
            // 若页面错误，或尚未加载，则继续
            if (IsLoad && !IsHiddenPage)
                return;
            IsLoad = true;
            // 刷新子页面隐藏情况
            PageSetupUI.HiddenRefresh();
            // 选择第一个未被禁用的子页面
            if (IsPageSwitched)
                return;
            if (!Settings.Get<bool>("UiHiddenOtherHelp"))
            {
                this.ItemHelp.SetChecked(true, false, false);
            }
            else
            {
                this.ItemAbout.SetChecked(true, false, false);
            }
        }
        private void PageOtherLeft_Unloaded(object sender, RoutedEventArgs e)
        {
            IsPageSwitched = false;
        }

        #region 页面切换

        /// <summary>
    /// 当前页面的编号。从 0 开始计算。
    /// </summary>
        public FormMain.PageSubType PageID;
        public PageOtherLeft()
        {
            this.InitializeComponent();
            // 选择第一个未被禁用的子页面
            if (!Settings.Get<bool>("UiHiddenOtherHelp"))
            {
                PageID = FormMain.PageSubType.OtherHelp;
            }
            else
            {
                PageID = FormMain.PageSubType.OtherAbout;
            }

            this.Loaded += PageOtherLeft_Loaded;
            this.Unloaded += PageOtherLeft_Unloaded;
        }

        /// <summary>
    /// 勾选事件改变页面。
    /// </summary>
        private void PageCheck(FrameworkElement sender, ModBase.RouteEventArgs e)
        {
            // 尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
            // 若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
            if (sender.Tag is not null)
                PageChange((FormMain.PageSubType)ModBase.Val(sender.Tag));
        }

        public object PageGet(FormMain.PageSubType ID = (FormMain.PageSubType)(-1))
        {
            if ((int)ID == -1)
                ID = PageID;
            switch (ID)
            {
                case FormMain.PageSubType.OtherHelp:
                    {
                        if (ModMain.FrmOtherHelp is null)
                            ModMain.FrmOtherHelp = new PageOtherHelp();
                        return ModMain.FrmOtherHelp;
                    }
                case FormMain.PageSubType.OtherAbout:
                    {
                        if (ModMain.FrmOtherAbout is null)
                            ModMain.FrmOtherAbout = new PageOtherAbout();
                        return ModMain.FrmOtherAbout;
                    }

                default:
                    {
                        throw new Exception("未知的更多子页面种类：" + ((int)ID).ToString());
                    }
            }
        }

        /// <summary>
    /// 切换现有页面。
    /// </summary>
        public void PageChange(FormMain.PageSubType ID)
        {
            if (PageID == ID)
                return;
            ModAnimation.AniControlEnabled += 1;
            IsPageSwitched = true;
            try
            {
                PageChangeRun((MyPageRight)PageGet(ID));
                PageID = ID;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"切换分页面失败（ID {ID}）");
            }
            finally
            {
                ModAnimation.AniControlEnabled -= 1;
            }
        }
        private static void PageChangeRun(MyPageRight Target)
        {
            ModAnimation.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
            if (Target.Parent is not null)
                Target.SetValue(ContentPresenter.ContentProperty, null);
            ModMain.FrmMain.PageRight = Target;
            ((MyPageRight)ModMain.FrmMain.PanMainRight.Child).PageOnExit();
            ModAnimation.AniStart(new[] {
                         ModAnimation.AaCode(() =>
                {
                                    ((MyPageRight)ModMain.FrmMain.PanMainRight.Child).PageOnForceExit();
                                    ModMain.FrmMain.PanMainRight.Child = ModMain.FrmMain.PageRight;
                                    ModMain.FrmMain.PageRight.Opacity = 0d;
                                }, 130),
                         ModAnimation.AaCode(() =>
                {
                                    // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                                    ModMain.FrmMain.PageRight.Opacity = 1d;
                                    ModMain.FrmMain.PageRight.PageOnEnter();
                                }, 30, true)
                     }, "PageLeft PageChange");
        }

        #endregion

        // 强制刷新
        public void Refresh(object sender, EventArgs e) // 由边栏按钮匿名调用
        {
            switch (ModBase.Val(sender.Fe().Tag))
            {
                case (double)FormMain.PageSubType.OtherHelp:
                    {
                        RefreshHelp();
                        this.ItemHelp.Checked = true;
                        break;
                    }
            }
            ModMain.Hint("正在刷新……", Log: false);
        }
        public static void RefreshHelp()
        {
            Settings.Set("SystemHelpVersion", 0); // 强制重新解压文件
            ModMain.FrmOtherHelp.PageLoaderRestart();
            ModMain.FrmOtherHelp.SearchBox.Text = "";
        }


    }
}