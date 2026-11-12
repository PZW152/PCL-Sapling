using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MeloongCore;

namespace PCL
{
    /// <summary>
    /// 界面语言。界面原文是简体中文；选择繁体中文时，所有只读文字（TextBlock）在显示时转换为繁体。
    /// 在显示层转换，而不是改动每一处文字：这样代码中作为逻辑使用的中文（事件名、文件名、设置值等）不受影响，
    /// 动态拼接的文字、Mod 的中文名等也能一并转换。可编辑的输入框不转换。
    /// 切换语言立即生效：转换时记住原文，切回简体时还原。
    /// </summary>
    public static class ModLanguage
    {
        private static readonly DependencyPropertyDescriptor TextDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        private static readonly ConditionalWeakTable<TextBlock, object> Known = new ConditionalWeakTable<TextBlock, object>();
        private static readonly ConditionalWeakTable<TextBlock, object> Watching = new ConditionalWeakTable<TextBlock, object>();
        private static readonly object Marker = new object();

        /// <summary>转换过的文字：原文与转换结果。文字被改成别的内容后，这条记录就作废。</summary>
        private sealed class Converted
        {
            public string Original;
            public string Result;
        }
        private static readonly ConditionalWeakTable<DependencyObject, Converted> Records = new ConditionalWeakTable<DependencyObject, Converted>();

        /// <summary>
        /// 启动时调用（在任何窗口显示之前）：决定界面语言，并开始跟踪文字。
        /// </summary>
        public static void Initialize()
        {
            UiLanguage.UseTraditional = UiLanguage.ResolveTraditional(Settings.Get<int>("UiLanguage"), CultureInfo.CurrentUICulture.Name);
            Logger.Info($"界面语言：{(UiLanguage.UseTraditional ? "繁体中文" : "简体中文")}（系统语言 {CultureInfo.CurrentUICulture.Name}）");
            // WPF 只对挂了实例处理函数的元素引发 Loaded，所以用每个元素第一次布局时都会引发的 SizeChanged 发现新的 TextBlock，
            // 再挂上 Loaded / Unloaded：显示时跟踪文字变化，移出界面后停止跟踪
            EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnSizeChanged), true);
        }

        /// <summary>
        /// 设置改变时调用：立即切换所有已打开窗口中的文字。暂时不在界面上的页面会在显示时切换。
        /// </summary>
        public static void Refresh()
        {
            bool Traditional = UiLanguage.ResolveTraditional(Settings.Get<int>("UiLanguage"), CultureInfo.CurrentUICulture.Name);
            if (Traditional == UiLanguage.UseTraditional)
                return;
            UiLanguage.UseTraditional = Traditional;
            Logger.Info($"界面语言切换为{(Traditional ? "繁体中文" : "简体中文")}");
            foreach (Window Window in System.Windows.Application.Current.Windows)
                ApplyTree(Window);
        }
        private static void ApplyTree(DependencyObject Root)
        {
            if (Root is TextBlock Block)
                Apply(Block);
            if (Root is Visual || Root is System.Windows.Media.Media3D.Visual3D)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(Root); i++)
                    ApplyTree(VisualTreeHelper.GetChild(Root, i));
            }
        }

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
            Apply(Block);
            if (!Watching.TryGetValue(Block, out _))
            {
                Watching.Add(Block, Marker);
                TextDescriptor.AddValueChanged(Block, OnTextChanged);
            }
        }
        private static void OnTextChanged(object sender, System.EventArgs e) => Apply((TextBlock)sender);

        /// <summary>
        /// 让一个 TextBlock 的文字符合当前语言。只有一段纯文字时处理 Text（保留绑定）；含有超链接、换行等内容时逐段处理，保留格式。
        /// </summary>
        private static void Apply(TextBlock Block)
        {
            var Inlines = Block.Inlines;
            if (Inlines.Count > 1 || (Inlines.Count == 1 && !(Inlines.FirstInline is Run)))
                ApplyInlines(Inlines);
            else
                ApplyText(Block, TextBlock.TextProperty);
        }
        private static void ApplyInlines(IEnumerable<Inline> Inlines)
        {
            foreach (var Inline in Inlines.ToList())
            {
                if (Inline is Run Run)
                    ApplyText(Run, Run.TextProperty);
                else if (Inline is Span Span)
                    ApplyInlines(Span.Inlines);
            }
        }
        private static void ApplyText(DependencyObject Target, DependencyProperty Property)
        {
            string Text = (string)Target.GetValue(Property) ?? "";
            bool HasRecord = Records.TryGetValue(Target, out var Record);
            if (HasRecord && Text != Record.Result)
            {
                // 文字已被改成新的内容（原文），之前的记录作废
                Records.Remove(Target);
                HasRecord = false;
            }
            if (UiLanguage.UseTraditional)
            {
                if (HasRecord || !ChineseConverter.MayNeedConversion(Text))
                    return;
                string Result = ChineseConverter.ToTraditional(Text);
                if (Result == Text)
                    return;
                Records.Add(Target, new Converted { Original = Text, Result = Result });
                Target.SetCurrentValue(Property, Result);
            }
            else if (HasRecord)
            {
                // 切回简体：还原原文
                Records.Remove(Target);
                Target.SetCurrentValue(Property, Record.Original);
            }
        }
    }
}
