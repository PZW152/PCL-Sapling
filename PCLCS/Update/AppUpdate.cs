using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace PCLCS;

/// <summary>
/// 一个可用的新版本。
/// </summary>
/// <param name="Version">版本号，如 1.2.0。</param>
/// <param name="Url">exe 的下载地址。</param>
/// <param name="Sha256">GitHub 给出的 exe 的 SHA-256（小写十六进制）。</param>
/// <param name="Notes">更新说明（Release 的正文）。</param>
public record UpdateInfo(string Version, string Url, string Sha256, string Notes);

/// <summary>
/// 启动器自动更新：从 GitHub Releases 获取最新版本，只接受带有 SHA-256 摘要的发布文件，下载后校验。
/// 做法与 Sapling 相同。
/// </summary>
public static class AppUpdate {

    public const string LatestReleaseApi = "https://api.github.com/repos/PZW152/PCL-Sapling/releases/latest";
    public const string AssetPrefix = "PCL-Sapling-";
    public const string AssetSuffix = ".exe";
    private const string DigestPrefix = "sha256:";

    /// <summary>
    /// 根据 GitHub Releases API 返回的最新版本，判断是否有比当前版本更新的版本。
    /// 版本号不能解析、没有发布文件或文件没有 SHA-256 摘要时，视为没有更新。
    /// </summary>
    public static UpdateInfo? FromRelease(JObject Release, string CurrentVersion) {
        string Tag = Release["tag_name"]?.ToString() ?? "";
        var Latest = ParseVersion(Tag);
        var Current = ParseVersion(CurrentVersion);
        if (Latest is null || Current is null || Compare(Latest.Value, Current.Value) <= 0)
            return null;
        if (Release["draft"]?.ToObject<bool>() == true || Release["prerelease"]?.ToObject<bool>() == true)
            return null;
        foreach (var Asset in Release["assets"] as JArray ?? []) {
            string Name = Asset["name"]?.ToString() ?? "";
            if (!IsReleaseFile(Name))
                continue;
            string Digest = Asset["digest"]?.ToString() ?? "";
            string Url = Asset["browser_download_url"]?.ToString() ?? "";
            if (!Digest.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase) || Url.Length == 0)
                return null; // 没有校验值的文件不下载
            return new UpdateInfo(Tag.TrimStart('v', 'V'), Url, Digest.Substring(DigestPrefix.Length).ToLowerInvariant(), Release["body"]?.ToString() ?? "");
        }
        return null;
    }

    /// <summary>发布文件的名称：PCL-Sapling-版本号.exe。</summary>
    public static bool IsReleaseFile(string Name) =>
        Name.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase) && Name.EndsWith(AssetSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>解析 1.2.3 或 v1.2.3。</summary>
    public static (int Major, int Minor, int Patch)? ParseVersion(string? Version) {
        if (string.IsNullOrWhiteSpace(Version))
            return null;
        string[] Parts = Version!.Trim().TrimStart('v', 'V').Split('.');
        if (Parts.Length != 3)
            return null;
        if (int.TryParse(Parts[0], out int Major) && int.TryParse(Parts[1], out int Minor) && int.TryParse(Parts[2], out int Patch) && Major >= 0 && Minor >= 0 && Patch >= 0)
            return (Major, Minor, Patch);
        return null;
    }

    private static int Compare((int, int, int) a, (int, int, int) b) => a.CompareTo(b);

    /// <summary>
    /// 校验下载内容的 SHA-256。
    /// </summary>
    public static bool Verify(byte[] Content, string ExpectedSha256) {
        using var Hasher = SHA256.Create();
        string Actual = BitConverter.ToString(Hasher.ComputeHash(Content)).Replace("-", "").ToLowerInvariant();
        return string.Equals(Actual, ExpectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>旧版本在更新时被改名为的文件名。</summary>
    public static string OldPath(string ExePath) => ExePath + ".old";

    /// <summary>
    /// 替换正在运行的程序：Windows 不能覆盖运行中的 exe，但可以改名，所以先把当前文件改名为 .old，再把新文件放到原位置。
    /// 放入失败时改回原名。旧文件在新版本启动后由 <see cref="RemoveOldVersion"/> 删除。
    /// </summary>
    public static void Install(string ExePath, byte[] NewContent) {
        string NewPath = ExePath + ".new";
        string Old = OldPath(ExePath);
        File.WriteAllBytes(NewPath, NewContent);
        if (File.Exists(Old))
            File.Delete(Old);
        File.Move(ExePath, Old);
        try {
            File.Move(NewPath, ExePath);
        } catch {
            File.Move(Old, ExePath);
            throw;
        }
    }

    /// <summary>
    /// 删除上一次更新留下的旧版本。旧进程可能还没完全退出，所以会重试几次。
    /// </summary>
    public static bool RemoveOldVersion(string ExePath, int Attempts = 10, int IntervalMs = 1000) {
        string Old = OldPath(ExePath);
        for (int i = 0; i < Attempts; i++) {
            try {
                if (File.Exists(Old))
                    File.Delete(Old);
                if (File.Exists(ExePath + ".new"))
                    File.Delete(ExePath + ".new");
                return true;
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
            Thread.Sleep(IntervalMs);
        }
        return false;
    }
}
