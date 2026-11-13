namespace PCL.Tests;

public class FileLinkerTests
{
    private static string NewFolder()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PclLink-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        return Folder;
    }

    [Test]
    public async Task 同一磁盘内创建硬链接_内容相同且不额外占用一份()
    {
        string Folder = NewFolder();
        try
        {
            string Source = Path.Combine(Folder, "a", "mod.jar");
            string Target = Path.Combine(Folder, "b", "sub", "mod.jar");
            Directory.CreateDirectory(Path.GetDirectoryName(Source));
            File.WriteAllText(Source, "content-1");
            bool Linked = FileLinker.LinkOrCopy(Source, Target);
            await Assert.That(File.ReadAllText(Target)).IsEqualTo("content-1");
            if (Linked)
            {
                // 两个路径指向同一份数据：改一处，另一处同步变化
                File.AppendAllText(Source, "+more");
                await Assert.That(File.ReadAllText(Target)).IsEqualTo("content-1+more");
            }
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 删除其中一个不影响另一个()
    {
        string Folder = NewFolder();
        try
        {
            string Source = Path.Combine(Folder, "lib.jar");
            string Target = Path.Combine(Folder, "copy", "lib.jar");
            File.WriteAllText(Source, "library");
            FileLinker.LinkOrCopy(Source, Target);
            File.Delete(Source);
            await Assert.That(File.ReadAllText(Target)).IsEqualTo("library");
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 目标已存在时被替换()
    {
        string Folder = NewFolder();
        try
        {
            string Source = Path.Combine(Folder, "new.jar");
            string Target = Path.Combine(Folder, "old.jar");
            File.WriteAllText(Source, "new");
            File.WriteAllText(Target, "old");
            File.SetAttributes(Target, FileAttributes.ReadOnly);
            FileLinker.LinkOrCopy(Source, Target);
            await Assert.That(File.ReadAllText(Target)).IsEqualTo("new");
        }
        finally
        {
            foreach (var f in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(Folder, true);
        }
    }

    [Test]
    public async Task 来源与目标相同时不做任何事()
    {
        string Folder = NewFolder();
        try
        {
            string Source = Path.Combine(Folder, "same.jar");
            File.WriteAllText(Source, "same");
            await Assert.That(FileLinker.LinkOrCopy(Source, Source)).IsFalse();
            await Assert.That(File.ReadAllText(Source)).IsEqualTo("same");
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 来源不存在时原来的目标保持不变()
    {
        string Folder = NewFolder();
        try
        {
            string Target = Path.Combine(Folder, "keep.jar");
            File.WriteAllText(Target, "player-edited");
            await Assert.That(() => FileLinker.LinkOrCopy(Path.Combine(Folder, "missing.jar"), Target)).Throws<FileNotFoundException>();
            await Assert.That(File.ReadAllText(Target)).IsEqualTo("player-edited");
            await Assert.That(Directory.GetFiles(Folder).Length).IsEqualTo(1); // 没有留下临时文件
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 替换只读的旧目标()
    {
        string Folder = NewFolder();
        try
        {
            string Source = Path.Combine(Folder, "new.jar");
            string Target = Path.Combine(Folder, "old.jar");
            File.WriteAllText(Source, "new");
            File.WriteAllText(Target, "old");
            File.SetAttributes(Target, FileAttributes.ReadOnly);
            FileLinker.LinkOrCopy(Source, Target);
            await Assert.That(File.ReadAllText(Target)).IsEqualTo("new");
            await Assert.That(Directory.GetFiles(Folder).Length).IsEqualTo(2);
        }
        finally
        {
            foreach (var File in Directory.GetFiles(Folder))
                System.IO.File.SetAttributes(File, FileAttributes.Normal);
            Directory.Delete(Folder, true);
        }
    }
}
