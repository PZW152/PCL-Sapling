using System.IO.Compression;

namespace PCLCS;

/// <summary>
/// 解压第三方整合包之前的预检查（#8）：用压缩包自己记录的大小判断，防止很小的压缩包展开后耗尽磁盘。
/// 路径越界（Zip Slip）由 MeloongCore 的解压负责检查，这里不重复。
/// 限制：压缩包记录的大小可以造假，解压过程中按实际写入量中止需要在 MeloongCore 中实现。
/// </summary>
public static class ArchiveBudget {
    /// <summary>条目数上限。正常的大型整合包也只有几万个文件。</summary>
    public const int MaxEntries = 200_000;
    /// <summary>展开后的总大小上限。</summary>
    public const long MaxTotalBytes = 32L * 1024 * 1024 * 1024;
    /// <summary>解压后至少还要剩下的磁盘空间。</summary>
    public const long KeepFreeBytes = 512L * 1024 * 1024;
    /// <summary>单个文件超过这个大小且压缩比极高时，视为异常压缩包。</summary>
    public const long SuspiciousEntryBytes = 256L * 1024 * 1024;
    public const double SuspiciousRatio = 1000;

    /// <summary>
    /// 检查压缩包；没有问题时返回 null，否则返回给玩家看的原因。
    /// </summary>
    /// <param name="freeBytes">解压目标所在磁盘的可用空间；未知时传入 -1。</param>
    public static string? Check(string zipPath, long freeBytes) {
        using var stream = File.OpenRead(zipPath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        return Check(zip.Entries.Select(e => (e.FullName, e.Length, e.CompressedLength)), freeBytes);
    }

    public static string? Check(IEnumerable<(string Name, long Length, long CompressedLength)> entries, long freeBytes) {
        int count = 0;
        long total = 0;
        foreach (var (name, length, compressed) in entries) {
            count++;
            if (count > MaxEntries)
                return $"整合包中的文件超过 {MaxEntries} 个，看起来不是正常的整合包，已停止安装。";
            if (length < 0)
                return $"整合包中的文件 {name} 记录的大小无效，压缩包可能已损坏。";
            if (length > SuspiciousEntryBytes && length / (double)Math.Max(1, compressed) > SuspiciousRatio)
                return $"整合包中的文件 {name} 压缩比异常（展开后约 {length / 1024 / 1024} MB），看起来不是正常的整合包，已停止安装。";
            total += length;
        }
        if (total > MaxTotalBytes)
            return $"整合包展开后约 {total / 1024 / 1024 / 1024} GB，超出了上限，已停止安装。";
        if (freeBytes >= 0 && total + KeepFreeBytes > freeBytes)
            return $"磁盘空间不足：整合包展开后约需要 {Math.Ceiling(total / 1024d / 1024)} MB，目标磁盘只剩 {freeBytes / 1024 / 1024} MB。";
        return null;
    }
}
