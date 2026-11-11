namespace PCLCS;

/// <summary>
/// 把登录令牌等敏感内容从文本文件中抹去。
/// 游戏自己的日志（logs\latest.log、logs\debug.log）会原样记录启动参数，其中包含登录令牌；
/// 玩家常把这些日志发给别人求助，所以在游戏退出后处理一次。
/// </summary>
public static class SecretScrubber {

    /// <summary>
    /// 打码后的形式：保留首尾各 5 个字符，中间替换为 *。
    /// </summary>
    public static string Mask(string secret) =>
        secret.Length < 10 ? new string('*', secret.Length) : secret.Substring(0, 5) + new string('*', secret.Length - 10) + secret.Substring(secret.Length - 5);

    /// <summary>
    /// 若文件中含有该敏感内容，把它替换为打码后的形式。文件的其余字节保持不变（不改变编码与换行）。
    /// </summary>
    /// <returns>是否修改了文件。文件不存在、不含该内容时返回 <see langword="false"/>。</returns>
    public static bool ScrubFile(string filePath, string? secret) {
        if (secret is null || secret.Length < 10 || !File.Exists(filePath)) return false;
        // 按单字节编码读写：令牌是 ASCII，这样无论日志是 UTF-8 还是 ANSI，其余内容都原样保留
        Encoding raw = Encoding.GetEncoding(28591);
        string text;
        using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (StreamReader reader = new(stream, raw, detectEncodingFromByteOrderMarks: false))
            text = reader.ReadToEnd();
        if (text.IndexOf(secret, StringComparison.Ordinal) < 0) return false;
        File.WriteAllBytes(filePath, raw.GetBytes(text.Replace(secret, Mask(secret))));
        return true;
    }

}
