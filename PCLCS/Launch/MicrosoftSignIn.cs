namespace PCLCS;

/// <summary>
/// 微软登录（设备代码流程）的辅助判断。
/// </summary>
public static class MicrosoftSignIn {

    /// <summary>
    /// 微软登录页的网址，用户要在这个页面输入代码。内嵌的二维码就是这个网址。
    /// </summary>
    public const string SignInPage = "https://www.microsoft.com/link";

    /// <summary>
    /// 只有登录页是这个固定网址时才显示二维码。
    /// 有些启动器用网址参数直接带入代码，但微软没有公开这个做法，有时会拒绝，所以二维码只打开登录页。
    /// </summary>
    public static bool CanShowQr(string? verificationUri) =>
        (verificationUri ?? "").Trim().TrimEnd('/').Equals(SignInPage, StringComparison.OrdinalIgnoreCase);

}
