using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MeloongCore;
using MeloongCore.Extensions;
using PCLCS;

namespace PCL
{
    /// <summary>
    /// 启动页的右侧：主角卡（取自 Sapling）。
    /// 显示当前实例的名字、大号版本号、加载器与游玩时间标签，以及“继续玩”（最近的世界与服务器，点一下直接进入）。
    /// </summary>
    public partial class PageLaunchRight : IRefreshable
    {
        /// <summary>
        /// “继续玩”最多放几张卡片。
        /// </summary>
        private const int TileLimit = 3;

        public PageLaunchRight()
        {
            this.Loaded += (_, __) => Init();
            this.InitializeComponent();
            this.PanBack.SizeChanged += (_, __) => FitHero();
        }

        private void Init()
        {
            this.PanBack.ScrollToHome();
            this.PanLog.Visibility = ModBase.ModeDebug.ToVisibility();
            RefreshHero();
        }

        /// <summary>
        /// 主角卡至少占满一屏，内容少时“继续玩”贴在底部。
        /// </summary>
        private void FitHero()
        {
            double Height = this.PanBack.ActualHeight - this.PanMain.Margin.Top - this.PanMain.Margin.Bottom - 15;
            this.PanHero.MinHeight = Math.Max(320, Height);
        }

        #region 主角卡

        private string HeroInstancePath = null;

        /// <summary>
        /// 按当前选择的实例刷新主角卡。选择变化、回到启动页、游戏结束时调用。
        /// </summary>
        public void RefreshHero()
        {
            if (!this.IsLoaded)
                return;
            var Instance = ModMinecraft.McInstanceSelected;
            HeroInstancePath = Instance?.PathVersion;
            this.PanHeroTags.Children.Clear();
            this.PanHeroTiles.Children.Clear();
            if (Instance is not null && !Instance.IsLoaded)
            {
                try
                {
                    Instance.Load(); // 从缓存读取的版本列表里，实例可能还没有加载
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "加载首页实例信息失败");
                }
            }
            if (Instance is null)
            {
                // 还没有任何版本：欢迎语
                this.LabHeroName.Text = "欢迎使用 PCL 树苗修改版";
                this.LabHeroName.Visibility = Visibility.Visible;
                this.BoxHeroVersion.Visibility = Visibility.Collapsed;
                this.LabHeroWelcome.Visibility = Visibility.Visible;
                this.LabHeroContinue.Visibility = Visibility.Collapsed;
                return;
            }
            this.LabHeroWelcome.Visibility = Visibility.Collapsed;
            this.BoxHeroVersion.Visibility = Visibility.Visible;
            this.LabHeroName.Text = Instance.Name;
            this.LabHeroName.ToolTip = Instance.Name;
            // 大号版本号：版本号太长时缩小字号
            string Version = Instance.State == ModMinecraft.McInstance.McInstanceState.Error || string.IsNullOrEmpty(Instance.Version?.VanillaName) || Instance.Version.VanillaName == "Unknown" ? Instance.Name : Instance.Version.VanillaName;
            this.LabHeroVersion.Text = Version;
            this.LabHeroName.Visibility = Version == Instance.Name ? Visibility.Collapsed : Visibility.Visible; // 名字就是版本号时不重复显示
            this.LabHeroVersion.FontSize = Version.Length <= 8 ? 76 : Version.Length <= 14 ? 56 : 40;
            // 标签
            foreach (string Tag in HeroTags(Instance))
                this.PanHeroTags.Children.Add(MakeTag(Tag));
            // 继续玩
            RefreshTiles(Instance);
        }

        /// <summary>
        /// 版本类型（正式版不显示）、加载器、游玩时间。
        /// </summary>
        private static List<string> HeroTags(ModMinecraft.McInstance Instance)
        {
            var Tags = new List<string>();
            if (Instance.State == ModMinecraft.McInstance.McInstanceState.Error)
            {
                Tags.Add("无法启动：" + Instance.Info);
                return Tags;
            }
            string[] Parts = Instance.VersionDisplayName().Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            if (Parts.Length > 0)
            {
                string Kind = Parts[0].Split(' ')[0];
                if (Kind != "正式版")
                    Tags.Add(Kind);
                Tags.AddRange(Parts.Skip(1));
            }
            try
            {
                string IniPath = Instance.PathVersion + @"PCL\Setup.ini";
                long Seconds = Coerce.ToLong(ModBase.ReadIni(IniPath, "PlayTimeSeconds", "0"));
                long LastTicks = Coerce.ToLong(ModBase.ReadIni(IniPath, "LastPlayed", "0"));
                string Total = PlayTime.FormatTotal(TimeSpan.FromSeconds(Seconds));
                if (Total != "")
                    Tags.Add("玩了 " + Total);
                if (LastTicks > 0)
                {
                    string Last = PlayTime.FormatLastPlayed(new DateTime(LastTicks), DateTime.Now);
                    Tags.Add(Last.Contains("/") ? Last + " 玩过" : Last + "玩过");
                }
                else
                {
                    Tags.Add("还没玩过");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取游玩时间失败");
            }
            return Tags;
        }

        private static Border MakeTag(string Text) => new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(12, 4, 12, 5),
            Margin = new Thickness(0, 6, 8, 0),
            Child = new TextBlock { Text = Text, FontSize = 13.5, Foreground = Brushes.White }
        };

        /// <summary>
        /// “继续玩”：最近的单人世界优先，空位放游戏里保存的服务器。
        /// 1.20 以前的版本不能直接进入世界，只放服务器。
        /// </summary>
        private void RefreshTiles(ModMinecraft.McInstance Instance)
        {
            List<SavedWorld> Worlds = new();
            List<SavedServer> Servers = new();
            try
            {
                if (Instance.ReleaseTime > new DateTime(2023, 4, 4))
                    Worlds = SavedGames.RecentWorlds(Instance.PathIndie, TileLimit);
                Servers = SavedGames.ReadServers(Instance.PathIndie).Take(TileLimit - Worlds.Count).ToList();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取存档与服务器失败");
            }
            int Index = 0;
            foreach (var World in Worlds)
            {
                string Icon = Path.Combine(Instance.PathIndie, "saves", World.Folder, "icon.png");
                this.PanHeroTiles.Children.Add(MakeTile(World.Folder, PlayTime.FormatLastPlayed(World.LastPlayed, DateTime.Now), File.Exists(Icon) ? Icon : null, Index++,
                    () => ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions { Instance = Instance, WorldFolder = World.Folder })));
            }
            foreach (var Server in Servers)
            {
                string Name = string.IsNullOrWhiteSpace(Server.Name) ? Server.Address : Server.Name;
                this.PanHeroTiles.Children.Add(MakeTile(Name, "服务器", null, Index++,
                    () => ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions { Instance = Instance, ServerIp = Server.Address })));
            }
            this.LabHeroContinue.Visibility = Index > 0 ? Visibility.Visible : Visibility.Collapsed;
            this.PanHeroTiles.Columns = TileLimit;
        }

        /// <summary>
        /// 一张“继续玩”卡片：世界图标（没有时用名字的第一个字）、名字、说明。悬停变亮，按下缩一下。
        /// </summary>
        private FrameworkElement MakeTile(string Title, string Info, string IconPath, int Index, Action OnClick)
        {
            // 图标
            FrameworkElement Icon;
            var IconBox = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), ClipToBounds = true };
            BitmapImage Image = IconPath is null ? null : LoadIcon(IconPath);
            if (Image is not null)
            {
                Icon = new Image { Source = Image, Stretch = Stretch.UniformToFill };
                RenderOptions.SetBitmapScalingMode(Icon, BitmapScalingMode.NearestNeighbor);
                IconBox.Child = Icon;
            }
            else
            {
                IconBox.Child = new TextBlock { Text = Title.Length > 0 ? char.IsSurrogate(Title[0]) ? Title.Substring(0, 2) : Title.Substring(0, 1) : "?", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }
            // 文字
            var Texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            Texts.Children.Add(new TextBlock { Text = Title, FontSize = 14.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis });
            Texts.Children.Add(new TextBlock { Text = Info, FontSize = 12.5, Foreground = new SolidColorBrush(Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            var Row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(IconBox, Dock.Left);
            Row.Children.Add(IconBox);
            Row.Children.Add(Texts);
            // 卡片
            var IdleBrush = Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
            var HoverBrush = Color.FromArgb(0x3D, 0xFF, 0xFF, 0xFF);
            var Background = new SolidColorBrush(IdleBrush);
            var Tile = new Border
            {
                Background = Background,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10),
                Margin = new Thickness(5, 0, 5, 0),
                Cursor = Cursors.Hand,
                Child = Row,
                ToolTip = Info == "服务器" ? "进入服务器 " + Title : "进入世界 " + Title,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
                Opacity = 0
            };
            Tile.MouseEnter += (_, __) => Background.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(HoverBrush, TimeSpan.FromMilliseconds(140)));
            Tile.MouseLeave += (_, __) =>
            {
                Background.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(IdleBrush, TimeSpan.FromMilliseconds(140)));
                ModAnimation.AniStart(ModAnimation.AaScaleTransform(Tile, 1d - ((ScaleTransform)Tile.RenderTransform).ScaleX, 300, Ease: new ModAnimation.AniEaseOutFluent()), "HeroTile Scale " + Index);
            };
            Tile.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                ModAnimation.AniStart(ModAnimation.AaScaleTransform(Tile, 0.96d - ((ScaleTransform)Tile.RenderTransform).ScaleX, 90, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)), "HeroTile Scale " + Index);
            };
            Tile.MouseLeftButtonUp += (_, e) =>
            {
                if (((ScaleTransform)Tile.RenderTransform).ScaleX >= 0.999d)
                    return; // 不是在这张卡片上按下的
                ModAnimation.AniStart(ModAnimation.AaScaleTransform(Tile, 1d - ((ScaleTransform)Tile.RenderTransform).ScaleX, 420, Ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)), "HeroTile Scale " + Index);
                if (ModLaunch.McLaunchLoader.State != ModBase.LoadState.Loading)
                    OnClick();
            };
            // 依次出现
            ModAnimation.AniStart(ModAnimation.AaOpacity(Tile, 1, 260, 60 + Index * 50), "HeroTile Enter " + Index);
            return Tile;
        }

        private static BitmapImage LoadIcon(string FilePath)
        {
            try
            {
                var Image = new BitmapImage();
                Image.BeginInit();
                Image.CacheOption = BitmapCacheOption.OnLoad; // 不占用文件，游戏里还能改图标
                Image.UriSource = new Uri(FilePath);
                Image.DecodePixelWidth = 64;
                Image.EndInit();
                Image.Freeze();
                return Image;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取世界图标失败：" + FilePath);
                return null;
            }
        }

        #endregion

        /// <summary>
        /// 回到启动页并滚动到顶部。
        /// </summary>
        public void ForceRefresh()
        {
            if (ModMain.FrmMain.PageCurrent.Page == FormMain.PageType.Launch)
            {
                this.PanBack.ScrollToHome();
                RefreshHero();
            }
            else
            {
                ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.Launch);
            }
        }

        void IRefreshable.Refresh() => ForceRefresh();
    }
}
