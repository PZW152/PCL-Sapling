namespace PCLCS;

/// <summary>一次游戏进程的日志。首页和崩溃分析读取同一份已打码的最近输出。</summary>
public sealed class GameSessionLog(string? onlineAccessToken) {

    private const int MaxLines = 500;
    private readonly Queue<string> lines = new(MaxLines);
    private IReadOnlyList<string>? snapshot;

    /// <summary>保存一行输出，并返回可以写入启动器日志的文本。</summary>
    public string Append(string line) {
        string scrubbed = ScrubText(line);
        lock (lines) {
            if (lines.Count == MaxLines) lines.Dequeue();
            lines.Enqueue(scrubbed);
            snapshot = null;
        }
        return scrubbed;
    }

    /// <summary>按输出顺序返回只读快照。没有新增输出时，重复读取返回同一个快照。</summary>
    public IReadOnlyList<string> Snapshot() {
        lock (lines) {
            return snapshot ??= Array.AsReadOnly(lines.ToArray());
        }
    }

    /// <summary>导出错误报告时，文件可能来自磁盘，也要遮盖本次启动的令牌。</summary>
    public string ScrubText(string text, char maskChar = '*') => SecretScrubber.ScrubText(text, onlineAccessToken, maskChar);

    /// <summary>退出时使用本次启动的令牌清理游戏写到磁盘的日志。</summary>
    public bool ScrubFile(string path) => SecretScrubber.ScrubFile(path, onlineAccessToken);
}
