using System.Text.RegularExpressions;

namespace PCL.Tests;

/// <summary>
/// 从 VB 转换来的代码中容易出现、但编译器不会报错的写法。
/// </summary>
public class ConversionPitfallTests
{
    private static string SourceRoot()
    {
        var Dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (Dir is not null && !Directory.Exists(Path.Combine(Dir.FullName, "Plain Craft Launcher 2", "Modules"))) Dir = Dir.Parent;
        return Path.Combine(Dir!.FullName, "Plain Craft Launcher 2");
    }

    [Test]
    public async Task 字符数组不能直接与字符串相加()
    {
        // VB 的 Chars() & "x" 会把字符拼起来；C# 的 Chars() + "x" 却会得到 "System.Char[]x"。
        // 曾导致文件夹名验证拒绝所有含 s、y、t、e、m、a、r 等字母的名称。
        var Pattern = new Regex(@"(GetInvalid(FileName|Path)Chars\(\)|ToCharArray\(\))\s*\+");
        var Found = Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\obj\"))
            .SelectMany(f => File.ReadAllLines(f).Select((Line, i) => (f, Line, i)))
            .Where(x => Pattern.IsMatch(x.Line))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}")
            .ToList();
        await Assert.That(string.Join("\n", Found)).IsEqualTo("");
    }
}
