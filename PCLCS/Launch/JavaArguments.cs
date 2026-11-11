namespace PCLCS;

/// <summary>
/// Java / Minecraft 启动参数的分割与去重。
/// </summary>
public static class JavaArguments {

    /// <summary>
    /// 把一整段参数文本分割为单个参数。
    /// 双引号内的空格不会分割参数；\" 视为转义的引号，原样保留。换行视为空格。
    /// </summary>
    public static IEnumerable<string> Split(string? arguments) {
        string text = (arguments ?? "").ReplaceLineEndings(" ", mergeMultiple: true).Trim();
        if (string.IsNullOrWhiteSpace(text)) yield break;
        StringBuilder current = new();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length && text[i + 1] == '"') {
                // 转义的双引号：保留反斜杠和引号，不切换引号状态
                current.Append("\\\"");
                i++;
            } else if (c == '"') {
                inQuotes = !inQuotes;
                current.Append(c);
            } else if (c == ' ' && !inQuotes) {
                // 引号外的空格：一个参数结束
                if (current.Length > 0) {
                    yield return current.ToString();
                    current.Clear();
                }
            } else {
                current.Append(c);
            }
        }
        if (current.Length > 0) yield return current.ToString();
    }

    /// <summary>
    /// 对参数去重。
    /// <para>单个参数（如 -XX:+UseG1GC）：重复的直接丢弃。</para>
    /// <para>键值对（如 --width 854）：游戏参数以后出现的值覆盖先前的值（--tweakClass 除外，它允许多个不同的值）；
    /// JVM 参数只丢弃键与值都相同的重复项。</para>
    /// </summary>
    /// <param name="isJvmArguments">是否为 JVM 参数；否则为游戏参数。</param>
    /// <param name="log">可选，接收去重过程的说明文本。</param>
    public static List<string> Deduplicate(IReadOnlyList<string> arguments, bool isJvmArguments, Action<string>? log = null) {
        string kind = isJvmArguments ? " JVM " : "游戏";
        List<string> result = [];
        int i = 0;
        while (i < arguments.Count) {
            string key = arguments[i];
            if (!IsKeyOfPair(arguments, i)) {
                // 单个参数
                i++;
                if (result.Contains(key)) {
                    log?.Invoke($"已去重单个{kind}参数：{key}");
                    continue;
                }
                result.Add(key);
                continue;
            }
            // 以空格间隔的键值对（例如 --width 233）
            string value = arguments[i + 1];
            i += 2;
            bool handled = false;
            for (int j = 0; j <= result.Count - 2; j++) {
                if (result[j] != key) continue;
                if (!isJvmArguments && key != "--tweakClass") {
                    // 多个相同的 --width、--uuid 等参数会让它们全部失效，所以用新的值覆盖旧的值
                    log?.Invoke($"已覆盖重复的{kind}参数：{key} {result[j + 1]} → {key} {value}");
                    result[j + 1] = value;
                    handled = true;
                    break;
                }
                if (result[j + 1] == value) {
                    log?.Invoke($"已去重{kind}参数对：{key} {value}");
                    handled = true;
                    break;
                }
            }
            if (handled) continue;
            result.Add(key);
            result.Add(value);
        }
        return result;
    }

    /// <summary>
    /// 位于 index 的参数是否是一个键值对的键：它以 - 开头，且后一个参数是它的值。
    /// 后一个参数若以 - 开头，仅当它是负数（如 -xPos -50 中的 -50）时才视为值。
    /// </summary>
    private static bool IsKeyOfPair(IReadOnlyList<string> arguments, int index) {
        if (!arguments[index].StartsWith("-", StringComparison.Ordinal)) return false;
        if (index + 1 >= arguments.Count) return false;
        string next = arguments[index + 1];
        if (!next.StartsWith("-", StringComparison.Ordinal)) return true;
        return LeadingNumber(next.Substring(1)) != 0d;
    }

    private static double LeadingNumber(string text) {
        try {
            return Coerce.Val(text);
        } catch (Exception) {
            return 0d;
        }
    }

}
