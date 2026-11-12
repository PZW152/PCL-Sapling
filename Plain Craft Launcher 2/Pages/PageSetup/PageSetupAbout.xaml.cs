using System.Windows;
using System.Windows.Input;
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

    }
}