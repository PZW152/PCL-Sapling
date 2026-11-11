using System.Windows;
using System.Xml.Linq;

namespace PCL
{
    /// <summary>
    /// 從 VB 轉換到 C# 時保留語義用的小工具。
    /// 原本的晚期綁定（dynamic）已全部改為強類型代碼，專案不再引用 Microsoft.CSharp。
    /// </summary>
    public static class VbCompat
    {
        // 以下為強類型替代：取代原本對 sender / Parent / Tag 的晚期綁定，讓錯誤在編譯期就能發現。
        /// <summary>把事件的 sender 等對象視為 FrameworkElement。</summary>
        public static FrameworkElement Fe(this object Value) => (FrameworkElement)Value;
        /// <summary>取得控件的父控件（FrameworkElement）。</summary>
        public static FrameworkElement ParentFe(this object Value) => (FrameworkElement)((FrameworkElement)Value).Parent;
        /// <summary>頁面附加參數、提示控件 Tag 等以 object[] 存放的參數列表。</summary>
        public static object[] Args(this object Value) => (object[])Value;

    }

    /// <summary>
    /// VB XML 字面量中使用的命名空間前綴。
    /// </summary>
    internal static class XmlImports
    {
        public static readonly XNamespace local = "clr-namespace:PCL;assembly=Plain Craft Launcher 2";
        public static readonly XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
    }
}
