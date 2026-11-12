namespace PCL.Tests;

public class ShellLinkTests
{
    [Test]
    public async Task 创建并读回快捷方式()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PCLTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        try
        {
            string Link = Path.Combine(Folder, ShellLink.SafeFileName("1.21.1: 生存?") + ".lnk");
            string Target = Path.Combine(Environment.SystemDirectory, "notepad.exe");
            string Args = LaunchRequest.BuildArguments(@"D:\游戏 文件夹\.minecraft\versions\Fabric 1.21.1\");
            ShellLink.Create(Link, new ShellLink.Info(Target, Args, Folder, "启动 Fabric 1.21.1", Target));
            await Assert.That(File.Exists(Link)).IsTrue();
            var Read = ShellLink.Read(Link);
            await Assert.That(Read.Target.ToLowerInvariant()).IsEqualTo(Target.ToLowerInvariant()); // 系统会规范路径的大小写
            await Assert.That(Read.Arguments).IsEqualTo(Args);
            await Assert.That(Read.WorkingDirectory).IsEqualTo(Folder);
            await Assert.That(Read.Description).IsEqualTo("启动 Fabric 1.21.1");
        }
        finally
        {
            Directory.Delete(Folder, true);
        }
    }

    [Test]
    public async Task 命令行参数往返()
    {
        string Instance = @"D:\游戏 文件夹\.minecraft\versions\Fabric 1.21.1\";
        string Args = LaunchRequest.BuildArguments(Instance);
        await Assert.That(Args).IsEqualTo("--launch \"D:\\游戏 文件夹\\.minecraft\\versions\\Fabric 1.21.1\"");
        // Windows 按通常的规则拆分命令行后，取回同一个文件夹
        string[] Split = ["--launch", @"D:\游戏 文件夹\.minecraft\versions\Fabric 1.21.1"];
        await Assert.That(LaunchRequest.Parse(Split)).IsEqualTo(Instance);
        await Assert.That(LaunchRequest.Parse(["--gpu", "x"])).IsNull();
        await Assert.That(LaunchRequest.Parse(["--launch"])).IsNull();
    }

    [Test]
    public async Task 文件名中的非法字符()
    {
        await Assert.That(ShellLink.SafeFileName("a:b?c*")).IsEqualTo("a_b_c_");
        await Assert.That(ShellLink.SafeFileName("...")).IsEqualTo("_");
    }
}
