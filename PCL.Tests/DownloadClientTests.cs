using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace PCL.Tests;

public class DownloadClientTests {
    [Test]
    public async Task 本机HTTP失败或取消时保留原文件() {
        string folder = Path.Combine(Path.GetTempPath(), "PclDownload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, "client.jar");
        File.WriteAllText(target, "old");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        try {
            listener.Start();
            var url = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/client.jar");
            async Task<byte[]> Receive(CancellationToken cancel) {
                using var response = await client.GetAsync(url, cancel);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }

            Task rejected = Reply(listener, "503 Service Unavailable", "try again");
            await Assert.That(() => DownloadOutput.SaveAsync(target, Receive, CancellationToken.None)).Throws<HttpRequestException>();
            await rejected;
            await Assert.That(File.ReadAllText(target)).IsEqualTo("old");

            using var canceled = new CancellationTokenSource();
            Task interrupted = Reply(listener, "200 OK", "not published");
            await Assert.That(() => DownloadOutput.SaveAsync(target, async cancel => {
                byte[] data = await Receive(cancel);
                canceled.Cancel();
                return data;
            }, canceled.Token)).Throws<OperationCanceledException>();
            await interrupted;
            await Assert.That(File.ReadAllText(target)).IsEqualTo("old");

            Task accepted = Reply(listener, "200 OK", "new");
            await DownloadOutput.SaveAsync(target, Receive, CancellationToken.None);
            await accepted;
            await Assert.That(File.ReadAllText(target)).IsEqualTo("new");
            await Assert.That(Directory.GetFiles(folder)).IsEquivalentTo(new[] { target });
        } finally {
            listener.Stop();
            Directory.Delete(folder, true);
        }
    }

    private static async Task Reply(TcpListener listener, string status, string body) {
        using var connection = await listener.AcceptTcpClientAsync();
        using var stream = connection.GetStream();
        using var request = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        while (!string.IsNullOrEmpty(await request.ReadLineAsync())) { }
        byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
        await stream.WriteAsync(response, 0, response.Length);
    }
}
