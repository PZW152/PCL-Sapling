namespace PCL.Tests;

public class GameSessionLogTests
{
    [Test]
    public async Task 两次启动分别打码各自的输出和磁盘日志()
    {
        const string FirstToken = "first-session-credential";
        const string SecondToken = "second-session-credential";
        var First = new GameSessionLog(FirstToken);
        var Second = new GameSessionLog(SecondToken);
        string Line = $"登录参数 {FirstToken}，重复 {FirstToken}";
        string Expected = Line.Replace(FirstToken, SecretScrubber.Mask(FirstToken));

        await Assert.That(Second.Append(SecondToken)).IsEqualTo(SecretScrubber.Mask(SecondToken));
        await Assert.That(First.Append(Line)).IsEqualTo(Expected);
        await Assert.That(First.Snapshot().Single()).IsEqualTo(Expected);
        await Assert.That(Second.Snapshot().Single()).IsEqualTo(SecretScrubber.Mask(SecondToken));

        string Path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllText(Path, Line);
            await Assert.That(Second.ScrubFile(Path)).IsFalse();
            await Assert.That(First.ScrubFile(Path)).IsTrue();
            await Assert.That(File.ReadAllText(Path)).IsEqualTo(Expected);
        }
        finally { File.Delete(Path); }
    }

    [Test]
    public async Task 离线日志保留玩家标识和空行()
    {
        const string PlayerId = "00112233445566778899aabbccddeeff";
        var Log = new GameSessionLog(null);
        await Assert.That(Log.Snapshot()).IsEmpty();
        Log.Append(PlayerId);
        Log.Append("");
        await Assert.That(Log.Snapshot().SequenceEqual(new[] { PlayerId, "" })).IsTrue();
    }

    [Test]
    public async Task 满五百行后末行重复也要刷新且旧快照保持原样()
    {
        var Log = new GameSessionLog(null);
        for (int i = 0; i < 499; i++) Log.Append(i.ToString());
        Log.Append("保存完成");
        var Before = Log.Snapshot();
        await Assert.That(ReferenceEquals(Before, Log.Snapshot())).IsTrue();

        Log.Append("新的诊断信息");
        Log.Append("保存完成");
        var After = Log.Snapshot();
        await Assert.That(ReferenceEquals(Before, After)).IsFalse();
        await Assert.That(Before.Count).IsEqualTo(500);
        await Assert.That(After.Count).IsEqualTo(500);
        await Assert.That(Before[0]).IsEqualTo("0");
        await Assert.That(After[0]).IsEqualTo("2");
        await Assert.That(Before[498]).IsEqualTo("498");
        await Assert.That(After[498]).IsEqualTo("新的诊断信息");
        await Assert.That(After[499]).IsEqualTo(Before[499]);
        await Assert.That(ReferenceEquals(After, Log.Snapshot())).IsTrue();
    }
}
