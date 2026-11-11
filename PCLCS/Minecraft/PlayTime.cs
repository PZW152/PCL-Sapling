namespace PCLCS;

/// <summary>
/// 游玩时间与上次游玩时间的显示文本。
/// </summary>
public static class PlayTime {

    /// <summary>
    /// 累计游玩时间，例如 “3 分钟”“2 小时 15 分钟”“128 小时”。不足 1 分钟时返回空字符串。
    /// </summary>
    public static string FormatTotal(TimeSpan total) {
        long minutes = (long) total.TotalMinutes;
        if (minutes < 1) return "";
        if (minutes < 60) return $"{minutes} 分钟";
        long hours = minutes / 60;
        long rest = minutes % 60;
        // 超过 100 小时后不再显示分钟
        return hours >= 100 || rest == 0 ? $"{hours} 小时" : $"{hours} 小时 {rest} 分钟";
    }

    /// <summary>
    /// 上次游玩的时间，例如 “今天”“昨天”“5 天前”“2026/3/14”。
    /// </summary>
    public static string FormatLastPlayed(DateTime lastPlayed, DateTime now) {
        int days = (now.Date - lastPlayed.Date).Days;
        if (days <= 0) return "今天";
        if (days == 1) return "昨天";
        if (days < 30) return $"{days} 天前";
        return lastPlayed.ToString("yyyy/M/d", System.Globalization.CultureInfo.InvariantCulture); // 固定用公历，不随系统日历（如民国历）变化
    }

    /// <summary>
    /// 组合成一行说明，例如 “已游玩 2 小时 15 分钟，上次游玩：昨天”。从未游玩时返回空字符串。
    /// </summary>
    public static string Describe(TimeSpan total, DateTime? lastPlayed, DateTime now) {
        string totalText = FormatTotal(total);
        List<string> parts = [];
        if (totalText != "") parts.Add("已游玩 " + totalText);
        if (lastPlayed is not null) parts.Add("上次游玩：" + FormatLastPlayed(lastPlayed.Value, now));
        return string.Join("，", parts);
    }

    /// <summary>
    /// 把一次游戏的时长计入累计值。时长为负（系统时间被调整）或超过 30 天（明显异常）时忽略。
    /// </summary>
    public static long AddSession(long totalSeconds, TimeSpan session) {
        if (session <= TimeSpan.Zero || session > TimeSpan.FromDays(30)) return totalSeconds;
        return totalSeconds + (long) session.TotalSeconds;
    }

}
