#nullable disable
using System.Net.Http;
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
            Request.Method == HttpMethod.Get && Request.Headers.Authorization is null && !Request.Headers.Contains("Authorization");

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
                    if (Info.FreshUntilUtc > DateTime.UtcNow.Ticks)
                        return File.ReadAllBytes(BodyPath);
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
                    byte[] Body = File.ReadAllBytes(BodyPath);
                    Info.FreshUntilUtc = GetFreshUntil(Response);
                    File.WriteAllText(Folder + KeyOf(Url) + ".json", JsonConvert.SerializeObject(Info), Encoding.UTF8);
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
                    // 先写临时文件再替换，避免其他线程读到写了一半的内容
                    string Temp = Folder + Key + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    File.WriteAllBytes(Temp, Body);
                    string BodyPath = Folder + Key + ".bin";
                    if (File.Exists(BodyPath)) File.Delete(BodyPath);
                    File.Move(Temp, BodyPath);
                    File.WriteAllText(Folder + Key + ".json", JsonConvert.SerializeObject(new Meta { Url = Url, ETag = ETag, LastModified = LastModified, FreshUntilUtc = FreshUntil }), Encoding.UTF8);
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
                if (ReadMeta(Url, out string BodyPath) is null) return null;
                try
                {
                    return File.ReadAllBytes(BodyPath);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "读取 HTTP 缓存失败");
                    return null;
                }
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
