using System.Text;

namespace PCL.Tests;

public class SecretScrubberTests
{
    private const string Token = "eyJhbGciOiJIUzI1NiJ9.payload-of-the-token.signature123";

    private static string TempFile(byte[] Content)
    {
        string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PclScrub-" + Guid.NewGuid().ToString("N") + ".log");
        File.WriteAllBytes(Path, Content);
        return Path;
    }

    [Test]
    public async Task 打码保留首尾各五个字符()
    {
        await Assert.That(SecretScrubber.Mask("0123456789abcdef")).IsEqualTo("01234******bcdef");
        await Assert.That(SecretScrubber.Mask("short")).IsEqualTo("*****");
    }

    [Test]
    public async Task 文件中的令牌被打码且其余内容不变()
    {
        string Before = "[main/INFO]: 設定使用者 龙腾猫跃\r\n--accessToken " + Token + " --userType msa\r\n結束\n";
        string Path = TempFile(Encoding.UTF8.GetBytes(Before));
        try
        {
            await Assert.That(SecretScrubber.ScrubFile(Path, Token)).IsTrue();
            string After = Encoding.UTF8.GetString(File.ReadAllBytes(Path));
            await Assert.That(After.Contains(Token)).IsFalse();
            await Assert.That(After).IsEqualTo(Before.Replace(Token, SecretScrubber.Mask(Token)));
        }
        finally { File.Delete(Path); }
    }

    [Test]
    public async Task 非UTF8编码的日志其余字节原样保留()
    {
        byte[] Prefix = Encoding.GetEncoding(936).GetBytes("启动参数：");
        byte[] Content = Prefix.Concat(Encoding.ASCII.GetBytes(Token + "\n")).ToArray();
        string Path = TempFile(Content);
        try
        {
            await Assert.That(SecretScrubber.ScrubFile(Path, Token)).IsTrue();
            byte[] After = File.ReadAllBytes(Path);
            await Assert.That(After.Length).IsEqualTo(Content.Length);
            await Assert.That(After.Take(Prefix.Length).SequenceEqual(Prefix)).IsTrue();
        }
        finally { File.Delete(Path); }
    }

    [Test]
    public async Task 不含令牌或文件不存在时不修改()
    {
        string Path = TempFile(Encoding.UTF8.GetBytes("nothing here"));
        try
        {
            var Time = File.GetLastWriteTimeUtc(Path);
            await Assert.That(SecretScrubber.ScrubFile(Path, Token)).IsFalse();
            await Assert.That(File.GetLastWriteTimeUtc(Path)).IsEqualTo(Time);
            await Assert.That(SecretScrubber.ScrubFile(Path + ".missing", Token)).IsFalse();
            await Assert.That(SecretScrubber.ScrubFile(Path, null)).IsFalse();
            await Assert.That(SecretScrubber.ScrubFile(Path, "short")).IsFalse();
        }
        finally { File.Delete(Path); }
    }
}
