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

namespace PCL
{
    /// <summary>
    /// 版本设置 → 截图（取自 Sapling）：浏览、放大查看与删除游戏截图，左右方向键换图。
    /// </summary>
    public partial class PageInstanceScreenshot
    {
        private const int ThumbWidth = 168, ThumbHeight = 95;
        private List<string> Files = new List<string>();
        private int Current = -1;
        private ModMinecraft.McInstance LoadedInstance;

        public PageInstanceScreenshot()
        {
            this.Loaded += (_, __) => { if (LoadedInstance != PageInstanceLeft.Instance) Reload(); this.Focusable = true; };
            this.PreviewKeyDown += OnKeyDown;
            this.InitializeComponent();
        }

        private string Folder => PageInstanceLeft.Instance.PathIndie + @"screenshots\";

        /// <summary>
        /// 重新读取截图列表，最新的在前。缩略图在后台按小尺寸解码，不锁定图片文件。
        /// </summary>
        public void Reload()
        {
            var Instance = PageInstanceLeft.Instance;
            LoadedInstance = Instance;
            this.PanThumbs.Children.Clear();
            this.CardPreview.Visibility = Visibility.Collapsed;
            Current = -1;
            try
            {
                Files = Directory.Exists(Folder)
                    ? new DirectoryInfo(Folder).GetFiles().Where(f => new[] { ".png", ".jpg", ".jpeg" }.Contains(f.Extension.ToLowerInvariant()))
                        .OrderByDescending(f => f.LastWriteTime).Select(f => f.FullName).ToList()
                    : new List<string>();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取截图列表失败");
                Files = new List<string>();
            }
            this.LabEmpty.Visibility = Files.Any() ? Visibility.Collapsed : Visibility.Visible;
            this.CardList.Title = Files.Any() ? $"截图（{Files.Count}）" : "截图";
            foreach (var File in Files)
                this.PanThumbs.Children.Add(CreateThumb(File));
            var Snapshot = Files.ToList();
            ModBase.RunInBackground(() =>
            {
                foreach (var File in Snapshot)
                {
                    if (LoadedInstance != Instance)
                        return;
                    var Image = Decode(File, ThumbWidth * 2);
                    if (Image is null)
                        continue;
                    ModBase.RunInUi(() =>
                    {
                        int Index = Files.IndexOf(File);
                        if (LoadedInstance == Instance && Index >= 0 && Index < this.PanThumbs.Children.Count && this.PanThumbs.Children[Index] is Border Thumb && Thumb.Child is Image Img)
                            Img.Source = Image;
                    });
                }
            }, "Screenshot Thumbnails");
        }

        private Border CreateThumb(string File)
        {
            var Thumb = new Border
            {
                Width = ThumbWidth,
                Height = ThumbHeight,
                Margin = new Thickness(0, 0, 10, 10),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)),
                Cursor = Cursors.Hand,
                ToolTip = Path.GetFileNameWithoutExtension(File),
                Child = new Image { Stretch = Stretch.UniformToFill }
            };
            Thumb.MouseLeftButtonUp += (_, __) => Show(Files.IndexOf(File));
            Thumb.MouseEnter += (_, __) => Thumb.Opacity = 0.85;
            Thumb.MouseLeave += (_, __) => Thumb.Opacity = 1;
            return Thumb;
        }

        /// <summary>
        /// 解码图片并冻结，可以跨线程使用。读取到内存后立即关闭文件，删除时不会被占用。
        /// </summary>
        private static BitmapImage Decode(string File, int Width)
        {
            try
            {
                var Image = new BitmapImage();
                using (var Stream = new MemoryStream(System.IO.File.ReadAllBytes(File)))
                {
                    Image.BeginInit();
                    Image.CacheOption = BitmapCacheOption.OnLoad;
                    if (Width > 0)
                        Image.DecodePixelWidth = Width;
                    Image.StreamSource = Stream;
                    Image.EndInit();
                }
                Image.Freeze();
                return Image;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"读取截图失败：{File}");
                return null;
            }
        }

        private void Show(int Index)
        {
            if (Index < 0 || Index >= Files.Count)
                return;
            Current = Index;
            this.ImgPreview.Source = Decode(Files[Index], 1600);
            this.LabPreview.Text = $"{Path.GetFileName(Files[Index])}（{Index + 1} / {Files.Count}）";
            this.BtnPrevious.IsEnabled = Index > 0;
            this.BtnNext.IsEnabled = Index < Files.Count - 1;
            if (this.CardPreview.Visibility != Visibility.Visible)
            {
                this.CardPreview.Visibility = Visibility.Visible;
                this.PanBack.ScrollToHome();
            }
            Keyboard.Focus(this);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Current < 0 || e.OriginalSource is TextBox)
                return;
            if (e.Key == Key.Left) { Show(Current - 1); e.Handled = true; }
            else if (e.Key == Key.Right) { Show(Current + 1); e.Handled = true; }
            else if (e.Key == Key.Delete) { BtnDelete_Click(null, null); e.Handled = true; }
        }

        private void BtnPrevious_Click(object sender, MouseButtonEventArgs e) => Show(Current - 1);
        private void BtnNext_Click(object sender, MouseButtonEventArgs e) => Show(Current + 1);
        private void BtnClose_Click(object sender, MouseButtonEventArgs e)
        {
            this.CardPreview.Visibility = Visibility.Collapsed;
            this.ImgPreview.Source = null;
            Current = -1;
        }
        private void BtnOpen_Click(object sender, MouseButtonEventArgs e)
        {
            if (Current >= 0)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Files[Current]) { UseShellExecute = true });
        }
        private void BtnCopy_Click(object sender, MouseButtonEventArgs e)
        {
            if (Current < 0)
                return;
            try
            {
                Clipboard.SetImage(Decode(Files[Current], 0));
                ModMain.Hint("已复制图片", ModMain.HintType.Green);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "复制截图失败", LogBehavior.Toast);
            }
        }
        private void BtnDelete_Click(object sender, MouseButtonEventArgs e)
        {
            if (Current < 0)
                return;
            string File = Files[Current];
            try
            {
                FileUtils.Delete(File, toRecycleBin: true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "删除截图失败", LogBehavior.Toast);
                return;
            }
            ModMain.Hint("已将截图移到回收站", ModMain.HintType.Green);
            int Index = Current;
            Files.RemoveAt(Index);
            this.PanThumbs.Children.RemoveAt(Index);
            this.CardList.Title = Files.Any() ? $"截图（{Files.Count}）" : "截图";
            this.LabEmpty.Visibility = Files.Any() ? Visibility.Collapsed : Visibility.Visible;
            if (Files.Any())
                Show(Math.Min(Index, Files.Count - 1));
            else
                BtnClose_Click(null, null);
        }
        private void BtnFolder_Click(object sender, MouseButtonEventArgs e)
        {
            Directory.CreateDirectory(Folder);
            ModBase.OpenExplorer(Folder);
        }
        private void BtnRefresh_Click(object sender, MouseButtonEventArgs e) => Reload();
    }
}
