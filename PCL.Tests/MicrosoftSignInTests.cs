namespace PCL.Tests;

public class MicrosoftSignInTests
{
    [Test]
    public async Task 只在登录页是微软的固定网址时显示二维码()
    {
        await Assert.That(MicrosoftSignIn.CanShowQr("https://www.microsoft.com/link")).IsTrue();
        await Assert.That(MicrosoftSignIn.CanShowQr("https://www.microsoft.com/link/")).IsTrue();
        await Assert.That(MicrosoftSignIn.CanShowQr(" HTTPS://WWW.MICROSOFT.COM/LINK ")).IsTrue();
        await Assert.That(MicrosoftSignIn.CanShowQr("https://microsoft.com/devicelogin")).IsFalse();
        await Assert.That(MicrosoftSignIn.CanShowQr("https://www.microsoft.com/link?otc=ABCD")).IsFalse();
        await Assert.That(MicrosoftSignIn.CanShowQr(null)).IsFalse();
    }
}
