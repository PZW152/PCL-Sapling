namespace PCL.Tests;

public class ChineseConverterTests
{
    private static string T(string Text) => ChineseConverter.ToTraditional(Text);

    [Test]
    public async Task 换成港澳台都看得懂的用词()
    {
        await Assert.That(T("默认文件夹")).IsEqualTo("預設資料夾");
        await Assert.That(T("网络服务器内存不足")).IsEqualTo("網路伺服器記憶體不足");
        await Assert.That(T("启动设置")).IsEqualTo("啟動設定");
        await Assert.That(T("正在下载 Minecraft")).IsEqualTo("正在下載 Minecraft");
        await Assert.That(T("登录微软账号")).IsEqualTo("登入微軟帳號");
    }

    [Test]
    public async Task 启动器用语()
    {
        await Assert.That(T("支持正版游戏！")).IsEqualTo("支持正版遊戲！");
        await Assert.That(T("赞助 PCL！")).IsEqualTo("贊助 PCL！");
        await Assert.That(T("整合包与数据包")).IsEqualTo("整合包與資料包");
        await Assert.That(T("在桌面创建快捷方式")).IsEqualTo("在桌面建立捷徑");
        await Assert.That(T("“引号”")).IsEqualTo("「引號」");
    }

    [Test]
    public async Task 路径与网址保持原样()
    {
        await Assert.That(T(@"游戏文件夹：C:\游戏\我的世界\.minecraft，已复制")).IsEqualTo(@"遊戲資料夾：C:\游戏\我的世界\.minecraft，已複製");
        await Assert.That(T("打开 https://example.com/下载 查看")).IsEqualTo("開啟 https://example.com/下载 檢視");
        await Assert.That(T(@"共享文件夹 \\Mac\Home\游戏 不可用")).IsEqualTo(@"共享資料夾 \\Mac\Home\游戏 不可用");
    }

    [Test]
    public async Task 没有中文时原样返回()
    {
        const string Text = "Minecraft 1.21.1 (Fabric)";
        await Assert.That(ReferenceEquals(T(Text), Text)).IsTrue();
        await Assert.That(T(null)).IsEqualTo("");
        await Assert.That(ChineseConverter.MayNeedConversion("abc")).IsFalse();
    }

    [Test]
    public async Task 已是繁体的文字不再改变()
    {
        foreach (var Text in new[] { "默认文件夹设置", "网络服务器内存", "下载完成后自动安装", "账号登录失败，请检查网络" })
        {
            string Once = T(Text);
            await Assert.That(T(Once)).IsEqualTo(Once);
        }
    }

    [Test]
    public async Task 根据系统语言选择()
    {
        await Assert.That(UiLanguage.ResolveTraditional(UiLanguage.FollowSystem, "zh-TW")).IsTrue();
        await Assert.That(UiLanguage.ResolveTraditional(UiLanguage.FollowSystem, "zh-HK")).IsTrue();
        await Assert.That(UiLanguage.ResolveTraditional(UiLanguage.FollowSystem, "zh-Hant-MO")).IsTrue();
        await Assert.That(UiLanguage.ResolveTraditional(UiLanguage.FollowSystem, "zh-CN")).IsFalse();
        await Assert.That(UiLanguage.ResolveTraditional(UiLanguage.FollowSystem, "en-US")).IsFalse();
        await Assert.That(UiLanguage.ResolveTraditional(UiLanguage.Traditional, "zh-CN")).IsTrue();
        await Assert.That(UiLanguage.ResolveTraditional(UiLanguage.Simplified, "zh-TW")).IsFalse();
    }
}
