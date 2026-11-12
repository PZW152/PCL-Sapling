namespace PCLCS;

/// <summary>
/// 导出与导入启动器设置。
/// 只处理“偏好”类设置：不含登录信息、加密内容、缓存、本机路径与版本独立设置，
/// 所以导出的文件可以放心分享，导入到另一台电脑上也不会出错。
/// </summary>
public static class SettingsTransfer {

    public const string FormatName = "PCL152 Settings";
    public const int FormatVersion = 1;

    /// <summary>只导出这些前缀的设置（启动、工具、界面）。</summary>
    private static readonly string[] IncludedPrefixes = ["Launch", "Tool", "Ui"];

    /// <summary>
    /// 与本机相关、换一台电脑就没有意义的设置，即使前缀匹配也不导出。
    /// </summary>
    private static readonly HashSet<string> MachineSpecific = new(StringComparer.Ordinal) {
        "LaunchFolders", "LaunchFolderSelect", "LaunchArgumentJavaSelect", "LaunchArgumentJavaAll",
        "LaunchSkinID", "ToolUpdateReleaseLast", "ToolUpdateSnapshotLast"
    };

    /// <summary>
    /// 该设置是否可以导出与导入。
    /// </summary>
    /// <param name="isPerInstance">是否为版本独立设置（存放在各版本文件夹里，不属于启动器）。</param>
    public static bool IsTransferable(string key, bool encrypted, bool isPerInstance) =>
        !encrypted && !isPerInstance && !MachineSpecific.Contains(key) &&
        IncludedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal) && key.Length > prefix.Length && char.IsUpper(key[prefix.Length]));

    /// <summary>
    /// 生成导出文件的内容。值为 null 的设置会被跳过。
    /// </summary>
    public static string Export(IEnumerable<KeyValuePair<string, object?>> settings, DateTime exportedAt) {
        JObject values = [];
        foreach (var setting in settings.OrderBy(s => s.Key, StringComparer.Ordinal)) {
            if (setting.Value is null) continue;
            values[setting.Key] = JToken.FromObject(setting.Value);
        }
        JObject root = new() {
            ["Format"] = FormatName,
            ["Version"] = FormatVersion,
            ["ExportedAt"] = exportedAt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            ["Settings"] = values
        };
        return root.ToString(Formatting.Indented);
    }

    /// <summary>
    /// 读取导出文件。文件不是本格式时抛出 <see cref="FormatException"/>。
    /// 返回其中记录的全部设置（是否导入、如何转换类型由调用方按设置定义决定）。
    /// </summary>
    public static Dictionary<string, JToken> Parse(string content) {
        JObject root;
        try {
            root = JObject.Parse(content);
        } catch (JsonException ex) {
            throw new FormatException("这不是 PCL 的设置文件。", ex);
        }
        if (root["Format"]?.ToString() != FormatName || root["Settings"] is not JObject values)
            throw new FormatException("这不是 PCL 的设置文件。");
        if ((root["Version"]?.Type == JTokenType.Integer ? root["Version"]!.Value<int>() : 0) > FormatVersion)
            throw new FormatException("这个设置文件来自更新的版本，请先更新启动器。");
        return values.Properties().ToDictionary(p => p.Name, p => p.Value);
    }

}
