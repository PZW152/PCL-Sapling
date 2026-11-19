namespace PCL.Tests;

public class InstanceUpdateTransactionTests {
    private static string NewRoot() {
        string Root = Path.Combine(Path.GetTempPath(), "PclInstanceTx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        return Root;
    }

    [Test]
    public async Task 中途失败时还原覆盖删除与新增的文件() {
        string Root = NewRoot();
        try {
            string Jar = Path.Combine(Root, "Instance.jar");
            string Json = Path.Combine(Root, "Instance.json");
            string NewMod = Path.Combine(Root, "mods", "new.jar");
            File.WriteAllText(Jar, "old jar");
            File.WriteAllText(Json, "old json");
            var Tx = new InstanceUpdateTransaction(Path.Combine(Root, "PCL", "UpdateBackup"));
            Tx.Protect(Jar);
            File.WriteAllText(Jar, "new jar");
            Tx.Protect(Json);
            File.Delete(Json);
            Tx.Protect(NewMod);
            Directory.CreateDirectory(Path.GetDirectoryName(NewMod)!);
            File.WriteAllText(NewMod, "new mod");
            Tx.Protect(Jar); // 第二次保护同一文件不能覆盖最初的备份
            File.WriteAllText(Jar, "newer jar");
            Tx.Rollback();

            await Assert.That(File.ReadAllText(Jar)).IsEqualTo("old jar");
            await Assert.That(File.ReadAllText(Json)).IsEqualTo("old json");
            await Assert.That(File.Exists(NewMod)).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(Root, "PCL", "UpdateBackup"))).IsFalse();
        } finally { Directory.Delete(Root, true); }
    }

    [Test]
    public async Task 提交后保留新文件并删除备份() {
        string Root = NewRoot();
        try {
            string Jar = Path.Combine(Root, "Instance.jar");
            File.WriteAllText(Jar, "old jar");
            using (var Tx = new InstanceUpdateTransaction(Path.Combine(Root, "backup"))) {
                Tx.Protect(Jar);
                File.WriteAllText(Jar, "new jar");
                Tx.Commit();
            }
            await Assert.That(File.ReadAllText(Jar)).IsEqualTo("new jar");
            await Assert.That(Directory.Exists(Path.Combine(Root, "backup"))).IsFalse();
        } finally { Directory.Delete(Root, true); }
    }

    [Test]
    public async Task 未提交就释放视为失败() {
        string Root = NewRoot();
        try {
            string Jar = Path.Combine(Root, "Instance.jar");
            File.WriteAllText(Jar, "old jar");
            using (var Tx = new InstanceUpdateTransaction(Path.Combine(Root, "backup"))) {
                Tx.Protect(Jar);
                File.WriteAllText(Jar, "half written");
            }
            await Assert.That(File.ReadAllText(Jar)).IsEqualTo("old jar");
        } finally { Directory.Delete(Root, true); }
    }
}
