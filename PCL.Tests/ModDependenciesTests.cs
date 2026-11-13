using System.IO.Compression;
using System.Text;

namespace PCL.Tests;

public class ModDependenciesTests
{
    /// <summary>
    /// 在内存中做一个 jar：每一项是压缩包中的路径与内容（文本或另一个 jar 的字节）。
    /// </summary>
    private static byte[] Jar(params (string Path, object Content)[] Entries)
    {
        using var Memory = new MemoryStream();
        using (var Zip = new ZipArchive(Memory, ZipArchiveMode.Create, true))
            foreach (var (Path, Content) in Entries)
            {
                using var Stream = Zip.CreateEntry(Path).Open();
                byte[] Bytes = Content is byte[] Raw ? Raw : Encoding.UTF8.GetBytes((string)Content);
                Stream.Write(Bytes, 0, Bytes.Length);
            }
        return Memory.ToArray();
    }

    private static ModMetadata Read(byte[] Jar, string Name = "test.jar") => ModDependencies.Read(new MemoryStream(Jar), Name)!;

    [Test]
    public async Task Fabric_读取依赖与内嵌的Mod()
    {
        byte[] Inner = Jar(("fabric.mod.json", """{"id":"cloth-basic-math"}"""));
        var Mod = Read(Jar(
            ("fabric.mod.json", """{"id":"cloth-config","name":"Cloth Config","provides":["cloth_config"],"depends":{"fabricloader":">=0.14","minecraft":"1.21.x","fabric-api":"*","cloth-basic-math":"*"},"jars":[{"file":"META-INF/jars/math.jar"}]}"""),
            ("META-INF/jars/math.jar", Inner)));
        await Assert.That(Mod.Name).IsEqualTo("Cloth Config");
        await Assert.That(Mod.Provides.Contains("cloth-config")).IsTrue();
        await Assert.That(Mod.Provides.Contains("cloth_config")).IsTrue();
        await Assert.That(Mod.Provides.Contains("cloth-basic-math")).IsTrue();
        // 内嵌的不算缺少；内置的在 FindMissing 中过滤
        await Assert.That(Mod.Requires.Contains("cloth-basic-math")).IsFalse();
        await Assert.That(Mod.Requires.Contains("fabric-api")).IsTrue();
    }

    [Test]
    public async Task 找出缺少的前置()
    {
        var Sodium = Read(Jar(("fabric.mod.json", """{"id":"sodium","name":"Sodium","depends":{"minecraft":"*","fabricloader":"*","fabric-api":"*"}}""")), "sodium.jar");
        var Iris = Read(Jar(("fabric.mod.json", """{"id":"iris","name":"Iris","depends":{"sodium":"*","java":">=21"}}""")), "iris.jar");
        var Missing = ModDependencies.FindMissing(new[] { Sodium, Iris });
        await Assert.That(Missing.Count).IsEqualTo(1);
        await Assert.That(Missing[0].ModName).IsEqualTo("Sodium");
        await Assert.That(Missing[0].RequiredId).IsEqualTo("fabric-api");
        var Api = Read(Jar(("fabric.mod.json", """{"id":"fabric-api","provides":["fabric"]}""")), "fabric-api.jar");
        await Assert.That(ModDependencies.FindMissing(new[] { Sodium, Iris, Api }).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Quilt_可选依赖不算()
    {
        var Mod = Read(Jar(("quilt.mod.json", """{"quilt_loader":{"id":"qmod","metadata":{"name":"Q Mod"},"depends":["quilt_loader",{"id":"qsl"},{"id":"optional_thing","optional":true}]}}""")));
        await Assert.That(Mod.Name).IsEqualTo("Q Mod");
        await Assert.That(Mod.Requires.Contains("qsl")).IsTrue();
        await Assert.That(Mod.Requires.Contains("optional_thing")).IsFalse();
        await Assert.That(ModDependencies.FindMissing(new[] { Mod }).Select(m => m.RequiredId).ToList()).IsEquivalentTo(new List<string> { "qsl" });
    }

    [Test]
    public async Task Forge_旧格式与NeoForge新格式()
    {
        string OldToml = """
            modLoader="javafml"
            [[mods]]
            modId="create"
            displayName="Create" # 注释
            [[dependencies.create]]
                modId="forge"
                mandatory=true
            [[dependencies.create]]
                modId="flywheel"
                mandatory=true
            [[dependencies.create]]
                modId="jei"
                mandatory=false
            """;
        var Create = Read(Jar(("META-INF/mods.toml", OldToml)));
        await Assert.That(Create.Name).IsEqualTo("Create");
        await Assert.That(ModDependencies.FindMissing(new[] { Create }).Select(m => m.RequiredId).ToList()).IsEquivalentTo(new List<string> { "flywheel" });
        string NewToml = """
            [[mods]]
            modId='mekanism'
            [[dependencies.mekanism]]
            modId="neoforge"
            type="required"
            [[dependencies.mekanism]]
            modId="jei"
            type="optional"
            """;
        var Mek = Read(Jar(("META-INF/neoforge.mods.toml", NewToml)));
        await Assert.That(ModDependencies.FindMissing(new[] { Mek }).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 不是Mod或损坏时返回空()
    {
        await Assert.That(ModDependencies.Read(new MemoryStream(Jar(("readme.txt", "hi"))), "x.jar")).IsNull();
        await Assert.That(ModDependencies.Read(new MemoryStream(Encoding.UTF8.GetBytes("not a zip")), "y.jar")).IsNull();
    }
}
