using System.Globalization;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL.Tests;

/// <summary>
/// 对照测试：对大量输入，Coerce 的结果（或是否抛出异常）必须与 VB 运行库完全一致。
/// </summary>
public class CoerceTests
{
    public enum Sample { Zero = 0, One = 1, Five = 5 }
    public enum LongSample : long { Big = 5000000000L }

    private static readonly string[] Strings =
    {
        null, "", " ", "0", "1", "-1", "+3", "42", " 42 ", "1.5", "2.5", "-2.5", "3.4999", "1e3", "1E-2", "1,234", "1,234.56", "12abc", "abc",
        "True", "true", "TRUE", "False", "false", " True", "yes", "&H1F", "&h1f", "&O17", "&HFFFFFFFF", "&H", "0x10", "１２３", "٣", ".5", "5.", "-", "+", ".",
        "1.0", "12.0", "007", "2147483647", "2147483648", "-2147483648", "-2147483649", "9999999999", "1e400", "NaN", "Infinity", "$5", "5%", "(5)", // 未包含 "¥5"：VB 在 zh-CN 下先做全角转半角，会把该符号改写而解析失败，属于其自身的怪癖
        "4294967295", "4294967296", "3.5", "0.5", "-0.5", "1 2", "1\t", "\n7", "2026-11-11", "15:00:00", "1970/1/1 15:00:00", "2026/11/11", "a", "A", "龙", // 未包含 "&"：VB 的 Val 对它会抛出 IndexOutOfRangeException（其自身缺陷）
    };

    private static readonly object[] Objects =
    {
        null, "", "0", "1", "1.5", "abc", "True", "False", "&H10", " 7 ", true, false, 0, 1, -1, 5, 255, int.MaxValue, int.MinValue, 5L, long.MaxValue, 5000000000L,
        (short)3, (byte)200, (sbyte)-5, (ushort)7, 7u, uint.MaxValue, 7ul, ulong.MaxValue, 1.5, 2.5, -2.5, 0.0, 3.4999, 1e20, double.NaN, 1.5f, 2.5f, 0.1f, 1.5m, 2.5m, 100m,
        'a', '5', new DateTime(2026, 11, 11), new DateTime(1970, 1, 1, 15, 0, 0), new DateTime(1, 1, 1, 8, 30, 0), Sample.Five, Sample.Zero, LongSample.Big,
        new char[] { 'h', 'i' }, new object(), new int[] { 1 }, 0.1, 1.0 / 3, 123456789.123456789, 1e-7, 12345678901234567890d,
    };

    private static readonly string[] Cultures = { "zh-CN", "zh-TW", "en-US", "de-DE", "fr-FR", "ja-JP", "" };

    /// <summary>执行并把结果或异常类型规整成可比较的文本。</summary>
    private static string Run<T>(Func<T> Action)
    {
        try
        {
            T Result = Action();
            return Result switch
            {
                null => "<null>",
                double d => "double:" + d.ToString("R", CultureInfo.InvariantCulture),
                DateTime t => "date:" + t.Ticks,
                _ => Result.GetType().Name + ":" + Result
            };
        }
        catch (InvalidCastException) { return "!InvalidCast"; }
        catch (OverflowException) { return "!Overflow"; }
        catch (Exception ex) { return "!" + ex.GetType().Name; }
    }

    private static string Show(object Value) => Value is null ? "null" : Value is string s ? "\"" + s + "\"" : Value.GetType().Name + "(" + Value + ")";

    private static List<string> ForEachCulture(Action<string, List<string>> Body)
    {
        var Failures = new List<string>();
        var Saved = CultureInfo.CurrentCulture;
        try
        {
            foreach (var Name in Cultures)
            {
                CultureInfo.CurrentCulture = Name == "" ? CultureInfo.InvariantCulture : new CultureInfo(Name);
                Body(Name, Failures);
            }
        }
        finally { CultureInfo.CurrentCulture = Saved; }
        return Failures;
    }

    private static void Check(List<string> Failures, string Culture, string What, object Input, string Expected, string Actual)
    {
        if (Expected != Actual) Failures.Add($"[{Culture}] {What}({Show(Input)}): VB={Expected} Coerce={Actual}");
    }

    private static async Task AssertNone(List<string> Failures)
    {
        // 调试用：设置 PCL_TEST_DIFF 环境变量时，把完整差异写入该文件
        var Dump = Environment.GetEnvironmentVariable("PCL_TEST_DIFF");
        if (Failures.Count > 0 && !string.IsNullOrEmpty(Dump))
            File.AppendAllLines(Dump, Failures);
        await Assert.That(string.Join("\n", Failures.Take(40))).IsEqualTo("");
    }

    [Test]
    public async Task 字符串转换_与VB一致()
    {
        var Failures = ForEachCulture((Culture, F) =>
        {
            foreach (var s in Strings)
            {
                Check(F, Culture, "ToInt", s, Run(() => Conversions.ToInteger(s)), Run(() => Coerce.ToInt(s)));
                Check(F, Culture, "ToLong", s, Run(() => Conversions.ToLong(s)), Run(() => Coerce.ToLong(s)));
                Check(F, Culture, "ToUInt", s, Run(() => Conversions.ToUInteger(s)), Run(() => Coerce.ToUInt(s)));
                Check(F, Culture, "ToDouble", s, Run(() => Conversions.ToDouble(s)), Run(() => Coerce.ToDouble(s)));
                Check(F, Culture, "ToBool", s, Run(() => Conversions.ToBoolean(s)), Run(() => Coerce.ToBool(s)));
                Check(F, Culture, "ToChar", s, Run(() => Conversions.ToChar(s)), Run(() => Coerce.ToChar(s)));
                Check(F, Culture, "ToDate", s, Run(() => Conversions.ToDate(s)), Run(() => Coerce.ToDate(s)));
                Check(F, Culture, "Val", s, Run(() => Conversion.Val(s)), Run(() => Coerce.Val(s)));
            }
        });
        await AssertNone(Failures);
    }

    [Test]
    public async Task 对象转换_与VB一致()
    {
        var Failures = ForEachCulture((Culture, F) =>
        {
            foreach (var o in Objects)
            {
                Check(F, Culture, "ToStr", o, Run(() => Conversions.ToString(o)), Run(() => Coerce.ToStr(o)));
                Check(F, Culture, "ToInt", o, Run(() => Conversions.ToInteger(o)), Run(() => Coerce.ToInt(o)));
                Check(F, Culture, "ToLong", o, Run(() => Conversions.ToLong(o)), Run(() => Coerce.ToLong(o)));
                Check(F, Culture, "ToDouble", o, Run(() => Conversions.ToDouble(o)), Run(() => Coerce.ToDouble(o)));
                Check(F, Culture, "ToBool", o, Run(() => Conversions.ToBoolean(o)), Run(() => Coerce.ToBool(o)));
            }
        });
        await AssertNone(Failures);
    }

    [Test]
    public async Task 转为指定类型_与VB一致()
    {
        Type[] Targets = { typeof(int), typeof(long), typeof(bool), typeof(double), typeof(string), typeof(Sample), typeof(object) };
        var Failures = ForEachCulture((Culture, F) =>
        {
            foreach (var o in Objects.Concat(Strings))
            {
                foreach (var Target in Targets)
                    Check(F, Culture, "ChangeType→" + Target.Name, o, Run(() => Conversion.CTypeDynamic(o, Target)), Run(() => Coerce.ChangeType(o, Target)));
                Check(F, Culture, "To<int>", o, Run(() => Conversions.ToGenericParameter<int>(o)), Run(() => Coerce.To<int>(o)));
                Check(F, Culture, "To<bool>", o, Run(() => Conversions.ToGenericParameter<bool>(o)), Run(() => Coerce.To<bool>(o)));
                Check(F, Culture, "To<string>", o, Run(() => Conversions.ToGenericParameter<string>(o)), Run(() => Coerce.To<string>(o)));
                Check(F, Culture, "To<long>", o, Run(() => Conversions.ToGenericParameter<long>(o)), Run(() => Coerce.To<long>(o)));
                Check(F, Culture, "To<Sample>", o, Run(() => Conversions.ToGenericParameter<Sample>(o)), Run(() => Coerce.To<Sample>(o)));
            }
        });
        await AssertNone(Failures);
    }

    [Test]
    public async Task 拼接与比较_与VB一致()
    {
        var Failures = ForEachCulture((Culture, F) =>
        {
            foreach (var a in Objects)
                foreach (var b in Objects)
                {
                    object Pair = Show(a) + ", " + Show(b);
                    Check(F, Culture, "Concat", Pair, Run(() => Operators.ConcatenateObject(a, b)), Run(() => (object)Coerce.Concat(a, b)));
                    Check(F, Culture, "Equal", Pair, Run(() => Operators.ConditionalCompareObjectEqual(a, b, false)), Run(() => Coerce.Equal(a, b)));
                    Check(F, Culture, "NotEqual", Pair, Run(() => Operators.ConditionalCompareObjectNotEqual(a, b, false)), Run(() => Coerce.NotEqual(a, b)));
                    Check(F, Culture, "LessEqual", Pair, Run(() => Operators.ConditionalCompareObjectLessEqual(a, b, false)), Run(() => Coerce.LessEqual(a, b)));
                    Check(F, Culture, "Greater", Pair, Run(() => Operators.ConditionalCompareObjectGreater(a, b, false)), Run(() => Coerce.Greater(a, b)));
                }
            foreach (var a in Strings)
                foreach (var b in Strings)
                    Check(F, Culture, "CompareStr", Show(a) + ", " + Show(b), Run(() => Operators.CompareString(a, b, false)), Run(() => Coerce.CompareStr(a, b)));
        });
        await AssertNone(Failures);
    }

    [Test]
    public async Task 通配符匹配_与VB一致()
    {
        string[] Sources = { "", "a", "abc", "mods/a.jar", "config/x/y.toml", "a1b", "file.txt", "FILE.TXT", "a*b", "[x]", "x", "12", "a-b", "saves/world/level.dat", "options.txt" };
        string[] Patterns = { "", "*", "?", "a", "a*", "*c", "a?c", "*.jar", "mods/*", "config/*.toml", "a#b", "[abc]", "[a-c]*", "[!a]*", "*.t?t", "file.*", "*world*", "#*", "[x]", "a[*]b", "*/level.dat", "options.txt", "**", "a**c" };
        var Failures = new List<string>();
        foreach (var s in Sources)
            foreach (var p in Patterns)
                Check(Failures, "-", "Like", s + " ~ " + p, Run(() => LikeOperator.LikeString(s, p, CompareMethod.Binary)), Run(() => Coerce.Like(s, p)));
        await AssertNone(Failures);
    }
    [Test]
    public async Task 字符串函数_与VB一致()
    {
        var Failures = new List<string>();
        string[] Texts = { null, "", "a", "abc", "abcdefghij", "龙腾猫跃", "0123456789abcdef0123456789abcdef" };
        foreach (var s in Texts)
        {
            Check(Failures, "-", "Len", s, Run(() => Microsoft.VisualBasic.Strings.Len(s)), Run(() => Coerce.Len(s)));
            foreach (int n in new[] { -1, 0, 1, 2, 5, 10, 31, 32, 33, 100 })
            {
                Check(Failures, "-", "Left " + n, s, Run(() => Microsoft.VisualBasic.Strings.Left(s, n)), Run(() => Coerce.Left(s, n)));
                Check(Failures, "-", "Right " + n, s, Run(() => Microsoft.VisualBasic.Strings.Right(s, n)), Run(() => Coerce.Right(s, n)));
            }
        }
        await AssertNone(Failures);
    }

    [Test]
    public async Task 繁体转简体()
    {
        // 不与 VB 对照：VB 的 StrConv 会经过系统 ANSI 代码页，在繁体中文系统上把简体字变成 “?”。
        await Assert.That(Coerce.ToSimplifiedChinese(null)).IsEqualTo("");
        await Assert.That(Coerce.ToSimplifiedChinese("abc 123")).IsEqualTo("abc 123");
        await Assert.That(Coerce.ToSimplifiedChinese("機械動力")).IsEqualTo("机械动力");
        await Assert.That(Coerce.ToSimplifiedChinese("模組管理")).IsEqualTo("模组管理");
        await Assert.That(Coerce.ToSimplifiedChinese("龍騰貓躍 Launcher")).IsEqualTo("龙腾猫跃 Launcher");
        await Assert.That(Coerce.ToSimplifiedChinese("鑽石劍與鐵鎬")).IsEqualTo("钻石剑与铁镐");
        await Assert.That(Coerce.ToSimplifiedChinese("应用能源 2")).IsEqualTo("应用能源 2");
    }
}
