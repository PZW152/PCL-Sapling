using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MeloongCore;
using MeloongCore.Extensions;
using PCLCS;

namespace PCL
{
    /// <summary>
    /// 启动页的右侧（设计方向 B「轻主角」，取自 Sapling）：
    /// 主角卡显示当前实例的名字、主题蓝的大号版本号、标签与“继续玩”，下方是最近截图、游戏日志。
    /// </summary>
    public partial class PageLaunchRight : IRefreshable
    {
        /// <summary>
        /// “继续玩”最多放几张卡片。
        /// </summary>
        private const int TileLimit = 3;
        /// <summary>
        /// 最近截图最多放几张。
        /// </summary>
        private const int ShotLimit = 4;
        /// <summary>
        /// PCL 版本设置侧栏中“Mod 管理”的图标。
        /// </summary>
        private const string ModLogo = "M789.504 912.896h-195.072a35.328 35.328 0 0 1-34.816-35.328v-77.312a39.936 39.936 0 0 0-40.448-39.936H481.28a40.448 40.448 0 0 0-40.448 39.936v77.312a34.816 34.816 0 0 1-34.816 35.328H204.8a41.984 41.984 0 0 1-40.448-42.496v-200.192a35.328 35.328 0 0 1 35.328-35.328h72.704a39.936 39.936 0 0 0 39.936-39.936v-37.888a39.936 39.936 0 0 0-39.936-40.448H199.68a35.328 35.328 0 0 1-35.328-35.328V287.744A41.984 41.984 0 0 1 204.8 245.76h176.64v-32.768a102.4 102.4 0 0 1 102.4-102.4h33.792a102.4 102.4 0 0 1 102.4 102.4v32.768h170.496a41.984 41.984 0 0 1 41.984 41.984V460.8h28.672a102.4 102.4 0 0 1 102.4 102.4v33.792a102.4 102.4 0 0 1-102.4 102.4h-28.672V870.4a41.984 41.984 0 0 1-43.008 42.496z m-159.744-70.144h131.584V665.6a34.304 34.304 0 0 1 34.816-34.816h64a31.744 31.744 0 0 0 31.744-31.744V563.2a31.744 31.744 0 0 0-31.744-31.744h-64a34.816 34.816 0 0 1-34.816-35.328V316.416h-177.152a35.328 35.328 0 0 1-35.328-35.328V212.992a31.744 31.744 0 0 0-31.744-31.744h-33.792a31.744 31.744 0 0 0-31.744 31.744v68.096a35.328 35.328 0 0 1-34.816 35.328H234.496v130.048h37.888a110.592 110.592 0 0 1 110.08 110.592v37.888a110.592 110.592 0 0 1-110.08 110.592h-37.888v137.216h136.192v-42.496a110.592 110.592 0 0 1 110.592-110.08h37.888a110.592 110.592 0 0 1 110.592 110.08z";
        /// <summary>
        /// “继续玩”卡片中没有世界图标时，按顺序使用的底色（低饱和的草绿、陶土橙、天蓝）。
        /// </summary>
        private static readonly Color[] TileColors = { Color.FromRgb(0x5B, 0x9E, 0x4A), Color.FromRgb(0xC9, 0x87, 0x3C), Color.FromRgb(0x4C, 0x7F, 0xC9) };

        public PageLaunchRight()
        {
            this.Loaded += (_, __) => Init();
            this.Unloaded += (_, __) => LogTimer.Stop();
            this.InitializeComponent();
            LogTimer.Tick += (_, __) => RefreshGameLog();
            this.BtnHeroFolder.Logo = ModBase.Logo.IconButtonOpen;
            this.BtnHeroMods.Logo = ModLogo;
            this.BtnHeroFolder.Click += (_, __) =>
            {
                var Instance = ModMinecraft.McInstanceSelected;
                if (Instance is not null)
                    ModBase.OpenExplorer(Instance.PathIndie);
            };
            this.BtnHeroMods.Click += (_, __) => OpenInstancePage(FormMain.PageSubType.InstanceMod);
            this.BtnHeroAllSaves.Click += (_, __) => OpenInstancePage(FormMain.PageSubType.InstanceOverall);
            this.BtnShotsAll.Click += (_, __) => OpenInstancePage(FormMain.PageSubType.InstanceScreenshot);
            this.BtnGameLogFolder.Click += (_, __) =>
            {
                var Instance = LogWatcher?.Instance ?? ModMinecraft.McInstanceSelected;
                if (Instance is not null)
                    ModBase.OpenExplorer(Instance.PathIndie + @"logs\");
            };
            this.BtnGameLogCopy.Click += (_, __) =>
            {
                ModBase.ClipboardSet(this.TextGameLog.Text, false);
                ModMain.Hint("已复制游戏日志", ModMain.HintType.Green);
            };
        }

        private void Init()
        {
            this.PanBack.ScrollToHome();
            this.PanLog.Visibility = ModBase.ModeDebug.ToVisibility();
            RefreshHero();
            RefreshGameLog();
            LogTimer.Start();
        }

        /// <summary>
        /// 打开当前实例的版本设置中的某个子页面。
        /// </summary>
        private static void OpenInstancePage(FormMain.PageSubType Sub)
        {
            var Instance = ModMinecraft.McInstanceSelected;
            if (Instance is null)
                return;
            PageInstanceLeft.Instance = Instance;
            ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSetup, Sub);
        }

        #region 游戏日志

        private readonly System.Windows.Threading.DispatcherTimer LogTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
        private ModWatcher.Watcher LogWatcher = null;
        private string LogLastLine = null;
        private int LogLastCount = -1;

        /// <summary>
        /// 显示当前实例最近一次运行的输出（最多 500 行），游戏运行中在主角卡右上角标出。
        /// </summary>
        private void RefreshGameLog()
        {
            var Instance = ModMinecraft.McInstanceSelected;
            var Watcher = ModWatcher.McWatcherList.LastOrDefault(w => Instance is not null && w.Instance?.PathVersion == Instance.PathVersion);
            bool Running = Watcher is not null && (Watcher.State == ModWatcher.Watcher.MinecraftState.Loading || Watcher.State == ModWatcher.Watcher.MinecraftState.Running);
            this.PanHeroRunning.Visibility = Running ? Visibility.Visible : Visibility.Collapsed;
            if (Running)
                this.LabHeroRunning.Text = Watcher.State == ModWatcher.Watcher.MinecraftState.Loading ? "游戏加载中" : "游戏运行中";
            if (Watcher is null)
            {
                this.CardGameLog.Visibility = Visibility.Collapsed;
                LogWatcher = null;
                return;
            }
            this.CardGameLog.Visibility = Visibility.Visible;
            this.CardGameLog.Title = Running ? "游戏日志" : Watcher.State == ModWatcher.Watcher.MinecraftState.Crashed ? "游戏日志（游戏崩溃了）" : "游戏日志（游戏已退出）";
            string[] Lines = Watcher.LatestLog.ToArray();
            string Last = Lines.LastOrDefault();
            if (ReferenceEquals(Watcher, LogWatcher) && Lines.Length == LogLastCount && Last == LogLastLine)
                return;
            LogWatcher = Watcher;
            LogLastCount = Lines.Length;
            LogLastLine = Last;
            bool AtEnd = this.TextGameLog.VerticalOffset + this.TextGameLog.ViewportHeight >= this.TextGameLog.ExtentHeight - 4;
            this.TextGameLog.Text = Lines.Length == 0 ? "还没有输出……" : string.Join(ModBase.CrLf, Lines);
            if (AtEnd)
                this.TextGameLog.ScrollToEnd(); // 用户往上翻时不打扰
        }

        #endregion

        #region 主角卡

        /// <summary>
        /// 按当前选择的实例刷新主角卡与最近截图。选择变化、回到启动页、游戏结束时调用。
        /// </summary>
        public void RefreshHero()
        {
            if (!this.IsLoaded)
                return;
            var Instance = ModMinecraft.McInstanceSelected;
            this.PanHeroTags.Children.Clear();
            this.PanHeroTiles.Children.Clear();
            this.PanShots.Children.Clear();
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
                this.LabHeroName.Text = "欢迎使用 PCL 樹苗修改版";
                this.LabHeroKind.Text = "";
                this.BoxHeroVersion.Visibility = Visibility.Collapsed;
                this.LabHeroWelcome.Visibility = Visibility.Visible;
                this.PanHeroContinue.Visibility = Visibility.Collapsed;
                this.PanHeroTiles.Visibility = Visibility.Collapsed;
                this.BtnHeroFolder.Visibility = Visibility.Collapsed;
                this.BtnHeroMods.Visibility = Visibility.Collapsed;
                this.CardShots.Visibility = Visibility.Collapsed;
                return;
            }
            this.LabHeroWelcome.Visibility = Visibility.Collapsed;
            this.BoxHeroVersion.Visibility = Visibility.Visible;
            this.BtnHeroFolder.Visibility = Visibility.Visible;
            this.BtnHeroMods.Visibility = Instance.Modable ? Visibility.Visible : Visibility.Collapsed;
            this.LabHeroName.Text = Instance.Name;
            this.LabHeroName.ToolTip = Instance.Name;
            bool Broken = Instance.State == ModMinecraft.McInstance.McInstanceState.Error || string.IsNullOrEmpty(Instance.Version?.VanillaName) || Instance.Version.VanillaName == "Unknown";
            string Version = Broken ? "无法启动" : Instance.Version.VanillaName;
            this.LabHeroVersion.Text = Version;
            if (Version == Instance.Name)
                this.LabHeroName.Text = "Minecraft"; // 名字就是版本号时，不在上面再写一遍
            this.LabHeroVersion.FontSize = Version.Length <= 10 ? 56 : Version.Length <= 18 ? 42 : 32;
            // 名字旁边的灰字：版本类型
            string[] Parts = Broken ? Array.Empty<string>() : Instance.VersionDisplayName().Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            this.LabHeroKind.Text = Parts.Length > 0 ? Parts[0].Split(' ')[0] : "";
            // 标签：加载器、游玩时间
            foreach (string Tag in Broken ? new List<string> { Instance.Info } : HeroTags(Instance, Parts))
                this.PanHeroTags.Children.Add(MakeTag(Tag));
            RefreshTiles(Instance);
            RefreshShots(Instance);
        }

        /// <summary>
        /// 加载器（每个一个标签）、累计游玩时间、上次游玩。
        /// </summary>
        private static List<string> HeroTags(ModMinecraft.McInstance Instance, string[] Parts)
        {
            var Tags = Parts.Skip(1).ToList();
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

        private static Border MakeTag(string Text)
        {
            var Label = new TextBlock { Text = Text, FontSize = 12.5 };
            Label.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushChipText");
            var Tag = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 3, 11, 4), Margin = new Thickness(0, 6, 8, 0), Child = Label };
            Tag.SetResourceReference(Border.BackgroundProperty, "ColorBrushChip");
            return Tag;
        }

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
                this.PanHeroTiles.Children.Add(MakeTile(World.Folder, PlayTime.FormatLastPlayed(World.LastPlayed, DateTime.Now), File.Exists(Icon) ? Icon : null, Index++, "进入世界 " + World.Folder,
                    () => ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions { Instance = Instance, WorldFolder = World.Folder })));
            }
            foreach (var Server in Servers)
            {
                string Name = string.IsNullOrWhiteSpace(Server.Name) ? Server.Address : Server.Name;
                this.PanHeroTiles.Children.Add(MakeTile(Name, "服务器", null, Index++, "进入服务器 " + Server.Address,
                    () => ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions { Instance = Instance, ServerIp = Server.Address })));
            }
            // 不足三张时用空位占住，卡片宽度保持一致
            for (int i = Index; i < TileLimit; i++)
                this.PanHeroTiles.Children.Add(new Border());
            var Shown = Index > 0 ? Visibility.Visible : Visibility.Collapsed;
            this.PanHeroContinue.Visibility = Shown;
            this.PanHeroTiles.Visibility = Shown;
        }

        /// <summary>
        /// 一张“继续玩”卡片：世界图标（没有时用名字的第一个字）、名字、说明。悬停变成浅主题色，按下缩一下。
        /// </summary>
        private FrameworkElement MakeTile(string Title, string Info, string IconPath, int Index, string ToolTip, Action OnClick)
        {
            // 图标
            var IconBox = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(5), ClipToBounds = true, Background = new SolidColorBrush(TileColors[Index % TileColors.Length]) };
            BitmapImage Image = IconPath is null ? null : LoadImage(IconPath, 64);
            if (Image is not null)
            {
                var Icon = new Image { Source = Image, Stretch = Stretch.UniformToFill };
                RenderOptions.SetBitmapScalingMode(Icon, BitmapScalingMode.NearestNeighbor);
                IconBox.Child = Icon;
            }
            else
            {
                IconBox.Child = new TextBlock { Text = Title.Length == 0 ? "?" : char.IsSurrogate(Title[0]) ? Title.Substring(0, 2) : Title.Substring(0, 1).ToUpperInvariant(), FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }
            // 文字
            var Name = new TextBlock { Text = Title, FontSize = 13.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            Name.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushText");
            var Sub = new TextBlock { Text = Info, FontSize = 12, Margin = new Thickness(0, 1, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            Sub.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushTextMuted");
            var Texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            Texts.Children.Add(Name);
            Texts.Children.Add(Sub);
            var Row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(IconBox, Dock.Left);
            Row.Children.Add(IconBox);
            Row.Children.Add(Texts);
            // 卡片：颜色从主题资源取，悬停时动画过渡
            Color Idle = (Color)FindResource("ColorObjectTile"), Hover = (Color)FindResource("ColorObjectTileHover");
            Color IdleBorder = (Color)FindResource("ColorObjectTileBorder"), HoverBorder = (Color)FindResource("ColorObjectTileHoverBorder");
            var Fill = new SolidColorBrush(Idle);
            var Stroke = new SolidColorBrush(IdleBorder);
            var Tile = new Border
            {
                Background = Fill,
                BorderBrush = Stroke,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 8, 9, 8),
                Margin = new Thickness(5, 0, 5, 0),
                Cursor = Cursors.Hand,
                Child = Row,
                ToolTip = ToolTip,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
                Opacity = 0
            };
            void Fade(Color To, Color ToBorder)
            {
                Fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(To, TimeSpan.FromMilliseconds(140)));
                Stroke.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(ToBorder, TimeSpan.FromMilliseconds(140)));
            }
            Tile.MouseEnter += (_, __) => Fade(Hover, HoverBorder);
            Tile.MouseLeave += (_, __) =>
            {
                Fade(Idle, IdleBorder);
                ModAnimation.AniStart(ModAnimation.AaScaleTransform(Tile, 1d - ((ScaleTransform)Tile.RenderTransform).ScaleX, 300, Ease: new ModAnimation.AniEaseOutFluent()), "HeroTile Scale " + Index);
            };
            Tile.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                ModAnimation.AniStart(ModAnimation.AaScaleTransform(Tile, 0.965d - ((ScaleTransform)Tile.RenderTransform).ScaleX, 90, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)), "HeroTile Scale " + Index);
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
            ModAnimation.AniStart(ModAnimation.AaOpacity(Tile, 1, 240, 40 + Index * 50), "HeroTile Enter " + Index);
            return Tile;
        }

        #endregion

        #region 最近截图

        /// <summary>
        /// 最近的几张截图，点一下打开截图页面。没有截图时整张卡片隐藏。
        /// </summary>
        private void RefreshShots(ModMinecraft.McInstance Instance)
        {
            List<string> Files;
            try
            {
                string Folder = Instance.PathIndie + @"screenshots\";
                Files = Directory.Exists(Folder)
                    ? new DirectoryInfo(Folder).EnumerateFiles("*.png").OrderByDescending(f => f.LastWriteTime).Take(ShotLimit).Select(f => f.FullName).ToList()
                    : new List<string>();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取截图失败");
                Files = new List<string>();
            }
            this.CardShots.Visibility = Files.Any() ? Visibility.Visible : Visibility.Collapsed;
            int Index = 0;
            foreach (string File in Files)
            {
                var Image = LoadImage(File, 320);
                var Thumb = new Border
                {
                    Height = 68,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(5, 0, 5, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = Path.GetFileName(File),
                    Background = Image is null ? (Brush)FindResource("ColorBrushTile") : new ImageBrush(Image) { Stretch = Stretch.UniformToFill },
                    Opacity = 0
                };
                Thumb.MouseLeftButtonUp += (_, __) => OpenInstancePage(FormMain.PageSubType.InstanceScreenshot);
                Thumb.MouseEnter += (_, __) => ModAnimation.AniStart(ModAnimation.AaOpacity(Thumb, 0.85 - Thumb.Opacity, 120), "HeroShot Hover " + File);
                Thumb.MouseLeave += (_, __) => ModAnimation.AniStart(ModAnimation.AaOpacity(Thumb, 1 - Thumb.Opacity, 120), "HeroShot Hover " + File);
                ModAnimation.AniStart(ModAnimation.AaOpacity(Thumb, 1, 240, 120 + Index++ * 50), "HeroShot Enter " + File);
                this.PanShots.Children.Add(Thumb);
            }
            for (int i = Files.Count; i < ShotLimit; i++)
                this.PanShots.Children.Add(new Border());
        }

        private static BitmapImage LoadImage(string FilePath, int DecodeWidth)
        {
            try
            {
                var Image = new BitmapImage();
                Image.BeginInit();
                Image.CacheOption = BitmapCacheOption.OnLoad; // 不占用文件，游戏里还能改写
                Image.UriSource = new Uri(FilePath);
                Image.DecodePixelWidth = DecodeWidth;
                Image.EndInit();
                Image.Freeze();
                return Image;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取图片失败：" + FilePath);
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
