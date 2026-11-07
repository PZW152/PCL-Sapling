using System.Windows;
using System.Windows.Input;
using MeloongCore.Extensions;

namespace PCL
{
    public partial class PageOtherAbout
    {

        private new bool IsLoaded = false;

        public PageOtherAbout()
        {
            this.Loaded += PageOtherAbout_Loaded;
            this.InitializeComponent();

        }
        private void PageOtherAbout_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();

            // 非重复加载部分
            if (IsLoaded)
                return;
            IsLoaded = true;

            this.ItemAboutPcl.Info = this.ItemAboutPcl.Info.Replace("%VERSION%", ModBase.VersionDisplay).Replace("%VERSIONCODE%", ModBase.VersionCode.ToString()).Replace("%BRANCH%", ((int)ModBase.BuildType).ToString());

        }

        private void BtnAboutUpdate_Click(object sender, MouseButtonEventArgs e)
        {
            ModSecret.UpdateCheckByButton();
        }

    }
}