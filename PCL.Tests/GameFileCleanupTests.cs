using Newtonsoft.Json.Linq;

namespace PCL.Tests;

public class GameFileCleanupTests
{
    private static string NewFolder()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PCLTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        return Folder;
    }
    private static void Write(string Root, string Relative, string Content = "x")
    {
        string Path_ = Path.Combine(Root, Relative);
        Directory.CreateDirectory(Path.GetDirectoryName(Path_)!);
        File.WriteAllText(Path_, Content);
    }

    [Test]
    public async Task 只清理没有版本在用的文件()
    {
        string Mc = NewFolder();
        try
        {
            Write(Mc, @"assets\indexes\17.json");
            Write(Mc, @"assets\indexes\5.json");
            Write(Mc, @"assets\objects\aa\aa11");
            Write(Mc, @"assets\objects\bb\bb22", "1234");
            Write(Mc, @"libraries\org\lwjgl\lwjgl\3.3.3\lwjgl-3.3.3.jar");
            Write(Mc, @"libraries\com\old\old\1.0\old-1.0.jar", "12345678");
            Write(Mc, @"versions\1.21.1\1.21.1.json"); // 版本文件夹不在清理范围
            var Keep = new GameFileCleanup.Keep();
            Keep.AddAssetIndex("17", JObject.Parse("""{"objects":{"minecraft/lang/zh_cn.json":{"hash":"aa11","size":1}}}"""));
            Keep.AddLibrary("org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar");
            var Plan = GameFileCleanup.Plan(Mc, Keep);
            var Names = Plan.Files.Select(f => f.Substring(Mc.Length + 1)).OrderBy(f => f).ToList();
            await Assert.That(Names).IsEquivalentTo(new[] { @"assets\indexes\5.json", @"assets\objects\bb\bb22", @"libraries\com\old\old\1.0\old-1.0.jar" });
            await Assert.That(Plan.Bytes).IsEqualTo(1 + 4 + 8);

            long Freed = GameFileCleanup.Run(Mc, Plan);
            await Assert.That(Freed).IsEqualTo(13);
            await Assert.That(File.Exists(Path.Combine(Mc, @"assets\objects\aa\aa11"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(Mc, @"libraries\org\lwjgl\lwjgl\3.3.3\lwjgl-3.3.3.jar"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(Mc, @"versions\1.21.1\1.21.1.json"))).IsTrue();
            // 变空的文件夹一并删除
            await Assert.That(Directory.Exists(Path.Combine(Mc, @"libraries\com"))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(Mc, @"assets\objects\bb"))).IsFalse();
        }
        finally
        {
            Directory.Delete(Mc, true);
        }
    }

    [Test]
    public async Task 无法确定支持库时不清理支持库()
    {
        string Mc = NewFolder();
        try
        {
            Write(Mc, @"libraries\net\minecraft\client\1.20.1-20230612.114412\client-1.20.1-20230612.114412-srg.jar");
            var Keep = new GameFileCleanup.Keep { Libraries = null };
            await Assert.That(GameFileCleanup.Plan(Mc, Keep).Files).IsEmpty();
        }
        finally
        {
            Directory.Delete(Mc, true);
        }
    }

    [Test]
    public async Task 斜线写法不影响比较()
    {
        await Assert.That(GameFileCleanup.NormalizeLibrary("org/a/b.jar")).IsEqualTo(@"org\a\b.jar");
        await Assert.That(GameFileCleanup.NormalizeLibrary(@"\org\a\b.jar")).IsEqualTo(@"org\a\b.jar");
        string Mc = NewFolder();
        try
        {
            Write(Mc, @"libraries\Org\A\b.jar");
            var Keep = new GameFileCleanup.Keep();
            Keep.AddLibrary("org/a/B.jar"); // Windows 路径不区分大小写
            await Assert.That(GameFileCleanup.Plan(Mc, Keep).Files).IsEmpty();
        }
        finally
        {
            Directory.Delete(Mc, true);
        }
    }
}
