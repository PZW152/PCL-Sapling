using System.Xml.Linq;

namespace PCL
{
    /// <summary>
    /// 從 VB 轉換到 C# 時保留語義用的小工具。
    /// VB 的 Option Strict Off 允許在 Object 上直接調用成員、做下標訪問（晚期綁定），C# 中對應的是 dynamic。
    /// </summary>
    public static class VbCompat
    {
        /// <summary>
        /// 把對象轉為 dynamic，用於 VB 晚期綁定的下標訪問或委託調用，例如 VB 的 <c>Control.Tag(0)</c>。
        /// </summary>
        public static dynamic AsDyn(this object Value) => Value;

        /// <summary>
        /// 對應 VB 的 My.Computer（記憶體、螢幕、剪貼簿、鍵盤狀態等）。
        /// </summary>
        public static readonly Microsoft.VisualBasic.Devices.Computer Computer = new Microsoft.VisualBasic.Devices.Computer();
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
