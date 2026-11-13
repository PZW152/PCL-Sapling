namespace PCLCS;

/// <summary>在目标文件所在目录准备下载结果，校验成功后才替换正式文件。</summary>
public sealed class DownloadOutput : IDisposable {
    private readonly string targetPath;
    public string StagingPath { get; }

    public DownloadOutput(string targetPath) {
        this.targetPath = Path.GetFullPath(targetPath);
        string directory = Path.GetDirectoryName(this.targetPath)!;
        DirectoryUtils.Create(directory);
        StagingPath = Path.Combine(directory, $".pcl-download-{Guid.NewGuid():N}.tmp");
    }

    public void Publish() {
        string target = PathUtils.ForApi(targetPath);
        string staging = PathUtils.ForApi(StagingPath);
        if (!File.Exists(target)) {
            File.Move(staging, target);
            return;
        }

        FileAttributes attributes = File.GetAttributes(target);
        bool readOnly = (attributes & FileAttributes.ReadOnly) != 0;
        if (readOnly) File.SetAttributes(target, attributes & ~FileAttributes.ReadOnly);
        try {
            File.Replace(staging, target, null, ignoreMetadataErrors: true);
        } catch {
            if (readOnly) {
                try { File.SetAttributes(target, attributes); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            throw;
        }
    }

    public void Dispose() {
        try { FileUtils.Delete(StagingPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
