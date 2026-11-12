namespace PCLCS;

/// <summary>
/// 主题配色参数：色相、饱和度、亮度微调，以及顶部栏的色相渐变幅度。
/// 颜色由启动器的 HSL2 函数生成，它会按色相修正感知亮度，所以各主题只需要给出色相与饱和度。
/// </summary>
public record ThemeArgs(int Hue, int Saturation, int LightAdjust, int TopbarHueDelta);

public static class ThemePalette {

    /// <summary>自定义主题在设置中的编号。</summary>
    public const int CustomTheme = 14;

    /// <summary>蓝色的色相范围。PCL 的使用指南要求界面主色相必须为蓝色，自定义主题也只能在这个范围内调整。</summary>
    public const int BlueHueMin = 200, BlueHueMax = 235;
    /// <summary>顶部栏色相渐变的最大幅度，保证渐变后仍是蓝色。</summary>
    public const int MaxTopbarDelta = 20;

    /// <summary>
    /// 预设主题。编号与设置页中主题选项的值一一对应；全部是蓝色系，只在色相、饱和度与亮度上有区别。
    /// </summary>
    public static ThemeArgs Preset(int theme) => theme switch {
        1 => new ThemeArgs(204, 92, 4, 0),    // 晴空蓝
        2 => new ThemeArgs(220, 74, -10, 0),  // 深海蓝
        3 => new ThemeArgs(207, 46, 6, 0),    // 冰川蓝
        4 => new ThemeArgs(229, 60, -4, 0),   // 暮光蓝
        _ => new ThemeArgs(210, 85, 0, 0),    // 龙猫蓝（默认）
    };

    /// <summary>
    /// 自定义主题：由设置页的四个滑块决定。
    /// </summary>
    /// <param name="hue">色调滑块，0 ~ 35，对应色相 200 ~ 235。</param>
    /// <param name="saturation">饱和度滑块，0 ~ 100。</param>
    /// <param name="delta">色调渐变滑块，0 ~ 40，20 为不渐变。</param>
    /// <param name="light">亮度滑块，0 ~ 40，20 为不调整。</param>
    public static ThemeArgs Custom(int hue, int saturation, int delta, int light) => new(
        BlueHueMin + Math.Max(0, Math.Min(BlueHueMax - BlueHueMin, hue)),
        Math.Max(0, Math.Min(100, saturation)),
        Math.Max(0, Math.Min(40, light)) - 20,
        Math.Max(0, Math.Min(MaxTopbarDelta * 2, delta)) - MaxTopbarDelta);

    /// <summary>
    /// 根据主题编号与自定义滑块的值，得到当前应使用的配色参数。
    /// </summary>
    public static ThemeArgs Resolve(int theme, int hue, int saturation, int delta, int light) =>
        theme == CustomTheme ? Custom(hue, saturation, delta, light) : Preset(theme);

}
