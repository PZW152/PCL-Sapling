using System.IO.Compression;

namespace PCL.Tests;

public class InstanceImportTests
{
    private static string NewInstance(string Name = "Fabulously Optimized")
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PclImport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Folder, "minecraft", "mods"));
        Directory.CreateDirectory(Path.Combine(Folder, "minecraft", "logs"));
        Directory.CreateDirectory(Path.Combine(Folder, "minecraft", "saves", "logs"));
        File.WriteAllText(Path.Combine(Folder, "mmc-pack.json"), "{\"components\":[],\"formatVersion\":1}");
        File.WriteAllText(Path.Combine(Folder, "instance.cfg"), "[General]\r\nInstanceType=OneSix\r\nname=" + Name + "\r\n");
        File.WriteAllText(Path.Combine(Folder, "minecraft", "mods", "sodium.jar"), "jar");
        File.WriteAllText(Path.Combine(Folder, "minecraft", "options.txt"), "lang:zh_cn");
        File.WriteAllText(Path.Combine(Folder, "minecraft", "logs", "latest.log"), "log");
        File.WriteAllText(Path.Combine(Folder, "minecraft", "saves", "logs", "keep.txt"), "a world folder named logs");
        File.WriteAllText(Path.Combine(Folder, "instance.cfg.lock"), "");
        return Folder;
    }

    [Test]
    public async Task 识别Prism和MultiMC的实例()
    {
        string Folder = NewInstance();
        try
        {
            await Assert.That(InstanceImport.IsMmcInstance(Folder)).IsTrue();
            await Assert.That(InstanceImport.IsMmcInstance(Path.Combine(Folder, "minecraft"))).IsFalse();
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 读取实例名称()
    {
        string Folder = NewInstance("我的整合包");
        try
        {
            await Assert.That(InstanceImport.GetDisplayName(Folder)).IsEqualTo("我的整合包");
            File.WriteAllText(Path.Combine(Folder, "instance.cfg"), "[General]\r\n");
            await Assert.That(InstanceImport.GetDisplayName(Folder)).IsEqualTo(Path.GetFileName(Folder));
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 打包时文件位于根目录且跳过日志与锁文件()
    {
        string Folder = NewInstance();
        string Zip = Folder + ".zip";
        try
        {
            InstanceImport.PackFolder(Folder, Zip);
            using var Archive = ZipFile.OpenRead(Zip);
            var Names = Archive.Entries.Select(e => e.FullName).ToList();
            await Assert.That(Names.Contains("mmc-pack.json")).IsTrue();
            await Assert.That(Names.Contains("instance.cfg")).IsTrue();
            await Assert.That(Names.Contains("minecraft/mods/sodium.jar")).IsTrue();
            await Assert.That(Names.Contains("minecraft/options.txt")).IsTrue();
            await Assert.That(Names.Contains("minecraft/saves/logs/keep.txt")).IsTrue(); // 只跳过游戏文件夹下一级的 logs
            await Assert.That(Names.Any(n => n.StartsWith("minecraft/logs/"))).IsFalse();
            await Assert.That(Names.Any(n => n.EndsWith(".lock"))).IsFalse();
            // 原文件夹不变
            await Assert.That(File.Exists(Path.Combine(Folder, "minecraft", "logs", "latest.log"))).IsTrue();
        }
        finally
        {
            Directory.Delete(Folder, true);
            if (File.Exists(Zip)) File.Delete(Zip);
        }
    }
}
