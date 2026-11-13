using System.IO.Compression;

namespace PCLCS;

/// <summary>
/// 解压第三方整合包时的限额（#8）：
/// 解压前用压缩包自己记录的大小预检查；解压时用 <see cref="Extract"/> 按实际写入的字节数检查，
/// 记录的大小造假（实际内容比记录的多）或总量超出预算时立即中止。也检查路径越界（Zip Slip）。
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

    /// <summary>
    /// 解压到文件夹，边写边统计实际字节数（#8）。
    /// 每个文件写入的量超过它记录的大小、或总量超过 <paramref name="budgetBytes"/> 时抛出 <see cref="InvalidDataException"/>；
    /// 每写一块检查一次取消。中止后已写出的文件由调用方连同临时文件夹一起删除。
    /// </summary>
    /// <param name="budgetBytes">总共最多写入多少字节；小于 0 时使用 <see cref="MaxTotalBytes"/>。</param>
    /// <param name="progress">进度，0 到 1。</param>
    public static long Extract(ZipArchive zip, string outputDirectory, long budgetBytes, CancellationToken cancel = default, Action<double>? progress = null) {
        if (budgetBytes < 0) budgetBytes = MaxTotalBytes;
        string root = Path.GetFullPath(outputDirectory).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);
        var entries = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList(); // 文件夹条目的 Name 为空
        if (entries.Count > MaxEntries)
            throw new InvalidDataException($"整合包中的文件超过 {MaxEntries} 个，看起来不是正常的整合包，已停止安装。");
        long total = 0;
        var buffer = new byte[81920];
        for (int i = 0; i < entries.Count; i++) {
            cancel.ThrowIfCancellationRequested();
            var entry = entries[i];
            string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"整合包中的文件 {entry.FullName} 的路径在压缩包之外，这可能导致安全问题，已停止安装。");
            Directory.CreateDirectory(MeloongCore.PathUtils.ForApi(Path.GetDirectoryName(target)!)); // 支持超过 260 字符的路径
            long written = 0;
            using (var input = entry.Open())
            using (var output = new FileStream(MeloongCore.PathUtils.ForApi(target), FileMode.Create, FileAccess.Write, FileShare.None)) {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
                    written += read;
                    total += read;
                    if (written > entry.Length)
                        throw new InvalidDataException($"整合包中的文件 {entry.FullName} 的实际内容比记录的大小更大，压缩包可能是恶意构造的，已停止安装。");
                    if (total > budgetBytes)
                        throw new InvalidDataException($"整合包展开后超出了可用的空间或上限（{budgetBytes / 1024 / 1024} MB），已停止安装。");
                    output.Write(buffer, 0, read);
                    cancel.ThrowIfCancellationRequested();
                }
            }
            progress?.Invoke((i + 1) / (double)entries.Count);
        }
        return total;
    }

    /// <summary>
    /// 按剩余空间算出解压预算：不超过 <see cref="MaxTotalBytes"/>，并给磁盘留下 <see cref="KeepFreeBytes"/>。
    /// </summary>
    public static long BudgetFor(long freeBytes) =>
        freeBytes < 0 ? MaxTotalBytes : Math.Max(0, Math.Min(MaxTotalBytes, freeBytes - KeepFreeBytes));
}
