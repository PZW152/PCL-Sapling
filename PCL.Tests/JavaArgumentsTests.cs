namespace PCL.Tests;

public class JavaArgumentsTests
{
    private static string Join(IEnumerable<string> Items) => string.Join("|", Items);

    [Test]
    public async Task 分割_按空格分割并忽略多余空白()
    {
        await Assert.That(Join(JavaArguments.Split("  -Xmx2G   -Xms1G  "))).IsEqualTo("-Xmx2G|-Xms1G");
        await Assert.That(Join(JavaArguments.Split(""))).IsEqualTo("");
        await Assert.That(Join(JavaArguments.Split(null))).IsEqualTo("");
        await Assert.That(Join(JavaArguments.Split("   "))).IsEqualTo("");
    }

    [Test]
    public async Task 分割_换行视为空格()
    {
        await Assert.That(Join(JavaArguments.Split("-a\r\n-b\n-c\r-d"))).IsEqualTo("-a|-b|-c|-d");
    }

    [Test]
    public async Task 分割_引号内的空格不分割且保留引号()
    {
        await Assert.That(Join(JavaArguments.Split("-Dpath=\"C:\\Program Files\\Java\" -Xmx2G"))).IsEqualTo("-Dpath=\"C:\\Program Files\\Java\"|-Xmx2G");
        await Assert.That(Join(JavaArguments.Split("\"a b\" \"c d\""))).IsEqualTo("\"a b\"|\"c d\"");
    }

    [Test]
    public async Task 分割_转义引号不切换引号状态()
    {
        // \" 原样保留，其后的空格仍然分割参数
        await Assert.That(Join(JavaArguments.Split("--title \\\"My Game\\\" --next"))).IsEqualTo("--title|\\\"My|Game\\\"|--next");
        // 引号内部的 \" 不会结束引号
        await Assert.That(Join(JavaArguments.Split("--title \"a \\\" b\" --next"))).IsEqualTo("--title|\"a \\\" b\"|--next");
    }

    [Test]
    public async Task 去重_单个参数重复时只保留一个()
    {
        var Result = JavaArguments.Deduplicate(["-XX:+UseG1GC", "-Xmx2G", "-XX:+UseG1GC"], true);
        await Assert.That(Join(Result)).IsEqualTo("-XX:+UseG1GC|-Xmx2G");
    }

    [Test]
    public async Task 去重_游戏参数的键值对以后者覆盖前者()
    {
        var Result = JavaArguments.Deduplicate(["--width", "854", "--height", "480", "--width", "1280"], false);
        await Assert.That(Join(Result)).IsEqualTo("--width|1280|--height|480");
    }

    [Test]
    public async Task 去重_tweakClass允许多个不同的值()
    {
        var Result = JavaArguments.Deduplicate(["--tweakClass", "a.A", "--tweakClass", "b.B", "--tweakClass", "a.A"], false);
        await Assert.That(Join(Result)).IsEqualTo("--tweakClass|a.A|--tweakClass|b.B");
    }

    [Test]
    public async Task 去重_JVM参数的键值对只去掉完全相同的()
    {
        var Result = JavaArguments.Deduplicate(["-cp", "a.jar", "-cp", "b.jar", "-cp", "a.jar"], true);
        await Assert.That(Join(Result)).IsEqualTo("-cp|a.jar|-cp|b.jar");
    }

    [Test]
    public async Task 去重_负数被视为前一个键的值()
    {
        // -xPos -50：-50 是值而不是另一个参数
        var Result = JavaArguments.Deduplicate(["-xPos", "23", "-xPos", "-50"], false);
        await Assert.That(Join(Result)).IsEqualTo("-xPos|-50");
    }

    [Test]
    public async Task 去重_后面跟着另一个参数时视为单个参数()
    {
        var Result = JavaArguments.Deduplicate(["--demo", "--fullscreen", "--demo", "--server", "a.com"], false);
        await Assert.That(Join(Result)).IsEqualTo("--demo|--fullscreen|--server|a.com");
    }

    [Test]
    public async Task 去重_不以减号开头的内容原样保留()
    {
        var Result = JavaArguments.Deduplicate(["net.minecraft.client.main.Main", "--username", "Steve", "net.minecraft.client.main.Main"], false);
        await Assert.That(Join(Result)).IsEqualTo("net.minecraft.client.main.Main|--username|Steve");
    }

    [Test]
    public async Task 去重_会报告处理了什么()
    {
        var Messages = new List<string>();
        JavaArguments.Deduplicate(["--width", "854", "--width", "1280", "-a", "-a"], false, Messages.Add);
        await Assert.That(Messages.Count).IsEqualTo(2);
        await Assert.That(Messages[0]).Contains("--width 854 → --width 1280");
    }

    [Test]
    public async Task 实际启动参数_分割后去重()
    {
        const string Raw = "--username ${auth_player_name} --version ${version_name} --gameDir ${game_directory} --assetsDir ${assets_root} --assetIndex ${assets_index_name} --uuid ${auth_uuid} --accessToken ${auth_access_token} --userProperties ${user_properties} --userType ${user_type} --height ${resolution_height} --width ${resolution_width} --width 854 --height 480";
        var Result = JavaArguments.Deduplicate(JavaArguments.Split(Raw).ToList(), false);
        await Assert.That(Result.Count).IsEqualTo(22);
        await Assert.That(Result[Result.IndexOf("--width") + 1]).IsEqualTo("854");
        await Assert.That(Result[Result.IndexOf("--height") + 1]).IsEqualTo("480");
        await Assert.That(Result.Count(a => a == "--width")).IsEqualTo(1);
    }
}
