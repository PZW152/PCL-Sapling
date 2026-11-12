using System;
using System.IO;
using System.Windows.Threading;
using MeloongCore;

namespace PCL
{
    /// <summary>
    /// 桌面快捷方式一键启动某个实例（取自 Sapling）。
    /// 快捷方式以 --launch "实例文件夹" 打开启动器；启动器已经打开时，新进程把请求交给已打开的启动器后退出。
    /// 两种情况都通过 PCL\LaunchRequest.txt 传递请求，由已加载完成的启动器处理。
    /// </summary>
    public static class ModShortcut
    {
        private static string RequestPath => Paths.Base + @"PCL\LaunchRequest.txt";
        private static DispatcherTimer Timer;

        /// <summary>
        /// 启动时调用（在单实例检查之前）：命令行中有启动请求时，把它写入请求文件。
        /// </summary>
        public static void AcceptCommandLine(string[] Args)
        {
            string Folder = LaunchRequest.Parse(Args);
            if (Folder is null)
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RequestPath));
                File.WriteAllText(RequestPath, Folder);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "保存快捷方式的启动请求失败");
            }
        }

        /// <summary>
        /// 主窗口加载后调用：每秒检查一次是否有启动请求。
        /// </summary>
        public static void StartWatching()
        {
            Timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, __) => TryHandleRequest(), System.Windows.Application.Current.Dispatcher);
            Timer.Start();
        }

        private static void TryHandleRequest()
        {
            if (!File.Exists(RequestPath))
                return;
            // 等到启动页面准备好、并且没有正在启动的游戏
            if (ModMain.FrmLaunchLeft is null || ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading)
                return;
            string Folder;
            try
            {
                Folder = File.ReadAllText(RequestPath).Trim();
                File.Delete(RequestPath);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取快捷方式的启动请求失败");
                return;
            }
            var Instance = new ModMinecraft.McInstance(Folder);
            if (!Directory.Exists(Folder) || !File.Exists(Instance.PathVersion + Instance.Name + ".json"))
            {
                ModMain.Hint($"快捷方式指向的版本已不存在：{Folder}", ModMain.HintType.Red);
                return;
            }
            Logger.Info($"从快捷方式启动：{Folder}");
            if (ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions { Instance = Instance }))
                ModMain.Hint($"正在启动 {Instance.Name}……");
        }

        /// <summary>
        /// 在桌面创建一键启动该实例的快捷方式。
        /// </summary>
        public static void CreateDesktopShortcut(ModMinecraft.McInstance Instance)
        {
            try
            {
                string Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string LinkPath = Path.Combine(Desktop, ShellLink.SafeFileName(Instance.Name) + ".lnk");
                ShellLink.Create(LinkPath, new ShellLink.Info(
                    ModBase.PathExe,
                    LaunchRequest.BuildArguments(Instance.PathVersion),
                    Path.GetDirectoryName(ModBase.PathExe),
                    $"使用 {ModBase.ProductShortName} 启动 {Instance.Name}",
                    ModBase.PathExe));
                ModMain.Hint($"已在桌面创建快捷方式：{Instance.Name}", ModMain.HintType.Green);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "创建桌面快捷方式失败", LogBehavior.Toast);
            }
        }
    }
}
