namespace PCLCS;

/// <summary>
/// 一个实例对内存的需求，单位为 GB。
/// </summary>
/// <param name="Minimum">无论如何也需要保证的最低限度内存。</param>
/// <param name="Playable">估计能勉强带动的内存。</param>
/// <param name="Comfortable">估计没啥问题的内存。</param>
/// <param name="Heavy">安装大量附加组件时需要的内存。</param>
public record RamNeeds(double Minimum, double Playable, double Comfortable, double Heavy);

/// <summary>
/// 决定启动游戏时分配多少内存。全局设置与实例设置共用这一套规则。
/// </summary>
public static class MemoryPlanner {

    /// <summary>
    /// 估算实例的内存需求：可装 Mod 的实例按 Mod 数量增加，OptiFine 实例略高于原版。
    /// </summary>
    public static RamNeeds Needs(bool Modable, bool HasOptiFine, int ModCount) {
        if (Modable)
            return new RamNeeds(0.5 + ModCount / 150d, 1.5 + ModCount / 90d, 2.7 + ModCount / 50d, 4.5 + ModCount / 25d);
        if (HasOptiFine)
            return new RamNeeds(0.5, 1.5, 3, 5);
        return new RamNeeds(0.5, 1.5, 2.5, 4);
    }

    /// <summary>
    /// 自动分配：按阶段从可用内存中划出内存，越往后的阶段越“舍不得”给。
    /// 阶段一 0 ~ 勉强：全给；阶段二 勉强 ~ 没问题：给 70%；阶段三 没问题 ~ 大量附加组件：给 40%；阶段四再给最多一份“大量附加组件”的 15%。
    /// 可用内存耗尽（不足 0.1 GB）时停止，结果不低于最低限度，保留一位小数。
    /// </summary>
    public static double Auto(double AvailableGb, RamNeeds Needs) {
        var Stages = new[] {
            (Delta: Needs.Playable, Ratio: 1d),
            (Delta: Needs.Comfortable - Needs.Playable, Ratio: 0.7),
            (Delta: Needs.Heavy - Needs.Comfortable, Ratio: 0.4),
            (Delta: Needs.Heavy, Ratio: 0.15)
        };
        double Give = 0, Available = AvailableGb;
        foreach (var (Delta, Ratio) in Stages) {
            Give += Math.Min(Available * Ratio, Delta);
            Available -= Delta / Ratio;
            if (Available < 0.1)
                break;
        }
        return Math.Round(Math.Max(Give, Needs.Minimum), 1);
    }

    /// <summary>
    /// 手动分配时滑块的值对应的内存：0.3 ~ 1.5 GB 每格 0.1，~ 8 GB 每格 0.5，~ 16 GB 每格 1，之后每格 2。
    /// </summary>
    public static double FromSlider(int Value) {
        if (Value <= 12)
            return Value * 0.1 + 0.3;
        if (Value <= 25)
            return (Value - 12) * 0.5 + 1.5;
        if (Value <= 33)
            return Value - 25 + 8;
        return (Value - 33) * 2 + 16;
    }

    /// <summary>
    /// 统计 mods 文件夹中的 Mod 文件数量（.jar、.zip、.litemod，不含子文件夹）。
    /// </summary>
    public static int CountMods(string ModsFolder) {
        if (!Directory.Exists(ModsFolder))
            return 0;
        string[] Extensions = [".jar", ".zip", ".litemod"];
        return new DirectoryInfo(ModsFolder).GetFiles().Count(f => Extensions.Contains(f.Extension.ToLowerInvariant()));
    }

    /// <summary>
    /// 把系统可用物理内存（字节）换算为 GB，保留一位小数。
    /// </summary>
    public static double BytesToGb(ulong Bytes) => Math.Round(Bytes / 1024d / 1024d / 1024d * 10d) / 10d;
}
