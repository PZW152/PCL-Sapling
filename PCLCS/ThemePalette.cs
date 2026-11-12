namespace PCLCS;

/// <summary>
/// 主题配色参数：色相、饱和度、亮度微调，以及顶部栏的色相渐变幅度。
/// 颜色由启动器的 HSL2 函数生成，它会按色相修正感知亮度，所以各主题只需要给出色相与饱和度。
/// </summary>
public record ThemeArgs(int Hue, int Saturation, int LightAdjust, int TopbarHueDelta);

public static class ThemePalette {

    /// <summary>自定义主题在设置中的编号。</summary>
    public const int CustomTheme = 14;

    /// <summary>
    /// 预设主题。编号与设置页中主题选项的值一一对应。
    /// </summary>
    public static ThemeArgs Preset(int theme) => theme switch {
        1 => new ThemeArgs(168, 64, -4, 0),   // 甜柠青
        2 => new ThemeArgs(108, 52, -8, 0),   // 小草绿
        3 => new ThemeArgs(42, 92, -6, 0),    // 菠萝黄
        4 => new ThemeArgs(20, 42, -8, 0),    // 橡木棕
        _ => new ThemeArgs(210, 85, 0, 0),    // 龙猫蓝（默认）
    };

    /// <summary>
    /// 自定义主题：由设置页的四个滑块决定。
    /// </summary>
    /// <param name="hue">色调滑块，0 ~ 360。</param>
    /// <param name="saturation">饱和度滑块，0 ~ 100。</param>
    /// <param name="delta">色调渐变滑块，0 ~ 180，90 为不渐变。</param>
    /// <param name="light">亮度滑块，0 ~ 40，20 为不调整。</param>
    public static ThemeArgs Custom(int hue, int saturation, int delta, int light) => new(
        Math.Max(0, Math.Min(360, hue)),
        Math.Max(0, Math.Min(100, saturation)),
        Math.Max(0, Math.Min(40, light)) - 20,
        Math.Max(0, Math.Min(180, delta)) - 90);

    /// <summary>
    /// 根据主题编号与自定义滑块的值，得到当前应使用的配色参数。
    /// </summary>
    public static ThemeArgs Resolve(int theme, int hue, int saturation, int delta, int light) =>
        theme == CustomTheme ? Custom(hue, saturation, delta, light) : Preset(theme);

}
