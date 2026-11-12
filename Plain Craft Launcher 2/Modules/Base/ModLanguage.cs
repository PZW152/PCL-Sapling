using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using MeloongCore;

namespace PCL
{
    /// <summary>
    /// 界面语言。界面原文是简体中文；选择繁体中文时，所有只读文字（TextBlock）在显示时转换为繁体。
    /// 在显示层转换，而不是改动每一处文字：这样代码中作为逻辑使用的中文（事件名、文件名、设置值等）不受影响，
    /// 动态拼接的文字、Mod 的中文名等也能一并转换。可编辑的输入框不转换。
    /// </summary>
    public static class ModLanguage
    {
        private static readonly DependencyPropertyDescriptor TextDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        private static readonly ConditionalWeakTable<TextBlock, object> Watching = new ConditionalWeakTable<TextBlock, object>();
        private static readonly object Marker = new object();

        /// <summary>
        /// 启动时调用：根据设置与系统语言决定界面语言，选择繁体时开始转换文字。
        /// </summary>
        public static void Initialize()
        {
            UiLanguage.UseTraditional = UiLanguage.ResolveTraditional(Settings.Get<int>("UiLanguage"), CultureInfo.CurrentUICulture.Name);
            Logger.Info($"界面语言：{(UiLanguage.UseTraditional ? "繁体中文" : "简体中文")}（系统语言 {CultureInfo.CurrentUICulture.Name}）");
            if (!UiLanguage.UseTraditional)
                return;
            // WPF 只对挂了实例处理函数的元素引发 Loaded，所以用每个元素第一次布局时都会引发的 SizeChanged 发现新的 TextBlock，
            // 再挂上 Loaded / Unloaded：显示时跟踪文字变化，移出界面后停止跟踪
            EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnSizeChanged), true);
        }

        private static readonly ConditionalWeakTable<TextBlock, object> Known = new ConditionalWeakTable<TextBlock, object>();

        private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var Block = (TextBlock)sender;
            if (Known.TryGetValue(Block, out _))
                return;
            Known.Add(Block, Marker);
            Block.Loaded += OnLoaded;
            Block.Unloaded += OnUnloaded;
            StartWatching(Block);
        }
        private static void OnLoaded(object sender, RoutedEventArgs e) => StartWatching((TextBlock)sender);
        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var Block = (TextBlock)sender;
            if (Watching.TryGetValue(Block, out _))
            {
                Watching.Remove(Block);
                TextDescriptor.RemoveValueChanged(Block, OnTextChanged);
            }
        }
        private static void StartWatching(TextBlock Block)
        {
            Convert(Block);
            if (!Watching.TryGetValue(Block, out _))
            {
                Watching.Add(Block, Marker);
                TextDescriptor.AddValueChanged(Block, OnTextChanged);
            }
        }
        private static void OnTextChanged(object sender, System.EventArgs e) => Convert((TextBlock)sender);

        /// <summary>
        /// 转换一个 TextBlock 的文字。只有一段纯文字时直接转换 Text（保留绑定）；含有超链接、换行等内容时逐段转换，保留格式。
        /// </summary>
        private static void Convert(TextBlock Block)
        {
            var Inlines = Block.Inlines;
            if (Inlines.Count > 1 || (Inlines.Count == 1 && !(Inlines.FirstInline is Run)))
            {
                ConvertInlines(Inlines);
                return;
            }
            string Text = Block.Text;
            if (!ChineseConverter.MayNeedConversion(Text))
                return;
            string Converted = ChineseConverter.ToTraditional(Text);
            if (Converted != Text)
                Block.SetCurrentValue(TextBlock.TextProperty, Converted);
        }
        private static void ConvertInlines(IEnumerable<Inline> Inlines)
        {
            foreach (var Inline in Inlines.ToList())
            {
                switch (Inline)
                {
                    case Run Run:
                        if (ChineseConverter.MayNeedConversion(Run.Text))
                        {
                            string Converted = ChineseConverter.ToTraditional(Run.Text);
                            if (Converted != Run.Text)
                                Run.SetCurrentValue(Run.TextProperty, Converted);
                        }
                        break;
                    case Span Span:
                        ConvertInlines(Span.Inlines);
                        break;
                }
            }
        }
    }
}
