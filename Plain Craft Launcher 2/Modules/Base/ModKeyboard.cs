using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MeloongCore;

namespace PCL
{
    /// <summary>
    /// 键盘操作（取自 Sapling 的无障碍原则：只用键盘也能完成所有操作）。
    /// PCL 的按钮、列表项、复选框等是自绘控件，原本拿不到焦点：这里让它们可以用 Tab 选中、显示焦点框，并用 Enter 或空格按下。
    /// </summary>
    public static class ModKeyboard
    {
        private static readonly Type[] Pressable =
        {
            typeof(MyButton), typeof(MyIconButton), typeof(MyTextButton), typeof(MyIconTextButton),
            typeof(MyCheckBox), typeof(MyRadioBox), typeof(MyRadioButton), typeof(MyListItem)
        };

        /// <summary>
        /// 在任何控件创建之前调用一次。
        /// </summary>
        /// <summary>
        /// 最近一次操作是否来自键盘（用来决定是否显示焦点框）。
        /// </summary>
        private static bool LastInputWasKeyboard = false;

        public static void Initialize()
        {
            EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, __) => LastInputWasKeyboard = true), true);
            EventManager.RegisterClassHandler(typeof(Window), Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((_, __) => LastInputWasKeyboard = false), true);
            foreach (var ControlType in Pressable)
            {
                if (ControlType == typeof(MyListItem))
                    UIElement.FocusableProperty.OverrideMetadata(ControlType, new FrameworkPropertyMetadata(true, null, CoerceListItemFocusable));
                else
                    UIElement.FocusableProperty.OverrideMetadata(ControlType, new FrameworkPropertyMetadata(true));
                FrameworkElement.FocusVisualStyleProperty.OverrideMetadata(ControlType, new FrameworkPropertyMetadata(null));
                EventManager.RegisterClassHandler(ControlType, UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotFocus));
                EventManager.RegisterClassHandler(ControlType, UIElement.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnLostFocus));
            }
        }

        #region 焦点框

        /// <summary>
        /// 只在用键盘（Tab、方向键）移动焦点时显示，用鼠标点击时不显示。
        /// </summary>
        private static void OnGotFocus(object Sender, KeyboardFocusChangedEventArgs e)
        {
            if (!ReferenceEquals(e.NewFocus, Sender) || Sender is not UIElement Target)
                return;
            if (!LastInputWasKeyboard)
                return;
            ShowRing(Target);
        }
        private static void OnLostFocus(object Sender, KeyboardFocusChangedEventArgs e)
        {
            if (ReferenceEquals(e.OldFocus, Sender) && Sender is UIElement Target)
                HideRing(Target);
        }

        private static void ShowRing(UIElement Target)
        {
            var Layer = AdornerLayer.GetAdornerLayer(Target);
            if (Layer is null || Layer.GetAdorners(Target)?.OfType<FocusRing>().Any() == true)
                return;
            Layer.Add(new FocusRing(Target));
        }
        private static void HideRing(UIElement Target)
        {
            var Layer = AdornerLayer.GetAdornerLayer(Target);
            foreach (var Ring in Layer?.GetAdorners(Target)?.OfType<FocusRing>().ToList() ?? new System.Collections.Generic.List<FocusRing>())
                Layer.Remove(Ring);
        }

        /// <summary>
        /// 主题色的圆角描边，比元素大一圈。
        /// </summary>
        private sealed class FocusRing : Adorner
        {
            public FocusRing(UIElement Target) : base(Target)
            {
                IsHitTestVisible = false;
            }
            protected override void OnRender(DrawingContext Context)
            {
                var Brush = TryFindResource("ColorBrush3") as Brush ?? Brushes.DodgerBlue;
                var Bounds = new Rect(AdornedElement.RenderSize);
                Bounds.Inflate(3, 3);
                Context.DrawRoundedRectangle(null, new Pen(Brush, 2), Bounds, 6, 6);
            }
        }

        #endregion

        /// <summary>
        /// 只是用来展示的列表项（不能点击、或者不响应鼠标）不需要停在 Tab 上。
        /// </summary>
        private static object CoerceListItemFocusable(DependencyObject Target, object Value)
        {
            if (Target is MyListItem Item && (Item.Type == MyListItem.CheckType.None || !Item.IsHitTestVisible))
                return false;
            return Value;
        }


        /// <summary>
        /// 让普通元素（例如首页的“继续玩”卡片）也能用 Tab 选中、用 Enter 或空格按下。
        /// </summary>
        public static void MakePressable(FrameworkElement Target, Action OnPress)
        {
            Target.Focusable = true;
            Target.FocusVisualStyle = null;
            Target.GotKeyboardFocus += (_, e) => OnGotFocus(Target, e);
            Target.LostKeyboardFocus += (_, e) => OnLostFocus(Target, e);
            Target.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter && e.Key != Key.Space)
                    return;
                e.Handled = true;
                OnPress();
            };
        }

        /// <summary>
        /// 焦点在可按下的控件上时，Enter 或空格相当于用鼠标点一下。返回是否已处理。
        /// </summary>
        public static bool TryPress(KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
                return false;
            if (Keyboard.FocusedElement is not UIElement Target || !Pressable.Any(t => t.IsInstanceOfType(Target)))
                return false;
            if (!Target.IsEnabled || !Target.IsVisible)
                return false;
            Press(Target);
            e.Handled = true;
            return true;
        }

        /// <summary>
        /// 依次发出按下与松开的鼠标事件，沿用控件原有的点击逻辑与动画。
        /// </summary>
        private static void Press(UIElement Target)
        {
            try
            {
                LastInputWasKeyboard = true;
                foreach (var Event in new[] { UIElement.PreviewMouseLeftButtonDownEvent, UIElement.MouseLeftButtonDownEvent, UIElement.PreviewMouseLeftButtonUpEvent, UIElement.MouseLeftButtonUpEvent })
                    Target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Event, Source = Target });
                Target.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent, Source = Target }); // 恢复悬停颜色
                if (Target.Focusable && !Target.IsKeyboardFocused)
                    Keyboard.Focus(Target);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "用键盘按下控件失败");
            }
        }
    }
}
