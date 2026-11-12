using System.Text;
using System.Text.RegularExpressions;

namespace PCLCS;

/// <summary>
/// 把简体中文（大陆用词）转换为繁体中文（港澳台都看得懂的用词，与 Sapling 一致）。
/// 先按最长匹配换词，再逐字转换；文件路径与网址保持原样，以免显示出与实际不同的路径。
/// 转换表由 tools/generate-zh-hant.py 产生。
/// </summary>
public static class ChineseConverter {

    private static readonly Lazy<(Dictionary<char, char> Chars, Dictionary<string, string> Phrases, Dictionary<char, int> MaxLength)> Table = new(Load);

    /// <summary>不转换的片段：Windows 路径、UNC 路径、网址。</summary>
    private static readonly Regex Protected = new(@"[A-Za-z]:\\[^\s""'<>|，。；：！？、）】」]*|\\\\[^\s""'<>|，。；：！？、）】」]+|[a-zA-Z][a-zA-Z0-9+.-]*://[^\s""'<>，。；！？、）】」]+", RegexOptions.Compiled);

    private static (Dictionary<char, char>, Dictionary<string, string>, Dictionary<char, int>) Load() {
        var Chars = new Dictionary<char, char>();
        var Phrases = new Dictionary<string, string>();
        var MaxLength = new Dictionary<char, int>();
        using var Stream = typeof(ChineseConverter).Assembly.GetManifestResourceStream("PCLCS.Lang.zh-Hant.txt")
            ?? throw new InvalidOperationException("缺少繁体转换表");
        using var Reader = new StreamReader(Stream, Encoding.UTF8);
        string? Line;
        while ((Line = Reader.ReadLine()) is not null) {
            if (Line.Length == 0 || Line[0] == '#')
                continue;
            int Tab = Line.IndexOf('\t');
            if (Tab <= 0)
                continue;
            string Key = Line.Substring(0, Tab), Value = Line.Substring(Tab + 1);
            if (Key.Length == 1 && Value.Length == 1) {
                Chars[Key[0]] = Value[0];
            } else {
                Phrases[Key] = Value;
                MaxLength[Key[0]] = Math.Max(MaxLength.TryGetValue(Key[0], out int Old) ? Old : 0, Key.Length);
            }
        }
        return (Chars, Phrases, MaxLength);
    }

    /// <summary>
    /// 是否可能需要转换：只有含有中日韩文字或中文引号时才需要。
    /// </summary>
    public static bool MayNeedConversion(string? Text) {
        if (string.IsNullOrEmpty(Text))
            return false;
        foreach (char c in Text!) {
            if ((c >= '㐀' && c <= '鿿') || c == '“' || c == '”' || c == '‘' || c == '’')
                return true;
        }
        return false;
    }

    /// <summary>
    /// 转换为繁体中文。已经是繁体的文字基本保持不变。
    /// </summary>
    public static string ToTraditional(string? Text) {
        if (Text is null)
            return "";
        if (!MayNeedConversion(Text))
            return Text;
        var Builder = new StringBuilder(Text.Length);
        int Last = 0;
        foreach (Match Keep in Protected.Matches(Text)) {
            ConvertSegment(Text, Last, Keep.Index, Builder);
            Builder.Append(Keep.Value);
            Last = Keep.Index + Keep.Length;
        }
        ConvertSegment(Text, Last, Text.Length, Builder);
        return Builder.ToString();
    }

    private static void ConvertSegment(string Text, int Start, int End, StringBuilder Builder) {
        var (Chars, Phrases, MaxLength) = Table.Value;
        int i = Start;
        while (i < End) {
            char c = Text[i];
            if (MaxLength.TryGetValue(c, out int Max)) {
                bool Matched = false;
                for (int Length = Math.Min(Max, End - i); Length >= 2; Length--) {
                    if (Phrases.TryGetValue(Text.Substring(i, Length), out var Phrase)) {
                        Builder.Append(Phrase);
                        i += Length;
                        Matched = true;
                        break;
                    }
                }
                if (Matched)
                    continue;
            }
            Builder.Append(Chars.TryGetValue(c, out char Traditional) ? Traditional : c);
            i++;
        }
    }
}

/// <summary>
/// 界面语言。原文是简体中文；选择繁体时，显示前经过 <see cref="ChineseConverter"/> 转换。
/// </summary>
public static class UiLanguage {

    /// <summary>设置中的值：0 跟随系统，1 简体中文，2 繁体中文。</summary>
    public const int FollowSystem = 0, Simplified = 1, Traditional = 2;

    /// <summary>当前是否显示繁体中文。启动时根据设置决定，切换后需要重新打开启动器。</summary>
    public static bool UseTraditional { get; set; }

    /// <summary>
    /// 根据设置与系统语言决定是否使用繁体：跟随系统时，繁体中文系统（台湾、香港、澳门）使用繁体。
    /// </summary>
    public static bool ResolveTraditional(int Setting, string SystemCultureName) => Setting switch {
        Traditional => true,
        Simplified => false,
        _ => IsTraditionalCulture(SystemCultureName),
    };

    public static bool IsTraditionalCulture(string Name) {
        Name = (Name ?? "").ToLowerInvariant();
        return Name.StartsWith("zh-hant") || Name is "zh-tw" or "zh-hk" or "zh-mo" || Name.StartsWith("zh-tw") || Name.StartsWith("zh-hk") || Name.StartsWith("zh-mo") || Name == "zh-cht";
    }

    /// <summary>
    /// 把要显示的文字转换为当前语言。
    /// </summary>
    public static string Display(string? Text) => UseTraditional ? ChineseConverter.ToTraditional(Text) : Text ?? "";
}
