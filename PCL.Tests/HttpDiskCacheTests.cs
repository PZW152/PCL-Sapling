using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PCL.Tests;

[NotInParallel] // 缓存文件夹是全局设置
public class HttpDiskCacheTests
{
    private static string NewFolder()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "PclHttpCacheTest-" + Guid.NewGuid().ToString("N")) + "\\";
        HttpDiskCache.Folder = Folder;
        return Folder;
    }

    private static HttpResponseMessage Response(HttpStatusCode Code, string ETag = null, int? MaxAgeSeconds = null, DateTimeOffset? LastModified = null, bool NoStore = false)
    {
        var Result = new HttpResponseMessage(Code) { Content = new ByteArrayContent(Array.Empty<byte>()) };
        if (ETag is not null) Result.Headers.ETag = new EntityTagHeaderValue(ETag);
        if (LastModified is not null) Result.Content.Headers.LastModified = LastModified;
        if (MaxAgeSeconds is not null || NoStore)
            Result.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = MaxAgeSeconds is null ? null : TimeSpan.FromSeconds(MaxAgeSeconds.Value), NoStore = NoStore };
        return Result;
    }

    private static HttpRequestMessage Get(string Url) => new(HttpMethod.Get, Url);
    private static byte[] Bytes(string Text) => Encoding.UTF8.GetBytes(Text);
    private static string Text(byte[] Body) => Body is null ? null : Encoding.UTF8.GetString(Body);

    [Test]
    public async Task 只缓存不带登录凭据的GET请求()
    {
        await Assert.That(HttpDiskCache.IsCacheable(Get("https://example.com/a"))).IsTrue();
        await Assert.That(HttpDiskCache.IsCacheable(new HttpRequestMessage(HttpMethod.Post, "https://example.com/a"))).IsFalse();
        var WithToken = Get("https://example.com/a");
        WithToken.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret");
        await Assert.That(HttpDiskCache.IsCacheable(WithToken)).IsFalse();
    }

    [Test]
    public async Task 有效期内直接使用缓存()
    {
        string Folder = NewFolder();
        try
        {
            const string Url = "https://example.com/manifest.json";
            HttpDiskCache.Store(Response(HttpStatusCode.OK, MaxAgeSeconds: 600), Url, Bytes("v1"));
            await Assert.That(Text(HttpDiskCache.TryUseOrPrepare(Get(Url), Url))).IsEqualTo("v1");
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 过期后附带条件请求头而不直接使用()
    {
        string Folder = NewFolder();
        try
        {
            const string Url = "https://example.com/list.json";
            var Modified = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
            HttpDiskCache.Store(Response(HttpStatusCode.OK, ETag: "\"abc\"", LastModified: Modified), Url, Bytes("body"));
            var Request = Get(Url);
            await Assert.That(HttpDiskCache.TryUseOrPrepare(Request, Url)).IsNull();
            await Assert.That(Request.Headers.GetValues("If-None-Match").Single()).IsEqualTo("\"abc\"");
            await Assert.That(Request.Headers.Contains("If-Modified-Since")).IsTrue();
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 服务器返回未修改时沿用缓存并刷新有效期()
    {
        string Folder = NewFolder();
        try
        {
            const string Url = "https://example.com/list.json";
            HttpDiskCache.Store(Response(HttpStatusCode.OK, ETag: "\"abc\""), Url, Bytes("body"));
            await Assert.That(Text(HttpDiskCache.ReadNotModified(Response(HttpStatusCode.NotModified, MaxAgeSeconds: 600), Url))).IsEqualTo("body");
            // 有效期已刷新：现在可以直接使用
            await Assert.That(Text(HttpDiskCache.TryUseOrPrepare(Get(Url), Url))).IsEqualTo("body");
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 网络不可用时可以取回过期的内容()
    {
        string Folder = NewFolder();
        try
        {
            const string Url = "https://example.com/versions.json";
            HttpDiskCache.Store(Response(HttpStatusCode.OK, ETag: "\"abc\""), Url, Bytes("old list"));
            await Assert.That(HttpDiskCache.TryUseOrPrepare(Get(Url), Url)).IsNull(); // 已过期，正常流程不会直接用
            await Assert.That(Text(HttpDiskCache.TryReadStale(Url))).IsEqualTo("old list");
            await Assert.That(HttpDiskCache.TryReadStale("https://example.com/never-fetched")).IsNull();
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 不该保存的响应不会落盘()
    {
        string Folder = NewFolder();
        try
        {
            HttpDiskCache.Store(Response(HttpStatusCode.OK), "https://example.com/no-validators", Bytes("x"));
            HttpDiskCache.Store(Response(HttpStatusCode.OK, ETag: "\"a\"", NoStore: true), "https://example.com/no-store", Bytes("x"));
            HttpDiskCache.Store(Response(HttpStatusCode.NotFound, ETag: "\"a\""), "https://example.com/missing", Bytes("x"));
            await Assert.That(HttpDiskCache.TryReadStale("https://example.com/no-validators")).IsNull();
            await Assert.That(HttpDiskCache.TryReadStale("https://example.com/no-store")).IsNull();
            await Assert.That(HttpDiskCache.TryReadStale("https://example.com/missing")).IsNull();
            await Assert.That(Directory.Exists(Folder) && Directory.EnumerateFiles(Folder).Any()).IsFalse();
        }
        finally { if (Directory.Exists(Folder)) Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 新的响应覆盖旧的内容()
    {
        string Folder = NewFolder();
        try
        {
            const string Url = "https://example.com/data";
            HttpDiskCache.Store(Response(HttpStatusCode.OK, ETag: "\"1\""), Url, Bytes("first"));
            HttpDiskCache.Store(Response(HttpStatusCode.OK, ETag: "\"2\""), Url, Bytes("second"));
            await Assert.That(Text(HttpDiskCache.TryReadStale(Url))).IsEqualTo("second");
            var Request = Get(Url);
            HttpDiskCache.TryUseOrPrepare(Request, Url);
            await Assert.That(Request.Headers.GetValues("If-None-Match").Single()).IsEqualTo("\"2\"");
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 未设置文件夹时不缓存()
    {
        HttpDiskCache.Folder = null;
        const string Url = "https://example.com/x";
        HttpDiskCache.Store(Response(HttpStatusCode.OK, MaxAgeSeconds: 600), Url, Bytes("x"));
        await Assert.That(HttpDiskCache.TryUseOrPrepare(Get(Url), Url)).IsNull();
        await Assert.That(HttpDiskCache.TryReadStale(Url)).IsNull();
    }

    [Test]
    public async Task 带Cookie或网址里带凭据时不缓存()
    {
        var WithCookie = Get("https://example.com/a");
        WithCookie.Headers.Add("Cookie", "session=abc");
        await Assert.That(HttpDiskCache.IsCacheable(WithCookie)).IsFalse();
        await Assert.That(HttpDiskCache.IsCacheable(Get("https://example.com/a?access_token=abc"))).IsFalse();
        await Assert.That(HttpDiskCache.IsCacheable(Get("https://example.com/a?page=2&key=abc"))).IsFalse();
        await Assert.That(HttpDiskCache.IsCacheable(Get("https://user:pass@example.com/a"))).IsFalse();
        // 普通参数照常缓存
        await Assert.That(HttpDiskCache.IsCacheable(Get("https://api.modrinth.com/v2/search?query=sodium&index=relevance"))).IsTrue();
    }

    [Test]
    public async Task 正文与记录不一致时忽略缓存()
    {
        string Folder = NewFolder();
        try
        {
            const string Url = "https://example.com/list";
            HttpDiskCache.Store(Response(HttpStatusCode.OK, ETag: "\"1\"", MaxAgeSeconds: 600), Url, Bytes("complete-content"));
            await Assert.That(Text(HttpDiskCache.TryReadStale(Url))).IsEqualTo("complete-content");
            // 模拟写到一半断电：正文被截断，元数据还是旧的
            string Body = Directory.GetFiles(Folder, "*.bin").Single();
            File.WriteAllText(Body, "compl");
            await Assert.That(HttpDiskCache.TryReadStale(Url)).IsNull();
            await Assert.That(HttpDiskCache.TryUseOrPrepare(Get(Url), Url)).IsNull();
        }
        finally { Directory.Delete(Folder, true); }
    }

    [Test]
    public async Task 清理很久没用与超出上限的条目()
    {
        string Folder = NewFolder();
        try
        {
            HttpDiskCache.Store(Response(HttpStatusCode.OK, ETag: "\"a\""), "https://example.com/old", Bytes("old-old-old"));
            HttpDiskCache.Store(Response(HttpStatusCode.OK, ETag: "\"b\""), "https://example.com/new", Bytes("new"));
            // 让第一条看起来 100 天没用过
            foreach (var Json in Directory.GetFiles(Folder, "*.json"))
                if (File.ReadAllText(Json).Contains("/old"))
                    File.SetLastWriteTimeUtc(Json, DateTime.UtcNow.AddDays(-100));
            File.WriteAllText(Path.Combine(Folder, "stale.tmp"), "x");
            File.SetLastWriteTimeUtc(Path.Combine(Folder, "stale.tmp"), DateTime.UtcNow.AddDays(-1));
            await Assert.That(HttpDiskCache.Prune()).IsEqualTo(1);
            await Assert.That(HttpDiskCache.TryReadStale("https://example.com/old")).IsNull();
            await Assert.That(Text(HttpDiskCache.TryReadStale("https://example.com/new"))).IsEqualTo("new");
            await Assert.That(File.Exists(Path.Combine(Folder, "stale.tmp"))).IsFalse();
            // 超出总大小上限时也会删除
            await Assert.That(HttpDiskCache.Prune(MaxBytes: 0)).IsEqualTo(1);
            await Assert.That(HttpDiskCache.TryReadStale("https://example.com/new")).IsNull();
        }
        finally { Directory.Delete(Folder, true); }
    }
}
