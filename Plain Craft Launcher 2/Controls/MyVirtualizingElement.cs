using System;
using System.Windows;
using System.Windows.Controls;
using MeloongCore;

namespace PCL
{
    /// <summary>MyVirtualizingElement&lt;T&gt; 的非泛型视图。</summary>
    public interface IMyVirtualizingElement
    {
        FrameworkElement InitElement();
    }
    public class MyVirtualizingElement<T> : FrameworkElement, IMyVirtualizingElement where T : FrameworkElement
    {

        private Func<T> Initializer;
        public MyVirtualizingElement(Func<T> Initializer)
        {
            this.Initializer = Initializer;
            LazyLoadBehavior.OnFirstEnterScrollViewerViewport(this, () => Init());
        }

        /// <summary>
    /// 实例化此控件。
    /// </summary>
        FrameworkElement IMyVirtualizingElement.InitElement() => Init();
        public T Init()
        {
            var Element = Initializer();
            if (Parent is not null)
            {
                if (!(Parent is Panel))
                    throw new Exception("MyVirtualizingElement 的父级必须是一个 Panel");
                Panel ParentPanel = (Panel)Parent;
                int CurrentIndex = ParentPanel.Children.IndexOf(this);
                ParentPanel.Children.RemoveAt(CurrentIndex);
                ParentPanel.Children.Insert(CurrentIndex, Element);
            }
            return Element;
        }
        public static implicit operator T(MyVirtualizingElement<T> Virtualized)
        {
            return Virtualized.Init();
        }

    }

    // 非泛型形式
    public class MyVirtualizingElement : FrameworkElement
    {

        private Func<FrameworkElement> Initializer;
        public MyVirtualizingElement(Func<FrameworkElement> Initializer)
        {
            this.Initializer = Initializer;
            LazyLoadBehavior.OnFirstEnterScrollViewerViewport(this, () => Init());
        }

        /// <summary>
    /// 实例化此控件。
    /// </summary>
        public FrameworkElement Init()
        {
            var Element = Initializer();
            if (Parent is not null)
            {
                if (!(Parent is Panel))
                    throw new Exception("MyVirtualizingElement 的父级必须是一个 Panel");
                Panel ParentPanel = (Panel)Parent;
                int CurrentIndex = ParentPanel.Children.IndexOf(this);
                ParentPanel.Children.RemoveAt(CurrentIndex);
                ParentPanel.Children.Insert(CurrentIndex, Element);
            }
            return Element;
        }

        /// <summary>
    /// 获取实例化后的控件。
    /// 如果该控件没有实例化，则会立即实例化。
    /// 如果类型错误，则返回原值。
    /// </summary>
        public static FrameworkElement TryInit(FrameworkElement Element)
        {
            if (GeneralUtils.IsGenericInstanceOf(Element, typeof(MyVirtualizingElement<>)))
            {
                return ((IMyVirtualizingElement)Element).InitElement();
            }
            else if (Element is MyVirtualizingElement)
            {
                return ((MyVirtualizingElement)Element).Init();
            }
            else
            {
                return Element;
            }
        }

    }
}