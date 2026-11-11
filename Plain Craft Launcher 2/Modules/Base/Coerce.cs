using System;
using System.Globalization;
using System.Text;

namespace PCL
{
    /// <summary>
    /// 弱类型值的转换与比较。
    /// 设置项、控件 Tag、INI / 注册表读取到的值在运行时可能是字符串、数字或布尔值；
    /// 本类以确定的规则在它们之间转换，规则与原先使用的 VB 运行库逐项一致（由 PCL.Tests 中的对照测试保证），
    /// 因此主程序不再需要引用 Microsoft.VisualBasic。
    /// </summary>
    public static class Coerce
    {
        private static CultureInfo Culture => CultureInfo.CurrentCulture;

        private static InvalidCastException Invalid(object Value, string Target) =>
            new InvalidCastException($"无法将 {(Value is string Text ? "字符串 \"" + (Text.Length > 32 ? Text.Substring(0, 32) + "..." : Text) + "\"" : "类型 " + Value?.GetType().Name)} 转换为 {Target}");

        #region 字符串解析

        /// <summary>
        /// 识别 &amp;H（十六进制）与 &amp;O（八进制）前缀的整数。
        /// </summary>
        private static bool TryParseHexOrOct(string Value, out long Result)
        {
            Result = 0;
            int Length = Value.Length;
            int Index = 0;
            while (Index < Length && (char.IsWhiteSpace(Value[Index]) || Value[Index] == '　')) Index++;
            if (Index + 1 >= Length || Value[Index] != '&') return false;
            char Kind = char.ToLowerInvariant(Value[Index + 1]);
            if (Kind != 'h' && Kind != 'o') return false;
            string Digits = Value.Substring(Index + 2).TrimEnd();
            // 去掉类型后缀（&H10&、&H10L 等）
            Digits = Digits.TrimEnd('&');
            try
            {
                Result = Convert.ToInt64(Digits, Kind == 'h' ? 16 : 8);
                return true;
            }
            catch (Exception)
            {
                throw Invalid(Value, "数字");
            }
        }

        private static string ToHalfWidthNumbers(string Value)
        {
            // 与 VB 一致：仅在东亚语言环境下把全角数字转为半角
            int Lang = Culture.LCID & 0x3FF;
            if (Lang != 0x04 && Lang != 0x11 && Lang != 0x12) return Value;
            StringBuilder Builder = null;
            for (int i = 0; i < Value.Length; i++)
            {
                char c = Value[i];
                if (c >= '！' && c <= '～')
                {
                    Builder ??= new StringBuilder(Value);
                    Builder[i] = (char)(c - 0xFEE0);
                }
                else if (c == '　')
                {
                    Builder ??= new StringBuilder(Value);
                    Builder[i] = ' ';
                }
            }
            return Builder?.ToString() ?? Value;
        }

        private static double ParseDouble(string Value)
        {
            string Text = ToHalfWidthNumbers(Value);
            var Format = Culture.NumberFormat;
            try
            {
                return double.Parse(Text, NumberStyles.Any, Format);
            }
            catch (FormatException)
            {
                // 货币符号与数字格式不一致时，按普通数字格式重试
                var Normalized = (NumberFormatInfo)Format.Clone();
                Normalized.CurrencyDecimalSeparator = Format.NumberDecimalSeparator;
                Normalized.CurrencyGroupSeparator = Format.NumberGroupSeparator;
                Normalized.CurrencyDecimalDigits = Format.NumberDecimalDigits;
                return double.Parse(Text, NumberStyles.Any, Normalized);
            }
        }

        #endregion

        #region 转为基础类型

        public static double ToDouble(string Value)
        {
            if (Value is null) return 0d;
            try
            {
                if (TryParseHexOrOct(Value, out long Number)) return Number;
                return ParseDouble(Value);
            }
            catch (FormatException)
            {
                throw Invalid(Value, "Double");
            }
        }

        public static double ToDouble(object Value)
        {
            if (Value is null) return 0d;
            if (Value is IConvertible Convertible)
            {
                switch (Convertible.GetTypeCode())
                {
                    case TypeCode.Boolean: return (bool)Value ? -1d : 0d;
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                    case TypeCode.Single:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                        return Convertible.ToDouble(null);
                    case TypeCode.String: return ToDouble(Convertible.ToString(null));
                }
            }
            throw Invalid(Value, "Double");
        }

        public static long ToLong(string Value)
        {
            if (Value is null) return 0L;
            try
            {
                if (TryParseHexOrOct(Value, out long Number)) return Number;
                return Convert.ToInt64(decimal.Parse(ToHalfWidthNumbers(Value), NumberStyles.Any, Culture.NumberFormat));
            }
            catch (FormatException)
            {
                throw Invalid(Value, "Long");
            }
        }

        public static long ToLong(object Value)
        {
            if (Value is null) return 0L;
            if (Value is IConvertible Convertible)
            {
                switch (Convertible.GetTypeCode())
                {
                    case TypeCode.Boolean: return (bool)Value ? -1L : 0L;
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                        return Convertible.ToInt64(null);
                    case TypeCode.Single:
                    case TypeCode.Double:
                        return checked((long)Math.Round(Convertible.ToDouble(null)));
                    case TypeCode.Decimal: return Convert.ToInt64(Convertible.ToDecimal(null));
                    case TypeCode.String: return ToLong(Convertible.ToString(null));
                }
            }
            throw Invalid(Value, "Long");
        }

        public static int ToInt(string Value)
        {
            if (Value is null) return 0;
            try
            {
                if (TryParseHexOrOct(Value, out long Number)) return checked((int)Number);
                return checked((int)Math.Round(ParseDouble(Value)));
            }
            catch (FormatException)
            {
                throw Invalid(Value, "Integer");
            }
        }

        public static int ToInt(object Value)
        {
            if (Value is null) return 0;
            if (Value is IConvertible Convertible)
            {
                switch (Convertible.GetTypeCode())
                {
                    case TypeCode.Boolean: return (bool)Value ? -1 : 0;
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                        return Convertible.ToInt32(null);
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                        return checked((int)Convertible.ToInt64(null));
                    case TypeCode.Single:
                    case TypeCode.Double:
                        return checked((int)Math.Round(Convertible.ToDouble(null)));
                    case TypeCode.Decimal: return Convert.ToInt32(Convertible.ToDecimal(null));
                    case TypeCode.String: return ToInt(Convertible.ToString(null));
                }
            }
            throw Invalid(Value, "Integer");
        }

        public static uint ToUInt(string Value)
        {
            if (Value is null) return 0u;
            try
            {
                if (TryParseHexOrOct(Value, out long Number)) return checked((uint)Number);
                return checked((uint)Math.Round(ParseDouble(Value)));
            }
            catch (FormatException)
            {
                throw Invalid(Value, "UInteger");
            }
        }

        public static bool ToBool(string Value)
        {
            if (Value is null) Value = "";
            try
            {
                if (string.Compare(Value, bool.FalseString, true, Culture) == 0) return false;
                if (string.Compare(Value, bool.TrueString, true, Culture) == 0) return true;
                if (TryParseHexOrOct(Value, out long Number)) return Number != 0;
                return ParseDouble(Value) != 0d;
            }
            catch (FormatException)
            {
                throw Invalid(Value, "Boolean");
            }
        }

        public static bool ToBool(object Value)
        {
            if (Value is null) return false;
            if (Value is IConvertible Convertible)
            {
                switch (Convertible.GetTypeCode())
                {
                    case TypeCode.Boolean: return (bool)Value;
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                        return Convertible.ToInt64(null) != 0;
                    case TypeCode.UInt64:
                        return Convertible.ToUInt64(null) != 0;
                    case TypeCode.Single:
                    case TypeCode.Double:
                        return Convertible.ToDouble(null) != 0d;
                    case TypeCode.Decimal: return Convertible.ToDecimal(null) != 0m;
                    case TypeCode.String: return ToBool(Convertible.ToString(null));
                }
            }
            throw Invalid(Value, "Boolean");
        }

        public static char ToChar(string Value) => string.IsNullOrEmpty(Value) ? '\0' : Value[0];

        public static DateTime ToDate(string Value)
        {
            if (DateTime.TryParse(ToHalfWidthNumbers(Value ?? ""), Culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault, out var Result))
                return Result;
            throw Invalid(Value, "Date");
        }

        private static string DateToStr(DateTime Value)
        {
            long TimeTicks = Value.TimeOfDay.Ticks;
            if (TimeTicks == Value.Ticks || (Value.Year == 1899 && Value.Month == 12 && Value.Day == 30))
                return Value.ToString("T", null); // 只有时间
            if (TimeTicks == 0)
                return Value.ToString("d", null); // 只有日期
            return Value.ToString("G", null);
        }

        /// <summary>
        /// 转为字符串。null 仍为 null；布尔值为 True / False；枚举为其数值。
        /// </summary>
        public static string ToStr(object Value)
        {
            if (Value is null) return null;
            if (Value is string Text) return Text;
            if (Value is IConvertible Convertible)
            {
                switch (Convertible.GetTypeCode())
                {
                    case TypeCode.Boolean: return (bool)Value ? bool.TrueString : bool.FalseString;
                    case TypeCode.Char: return Convertible.ToChar(null).ToString();
                    case TypeCode.Byte: return Convertible.ToByte(null).ToString();
                    case TypeCode.SByte: return Convertible.ToSByte(null).ToString();
                    case TypeCode.Int16: return Convertible.ToInt16(null).ToString();
                    case TypeCode.UInt16: return Convertible.ToUInt16(null).ToString();
                    case TypeCode.Int32: return Convertible.ToInt32(null).ToString();
                    case TypeCode.UInt32: return Convertible.ToUInt32(null).ToString();
                    case TypeCode.Int64: return Convertible.ToInt64(null).ToString();
                    case TypeCode.UInt64: return Convertible.ToUInt64(null).ToString();
                    case TypeCode.Single: return Convertible.ToSingle(null).ToString((string)null, null);
                    case TypeCode.Double: return Convertible.ToDouble(null).ToString("G", null);
                    case TypeCode.Decimal: return Convertible.ToDecimal(null).ToString("G", null);
                    case TypeCode.DateTime: return DateToStr(Convertible.ToDateTime(null));
                    case TypeCode.String: return Convertible.ToString(null);
                }
            }
            else if (Value is char[] Chars)
            {
                return new string(Chars);
            }
            throw Invalid(Value, "String");
        }

        /// <summary>
        /// 转为指定类型：null 得到该类型的默认值；基础类型按本类的规则转换；枚举按数值转换。
        /// </summary>
        public static object ChangeType(object Value, Type Target)
        {
            if (Value is null) return Target.IsValueType ? Activator.CreateInstance(Target) : null;
            if (Target.IsInstanceOfType(Value)) return Value;
            if (Target.IsEnum)
                return Enum.ToObject(Target, ChangeType(Value, Enum.GetUnderlyingType(Target)));
            switch (Type.GetTypeCode(Target))
            {
                case TypeCode.Boolean: return ToBool(Value);
                case TypeCode.Int32: return ToInt(Value);
                case TypeCode.Int64: return ToLong(Value);
                case TypeCode.Double: return ToDouble(Value);
                case TypeCode.String: return ToStr(Value);
                case TypeCode.Single: return (float)ToDouble(Value);
                case TypeCode.UInt32: return checked((uint)ToLong(Value));
                case TypeCode.Int16: return checked((short)ToInt(Value));
                case TypeCode.Byte: return checked((byte)ToInt(Value));
                case TypeCode.Decimal: return Value is string DecimalText ? decimal.Parse(ToHalfWidthNumbers(DecimalText), NumberStyles.Any, Culture.NumberFormat) : Convert.ToDecimal(Value);
                case TypeCode.Char: return Value is string CharText ? ToChar(CharText) : throw Invalid(Value, "Char");
                case TypeCode.DateTime: return Value is string DateText ? ToDate(DateText) : throw Invalid(Value, "Date");
            }
            throw Invalid(Value, Target.Name);
        }

        /// <summary>
        /// 转为指定类型，见 <see cref="ChangeType"/>。
        /// </summary>
        public static T To<T>(object Value)
        {
            if (Value is T Typed) return Typed;
            if (Value is null) return default;
            return (T)ChangeType(Value, typeof(T));
        }

        #endregion

        #region 拼接与比较

        /// <summary>
        /// 拼接两个值的字符串形式，null 视为空字符串。
        /// </summary>
        public static string Concat(object Left, object Right) => (Left is DBNull ? "" : ToStr(Left) ?? "") + (Right is DBNull ? "" : ToStr(Right) ?? "");

        private static bool IsNumeric(TypeCode Code) => Code >= TypeCode.SByte && Code <= TypeCode.Decimal;

        /// <summary>
        /// 比较两个弱类型值：字符串按字符顺序比较（null 等同空字符串）；数字、布尔值之间按数值比较；
        /// 字符串与数字比较时先把字符串转为数字。
        /// </summary>
        public static int Compare(object Left, object Right) => CompareCore(Left, Right) ?? 1;

        /// <summary>返回 null 表示无法排序（其中一方为 NaN）：此时相等、小于等于、大于均不成立。</summary>
        private static int? CompareCore(object Left, object Right)
        {
            TypeCode LeftCode = Left is null ? TypeCode.Empty : Left is IConvertible LeftConvertible ? LeftConvertible.GetTypeCode() : TypeCode.Object;
            TypeCode RightCode = Right is null ? TypeCode.Empty : Right is IConvertible RightConvertible ? RightConvertible.GetTypeCode() : TypeCode.Object;
            if (LeftCode == TypeCode.Object || RightCode == TypeCode.Object)
            {
                if (Left is char[] LeftChars && Right is not char) return CompareCore(new string(LeftChars), Right);
                if (Right is char[] RightChars && Left is not char) return CompareCore(Left, new string(RightChars));
                throw new InvalidCastException($"无法比较 {Left?.GetType().Name ?? "null"} 与 {Right?.GetType().Name ?? "null"}");
            }
            if (LeftCode == TypeCode.Empty && RightCode == TypeCode.Empty) return 0;
            bool LeftText = LeftCode == TypeCode.String || LeftCode == TypeCode.Char;
            bool RightText = RightCode == TypeCode.String || RightCode == TypeCode.Char;
            // 字符串之间（或与 null）：按字符顺序
            if ((LeftText || LeftCode == TypeCode.Empty) && (RightText || RightCode == TypeCode.Empty))
                return Math.Sign(string.CompareOrdinal(ToStr(Left) ?? "", ToStr(Right) ?? ""));
            if (LeftCode == TypeCode.DateTime || RightCode == TypeCode.DateTime)
            {
                DateTime LeftDate = Left is null ? default : Left is string LeftDateText ? ToDate(LeftDateText) : LeftCode == TypeCode.DateTime ? (DateTime)Left : throw Invalid(Left, "Date");
                DateTime RightDate = Right is null ? default : Right is string RightDateText ? ToDate(RightDateText) : RightCode == TypeCode.DateTime ? (DateTime)Right : throw Invalid(Right, "Date");
                return Math.Sign(DateTime.Compare(LeftDate, RightDate));
            }
            if (LeftCode == TypeCode.Char || RightCode == TypeCode.Char)
                throw new InvalidCastException("无法将字符与数字比较");
            // 布尔值之间：False < True 的反序（True 为 -1）
            if ((LeftCode == TypeCode.Boolean || LeftCode == TypeCode.Empty) && (RightCode == TypeCode.Boolean || RightCode == TypeCode.Empty))
                return Math.Sign(ToInt(Left).CompareTo(ToInt(Right)));
            // 布尔值与字符串：把字符串转为布尔值
            if (LeftCode == TypeCode.Boolean && RightText) return Math.Sign(ToInt((object)(bool)Left).CompareTo(ToBool(ToStr(Right)) ? -1 : 0));
            if (RightCode == TypeCode.Boolean && LeftText) return Math.Sign((ToBool(ToStr(Left)) ? -1 : 0).CompareTo(ToInt((object)(bool)Right)));
            // 其余：按数值
            if (LeftCode == TypeCode.Decimal || RightCode == TypeCode.Decimal)
            {
                bool OtherIsFloat = LeftCode == TypeCode.Single || LeftCode == TypeCode.Double || RightCode == TypeCode.Single || RightCode == TypeCode.Double || LeftText || RightText;
                if (!OtherIsFloat)
                    return Math.Sign(decimal.Compare(Left is null ? 0m : LeftCode == TypeCode.Boolean ? ((bool)Left ? -1m : 0m) : Convert.ToDecimal(Left), Right is null ? 0m : RightCode == TypeCode.Boolean ? ((bool)Right ? -1m : 0m) : Convert.ToDecimal(Right)));
            }
            double LeftNumber = ToDouble(Left), RightNumber = ToDouble(Right);
            if (double.IsNaN(LeftNumber) || double.IsNaN(RightNumber)) return null;
            return Math.Sign(LeftNumber.CompareTo(RightNumber));
        }

        public static bool Equal(object Left, object Right) => CompareCore(Left, Right) == 0;
        public static bool NotEqual(object Left, object Right) => CompareCore(Left, Right) != 0;
        public static bool LessEqual(object Left, object Right) => CompareCore(Left, Right) <= 0;
        public static bool Greater(object Left, object Right) => CompareCore(Left, Right) > 0;

        /// <summary>
        /// 按字符顺序比较两个字符串，null 等同空字符串。返回 -1、0 或 1。
        /// </summary>
        public static int CompareStr(string Left, string Right) => Math.Sign(string.CompareOrdinal(Left ?? "", Right ?? ""));

        #endregion

        #region 字符串

        /// <summary>取左侧最多 Length 个字符；null 视为空字符串。</summary>
        public static string Left(string Text, int Length)
        {
            if (Length < 0) throw new ArgumentException("长度不能为负数", nameof(Length));
            if (string.IsNullOrEmpty(Text)) return "";
            return Length >= Text.Length ? Text : Text.Substring(0, Length);
        }

        /// <summary>取右侧最多 Length 个字符；null 视为空字符串。</summary>
        public static string Right(string Text, int Length)
        {
            if (Length < 0) throw new ArgumentException("长度不能为负数", nameof(Length));
            if (string.IsNullOrEmpty(Text)) return "";
            return Length >= Text.Length ? Text : Text.Substring(Text.Length - Length);
        }

        /// <summary>字符串长度；null 为 0。</summary>
        public static int Len(string Text) => Text?.Length ?? 0;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern int LCMapStringEx(string LocaleName, uint MapFlags, string Source, int SourceLength, StringBuilder Destination, int DestinationLength, IntPtr VersionInformation, IntPtr Reserved, IntPtr SortHandle);
        private const uint LCMAP_SIMPLIFIED_CHINESE = 0x02000000;

        /// <summary>把繁体中文转换为简体中文（逐字映射，由系统提供）。</summary>
        public static string ToSimplifiedChinese(string Text)
        {
            if (string.IsNullOrEmpty(Text)) return "";
            var Buffer = new StringBuilder(Text.Length);
            int Written = LCMapStringEx("zh-CN", LCMAP_SIMPLIFIED_CHINESE, Text, Text.Length, Buffer, Text.Length, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return Written == 0 ? Text : Buffer.ToString(0, Written);
        }

        #endregion

        #region 其他

        /// <summary>
        /// 读取字符串开头的数字，忽略空白；遇到无法识别的字符即停止。不识别任何数字时返回 0。始终使用小数点 “.”。
        /// </summary>
        public static double Val(string Input)
        {
            if (Input is null) return 0d;
            int Length = Input.Length;
            int Index = 0;
            bool IsBlank(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '　';
            while (Index < Length && IsBlank(Input[Index])) Index++;
            if (Index >= Length) return 0d;
            // 十六进制 / 八进制
            if (Input[Index] == '&')
            {
                Index++;
                if (Index >= Length) return 0d;
                char Kind = char.ToLowerInvariant(Input[Index]);
                if (Kind != 'h' && Kind != 'o') return 0d;
                int Radix = Kind == 'h' ? 16 : 8;
                var Digits = new StringBuilder();
                for (Index++; Index < Length; Index++)
                {
                    char c = Input[Index];
                    if (IsBlank(c)) continue;
                    bool IsDigit = Radix == 16 ? Uri.IsHexDigit(c) : c >= '0' && c <= '7';
                    if (!IsDigit) break;
                    Digits.Append(c);
                }
                if (Digits.Length == 0) return 0d;
                long Number = Convert.ToInt64(Digits.ToString(), Radix);
                // 与 16 位 / 32 位整数的补码表示一致
                if (Number > int.MaxValue && Number <= uint.MaxValue) Number -= 0x100000000L;
                else if (Radix == 16 && Digits.Length <= 4 && Number > short.MaxValue) Number -= 0x10000L;
                return Number;
            }
            var Builder = new StringBuilder();
            bool HasDigit = false, HasDot = false, HasExponent = false;
            for (; Index < Length; Index++)
            {
                char c = Input[Index];
                if (IsBlank(c)) continue;
                if (c >= '0' && c <= '9') { Builder.Append(c); HasDigit = true; }
                else if (c == '.' && !HasDot && !HasExponent) { Builder.Append(c); HasDot = true; }
                else if ((c == '+' || c == '-') && (Builder.Length == 0 || Builder[Builder.Length - 1] == 'E')) Builder.Append(c);
                else if ((c == 'e' || c == 'E' || c == 'd' || c == 'D') && HasDigit && !HasExponent) { Builder.Append('E'); HasExponent = true; }
                else break;
            }
            if (!HasDigit) return 0d;
            string Text = Builder.ToString();
            // 去掉不完整的指数部分
            while (Text.Length > 0 && (Text[Text.Length - 1] == 'E' || Text[Text.Length - 1] == '+' || Text[Text.Length - 1] == '-')) Text = Text.Substring(0, Text.Length - 1);
            // 文本已保证是合法的数字格式，解析失败只可能是超出范围
            if (!double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double Result) || double.IsInfinity(Result))
                throw new OverflowException("数字过大");
            return Result;
        }

        /// <summary>
        /// 通配符匹配：? 匹配单个字符，* 匹配任意个字符，# 匹配单个数字，[abc] / [a-z] / [!abc] 匹配字符集合。区分大小写。
        /// </summary>
        public static bool Like(string Source, string Pattern)
        {
            Source ??= "";
            Pattern ??= "";
            return LikeCore(Source, 0, Pattern, 0);
        }

        private static bool LikeCore(string Source, int s, string Pattern, int p)
        {
            while (p < Pattern.Length)
            {
                char Token = Pattern[p];
                if (Token == '*')
                {
                    while (p < Pattern.Length && Pattern[p] == '*') p++;
                    if (p == Pattern.Length) return true;
                    for (int i = s; i <= Source.Length; i++)
                        if (LikeCore(Source, i, Pattern, p)) return true;
                    return false;
                }
                if (s >= Source.Length) return false;
                if (Token == '?')
                {
                    s++; p++;
                }
                else if (Token == '#')
                {
                    if (!char.IsDigit(Source[s])) return false;
                    s++; p++;
                }
                else if (Token == '[')
                {
                    int Close = Pattern.IndexOf(']', p + 1);
                    if (Close == -1) throw new ArgumentException("通配符中的 [ 没有对应的 ]");
                    string Set = Pattern.Substring(p + 1, Close - p - 1);
                    bool Negate = Set.StartsWith("!");
                    if (Negate) Set = Set.Substring(1);
                    bool Match = false;
                    if (Set.Length == 0 && !Negate)
                    {
                        // [] 匹配空串
                        p = Close + 1;
                        continue;
                    }
                    for (int i = 0; i < Set.Length; i++)
                    {
                        if (i + 2 < Set.Length && Set[i + 1] == '-')
                        {
                            if (Set[i] > Set[i + 2]) throw new ArgumentException("通配符中的字符范围无效");
                            if (Source[s] >= Set[i] && Source[s] <= Set[i + 2]) Match = true;
                            i += 2;
                        }
                        else if (Source[s] == Set[i])
                        {
                            Match = true;
                        }
                    }
                    if (Match == Negate) return false;
                    s++; p = Close + 1;
                }
                else
                {
                    if (Source[s] != Token) return false;
                    s++; p++;
                }
            }
            return s == Source.Length;
        }

        #endregion

    }
}
