using System.Net.Security;

namespace PCLCS;

/// <summary>
/// 证书验证的例外范围（#1）。
/// PCL 原作者在上游 #3018、#5879 中说明：部分网络环境（旧版 Windows 缺少新的根证书、会改写证书的代理）中，
/// 正版登录接口的证书链无法验证，严格验证会让这些玩家无法登录，所以对登录接口放宽验证。
/// 这里把放宽限制到最小：只针对 Xbox Live 与 Minecraft Services 的域名，只容忍证书链问题；
/// 证书的名称与访问的地址不符（可能是被冒充）时一律拒绝，来源不明的请求也拒绝。
/// </summary>
public static class TlsPolicy {
    /// <summary>
    /// 允许放宽证书链验证的域名（及其子域名），精确比较，不用“包含”。
    /// </summary>
    public static readonly string[] LoginDomains = ["xboxlive.com", "minecraftservices.com"];

    /// <summary>
    /// 是否接受这次证书验证的结果。
    /// </summary>
    /// <param name="host">请求的主机名；来源不明时为 null。</param>
    /// <param name="errors">系统验证得到的错误。</param>
    /// <param name="allowLoginChainErrors">是否启用对登录接口证书链问题的兼容。</param>
    public static bool Accept(string? host, SslPolicyErrors errors, bool allowLoginChainErrors) {
        if (errors == SslPolicyErrors.None) return true;
        if (!allowLoginChainErrors || string.IsNullOrEmpty(host)) return false;
        if (errors != SslPolicyErrors.RemoteCertificateChainErrors) return false; // 名称不符、没有证书都不能放过
        return IsLoginHost(host!);
    }

    public static bool IsLoginHost(string host) {
        string normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        return LoginDomains.Any(domain => normalized == domain || normalized.EndsWith("." + domain, StringComparison.Ordinal));
    }
}
