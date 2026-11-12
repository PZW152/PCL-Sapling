namespace PCL.Tests;

public class ThemePaletteTests
{
    [Test]
    public async Task 每个预设主题的色相都不同()
    {
        var Hues = new[] { 0, 1, 2, 3, 4 }.Select(t => ThemePalette.Preset(t).Hue).ToList();
        await Assert.That(Hues.Distinct().Count()).IsEqualTo(5);
        // 相邻色相至少相差 20 度，肉眼才分得出来
        var Sorted = Hues.OrderBy(h => h).ToList();
        for (int i = 1; i < Sorted.Count; i++)
            await Assert.That(Sorted[i] - Sorted[i - 1] >= 20).IsTrue();
    }

    [Test]
    public async Task 默认主题与原先的龙猫蓝一致()
    {
        await Assert.That(ThemePalette.Preset(0)).IsEqualTo(new ThemeArgs(210, 85, 0, 0));
        await Assert.That(ThemePalette.Preset(99)).IsEqualTo(ThemePalette.Preset(0)); // 未知编号回到默认
    }

    [Test]
    public async Task 自定义主题读取滑块的值()
    {
        // 滑块的默认值：色调 180、饱和度 80、渐变 90、亮度 20
        await Assert.That(ThemePalette.Resolve(14, 180, 80, 90, 20)).IsEqualTo(new ThemeArgs(180, 80, 0, 0));
        await Assert.That(ThemePalette.Resolve(14, 300, 40, 120, 35)).IsEqualTo(new ThemeArgs(300, 40, 15, 30));
        await Assert.That(ThemePalette.Resolve(14, 999, -5, 999, -1)).IsEqualTo(new ThemeArgs(360, 0, -20, 90));
    }

    [Test]
    public async Task 预设主题不受自定义滑块影响()
    {
        await Assert.That(ThemePalette.Resolve(3, 300, 10, 0, 0)).IsEqualTo(ThemePalette.Preset(3));
    }
}
