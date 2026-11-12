using Newtonsoft.Json.Linq;

namespace PCL.Tests;

public class LaunchRulesTests
{
    private static readonly RuleEnvironment Win10 = new("windows", "10.0.26100.0", "x86_64");
    private static bool Check(string Json, RuleEnvironment? Env = null) => LaunchRules.Check(JToken.Parse(Json), Env ?? Win10);

    [Test]
    public async Task 没有规则时允许()
    {
        await Assert.That(LaunchRules.Check(null, Win10)).IsTrue();
    }

    [Test]
    public async Task 按操作系统允许或禁止()
    {
        await Assert.That(Check("""[{"action":"allow"}]""")).IsTrue();
        await Assert.That(Check("""[{"action":"allow"},{"action":"disallow","os":{"name":"osx"}}]""")).IsTrue();
        await Assert.That(Check("""[{"action":"allow","os":{"name":"osx"}}]""")).IsFalse();
        await Assert.That(Check("""[{"action":"allow","os":{"name":"linux"}}]""")).IsFalse();
        await Assert.That(Check("""[{"action":"allow","os":{"name":"windows"}}]""")).IsTrue();
        await Assert.That(Check("""[{"action":"allow"},{"action":"disallow","os":{"name":"windows"}}]""")).IsFalse();
        await Assert.That(Check("""[{"action":"allow","os":{"name":"unknown"}}]""")).IsTrue();
    }

    [Test]
    public async Task 操作系统版本按正则匹配()
    {
        // 1.12 及更早的版本 JSON：Windows 10 上加 -Dos.name=Windows 10
        const string Rule = """[{"action":"allow","os":{"name":"windows","version":"^10\\."}}]""";
        await Assert.That(Check(Rule)).IsTrue();
        await Assert.That(Check(Rule, Win10 with { OsVersion = "6.1.7601.0" })).IsFalse();
    }

    [Test]
    public async Task 架构规则()
    {
        // 32 位 natives 不用于 64 位 Java
        await Assert.That(Check("""[{"action":"allow","os":{"name":"windows","arch":"x86"}}]""")).IsFalse();
        await Assert.That(Check("""[{"action":"allow","os":{"name":"windows","arch":"x86"}}]""", Win10 with { Arch = "x86" })).IsTrue();
        await Assert.That(Check("""[{"action":"allow","os":{"name":"windows","arch":"x86_64"}}]""")).IsTrue();
        // ARM 的 natives 不用于 x64 Java（原先的判断写错了，ARM 规则总是成立）
        await Assert.That(Check("""[{"action":"allow","os":{"name":"windows","arch":"arm64"}}]""")).IsFalse();
        await Assert.That(Check("""[{"action":"allow","os":{"name":"windows","arch":"arm64"}}]""", Win10 with { Arch = "arm64" })).IsTrue();
    }

    [Test]
    public async Task 特性规则()
    {
        await Assert.That(Check("""[{"action":"allow","features":{"is_demo_user":true}}]""")).IsFalse();
        await Assert.That(Check("""[{"action":"allow","features":{"has_custom_resolution":true}}]""")).IsTrue();
        await Assert.That(Check("""[{"action":"allow","features":{"is_quick_play_singleplayer":true}}]""")).IsFalse();
        await Assert.That(Check("""[{"action":"allow","features":{"has_quick_plays_support":true}}]""")).IsFalse();
    }
}

public class MavenCoordinateTests
{
    // 原先 McLibGet 的算法，作为对照
    private static string Original(string Name)
    {
        string[] Splited = Name.Split(':');
        return $@"{Splited[0].Replace(".", @"\")}\{Splited[1]}\{Splited[2]}\{string.Join("-", Splited.Skip(1))}.jar";
    }

    [Test]
    public async Task 与原算法相同()
    {
        foreach (var Name in new[] {
            "com.mumfrey:liteloader:1.12.2-SNAPSHOT",
            "org.lwjgl:lwjgl:3.3.3:natives-windows",
            "net.minecraftforge:forge:1.21.4-54.0.34:client",
            "net.fabricmc:fabric-loader:0.16.9",
            "a.b.c:d:1:e:f" })
        {
            await Assert.That(MavenCoordinate.Parse(Name).RelativePath).IsEqualTo(Original(Name));
        }
    }

    [Test]
    public async Task 分类器只出现在文件名中()
    {
        var Coordinate = MavenCoordinate.Parse("net.minecraftforge:forge:1.21.4-54.0.34:client");
        await Assert.That(Coordinate.Version).IsEqualTo("1.21.4-54.0.34");
        await Assert.That(Coordinate.Classifier).IsEqualTo("client");
        await Assert.That(Coordinate.RelativePath).IsEqualTo(@"net\minecraftforge\forge\1.21.4-54.0.34\forge-1.21.4-54.0.34-client.jar");
    }

    [Test]
    public async Task 支持扩展名()
    {
        // 原先会把 @zip 留在版本号里，并且总是使用 .jar
        var Coordinate = MavenCoordinate.Parse("de.oceanlabs.mcp:mcp_config:1.20.1-20230612.114412@zip");
        await Assert.That(Coordinate.RelativePath).IsEqualTo(@"de\oceanlabs\mcp\mcp_config\1.20.1-20230612.114412\mcp_config-1.20.1-20230612.114412.zip");
    }

    [Test]
    public async Task 无效坐标()
    {
        await Assert.That(() => MavenCoordinate.Parse("only:two")).Throws<FormatException>();
    }
}
