using System.Text.RegularExpressions;

namespace PCL.Tests;

/// <summary>
/// 设置项以字符串键访问，编译器无法检查拼写。
/// 这里扫描主程序的全部源码与 XAML，确保用到的每个键都在 Settings.Entries 中声明过。
/// </summary>
public class SettingKeyTests
{
    private static string FindSourceRoot()
    {
        var Dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (Dir is not null)
        {
            string Candidate = Path.Combine(Dir.FullName, "Plain Craft Launcher 2");
            if (Directory.Exists(Path.Combine(Candidate, "Pages", "PageSetup"))) return Candidate;
            Dir = Dir.Parent;
        }
        throw new DirectoryNotFoundException("找不到主程序源码目录");
    }

    private static IEnumerable<string> SourceFiles(string Root, string Pattern) =>
        Directory.EnumerateFiles(Root, Pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\obj\") && !f.Contains(@"\bin\"));

    private static List<string> DeclaredKeys(string Root)
    {
        string Text = File.ReadAllText(Path.Combine(Root, "Pages", "PageSetup", "Settings.cs"));
        return Regex.Matches(Text, @"new Setting\(""([A-Za-z0-9]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
    }

    [Test]
    public async Task 设置键没有重复声明()
    {
        var Keys = DeclaredKeys(FindSourceRoot());
        await Assert.That(Keys.Count > 60).IsTrue(); // 只是确认正则真的读到了设置表
        var Duplicated = Keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        await Assert.That(string.Join(", ", Duplicated)).IsEqualTo("");
    }

    [Test]
    public async Task 代码中用到的设置键都已声明()
    {
        string Root = FindSourceRoot();
        var Keys = new HashSet<string>(DeclaredKeys(Root));
        var Missing = new List<string>();
        var Usage = new Regex(@"Settings\.(?:Get|Set|SetSafe|GetSafe|Reset|HasSaved|GetDefault|TryGetValue)(?:<[^>(]*>)?\(\s*""([A-Za-z0-9]+)""");
        foreach (var File in SourceFiles(Root, "*.cs"))
            foreach (Match m in Usage.Matches(System.IO.File.ReadAllText(File)))
                if (!Keys.Contains(m.Groups[1].Value))
                    Missing.Add($"{Path.GetFileName(File)}: {m.Groups[1].Value}");
        await Assert.That(string.Join("\n", Missing.Distinct())).IsEqualTo("");
    }

    [Test]
    public async Task 界面中绑定的设置键都已声明()
    {
        string Root = FindSourceRoot();
        var Keys = new HashSet<string>(DeclaredKeys(Root));
        var Missing = new List<string>();
        var Usage = new Regex(@"(?:SettingService\.Key|RelativeSetup)=""([A-Za-z0-9]+)""");
        int Count = 0;
        foreach (var File in SourceFiles(Root, "*.xaml"))
            foreach (Match m in Usage.Matches(System.IO.File.ReadAllText(File)))
            {
                Count++;
                if (!Keys.Contains(m.Groups[1].Value))
                    Missing.Add($"{Path.GetFileName(File)}: {m.Groups[1].Value}");
            }
        await Assert.That(Count > 50).IsTrue();
        await Assert.That(string.Join("\n", Missing.Distinct())).IsEqualTo("");
    }
}
