using System.Globalization;

namespace PCLCS;

/// <summary>游戏里保存的一个多人服务器。</summary>
public record SavedServer(string Name, string Address);

/// <summary>一个单人世界。</summary>
/// <param name="Folder">世界在 saves 中的文件夹名，直接进入世界时要用它。</param>
/// <param name="LastPlayed">level.dat 最后保存的时间。</param>
public record SavedWorld(string Folder, DateTime LastPlayed);

/// <summary>
/// 读取游戏目录中的单人世界与已保存的服务器，以及备份世界。
/// </summary>
public static class SavedGames {

    private const byte TagEnd = 0, TagString = 8, TagList = 9, TagCompound = 10;
    /// <summary>文件被改坏时，嵌套过深也不能让程序出错。</summary>
    private const int MaxDepth = 16;

    #region 服务器

    /// <summary>
    /// 读取游戏目录下 servers.dat 中保存的服务器。没有文件或无法识别时返回空列表。
    /// </summary>
    public static List<SavedServer> ReadServers(string gameDirectory) {
        string path = Path.Combine(gameDirectory, "servers.dat");
        if (!File.Exists(path)) return [];
        try {
            return ParseServers(File.ReadAllBytes(path)) ?? [];
        } catch (IOException) {
            return [];
        } catch (UnauthorizedAccessException) {
            return [];
        }
    }

    /// <summary>
    /// 解析 servers.dat（未压缩的 NBT）。只读取 name 与 ip 字段，其余跳过。文件不完整时返回 null。
    /// </summary>
    public static List<SavedServer>? ParseServers(byte[] bytes) {
        NbtReader reader = new(bytes);
        if (reader.Byte() != TagCompound) return null;
        if (reader.Text() is null) return null;
        while (true) {
            int? tag = reader.Byte();
            if (tag is null) return null;
            if (tag == TagEnd) return [];
            string? key = reader.Text();
            if (key is null) return null;
            if (tag != TagList || key != "servers") {
                if (!reader.Skip((byte) tag, 1)) return null;
                continue;
            }
            int? inner = reader.Byte();
            if (inner is null) return null;
            if (inner != TagCompound) return [];
            int? count = reader.Int();
            if (count is null) return null;
            List<SavedServer> servers = [];
            for (int i = 0; i < count; i++) {
                SavedServer? server = reader.Server();
                if (server is null) return null;
                if (server.Address != "") servers.Add(server);
            }
            return servers;
        }
    }

    private sealed class NbtReader(byte[] bytes) {
        private int position;

        private bool Take(int count, out int start) {
            start = position;
            if (count < 0 || bytes.Length - position < count) return false;
            position += count;
            return true;
        }
        public int? Byte() => Take(1, out int start) ? bytes[start] : null;
        private int? Short() => Take(2, out int start) ? (bytes[start] << 8) | bytes[start + 1] : null;
        public int? Int() {
            if (!Take(4, out int start)) return null;
            int value = (bytes[start] << 24) | (bytes[start + 1] << 16) | (bytes[start + 2] << 8) | bytes[start + 3];
            return value < 0 ? null : value;
        }
        public string? Text() {
            int? length = Short();
            if (length is null || !Take(length.Value, out int start)) return null;
            return Encoding.UTF8.GetString(bytes, start, length.Value);
        }

        /// <summary>跳过一个值。返回 false 表示文件不完整。</summary>
        public bool Skip(byte tag, int depth) {
            if (depth > MaxDepth) return false;
            switch (tag) {
                case 1: return Take(1, out _);
                case 2: return Take(2, out _);
                case 3: case 5: return Take(4, out _);
                case 4: case 6: return Take(8, out _);
                case 7: { int? count = Int(); return count is not null && Take(count.Value, out _); }
                case TagString: return Text() is not null;
                case TagList: {
                    int? inner = Byte();
                    int? count = Int();
                    if (inner is null || count is null) return false;
                    for (int i = 0; i < count; i++) if (!Skip((byte) inner, depth + 1)) return false;
                    return true;
                }
                case TagCompound:
                    while (true) {
                        int? inner = Byte();
                        if (inner is null) return false;
                        if (inner == TagEnd) return true;
                        if (Text() is null || !Skip((byte) inner, depth + 1)) return false;
                    }
                case 11: { int? count = Int(); return count is not null && count <= int.MaxValue / 4 && Take(count.Value * 4, out _); }
                case 12: { int? count = Int(); return count is not null && count <= int.MaxValue / 8 && Take(count.Value * 8, out _); }
                default: return false;
            }
        }

        public SavedServer? Server() {
            string name = "", address = "";
            while (true) {
                int? tag = Byte();
                if (tag is null) return null;
                if (tag == TagEnd) return new SavedServer(name, address);
                string? key = Text();
                if (key is null) return null;
                if (tag == TagString && key is "name" or "ip") {
                    string? value = Text();
                    if (value is null) return null;
                    if (key == "name") name = value; else address = value;
                } else if (!Skip((byte) tag, 1)) {
                    return null;
                }
            }
        }
    }

    #endregion

    #region 世界

    /// <summary>
    /// 最近游玩的单人世界，新的在前。没有 saves 文件夹时返回空列表。
    /// 没有 level.dat 的文件夹不是世界（例如玩家自己放的东西），会被忽略。
    /// </summary>
    public static List<SavedWorld> RecentWorlds(string gameDirectory, int limit) {
        string saves = Path.Combine(gameDirectory, "saves");
        if (!Directory.Exists(saves)) return [];
        List<SavedWorld> worlds = [];
        foreach (string folder in Directory.EnumerateDirectories(saves)) {
            string level = Path.Combine(folder, "level.dat");
            if (!File.Exists(level)) continue;
            worlds.Add(new SavedWorld(Path.GetFileName(folder), File.GetLastWriteTime(level)));
        }
        return [.. worlds.OrderByDescending(w => w.LastPlayed).Take(limit)];
    }

    /// <summary>
    /// 把世界复制为 saves 中的另一个世界，返回新的文件夹名。要还原时在游戏里直接打开备份即可。
    /// 游戏运行时的 session.lock 不会被复制。
    /// </summary>
    public static string BackupWorld(string gameDirectory, string folder, DateTime now) {
        string saves = Path.Combine(gameDirectory, "saves");
        if (string.IsNullOrWhiteSpace(folder) || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || folder.Contains('\\') || folder is "." or "..")
            throw new DirectoryNotFoundException("找不到这个世界：" + folder);
        string source = Path.Combine(saves, folder);
        if (!File.Exists(PathUtils.ForApi(Path.Combine(source, "level.dat"))))
            throw new DirectoryNotFoundException("找不到这个世界：" + folder);
        if (IsReparsePoint(source))
            throw new IOException("无法备份通过符号链接或目录联接指向其他位置的世界：" + folder);

        // 暂存目录与 saves 位于同一个卷，未完成的世界也不会被游戏或启动器列出。
        string stagingRoot = Path.Combine(saves, ".pcl-world-backups");
        if (Directory.Exists(PathUtils.ForApi(stagingRoot))) {
            bool isWorld = File.Exists(PathUtils.ForApi(Path.Combine(stagingRoot, "level.dat")))
                || File.Exists(PathUtils.ForApi(Path.Combine(stagingRoot, "level.dat_old")));
            if (isWorld || IsReparsePoint(stagingRoot))
                throw new IOException("世界备份的暂存目录已被占用：" + stagingRoot);
        }
        string staging = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        string baseName = folder + " 备份 " + now.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture);
        string sessionLock = PathUtils.ForApi(Path.Combine(source, "session.lock"));
        using FileStream? worldLock = OpenWorldLock(sessionLock);
        try {
            CopyDirectory(source, staging);
            for (int i = 1; ; i++) {
                string name = i == 1 ? baseName : $"{baseName} ({i})";
                string target = PathUtils.ForApi(Path.Combine(saves, name));
                if (Directory.Exists(target) || File.Exists(target)) continue;
                try {
                    Directory.Move(PathUtils.ForApi(staging), target);
                    return name;
                } catch (IOException) when (Directory.Exists(target) || File.Exists(target)) {
                    // 另一次备份先用了同一个名称，换一个名称再发布。
                }
            }
        } finally {
            // 清理这次任务自己的文件，保留原来的复制错误。
            try {
                if (Directory.Exists(PathUtils.ForApi(staging))) Directory.Delete(PathUtils.ForApi(staging), recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(PathUtils.ForApi(path)) & FileAttributes.ReparsePoint) != 0;

    private static FileStream? OpenWorldLock(string path) {
        // 多个备份可以同时读取会话锁。Minecraft 打开会话锁写入时，两者互斥。
        try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (FileNotFoundException) { return null; } // 旧版本或尚未被游戏打开过的存档可能没有会话锁。
        catch (IOException ex) { throw new IOException("无法取得世界的会话锁，请先关闭正在使用这个世界的 Minecraft。", ex); }
    }

    private static void CopyDirectory(string source, string target) {
        Directory.CreateDirectory(PathUtils.ForApi(target));
        foreach (string file in Directory.EnumerateFiles(PathUtils.ForApi(source))) {
            if (Path.GetFileName(file).Equals("session.lock", StringComparison.OrdinalIgnoreCase)) continue;
            if (IsReparsePoint(file)) throw new IOException("无法备份存档内的链接文件：" + file);
            // 允许读取被其他程序以共享方式打开的文件。
            using FileStream input = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using FileStream output = new(PathUtils.ForApi(Path.Combine(target, Path.GetFileName(file))), FileMode.CreateNew, FileAccess.Write);
            input.CopyTo(output);
        }
        foreach (string directory in Directory.EnumerateDirectories(PathUtils.ForApi(source))) {
            if (IsReparsePoint(directory)) throw new IOException("无法备份存档内的链接目录：" + directory);
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    #endregion

}
