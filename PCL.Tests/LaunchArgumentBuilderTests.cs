namespace PCL.Tests;

public class LaunchArgumentBuilderTests
{
    [Test]
    public async Task 替换占位符()
    {
        var Result = LaunchArgumentBuilder.Build(
            ["--username", "${auth_player_name}", "--gameDir", "${game_directory}", "-Djava.library.path=${natives_directory}", "--unknown", "${not_provided}"],
            [("${auth_player_name}", "Steve"), ("${game_directory}", @"C:\Games\.minecraft"), ("${natives_directory}", @"C:\n")]);
        await Assert.That(Result).IsEquivalentTo(new[] { "--username", "Steve", "--gameDir", @"C:\Games\.minecraft", @"-Djava.library.path=C:\n", "--unknown", "${not_provided}" });
    }

    [Test]
    public async Task 替换出来的内容不会再被替换()
    {
        // 原先依次对每个占位符调用 Replace，玩家名中的 ${...} 会被后面的替换再次处理
        var Result = LaunchArgumentBuilder.Build(["${auth_player_name}"], [("${auth_player_name}", "${auth_access_token}"), ("${auth_access_token}", "secret")]);
        await Assert.That(Result.Single()).IsEqualTo("${auth_access_token}");
    }

    [Test]
    public async Task 同一占位符使用第一个值()
    {
        var Result = LaunchArgumentBuilder.Build(["${version_type}"], [("${version_type}", "PCL"), ("${version_type}", "other")]);
        await Assert.That(Result.Single()).IsEqualTo("PCL");
    }

    [Test]
    public async Task 含特殊字符时加引号()
    {
        await Assert.That(LaunchArgumentBuilder.Quote("plain")).IsEqualTo("plain");
        await Assert.That(LaunchArgumentBuilder.Quote(@"C:\My Games")).IsEqualTo("\"C:\\My Games\"");
        await Assert.That(LaunchArgumentBuilder.Quote("a&b")).IsEqualTo("\"a&b\"");
        await Assert.That(LaunchArgumentBuilder.Quote("say \"hi\" now")).IsEqualTo("\"say \\\"hi\\\" now\"");
        await Assert.That(LaunchArgumentBuilder.Quote("\"already quoted\"")).IsEqualTo("\"already quoted\"");
        // 替换后才出现空格的参数同样会加引号
        var Result = LaunchArgumentBuilder.Build(["--gameDir", "${game_directory}"], [("${game_directory}", @"C:\My Games\.minecraft")]);
        await Assert.That(Result[1]).IsEqualTo("\"C:\\My Games\\.minecraft\"");
    }
}
