using Newtonsoft.Json.Linq;

namespace PCLCS;

/// <summary>
/// 要清理的文件与能清出的空间。
/// </summary>
public record CleanupPlan(IReadOnlyList<string> Files, long Bytes);

/// <summary>
/// 清理没有任何版本在用的共用游戏文件（取自 Sapling）：assets 中的资源与索引、libraries 中的支持库。
/// 会删资料，所以宁可少删：只清理这三类文件，不碰版本文件夹、存档与 Mod；调用方无法确定支持库时不清理支持库。
/// </summary>
public static class GameFileCleanup {

    /// <summary>
    /// 各版本仍在使用的文件。
    /// </summary>
    public sealed class Keep {
        /// <summary>仍在使用的支持库，相对于 libraries 文件夹的路径。为 null 时表示无法确定，不清理支持库。</summary>
        public HashSet<string>? Libraries = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>仍在使用的资源索引名称，如 17。</summary>
        public HashSet<string> AssetIndexes = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>仍在使用的资源文件哈希。</summary>
        public HashSet<string> AssetHashes = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 有版本的资源依赖无法完整读出（索引缺失、格式异常、只能退回 legacy）。为 true 时不清理任何资源文件（#3）。
        /// </summary>
        public bool AssetsUnknown;

        public void AddLibrary(string RelativePath) => Libraries?.Add(NormalizeLibrary(RelativePath));

        /// <summary>
        /// 记录一个资源索引及其中所有资源文件。
        /// </summary>
        public void AddAssetIndex(string Id, JObject IndexJson) {
            AssetIndexes.Add(Id);
            if (IndexJson["objects"] is not JObject Objects) {
                AssetsUnknown = true; // 读得懂 JSON 但没有资源列表：分不清“本来为空”和“损坏”，按不确定处理
                return;
            }
            foreach (var Property in Objects.Properties()) {
                string? Hash = Property.Value["hash"]?.ToString();
                if (!string.IsNullOrEmpty(Hash))
                    AssetHashes.Add(Hash!);
            }
        }
    }

    /// <summary>同一个路径可能用 / 或 \ 写，比较前统一为 \。</summary>
    public static string NormalizeLibrary(string RelativePath) =>
        RelativePath.Replace('/', '\\').Trim('\\');

    /// <summary>
    /// 找出可以清理的文件，不删除任何东西。
    /// </summary>
    /// <param name="McFolder">游戏文件夹（.minecraft），以 \ 结尾或不结尾均可。</param>
    public static CleanupPlan Plan(string McFolder, Keep Keep) {
        var Files = new List<string>();
        string Assets = Path.Combine(McFolder, "assets");
        // 资源索引
        string Indexes = Path.Combine(Assets, "indexes");
        if (!Keep.AssetsUnknown && Directory.Exists(Indexes)) {
            foreach (var File in Directory.GetFiles(Indexes, "*.json"))
                if (!Keep.AssetIndexes.Contains(Path.GetFileNameWithoutExtension(File)))
                    Files.Add(File);
        }
        // 资源文件：objects\前两位\哈希
        string Objects = Path.Combine(Assets, "objects");
        if (!Keep.AssetsUnknown && Directory.Exists(Objects)) {
            foreach (var Folder in Directory.GetDirectories(Objects))
                foreach (var File in Directory.GetFiles(Folder))
                    if (!Keep.AssetHashes.Contains(Path.GetFileName(File)))
                        Files.Add(File);
        }
        // 支持库
        string Libraries = Path.Combine(McFolder, "libraries");
        if (Keep.Libraries is not null && Directory.Exists(Libraries)) {
            string Root = Path.GetFullPath(Libraries).TrimEnd('\\') + "\\";
            foreach (var File in Directory.GetFiles(Libraries, "*", SearchOption.AllDirectories)) {
                string Relative = Path.GetFullPath(File).Substring(Root.Length);
                if (!Keep.Libraries.Contains(NormalizeLibrary(Relative)))
                    Files.Add(File);
            }
        }
        long Bytes = 0;
        foreach (var File in Files) {
            try { Bytes += new FileInfo(File).Length; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return new CleanupPlan(Files, Bytes);
    }

    /// <summary>
    /// 删除计划中的文件，返回实际清出的空间。删不掉的文件（例如正在运行的游戏占用的）直接跳过。
    /// 之后删除 libraries 与 assets\objects 中变空的文件夹。
    /// </summary>
    public static long Run(string McFolder, CleanupPlan Plan) {
        long Freed = 0;
        foreach (var File in Plan.Files) {
            try {
                long Size = new FileInfo(File).Length;
                System.IO.File.Delete(File);
                Freed += Size;
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }
        RemoveEmptyFolders(Path.Combine(McFolder, "libraries"), false);
        RemoveEmptyFolders(Path.Combine(McFolder, "assets", "objects"), false);
        return Freed;
    }

    private static void RemoveEmptyFolders(string Folder, bool RemoveSelf) {
        if (!Directory.Exists(Folder))
            return;
        foreach (var Sub in Directory.GetDirectories(Folder))
            RemoveEmptyFolders(Sub, true);
        try {
            if (RemoveSelf && !Directory.EnumerateFileSystemEntries(Folder).Any())
                Directory.Delete(Folder);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
