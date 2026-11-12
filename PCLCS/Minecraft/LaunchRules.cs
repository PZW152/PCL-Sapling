using MeloongCore.Extensions;
using Newtonsoft.Json.Linq;

namespace PCLCS;

/// <summary>
/// 判断版本 JSON 中 "rules" 是否成立时所用的环境。
/// </summary>
/// <param name="OsName">Mojang 的操作系统名称：windows、osx 或 linux。</param>
/// <param name="OsVersion">操作系统版本号，会与规则中的版本正则匹配。</param>
/// <param name="Arch">游戏运行的架构：x86（32 位）、x86_64 或 arm64。</param>
public record RuleEnvironment(string OsName, string OsVersion, string Arch) {
    /// <summary>
    /// PCL 的运行环境：Windows，64 位 Java（ARM 设备上也使用转译的 x64 Java）。
    /// </summary>
    public static RuleEnvironment Current { get; } = new("windows", Environment.OSVersion.Version.ToString(), "x86_64");
}

/// <summary>
/// 版本 JSON 中 "rules" 的判断。
/// </summary>
public static class LaunchRules {

    /// <summary>
    /// 判断一组规则是否允许当前项目。规则依次生效：条件成立的 allow 允许，条件成立的 disallow 禁止；没有规则时允许。
    /// 条件中的 features：要求是 Demo 用户的不成立；与快速游戏（quick_play）相关的不成立，由玩家自己添加；其余（如自定义分辨率）成立。
    /// </summary>
    public static bool Check(JToken? Rules, RuleEnvironment? Environment = null) {
        if (Rules is null)
            return true;
        var Env = Environment ?? RuleEnvironment.Current;
        bool Allowed = false;
        foreach (JToken Rule in Rules) {
            bool IsMatch = Matches(Rule, Env);
            if (Rule["action"]?.ToString() == "allow") {
                if (IsMatch)
                    Allowed = true;
            } else if (IsMatch) {
                Allowed = false;
            }
        }
        return Allowed;
    }

    /// <summary>
    /// 单条规则的条件在该环境下是否成立。
    /// </summary>
    public static bool Matches(JToken Rule, RuleEnvironment Env) {
        if (Rule["os"] is JToken Os) {
            string? Name = Os["name"]?.ToString();
            if (Name is not null && Name != "unknown") {
                if (Name != Env.OsName)
                    return false;
                if (Os["version"] is JToken Version && !Env.OsVersion.RegexCheck(Version.ToString()))
                    return false;
            }
            if (Os["arch"] is JToken ArchToken && !ArchMatches(ArchToken.ToString(), Env.Arch))
                return false;
        }
        if (Rule["features"] is JObject Features) {
            if (Features["is_demo_user"] is not null)
                return false;
            if (Features.Properties().Any(f => f.Name.Contains("quick_play")))
                return false;
        }
        return true;
    }

    private static bool ArchMatches(string RuleArch, string EnvArch) {
        RuleArch = RuleArch.ToLowerInvariant();
        if (RuleArch.Contains("arm") || RuleArch == "aarch64")
            return EnvArch.ToLowerInvariant().Contains("arm") || EnvArch == "aarch64";
        if (RuleArch == "x86")
            return EnvArch == "x86";
        return RuleArch is "x86_64" or "amd64" or "x64" ? EnvArch == "x86_64" : true;
    }
}

/// <summary>
/// Maven 坐标，形如 group:artifact:version[:classifier][@extension]。
/// </summary>
public record MavenCoordinate(string Group, string Artifact, string Version, string? Classifier, string Extension) {

    public static MavenCoordinate Parse(string Name) {
        string Extension = "jar";
        int At = Name.LastIndexOf('@');
        if (At >= 0) {
            Extension = Name.Substring(At + 1);
            Name = Name.Substring(0, At);
        }
        string[] Parts = Name.Split(':');
        if (Parts.Length < 3)
            throw new FormatException($"无效的 Maven 坐标：{Name}");
        return new MavenCoordinate(Parts[0], Parts[1], Parts[2], Parts.Length > 3 ? string.Join("-", Parts.Skip(3)) : null, Extension);
    }

    /// <summary>
    /// 文件名，如 forge-1.21.4-54.0.34-client.jar。分类器（client 等）只出现在文件名中，不影响文件夹。
    /// </summary>
    public string FileName => $"{Artifact}-{Version}{(Classifier is null ? "" : "-" + Classifier)}.{Extension}";

    /// <summary>
    /// 相对于 libraries 文件夹的路径，使用反斜杠分隔，如 net\minecraftforge\forge\1.21.4-54.0.34\forge-1.21.4-54.0.34-client.jar。
    /// </summary>
    public string RelativePath => $@"{Group.Replace('.', '\\')}\{Artifact}\{Version}\{FileName}";
}
