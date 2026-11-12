namespace PCL.Tests;

public class ValidationTests
{
    private static string NewFolder()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PCLTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Folder, "Existing"));
        return Folder;
    }

    [Test]
    public async Task 文件夹名()
    {
        string Folder = NewFolder();
        try
        {
            var Rule = new ValidateFolderName(Folder);
            await Assert.That(Rule.Validate("Hello World")).IsEqualTo("");
            // 曾因 char[] 与 string 相加，把含有 System.Char[] 中字母的名字判为非法
            await Assert.That(Rule.Validate("System Char 1.21")).IsEqualTo("");
            await Assert.That(Rule.Validate(" a")).IsEqualTo("文件夹名不能以空格开头！");
            await Assert.That(Rule.Validate("a ")).IsEqualTo("文件夹名不能以空格结尾！");
            await Assert.That(Rule.Validate("a.")).IsEqualTo("文件夹名不能以小数点结尾！");
            await Assert.That(Rule.Validate("a?b")).IsEqualTo("文件夹名不可包含 ? 字符！");
            await Assert.That(Rule.Validate("a!b")).IsEqualTo("文件夹名不可包含 ! 字符！");
            await Assert.That(new ValidateFolderName(Folder, UseMinecraftCharCheck: false).Validate("a!b")).IsEqualTo("");
            await Assert.That(Rule.Validate("con")).IsEqualTo("文件夹名不可为 CON！");
            await Assert.That(Rule.Validate("PROGRA~1")).IsEqualTo("文件夹名不能包含这一特殊格式！");
            await Assert.That(Rule.Validate("existing")).IsEqualTo("不可与现有文件夹重名！");
            await Assert.That(new ValidateFolderName(Folder, IgnoreList: ["Existing"]).Validate("existing")).IsEqualTo("");
            await Assert.That(Rule.Validate(new string('a', 101))).IsNotEqualTo("");
        }
        finally
        {
            Directory.Delete(Folder, true);
        }
    }

    [Test]
    public async Task 文件夹路径()
    {
        string Drive = Path.GetPathRoot(Path.GetTempPath())!.ToUpperInvariant(); // 例如 C:\
        var Rule = new ValidateFolderPath();
        await Assert.That(Rule.Validate(Drive)).IsEqualTo("");
        await Assert.That(Rule.Validate(Drive + @"Games\.minecraft")).IsEqualTo("");
        await Assert.That(Rule.Validate(Drive.Replace('\\', '/') + "Games/.minecraft/")).IsEqualTo("");
        await Assert.That(Rule.Validate(Drive + @"Games\a?b")).IsEqualTo("路径中存在无效字符！");
        await Assert.That(Rule.Validate(Drive + @"Games \x")).IsEqualTo("文件夹名不能以空格结尾！");
        await Assert.That(Rule.Validate(Drive + @"Games\x.")).IsEqualTo("文件夹名不能以小数点结尾！");
        await Assert.That(Rule.Validate(Drive + @"Games\\x")).IsEqualTo("文件夹路径存在错误！");
        // 设备名不区分大小写（原先小写可以通过）
        await Assert.That(Rule.Validate(Drive + @"Games\CON")).IsEqualTo("文件夹名不可为 CON！");
        await Assert.That(Rule.Validate(Drive + @"Games\con")).IsEqualTo("文件夹名不可为 CON！");
        await Assert.That(Rule.Validate(@"\\Mac\Home\Games")).IsEqualTo("");
        // 不存在的盘符
        char Missing = Enumerable.Range('D', 23).Select(c => (char)c).First(c => !Directory.Exists(c + @":\"));
        await Assert.That(Rule.Validate(Missing + @":\Games")).IsEqualTo("文件夹路径头存在错误！");
        await Assert.That(Rule.Validate("Games")).IsEqualTo("文件夹路径头存在错误！");
    }

    [Test]
    public async Task 数字长度与网址()
    {
        var Integer = new ValidateInteger(1, 100);
        await Assert.That(Integer.Validate("50")).IsEqualTo("");
        await Assert.That(Integer.Validate("0")).IsEqualTo("不可低于 1！");
        await Assert.That(Integer.Validate("101")).IsEqualTo("不可超过 100！");
        await Assert.That(Integer.Validate("1.5")).IsNotEqualTo("");
        await Assert.That(new ValidateLength(2, 3).Validate("ab")).IsEqualTo("");
        await Assert.That(new ValidateLength(2, 3).Validate("a")).IsNotEqualTo("");
        await Assert.That(new ValidateHttp().Validate("https://example.com/a")).IsEqualTo("");
        await Assert.That(new ValidateHttp().Validate("example")).IsEqualTo("输入的网址无效！");
    }

    [Test]
    public async Task 规则依次检查()
    {
        // ValidateNullable：为空时直接通过，不再检查后面的规则
        await Assert.That(ModValidate.Validate("", [new ValidateNullable(), new ValidateLength(5)])).IsEqualTo("");
        await Assert.That(ModValidate.Validate("abc", [new ValidateNullable(), new ValidateLength(5)])).IsNotEqualTo("");
    }
}
