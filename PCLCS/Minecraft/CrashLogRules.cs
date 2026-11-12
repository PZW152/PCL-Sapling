using System.Text.RegularExpressions;
using MeloongCore.Extensions;

namespace PCLCS;

/// <summary>
/// 导致崩溃的原因。数值会出现在给玩家的提示中，新增时只能加在末尾。
/// </summary>
public enum CrashReason
{
    Java虚拟机参数有误,
    Mod文件被解压,
    MixinBootstrap缺失,
    内存不足,
    使用JDK,
    显卡不支持OpenGL,
    使用OpenJ9,
    Java版本过高,
    Java版本不兼容,
    Mod名称包含特殊字符,
    显卡驱动不支持导致无法设置像素格式,
    极短的程序输出,
    Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, // https://bugs.mojang.com/browse/MC-32606
    AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, // https://bugs.mojang.com/browse/MC-31618
    Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION,
    玩家手动触发调试崩溃,
    光影或资源包导致OpenGL1282错误,
    文件或内容校验失败,
    确定Mod导致游戏崩溃,
    怀疑Mod导致游戏崩溃,
    Mod配置文件导致游戏崩溃,
    ModMixin失败,
    Mod加载器报错,
    Mod初始化失败,
    堆栈分析发现关键字,
    堆栈分析发现Mod名称,
    OptiFine导致无法加载世界, // https://www.minecraftforum.net/forums/support/java-edition-support/3051132-exception-ticking-world
    特定方块导致崩溃,
    特定实体导致崩溃,
    材质过大或显卡配置不足,
    没有可用的分析文件,
    使用32位Java导致JVM无法分配足够多的内存,
    Mod重复安装,
    Mod互不兼容,
    OptiFine与Forge不兼容,
    Fabric报错,
    Fabric报错并给出解决方案,
    Forge报错,
    低版本Forge与高版本Java不兼容,
    版本Json中存在多个Forge,
    Mod过多导致超出ID限制,
    NightConfig的Bug,
    ShadersMod与OptiFine同时安装,
    Forge安装不完整,
    Mod需要Java11,
    Mod缺少前置或MC版本错误
}

/// <summary>
/// 崩溃日志的规则匹配：根据游戏日志、虚拟机日志与崩溃报告的文本找出可能的崩溃原因。
/// 只做文本分析；把关键词换成实际 Mod 名称（需要读取 Mod 文件）由调用方通过 FindModNames 提供。
/// </summary>
public sealed class CrashLogRules {

    public string? LogMc { get; }
    public string? LogMcDebug { get; }
    public string? LogHs { get; }
    public string? LogCrash { get; }

    /// <summary>找到的原因与附加信息，按找到的先后排列。</summary>
    public Dictionary<CrashReason, List<string>> Reasons { get; } = new();

    private readonly Func<IEnumerable<string>, List<string>?> FindModNames;
    private readonly Action<string>? Log;

    /// <param name="FindModNames">根据关键词找出实际的 Mod 名称；找不到时返回 null。</param>
    /// <param name="Log">输出分析过程的日志。</param>
    public CrashLogRules(string? LogMc, string? LogMcDebug, string? LogHs, string? LogCrash, Func<IEnumerable<string>, List<string>?>? FindModNames = null, Action<string>? Log = null) {
        this.LogMc = LogMc;
        this.LogMcDebug = LogMcDebug;
        this.LogHs = LogHs;
        this.LogCrash = LogCrash;
        this.FindModNames = FindModNames ?? (_ => null);
        this.Log = Log;
    }

    /// <summary>所有日志拼接在一起的文本，用于粗略判断。</summary>
    public string LogAll => (LogMc ?? LogMcDebug ?? "") + (LogHs ?? "") + (LogCrash ?? "");

    /// <summary>
    /// 增加一个可能的崩溃原因。同一原因的附加信息会合并去重。
    /// </summary>
    public void Append(CrashReason Reason, IEnumerable<string>? Additional = null) {
        var List = Additional?.ToList();
        if (Reasons.TryGetValue(Reason, out var Existing)) {
            if (List is not null)
                Reasons[Reason] = Existing.Concat(List).Distinct().ToList();
        } else {
            Reasons.Add(Reason, List ?? []);
        }
        Log?.Invoke($"可能的崩溃原因：{Reason}{(List is not null && List.Any() ? "（" + List.Join('；') + "）" : "")}");
    }
    public void Append(CrashReason Reason, string? Additional) =>
        Append(Reason, string.IsNullOrEmpty(Additional) ? null : [Additional!]);

    private List<string> TryModName(string? Keyword) {
        var RawList = new List<string> { Keyword ?? "" };
        if (string.IsNullOrEmpty(Keyword))
            return RawList;
        return FindModNames(RawList) ?? RawList;
    }
    private List<string> TryModName(IEnumerable<string> Keywords) {
        if (!Keywords.Any())
            return Keywords.ToList();
        return FindModNames(Keywords) ?? Keywords.ToList();
    }

    /// <summary>
    /// 精准日志匹配，高优先级：优先于堆栈分析。
    /// </summary>
    public void Crit1() {

        // 空白分析
        if (LogMc is null && LogHs is null && LogCrash is null)
        {
            Append(CrashReason.没有可用的分析文件);
            return;
        }

        // 崩溃报告分析，高优先级
        if (LogCrash is not null)
        {
            if (LogCrash.Contains("Unable to make protected final java.lang.Class java.lang.ClassLoader.defineClass"))
                Append(CrashReason.Java版本过高);
            if (LogCrash.Contains("Failed loading config file "))
                Append(CrashReason.Mod配置文件导致游戏崩溃, new[] { TryModName((LogCrash.RegexSeek(@"(?<=Failed loading config file .+ for modid )[^\n]+") ?? "").TrimEnd('\r', '\n')).First(), (LogCrash.RegexSeek("(?<=Failed loading config file ).+(?= of type)") ?? "").TrimEnd('\r', '\n') }); // #7071
        }

        // 游戏日志分析
        if (LogMc is not null)
        {
            if (LogMc.Contains("Unrecognized option:"))
                Append(CrashReason.Java虚拟机参数有误);
            if (LogMc.Contains("Found multiple arguments for option fml.forgeVersion, but you asked for only one"))
                Append(CrashReason.版本Json中存在多个Forge);
            if (LogMc.Contains("The driver does not appear to support OpenGL"))
                Append(CrashReason.显卡不支持OpenGL);
            if (LogMc.Contains("java.lang.ClassCastException: java.base/jdk"))
                Append(CrashReason.使用JDK);
            if (LogMc.Contains("java.lang.ClassCastException: class jdk."))
                Append(CrashReason.使用JDK);
            if (LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.texture.SpriteContents.<init>"))
                Append(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains("java.lang.NoSuchMethodError: 'java.lang.String com.mojang.blaze3d.systems.RenderSystem.getBackendDescription"))
                Append(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.block.model.BakedQuad.<init>"))
                Append(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraftforge.client.gui.overlay.ForgeGui.renderSelectedItemName"))
                Append(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.server.level.DistanceManager"))
                Append(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains("java.lang.NoSuchMethodError: 'net.minecraft.network.chat.FormattedText net.minecraft.client.gui.Font.ellipsize"))
                Append(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains("Open J9 is not supported") || LogMc.Contains("OpenJ9 is incompatible") || LogMc.Contains(".J9VMInternals."))
                Append(CrashReason.使用OpenJ9);
            if (LogMc.Contains("java.lang.NoSuchFieldException: ucp"))
                Append(CrashReason.Java版本过高);
            if (LogMc.Contains("because module java.base does not export"))
                Append(CrashReason.Java版本过高);
            if (LogMc.Contains("java.lang.ClassNotFoundException: jdk.nashorn.api.scripting.NashornScriptEngineFactory"))
                Append(CrashReason.Java版本过高);
            if (LogMc.Contains("java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory"))
                Append(CrashReason.Java版本过高);
            if (LogMc.Contains("The directories below appear to be extracted jar files. Fix this before you continue."))
                Append(CrashReason.Mod文件被解压);
            if (LogMc.Contains("Extracted mod jars found, loading will NOT continue"))
                Append(CrashReason.Mod文件被解压);
            if (LogMc.Contains("java.lang.ClassNotFoundException: org.spongepowered.asm.launch.MixinTweaker"))
                Append(CrashReason.MixinBootstrap缺失);
            if (LogMc.Contains("Couldn't set pixel format"))
                Append(CrashReason.显卡驱动不支持导致无法设置像素格式);
            if (LogMc.Contains("java.lang.OutOfMemoryError") || LogMc.Contains("an out of memory error"))
                Append(CrashReason.内存不足);
            if (LogMc.Contains("java.lang.RuntimeException: Shaders Mod detected. Please remove it, OptiFine has built-in support for shaders."))
                Append(CrashReason.ShadersMod与OptiFine同时安装);
            if (LogMc.Contains("java.lang.NoSuchMethodError: sun.security.util.ManifestEntryVerifier") || LogMc.Contains("java.lang.NoSuchMethodError: 'void sun.security.util.ManifestEntryVerifier"))
                Append(CrashReason.低版本Forge与高版本Java不兼容);
            if (LogMc.Contains("1282: Invalid operation"))
                Append(CrashReason.光影或资源包导致OpenGL1282错误);
            if (LogMc.Contains("signer information does not match signer information of other classes in the same package"))
                Append(CrashReason.文件或内容校验失败, (LogMc.RegexSeek("(?<=class \")[^']+(?=\"'s signer information)") ?? "").TrimEnd('\r', '\n'));
            if (LogMc.Contains("Maybe try a lower resolution resourcepack?"))
                Append(CrashReason.材质过大或显卡配置不足);
            if (LogMc.Contains("java.lang.NoSuchMethodError: net.minecraft.world.server.ChunkManager$ProxyTicketManager.shouldForceTicks(J)Z") && LogMc.Contains("OptiFine"))
                Append(CrashReason.OptiFine导致无法加载世界);
            if (LogMc.Contains("com.electronwill.nightconfig.core.io.ParsingException: Not enough data available") && !Reasons.ContainsKey(CrashReason.Mod配置文件导致游戏崩溃))
                Append(CrashReason.NightConfig的Bug);
            if (LogMc.Contains("Cannot find launch target fmlclient, unable to launch"))
                Append(CrashReason.Forge安装不完整);
            if (LogMc.Contains("Invalid paths argument, contained no existing paths") && LogMc.Contains(@"libraries\net\minecraftforge\fmlcore"))
                Append(CrashReason.Forge安装不完整);
            if (LogMc.Contains("Invalid module name: '' is not a Java identifier"))
                Append(CrashReason.Mod名称包含特殊字符);
            if (LogMc.Contains("has been compiled by a more recent version of the Java Runtime (class file version 55.0), this version of the Java Runtime only recognizes class file versions up to"))
                Append(CrashReason.Mod需要Java11);
            if (LogMc.Contains("java.lang.RuntimeException: java.lang.NoSuchMethodException: no such method: sun.misc.Unsafe.defineAnonymousClass(Class,byte[],Object[])Class/invokeVirtual"))
                Append(CrashReason.Mod需要Java11);
            if (LogMc.Contains("java.lang.IllegalArgumentException: The requested compatibility level JAVA_11 could not be set. Level is not supported by the active JRE or ASM version"))
                Append(CrashReason.Mod需要Java11);
            if (LogMc.Contains("Unsupported class file major version"))
                Append(CrashReason.Java版本不兼容);
            if (LogMc.Contains("Unsupported major.minor version"))
                Append(CrashReason.Java版本不兼容);
            if (LogMc.Contains("Level is not supported by the active JRE or ASM version"))
                Append(CrashReason.Java版本不兼容);
            if (LogMc.Contains("Invalid maximum heap size"))
                Append(CrashReason.使用32位Java导致JVM无法分配足够多的内存);
            if (LogMc.Contains("Could not reserve enough space"))
            {
                if (LogMc.Contains("for 1048576KB object heap"))
                {
                    Append(CrashReason.使用32位Java导致JVM无法分配足够多的内存);
                }
                else
                {
                    Append(CrashReason.内存不足);
                }
            }
            // 确定的 Mod 导致崩溃
            if (LogMc.Contains("Caught exception from "))
                Append(CrashReason.确定Mod导致游戏崩溃, TryModName(LogMc.RegexSeek(@"(?<=Caught exception from )[^\n]+")?.TrimEnd('\r', '\n', ' ')));
            // Mod 重复 / 前置问题
            if (LogMc.Contains("DuplicateModsFoundException"))
                Append(CrashReason.Mod重复安装, LogMc.RegexSearch(@"(?<=\n\t[\w]+ : [A-Z]:[^\n]+(/|\\))[^/\\\n]+?.jar", RegexOptions.IgnoreCase));
            if (LogMc.Contains("Found a duplicate mod"))
                Append(CrashReason.Mod重复安装, (LogMc.RegexSeek(@"Found a duplicate mod[^\n]+") ?? "").RegexSearch(@"[^\\/]+.jar", RegexOptions.IgnoreCase));
            if (LogMc.Contains("Found duplicate mods"))
                Append(CrashReason.Mod重复安装, LogMc.RegexSearch(@"(?<=Mod ID: ')\w+?(?=' from mod files:)").Distinct().ToList());
            if (LogMc.Contains("ModResolutionException: Duplicate"))
                Append(CrashReason.Mod重复安装, (LogMc.RegexSeek(@"ModResolutionException: Duplicate[^\n]+") ?? "").RegexSearch(@"[^\\/]+.jar", RegexOptions.IgnoreCase));
            if (LogMc.Contains("Incompatible mods found!")) // #5006, #5115
            {
                Append(CrashReason.Mod互不兼容, (LogMc.RegexSeek(@"(?<=Incompatible mods found![\s\S]+: )[\s\S]+?(?=\tat )") ?? "").BeforeFirst("更多信息：").Replace("Some of your mods are incompatible with the game or each other!", "").Trim('\r', '\n', ' '));
            }
            if (LogMc.Contains("Missing or unsupported mandatory dependencies:"))
            {
                Append(CrashReason.Mod缺少前置或MC版本错误, LogMc.RegexSearch(@"(?<=Missing or unsupported mandatory dependencies:)([\n\r]+\t(.*))+", RegexOptions.IgnoreCase).Select(s => s.Trim('\r', '\n', '\t', ' ')).Distinct().ToList());
            }
        }

        // 虚拟机日志分析
        if (LogHs is not null)
        {
            if (LogHs.Contains("The system is out of physical RAM or swap space"))
                Append(CrashReason.内存不足);
            if (LogHs.Contains("Out of Memory Error"))
                Append(CrashReason.内存不足);
            if (LogHs.Contains("EXCEPTION_ACCESS_VIOLATION"))
            {
                if (LogHs.Contains("# C  [ig"))
                    Append(CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION);
                if (LogHs.Contains("# C  [atio"))
                    Append(CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION);
                if (LogHs.Contains("# C  [nvoglv"))
                    Append(CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION);
            }
        }

        // 崩溃报告分析
        if (LogCrash is not null)
        {
            if (LogCrash.Contains("maximum id range exceeded"))
                Append(CrashReason.Mod过多导致超出ID限制);
            if (LogCrash.Contains("java.lang.OutOfMemoryError"))
                Append(CrashReason.内存不足);
            if (LogCrash.Contains("Pixel format not accelerated"))
                Append(CrashReason.显卡驱动不支持导致无法设置像素格式);
            if (LogCrash.Contains("Manually triggered debug crash"))
                Append(CrashReason.玩家手动触发调试崩溃);
            if (LogCrash.Contains("has mods that were not found") && LogCrash.RegexCheck(@"The Mod File [^\n]+optifine\\OptiFine[^\n]+ has mods that were not found"))
                Append(CrashReason.OptiFine与Forge不兼容);
            // Mod 导致的崩溃
            if (LogCrash.Contains("-- MOD "))
            {
                string LogCrashMod = LogCrash.Between("-- MOD ", "Failure message:");
                if (LogCrashMod.ContainsIgnoreCase(".jar"))
                {
                    Append(CrashReason.确定Mod导致游戏崩溃, (LogCrashMod.RegexSeek("(?<=Mod File: ).+") ?? "").TrimEnd('\r', '\n', ' '));
                }
                else
                {
                    Append(CrashReason.Mod加载器报错, (LogCrash.RegexSeek(@"(?<=Failure message: )[\w\W]+?(?=\tMod)") ?? "").Replace("\t", " ").TrimEnd('\r', '\n', ' '));
                }
            }
            if (LogCrash.Contains("Multiple entries with same key: "))
                Append(CrashReason.确定Mod导致游戏崩溃, TryModName((LogCrash.RegexSeek("(?<=Multiple entries with same key: )[^=]+") ?? "").TrimEnd('\r', '\n', ' ')));
            if (LogCrash.Contains("LoaderExceptionModCrash: Caught exception from "))
                Append(CrashReason.确定Mod导致游戏崩溃, TryModName((LogCrash.RegexSeek(@"(?<=LoaderExceptionModCrash: Caught exception from )[^\n]+") ?? "").TrimEnd('\r', '\n', ' ')));
            if (LogCrash.Contains("Failed loading config file "))
                Append(CrashReason.Mod配置文件导致游戏崩溃, new[] { TryModName((LogCrash.RegexSeek(@"(?<=Failed loading config file .+ for modid )[^\n]+") ?? "").TrimEnd('\r', '\n')).First(), (LogCrash.RegexSeek("(?<=Failed loading config file ).+(?= of type)") ?? "").TrimEnd('\r', '\n') });
        }
    }

    /// <summary>
    /// 精准日志匹配，中优先级：优先于堆栈分析，但低于 Crit1。Crit1 已找到原因时不应执行。
    /// </summary>
    public void Crit2() {

        // Mixin 分析
        bool MixinAnalyze(string LogText)
        {
            bool IsMixin = LogText.Contains("Mixin prepare failed ") || LogText.Contains("Mixin apply failed ") || LogText.Contains("MixinApplyError") || LogText.Contains("MixinTransformerError") || LogText.Contains("mixin.injection.throwables.") || LogText.Contains(".json] FAILED during )");
            if (!IsMixin)
                return false;
            // Mod 名称匹配
            string? ModName = LogText.RegexSeek(@"(?<=from mod )[^.\/ ]+(?=\] from)");
            if (ModName is null)
                ModName = LogText.RegexSeek(@"(?<=for mod )[^.\/ ]+(?= failed)");
            if (ModName is not null)
            {
                Append(CrashReason.ModMixin失败, TryModName(ModName.TrimEnd('\r', '\n', ' ')));
                return true;
            }
            // JSON 名称匹配
            foreach (var JsonName in LogText.RegexSearch(@"(?<=^[^\t]+[ \[{(])[^ \[{(]+\.[^ ]+(?=\.json)", RegexOptions.Multiline))
            {
                Append(CrashReason.ModMixin失败, TryModName(JsonName.Replace("mixins", "mixin").Replace(".mixin", "").Replace("mixin.", "")));
                return true;
            }
            // 没有明确匹配
            Append(CrashReason.ModMixin失败);
            return true;
        };

        // 游戏日志分析
        if (LogMc is not null)
        {
            // Mixin 崩溃
            bool IsMixin = MixinAnalyze(LogMc);
            // 常规信息
            if (LogMc.Contains("An exception was thrown, the game will display an error screen and halt."))
                Append(CrashReason.Forge报错, LogMc.RegexSeek(@"(?<=the game will display an error screen and halt.[\n\r]+[^\n]+?Exception: )[\s\S]+?(?=\n\tat)")?.Trim('\r', '\n'));
            if (LogMc.Contains("A potential solution has been determined:"))
                Append(CrashReason.Fabric报错并给出解决方案, (LogMc.RegexSeek(@"(?<=A potential solution has been determined:\n)(\s+ - [^\n]+\n)+") ?? "").Split('\n').Select(Line => Line.Trim()).Where(Line => Line.Length > 0).Join("\n"));
            if (LogMc.Contains("A potential solution has been determined, this may resolve your problem:"))
                Append(CrashReason.Fabric报错并给出解决方案, (LogMc.RegexSeek(@"(?<=A potential solution has been determined, this may resolve your problem:\n)(\s+ - [^\n]+\n)+") ?? "").Split('\n').Select(Line => Line.Trim()).Where(Line => Line.Length > 0).Join("\n"));
            if (LogMc.Contains("确定了一种可能的解决方法，这样做可能会解决你的问题："))
                Append(CrashReason.Fabric报错并给出解决方案, (LogMc.RegexSeek(@"(?<=确定了一种可能的解决方法，这样做可能会解决你的问题：\n)(\s+ - [^\n]+\n)+") ?? "").Split('\n').Select(Line => Line.Trim()).Where(Line => Line.Length > 0).Join("\n"));
            if (!IsMixin && LogMc.Contains("due to errors, provided by ")) // 在 #3104 的情况下，这一句导致 OptiFabric 的 Mixin 失败错判为 Fabric Loader 加载失败
            {
                Append(CrashReason.确定Mod导致游戏崩溃, TryModName((LogMc.RegexSeek("(?<=due to errors, provided by ')[^']+") ?? "").TrimEnd('\r', '\n', ' ')));
            }
        }

        // 崩溃报告分析
        if (LogCrash is not null)
        {
            // Mixin 崩溃
            MixinAnalyze(LogCrash);
            // 常规信息
            if (LogCrash.Contains("Suspected Mod"))
            {
                string SuspectsRaw = LogCrash.Between("Suspected Mod", "Stacktrace");
                if (!SuspectsRaw.StartsWithF("s: None")) // Suspected Mods: None
                {
                    var Suspects = SuspectsRaw.RegexSearch(@"(?<=\n\t[^(\t]+\()[^)\n]+");
                    if (Suspects.Any())
                        Append(CrashReason.怀疑Mod导致游戏崩溃, TryModName(Suspects));
                }
            }
        }
    }

    /// <summary>
    /// 精准日志匹配，低优先级：低于堆栈分析。
    /// </summary>
    public void Crit3() {

        // 游戏日志分析
        if (LogMc is not null)
        {
            // 极短的程序输出
            if (!(LogMc.Contains("at net.") || LogMc.Contains("INFO]")) && LogHs is null && LogCrash is null && LogMc.Length < 100)
            {
                Append(CrashReason.极短的程序输出, LogMc);
            }
            // Mod 解析错误（常见于 Fabric 前置校验失败）
            if (LogMc.Contains("Mod resolution failed"))
                Append(CrashReason.Mod加载器报错);
            // Mixin 失败可以导致大量 Mod 实例创建失败，因此需要放到低优先级中
            if (LogMc.Contains("Failed to create mod instance."))
                Append(CrashReason.Mod初始化失败, TryModName((LogMc.RegexSeek("(?<=Failed to create mod instance. ModID: )[^,]+") ?? LogMc.RegexSeek(@"(?<=Failed to create mod instance. ModId )[^\n]+(?= for )") ?? "").TrimEnd('\r', '\n')));
            // 注意：Fabric 的 Warnings were found! 不一定是崩溃原因，它可能是单纯的警报
        }

        // 崩溃报告分析
        if (LogCrash is not null)
        {
            if (LogCrash.Contains("\t" + "Block location: World: "))
                Append(CrashReason.特定方块导致崩溃, (LogCrash.RegexSeek(@"(?<=\tBlock: Block\{)[^\}]+") ?? "") + " " + (LogCrash.RegexSeek(@"(?<=\tBlock location: World: )\([^\)]+\)") ?? ""));
            if (LogCrash.Contains("\t" + "Entity's Exact location: "))
                Append(CrashReason.特定实体导致崩溃, (LogCrash.RegexSeek(@"(?<=\tEntity Type: )[^\n]+(?= \()") ?? "") + " (" + (LogCrash.RegexSeek(@"(?<=\tEntity's Exact location: )[^\n]+") ?? "").TrimEnd('\r', '\n') + ")");
        }
    }

    /// <summary>
    /// 从堆栈中提取可能是 Mod ID 的关键词。排除常见的库与通用单词；关键词过多（超过 10 个）时视为匹配出错，返回空列表。
    /// </summary>
    public static List<string> StackKeywords(string? ErrorStack, Action<string>? Log = null) {
        ErrorStack = "\n" + (ErrorStack ?? "") + "\n";

        // 进行正则匹配
        var StackSearchResults = new List<string>();
        StackSearchResults.AddRange(ErrorStack.RegexSearch(@"(?<=\n[^{]+)[a-zA-Z_]+\w+\.[a-zA-Z_]+[\w\.]+(?=\.[\w\.$]+\.)"));
        StackSearchResults.AddRange(ErrorStack.RegexSearch(@"(?<=at [^(]+?\.\w+\$\w+\$)[\w\$]+?(?=\$\w+\()").Select(s => s.Replace("$", "."))); // Mixin 堆栈：xxx.xxx.xxxx$xxxx$xxx
        StackSearchResults = StackSearchResults.Distinct().ToList();

        // 检查堆栈开头
        var PossibleStacks = new List<string>();
        foreach (string Stack in StackSearchResults)
        {
            if (new[] { "java", "sun", "javax", "jdk", "oolloo", "org.lwjgl", "com.sun", "net.minecraftforge", "paulscode.sound", "com.mojang", "net.minecraft", "cpw.mods", "com.google", "org.apache", "org.spongepowered", "net.fabricmc", "com.mumfrey", "com.electronwill.nightconfig", "it.unimi.dsi", "MojangTricksIntelDriversForPerformance_javaw" }.Any(IgnoreStack => Stack.StartsWithF(IgnoreStack)))
                continue;
            PossibleStacks.Add(Stack.Trim());
        }
        PossibleStacks = PossibleStacks.Distinct().ToList();
        Log?.Invoke($"找到 {PossibleStacks.Count} 条可能的堆栈信息");
        if (!PossibleStacks.Any())
            return new List<string>();
        foreach (string Stack in PossibleStacks)
            Log?.Invoke($"- {Stack}");

        // 检查堆栈关键词
        var PossibleWords = new List<string>();
        foreach (string Stack in PossibleStacks)
        {
            string[] Splited = Stack.Split(".");
            for (int i = 0, loopTo = Math.Min(3, Splited.Count() - 1); i <= loopTo; i++) // 最多取前 4 节
            {
                string Word = Splited[i];
                if (Word.Length <= 2 || Word.StartsWithF("func_"))
                    continue;
                if (new[] { "com", "org", "net", "asm", "fml", "mod", "jar", "sun", "lib", "map", "gui", "dev", "nio", "api", "dsi", "top", "mcp", "core", "init", "mods", "main", "file", "game", "load", "read", "done", "util", "tile", "item", "base", "fake", "oshi", "impl", "data", "pool", "task", "forge", "setup", "block", "model", "mixin", "event", "unimi", "netty", "world", "lwjgl", "fakes", "fabric", "gitlab", "common", "server", "config", "mixins", "compat", "loader", "launch", "script", "entity", "assist", "client", "plugin", "modapi", "mojang", "shader", "events", "github", "recipe", "render", "packet", "events", "preinit", "preload", "machine", "reflect", "channel", "general", "handler", "content", "systems", "modules", "service", "scripts", "network", "fastutil", "optifine", "internal", "platform", "override", "fabricmc", "neoforge", "external", "injection", "listeners", "scheduler", "minecraft", "universal", "multipart", "neoforged", "microsoft", "transformer", "transformers", "minecraftforge", "blockentity", "spongepowered", "electronwill", "concurrent" }.Contains(Word.Lower()))
                    continue;
                PossibleWords.Add(Word.Trim());
            }
        }
        PossibleWords = PossibleWords.Distinct().ToList();
        Log?.Invoke($"从堆栈信息中找到 {PossibleWords.Count} 个可能的 Mod ID 关键词");
        if (PossibleWords.Any())
            Log?.Invoke($"- {PossibleWords.Join(", ")}");
        if (PossibleWords.Count > 10)
        {
            Log?.Invoke("关键词过多，考虑匹配出错，不纳入考虑");
            return new List<string>();
        }
        else
        {
            return PossibleWords;
        }
    }
}
