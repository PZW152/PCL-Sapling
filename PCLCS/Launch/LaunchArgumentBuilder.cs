using System.Text.RegularExpressions;

namespace PCLCS;

/// <summary>
/// 生成最终的启动参数：替换版本 JSON 中的 ${...} 占位符，并为含有特殊字符的参数加上双引号。
/// </summary>
public static class LaunchArgumentBuilder {

    private static readonly Regex Placeholder = new(@"\$\{[^}]+\}", RegexOptions.Compiled);
    private static readonly char[] SpecialChars = ['&', '|', '<', '>', '^', ' '];

    /// <summary>
    /// 替换每个参数中的占位符并按需加引号。
    /// 替换只进行一次：替换进来的内容（例如玩家名、路径）即使含有 ${...} 也不会再被替换。
    /// 同一个占位符出现多次时使用第一个值；没有对应值的占位符保持原样。
    /// </summary>
    public static List<string> Build(IEnumerable<string> Arguments, IEnumerable<(string Key, string Value)> Replacements) {
        var Values = new Dictionary<string, string>();
        foreach (var (Key, Value) in Replacements) {
            if (!Values.ContainsKey(Key))
                Values[Key] = Value;
        }
        return Arguments.Select(Arg => Quote(Placeholder.Replace(Arg, m => Values.TryGetValue(m.Value, out var Value) ? Value : m.Value))).ToList();
    }

    /// <summary>
    /// 参数含有空格或 cmd 的特殊字符（&amp; | &lt; &gt; ^），且两端没有引号时，加上双引号，并转义其中原有的双引号。
    /// </summary>
    public static string Quote(string Arg) {
        if (Arg.IndexOfAny(SpecialChars) < 0 || Arg.StartsWith("\"") || Arg.EndsWith("\""))
            return Arg;
        return "\"" + Arg.Replace("\"", "\\\"") + "\"";
    }
}
