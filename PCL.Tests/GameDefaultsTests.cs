namespace PCL.Tests;

public class GameDefaultsTests
{
    private static string Text(List<KeyValuePair<string, string>> Entries) => string.Join("|", Entries.Select(e => e.Key + ":" + e.Value));

    [Test]
    public async Task 全部不设置时不写入任何内容()
    {
        await Assert.That(GameDefaults.Entries(0, 0, 0, false).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 各项按选项写入()
    {
        await Assert.That(Text(GameDefaults.Entries(4, 2, 0, false))).IsEqualTo("renderDistance:16|soundCategory_master:0.5");
        await Assert.That(Text(GameDefaults.Entries(0, 0, 0, true))).IsEqualTo("pauseOnLostFocus:false");
    }

    [Test]
    public async Task 画质为每个版本的写法各写一行()
    {
        await Assert.That(Text(GameDefaults.Entries(0, 0, 2, false))).IsEqualTo("fancyGraphics:true|graphicsMode:1|graphicsPreset:\"fancy\"");
        await Assert.That(Text(GameDefaults.Entries(0, 0, 1, false))).IsEqualTo("fancyGraphics:false|graphicsMode:0|graphicsPreset:\"fast\"");
    }

    [Test]
    public async Task 超出范围的选项被忽略()
    {
        await Assert.That(GameDefaults.Entries(99, -1, 7, false).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 音量在任何语言环境下都用小数点()
    {
        var Saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            await Assert.That(Text(GameDefaults.Entries(0, 1, 0, false))).IsEqualTo("soundCategory_master:0.25");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = Saved; }
    }
}
