using System;
using System.Windows;
using MeloongCore;
using MeloongCore.Extensions;

namespace PCL
{
    /// <summary>
    /// 启动页的右侧。原版 PCL 的自定义主页（Custom.xaml / 联网主页）已移除，这里暂时只在调试模式下显示启动日志。
    /// </summary>
    public partial class PageLaunchRight : IRefreshable
    {
        public PageLaunchRight()
        {
            this.Loaded += (_, __) => Init();
            this.InitializeComponent();
        }

        private void Init()
        {
            this.PanBack.ScrollToHome();
            this.PanLog.Visibility = ModBase.ModeDebug.ToVisibility();
        }

        /// <summary>
        /// 回到启动页并滚动到顶部。
        /// </summary>
        public void ForceRefresh()
        {
            if (ModMain.FrmMain.PageCurrent.Page == FormMain.PageType.Launch)
                this.PanBack.ScrollToHome();
            else
                ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Launch);
        }

        void IRefreshable.Refresh() => ForceRefresh();
    }
}
