namespace PCL.Tests;

public class SettingsTransferTests
{
    [Test]
    public async Task 只导出偏好类设置()
    {
        await Assert.That(SettingsTransfer.IsTransferable("UiLauncherTheme", false, false)).IsTrue();
        await Assert.That(SettingsTransfer.IsTransferable("LaunchRamCustom", false, false)).IsTrue();
        await Assert.That(SettingsTransfer.IsTransferable("ToolDownloadThread", false, false)).IsTrue();
        // 登录信息、加密内容、缓存、系统记录不导出
        await Assert.That(SettingsTransfer.IsTransferable("LoginLegacyName", true, false)).IsFalse();
        await Assert.That(SettingsTransfer.IsTransferable("CacheMsV2Access", true, false)).IsFalse();
        await Assert.That(SettingsTransfer.IsTransferable("SystemCount", false, false)).IsFalse();
        await Assert.That(SettingsTransfer.IsTransferable("HintBuy", false, false)).IsFalse();
        // 本机路径与版本独立设置不导出
        await Assert.That(SettingsTransfer.IsTransferable("LaunchFolders", false, false)).IsFalse();
        await Assert.That(SettingsTransfer.IsTransferable("LaunchArgumentJavaSelect", false, false)).IsFalse();
        await Assert.That(SettingsTransfer.IsTransferable("VersionRamCustom", false, true)).IsFalse();
        // 前缀必须是完整的单词
        await Assert.That(SettingsTransfer.IsTransferable("Uid", false, false)).IsFalse();
        await Assert.That(SettingsTransfer.IsTransferable("Toolbox", false, false)).IsFalse();
    }

    [Test]
    public async Task 导出后可以原样读回()
    {
        var Values = new Dictionary<string, object>
        {
            ["UiLauncherTheme"] = 3,
            ["UiBackgroundColorful"] = false,
            ["LaunchAdvanceJvm"] = "-XX:+UseG1GC -Dfoo=\"a b\"",
            ["UiLogoText"] = null,
        };
        string Text = SettingsTransfer.Export(Values, new DateTime(2026, 11, 12, 13, 0, 0));
        var Parsed = SettingsTransfer.Parse(Text);
        await Assert.That(Parsed.Count).IsEqualTo(3);
        await Assert.That(Parsed["UiLauncherTheme"].ToObject<int>()).IsEqualTo(3);
        await Assert.That(Parsed["UiBackgroundColorful"].ToObject<bool>()).IsFalse();
        await Assert.That(Parsed["LaunchAdvanceJvm"].ToString()).IsEqualTo("-XX:+UseG1GC -Dfoo=\"a b\"");
        await Assert.That(Text.Contains("2026-11-12 13:00:00")).IsTrue();
    }

    [Test]
    public async Task 拒绝不是设置文件的内容()
    {
        await Assert.That(() => SettingsTransfer.Parse("not json")).Throws<FormatException>();
        await Assert.That(() => SettingsTransfer.Parse("{\"files\":[]}")).Throws<FormatException>();
        await Assert.That(() => SettingsTransfer.Parse("{\"Format\":\"PCL152 Settings\",\"Version\":99,\"Settings\":{}}")).Throws<FormatException>();
    }
}
