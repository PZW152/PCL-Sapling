
namespace PCL
{
    public partial class PageSpeedRight
    {
        public PageSpeedRight()
        {
            this.Loaded += (_, __) => Init();
            this.InitializeComponent();
        }

        private void Init()
        {
            this.PanBack.ScrollToHome();
        }

    }
}