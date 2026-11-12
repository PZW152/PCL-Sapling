namespace PCL.Tests;

public class ThemePaletteTests
{
    [Test]
    public async Task 预设主题都是蓝色且互不相同()
    {
        // PCL 的使用指南要求界面主色相必须为蓝色
        var Presets = new[] { 0, 1, 2, 3, 4 }.Select(ThemePalette.Preset).ToList();
        foreach (var Args in Presets)
            await Assert.That(Args.Hue >= ThemePalette.BlueHueMin && Args.Hue <= ThemePalette.BlueHueMax).IsTrue();
        await Assert.That(Presets.Distinct().Count()).IsEqualTo(5);
        await Assert.That(Presets.Select(a => a.Hue).Distinct().Count()).IsEqualTo(5);
    }

    [Test]
    public async Task 默认主题与原先的龙猫蓝一致()
    {
        await Assert.That(ThemePalette.Preset(0)).IsEqualTo(new ThemeArgs(210, 85, 0, 0));
        await Assert.That(ThemePalette.Preset(99)).IsEqualTo(ThemePalette.Preset(0)); // 未知编号回到默认
    }

    [Test]
    public async Task 自定义主题只能在蓝色范围内调整()
    {
        // 滑块的默认值：色调 10、饱和度 80、渐变 20、亮度 20，即默认的龙猫蓝色相且不渐变
        await Assert.That(ThemePalette.Resolve(14, 10, 80, 20, 20)).IsEqualTo(new ThemeArgs(210, 80, 0, 0));
        await Assert.That(ThemePalette.Resolve(14, 0, 40, 40, 35)).IsEqualTo(new ThemeArgs(200, 40, 15, 20));
        // 超出范围的旧值（例如从原版 PCL 导入的 180 / 300）会被限制在蓝色范围内
        await Assert.That(ThemePalette.Resolve(14, 300, -5, 999, -1)).IsEqualTo(new ThemeArgs(235, 0, -20, 20));
        for (int Hue = -10; Hue <= 400; Hue += 7)
            for (int Delta = -10; Delta <= 200; Delta += 9)
            {
                var Args = ThemePalette.Resolve(14, Hue, 50, Delta, 20);
                int TopHue = Args.Hue + Args.TopbarHueDelta;
                await Assert.That(TopHue >= ThemePalette.BlueHueMin - ThemePalette.MaxTopbarDelta && TopHue <= ThemePalette.BlueHueMax + ThemePalette.MaxTopbarDelta).IsTrue();
            }
    }

    [Test]
    public async Task 预设主题不受自定义滑块影响()
    {
        await Assert.That(ThemePalette.Resolve(3, 300, 10, 0, 0)).IsEqualTo(ThemePalette.Preset(3));
    }
}
