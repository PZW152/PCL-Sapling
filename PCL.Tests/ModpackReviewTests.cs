using Newtonsoft.Json.Linq;

namespace PCL.Tests;

public class ModpackReviewTests
{
    private static JObject Index(params string[] Urls) => new()
    {
        ["files"] = new JArray(Urls.Select(u => new JObject { ["path"] = "mods/a.jar", ["downloads"] = new JArray(u) }))
    };

    [Test]
    public async Task 正常的整合包没有风险()
    {
        var Result = ModpackReview.ReviewModrinth(
            Index("https://cdn.modrinth.com/data/AANobbMI/versions/x/sodium.jar", "https://github.com/a/b/releases/download/1/c.jar", "https://edge.forgecdn.net/files/1/2/d.jar"),
            ["modrinth.index.json", "overrides/", "overrides/config/sodium-options.json", "overrides/options.txt"]);
        await Assert.That(Result.HasRisk).IsFalse();
        await Assert.That(Result.DownloadCount).IsEqualTo(3);
        await Assert.That(Result.BundledJarCount).IsEqualTo(0);
    }

    [Test]
    public async Task 来自其他网站的下载会被指出()
    {
        var Result = ModpackReview.ReviewModrinth(
            Index("https://cdn.modrinth.com/a.jar", "https://evil.example.com/payload.jar", "https://EVIL.example.com/b.jar", "http://cdn.modrinth.com/plain-http.jar"),
            ["modrinth.index.json"]);
        await Assert.That(Result.HasRisk).IsTrue();
        await Assert.That(Result.UntrustedHosts.Count).IsEqualTo(2);
        await Assert.That(Result.UntrustedHosts.Contains("evil.example.com")).IsTrue();
        await Assert.That(Result.UntrustedHosts.Any(h => h.StartsWith("http://"))).IsTrue();
    }

    [Test]
    public async Task 冒充可信域名的网站不被信任()
    {
        await Assert.That(ModpackReview.IsTrustedHost("cdn.modrinth.com")).IsTrue();
        await Assert.That(ModpackReview.IsTrustedHost("cdn.modrinth.com.evil.net")).IsFalse();
        await Assert.That(ModpackReview.IsTrustedHost("notgithub.com")).IsFalse();
        await Assert.That(ModpackReview.IsTrustedHost("evil-cdn.modrinth.com")).IsFalse();
    }

    [Test]
    public async Task 附带的可执行文件会被指出而jar只计数()
    {
        var Result = ModpackReview.ReviewModrinth(Index(),
            ["modrinth.index.json", "overrides/mods/private-mod.jar", "overrides/mods/other.JAR", "overrides/install.EXE", "overrides/run.bat", "overrides/readme.txt", "client-overrides/tool.ps1"]);
        await Assert.That(Result.HasRisk).IsTrue();
        await Assert.That(Result.ExecutableFiles.Count).IsEqualTo(3);
        await Assert.That(Result.BundledJarCount).IsEqualTo(2);
        await Assert.That(Result.UntrustedHosts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 仅服务端的文件不计入下载()
    {
        var Json = new JObject
        {
            ["files"] = new JArray(
                new JObject { ["downloads"] = new JArray("https://bad.example.org/server-only.jar"), ["env"] = new JObject { ["client"] = "unsupported" } },
                new JObject { ["downloads"] = new JArray("https://cdn.modrinth.com/client.jar"), ["env"] = new JObject { ["client"] = "required" } })
        };
        var Result = ModpackReview.ReviewModrinth(Json, []);
        await Assert.That(Result.DownloadCount).IsEqualTo(1);
        await Assert.That(Result.HasRisk).IsFalse();
    }

    [Test]
    public async Task 没有文件列表时不出错()
    {
        var Result = ModpackReview.ReviewModrinth(new JObject(), []);
        await Assert.That(Result.DownloadCount).IsEqualTo(0);
        await Assert.That(Result.HasRisk).IsFalse();
    }
}
