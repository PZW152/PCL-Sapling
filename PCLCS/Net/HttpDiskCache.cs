#nullable disable
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace PCLCS
{
    /// <summary>
    /// 简单的 HTTP 磁盘缓存，取代原先的 CacheCow。
    /// 只缓存不带 Authorization 的 GET 请求：
    /// 在 Cache-Control max-age 内直接使用缓存；过期后带上 ETag / Last-Modified 做条件请求，服务器返回 304 时复用缓存内容。
    /// 带登录凭据的响应（如正版登录）不会写入磁盘。
    /// 网络不可用时，可以用 <see cref="TryReadStale"/> 取回上一次成功的内容（即使已过期）。
    /// </summary>
    public static class HttpDiskCache
    {
        private sealed class Meta
        {
            public string Url;
            public string ETag;
            public string LastModified;
            /// <summary>缓存保持新鲜直到此时间（UTC Ticks）；0 表示每次都需要向服务器确认。</summary>
            public long FreshUntilUtc;
            /// <summary>正文长度与 SHA1：与正文文件对不上时（例如写入时断电）视为没有缓存（#6）。</summary>
            public long Length = -1;
            public string Sha1;
        }

        private static readonly object Lock = new object();
        /// <summary>缓存文件夹，以 \ 结尾。由宿主程序在启动时设置；未设置时不进行任何缓存。</summary>
        public static string Folder { get; set; }
        private static bool Enabled => !string.IsNullOrEmpty(Folder);

        private static string KeyOf(string Url)
        {
            using (var Sha = SHA1.Create())
                return string.Concat(Sha.ComputeHash(Encoding.UTF8.GetBytes(Url)).Select(b => b.ToString("x2")));
        }

        public static bool IsCacheable(HttpRequestMessage Request) =>
            Request.Method == HttpMethod.Get && Request.Headers.Authorization is null && !Request.Headers.Contains("Authorization") &&
            !Request.Headers.Contains("Cookie") && !LooksLikeCredentialUrl(Request.RequestUri);

        private static readonly Regex CredentialQuery = new Regex(@"(^|[?&])(access_token|token|key|api_key|apikey|sig|signature|code|password|session)=", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        /// <summary>
        /// 网址里带用户信息（user:pass@）或常见的凭据参数时，不写入磁盘（#6）。
        /// </summary>
        public static bool LooksLikeCredentialUrl(Uri Url) =>
            Url is not null && Url.IsAbsoluteUri && (!string.IsNullOrEmpty(Url.UserInfo) || CredentialQuery.IsMatch(Url.Query));

        private static string Sha1Of(byte[] Data)
        {
            using (var Sha = SHA1.Create())
                return string.Concat(Sha.ComputeHash(Data).Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// 读取正文并与元数据核对；对不上时返回 null。旧版本写入的缓存没有长度信息，照常使用。
        /// </summary>
        private static byte[] ReadBody(Meta Info, string BodyPath)
        {
            byte[] Body = File.ReadAllBytes(BodyPath);
            if (Info.Length >= 0 && (Body.Length != Info.Length || !string.Equals(Sha1Of(Body), Info.Sha1, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.Warn($"HTTP 缓存内容与记录不一致，已忽略：{Info.Url}");
                return null;
            }
            return Body;
        }

        /// <summary>
        /// 先写临时文件，再以系统的替换操作换上，避免其他线程或断电时读到写了一半的内容。
        /// </summary>
        private static void WriteAtomic(string Target, byte[] Data)
        {
            string Temp = Target + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            File.WriteAllBytes(Temp, Data);
            if (File.Exists(Target))
                File.Replace(Temp, Target, null, true);
            else
                File.Move(Temp, Target);
        }

        private static Meta ReadMeta(string Url, out string BodyPath)
        {
            string Key = KeyOf(Url);
            BodyPath = Folder + Key + ".bin";
            try
            {
                string MetaPath = Folder + Key + ".json";
                if (!File.Exists(MetaPath) || !File.Exists(BodyPath)) return null;
                var Result = JsonConvert.DeserializeObject<Meta>(File.ReadAllText(MetaPath, Encoding.UTF8));
                return Result?.Url == Url ? Result : null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取 HTTP 缓存信息失败");
                return null;
            }
        }

        /// <summary>
        /// 若缓存仍在有效期内，返回缓存内容；否则为请求附加条件请求头，并返回 null。
        /// </summary>
        public static byte[] TryUseOrPrepare(HttpRequestMessage Request, string Url)
        {
            if (!Enabled) return null;
            lock (Lock)
            {
                var Info = ReadMeta(Url, out string BodyPath);
                if (Info is null) return null;
                try
                {
                    if (Info.FreshUntilUtc > DateTime.UtcNow.Ticks && ReadBody(Info, BodyPath) is byte[] Fresh)
                        return Fresh;
                    if (!string.IsNullOrEmpty(Info.ETag))
                        Request.Headers.TryAddWithoutValidation("If-None-Match", Info.ETag);
                    if (!string.IsNullOrEmpty(Info.LastModified))
                        Request.Headers.TryAddWithoutValidation("If-Modified-Since", Info.LastModified);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "读取 HTTP 缓存失败");
                }
                return null;
            }
        }

        /// <summary>
        /// 服务器返回 304 时读取缓存内容，并刷新有效期。读取失败返回 null，调用方应改为不带条件头重新请求。
        /// </summary>
        public static byte[] ReadNotModified(HttpResponseMessage Response, string Url)
        {
            if (!Enabled) return null;
            lock (Lock)
            {
                var Info = ReadMeta(Url, out string BodyPath);
                if (Info is null) return null;
                try
                {
                    byte[] Body = ReadBody(Info, BodyPath);
                    if (Body is null) return null;
                    Info.FreshUntilUtc = GetFreshUntil(Response);
                    WriteAtomic(Folder + KeyOf(Url) + ".json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Info)));
                    return Body;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "读取 HTTP 缓存失败");
                    return null;
                }
            }
        }

        /// <summary>
        /// 保存 200 响应。没有任何缓存依据（ETag、Last-Modified、max-age）或要求 no-store 时不保存。
        /// </summary>
        public static void Store(HttpResponseMessage Response, string Url, byte[] Body)
        {
            if (!Enabled || Response.StatusCode != HttpStatusCode.OK) return;
            var Control = Response.Headers.CacheControl;
            if (Control is not null && Control.NoStore) return;
            string ETag = Response.Headers.ETag?.ToString();
            string LastModified = Response.Content?.Headers.LastModified?.ToString("R");
            long FreshUntil = GetFreshUntil(Response);
            if (string.IsNullOrEmpty(ETag) && string.IsNullOrEmpty(LastModified) && FreshUntil == 0) return;
            lock (Lock)
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    string Key = KeyOf(Url);
                    // 正文与元数据各自原子替换；元数据记录正文的长度与哈希，两者不一致时读取会忽略这条缓存
                    WriteAtomic(Folder + Key + ".bin", Body);
                    WriteAtomic(Folder + Key + ".json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Meta { Url = Url, ETag = ETag, LastModified = LastModified, FreshUntilUtc = FreshUntil, Length = Body.Length, Sha1 = Sha1Of(Body) })));
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "写入 HTTP 缓存失败");
                }
            }
        }

        /// <summary>
        /// 取回上一次成功保存的内容，不论是否已过期。用于网络不可用时的后备；没有缓存时返回 null。
        /// </summary>
        public static byte[] TryReadStale(string Url)
        {
            if (!Enabled) return null;
            lock (Lock)
            {
                var Info = ReadMeta(Url, out string BodyPath);
                if (Info is null) return null;
                try
                {
                    return ReadBody(Info, BodyPath);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "读取 HTTP 缓存失败");
                    return null;
                }
            }
        }

        /// <summary>
        /// 温和地清理缓存（#6）：删除很久没有用到的条目，总大小超出上限时从最久没用的开始删，并删除残留的临时文件。
        /// 离线时需要的版本列表等会经常被刷新，不会因此被删掉。返回删除的条目数。
        /// </summary>
        public static int Prune(long MaxBytes = 256L * 1024 * 1024, int MaxAgeDays = 90)
        {
            if (!Enabled || !Directory.Exists(Folder)) return 0;
            lock (Lock)
            {
                int Removed = 0;
                void Delete(string Path)
                {
                    try { if (File.Exists(Path)) File.Delete(Path); } catch (Exception) { }
                }
                foreach (string Temp in Directory.GetFiles(Folder, "*.tmp"))
                    if (File.GetLastWriteTimeUtc(Temp) < DateTime.UtcNow.AddHours(-1)) Delete(Temp);
                // 以元数据的修改时间作为“最近用到”：保存与 304 刷新都会更新它
                var Entries = Directory.GetFiles(Folder, "*.bin").Select(Bin =>
                {
                    string Json = System.IO.Path.ChangeExtension(Bin, ".json");
                    var Used = File.Exists(Json) ? File.GetLastWriteTimeUtc(Json) : File.GetLastWriteTimeUtc(Bin);
                    return (Bin, Json, Used, Size: new FileInfo(Bin).Length);
                }).OrderBy(e => e.Used).ToList();
                long Total = Entries.Sum(e => e.Size);
                var Oldest = DateTime.UtcNow.AddDays(-MaxAgeDays);
                foreach (var Entry in Entries)
                {
                    if (Entry.Used >= Oldest && Total <= MaxBytes) break;
                    Delete(Entry.Bin);
                    Delete(Entry.Json);
                    Total -= Entry.Size;
                    Removed++;
                }
                if (Removed > 0) Logger.Info($"已清理 {Removed} 条 HTTP 缓存");
                return Removed;
            }
        }

        private static long GetFreshUntil(HttpResponseMessage Response)
        {
            var Control = Response.Headers.CacheControl;
            if (Control is null || Control.NoCache || Control.MustRevalidate && Control.MaxAge is null) return 0;
            if (Control.MaxAge is TimeSpan MaxAge && MaxAge > TimeSpan.Zero)
            {
                var Age = Response.Headers.Age ?? TimeSpan.Zero;
                var Remaining = MaxAge - Age;
                return Remaining > TimeSpan.Zero ? DateTime.UtcNow.Add(Remaining).Ticks : 0;
            }
            return 0;
        }
    }
}
