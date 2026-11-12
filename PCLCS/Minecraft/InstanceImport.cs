using System.IO.Compression;

namespace PCLCS;

/// <summary>
/// 从其他启动器导入实例。
/// Prism Launcher / MultiMC 的实例文件夹与它们导出的整合包格式相同（mmc-pack.json + instance.cfg + 游戏文件夹），
/// 所以把文件夹打包后，交给现有的 MMC 整合包安装流程即可。
/// </summary>
public static class InstanceImport {

    /// <summary>
    /// 该文件夹是否是 Prism Launcher / MultiMC 的实例。
    /// </summary>
    public static bool IsMmcInstance(string folder) =>
        File.Exists(Path.Combine(folder, "mmc-pack.json")) && File.Exists(Path.Combine(folder, "instance.cfg"));

    /// <summary>
    /// 读取实例在原启动器中显示的名称；读取失败时返回文件夹名。
    /// </summary>
    public static string GetDisplayName(string folder) {
        try {
            foreach (string line in File.ReadLines(Path.Combine(folder, "instance.cfg"))) {
                if (!line.StartsWith("name=", StringComparison.Ordinal)) continue;
                string name = line.Substring(5).Trim();
                if (name != "") return name;
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
        return Path.GetFileName(folder.TrimEnd('\\', '/'));
    }

    /// <summary>
    /// 这些内容不需要随实例一起导入：日志、崩溃报告和临时文件。
    /// </summary>
    private static readonly string[] SkippedFolders = ["logs", "crash-reports", ".cache"];

    /// <summary>
    /// 把实例文件夹打包为 zip（文件位于压缩包根目录），用于交给整合包安装流程。原文件夹不会被修改。
    /// </summary>
    public static void PackFolder(string instanceFolder, string zipPath) {
        string root = Path.GetFullPath(instanceFolder).TrimEnd('\\') + "\\";
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create, Encoding.UTF8);
        AddFolder(zip, root, root);
    }

    private static void AddFolder(ZipArchive zip, string root, string folder) {
        foreach (string file in Directory.EnumerateFiles(folder)) {
            if (file.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)) continue; // 游戏或启动器运行时的锁文件
            string entryName = file.Substring(root.Length).Replace('\\', '/');
            ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
            entry.LastWriteTime = File.GetLastWriteTime(file);
            using FileStream input = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using Stream output = entry.Open();
            input.CopyTo(output);
        }
        foreach (string directory in Directory.EnumerateDirectories(folder)) {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue; // 不跟随符号链接
            string relative = directory.Substring(root.Length);
            // 只跳过游戏文件夹下一级的日志等内容
            string[] parts = relative.Split('\\');
            if (parts.Length == 2 && parts[0] is "minecraft" or ".minecraft" && SkippedFolders.Contains(parts[1], StringComparer.OrdinalIgnoreCase)) continue;
            AddFolder(zip, root, directory);
        }
    }

}
