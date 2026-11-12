using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MeloongCore;
using MeloongCore.Extensions;
using Newtonsoft.Json.Linq;

namespace PCL
{
    /// <summary>
    /// 启动器自动更新：从本仓库的 GitHub Releases 获取新版本，校验 SHA-256 后替换并重新启动。
    /// </summary>
    public static class ModUpdate
    {
        private static int Checking = 0;

        /// <summary>
        /// 启动时调用：清理上次更新留下的旧版本，并在正式版中按设置检查更新。
        /// </summary>
        public static void OnStartup()
        {
            ModBase.RunInBackground(() => AppUpdate.RemoveOldVersion(ModBase.PathExe), "清理旧版本");
            if (ModBase.BuildType != ModBase.BuildTypes.Release || !Settings.Get<bool>("SystemAutoUpdate"))
                return;
            ModBase.RunInBackgroundAsync(async () =>
            {
                await Task.Delay(8000); // 不和启动时的加载抢网络
                await CheckAsync(false);
            }, "自动检查更新");
        }

        /// <summary>
        /// 检查更新。手动检查时，没有更新或检查失败都会提示。
        /// </summary>
        public static async Task CheckAsync(bool Manual)
        {
            if (Interlocked.Exchange(ref Checking, 1) == 1)
            {
                if (Manual)
                    ModMain.Hint("正在检查更新，请稍候……");
                return;
            }
            try
            {
                if (Manual)
                    ModMain.Hint("正在检查更新……");
                JObject Release = (JObject)(await ModNet.NetRequestByClientAsync(AppUpdate.LatestReleaseApi, Accept: "application/vnd.github+json", Timeout: 15000, RequireJson: true)).DeserializeJson();
                var Update = AppUpdate.FromRelease(Release, ModBase.VersionBaseName);
                if (Update is null)
                {
                    Logger.Info($"没有可用的更新（当前 {ModBase.VersionBaseName}，最新 {Release["tag_name"]}）");
                    if (Manual)
                        ModMain.Hint($"已是最新版本（{ModBase.VersionBaseName}）！", ModMain.HintType.Green);
                    return;
                }
                Logger.Info($"发现新版本 {Update.Version}");
                string Notes = Update.Notes.Trim();
                if (Notes.Length > 800)
                    Notes = Notes.Substring(0, 800) + "……";
                if (ModMain.MyMsgBox($"发现新版本 {Update.Version}（当前为 {ModBase.VersionBaseName}）。" + (Notes.Length == 0 ? "" : ModBase.CrLf + ModBase.CrLf + Notes),
                                     "发现新版本", "立即更新", "以后再说") != 1)
                    return;
                await InstallAsync(Update);
            }
            catch (HttpRequestCodeException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // 还没有发布过任何版本
                Logger.Info("仓库中还没有发布的版本");
                if (Manual)
                    ModMain.Hint($"已是最新版本（{ModBase.VersionBaseName}）！", ModMain.HintType.Green);
            }
            catch (Exception ex)
            {
                if (ex.IsCanceled())
                    return;
                Logger.Warn(ex, "检查更新失败");
                if (Manual)
                    ModMain.Hint("检查更新失败：" + ex.Message, ModMain.HintType.Red);
            }
            finally
            {
                Interlocked.Exchange(ref Checking, 0);
            }
        }

        private static async Task InstallAsync(UpdateInfo Update)
        {
            ModMain.Hint($"正在下载新版本 {Update.Version}……");
            string Temp = ModMain.RequestTaskTempFolder() + "update.exe";
            await ModNet.NetDownloadByClientAsync(Update.Url, Temp);
            byte[] Content = File.ReadAllBytes(Temp);
            FileUtils.Delete(Temp);
            if (!AppUpdate.Verify(Content, Update.Sha256))
                throw new Exception("下载的新版本校验失败，可能已损坏，请稍后再试");
            AppUpdate.Install(ModBase.PathExe, Content);
            Logger.Info($"已更新至 {Update.Version}，正在重新启动");
            Process.Start(new ProcessStartInfo(ModBase.PathExe) { WorkingDirectory = Path.GetDirectoryName(ModBase.PathExe), UseShellExecute = true });
            ModBase.RunInUi(() => FormMain.EndProgramForce());
        }
    }
}
