namespace PCL.Tests;

public class PlayTimeTests
{
    [Test]
    public async Task 累计时间的显示()
    {
        await Assert.That(PlayTime.FormatTotal(TimeSpan.FromSeconds(59))).IsEqualTo("");
        await Assert.That(PlayTime.FormatTotal(TimeSpan.FromMinutes(3.9))).IsEqualTo("3 分钟");
        await Assert.That(PlayTime.FormatTotal(TimeSpan.FromMinutes(60))).IsEqualTo("1 小时");
        await Assert.That(PlayTime.FormatTotal(TimeSpan.FromMinutes(135))).IsEqualTo("2 小时 15 分钟");
        await Assert.That(PlayTime.FormatTotal(TimeSpan.FromMinutes(128 * 60 + 40))).IsEqualTo("128 小时");
    }

    [Test]
    public async Task 上次游玩的显示()
    {
        var Now = new DateTime(2026, 11, 12, 0, 30, 0);
        await Assert.That(PlayTime.FormatLastPlayed(new DateTime(2026, 11, 12, 0, 5, 0), Now)).IsEqualTo("今天");
        await Assert.That(PlayTime.FormatLastPlayed(new DateTime(2026, 11, 11, 23, 59, 0), Now)).IsEqualTo("昨天");
        await Assert.That(PlayTime.FormatLastPlayed(new DateTime(2026, 11, 7, 12, 0, 0), Now)).IsEqualTo("5 天前");
        await Assert.That(PlayTime.FormatLastPlayed(new DateTime(2026, 3, 14, 12, 0, 0), Now)).IsEqualTo("2026/3/14");
        await Assert.That(PlayTime.FormatLastPlayed(new DateTime(2026, 11, 13), Now)).IsEqualTo("今天"); // 时间被调回去
    }

    [Test]
    public async Task 组合说明()
    {
        var Now = new DateTime(2026, 11, 12, 12, 0, 0);
        await Assert.That(PlayTime.Describe(TimeSpan.Zero, null, Now)).IsEqualTo("");
        await Assert.That(PlayTime.Describe(TimeSpan.FromMinutes(135), Now.AddDays(-1), Now)).IsEqualTo("已游玩 2 小时 15 分钟，上次游玩：昨天");
        await Assert.That(PlayTime.Describe(TimeSpan.FromSeconds(20), Now, Now)).IsEqualTo("上次游玩：今天");
    }

    [Test]
    public async Task 累加时忽略异常的时长()
    {
        await Assert.That(PlayTime.AddSession(100, TimeSpan.FromSeconds(50))).IsEqualTo(150);
        await Assert.That(PlayTime.AddSession(100, TimeSpan.FromSeconds(-5))).IsEqualTo(100);
        await Assert.That(PlayTime.AddSession(100, TimeSpan.FromDays(40))).IsEqualTo(100);
    }
}
