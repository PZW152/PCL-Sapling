using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MeloongCore;
using Newtonsoft.Json.Linq;

namespace PCL
{
    /// <summary>
    /// 性能基准（#13）：设置环境变量 PCL_BENCHMARK 为一个文件路径时，记录本次启动的各阶段耗时、内存与线程数，写入该文件后退出。
    /// 正常使用时完全不运行，也不会上传任何数据。配合 tools/benchmark.ps1 重复运行并统计中位数与 P95。
    /// </summary>
    public static class ModBenchmark
    {
        public static string OutputPath { get; } = Environment.GetEnvironmentVariable("PCL_BENCHMARK");
        public static bool Enabled => !string.IsNullOrEmpty(OutputPath);

        /// <summary>
        /// 主窗口加载完成时调用：记录到这一刻的耗时，然后在后台等启动按钮可用、再完整扫描一次版本文件夹。
        /// </summary>
        public static void OnWindowLoaded()
        {
            if (!Enabled)
                return;
            var Process_ = Process.GetCurrentProcess();
            double WindowMs = (DateTime.Now - Process_.StartTime).TotalMilliseconds;
            ModBase.RunInDedicatedThread(() =>
            {
                var Result = new JObject { ["windowReadyMs"] = Math.Round(WindowMs) };
                try
                {
                    // 启动按钮可用：首页显示了当前版本（正常启动会直接用缓存的版本列表）
                    var Watch = Stopwatch.StartNew();
                    bool Ready = false;
                    while (Watch.ElapsedMilliseconds < 60000 && !Ready)
                    {
                        ModBase.RunInUiWait(() => Ready = ModMain.FrmLaunchLeft?.BtnLaunch is { } Button && Button.Text != "正在加载");
                        if (!Ready) Thread.Sleep(20);
                    }
                    Process_.Refresh();
                    Result["launchReadyMs"] = Math.Round((DateTime.Now - Process_.StartTime).TotalMilliseconds);
                    // 完整扫描一次版本文件夹（不使用缓存）
                    var Scan = Stopwatch.StartNew();
                    ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                    Thread.Sleep(50);
                    while (Scan.ElapsedMilliseconds < 60000 && (ModMinecraft.McInstanceListLoader.State == ModBase.LoadState.Loading || ModMinecraft.McInstanceListLoader.State == ModBase.LoadState.Waiting))
                        Thread.Sleep(10);
                    Result["instanceScanMs"] = Scan.ElapsedMilliseconds;
                    Result["instanceCount"] = ModMinecraft.McInstanceList?.Sum(Group => Group.Value.Count) ?? 0;
                    Thread.Sleep(1000); // 让启动时的后台工作告一段落再取内存与线程数
                    Process_.Refresh();
                    Result["workingSetMb"] = Math.Round(Process_.WorkingSet64 / 1024d / 1024d, 1);
                    Result["privateMb"] = Math.Round(Process_.PrivateMemorySize64 / 1024d / 1024d, 1);
                    Result["threads"] = Process_.Threads.Count;
                    Result["cpuMs"] = Math.Round(Process_.TotalProcessorTime.TotalMilliseconds);
                    Result["version"] = ModBase.VersionDisplay;
                }
                catch (Exception ex)
                {
                    Result["error"] = ex.Message;
                }
                File.WriteAllText(OutputPath, Result.ToString());
                Logger.Info("性能基准：" + Result.ToString(Newtonsoft.Json.Formatting.None));
                ModBase.RunInUi(() => FormMain.EndProgramForce());
            }, "Benchmark");
        }
    }
}
