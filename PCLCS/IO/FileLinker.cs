namespace PCLCS;

/// <summary>
/// 让多个位置共用同一份文件内容（硬链接），避免相同的支持库、资源、模组在磁盘上占用多份空间。
/// 硬链接只在同一个 NTFS 磁盘内可用；不可用时退回为普通复制。
/// </summary>
public static class FileLinker {

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    /// <summary>
    /// 让 <paramref name="targetPath"/> 成为 <paramref name="sourcePath"/> 的硬链接；做不到时复制文件。
    /// 目标已存在时会被替换。
    /// </summary>
    /// <returns>是否成功创建了硬链接；返回 <see langword="false"/> 表示改为了复制。</returns>
    public static bool LinkOrCopy(string sourcePath, string targetPath) {
        string source = Path.GetFullPath(sourcePath);
        string target = Path.GetFullPath(targetPath);
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(source)) throw new FileNotFoundException("要共用的文件不存在", source);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        // 先在目标旁边做好临时文件，成功后再换上去：中途失败时原来的目标保持不变（#2）
        string temp = target + ".pcl-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
        bool linked;
        try {
            linked = SameVolume(source, target) && CreateHardLink(ToExtendedPath(temp), ToExtendedPath(source), IntPtr.Zero);
            if (!linked) File.Copy(source, temp, overwrite: true);
            if (File.Exists(target)) {
                File.SetAttributes(target, FileAttributes.Normal);
                File.Replace(temp, target, null, ignoreMetadataErrors: true); // 系统层面一次完成替换
            } else {
                File.Move(temp, target);
            }
        } catch {
            try {
                if (File.Exists(temp)) {
                    File.SetAttributes(temp, FileAttributes.Normal);
                    File.Delete(temp);
                }
            } catch {
                // 清理临时文件失败不影响原来的错误
            }
            throw;
        }
        return linked;
    }

    private static bool SameVolume(string a, string b) =>
        string.Equals(Path.GetPathRoot(a), Path.GetPathRoot(b), StringComparison.OrdinalIgnoreCase);

    // 超过 260 字符的路径需要 \?\ 前缀
    private static string ToExtendedPath(string path) =>
        path.Length >= 248 && !path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\?\" + path : path;

}
