using System.Text;

namespace PCL.Tests;

public class SavedGamesTests
{
    private static void Text(List<byte> Out, string Value)
    {
        byte[] Bytes = Encoding.UTF8.GetBytes(Value);
        Out.Add((byte)(Bytes.Length >> 8)); Out.Add((byte)Bytes.Length);
        Out.AddRange(Bytes);
    }

    private static void StringTag(List<byte> Out, string Key, string Value)
    {
        Out.Add(8); Text(Out, Key); Text(Out, Value);
    }

    private static byte[] ServersFile(params (string Name, string Address)[] Servers)
    {
        var Out = new List<byte> { 10 };
        Text(Out, "");
        Out.Add(9); Text(Out, "servers"); Out.Add(10);
        Out.AddRange(new byte[] { 0, 0, 0, (byte)Servers.Length });
        foreach (var (Name, Address) in Servers)
        {
            // 真实文件里还有其他字段，要能跳过
            Out.Add(1); Text(Out, "acceptTextures"); Out.Add(1);
            StringTag(Out, "icon", "iVBORw0KGgo=");
            StringTag(Out, "name", Name);
            StringTag(Out, "ip", Address);
            Out.Add(0);
        }
        Out.Add(0);
        return Out.ToArray();
    }

    private static string NewGameFolder()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PclSaves-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        return Folder;
    }

    [Test]
    public async Task 读取服务器名称与地址()
    {
        var Servers = SavedGames.ParseServers(ServersFile(("朋友的服务器", "play.example.com:25565"), ("B", "b.net")));
        await Assert.That(Servers.Count).IsEqualTo(2);
        await Assert.That(Servers[0]).IsEqualTo(new SavedServer("朋友的服务器", "play.example.com:25565"));
        await Assert.That(Servers[1]).IsEqualTo(new SavedServer("B", "b.net"));
    }

    [Test]
    public async Task 没有地址的服务器被忽略()
    {
        var Servers = SavedGames.ParseServers(ServersFile(("空的", ""), ("A", "a.net")));
        await Assert.That(Servers.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 损坏的文件不会出错()
    {
        byte[] Whole = ServersFile(("A", "a.net"));
        for (int Length = 0; Length < Whole.Length - 1; Length++)
            await Assert.That(SavedGames.ParseServers(Whole.Take(Length).ToArray())).IsNull();
        await Assert.That(SavedGames.ParseServers(Encoding.ASCII.GetBytes("not nbt at all"))).IsNull();
        await Assert.That(SavedGames.ReadServers(Path.Combine(Path.GetTempPath(), "no-such-folder-" + Guid.NewGuid()))).IsEmpty();
    }

    [Test]
    public async Task 最近的世界按时间排序且忽略不是世界的文件夹()
    {
        string Game = NewGameFolder();
        try
        {
            void World(string Name, DateTime Time)
            {
                string Dir = Path.Combine(Game, "saves", Name);
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, "level.dat"), "x");
                File.SetLastWriteTime(Path.Combine(Dir, "level.dat"), Time);
            }
            World("旧世界", new DateTime(2025, 1, 1));
            World("新世界", new DateTime(2026, 11, 1));
            World("中间", new DateTime(2026, 5, 1));
            Directory.CreateDirectory(Path.Combine(Game, "saves", "不是世界"));
            var Worlds = SavedGames.RecentWorlds(Game, 2);
            await Assert.That(string.Join("|", Worlds.Select(w => w.Folder))).IsEqualTo("新世界|中间");
            await Assert.That(SavedGames.RecentWorlds(Path.Combine(Game, "nothing"), 5)).IsEmpty();
        }
        finally { Directory.Delete(Game, true); }
    }

    [Test]
    public async Task 备份世界会复制全部内容但不含会话锁()
    {
        string Game = NewGameFolder();
        try
        {
            string World = Path.Combine(Game, "saves", "我的世界");
            Directory.CreateDirectory(Path.Combine(World, "region"));
            File.WriteAllText(Path.Combine(World, "level.dat"), "level");
            File.WriteAllText(Path.Combine(World, "region", "r.0.0.mca"), "chunks");
            File.WriteAllText(Path.Combine(World, "session.lock"), "lock");
            var Now = new DateTime(2026, 11, 12, 9, 5, 0);
            string First = SavedGames.BackupWorld(Game, "我的世界", Now);
            string Second = SavedGames.BackupWorld(Game, "我的世界", Now);
            await Assert.That(First).IsEqualTo("我的世界 备份 2026-11-12 09-05");
            await Assert.That(Second).IsEqualTo("我的世界 备份 2026-11-12 09-05 (2)");
            string Copy = Path.Combine(Game, "saves", First);
            await Assert.That(File.ReadAllText(Path.Combine(Copy, "region", "r.0.0.mca"))).IsEqualTo("chunks");
            await Assert.That(File.Exists(Path.Combine(Copy, "session.lock"))).IsFalse();
        }
        finally { Directory.Delete(Game, true); }
    }

    [Test]
    public async Task 区块文件无法读取时不会发布只有元数据的世界()
    {
        string Game = NewGameFolder();
        try
        {
            string Saves = Path.Combine(Game, "saves");
            string World = Path.Combine(Saves, "我的世界");
            string Region = Path.Combine(World, "region");
            Directory.CreateDirectory(Region);
            File.WriteAllText(Path.Combine(World, "level.dat"), "level");
            string Chunk = Path.Combine(Region, "r.0.0.mca");
            File.WriteAllText(Chunk, "chunks");
            using (File.Open(Chunk, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await Assert.That(() => SavedGames.BackupWorld(Game, "我的世界", DateTime.Now)).Throws<IOException>();

            await Assert.That(SavedGames.RecentWorlds(Game, 10).Select(Entry => Entry.Folder).ToArray()).IsEquivalentTo(new[] { "我的世界" });
            await Assert.That(Directory.GetFileSystemEntries(Path.Combine(Saves, ".pcl-world-backups"))).IsEmpty();
            await Assert.That(File.ReadAllText(Chunk)).IsEqualTo("chunks");
        }
        finally { Directory.Delete(Game, true); }
    }

    [Test]
    public async Task 同时备份同一个世界会发布到不同目录()
    {
        string Game = NewGameFolder();
        try
        {
            string World = Path.Combine(Game, "saves", "我的世界");
            Directory.CreateDirectory(World);
            File.WriteAllText(Path.Combine(World, "level.dat"), "level");
            File.WriteAllText(Path.Combine(World, "session.lock"), "lock");
            var Now = new DateTime(2026, 11, 12, 9, 5, 0);
            var Copies = await Task.WhenAll(Task.Run(() => SavedGames.BackupWorld(Game, "我的世界", Now)),
                Task.Run(() => SavedGames.BackupWorld(Game, "我的世界", Now)));

            await Assert.That(Copies).IsEquivalentTo(new[] { "我的世界 备份 2026-11-12 09-05", "我的世界 备份 2026-11-12 09-05 (2)" });
            foreach (string Copy in Copies)
                await Assert.That(File.ReadAllText(Path.Combine(Game, "saves", Copy, "level.dat"))).IsEqualTo("level");
        }
        finally { Directory.Delete(Game, true); }
    }

    [Test]
    public async Task 会话锁仍被游戏占用时不创建备份()
    {
        string Game = NewGameFolder();
        try
        {
            string Saves = Path.Combine(Game, "saves");
            string World = Path.Combine(Saves, "我的世界");
            Directory.CreateDirectory(World);
            File.WriteAllText(Path.Combine(World, "level.dat"), "level");
            using (File.Open(Path.Combine(World, "session.lock"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
                await Assert.That(() => SavedGames.BackupWorld(Game, "我的世界", DateTime.Now)).Throws<IOException>();

            await Assert.That(Directory.GetDirectories(Saves)).IsEquivalentTo(new[] { World });
        }
        finally { Directory.Delete(Game, true); }
    }

    [Test]
    public async Task 不能备份不存在或越界的世界()
    {
        string Game = NewGameFolder();
        try
        {
            Directory.CreateDirectory(Path.Combine(Game, "saves"));
            await Assert.That(() => SavedGames.BackupWorld(Game, "不存在", DateTime.Now)).Throws<DirectoryNotFoundException>();
            await Assert.That(() => SavedGames.BackupWorld(Game, "..", DateTime.Now)).Throws<DirectoryNotFoundException>();
            await Assert.That(() => SavedGames.BackupWorld(Game, "a\\..\\..", DateTime.Now)).Throws<DirectoryNotFoundException>();
        }
        finally { Directory.Delete(Game, true); }
    }
}
