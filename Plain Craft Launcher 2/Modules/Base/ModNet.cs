using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf.Extensions;

namespace PCL
{

    public static class ModNet
    {

        #region 网络请求

        /// <summary>
        /// 发送一次网络请求并获取返回内容。
        /// </summary>
        public static string NetRequestByClient(string Url, HttpMethod Method = null, object Content = null, string ContentType = null, string Accept = "*/*", int Timeout = 25000, string[,] Headers = null, bool RequireJson = false, Encoding Encoding = null, bool SimulateBrowserHeaders = false, bool MakeLog = true)
        {
            EnsureNotUiThread(Url);
            return NetRequestByClientAsync(Url, Method, Content, ContentType, Accept, Timeout, Headers, RequireJson, Encoding, SimulateBrowserHeaders, MakeLog).GetAwaiter().GetResult();
        }
        /// <summary>
        /// 发送一次网络请求并获取返回内容。
        /// </summary>
        public static async Task<string> NetRequestByClientAsync(string Url, HttpMethod Method = null, object Content = null, string ContentType = null, string Accept = "*/*", int Timeout = 25000, string[,] Headers = null, bool RequireJson = false, Encoding Encoding = null, bool SimulateBrowserHeaders = false, bool MakeLog = true, CancellationToken Cancel = default)
        {
            if (Method is null)
                Method = HttpMethod.Get;
            if (MakeLog)
                Logger.Info($"发起网络请求（{Method.Method}，{Url}），最大超时 {Timeout}");
            try
            {
                var HeaderDictionary = (Headers ?? (new string[,] { })).ToDictionary();
                if (ContentType is not null)
                    HeaderDictionary["Content-Type"] = ContentType;
                if (Accept is not null)
                    HeaderDictionary["Accept"] = Accept;
                string Result = (await SendRequestAsync(Url, Method, Content, HeaderDictionary, SimulateBrowserHeaders, Timeout, Encoding, Cancel).ConfigureAwait(false)).GetString(Encoding);
                // 检查结果是否为完整 JSON（在 #6683 中可能返回空，在被 GFW 截断时可能不完整）
                // 在此处检查并报错，可以让 NetRequestByClientRetry 等方法进行重试
                if (RequireJson)
                {
                    Result = Result.Trim((ModBase.CrLf + " ").ToCharArray()); // Authlib-Injector 的结果会在末尾附带一个空行
                    if (!(Result.StartsWithF("{") && Result.EndsWithF("}")) && !(Result.StartsWithF("[") && Result.EndsWithF("]")))
                    {
                        throw new FormatException("返回结果并非 JSON 格式：" + (Result.Length > 2000 ? Result.Substring(0, 1000) + "..." + Result.Substring(Result.Length - 1000) : Result));
                    }
                }
                return Result;
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (MakeLog && !ex.IsCanceled())
                    Logger.Warn(ex, $"网络请求失败（{Method.Method}，{Url}）");
                throw;
            }
        }
        /// <summary>
        /// 发送网络请求并获取返回内容。
        /// 会进行至多 45 秒 3 次的尝试，允许最长 30s 的超时。
        /// </summary>
        /// <param name="BackupUrl">保留的参数，目前不会使用。</param>
        public static string NetRequestByClientRetry(string Url, HttpMethod Method = null, string BackupUrl = null, object Content = null, string ContentType = null, string Accept = "*/*", string[,] Headers = null, bool RequireJson = false, Encoding Encoding = null, bool SimulateBrowserHeaders = false)
        {
            EnsureNotUiThread(Url);
            return NetRequestByClientRetryAsync(Url, Method, Content, ContentType, Accept, Headers, RequireJson, Encoding, SimulateBrowserHeaders).GetAwaiter().GetResult();
        }
        /// <summary>
        /// 发送网络请求并获取返回内容。
        /// 依次以 10s、30s、4s 的超时尝试；第三次只在前两次共耗时 5.5s 以上时才进行（即网络慢而非直接拒绝）。
        /// 403、404 不重试；429 会等待 10s 再试。
        /// </summary>
        public static async Task<string> NetRequestByClientRetryAsync(string Url, HttpMethod Method = null, object Content = null, string ContentType = null, string Accept = "*/*", string[,] Headers = null, bool RequireJson = false, Encoding Encoding = null, bool SimulateBrowserHeaders = false, CancellationToken Cancel = default)
        {
            int[] Timeouts = [10000, 30000, 4000];
            Exception FirstException = null;
            long StartTime = ModBase.GetTimeMs();
            for (int RetryCount = 0; ; RetryCount++)
            {
                try
                {
                    if (RetryCount > 0)
                        await Task.Delay(500, Cancel).ConfigureAwait(false);
                    return await NetRequestByClientAsync(Url, Method, Content, ContentType, Accept, Timeouts[RetryCount], Headers, RequireJson, Encoding, SimulateBrowserHeaders, Cancel: Cancel).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ex.ThrowIfCanceled();
                    if (ex is HttpRequestCodeException CodeEx)
                    {
                        if (CodeEx.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                            throw;
                        if ((int)CodeEx.StatusCode == 429)
                            await Task.Delay(10000, Cancel).ConfigureAwait(false); // Too Many Requests
                    }
                    FirstException ??= ex;
                    if (RetryCount >= 2)
                        throw;
                    // 前两次失败得很快（通常是直接被拒绝），再试也没有意义
                    if (RetryCount == 1 && ModBase.GetTimeMs() - StartTime <= 5500L)
                        throw FirstException;
                }
            }
        }
        /// <summary>
        /// 同时发起数个相同的网络请求（每个比上一个晚一些开始），返回最先成功的结果。
        /// 所有请求都失败时，抛出最后一个失败的原因。
        /// </summary>
        public static string NetRequestByClientMultiple(string Url, HttpMethod Method = null, int ThreadCount = 3, object Content = null, string ContentType = null, string Accept = "*/*", string[,] Headers = null, bool RequireJson = false, int Timeout = 25000, Encoding Encoding = null, bool SimulateBrowserHeaders = false)
        {
            EnsureNotUiThread(Url);
            return NetRequestByClientMultipleAsync(Url, Method, ThreadCount, Content, ContentType, Accept, Headers, RequireJson, Timeout, Encoding, SimulateBrowserHeaders).GetAwaiter().GetResult();
        }
        /// <summary>
        /// 同时发起数个相同的网络请求（第 i 个在第 i-1 个开始后 i×250ms 开始），返回最先成功的结果。
        /// 所有请求都失败时，抛出最后一个失败的原因。
        /// </summary>
        public static async Task<string> NetRequestByClientMultipleAsync(string Url, HttpMethod Method = null, int Count = 3, object Content = null, string ContentType = null, string Accept = "*/*", string[,] Headers = null, bool RequireJson = false, int Timeout = 25000, Encoding Encoding = null, bool SimulateBrowserHeaders = false, CancellationToken Cancel = default)
        {
            using var Others = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
            var Running = new List<Task<string>>();
            Task NextStart = Task.CompletedTask; // 到时间开始下一个请求；所有请求都已开始时为 null
            int Started = 0;
            Exception LastException = null;
            while (true)
            {
                if (NextStart is not null && NextStart.IsCompleted)
                {
                    Started++;
                    Running.Add(NetRequestByClientAsync(Url, Method, Content, ContentType, Accept, Timeout, Headers, RequireJson, Encoding, SimulateBrowserHeaders, Cancel: Others.Token));
                    NextStart = Started < Count ? Task.Delay(Started * 250, Cancel) : null;
                }
                if (!Running.Any() && NextStart is null)
                    throw LastException;
                var Waiting = new List<Task>(Running);
                if (NextStart is not null)
                    Waiting.Add(NextStart);
                var Ended = await Task.WhenAny(Waiting).ConfigureAwait(false);
                Cancel.ThrowIfCancellationRequested();
                if (Ended is not Task<string> Request)
                    continue;
                Running.Remove(Request);
                if (Request.Status == TaskStatus.RanToCompletion)
                {
                    // 取消其余的请求，并观察它们的异常
                    Others.Cancel();
                    foreach (var Other in Running)
                        _ = Other.ContinueWith(o => o.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    return Request.Result;
                }
                LastException = Request.Exception?.GetBaseException() ?? new OperationCanceledException();
            }
        }

        /// <summary>
        /// 同步的网络请求会阻塞当前线程，不允许在 UI 线程中执行（本地回环地址除外）。
        /// </summary>
        public static void EnsureNotUiThread(string Url)
        {
            if (ModBase.RunInUi() && !Url.Contains("//127."))
                throw new Exception("在 UI 线程执行了网络请求");
        }

        /// <summary>
    /// 以多线程下载网页文件的方式获取内容。
    /// 不支持缓存协商；若需缓存协商，需要换用 NetRequestByClient。
    /// </summary>
        public static string NetRequestByLoader(string Url, int Timeout = 45000, bool IsJson = false, bool SimulateBrowserHeaders = false)
        {
            return NetRequestByLoader(new[] { Url }, Timeout, IsJson, SimulateBrowserHeaders);
        }
        /// <summary>
    /// 以多线程下载网页文件的方式获取内容。
    /// 不支持缓存协商；若需缓存协商，需要换用 NetRequestByClient。
    /// </summary>
        public static string NetRequestByLoader(IEnumerable<string> Urls, int Timeout = 45000, bool IsJson = false, bool SimulateBrowserHeaders = false)
        {
            string Temp = ModMain.RequestTaskTempFolder() + "download.txt";
            var NewTask = new LoaderDownload("源码获取 " + ModBase.GetUuid() + "#", new List<NetFile>() { new NetFile(Urls, Temp, new FileChecker() { IsJson = IsJson }, SimulateBrowserHeaders) });
            try
            {
                NewTask.WaitForExitTime(Timeout, TimeoutMessage: "连接服务器超时（第一下载源：" + Urls.First() + "）");
                string Result = FileUtils.ReadAsString(Temp);
                FileUtils.Delete(Temp);
                return Result;
            }
            finally
            {
                NewTask.Cancel();
            }
        }

        #endregion

        #region 文件下载

        /// <summary>
    /// 通过网络请求直接下载小文件，若文件已存在将被覆盖。
    /// 不建议用于下载大文件。
    /// </summary>
        public static void NetDownloadByClient(string Url, string LocalPath, bool SimulateBrowserHeaders = false)
        {
            EnsureNotUiThread(Url);
            NetDownloadByClientAsync(Url, LocalPath, SimulateBrowserHeaders).GetAwaiter().GetResult();
        }
        /// <summary>
        /// 通过网络请求直接下载小文件，若文件已存在将被覆盖。最多尝试 3 次。
        /// 不建议用于下载大文件。
        /// </summary>
        public static async Task NetDownloadByClientAsync(string Url, string LocalPath, bool SimulateBrowserHeaders = false, CancellationToken Cancel = default)
        {
            Logger.Info($"通过网络请求直接下载小文件：{Url} → {LocalPath}");
            for (int RetryCount = 0; ; RetryCount++)
            {
                try
                {
                    FileUtils.Write(LocalPath, await SendRequestAsync(Url, HttpMethod.Get, SimulateBrowserHeaders: SimulateBrowserHeaders, Cancel: Cancel).ConfigureAwait(false));
                    return;
                }
                catch (Exception ex)
                {
                    FileUtils.Delete(LocalPath);
                    ex.ThrowIfCanceled();
                    if (RetryCount >= 2)
                        throw new HttpRequestException($"通过网络请求直接下载小文件失败（{LocalPath}）", ex);
                }
            }
        }

        /// <summary>
    /// 通过多线程下载引擎下载文件。
    /// </summary>
        public static void NetDownloadByLoader(string Url, string LocalPath, ModLoader.LoaderBase LoaderToSyncProgress = null, FileChecker Check = null, bool SimulateBrowserHeaders = false)
        {
            NetDownloadByLoader(new[] { Url }, LocalPath, LoaderToSyncProgress, Check, SimulateBrowserHeaders);
        }
        /// <summary>
    /// 通过多线程下载引擎下载文件。
    /// </summary>
    /// <param name="Urls">文件的 Url 列表。</param>
    /// <param name="LocalPath">下载的本地地址。</param>
        public static void NetDownloadByLoader(IEnumerable<string> Urls, string LocalPath, ModLoader.LoaderBase LoaderToSyncProgress = null, FileChecker Check = null, bool SimulateBrowserHeaders = false)
        {
            var NewTask = new LoaderDownload($"文件下载 {ModBase.GetUuid()}#", new List<NetFile>() { new NetFile(Urls, LocalPath, Check, SimulateBrowserHeaders) });
            try
            {
                NewTask.WaitForExit(LoaderToSyncProgress: LoaderToSyncProgress);
            }
            catch (Exception ex)
            {
                throw new WebException($"多线程直接下载文件失败（第一下载源：" + Urls.First() + "）", ex);
            }
            finally
            {
                NewTask.Cancel();
            }
        }

        #endregion

        #region 基础请求引擎

        // 基础请求函数
        private static byte[] SendRequest(string Url, HttpMethod Method, object Content = null, Dictionary<string, string> Headers = null, bool SimulateBrowserHeaders = false, int Timeout = 25000, Encoding Encoding = null)
        {
            EnsureNotUiThread(Url);
            return SendRequestAsync(Url, Method, Content, Headers, SimulateBrowserHeaders, Timeout, Encoding).GetAwaiter().GetResult();
        }
        /// <summary>
        /// 发送请求并读取全部返回内容。发送与读取各自最多等待 Timeout 毫秒。
        /// 带有磁盘缓存：有效期内直接返回缓存，否则进行缓存协商；网络不可用时沿用上一次成功的内容。
        /// </summary>
        private static async Task<byte[]> SendRequestAsync(string Url, HttpMethod Method, object Content = null, Dictionary<string, string> Headers = null, bool SimulateBrowserHeaders = false, int Timeout = 25000, Encoding Encoding = null, CancellationToken Cancel = default)
        {
            HttpRequestMessage Request = null;
            HttpResponseMessage Response = null;
            CancellationTokenSource StepToken = null;
            bool Cacheable = false;
            // 每一步（发送、读取）单独计时；超时时释放响应，以中断卡住的读取（.NET Framework 的流读取不响应取消令牌）
            CancellationToken NewStep()
            {
                StepToken?.Dispose();
                StepToken = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
                StepToken.CancelAfter(Timeout);
                StepToken.Token.Register(() => { try { Response?.Dispose(); } catch { } });
                return StepToken.Token;
            }
            bool IsTimeout() => StepToken is not null && StepToken.IsCancellationRequested && !Cancel.IsCancellationRequested;
            try
            {
                // 构建请求
                Url = ModSecret.SecretCdnSign(Url);
                Request = new HttpRequestMessage(Method, Url);
                if (Content is not null && Request.Method != HttpMethod.Get && Request.Method != HttpMethod.Head) // 写入 Content
                {
                    Request.Content = Content switch
                    {
                        HttpContent Http => Http.Clone(),
                        byte[] Bytes => new ByteArrayContent(Bytes),
                        _ => new ByteArrayContent((Encoding ?? Encoding.UTF8).GetBytes(Content.ToString()))
                    };
                }
                foreach (var Header in Headers ?? new Dictionary<string, string>()) // 写入 Headers
                {
                    if (Header.Key.Lower() == "content-type")
                    {
                        Request.Content?.Headers.TryAddWithoutValidation(Header.Key, Header.Value);
                    }
                    else
                    {
                        Request.Headers.TryAddWithoutValidation(Header.Key, Header.Value);
                    }
                }
                ModSecret.SecretHeadersSign(Url, ref Request, SimulateBrowserHeaders);
                // 磁盘缓存：有效期内直接返回；否则附加条件请求头
                HttpDiskCache.Folder ??= ModBase.PathTemp + @"Cache\HttpV2\";
                Cacheable = HttpDiskCache.IsCacheable(Request);
                if (Cacheable)
                {
                    var Fresh = HttpDiskCache.TryUseOrPrepare(Request, Url);
                    if (Fresh is not null)
                        return Fresh;
                }
                // 发送请求
                Response = await RequestClient.SendAsync(Request, HttpCompletionOption.ResponseHeadersRead, NewStep()).ConfigureAwait(false);
                if (Cacheable && Response.StatusCode == HttpStatusCode.NotModified)
                {
                    var Cached = HttpDiskCache.ReadNotModified(Response, Url);
                    if (Cached is not null)
                        return Cached;
                    // 缓存文件已丢失：不带条件请求头重新请求一次
                    Response.Dispose();
                    var Retry = Request.Clone();
                    Retry.Headers.Remove("If-None-Match");
                    Retry.Headers.Remove("If-Modified-Since");
                    Request.Dispose();
                    Request = Retry;
                    Response = await RequestClient.SendAsync(Request, HttpCompletionOption.ResponseHeadersRead, NewStep()).ConfigureAwait(false);
                }
                // 读取响应
                byte[] ResponseBytes;
                var ReadToken = NewStep();
                using (var ResponseStream = await Response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var Stream = new MemoryStream())
                {
                    await ResponseStream.CopyToAsync(Stream, 81920, ReadToken).ConfigureAwait(false);
                    ResponseBytes = Stream.ToArray();
                }
                if (!Response.IsSuccessStatusCode)
                {
                    string ResponseMessage = ResponseBytes.GetString(Encoding);
                    throw new HttpRequestCodeException($"错误码 {Response.StatusCode} ({(int)Response.StatusCode})，{Method}，{Url}" + (string.IsNullOrEmpty(ResponseMessage) ? "" : ModBase.CrLf + ResponseMessage), Response.StatusCode, ResponseMessage);
                }
                if (Cacheable)
                    HttpDiskCache.Store(Response, Url, ResponseBytes);
                return ResponseBytes;
            }
            catch (Exception) when (IsTimeout())
            {
                if (TryUseStale(Cacheable, Url, out var Stale))
                    return Stale;
                throw new WebException($"连接服务器超时，请稍后再试，或使用 VPN 改善网络环境（{Method}, {Url}）", WebExceptionStatus.Timeout);
            }
            catch (HttpRequestException ex)
            {
                // 连接失败（而不是服务器返回了错误码）时，沿用上一次成功的内容
                if (ex is not HttpRequestCodeException && TryUseStale(Cacheable, Url, out var Stale))
                    return Stale;
                throw;
            }
            catch (Exception ex)
            {
                Cancel.ThrowIfCancellationRequested();
                ex.ThrowIfCanceled();
                if (ex.IsBadNetwork())
                {
                    if (TryUseStale(Cacheable, Url, out var Stale))
                        return Stale;
                    throw new WebException($"网络请求失败，请稍后再试，或使用 VPN 改善网络环境（{Method}, {Url}）", WebExceptionStatus.Timeout);
                }
                else
                {
                    throw new Exception($"网络请求出现意外异常（{Method}, {Url}）", ex);
                }
            }
            finally
            {
                Request?.Dispose();
                StepToken?.Dispose();
                Response?.Dispose();
            }
        }
        /// <summary>
        /// 网络不可用时，尝试取回该地址上一次成功获取的内容（版本列表等），让下载页等界面在离线时仍可使用。
        /// </summary>
        private static bool TryUseStale(bool Cacheable, string Url, out byte[] Body)
        {
            Body = Cacheable ? HttpDiskCache.TryReadStale(Url) : null;
            if (Body is null)
                return false;
            Logger.Warn($"网络不可用，改用上一次获取的内容：{Url}");
            return true;
        }
        private static readonly HttpClient RequestClient = new HttpClient(new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
            UseCookies = false // 不设为 False 就不能从 Header 手动传入 Cookies
        })
        { Timeout = System.Threading.Timeout.InfiniteTimeSpan }; // 超时由每次请求的 CancellationToken 控制

        #endregion

        #region 多线程下载引擎

        private static HttpClient ThreadClient = new HttpClient(new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip });

        /// <summary>
    /// 最大线程数。
    /// </summary>
        public static int NetTaskThreadLimit
        {
            get
            {
                return Settings.Get<int>("ToolDownloadThread") + 1;
            }
        }
        /// <summary>
    /// 速度下限。
    /// </summary>
        public static long NetTaskSpeedLimitLow = 256L * 1024L; // 256K/s
                                                                /// <summary>
    /// 速度上限。若无限制则为 -1。
    /// </summary>
        public static long NetTaskSpeedLimitHigh
        {
            get
            {
                if (_NetTaskSpeedLimitHigh is null)
                {
                    int Setting = Settings.Get<int>("ToolDownloadSpeed");
                    if (Setting <= 14)
                    {
                        _NetTaskSpeedLimitHigh = (long?)Math.Round((Setting + 1) * 0.1d * 1024d * 1024d);
                    }
                    else if (Setting <= 31)
                    {
                        _NetTaskSpeedLimitHigh = (long?)Math.Round((Setting - 11) * 0.5d * 1024d * 1024d);
                    }
                    else if (Setting <= 41)
                    {
                        _NetTaskSpeedLimitHigh = (Setting - 21) * 1024 * 1024L;
                    }
                    else
                    {
                        _NetTaskSpeedLimitHigh = (long?)-1;
                    }
                }
                return _NetTaskSpeedLimitHigh.Value;
            }
        }
        private static long? _NetTaskSpeedLimitHigh = default;
        public static void UpdateNetTaskSpeedLimitHigh(int Value)
        {
            _NetTaskSpeedLimitHigh = default;
        }
        /// <summary>
    /// 基于限速，当前可以下载的剩余量。
    /// </summary>
        public static long NetTaskSpeedLimitLeft = -1;
        /// <summary>
    /// 正在运行中的线程数。
    /// </summary>
        public static int NetTaskThreadCount = 0;

        /// <summary>
    /// 下载源。
    /// </summary>
        public class NetSource
        {
            public int Id;
            public string Url;
            public int FailCount;
            public Exception Ex;
            /// <summary>
        /// 若该下载源正在进行强制单线程下载，标记这个唯一的线程。
        /// </summary>
            public NetThread SingleThread;
            public bool IsFailed;
            public override string ToString()
            {
                return Url;
            }
        }
        /// <summary>
    /// 下载进度标示。
    /// </summary>
        public enum NetState
        {
            /// <summary>
        /// 尚未进行已存在检查。
        /// </summary>
            WaitingToCheck = -1,
            /// <summary>
        /// 尚未开始。
        /// </summary>
            WaitingToDownload = 0,
            /// <summary>
        /// 正在连接，尚未获取文件大小。
        /// </summary>
            Connecting = 1,
            /// <summary>
        /// 已连接，正在尝试接收首个下载数据包。
        /// </summary>
            Reading = 2,
            /// <summary>
        /// 正在下载。
        /// </summary>
            Downloading = 3,
            /// <summary>
        /// 正在合并文件。
        /// </summary>
            Merging = 4,
            /// <summary>
        /// 已完成。
        /// </summary>
            Finished = 5,
            /// <summary>
        /// 已失败或中断。
        /// </summary>
            Canceled = 6
        }
        /// <summary>
    /// 预下载检查行为。
    /// </summary>
        public enum NetPreDownloadBehaviour
        {
            /// <summary>
        /// 当文件已存在时，显示提示以提醒用户是否继续下载。
        /// </summary>
            HintWhileExists,
            /// <summary>
        /// 当文件已存在或正在下载时，直接退出下载函数执行，不对用户进行提示。
        /// </summary>
            ExitWhileExistsOrDownloading,
            /// <summary>
        /// 不进行已存在检查。
        /// </summary>
            IgnoreCheck
        }

        /// <summary>
    /// 下载线程。
    /// </summary>
        public class NetThread : IEnumerable<NetThread>, IEquatable<NetThread>
        {

            /// <summary>
        /// 对应的下载任务。
        /// </summary>
            public NetFile Task;
            /// <summary>
        /// 对应的线程。
        /// </summary>
            public Thread Thread;
            /// <summary>
        /// 链表中的下一个线程。
        /// </summary>
            public NetThread NextThread;
            private IEnumerable<NetThread> Next
            {
                get
                {
                    var CurrentChain = this;
                    while (CurrentChain is not null)
                    {
                        yield return CurrentChain;
                        CurrentChain = CurrentChain.NextThread;
                    }
                }
            }
            public IEnumerator<NetThread> GetEnumerator()
            {
                return Next.GetEnumerator();
            }
            private IEnumerator IEnumerable_GetEnumerator()
            {
                return Next.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => IEnumerable_GetEnumerator();

            /// <summary>
        /// 分配给任务中每个线程（无论其是否失败）的编号。
        /// </summary>
            public int Uuid;
            /// <summary>
        /// 是否为第一个线程。
        /// </summary>
            public bool IsFirstThread
            {
                get
                {
                    return DownloadStart == 0L && Task.FileSize == -2;
                }
            }
            /// <summary>
        /// 该线程的缓存文件。
        /// </summary>
            public string Temp;

            /// <summary>
        /// 线程下载起始位置。
        /// </summary>
            public long DownloadStart;
            /// <summary>
        /// 线程下载结束位置。
        /// </summary>
            public long DownloadEnd
            {
                get
                {
                    lock (Task.LockChain)
                    {
                        if (NextThread is null)
                        {
                            if (Task.IsUnknownSize)
                            {
                                return 1000 * 1000 * 1000 * 1000L; // 约 1T
                            }
                            else
                            {
                                return Task.FileSize - 1L;
                            }
                        }
                        else
                        {
                            return NextThread.DownloadStart - 1L;
                        }
                    }
                }
            }
            /// <summary>
        /// 线程未下载的文件大小。
        /// </summary>
            public long DownloadUndone
            {
                get
                {
                    return DownloadEnd - (DownloadStart + DownloadDone) + 1L;
                }
            }
            /// <summary>
        /// 线程已下载的文件大小。
        /// </summary>
            public long DownloadDone = 0L;

            /// <summary>
        /// 上次记速时的时间。
        /// </summary>
            private long SpeedLastTime = ModBase.GetTimeMs();
            /// <summary>
        /// 上次记速时的已下载大小。
        /// </summary>
            private long SpeedLastDone = 0L;
            /// <summary>
        /// 当前的下载速度，单位为 Byte / 秒。
        /// </summary>
            public long Speed
            {
                get
                {
                    if (ModBase.GetTimeMs() - SpeedLastTime > 200L)
                    {
                        long DeltaTime = ModBase.GetTimeMs() - SpeedLastTime;
                        _Speed = (long)Math.Round((DownloadDone - SpeedLastDone) / (DeltaTime / 1000d));
                        SpeedLastDone = DownloadDone;
                        SpeedLastTime += DeltaTime;
                    }
                    return _Speed;
                }
            }
            private long _Speed = 0L;

            /// <summary>
        /// 线程初始化时的时间。
        /// </summary>
            public long InitTime = ModBase.GetTimeMs();
            /// <summary>
        /// 上次接受到有效数据的时间，-1 表示尚未有有效数据。
        /// </summary>
            public long LastReceiveTime = -1;

            /// <summary>
        /// 当前线程的状态。
        /// </summary>
            public NetState State = NetState.WaitingToDownload;
            /// <summary>
        /// 是否已经结束。
        /// </summary>
            public bool IsEnded
            {
                get
                {
                    return State == NetState.Finished || State == NetState.Canceled;
                }
            }

            /// <summary>
        /// 当前选取的是哪一个 Url。
        /// </summary>
            public NetSource Source;

            // 允许进行 UUID 比较
            public bool Equals(NetThread other)
            {
                return other is not null && Uuid == other.Uuid;
            }
            public override bool Equals(object obj)
            {
                return Equals(obj as NetThread);
            }
            public static bool operator ==(NetThread left, NetThread right)
            {
                return EqualityComparer<NetThread>.Default.Equals(left, right);
            }
            public static bool operator !=(NetThread left, NetThread right)
            {
                return !(left == right);
            }
            public override int GetHashCode()
            {
                return Uuid;
            }
        }
        /// <summary>
    /// 下载单个文件。
    /// </summary>
        public class NetFile
        {

            #region 属性

            /// <summary>
        /// 所属的文件列表任务。
        /// </summary>
            public ConcurrentList<LoaderDownload> Tasks = new ConcurrentList<LoaderDownload>();
            /// <summary>
        /// 所有下载源。
        /// </summary>
            public List<NetSource> Sources;
            /// <summary>
        /// 用于在第一个线程出错时切换下载源。
        /// </summary>
            private int FirstThreadSource = 0;
            /// <summary>
        /// 所有已经被标记为失败的，但未完整尝试过的，不允许断点续传的下载源。
        /// </summary>
            public ConcurrentList<NetSource> SourcesOnce = new ConcurrentList<NetSource>();
            /// <summary>
        /// 仅当合并失败或首次下载失败时，会将所有下载源重新标记为不允许断点续传的下载源，逐个重新尝试下载。
        /// 这一策略可以兼容多个下载源中的一部分返回错误的文件的情况，以及部分在多线程下载时会抽风的源。
        /// </summary>
            private bool Retried = false;
            /// <summary>
        /// 获取从某个源开始，第一个可用的源。
        /// </summary>
            private NetSource GetSource(int Id = 0)
            {
                if (Id >= Sources.Count || Id < 0)
                    Id = 0;
                lock (LockSource)
                {
                    if (HasAvailableSource(false))
                    {
                        // 存在多线程可用源
                        var CurrentSource = Sources[Id];
                        while (CurrentSource.IsFailed)
                        {
                            Id += 1;
                            if (Id >= Sources.Count)
                                Id = 0;
                            CurrentSource = Sources[Id];
                        }
                        return CurrentSource;
                    }
                    else if (SourcesOnce.Any())
                    {
                        // 仅存在单线程可用源
                        return SourcesOnce.First();
                    }
                    else
                    {
                        // 没有可用源
                        return null;
                    }
                }
            }
            /// <summary>
        /// 是否存在可用源。
        /// </summary>
            public bool HasAvailableSource(bool AllowOnceSource = true)
            {
                lock (LockSource)
                {
                    if (Sources.Any(s => !s.IsFailed))
                        return true; // 存在多线程可用源
                    if (AllowOnceSource && SourcesOnce.Any())
                        return true; // 存在单线程可用源
                }
                return false;
            }

            /// <summary>
        /// 存储在本地的带文件名的地址。
        /// </summary>
            public string LocalPath = null;
            /// <summary>
        /// 存储在本地的文件名。
        /// </summary>
            public string LocalName = null;

            /// <summary>
        /// 当前的下载状态。
        /// </summary>
            public NetState State = NetState.WaitingToCheck;
            /// <summary>
        /// 导致下载失败的原因。
        /// </summary>
            public List<Exception> Ex = new List<Exception>();

            /// <summary>
        /// 作为文件组成部分的线程链表。
        /// 如果没有线程，可以为 Nothing。
        /// </summary>
            public NetThread Threads;

            /// <summary>
        /// 文件的总大小。若为 -2 则为未获取，若为 -1 则为无法获取准确大小。
        /// </summary>
            public long FileSize = -2;
            /// <summary>
        /// 该文件是否无法获取准确大小。
        /// </summary>
            public bool IsUnknownSize = false;
            /// <summary>
        /// 该文件是否不需要分割。
        /// </summary>
            public bool IsNoSplit
            {
                get
                {
                    return IsUnknownSize || FileSize < 1024 * 1024; // 小于 1MB 的文件不分割
                }
            }
            /// <summary>
        /// 为不需要分割的小文件在内存中提供临时存储。
        /// </summary>
            private MemoryStream Cache;

            /// <summary>
        /// 文件的已下载大小。
        /// </summary>
            public long DownloadDone = 0L;
            /// <summary>
        /// 文件的校验规则。
        /// </summary>
            public FileChecker Check;
            /// <summary>
        /// 是否模拟浏览器的 UserAgent 和 Referer。
        /// </summary>
            public bool SimulateBrowserHeaders;

            /// <summary>
        /// 上次记速时的时间。
        /// </summary>
            private long SpeedLastTime = ModBase.GetTimeMs();
            /// <summary>
        /// 上次记速时的已下载大小。
        /// </summary>
            private long SpeedLastDone = 0L;
            /// <summary>
        /// 当前的下载速度，单位为 Byte / 秒。
        /// </summary>
            public long Speed
            {
                get
                {
                    if (ModBase.GetTimeMs() - SpeedLastTime > 200L)
                    {
                        long DeltaTime = ModBase.GetTimeMs() - SpeedLastTime;
                        _Speed = (long)Math.Round((DownloadDone - SpeedLastDone) / (DeltaTime / 1000d));
                        SpeedLastDone = DownloadDone;
                        SpeedLastTime += DeltaTime;
                    }
                    return _Speed;
                }
            }
            private long _Speed = 0L;

            /// <summary>
        /// 该文件是否由本地文件直接复制完成。
        /// </summary>
            public bool IsCopy = false;
            /// <summary>
        /// 本文件的显示进度。
        /// </summary>
            public double Progress
            {
                get
                {
                    switch (State)
                    {
                        case NetState.WaitingToCheck:
                            {
                                return 0d;
                            }
                        case NetState.WaitingToDownload:
                            {
                                return 0.01d;
                            }
                        case NetState.Connecting:
                            {
                                return 0.02d;
                            }
                        case NetState.Reading:
                            {
                                return 0.04d;
                            }
                        case NetState.Downloading:
                            {
                                // 正在下载中，对应 5% ~ 98%
                                double OriginalProgress = IsUnknownSize ? 0.5d : DownloadDone / (double)Math.Max(FileSize, 1L);
                                OriginalProgress = 1d - Math.Pow(1d - OriginalProgress, 0.9d);
                                return OriginalProgress * 0.93d + 0.05d;
                            }
                        case NetState.Merging:
                            {
                                return 0.99d;
                            }
                        case NetState.Finished:
                        case NetState.Canceled:
                            {
                                return 1d;
                            }

                        default:
                            {
                                throw new ArgumentOutOfRangeException("文件状态未知：" + ((int)State).ToString());
                            }
                    }
                }
            }

            /// <summary>
        /// 各个线程建立连接成功的总次数。
        /// </summary>
            private int ConnectCount = 0;
            /// <summary>
        /// 各个线程建立连接成功的总时间。
        /// </summary>
            private long ConnectTime = 0L;
            /// <summary>
        /// 各个线程建立连接成功的平均时间，单位为毫秒，-1 代表尚未有成功连接。
        /// </summary>
            private int ConnectAverage
            {
                get
                {
                    int CurrentConnectCount = ConnectCount; // 防止竞态条件
                    return (int)Math.Round(CurrentConnectCount == 0 ? -1 : ConnectTime / (double)CurrentConnectCount);
                }
            }

            private const long FilePieceLimit = 262144L;
            public readonly object LockState = new object();
            public readonly object LockChain = new object();
            public readonly object LockSource = new object();

            public readonly int Uuid = ModBase.GetUuid();
            public override bool Equals(object obj)
            {
                NetFile @file = obj as NetFile;
                return @file is not null && Uuid == @file.Uuid;
            }
            public override int GetHashCode()
            {
                return Uuid;
            }

            #endregion

            /// <summary>
        /// 新建一个需要下载的文件。
        /// </summary>
        /// <param name="LocalPath">包含文件名的本地地址。</param>
            public NetFile(IEnumerable<string> Urls, string LocalPath, FileChecker Checker = null, bool SimulateBrowserHeaders = false)
            {
                var Sources = new List<NetSource>();
                int Count = 0;
                Urls = Urls.Distinct().ToArray();
                foreach (string Source in Urls)
                {
                    Sources.Add(new NetSource() { FailCount = 0, Url = ModSecret.SecretCdnSign(Source.ReplaceLineEndings("").Trim()), Id = Count, IsFailed = false, Ex = null });
                    Count += 1;
                }
                this.Sources = Sources;
                this.LocalPath = LocalPath;
                Check = Checker;
                this.SimulateBrowserHeaders = SimulateBrowserHeaders;
                LocalName = PathUtils.GetLastPart(LocalPath);
            }

            /// <summary>
        /// 尝试开始一个新的下载线程。
        /// 如果失败，返回 Nothing。
        /// </summary>
            public NetThread TryBeginThread()
            {
                try
                {

                    long StartPosition = 0L;
                    NetSource StartSource = null;
                    Thread Th;
                    NetThread ThreadInfo;
                    // 条件检测
                    lock (LockSource)
                    {
                        if (NetTaskThreadCount >= NetTaskThreadLimit || !HasAvailableSource() || IsNoSplit && Threads is not null && Threads.State != NetState.Canceled)
                            return null;
                        if (State >= NetState.Merging || State == NetState.WaitingToCheck)
                            return null;
                        lock (LockState)
                        {
                            if (State < NetState.Connecting)
                                State = NetState.Connecting;
                        }
                        // 初始化参数
                        // 获取线程起点与下载源
                        // 不分割
                        // 只剩下单线程可用点（不分割时直接以单线程开始）
                        if (IsNoSplit || !HasAvailableSource(false))
                        {
                            if (!IsNoSplit && SourcesOnce.First().SingleThread is not null && SourcesOnce.First().SingleThread.State != NetState.Canceled)
                                return null;
                            Reset();
                            State = NetState.Reading;
                        }
                    }
                    lock (LockChain)
                    {
                        // 决定新线程从哪里开始下载；返回 False 表示不需要再开新线程
                        bool FindStartPoint()
                        {
                            // 首个开始点
                            if (Threads is null)
                            {
                                StartPosition = 0L;
                                StartSource = GetSource(FirstThreadSource);
                                FirstThreadSource = StartSource.Id + 1;
                                return true;
                            }
                            // 寻找失败点
                            foreach (NetThread Thread in Threads)
                            {
                                if (Thread.State == NetState.Canceled && Thread.DownloadUndone > 0L)
                                {
                                    StartPosition = Thread.DownloadStart + Thread.DownloadDone;
                                    StartSource = GetSource(Thread.Source.Id + 1);
                                    return true;
                                }
                            }
                            // 是否禁用多线程，以及规定碎片大小
                            var Source = GetSource();
                            if (Source is null)
                                return false;
                            string TargetUrl = Source.Url;
                            if (TargetUrl.Contains("pcl2-server") || TargetUrl.Contains("meloong.com") || TargetUrl.Contains("github.com") || TargetUrl.Contains("optifine.net") || TargetUrl.Contains("momot.rs"))
                                return false;
                            // 寻找最大碎片，从它的后段开始新线程
                            var FilePieceMax = Threads;
                            foreach (NetThread Thread in Threads)
                            {
                                if (Thread.DownloadUndone > FilePieceMax.DownloadUndone)
                                    FilePieceMax = Thread;
                            }
                            if (FilePieceMax is null || FilePieceMax.DownloadUndone < FilePieceLimit)
                                return false;
                            StartPosition = (long)Math.Round(FilePieceMax.DownloadEnd - FilePieceMax.DownloadUndone * 0.4d);
                            StartSource = GetSource();
                            return true;
                        }
                        if (!FindStartPoint())
                            return null;

                        // 开始线程
                        if (StartPosition > FileSize && FileSize >= 0L && !IsUnknownSize || StartPosition < 0L || StartSource == null)
                            return null;
                        // 构建线程
                        int ThreadUuid = ModBase.GetUuid();
                        if (!Tasks.Any())
                            return null; // 由于中断，已没有可用任务
                        Th = new Thread((_) => this.Thread((ModNet.NetThread)(object)_)) { Name = $"下载 {Tasks[0].Uuid}/{Uuid}/{ThreadUuid}#", Priority = ThreadPriority.BelowNormal };
                        ThreadInfo = new NetThread() { Uuid = ThreadUuid, DownloadStart = StartPosition, Thread = Th, Source = StartSource, Task = this, State = NetState.WaitingToDownload };
                        // 链表处理
                        if (ThreadInfo.IsFirstThread || Threads is null)
                        {
                            Threads = ThreadInfo;
                        }
                        else
                        {
                            var CurrentChain = Threads;
                            while (CurrentChain.DownloadEnd <= StartPosition)
                                CurrentChain = CurrentChain.NextThread;
                            ThreadInfo.NextThread = CurrentChain.NextThread;
                            CurrentChain.NextThread = ThreadInfo;
                        }

                    }
                    // 开始线程
                    Interlocked.Increment(ref NetTaskThreadCount);
                    lock (LockSource)
                    {
                        if (!HasAvailableSource(false))
                            SourcesOnce.First().SingleThread = ThreadInfo;
                    }
                    Th.Start(ThreadInfo);
                    return ThreadInfo;
                }

                catch (Exception ex)
                {
                    Fail(new Exception($"尝试开始下载线程失败（{LocalName}）", ex));
                    return null;
                }
            }
            /// <summary>
        /// 每个下载线程执行的代码。
        /// </summary>
            private void Thread(NetThread Th)
            {
                Logger.Log($"{LocalName}：开始，起始点 {Th.DownloadStart}，{Th.Source.Url}", Th.DownloadStart == 0L ? LogLevel.Info : LogLevel.Trace);
                Stream ResultStream = null;
                HttpRequestMessage HttpRequest = null;
                HttpResponseMessage Response = null;
                Stream ResponseStream = null;
                CancellationTokenSource CancelToken = null;
                // 部分下载源真的特别慢，并且只需要一个请求，例如 Ping 为 20s，如果增长太慢，就会造成类似 2.5s 5s 7.5s 10s 12.5s... 的极大延迟
                // 延迟过长会导致某些特别慢的链接迟迟不被掐死
                int Timeout = Math.Min(Math.Max(ConnectAverage, 15000) * (1 + Th.Source.FailCount), 30000);
                Th.State = NetState.Connecting;
                try
                {
                    int HttpDataCount = 0;
                    long ContentLength = 0L;
                    // 建立连接并接收数据；中途被取消或该下载源只允许单线程时提前返回
                    void Transfer()
                    {
                        if (SourcesOnce.Contains(Th.Source) && Th != Th.Source.SingleThread)
                            return; // 结束本线程的传输
                        HttpRequest = new HttpRequestMessage(HttpMethod.Get, Th.Source.Url);
                        ModSecret.SecretHeadersSign(Th.Source.Url, ref HttpRequest, SimulateBrowserHeaders);
                        CancelToken = new CancellationTokenSource(Timeout);
                        if (!Th.IsFirstThread)
                            HttpRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(Th.DownloadStart, default);
                        Response = ThreadClient.SendAsync(HttpRequest, HttpCompletionOption.ResponseHeadersRead, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout);
                        if (!Response.IsSuccessStatusCode) // 状态码检查
                        {
                            throw new HttpRequestCodeException($"错误码 {Response.StatusCode} ({(int)Response.StatusCode})，{Th.Source.Url}", Response.StatusCode, null);
                        }
                        if (State == NetState.Canceled)
                            return; // 结束本线程的传输 // 快速中断
                        if ((Response.RequestMessage.RequestUri.ToString() ?? "") != (Th.Source.Url ?? ""))
                            Logger.Trace(() => $"{LocalName}：重定向至 {Response.RequestMessage.RequestUri}");
                        // 文件大小校验
                        ContentLength = Response.Content.Headers.ContentLength.GetValueOrDefault(-1);
                        bool IsRangeNotSupported = false; // 原 VB 以 GoTo NotSupportRange 跳入下方分支
                        if (ContentLength < 0L)
                        {
                            if (FileSize > 1L)
                            {
                                if (Th.DownloadStart == 0L)
                                {
                                    Logger.Info($"{LocalName}：文件大小未知，但已从其他下载源获取，不作处理");
                                }
                                else
                                {
                                    Logger.Info($"{LocalName}：ContentLength 返回了 {ContentLength}，无法确定是否支持分段下载，视作不支持");
                                    IsRangeNotSupported = true;
                                }
                            }
                            else
                            {
                                FileSize = -1;
                                IsUnknownSize = true;
                                Logger.Info($"{LocalName}：文件大小未知");
                            }
                        }
                        else if (Th.IsFirstThread)
                        {
                            if (Check is not null)
                            {
                                if (ContentLength < Check.MinSize && Check.MinSize > 0L)
                                {
                                    throw new Exception($"文件大小不足，获取结果为 {ContentLength}，要求至少为 {Check.MinSize}。");
                                }
                                if (ContentLength != Check.ActualSize && Check.ActualSize > 0L)
                                {
                                    throw new Exception($"文件大小不一致，获取结果为 {ContentLength}，要求必须为 {Check.ActualSize}。");
                                }
                            }
                            FileSize = ContentLength;
                            IsUnknownSize = false;
                            Logger.Info($"{LocalName}：文件大小 {ContentLength}（{StringUtils.FormatByteSize(ContentLength)}）");
                            // 若文件大小大于 50 M，进行剩余磁盘空间校验
                            if (ContentLength > 50 * 1024 * 1024)
                            {
                                foreach (DriveInfo Drive in DriveInfo.GetDrives())
                                {
                                    string DriveName = Drive.Name.First().ToString();
                                    double RequiredSpace = (ModBase.PathTemp.StartsWithF(DriveName) ? ContentLength * 1.1d : 0d) + (LocalPath.StartsWithF(DriveName) ? ContentLength + 5 * 1024 * 1024 : 0L);
                                    if (RequiredSpace > 0d && Drive.TotalFreeSpace < RequiredSpace)
                                    {
                                        throw new Exception(DriveName + " 盘空间不足，无法进行下载。" + ModBase.CrLf + "需要至少 " + StringUtils.FormatByteSize((long)Math.Round(RequiredSpace)) + " 空间，但当前仅剩余 " + StringUtils.FormatByteSize(Drive.TotalFreeSpace) + "。" + (ModBase.PathTemp.StartsWithF(DriveName) ? ModBase.CrLf + ModBase.CrLf + "下载时需要与文件同等大小的空间存放缓存，你可以在设置中调整缓存文件夹的位置。" : ""));
                                    }
                                }
                            }
                        }
                        else if (FileSize < 0L)
                        {
                            throw new Exception("非首线程运行时，尚未获取文件大小");
                        }
                        else if (Th.DownloadStart > 0L && ContentLength >= FileSize)
                        {
                            IsRangeNotSupported = true;
                        }
                        else if (!(FileSize - Th.DownloadStart == ContentLength))
                        {
                            throw new RangeNotSupportedException($"获取到的分段大小不一致：Range 起始于 {Th.DownloadStart}，预期 ContentLength 为 {FileSize - Th.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}");
                        }
                        if (IsRangeNotSupported)
                        {
                            lock (LockSource)
                            {
                                if (SourcesOnce.Contains(Th.Source))
                                    return; // 结束本线程的传输
                            }
                            throw new RangeNotSupportedException($"该下载源不支持分段下载：Range 起始于 {Th.DownloadStart}，预期 ContentLength 为 {FileSize - Th.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}");
                        }
                        // Log($"[Download] {LocalName} {Info.Uuid}#：通过大小检查，文件大小 {FileSize}，起始点 {Info.DownloadStart}，ContentLength {ContentLength}")
                        Th.State = NetState.Reading;
                        lock (LockState)
                        {
                            if (State < NetState.Reading)
                                State = NetState.Reading;
                        }
                        // 创建缓存文件
                        if (IsNoSplit)
                        {
                            Th.Temp = null;
                            Cache = new MemoryStream();
                            ResultStream = Cache;
                        }
                        else
                        {
                            Th.Temp = $@"{ModBase.PathTemp}Download\{Uuid}_{Th.Uuid}_{ModBase.RandomInteger(0, 999999)}.tmp";
                            ResultStream = FileUtils.CreateAsStream(Th.Temp);
                        }
                        // 开始下载
                        CancelToken.Dispose();
                        CancelToken = new CancellationTokenSource(); // 重置超时，不再让 CancellationToken 在固定时间后触发（#8488）
                        ResponseStream = Response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                        if (Settings.Get<bool>("SystemDebugDelay"))
                            System.Threading.Thread.Sleep(ModBase.RandomInteger(50, 3000));
                        byte[] ResponseBytes = new byte[16385];
                        HttpDataCount = ResponseStream.ReadAsync(ResponseBytes, 0, 16384, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout);
                        while ((IsUnknownSize || Th.DownloadUndone > 0L) && HttpDataCount > 0 && !ModBase.IsProgramEnding && State < NetState.Merging && (!Th.Source.IsFailed || Th.Source.SingleThread == Th)) // 判断是否下载完成
                        {
                            // 限速
                            while (NetTaskSpeedLimitHigh > 0L && NetTaskSpeedLimitLeft <= 0L)
                                System.Threading.Thread.Sleep(16);
                            int RealDataCount = (int)(IsUnknownSize ? HttpDataCount : Math.Min(HttpDataCount, Th.DownloadUndone));
                            if (NetTaskSpeedLimitHigh > 0L)
                                Interlocked.Add(ref NetTaskSpeedLimitLeft, -RealDataCount);
                            long DeltaTime = ModBase.GetTimeMs() - Th.LastReceiveTime;
                            if (DeltaTime > 100000000L)
                                DeltaTime = 1L; // 避免时间刻反转导致出现极大值
                            if (RealDataCount > 0)
                            {
                                // 有数据
                                if (Th.DownloadDone == 0L)
                                {
                                    // 第一次接受到数据
                                    Th.State = NetState.Downloading;
                                    lock (LockState)
                                    {
                                        if (State < NetState.Downloading)
                                            State = NetState.Downloading;
                                    }
                                    Interlocked.Increment(ref ConnectCount);
                                    Interlocked.Add(ref ConnectTime, ModBase.GetTimeMs() - Th.InitTime);
                                }
                                Th.Source.FailCount = 0;
                                foreach (var Task in Tasks)
                                    Task.FailCount = 0;
                                Interlocked.Add(ref NetManager.DownloadDone, RealDataCount);
                                Interlocked.Add(ref DownloadDone, RealDataCount);
                                Th.DownloadDone += RealDataCount;
                                ResultStream.Write(ResponseBytes, 0, RealDataCount);
                                // 已完成
                                if (Th.DownloadUndone == 0L && !IsUnknownSize)
                                    break;
                                // 检查速度是否过慢
                                if (Th.LastReceiveTime > 0L && DeltaTime > 5000L && DeltaTime > RealDataCount && Th.Source.SingleThread is null) // 数据包间隔大于 5s，且速度小于 1K/s
                                                                                                                                                 // 且并非单线程下载
                                {
                                    throw new TimeoutException("由于速度过慢断开链接，下载 " + RealDataCount + " B，消耗 " + DeltaTime + " ms。");
                                }
                                Th.LastReceiveTime = ModBase.GetTimeMs();
                            }
                            else if (Th.LastReceiveTime > 0L && DeltaTime > Timeout)
                            {
                                // 无数据，且已超时
                                throw new TimeoutException("操作超时，无数据。");
                            }
                            HttpDataCount = ResponseStream.ReadAsync(ResponseBytes, 0, 16384, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout);
                        }
                    }
                    Transfer();

                    if (State == NetState.Canceled || Th.Source.IsFailed && Th.Source.SingleThread != Th || Th.DownloadUndone > 0L && !IsUnknownSize)
                    {
                        // 被外部中断
                        Th.State = NetState.Canceled;
                        Logger.Info($"{LocalName}：中断");
                    }
                    else if (HttpDataCount == 0 && Th.DownloadUndone > 0L && !IsUnknownSize)
                    {
                        // 服务器无返回数据
                        throw new Exception($"返回的 ContentLength 过多：ContentLength 为 {ContentLength}，但获取到的总数据量仅为 {Th.DownloadDone}（全文件总数据量 {DownloadDone}）");
                    }
                    else
                    {
                        // 本线程完成
                        Th.State = NetState.Finished;
                        Logger.Trace(() => $"{LocalName}：完成，已下载 {Th.DownloadDone}（{Th.DownloadStart}~{Th.DownloadEnd}）");
                    }
                }
                catch (Exception ex)
                {
                    if (ex.IsBadNetwork() || ex is TaskCanceledException)
                    {
                        Logger.Info(new TimeoutException($"{LocalName}：超时（{Timeout}ms），{ex.Message}"));
                    }
                    else
                    {
                        Logger.Info(ex, $"{LocalName}：出错");
                    }
                    SourceFail(Th, ex, false);
                }
                finally
                {
                    // 释放资源
                    HttpRequest?.Dispose();
                    Response?.Dispose();
                    ResponseStream?.Dispose();
                    CancelToken?.Dispose();
                    if (!IsNoSplit)
                        ResultStream?.Dispose();
                    // 改变计数
                    Interlocked.Decrement(ref NetTaskThreadCount);
                    // 合并
                    if ((FileSize >= 0L && DownloadDone >= FileSize || FileSize == -1 && DownloadDone > 0L) && State < NetState.Merging && Th.State != NetState.Canceled)
                        Merge(Th);
                }
            }
            private void SourceFail(NetThread Th, Exception ex, bool IsMergeFailure)
            {
                // 状态变更
                Interlocked.Increment(ref Th.Source.FailCount);
                foreach (var Task in Tasks)
                {
                    int arglocation = Task.FailCount;
                    Interlocked.Increment(ref arglocation);
                    Task.FailCount = arglocation;
                }
                Th.State = NetState.Canceled;
                Th.Source.Ex = ex;
                // 根据情况判断，是否在多线程下禁用下载源（连续错误过多，或不支持断点续传）
                bool IsRangeNotSupported = ex is RangeNotSupportedException || ex.Message.Contains("(416)");
                if (IsMergeFailure || IsRangeNotSupported || ex.Message.Contains("(502)") || ex.Message.Contains("(404)") || ex.Message.Contains("未能解析") || ex.Message.Contains("无返回数据") || ex.Message.Contains("空间不足") || (ex.Message.Contains("(403)") || ex.Message.Contains("(429)")) || Th.Source.FailCount >= NetTaskThreadLimit.Clamp(5, 30) && DownloadDone < 1L || Th.Source.FailCount > NetTaskThreadLimit + 2) // 部分下载源在高频率请求下会返回 403/429，所以不应因此禁用下载源
                {
                    // 当一个下载源有多个线程在下载时，只选择其中一个线程进行后续处理
                    bool IsThisFail = false;
                    lock (LockSource)
                    {
                        if (!Th.Source.IsFailed || Th.Source.SingleThread == Th)
                        {
                            IsThisFail = true;
                            Th.Source.IsFailed = true;
                        }
                    }
                    // ……后续处理
                    if (IsThisFail)
                    {
                        Logger.Info($"{LocalName}：下载源被禁用（{Th.Source.Id}，Range 问题：{IsRangeNotSupported}）：{Th.Source.Url}");
                        Logger.Warn(ex, $"{(SourcesOnce.FirstOrDefault()?.SingleThread is null ? "" : "单线程")}下载源 {Th.Source.Id} 已被禁用");
                        lock (LockSource)
                            SourcesOnce.Remove(Th.Source);
                        if (ex.Message.Contains("空间不足"))
                        {
                            // 硬盘空间不足：强制失败
                            Fail(ex);
                        }
                        else if (HasAvailableSource() && !IsMergeFailure)
                        {
                        }
                        // 当前源失败，但还有下载源：正常地继续执行
                        else if (!Retried)
                        {
                            // 合并失败或首次下载失败，未重试：将所有下载源重新标记为不允许断点续传的下载源，逐个重新尝试下载
                            // 若所有源均不支持 Range，也会走到这里重试
                            if (!IsRangeNotSupported)
                                Logger.Warn($"{LocalName}：文件下载失败，正在自动重试……");
                            Retried = true;
                            lock (LockSource)
                            {
                                SourcesOnce.Clear();
                                foreach (var Source in Sources)
                                {
                                    SourcesOnce.Add(Source);
                                    Source.IsFailed = true;
                                }
                            }
                            Reset();
                            lock (LockState)
                                State = NetState.WaitingToDownload;
                        }
                        else if (HasAvailableSource() && IsMergeFailure)
                        {
                            // 合并失败且单个源失败：继续下一个源
                            Reset();
                            lock (LockState)
                                State = NetState.WaitingToDownload;
                        }
                        else
                        {
                            // 失败
                            Logger.Info($"{LocalName}：已无可用下载源，下载失败");
                            Exception ExampleEx = null;
                            lock (LockSource)
                            {
                                foreach (NetSource Source in Sources)
                                {
                                    Logger.Info($"已禁用的下载源：{Source.Url}");
                                    if (Source.Ex is not null)
                                    {
                                        ExampleEx = Source.Ex;
                                        Logger.Warn(Source.Ex, "下载源禁用原因");
                                    }
                                }
                            }
                            Fail(ExampleEx);
                        }
                    }
                }
                // 清理当前已下载的内容
                if (FileSize == -2)
                    Reset();
            }
            private void Reset()
            {
                FileSize = -2;
                Cache?.Dispose();
                Cache = null;
                lock (LockChain)
                    Threads = null;
                Interlocked.Add(ref NetManager.DownloadDone, -DownloadDone);
                DownloadDone = 0L;
                SpeedLastDone = 0L;
            }

            // 最终收束事件

            /// <summary>
        /// 下载完成。合并文件。
        /// </summary>
            private void Merge(NetThread Th)
            {
                // 状态判断
                lock (LockState)
                {
                    if (State < NetState.Merging)
                    {
                        State = NetState.Merging;
                    }
                    else
                    {
                        return;
                    }
                }
                int RetryCount = 0;
                try
                {
                    while (true)
                    {
                        try
                        {

                            // 创建文件夹
                            DirectoryUtils.Create(PathUtils.RemoveLastPart(LocalPath));
                            FileUtils.Delete(LocalPath);
                            lock (LockChain)
                            {
                                // 合并文件
                                if (IsNoSplit)
                                {
                                    // 仅有一个线程，从内存中输出
                                    Logger.Trace(() => $"{LocalName}：下载结束，从内存输出文件");
                                    FileUtils.Write(LocalPath, Cache);
                                }
                                else if (Threads.DownloadDone == DownloadDone && Threads.Temp is not null)
                                {
                                    // 仅有一个文件，直接复制
                                    Logger.Trace(() => $"{LocalName}：下载结束，仅有一个文件，无需合并");
                                    FileUtils.Copy(Threads.Temp, LocalPath);
                                }
                                else
                                {
                                    // 有多个线程，合并
                                    Logger.Trace(() => $"{LocalName}：下载结束，开始合并文件");
                                    using (var MergeFile = FileUtils.CreateAsStream(LocalPath))
                                    {
                                        using (var AddWriter = new BinaryWriter(MergeFile))
                                        {
                                            foreach (NetThread Thread in Threads)
                                            {
                                                if (Thread.DownloadDone == 0L || Thread.Temp is null)
                                                    continue;
                                                using (var fs = FileUtils.ReadAsStream(Thread.Temp))
                                                {
                                                    using (var TempReader = new BinaryReader(fs))
                                                    {
                                                        AddWriter.Write(TempReader.ReadBytes((int)Thread.DownloadDone));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                // 检查文件
                                string CheckResult = Check?.Check(LocalPath);
                                if (CheckResult is null && !IsUnknownSize && Check is not null)
                                {
                                    if (Check.ActualSize == -1)
                                    {
                                        CheckResult = new FileChecker() { ActualSize = FileSize }.Check(LocalPath); // 不修改原始的 Checker，以免污染原始实例
                                    }
                                    else if (Check.ActualSize != FileSize)
                                    {
                                        CheckResult = $"文件大小不一致：任务校验要求为 {Check.ActualSize}，请求结果为 {FileSize}";
                                    }
                                }
                                if (CheckResult is not null)
                                {
                                    Logger.Info($"{LocalName} 文件校验失败，下载线程细节：");
                                    foreach (NetThread T in Threads)
                                        Logger.Info($"- {T.Uuid}#，状态 {T.State}，范围 {T.DownloadStart}~{T.DownloadEnd}，已下载 {T.DownloadDone}，未下载 {T.DownloadUndone}");
                                    throw new Exception(CheckResult);
                                }
                                // 后处理
                                if (!IsNoSplit)
                                {
                                    foreach (NetThread Thread in Threads)
                                    {
                                        if (Thread.Temp is not null)
                                            FileUtils.Delete(Thread.Temp);
                                    }
                                }
                                Finish();
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            RetryCount += 1;
                            if (State > NetState.Merging)
                                return; // 通常是因为在合并时被中断了
                            Logger.Warn(ex, $"合并文件出错，第 {RetryCount} 次尝试（{LocalName}）");
                            // 重新尝试合并
                            if (RetryCount < 3)
                            {
                                System.Threading.Thread.Sleep(500 * RetryCount);
                                continue;
                            }
                            // 失败，禁用当前下载源并重启下载
                            try
                            {
                                FileUtils.Delete(LocalPath);
                            }
                            catch (Exception)
                            {
                                Logger.Warn(ex, $"删除合并失败的文件出错（{LocalPath}）");
                            }
                            SourceFail(Th, ex, true);
                            break;
                        }
                    }
                }
                finally
                {
                    Cache?.Dispose();
                }
            }
            /// <summary>
        /// 下载失败。
        /// </summary>
            private void Fail(Exception RaiseEx = null)
            {
                lock (LockState)
                {
                    if (State >= NetState.Finished)
                        return;
                    if (RaiseEx is not null)
                        Ex.Add(RaiseEx);
                    // 凉凉
                    Logger.Info($"{LocalName}：已失败，当前状态 {State}");
                    State = NetState.Canceled;
                }
                CancelInternal();
                foreach (var Task in Tasks)
                    Task.OnFileFail(this);
            }
            /// <summary>
        /// 取消下载。
        /// </summary>
            public void Cancel(LoaderDownload CanceledTask)
            {
                // 从特定任务中移除，如果它还属于其他任务，则继续下载
                Tasks.Remove(CanceledTask);
                if (Tasks.Any())
                    return;
                // 确认中断
                lock (LockState)
                {
                    if (State >= NetState.Finished)
                        return;
                    Logger.Info($"{LocalName}：已中断，当前状态 {State}");
                    State = NetState.Canceled;
                }
                CancelInternal();
            }
            private void CancelInternal()
            {
                // 原 VB 為 On Error Resume Next：出錯即跳過該語句
                try { Reset(); } catch { }
                Interlocked.Decrement(ref NetManager.FileRemain);
            }

            // 状态改变接口
            /// <summary>
        /// 将该文件设置为已下载完成。
        /// </summary>
            public void Finish(bool PrintLog = true)
            {
                lock (LockState)
                {
                    if (State >= NetState.Finished)
                        return;
                    State = NetState.Finished;
                }
                Interlocked.Decrement(ref NetManager.FileRemain);
                if (PrintLog)
                    Logger.Info($"{LocalName}：已完成");
                foreach (var Task in Tasks)
                    Task.OnFileFinish(this);
            }

        }
        private class RangeNotSupportedException : HttpRequestException
        {
            public RangeNotSupportedException(string Message) : base(Message)
            {
            }
        }
        // 根据类别进行查找
        private static ConcurrentDictionary<string, long> _CheckExistingFile_Sizes = new ConcurrentDictionary<string, long>();
        private static Func<string, FileChecker, bool> _CheckExistingFile_CheckCandidate = (Candidate, Checker) =>
    {
        // 快速进行大小校验
        long Length = _CheckExistingFile_Sizes.GetOrAdd(Candidate, c => FileUtils.Exists(c) ? FileUtils.GetInfo(c).Length : -1);
        if (Checker.ActualSize != Length)
            return false;
        // 进一步进行 Hash 校验
        return Checker.Check(Candidate) is null;
    };
        /// <summary>
    /// 下载一系列文件的加载器。
    /// </summary>
        public class LoaderDownload : ModLoader.LoaderBase
        {

            #region 属性

            /// <summary>
        /// 需要下载的文件。
        /// </summary>
            public ConcurrentQueue<NetFile> Files;
            /// <summary>
        /// 剩余未完成的文件数。（用于减轻 FilesLock 的占用）
        /// </summary>
            private int FileRemain;
            private readonly object FileRemainLock = new object();

            /// <summary>
        /// 用于显示的百分比进度。
        /// </summary>
            public override double Progress
            {
                get
                {
                    if (State >= ModBase.LoadState.Finished)
                        return 1d;
                    if (!Files.Any())
                        return 0d; // 必须返回 0，否则在获取列表的时候会错觉已经下载完了
                    return _Progress;
                }
                set
                {
                    throw new Exception("文件下载不允许指定进度");
                }
            }
            private double _Progress = 0d;

            /// <summary>
        /// 任务中的文件的连续失败计数。
        /// </summary>
            public int FailCount
            {
                get
                {
                    return _FailCount;
                }
                set
                {
                    _FailCount = value;
                    if (State == ModBase.LoadState.Loading && value >= Math.Min(10000, Math.Max(FileRemain * 8, NetTaskThreadLimit * 8 + 3)))
                    {
                        Logger.Error($"由于同加载器中失败次数过多引发强制失败：连续失败了 {value} 次", LogBehavior.ToastIfDebug);
                        // 收集最多 11 个下载源的错误；集合在收集时被修改等异常不影响后续的失败处理
                        var ExList = new List<Exception>();
                        try
                        {
                            ExList.AddRange(Files.SelectMany(File => File.Sources).Select(Source => Source.Ex).Where(SourceEx => SourceEx is not null).Take(11));
                        }
                        catch { }
                        OnFail(ExList);
                    }
                }
            }
            private int _FailCount = 0;

            #endregion

            /// <summary>
        /// 刷新公开属性。由 NetManager 每 0.1 秒调用一次。
        /// </summary>
            public void RefreshStat()
            {
                // 计算进度
                double NewProgress = 0d;
                double TotalProgress = 0d;
                foreach (var File in Files)
                {
                    if (File.IsCopy)
                    {
                        NewProgress += File.Progress * 0.2d;
                        TotalProgress += 0.2d;
                    }
                    else
                    {
                        NewProgress += File.Progress;
                        TotalProgress += 1d;
                    }
                }
                if (TotalProgress > 0d && !double.IsNaN(TotalProgress))
                    NewProgress /= TotalProgress;
                // 刷新进度
                _Progress = NewProgress;
            }

            public LoaderDownload(string Name, IEnumerable<NetFile> FileTasks)
            {
                this.Name = Name;
                Files = new ConcurrentQueue<NetFile>(FileTasks);
            }
            public override void Start(object Input = null, bool IsForceRestart = false)
            {
                if (Input is not null)
                    Files = new ConcurrentQueue<NetFile>((IEnumerable<NetFile>)Input);
                // 去重
                Files = new ConcurrentQueue<NetFile>(Files.DistinctBy(a => PathUtils.ForCompare(a.LocalPath).Lower()));
                // 设置剩余文件数
                lock (FileRemainLock)
                    FileRemain += Files.Where(f => f.State != NetState.Finished).Count();
                State = ModBase.LoadState.Loading;
                // 开始执行
                // 输入检测
                // 接入下载管理器，获取新开始下载的文件列表


                // ====================================
                // 已存在文件查找
                // ====================================

                // 整理允许进行查找的文件
                // 获取 MC 文件夹列表
                // 平均分配到多个检查线程
                ModBase.RunInDedicatedThread(() => { try { if (!Files.Any()) { OnFinish(); return; } foreach (NetFile File in Files) { if (File is null) throw new ArgumentException("存在空文件请求！"); foreach (NetSource Source in File.Sources) { if (!(Source.Url.StartsWithF("https://", true) || Source.Url.StartsWithF("http://", true))) { Source.Ex = new ArgumentException("输入的下载链接不正确：" + Source.Url); Source.IsFailed = true; } } if (!File.HasAvailableSource()) throw new ArgumentException("输入的下载链接不正确！"); File.LocalPath = File.LocalPath.Replace("/", @"\"); if (!File.LocalPath.Contains(@":\")) throw new ArgumentException("输入的本地文件地址不正确：" + File.LocalPath); if (File.LocalPath.EndsWithF(@"\")) throw new ArgumentException("请输入含文件名的完整文件路径：" + File.LocalPath); DirectoryUtils.Create(PathUtils.RemoveLastPart(File.LocalPath)); } var NewFiles = NetManager.Start(this); var FilesToCheck = new List<NetFile>(); foreach (var File in NewFiles) { if (File.Check is not null) { FilesToCheck.Add(File); } else { lock (LockState) { File.State = NetState.WaitingToDownload; File.IsCopy = false; } } } if (!FilesToCheck.Any()) return; var Folders = new List<string>(); Folders.Add(Paths.AppData + @".minecraft\"); Folders.AddRange(ModMinecraft.McFolderList.Select(f => f.Location)); Folders = Folders.Distinct().Where(f => DirectoryUtils.Exists(f)).ToList(); int ThreadCount = (FilesToCheck.Count / 40).Clamp(1, 8); if (ThreadCount == 1) { CheckExistingFiles(FilesToCheck, Folders); } else { int BaseSize = FilesToCheck.Count / ThreadCount; int Remainder = FilesToCheck.Count % ThreadCount; int Index = 0; for (int i = 0, loopTo = ThreadCount - 1; i <= loopTo; i++) { int Size = BaseSize + (i < Remainder ? 1 : 0); var ThreadFiles = FilesToCheck.GetRange(Index, Size); Index += Size; ModBase.RunInDedicatedThread(() => CheckExistingFiles(ThreadFiles, Folders), $"下载 文件复制 {Uuid}/{ModBase.GetUuid()}"); } } } catch (Exception ex) { OnFail(new List<Exception>() { new Exception("下载初始化失败", ex) }); } }, "L/下载 " + Uuid); // 创建目标文件夹
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                // 没有校验信息，直接开始下载
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                // 总是添加官启文件夹，因为 HMCL 会把所有文件存在这里
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                // 每个线程至少 40 个文件，最多 8 线程
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                // 只有一个线程，直接执行
            }
            private void CheckExistingFiles(List<NetFile> Files, List<string> FolderList)
            {
                try
                {
                    Logger.Trace(() => $"文件检查开始，本线程负责 {Files.Count} 个文件，首个文件为 {Files.FirstOrDefault()?.LocalName}");
                    // 列出 MC 文件夹中的各个版本文件夹
                    var VersionFolders = new List<string>();
                    foreach (var McFolder in FolderList)
                    {
                        foreach (var VersionFolder in DirectoryUtils.EnumerateDirectories(McFolder + @"versions\"))
                            VersionFolders.Add(PathUtils.AddSlashSuffix(VersionFolder));
                    }
                    // 处理每个文件
                    foreach (NetFile File in Files)
                    {
                        string Target = CheckExistingFile(FolderList, VersionFolders, File);
                        Logger.Trace(() => $"本地文件匹配：{File.LocalName} → {Target ?? "无匹配"}，当前状态 {File.State}");
                        if (File.State >= NetState.WaitingToDownload)
                            continue; // 中断
                                      // 已找到相同文件
                        if (Target is not null)
                        {
                            File.IsCopy = true;
                            try
                            {
                                // 同一磁盘内用硬链接共用同一份内容，不重复占用空间；否则复制
                                if (!string.Equals(Target, File.LocalPath, StringComparison.OrdinalIgnoreCase))
                                    FileLinker.LinkOrCopy(Target, File.LocalPath);
                                File.Finish(false);
                                continue;
                            }
                            catch
                            {
                            }
                        }
                        // 回退到下载
                        lock (LockState)
                        {
                            File.State = NetState.WaitingToDownload;
                            File.IsCopy = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnFail(new List<Exception>() { new Exception("下载已存在文件查找失败", ex) });
                }
            }
            private string CheckExistingFile(List<string> FolderList, List<string> VersionFolders, NetFile File)
            {
                // 目标文件已存在
                if (File.Check.Check(File.LocalPath) is null)
                    return File.LocalPath;
                // 在设置中禁用了复制
                if (Settings.Get<bool>("SystemDebugSkipCopy"))
                    return null;
                // 没有可用的检查规则，只能开始下载
                if (File.Check.Hash is null && File.Check.ActualSize < 0L)
                    return null;
                // 大致判断文件类别
                var TypeIndexes = new[] { @"\assets\", @"\libraries\", @"\versions\", @"\mods\", @"\coremods\", @"\lib\", @"\resourcepacks\", @"\texturepacks\", @"\shaderpacks\" }.Select(FolderName => (FolderName, File.LocalPath.IndexOfF(FolderName, true))).Where(kv => kv.Item2 >= 0).ToList();
                if (!TypeIndexes.Any())
                {
                    if (PathUtils.GetExtension(File.LocalName) == "jar")
                    {
                        TypeIndexes.Add((@"\versions\", 1)); // 总是对 jar 进行版本文件检查，以包括另存为 jar 的情况
                    }
                    else
                    {
                        return null;
                    }
                }
                string Type = TypeIndexes.MaxBy(kv => kv.Item2).FolderName.TrimStart('\\');
                switch (Type ?? "")
                {
                    case @"assets\":
                    case @"libraries\":
                        {
                            // assets/libraries：查找 MC 文件夹下的相同路径
                            foreach (var Folder in FolderList)
                            {
                                string Candidate = Folder + Type + File.LocalPath.AfterFirst(Type);
                                if (_CheckExistingFile_CheckCandidate(Candidate, File.Check))
                                    return Candidate;
                            }
                            return null;
                        }
                    case @"versions\":
                        {
                            // 版本 jar 或 json：查找 MC 文件夹下的各个版本文件夹
                            foreach (var VersionFolder in VersionFolders)
                            {
                                foreach (var Candidate in DirectoryUtils.EnumerateFiles(VersionFolder, searchPattern: "*." + PathUtils.GetExtension(File.LocalPath)))
                                {
                                    if (_CheckExistingFile_CheckCandidate(Candidate, File.Check))
                                        return Candidate;
                                }
                            }

                            break;
                        }

                    default:
                        {
                            // 社区资源
                            if (File.Check.ActualSize < 0L || File.Check.Hash is null)
                                return null; // 必须要求指定了文件大小和 Hash
                            foreach (var Folder in FolderList.Concat(VersionFolders))
                            {
                                foreach (var Candidate in DirectoryUtils.EnumerateFiles(Folder + Type))
                                {
                                    if (_CheckExistingFile_CheckCandidate(Candidate, File.Check))
                                        return Candidate;
                                }
                            }

                            break;
                        }
                }
                return null;
            }

            public void OnFileFinish(NetFile File)
            {
                // 要求全部文件完成
                lock (FileRemainLock)
                {
                    FileRemain -= 1;
                    if (FileRemain > 0)
                        return;
                }
                OnFinish();
            }
            public void OnFinish()
            {
                RaisePreviewFinish();
                lock (LockState)
                {
                    if (State > ModBase.LoadState.Loading)
                        return;
                    State = ModBase.LoadState.Finished;
                }
            }
            public void OnFileFail(NetFile File)
            {
                // 将下载源的错误加入主错误列表
                foreach (var Source in File.Sources)
                {
                    if (!(Source.Ex == null))
                        File.Ex.Add(Source.Ex);
                }
                OnFail(File.Ex);
            }
            public override void Failed(Exception Ex)
            {
                OnFail(new List<Exception>() { Ex });
            }
            public void OnFail(List<Exception> ExList)
            {
                lock (LockState)
                {
                    if (State >= ModBase.LoadState.Finished)
                        return;
                    if (ExList is null || !ExList.Any())
                        ExList = new List<Exception>() { new Exception("未知错误！") };
                    // 寻找有效的错误信息
                    var UsefulExs = ExList.Where(e => !e.IsCanceled() && !(e is TimeoutException)).ToList();
                    Error = UsefulExs.FirstOrDefault() ?? ExList.FirstOrDefault();
                    // 获取实际失败的文件
                    foreach (var File in Files)
                    {
                        if (File.State != NetState.Canceled)
                            continue;
                        if (File.Sources.All(s => s.Ex.IsCanceled() || s.Ex is TimeoutException))
                            continue;
                        string Detail = Enumerable.Select<NetSource, string>((IEnumerable<NetSource>)File.Sources, s => $"{(s.Ex is null ? "无错误信息。" : ExceptionExtensions.GetDisplay(s.Ex, false))}（{s.Url}）").Join(ModBase.CrLf);
                        Error = new Exception("文件下载失败：" + File.LocalPath + ModBase.CrLf + "各下载源的错误如下：" + ModBase.CrLf + Detail, Error);
                        break;
                    }
                    // 在设置 Error 对象后再更改为失败，避免 WaitForExit 无法捕获错误
                    State = ModBase.LoadState.Failed;
                }
                // 中断所有文件
                foreach (var TaskFile in Files)
                    TaskFile.Cancel(this);
                // 在退出同步锁后再进行日志输出
                Logger.Error(new AggregateException(ExList), "下载加载器失败", LogBehavior.None);
            }
            public override void Cancel()
            {
                lock (LockState)
                {
                    if (State >= ModBase.LoadState.Finished)
                        return;
                    State = ModBase.LoadState.Canceled;
                }
                Logger.Info($"{Name} 已取消！");
                // 中断所有文件
                foreach (var TaskFile in Files)
                    TaskFile.Cancel(this);
            }

        }

        public static NetManagerClass NetManager = new NetManagerClass();
        #region 刷新整体速度
        // 计算瞬时速度
        private static List<long> _RefreshStat_SpeedLast = new List<long>(); // 记录至多最近 30 次下载速度的记录，较新的在前面
                                                                             // 上次记速时的已下载大小
        private static long _RefreshStat_SpeedLastDone = 0L;
        private static bool _StartManager_IsStarted = false;
        /// <summary>
    /// 下载文件管理。
    /// </summary>
        public class NetManagerClass
        {

            #region 属性

            /// <summary>
        /// 需要下载的文件。为“本地地址 - 文件对象”键值对。
        /// </summary>
            public ConcurrentDictionary<string, NetFile> AllFiles = new ConcurrentDictionary<string, NetFile>();

            /// <summary>
        /// 当前的所有下载任务。
        /// </summary>
            public ConcurrentBag<LoaderDownload> Tasks = new ConcurrentBag<LoaderDownload>();

            /// <summary>
        /// 已下载完成的大小。
        /// </summary>
            public long DownloadDone = 0L;

            /// <summary>
        /// 尚未完成下载的文件数。
        /// </summary>
            public int FileRemain = 0;

            // 这些属性由 RefreshStat 刷新
            /// <summary>
        /// 当前的全局下载速度，单位为 Byte / 秒。
        /// </summary>
            public long Speed = 0L;

            public readonly int Uuid = ModBase.GetUuid();

            #endregion

            /// <summary>
        /// 进度与下载速度由下载管理线程每隔约 0.1 秒刷新一次。
        /// </summary>
            private void RefreshStat()
            {
                try
                {
                    long DeltaTime = ModBase.GetTimeMs() - RefreshStatLast;
                    if (DeltaTime == 0L)
                        return;
                    RefreshStatLast += DeltaTime;
                    double ActualSpeed = Math.Max(0d, (DownloadDone - _RefreshStat_SpeedLastDone) / (DeltaTime / 1000d));
                    _RefreshStat_SpeedLast.Insert(0, (long)Math.Round(ActualSpeed));
                    if (_RefreshStat_SpeedLast.Count >= 31)
                        _RefreshStat_SpeedLast.RemoveAt(30);
                    _RefreshStat_SpeedLastDone = DownloadDone;
                    // 计算用于显示的速度
                    long SpeedSum = 0L;
                    long SpeedDiv = 0L;
                    int Weight = _RefreshStat_SpeedLast.Count;
                    foreach (var SpeedRecord in _RefreshStat_SpeedLast)
                    {
                        SpeedSum += SpeedRecord * Weight;
                        SpeedDiv += Weight;
                        Weight -= 1;
                    }
                    Speed = (long)Math.Round(SpeedDiv > 0L ? SpeedSum / (double)SpeedDiv : 0d);
                    // 计算新的速度下限
                    long Limit = 0L;
                    if (_RefreshStat_SpeedLast.Count >= 10)
                        Limit = (long)Math.Round(_RefreshStat_SpeedLast.Take(10).Average() * 0.85d); // 取近 1 秒的平均速度的 85%
                    if (Limit > NetTaskSpeedLimitLow)
                    {
                        NetTaskSpeedLimitLow = Limit;
                        Logger.Info($"速度下限已提升到 {StringUtils.FormatByteSize(Limit)}");
                    }
                    #endregion
                    #region 刷新下载任务属性
                    foreach (var Task in Tasks)
                        Task.RefreshStat();
                }
                #endregion
                catch (Exception ex)
                {
                    Logger.Warn(ex, "刷新下载公开属性失败");
                }
            }
            private long RefreshStatLast;

            /// <summary>
        /// 启动监控线程，用于新增下载线程。
        /// </summary>
            private void StartManager()
            {
                if (_StartManager_IsStarted)
                    return;
                _StartManager_IsStarted = true;
                // 获取文件列表
                // 为等待中的文件开始线程
                // 为进行中的文件追加线程
                // 线程种类计数
                // 新增线程
                void ThreadStarter(int Id) { try { while (true) { Thread.Sleep(20); var WaitingFiles = new List<NetFile>(); var OngoingFiles = new List<NetFile>(); if (Id == 0 && FileRemain == 0) AllFiles.Clear(); foreach (NetFile File in AllFiles.Values) { if (File.Uuid % 2 == Id) continue; if (File.State == NetState.WaitingToDownload) { WaitingFiles.Add(File); } else if (File.State < NetState.Merging && File.State >= NetState.WaitingToDownload) { OngoingFiles.Add(File); } } bool continueWhile = false; foreach (NetFile File in WaitingFiles) { if (NetTaskThreadCount >= NetTaskThreadLimit) { continueWhile = true; break; } var NewThread = File.TryBeginThread(); } if (continueWhile) { continue; } if (Speed >= NetTaskSpeedLimitLow) continue; bool continueWhile1 = false; foreach (NetFile File in OngoingFiles) { if (NetTaskThreadCount >= NetTaskThreadLimit) { continueWhile1 = true; break; } int PreparingCount = 0; int DownloadingCount = 0; if (File.Threads is not null) { foreach (NetThread Thread in File.Threads.ToList()) { if (Thread.State < NetState.Downloading) { PreparingCount += 1; } else if (Thread.State == NetState.Downloading) { DownloadingCount += 1; } } } if (PreparingCount > DownloadingCount) continue; var NewThread = File.TryBeginThread(); } if (continueWhile1) { continue; } } } catch (Exception ex) { Logger.Error(ex, $"下载管理启动线程 {Id} 出错", LogBehavior.AlertThenCrash); } }; // 0 或 1
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             // 若已完成，则清空
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             // 最大线程数检查
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             // 减少 BMCLAPI 请求频率
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             // 下载速度足够，无需新增
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             // 最大线程数检查
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             // 准备中的线程已多于下载中的线程，不再新增
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             // 减少 BMCLAPI 请求频率
                ModBase.RunInDedicatedThread(() => ThreadStarter(0), "NetManager ThreadStarter 0");
                ModBase.RunInDedicatedThread(() => ThreadStarter(1), "NetManager ThreadStarter 1");
                // 增加限速余量
                // 刷新公开属性
                // 等待 100 ms
                ModBase.RunInDedicatedThread(() => { try { long NextTick = ModBase.GetTimeMs(); while (true) { if (NetTaskSpeedLimitHigh > 0L) Interlocked.Add(ref NetTaskSpeedLimitLeft, (long)Math.Round(NetTaskSpeedLimitHigh / 10d)); RefreshStat(); NextTick += 100L; long SleepTime = NextTick - ModBase.GetTimeMs(); if (SleepTime > 0L) { Thread.Sleep((int)SleepTime); } else { NextTick = ModBase.GetTimeMs(); } } } catch (Exception ex) { Logger.Error(ex, "下载管理刷新线程出错", LogBehavior.AlertThenCrash); } }, "NetManager StatRefresher"); // 超时，直接追帧，不等待
            }

            private object DownloadCacheLock = new object();
            private bool IsDownloadCacheCleared = false;
            /// <summary>
        /// 开始一个下载任务。
        /// </summary>
            public List<NetFile> Start(LoaderDownload Task)
            {
                StartManager();
                // 清理缓存
                lock (DownloadCacheLock) // 防止同时开启多个下载任务时重复清理
                {
                    if (!IsDownloadCacheCleared)
                    {
                        try
                        {
                            Logger.Info("开始清理下载缓存");
                            DirectoryUtils.Delete(ModBase.PathTemp + "Download");
                            Logger.Info("下载缓存已清理");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, "清理下载缓存失败");
                        }
                        IsDownloadCacheCleared = true;
                    }
                }
                // 添加每个文件
                var NewFiles = new List<NetFile>();
                var NewFilesList = new List<NetFile>();
                foreach (var RawFile in Task.Files)
                {
                    var File = RawFile;
                    NetFile OngoingFile = null;
                    if (AllFiles.TryGetValue(File.LocalPath, out OngoingFile) && OngoingFile.State < NetState.Finished)
                    {
                        // 该文件正在被另一个任务下载
                        Logger.Trace(() => $"{File.LocalName}：无需开始，该文件已在下载中，目标 {File.LocalPath}");
                        File = OngoingFile; // 将列表中的文件替换成下载中的文件，即两个任务指向同一个文件；抛弃现在的 File 对象
                    }
                    else
                    {
                        // 常规情况（该文件可能已在其他任务中下载完成，但这并不影响）
                        Logger.Trace(() => $"{File.LocalName}：开始，目标 {File.LocalPath}");
                        AllFiles[File.LocalPath] = File;
                        NewFiles.Add(File);
                        Interlocked.Increment(ref FileRemain);
                    }
                    NewFilesList.Add(File);
                    File.Tasks.Add(Task);
                }
                Task.Files = new ConcurrentQueue<NetFile>(NewFilesList);
                Tasks.Add(Task);
                return NewFiles;
            }

        }

        /// <summary>
    /// 是否有正在进行中、需要在任务管理页面显示的下载任务？
    /// </summary>
        public static bool HasDownloadingTask(bool IgnoreCustomDownload = false)
        {
            return ModLoader.LoaderTaskbar.Any(Task => Task.Show && Task.State == ModBase.LoadState.Loading && (!IgnoreCustomDownload || !Task.Name.ToString().Contains("自定义下载")));
        }

        #endregion

        #region 端口

        #endregion

        /// <summary>
    /// 设置是否应当在正版登录时校验 SSL 证书。
    /// </summary>
        public static void ShouldValidateSslCertificateOnLogin(bool Enabled)
        {
            ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback((Sender, Certificate, Chain, Failure) =>
        {
            HttpWebRequest Request = Sender as HttpWebRequest;
            if (Failure == System.Net.Security.SslPolicyErrors.None)
                return true; // 已通过验证
            // 基于 #3018 和 #5879，只在访问正版登录 API 时跳过证书验证
            Logger.Warn($"未通过 SSL 证书验证（{Failure}），提供的证书为 {Certificate?.Subject}，URL：{Request?.Address}");
            if (Request is null)
            {
                return !Enabled;
            }
            else if (Request.Address.Host.Contains("xboxlive") || Request.Address.Host.Contains("minecraftservices"))
            {
                return !Enabled; // 根据设置决定是否忽略错误
            }
            else
            {
                return false;
            }
        });
        }

    }
}