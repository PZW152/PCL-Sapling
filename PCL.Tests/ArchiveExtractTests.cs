using System.IO.Compression;
using System.Text;

namespace PCL.Tests;

public class ArchiveExtractTests
{
    private static string NewFolder()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PclExtract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        return Folder;
    }

    private static string MakeZip(string Folder, params (string Name, string Content)[] Entries)
    {
        string Path_ = Path.Combine(Folder, "pack.zip");
        using (var Zip = ZipFile.Open(Path_, ZipArchiveMode.Create))
            foreach (var (Name, Content) in Entries)
                using (var Writer = new StreamWriter(Zip.CreateEntry(Name, CompressionLevel.Optimal).Open()))
                    Writer.Write(Content);
        return Path_;
    }

    [Test]
    public async Task 正常解压()
    {
        string Folder = NewFolder();
        try
        {
            string Zip = MakeZip(Folder, ("overrides/config/a.toml", "x=1"), ("manifest.json", "{}"));
            string Out = Path.Combine(Folder, "out");
            using (var Archive = ZipFile.OpenRead(Zip))
                await Assert.That(ArchiveBudget.Extract(Archive, Out, -1)).IsEqualTo(5L);
            await Assert.That(File.ReadAllText(Path.Combine(Out, "overrides", "config", "a.toml"))).IsEqualTo("x=1");
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 路径越界时停止()
    {
        string Folder = NewFolder();
        try
        {
            string Zip = MakeZip(Folder, ("../evil.txt", "bad"));
            using var Archive = ZipFile.OpenRead(Zip);
            await Assert.That(() => ArchiveBudget.Extract(Archive, Path.Combine(Folder, "out"), -1)).Throws<InvalidDataException>();
            await Assert.That(File.Exists(Path.Combine(Folder, "evil.txt"))).IsFalse();
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 记录的大小造假时停止()
    {
        string Folder = NewFolder();
        try
        {
            string Zip = MakeZip(Folder, ("big.bin", new string('A', 100_000)));
            // 把本地文件头与中央目录里记录的“解压后大小”改成 10 字节
            byte[] Bytes = File.ReadAllBytes(Zip);
            for (int i = 0; i + 30 < Bytes.Length; i++)
            {
                if (Bytes[i] == 0x50 && Bytes[i + 1] == 0x4B && Bytes[i + 2] == 0x03 && Bytes[i + 3] == 0x04)
                    BitConverter.GetBytes(10).CopyTo(Bytes, i + 22);
                if (Bytes[i] == 0x50 && Bytes[i + 1] == 0x4B && Bytes[i + 2] == 0x01 && Bytes[i + 3] == 0x02)
                    BitConverter.GetBytes(10).CopyTo(Bytes, i + 24);
            }
            File.WriteAllBytes(Zip, Bytes);
            using var Archive = ZipFile.OpenRead(Zip);
            await Assert.That(Archive.Entries[0].Length).IsEqualTo(10L);
            string Out = Path.Combine(Folder, "out");
            await Assert.That(() => ArchiveBudget.Extract(Archive, Out, -1)).Throws<InvalidDataException>();
            // 即使写出了部分内容，也不会超过记录的大小太多（一个缓冲区以内）
            string Written = Path.Combine(Out, "big.bin");
            await Assert.That(!File.Exists(Written) || new FileInfo(Written).Length <= 81920).IsTrue();
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 超出预算或取消时停止()
    {
        string Folder = NewFolder();
        try
        {
            string Zip = MakeZip(Folder, ("a.txt", new string('B', 5000)), ("b.txt", new string('C', 5000)));
            using (var Archive = ZipFile.OpenRead(Zip))
                await Assert.That(() => ArchiveBudget.Extract(Archive, Path.Combine(Folder, "out1"), 6000)).Throws<InvalidDataException>();
            using (var Archive = ZipFile.OpenRead(Zip))
            {
                var Cancelled = new CancellationTokenSource();
                Cancelled.Cancel();
                await Assert.That(() => ArchiveBudget.Extract(Archive, Path.Combine(Folder, "out2"), -1, Cancelled.Token)).Throws<OperationCanceledException>();
            }
            await Assert.That(ArchiveBudget.BudgetFor(-1)).IsEqualTo(ArchiveBudget.MaxTotalBytes);
            await Assert.That(ArchiveBudget.BudgetFor(100)).IsEqualTo(0L);
        }
        finally { Directory.Delete(Folder, true); }
    }
}
