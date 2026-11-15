namespace PCL.Tests;

public class DownloadOutputTests {
    [Test]
    public async Task 未通过校验的下载不会替换现有文件() {
        string Folder = Path.Combine(Path.GetTempPath(), "PclDownload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        try {
            string Target = Path.Combine(Folder, "client.jar");
            File.WriteAllText(Target, "old");
            using (var Output = new DownloadOutput(Target)) {
                File.WriteAllText(Output.StagingPath, "partial");
                await Assert.That(File.ReadAllText(Target)).IsEqualTo("old");
                // 校验失败或取消时，调用方不发布暂存文件。
            }
            await Assert.That(File.ReadAllText(Target)).IsEqualTo("old");
            await Assert.That(Directory.GetFiles(Folder)).IsEquivalentTo(new[] { Target });
        } finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 发布可创建新文件或替换旧文件() {
        string Folder = Path.Combine(Path.GetTempPath(), "PclDownload-" + Guid.NewGuid().ToString("N"));
        string Target = Path.Combine(Folder, "client.jar");
        try {
            using (var First = new DownloadOutput(Target)) {
                File.WriteAllText(First.StagingPath, "first");
                await Assert.That(File.Exists(Target)).IsFalse();
                First.Publish();
            }
            File.SetAttributes(Target, FileAttributes.ReadOnly);
            using (var Second = new DownloadOutput(Target)) {
                File.WriteAllText(Second.StagingPath, "second");
                Second.Publish();
            }
            await Assert.That(File.ReadAllText(Target)).IsEqualTo("second");
            await Assert.That(Directory.GetFiles(Folder)).IsEquivalentTo(new[] { Target });
        } finally {
            if (File.Exists(Target)) File.SetAttributes(Target, FileAttributes.Normal);
            Directory.Delete(Folder, true);
        }
    }

    [Test]
    public async Task 目标文件被占用时发布失败并清理暂存() {
        string Folder = Path.Combine(Path.GetTempPath(), "PclDownload-" + Guid.NewGuid().ToString("N"));
        string Target = Path.Combine(Folder, "client.jar");
        try {
            using (var Output = new DownloadOutput(Target)) {
                File.WriteAllText(Target, "old");
                File.WriteAllText(Output.StagingPath, "new");
                using (File.Open(Target, FileMode.Open, FileAccess.Read, FileShare.Read))
                    await Assert.That(() => Output.Publish()).Throws<IOException>();
            }
            await Assert.That(File.ReadAllText(Target)).IsEqualTo("old");
            await Assert.That(Directory.GetFiles(Folder)).IsEquivalentTo(new[] { Target });
        } finally { Directory.Delete(Folder, true); }
    }
}
