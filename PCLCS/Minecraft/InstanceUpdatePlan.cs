namespace PCLCS;

/// <summary>在修改现有实例前，列出文件变更并确认安装产物可以读取。</summary>
public sealed class InstanceUpdatePlan {
    public record FileChange(string Source, string Target);

    public IReadOnlyList<FileChange> Resources { get; }
    public IReadOnlyList<string> Recycle { get; }
    public IReadOnlyList<FileChange> VersionFiles { get; }
    public IReadOnlyList<FileChange> ManagedFiles { get; }

    private InstanceUpdatePlan(List<FileChange> resources, List<string> recycle, List<FileChange> versionFiles, List<FileChange> managedFiles) {
        Resources = resources.AsReadOnly();
        Recycle = recycle.AsReadOnly();
        VersionFiles = versionFiles.AsReadOnly();
        ManagedFiles = managedFiles.AsReadOnly();
    }

    public static InstanceUpdatePlan Prepare(string buildFolder, string gameFolder, string oldVersionJson, string versionJar,
        IEnumerable<string> oldManagedPaths, IEnumerable<string> newManagedPaths, CancellationToken cancel = default) {
        string buildRoot = WithTrailingSeparator(buildFolder);
        string gameRoot = WithTrailingSeparator(gameFolder);
        string instanceName = Path.GetFileNameWithoutExtension(versionJar);
        string resourceRoot = WithTrailingSeparator(Path.Combine(buildRoot, "resources"));
        var resources = DirectoryUtils.EnumerateFiles(resourceRoot, includeSubDirectories: true)
            .Select(source => new FileChange(source, Resolve(gameRoot, "resources/" + source.Substring(resourceRoot.Length))))
            .ToList();
        var versionFiles = new List<FileChange> {
            new(Path.Combine(buildRoot, instanceName + ".jar"), versionJar),
            new(Path.Combine(buildRoot, instanceName + ".json"), oldVersionJson)
        };
        var managedFiles = newManagedPaths.Select(relativePath => new FileChange(Resolve(buildRoot, relativePath), Resolve(gameRoot, relativePath))).ToList();
        var managedTargets = new HashSet<string>(managedFiles.Select(file => file.Target), StringComparer.OrdinalIgnoreCase);
        var recycle = oldManagedPaths.Select(path => Resolve(gameRoot, path)).Where(path => !managedTargets.Contains(path))
            .Concat(new[] { oldVersionJson, Path.ChangeExtension(oldVersionJson, ".jar"), versionJar })
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in resources.Concat(versionFiles).Concat(managedFiles)) {
            cancel.ThrowIfCancellationRequested();
            if (!destinations.Add(file.Target))
                throw new IOException($"安装清单中有重复的目标文件：{file.Target}");
            using var source = new FileStream(PathUtils.ForApi(file.Source), FileMode.Open, FileAccess.Read, FileShare.Read);
            if (DirectoryUtils.Exists(file.Target))
                throw new IOException($"安装目标是一个文件夹：{file.Target}");
            for (string? directory = Path.GetDirectoryName(file.Target); directory is not null && !DirectoryUtils.Exists(directory); directory = Path.GetDirectoryName(directory)) {
                if (FileUtils.Exists(directory))
                    throw new IOException($"无法创建安装目标文件夹，已有同名文件：{directory}");
            }
        }
        foreach (string target in destinations) {
            for (string? directory = Path.GetDirectoryName(target); directory is not null && !DirectoryUtils.Exists(directory); directory = Path.GetDirectoryName(directory)) {
                if (destinations.Contains(directory))
                    throw new IOException($"安装清单同时将一个路径用作文件与文件夹：{directory}");
            }
        }
        return new InstanceUpdatePlan(resources, recycle, versionFiles, managedFiles);
    }

    private static string WithTrailingSeparator(string folder) =>
        Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

    private static string Resolve(string root, string relativePath) {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new IOException($"安装清单中的文件路径无效：{relativePath}");
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || path.Length == root.Length)
            throw new IOException($"安装清单中的文件不在目标目录下：{relativePath}");
        return path;
    }
}
