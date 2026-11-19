namespace PCLCS;

/// <summary>
/// 修改实例时的回滚记录：每个文件在第一次被覆盖、移动或删除前先备份；提交途中失败时按相反顺序还原，
/// 不让原本能启动的实例留下新旧混杂的文件。只覆盖修改流程会碰到的文件，存档与玩家自己放的文件不在其中。
/// </summary>
public sealed class InstanceUpdateTransaction : IDisposable {
    private record Entry(string Target, string? Backup);

    private readonly string backupFolder;
    private readonly List<Entry> entries = new();
    private readonly HashSet<string> protectedTargets = new(StringComparer.OrdinalIgnoreCase);
    private bool finished;

    /// <param name="backupFolder">备份文件夹，应与实例位于同一个卷；会在开始时清空。</param>
    public InstanceUpdateTransaction(string backupFolder) {
        this.backupFolder = Path.GetFullPath(backupFolder);
        if (Directory.Exists(PathUtils.ForApi(this.backupFolder)))
            Directory.Delete(PathUtils.ForApi(this.backupFolder), recursive: true);
        Directory.CreateDirectory(PathUtils.ForApi(this.backupFolder));
    }

    /// <summary>修改 <paramref name="target"/> 之前调用。同一文件只备份第一次的内容。</summary>
    public void Protect(string target) {
        if (finished) throw new InvalidOperationException("回滚记录已经结束");
        target = Path.GetFullPath(target);
        if (!protectedTargets.Add(target)) return;
        if (!File.Exists(PathUtils.ForApi(target))) {
            entries.Add(new Entry(target, null)); // 原来没有：还原时删除
            return;
        }
        string backup = Path.Combine(backupFolder, entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.Copy(PathUtils.ForApi(target), PathUtils.ForApi(backup));
        entries.Add(new Entry(target, backup));
    }

    /// <summary>所有修改都已成功写入：删除备份。</summary>
    public void Commit() {
        finished = true;
        TryDeleteBackups();
    }

    /// <summary>
    /// 还原所有已备份的文件。还原全部成功才删除备份；有文件还原失败时保留备份文件夹并抛出异常，以便手动恢复。
    /// </summary>
    public void Rollback() {
        if (finished) return;
        finished = true;
        var errors = new List<Exception>();
        for (int i = entries.Count - 1; i >= 0; i--) {
            var entry = entries[i];
            string target = PathUtils.ForApi(entry.Target);
            try {
                if (File.Exists(target))
                    File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.ReadOnly);
                if (entry.Backup is null) {
                    if (File.Exists(target)) File.Delete(target);
                } else {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(PathUtils.ForApi(entry.Backup), target, overwrite: true);
                }
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                errors.Add(new IOException($"无法还原 {entry.Target}", ex));
            }
        }
        if (errors.Any())
            throw new AggregateException($"部分文件未能还原，备份保留在 {backupFolder}", errors);
        TryDeleteBackups();
    }

    /// <summary>未提交就释放时视为失败，进行回滚。</summary>
    public void Dispose() {
        if (!finished) Rollback();
    }

    private void TryDeleteBackups() {
        try {
            if (Directory.Exists(PathUtils.ForApi(backupFolder)))
                Directory.Delete(PathUtils.ForApi(backupFolder), recursive: true);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
