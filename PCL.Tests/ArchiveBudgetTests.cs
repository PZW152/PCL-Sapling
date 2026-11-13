namespace PCL.Tests;

public class ArchiveBudgetTests
{
    private const long MB = 1024 * 1024;

    [Test]
    public async Task 正常的整合包通过()
    {
        var Entries = Enumerable.Range(0, 5000).Select(i => ($"overrides/config/{i}.toml", 20_000L, 6_000L)).Append(("overrides/mods/big.jar", 80 * MB, 75 * MB));
        await Assert.That(ArchiveBudget.Check(Entries, 50_000 * MB)).IsNull();
        await Assert.That(ArchiveBudget.Check(Entries, -1)).IsNull(); // 不知道剩余空间时只检查其他项
    }

    [Test]
    public async Task 压缩比异常的大文件被拦下()
    {
        var Entries = new[] { ("bomb.bin", 4096 * MB, 2 * MB) };
        await Assert.That(ArchiveBudget.Check(Entries, -1)).Contains("压缩比异常");
        // 压缩比高但文件不大（例如大量空白的文本）不算异常
        await Assert.That(ArchiveBudget.Check(new[] { ("blank.txt", 50 * MB, 10_000L) }, -1)).IsNull();
    }

    [Test]
    public async Task 空间不足或文件太多时停止()
    {
        await Assert.That(ArchiveBudget.Check(new[] { ("a.jar", 900 * MB, 800 * MB) }, 1000 * MB)).Contains("磁盘空间不足");
        var Many = Enumerable.Range(0, ArchiveBudget.MaxEntries + 1).Select(i => ($"f{i}", 1L, 1L));
        await Assert.That(ArchiveBudget.Check(Many, -1)).Contains("超过");
        await Assert.That(ArchiveBudget.Check(new[] { ("bad", -1L, 1L) }, -1)).Contains("无效");
    }
}
