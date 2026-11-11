using System.Globalization;

namespace PCLCS;

/// <summary>
/// 新游戏的默认设置：游戏第一次启动前（还没有 options.txt 时）写入一次，之后玩家自己的改动一律不动。
/// 每项都可以不设，交给游戏自己决定。
/// </summary>
public static class GameDefaults {

    /// <summary>视距选项（区块数）；0 表示不设置。下标与设置中的选项一一对应。</summary>
    public static readonly int[] RenderDistanceChoices = [0, 6, 8, 12, 16, 24, 32];
    /// <summary>音量选项（百分比）；0 表示不设置。</summary>
    public static readonly int[] VolumeChoices = [0, 25, 50, 75, 100];

    /// <summary>
    /// 生成要写入 options.txt 的键值对。
    /// 新旧版本名称不同的设置会把每种写法都写上：游戏只读取自己认得的那一项，其余的会被忽略。
    /// </summary>
    /// <param name="renderDistanceIndex"><see cref="RenderDistanceChoices"/> 的下标。</param>
    /// <param name="volumeIndex"><see cref="VolumeChoices"/> 的下标。</param>
    /// <param name="graphics">画质：0 不设置，1 流畅，2 高品质。</param>
    /// <param name="keepRunningUnfocused">切到其他窗口时游戏是否继续运行，不自动暂停。</param>
    public static List<KeyValuePair<string, string>> Entries(int renderDistanceIndex, int volumeIndex, int graphics, bool keepRunningUnfocused) {
        List<KeyValuePair<string, string>> result = [];
        int distance = Pick(RenderDistanceChoices, renderDistanceIndex);
        if (distance > 0) result.Add(new("renderDistance", distance.ToString(CultureInfo.InvariantCulture)));
        int volume = Pick(VolumeChoices, volumeIndex);
        if (volume > 0) result.Add(new("soundCategory_master", (volume / 100d).ToString(CultureInfo.InvariantCulture)));
        if (graphics is 1 or 2) {
            bool fancy = graphics == 2;
            // 1.15 以前、1.16 ~ 1.21.10、1.21.11 起各用一个名称
            result.Add(new("fancyGraphics", fancy ? "true" : "false"));
            result.Add(new("graphicsMode", fancy ? "1" : "0"));
            result.Add(new("graphicsPreset", fancy ? "\"fancy\"" : "\"fast\""));
        }
        if (keepRunningUnfocused) result.Add(new("pauseOnLostFocus", "false"));
        return result;
    }

    private static int Pick(int[] choices, int index) => index > 0 && index < choices.Length ? choices[index] : 0;

}
