namespace PCL.Tests;

public class MemoryPlannerTests
{
    // 原先两个设置页面里各有一份的算法，原样保留在这里作为对照
    private static double Original(double RamAvailable, double RamMininum, double RamTarget1, double RamTarget2, double RamTarget3)
    {
        double RamGive = 0, RamDelta;
        RamDelta = RamTarget1;
        RamGive += Math.Min(RamAvailable, RamDelta);
        RamAvailable -= RamDelta;
        if (RamAvailable < 0.1d) return Math.Round(Math.Max(RamGive, RamMininum), 1);
        RamDelta = RamTarget2 - RamTarget1;
        RamGive += Math.Min(RamAvailable * 0.7d, RamDelta);
        RamAvailable -= RamDelta / 0.7d;
        if (RamAvailable < 0.1d) return Math.Round(Math.Max(RamGive, RamMininum), 1);
        RamDelta = RamTarget3 - RamTarget2;
        RamGive += Math.Min(RamAvailable * 0.4d, RamDelta);
        RamAvailable -= RamDelta / 0.4d;
        if (RamAvailable < 0.1d) return Math.Round(Math.Max(RamGive, RamMininum), 1);
        RamDelta = RamTarget3;
        RamGive += Math.Min(RamAvailable * 0.15d, RamDelta);
        return Math.Round(Math.Max(RamGive, RamMininum), 1);
    }

    [Test]
    public async Task 自动分配与原算法逐一相同()
    {
        foreach (var (Modable, OptiFine) in new[] { (false, false), (false, true), (true, false) })
        {
            foreach (int Mods in new[] { 0, 1, 37, 150, 400 })
            {
                var Needs = MemoryPlanner.Needs(Modable, OptiFine, Mods);
                for (double Available = 0; Available <= 64; Available += 0.1)
                {
                    double Gb = Math.Round(Available, 1);
                    await Assert.That(MemoryPlanner.Auto(Gb, Needs)).IsEqualTo(Original(Gb, Needs.Minimum, Needs.Playable, Needs.Comfortable, Needs.Heavy));
                }
            }
        }
    }

    [Test]
    public async Task 内存需求随实例类型变化()
    {
        await Assert.That(MemoryPlanner.Needs(false, false, 99)).IsEqualTo(new RamNeeds(0.5, 1.5, 2.5, 4));
        await Assert.That(MemoryPlanner.Needs(false, true, 99)).IsEqualTo(new RamNeeds(0.5, 1.5, 3, 5));
        await Assert.That(MemoryPlanner.Needs(true, false, 0)).IsEqualTo(new RamNeeds(0.5, 1.5, 2.7, 4.5));
        await Assert.That(MemoryPlanner.Needs(true, false, 150).Minimum).IsEqualTo(1.5);
    }

    [Test]
    public async Task 内存紧张时至少给最低值()
    {
        await Assert.That(MemoryPlanner.Auto(0, MemoryPlanner.Needs(true, false, 300))).IsEqualTo(2.5);
        await Assert.That(MemoryPlanner.Auto(1, MemoryPlanner.Needs(false, false, 0))).IsEqualTo(1.0);
        // 内存充足时原版拿满四个阶段：1.5 + 1 + 1.5 + 4
        await Assert.That(MemoryPlanner.Auto(64, MemoryPlanner.Needs(false, false, 0))).IsEqualTo(8.0);
    }

    [Test]
    public async Task 手动滑块的分段()
    {
        await Assert.That(MemoryPlanner.FromSlider(0)).IsEqualTo(0.3);
        await Assert.That(Math.Round(MemoryPlanner.FromSlider(12), 6)).IsEqualTo(1.5);
        await Assert.That(MemoryPlanner.FromSlider(13)).IsEqualTo(2.0);
        await Assert.That(MemoryPlanner.FromSlider(25)).IsEqualTo(8.0);
        await Assert.That(MemoryPlanner.FromSlider(26)).IsEqualTo(9.0);
        await Assert.That(MemoryPlanner.FromSlider(33)).IsEqualTo(16.0);
        await Assert.That(MemoryPlanner.FromSlider(34)).IsEqualTo(18.0);
    }

    [Test]
    public async Task 只统计根目录中的Mod文件()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PCLTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Folder, "sub"));
        try
        {
            foreach (var Name in new[] { "a.jar", "b.ZIP", "c.litemod", "d.txt", "e.jar.disabled", "sub/f.jar" })
                File.WriteAllText(Path.Combine(Folder, Name), "");
            await Assert.That(MemoryPlanner.CountMods(Folder)).IsEqualTo(3);
            await Assert.That(MemoryPlanner.CountMods(Path.Combine(Folder, "none"))).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(Folder, true);
        }
    }

    [Test]
    public async Task 字节换算为GB()
    {
        await Assert.That(MemoryPlanner.BytesToGb(8UL * 1024 * 1024 * 1024)).IsEqualTo(8.0);
        await Assert.That(MemoryPlanner.BytesToGb(1610612736UL)).IsEqualTo(1.5);
    }
}
