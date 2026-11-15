using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace PCL.Tests;

public class AppUpdateTests
{
    private static readonly string Digest = "sha256:" + new string('A', 64);

    private static JObject Release(string Tag, string? Digest, string? AssetName = null, bool Prerelease = false, string? Url = null)
    {
        AssetName ??= "PCL-Sapling-" + Tag.TrimStart('v') + ".exe";
        var Asset = new JObject { ["name"] = AssetName, ["browser_download_url"] = Url ?? "https://github.com/PZW152/PCL-Sapling/releases/download/" + Tag + "/" + AssetName };
        if (Digest is not null)
            Asset["digest"] = Digest;
        return new JObject
        {
            ["tag_name"] = Tag,
            ["prerelease"] = Prerelease,
            ["body"] = "更新说明",
            ["assets"] = new JArray(new JObject { ["name"] = "PCL-Sapling-1.1.0.exe.sha256", ["digest"] = "sha256:bad" }, Asset)
        };
    }

    [Test]
    public async Task 找到更新的版本()
    {
        var Update = AppUpdate.FromRelease(Release("v1.10.0", Digest), "1.9.9");
        await Assert.That(Update).IsNotNull();
        await Assert.That(Update!.Version).IsEqualTo("1.10.0");
        await Assert.That(Update.Sha256).IsEqualTo(new string('a', 64));
        await Assert.That(Update.Url).EndsWith("/PCL-Sapling-1.10.0.exe");
        await Assert.That(Update.Notes).IsEqualTo("更新说明");
    }

    [Test]
    public async Task 相同或更旧的版本不更新()
    {
        await Assert.That(AppUpdate.FromRelease(Release("v1.0.0", Digest), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v0.9.9", Digest), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", Digest, Prerelease: true), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("nightly", Digest), "1.0.0")).IsNull();
    }

    [Test]
    public async Task 没有校验值的文件不下载()
    {
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", null), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", "md5:abc"), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", "sha256:abc"), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", "sha256:" + new string('Z', 64)), "1.0.0")).IsNull();
    }

    [Test]
    public async Task 更新资产必须来自本仓库并对应发布标签()
    {
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", Digest, AssetName: "Plain Craft Launcher 2.exe"), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", Digest, AssetName: "PCL-Sapling-1.2.0.exe"), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", Digest, Url: "https://example.invalid/PCL-Sapling-1.1.0.exe"), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.01.0", Digest), "1.0.0")).IsNull();
    }

    [Test]
    public async Task 校验下载内容()
    {
        byte[] Content = [1, 2, 3, 4];
        string Hash;
        using (var Sha = SHA256.Create())
            Hash = BitConverter.ToString(Sha.ComputeHash(Content)).Replace("-", "");
        await Assert.That(AppUpdate.Verify(Content, Hash)).IsTrue();
        await Assert.That(AppUpdate.Verify(Content, Hash.ToLowerInvariant())).IsTrue();
        await Assert.That(AppUpdate.Verify([1, 2, 3], Hash)).IsFalse();
    }

    [Test]
    public async Task 替换运行中的程序并清理旧版本()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PCLTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        try
        {
            string Exe = Path.Combine(Folder, "PCL.exe");
            File.WriteAllText(Exe, "old");
            using (var Running = new FileStream(Exe, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                // 模拟正在运行：文件被打开时仍可以改名
                AppUpdate.Install(Exe, System.Text.Encoding.UTF8.GetBytes("new"));
            }
            await Assert.That(File.ReadAllText(Exe)).IsEqualTo("new");
            await Assert.That(File.ReadAllText(AppUpdate.OldPath(Exe))).IsEqualTo("old");
            await Assert.That(AppUpdate.RemoveOldVersion(Exe, 1, 0)).IsTrue();
            await Assert.That(File.Exists(AppUpdate.OldPath(Exe))).IsFalse();
        }
        finally
        {
            Directory.Delete(Folder, true);
        }
    }

    [Test]
    public async Task 解析版本号()
    {
        await Assert.That(AppUpdate.ParseVersion("v1.2.3")).IsEqualTo((1, 2, 3));
        await Assert.That(AppUpdate.ParseVersion("1.2")).IsNull();
        await Assert.That(AppUpdate.ParseVersion("1.2.x")).IsNull();
        await Assert.That(AppUpdate.ParseVersion(null)).IsNull();
    }
}
