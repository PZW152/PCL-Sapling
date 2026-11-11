namespace PCLCS;

/// <summary>
/// 安装整合包之前的安全检查结果。
/// </summary>
/// <param name="DownloadCount">整合包声明需要下载的文件数。</param>
/// <param name="UntrustedHosts">下载地址中不属于 Modrinth、GitHub、GitLab、CurseForge 的网站。</param>
/// <param name="ExecutableFiles">整合包内直接附带的可执行文件（程序、脚本、快捷方式）。</param>
/// <param name="BundledJarCount">整合包内直接附带的 jar 文件数（未经资源站分发的模组）。</param>
public record ModpackReviewResult(int DownloadCount, IReadOnlyList<string> UntrustedHosts, IReadOnlyList<string> ExecutableFiles, int BundledJarCount) {
    /// <summary>
    /// 是否存在需要提醒用户的风险。
    /// </summary>
    public bool HasRisk => UntrustedHosts.Count > 0 || ExecutableFiles.Count > 0;
}

/// <summary>
/// 安装整合包之前，检查它会从哪里下载文件、是否附带了可执行文件。
/// </summary>
public static class ModpackReview {

    /// <summary>
    /// Modrinth 整合包格式允许的下载来源，以及 CurseForge 的文件分发域名。
    /// </summary>
    private static readonly string[] TrustedHosts = [
        "cdn.modrinth.com", "github.com", "raw.githubusercontent.com", "gitlab.com",
        "edge.forgecdn.net", "mediafilez.forgecdn.net", "media.forgecdn.net"
    ];

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase) {
        ".exe", ".bat", ".cmd", ".com", ".scr", ".msi", ".ps1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".lnk", ".pif", ".reg"
    };

    public static bool IsTrustedHost(string host) =>
        TrustedHosts.Any(trusted => host.Equals(trusted, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 检查 Modrinth 整合包（.mrpack）。
    /// </summary>
    /// <param name="index">modrinth.index.json 的内容。</param>
    /// <param name="archiveEntries">压缩包内所有条目的完整路径。</param>
    public static ModpackReviewResult ReviewModrinth(JObject index, IEnumerable<string> archiveEntries) {
        int downloadCount = 0;
        SortedSet<string> untrustedHosts = new(StringComparer.OrdinalIgnoreCase);
        foreach (JToken file in index["files"] as JArray ?? []) {
            if (file["env"]?["client"]?.ToString() == "unsupported") continue; // 仅服务端需要的文件不会被下载
            downloadCount++;
            foreach (JToken download in file["downloads"] as JArray ?? []) {
                string url = download.ToString();
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps) {
                    untrustedHosts.Add(url.Length > 60 ? url.Substring(0, 60) + "…" : url); // 不是 https 地址
                } else if (!IsTrustedHost(uri.Host)) {
                    untrustedHosts.Add(uri.Host);
                }
            }
        }
        List<string> executables = [];
        int bundledJars = 0;
        foreach (string entry in archiveEntries) {
            if (entry.EndsWith("/", StringComparison.Ordinal)) continue;
            string extension = Path.GetExtension(entry);
            if (ExecutableExtensions.Contains(extension)) executables.Add(entry);
            else if (extension.Equals(".jar", StringComparison.OrdinalIgnoreCase)) bundledJars++;
        }
        return new ModpackReviewResult(downloadCount, [.. untrustedHosts], executables, bundledJars);
    }

}
