using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace PCL.Tests;

public class AppUpdateTests
{
    private static JObject Release(string Tag, string? Digest, string AssetName = "PCL-Sapling-1.1.0.exe", bool Prerelease = false)
    {
        var Asset = new JObject { ["name"] = AssetName, ["browser_download_url"] = "https://github.com/PZW152/PCL-Sapling/releases/download/" + Tag + "/" + AssetName };
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
        var Update = AppUpdate.FromRelease(Release("v1.10.0", "sha256:ABC"), "1.9.9");
        await Assert.That(Update).IsNotNull();
        await Assert.That(Update!.Version).IsEqualTo("1.10.0");
        await Assert.That(Update.Sha256).IsEqualTo("abc");
        await Assert.That(Update.Url).EndsWith("/PCL-Sapling-1.1.0.exe");
        await Assert.That(Update.Notes).IsEqualTo("更新说明");
    }

    [Test]
    public async Task 相同或更旧的版本不更新()
    {
        await Assert.That(AppUpdate.FromRelease(Release("v1.0.0", "sha256:abc"), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v0.9.9", "sha256:abc"), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", "sha256:abc", Prerelease: true), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("nightly", "sha256:abc"), "1.0.0")).IsNull();
    }

    [Test]
    public async Task 没有校验值的文件不下载()
    {
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", null), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", "md5:abc"), "1.0.0")).IsNull();
        await Assert.That(AppUpdate.FromRelease(Release("v1.1.0", "sha256:abc", AssetName: "Plain Craft Launcher 2.exe"), "1.0.0")).IsNull();
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
