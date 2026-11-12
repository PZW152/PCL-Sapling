using Newtonsoft.Json.Linq;

namespace PCL.Tests;

public class QuiltDetectionTests
{
    private static McVersion Parse(string Library) => new("Test", JObject.Parse($$"""
        {
          "id": "Test",
          "inheritsFrom": "1.21.1",
          "releaseTime": "2024-08-08T12:24:45+00:00",
          "type": "release",
          "mainClass": "org.quiltmc.loader.impl.launch.knot.KnotClient",
          "libraries": [ { "name": "{{Library}}" } ],
          "arguments": { "game": [] },
          "clientVersion": "1.21.1"
        }
        """));

    [Test]
    public async Task Quilt_按Fabric兼容并记下版本()
    {
        var Version = Parse("org.quiltmc:quilt-loader:0.30.2");
        await Assert.That(Version.IsQuilt).IsTrue();
        await Assert.That(Version.FabricLikeName).IsEqualTo("Quilt");
        await Assert.That(Version.ModLoaderVersions[ModLoaders.Fabric]).IsEqualTo("0.30.2");
        await Assert.That(Version.ModLoaderVersions.ContainsKey(ModLoaders.Quilt)).IsFalse();
    }

    [Test]
    public async Task Fabric_不是Quilt()
    {
        var Version = Parse("net.fabricmc:fabric-loader:0.16.5");
        await Assert.That(Version.IsQuilt).IsFalse();
        await Assert.That(Version.FabricLikeName).IsEqualTo("Fabric");
        await Assert.That(Version.ModLoaderVersions[ModLoaders.Fabric]).IsEqualTo("0.16.5");
    }

    [Test]
    public async Task Quilt_安装请求单独记录()
    {
        var Components = new McComponents("1.21.1", null, null, null, null, null, null, null, quiltVersion: "0.30.2");
        await Assert.That(Components.ModLoaderVersions[ModLoaders.Quilt]).IsEqualTo("0.30.2");
        await Assert.That(Components.ModLoaderVersions.ContainsKey(ModLoaders.Fabric)).IsFalse();
    }
}
