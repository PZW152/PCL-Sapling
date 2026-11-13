using System.IO.Compression;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace PCLCS;

/// <summary>
/// 一个 Mod 文件自己声明的信息：它提供哪些 Mod ID，需要哪些 Mod ID。
/// </summary>
/// <param name="FileName">文件名，用来在提示里指出是哪个文件。</param>
/// <param name="Name">Mod 的显示名称；没有时为 ID 或文件名。</param>
/// <param name="Provides">它提供的 ID：自己的 ID、声明的别名，以及打包在里面的 Mod（Jar-in-Jar）。</param>
/// <param name="Requires">它必须的前置 ID。</param>
public record ModMetadata(string FileName, string Name, HashSet<string> Provides, List<string> Requires);

/// <summary>
/// 缺少的前置：哪个 Mod 需要哪个 ID。
/// </summary>
public record MissingDependency(string ModName, string FileName, string RequiredId);

/// <summary>
/// 检查已安装的 Mod 是否缺少前置（取自 PCL CE 的 Jar-in-Jar 依赖查看，这里只做玩家真正需要的部分：缺了什么）。
/// 读取 Mod 文件里的 fabric.mod.json、quilt.mod.json、META-INF/mods.toml 与 META-INF/neoforge.mods.toml。
/// 只检查前置是否存在，不比较版本范围。
/// </summary>
public static class ModDependencies {
    /// <summary>
    /// 加载器、游戏本身与 Java 提供的 ID，不需要另外安装。
    /// </summary>
    public static readonly HashSet<string> BuiltIn = new(StringComparer.OrdinalIgnoreCase) {
        "minecraft", "java", "fabricloader", "fabric-loader", "quilt_loader", "quilt_base", "forge", "neoforge", "fml", "javafml", "lowcodefml", "mixinextras"
    };

    /// <summary>
    /// 读取一个 Mod 文件。不是 Mod、读不懂或损坏时返回 null。
    /// </summary>
    public static ModMetadata? Read(string filePath) {
        try {
            using var stream = File.OpenRead(filePath);
            return Read(stream, Path.GetFileName(filePath));
        } catch (Exception) {
            return null;
        }
    }

    public static ModMetadata? Read(Stream jar, string fileName) {
        try {
            using var zip = new ZipArchive(jar, ZipArchiveMode.Read, leaveOpen: true);
            var provides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requires = new List<string>();
            string? name = null;
            bool found = ReadInto(zip, provides, requires, ref name, depth: 0);
            if (!found) return null;
            requires = requires.Where(id => !provides.Contains(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return new ModMetadata(fileName, name ?? provides.FirstOrDefault() ?? fileName, provides, requires);
        } catch (InvalidDataException) {
            return null;
        }
    }

    /// <summary>
    /// 读取一个压缩包中的元数据，并递归读取打包在里面的 Mod。返回是否找到了任何元数据。
    /// 只有最外层的“需要”会计入：被打包的 Mod 的前置一般也由外层一起打包或声明。
    /// </summary>
    private static bool ReadInto(ZipArchive zip, HashSet<string> provides, List<string> requires, ref string? name, int depth) {
        bool found = false;
        var nested = new List<string>();
        // Fabric
        if (ReadJson(zip, "fabric.mod.json") is JObject fabric) {
            found = true;
            AddId(provides, fabric.Value<string>("id"));
            if (fabric["provides"] is JArray aliases)
                foreach (var alias in aliases) AddId(provides, alias.ToString());
            if (depth == 0) {
                name ??= fabric.Value<string>("name");
                if (fabric["depends"] is JObject depends)
                    requires.AddRange(depends.Properties().Select(p => p.Name));
            }
            if (fabric["jars"] is JArray jars)
                nested.AddRange(jars.OfType<JObject>().Select(j => j.Value<string>("file")).Where(f => !string.IsNullOrEmpty(f))!);
        }
        // Quilt
        if (ReadJson(zip, "quilt.mod.json") is JObject quilt && quilt["quilt_loader"] is JObject loader) {
            found = true;
            AddId(provides, loader.Value<string>("id"));
            if (loader["provides"] is JArray aliases)
                foreach (var alias in aliases) AddId(provides, alias is JObject o ? o.Value<string>("id") : alias.ToString());
            if (depth == 0) {
                name ??= loader["metadata"]?.Value<string>("name");
                if (loader["depends"] is JArray depends)
                    foreach (var dep in depends) {
                        if (dep is JObject o) {
                            if (o.Value<bool?>("optional") == true) continue;
                            AddId(requires, o.Value<string>("id"));
                        } else {
                            AddId(requires, dep.ToString());
                        }
                    }
            }
            if (loader["jars"] is JArray jars)
                nested.AddRange(jars.Select(j => j.ToString()));
        }
        // Forge 与 NeoForge
        foreach (string toml in new[] { "META-INF/mods.toml", "META-INF/neoforge.mods.toml" }) {
            if (ReadText(zip, toml) is not string text) continue;
            found = true;
            var parsed = ParseModsToml(text);
            foreach (var id in parsed.Ids) AddId(provides, id);
            if (depth == 0) {
                name ??= parsed.Name;
                requires.AddRange(parsed.Requires);
            }
        }
        // Forge 的 Jar-in-Jar 清单
        if (ReadJson(zip, "META-INF/jarjar/metadata.json") is JObject jarjar && jarjar["jars"] is JArray forgeJars)
            nested.AddRange(forgeJars.OfType<JObject>().Select(j => j.Value<string>("path")).Where(f => !string.IsNullOrEmpty(f))!);
        // 打包在里面的 Mod 也算“提供”
        if (depth < 3)
            foreach (string entryName in nested.Distinct()) {
                var entry = zip.GetEntry(entryName);
                if (entry is null) continue;
                try {
                    using var inner = new MemoryStream();
                    using (var source = entry.Open()) source.CopyTo(inner);
                    inner.Position = 0;
                    using var innerZip = new ZipArchive(inner, ZipArchiveMode.Read);
                    string? ignored = null;
                    ReadInto(innerZip, provides, requires, ref ignored, depth + 1);
                } catch (InvalidDataException) {
                }
            }
        return found;
    }

    /// <summary>
    /// mods.toml 中需要的部分：每个 [[mods]] 的 modId 与 displayName，以及必须的 [[dependencies.*]]。
    /// 旧格式用 mandatory=true，新格式用 type="required"。
    /// </summary>
    public static (List<string> Ids, string? Name, List<string> Requires) ParseModsToml(string text) {
        var ids = new List<string>();
        var requires = new List<string>();
        string? name = null;
        string section = "";
        string? depId = null;
        bool? depRequired = null;
        void FlushDependency() {
            if (depId is not null && depRequired == true) requires.Add(depId);
            depId = null;
            depRequired = null;
        }
        foreach (string rawLine in text.Split('\n')) {
            string line = rawLine.Trim();
            if (line.StartsWith("#") || line.Length == 0) continue;
            if (line.StartsWith("[")) {
                FlushDependency();
                section = line.Trim('[', ']', ' ').ToLowerInvariant();
                continue;
            }
            var match = Regex.Match(line, @"^([A-Za-z_]+)\s*=\s*(.+?)\s*(#.*)?$");
            if (!match.Success) continue;
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim().Trim('"', '\'');
            if (section == "mods") {
                if (key == "modId") ids.Add(value);
                else if (key == "displayName") name ??= value;
            } else if (section.StartsWith("dependencies")) {
                if (key == "modId") depId = value;
                else if (key == "mandatory") depRequired = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                else if (key == "type") depRequired = value.Equals("required", StringComparison.OrdinalIgnoreCase);
            }
        }
        FlushDependency();
        return (ids, name, requires);
    }

    /// <summary>
    /// 找出缺少的前置：某个 Mod 需要的 ID 既不是内置的，也没有任何已安装的 Mod 提供。
    /// </summary>
    public static List<MissingDependency> FindMissing(IEnumerable<ModMetadata> mods) {
        var list = mods.ToList();
        var available = new HashSet<string>(BuiltIn, StringComparer.OrdinalIgnoreCase);
        foreach (var mod in list) available.UnionWith(mod.Provides);
        // Fabric API 的旧 ID：Quilt 标准库（QFAPI）也提供它
        if (available.Contains("fabric-api")) available.Add("fabric");
        if (available.Contains("fabric")) available.Add("fabric-api");
        return [.. list.SelectMany(mod => mod.Requires.Where(id => !available.Contains(id)).Select(id => new MissingDependency(mod.Name, mod.FileName, id)))];
    }

    private static void AddId(ICollection<string> target, string? id) {
        if (!string.IsNullOrWhiteSpace(id)) target.Add(id!.Trim());
    }

    private static string? ReadText(ZipArchive zip, string entryName) {
        var entry = zip.GetEntry(entryName);
        if (entry is null) return null;
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static JObject? ReadJson(ZipArchive zip, string entryName) {
        try {
            return ReadText(zip, entryName) is string text ? JObject.Parse(text) : null;
        } catch (Exception) {
            return null; // 写错的 JSON 不影响其他 Mod
        }
    }
}
