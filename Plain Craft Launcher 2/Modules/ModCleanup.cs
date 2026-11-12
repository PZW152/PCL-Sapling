using System;
using System.IO;
using System.Linq;
using MeloongCore;
using MeloongCore.Extensions;
using Newtonsoft.Json.Linq;

namespace PCL
{
    /// <summary>
    /// 清理当前游戏文件夹中没有任何版本在用的共用文件（取自 Sapling）。
    /// 规则在 PCLCS.GameFileCleanup：只清理资源与支持库，看不懂任何一个版本就整个放弃。
    /// </summary>
    public static class ModCleanup
    {
        public static void Start()
        {
            if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading || ModLoader.LoaderTaskbar.Any(l => l.State == ModBase.LoadState.Loading))
            {
                ModMain.Hint("正在启动游戏或下载文件，请等它结束后再清理", ModMain.HintType.Red);
                return;
            }
            string McFolder = ModMinecraft.McFolderSelected;
            if (string.IsNullOrEmpty(McFolder))
                return;
            ModMain.Hint("正在检查可以清理的文件……");
            ModBase.RunInDedicatedThread(() =>
            {
                try
                {
                    var Keep = CollectKeep(McFolder, out string Problem, out bool LibrariesSkipped);
                    if (Keep is null)
                    {
                        ModMain.MyMsgBox(Problem, "无法清理");
                        return;
                    }
                    var Plan = GameFileCleanup.Plan(McFolder, Keep);
                    if (!Plan.Files.Any())
                    {
                        ModMain.Hint("没有需要清理的文件", ModMain.HintType.Green);
                        return;
                    }
                    string Size = StringUtils.FormatByteSize(Plan.Bytes);
                    if (ModMain.MyMsgBox($"有 {Plan.Files.Count} 个文件没有任何版本在用，共 {Size}。" + ModBase.CrLf +
                                         "这些是共用的资源和支持库，不包括存档、Mod 和版本本身；以后需要时会重新下载。" +
                                         (LibrariesSkipped ? ModBase.CrLf + ModBase.CrLf + "因为有 Forge 或 NeoForge 版本，支持库不会被清理（它们在安装时会生成额外的文件）。" : ""),
                                         "清理游戏文件", "清理", "取消") != 1)
                        return;
                    long Freed = GameFileCleanup.Run(McFolder, Plan);
                    Logger.Info($"清理游戏文件：计划 {Plan.Files.Count} 个，共 {Plan.Bytes} 字节，实际清出 {Freed} 字节");
                    ModMain.Hint($"已清出 {StringUtils.FormatByteSize(Freed)}", ModMain.HintType.Green);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "清理游戏文件失败", LogBehavior.Alert);
                }
            }, "Game File Cleanup");
        }

        /// <summary>
        /// 收集各版本仍在使用的文件。有版本读不懂时返回 null，并说明原因。
        /// </summary>
        private static GameFileCleanup.Keep CollectKeep(string McFolder, out string Problem, out bool LibrariesSkipped)
        {
            Problem = null;
            LibrariesSkipped = false;
            var Keep = new GameFileCleanup.Keep();
            string LibrariesRoot = McFolder + @"libraries\";
            var Folders = Directory.Exists(McFolder + "versions") ? Directory.GetDirectories(McFolder + "versions") : new string[0];
            var Instances = Folders.Where(f => File.Exists(Path.Combine(f, Path.GetFileName(f) + ".json"))).ToList();
            if (!Instances.Any())
            {
                Problem = "这个游戏文件夹中没有版本。为了避免误删，没有版本时不清理。";
                return null;
            }
            foreach (var Folder in Instances)
            {
                var Instance = new ModMinecraft.McInstance(Folder + @"\").Load();
                if (Instance.State == ModMinecraft.McInstance.McInstanceState.Error)
                {
                    Problem = $"版本 {Instance.Name} 存在问题（{Instance.Info}），无法确定它需要哪些文件。为了避免误删，没有进行清理。";
                    return null;
                }
                if (Instance.Version.ModLoaders.HasFlagF(ModLoaders.Forge) || Instance.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge))
                {
                    Keep.Libraries = null;
                    LibrariesSkipped = true;
                }
                if (Keep.Libraries is not null)
                {
                    foreach (var Library in ModMinecraft.McLibListGet(Instance, true))
                    {
                        if (Library.LocalPath is not null && Library.LocalPath.StartsWith(LibrariesRoot, StringComparison.OrdinalIgnoreCase))
                            Keep.AddLibrary(Library.LocalPath.Substring(LibrariesRoot.Length));
                    }
                }
                var Index = ModMinecraft.McAssetsGetIndex(Instance, ReturnLegacyOnError: true);
                string Id = Index?["id"]?.ToString();
                if (string.IsNullOrEmpty(Id))
                    continue;
                string IndexPath = McFolder + @"assets\indexes\" + Id + ".json";
                if (File.Exists(IndexPath))
                    Keep.AddAssetIndex(Id, (JObject)FileUtils.ReadAsString(IndexPath).DeserializeJson());
                else
                    Keep.AssetIndexes.Add(Id);
            }
            return Keep;
        }
    }
}
