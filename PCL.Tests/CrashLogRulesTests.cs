namespace PCL.Tests;

public class CrashLogRulesTests
{
    private static CrashLogRules Rules(string? Mc = null, string? Hs = null, string? Crash = null)
        => new(Mc, null, Hs, Crash);

    private static CrashLogRules RunCrit1(string? Mc = null, string? Hs = null, string? Crash = null)
    {
        var Result = Rules(Mc, Hs, Crash);
        Result.Crit1();
        return Result;
    }

    [Test]
    public async Task 没有日志()
    {
        var Result = RunCrit1();
        await Assert.That(Result.Reasons.Keys.ToList()).IsEquivalentTo(new[] { CrashReason.没有可用的分析文件 });
    }

    [Test]
    public async Task 内存不足()
    {
        await Assert.That(RunCrit1(Mc: "[main/ERROR]: java.lang.OutOfMemoryError: Java heap space").Reasons.ContainsKey(CrashReason.内存不足)).IsTrue();
        await Assert.That(RunCrit1(Hs: "# There is insufficient memory\n# The system is out of physical RAM or swap space").Reasons.ContainsKey(CrashReason.内存不足)).IsTrue();
        // 32 位 Java 无法分配 1G 堆
        var Reserve = RunCrit1(Mc: "Error occurred during initialization of VM\nCould not reserve enough space for 1048576KB object heap");
        await Assert.That(Reserve.Reasons.ContainsKey(CrashReason.使用32位Java导致JVM无法分配足够多的内存)).IsTrue();
        await Assert.That(Reserve.Reasons.ContainsKey(CrashReason.内存不足)).IsFalse();
    }

    [Test]
    public async Task 确定的Mod导致崩溃时取完整的Mod名()
    {
        // 原先的正则以惰性量词结尾，只取到一个字符
        var Result = RunCrit1(Mc: "[Client thread/ERROR]: Caught exception from Example Mod (examplemod)\nnet.minecraftforge.fml.common.LoaderExceptionModCrash");
        await Assert.That(Result.Reasons[CrashReason.确定Mod导致游戏崩溃]).IsEquivalentTo(new[] { "Example Mod (examplemod)" });
    }

    [Test]
    public async Task 关键词交给调用方换成Mod名称()
    {
        var Result = new CrashLogRules("Caught exception from examplemod\n", null, null, null, Keywords => Keywords.Select(k => "Example Mod (" + k + ")").ToList());
        Result.Crit1();
        await Assert.That(Result.Reasons[CrashReason.确定Mod导致游戏崩溃]).IsEquivalentTo(new[] { "Example Mod (examplemod)" });
    }

    [Test]
    public async Task 配置文件出错时同时给出Mod与文件名()
    {
        var Result = RunCrit1(Crash: "Failed loading config file examplemod-client.toml of type CLIENT for modid examplemod\r\n\tat foo");
        // 原先只去掉了行尾的 \r，留下了 \n
        await Assert.That(Result.Reasons[CrashReason.Mod配置文件导致游戏崩溃]).IsEquivalentTo(new[] { "examplemod", "examplemod-client.toml" });
    }

    [Test]
    public async Task 崩溃报告中的Mod文件()
    {
        const string Crash = "-- MOD examplemod --\nDetails:\n\tMod File: /C:/game/mods/examplemod-1.0.jar\n\tFailure message: Example Mod has failed\n\tMod Version: 1.0\n";
        var Result = RunCrit1(Crash: Crash);
        await Assert.That(Result.Reasons[CrashReason.确定Mod导致游戏崩溃]).IsEquivalentTo(new[] { "/C:/game/mods/examplemod-1.0.jar" });
    }

    [Test]
    public async Task 显卡驱动()
    {
        var Result = RunCrit1(Hs: "# EXCEPTION_ACCESS_VIOLATION (0xc0000005) at pc=0x1\n# Problematic frame:\n# C  [ig9icd64.dll+0x1234]");
        await Assert.That(Result.Reasons.Keys.ToList()).IsEquivalentTo(new[] { CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION });
    }

    [Test]
    public async Task Fabric给出的解决方案()
    {
        var Result = Rules(Mc: "[main/ERROR]: Incompatible mod set!\nA potential solution has been determined:\n\t - Install fabric-api, version 0.92.0 or later.\n\t - Replace sodium with version 0.5.0.\nUnmet dependency listing:\n");
        Result.Crit2();
        await Assert.That(Result.Reasons[CrashReason.Fabric报错并给出解决方案].Single()).IsEqualTo("- Install fabric-api, version 0.92.0 or later.\n- Replace sodium with version 0.5.0.");
    }

    [Test]
    public async Task Mixin失败()
    {
        var Result = Rules(Mc: "[main/ERROR]: Mixin apply failed examplemod.mixins.json:MixinFoo -> net.minecraft.Foo: InvalidInjectionException [from mod examplemod] from net.minecraft.Foo\n");
        Result.Crit2();
        await Assert.That(Result.Reasons[CrashReason.ModMixin失败]).IsEquivalentTo(new[] { "examplemod" });
    }

    [Test]
    public async Task 同一原因的信息合并去重()
    {
        var Result = Rules();
        Result.Append(CrashReason.Mod重复安装, new[] { "a.jar", "b.jar" });
        Result.Append(CrashReason.Mod重复安装, new[] { "b.jar", "c.jar" });
        Result.Append(CrashReason.Mod重复安装, (string?)null);
        await Assert.That(Result.Reasons[CrashReason.Mod重复安装]).IsEquivalentTo(new[] { "a.jar", "b.jar", "c.jar" });
    }

    [Test]
    public async Task 低优先级的特定实体()
    {
        var Result = Rules(Crash: "-- Entity being ticked --\nDetails:\n\tEntity Type: minecraft:zombie (net.minecraft.world.entity.monster.Zombie)\n\tEntity's Exact location: 1.50, 64.00, -3.25\n");
        Result.Crit3();
        await Assert.That(Result.Reasons[CrashReason.特定实体导致崩溃].Single()).IsEqualTo("minecraft:zombie (1.50, 64.00, -3.25)");
    }

    [Test]
    public async Task 堆栈关键词()
    {
        const string Stack = "java.lang.NullPointerException\n\tat com.example.coolmod.Thing.method(Thing.java:10)\n\tat net.minecraft.client.Minecraft.run(Minecraft.java:100)\n\tat java.lang.Thread.run(Thread.java:1)";
        var Words = CrashLogRules.StackKeywords(Stack);
        await Assert.That(Words).Contains("coolmod");
        await Assert.That(Words).DoesNotContain("minecraft");
        await Assert.That(Words).DoesNotContain("com");
        await Assert.That(CrashLogRules.StackKeywords(null)).IsEmpty();
    }
}
