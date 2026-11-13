using System.Net.Security;

namespace PCL.Tests;

public class TlsPolicyTests
{
    [Test]
    public async Task 验证通过时总是接受()
    {
        await Assert.That(TlsPolicy.Accept("example.com", SslPolicyErrors.None, false)).IsTrue();
        await Assert.That(TlsPolicy.Accept(null, SslPolicyErrors.None, true)).IsTrue();
    }

    [Test]
    public async Task 只对登录域名容忍证书链问题()
    {
        await Assert.That(TlsPolicy.Accept("user.auth.xboxlive.com", SslPolicyErrors.RemoteCertificateChainErrors, true)).IsTrue();
        await Assert.That(TlsPolicy.Accept("api.minecraftservices.com", SslPolicyErrors.RemoteCertificateChainErrors, true)).IsTrue();
        await Assert.That(TlsPolicy.Accept("API.MinecraftServices.com.", SslPolicyErrors.RemoteCertificateChainErrors, true)).IsTrue();
        // 关闭兼容时严格验证
        await Assert.That(TlsPolicy.Accept("api.minecraftservices.com", SslPolicyErrors.RemoteCertificateChainErrors, false)).IsFalse();
    }

    [Test]
    public async Task 名称里带登录域名的其他网站不算()
    {
        await Assert.That(TlsPolicy.Accept("xboxlive.evil.example", SslPolicyErrors.RemoteCertificateChainErrors, true)).IsFalse();
        await Assert.That(TlsPolicy.Accept("fakeminecraftservices.com", SslPolicyErrors.RemoteCertificateChainErrors, true)).IsFalse();
        await Assert.That(TlsPolicy.Accept("piston-meta.mojang.com", SslPolicyErrors.RemoteCertificateChainErrors, true)).IsFalse();
    }

    [Test]
    public async Task 名称不符或来源不明时拒绝()
    {
        await Assert.That(TlsPolicy.Accept("user.auth.xboxlive.com", SslPolicyErrors.RemoteCertificateNameMismatch, true)).IsFalse();
        await Assert.That(TlsPolicy.Accept("user.auth.xboxlive.com", SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors, true)).IsFalse();
        await Assert.That(TlsPolicy.Accept("user.auth.xboxlive.com", SslPolicyErrors.RemoteCertificateNotAvailable, true)).IsFalse();
        await Assert.That(TlsPolicy.Accept(null, SslPolicyErrors.RemoteCertificateChainErrors, true)).IsFalse();
    }
}
