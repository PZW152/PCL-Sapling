using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf.Extensions;
using Newtonsoft.Json.Linq;
using PCLCS;

namespace PCL
{
    public static class ModDownload
    {

        #region DlClient* | Minecraft 客户端

        /// <summary>
    /// 返回某 Minecraft 版本对应的原版主 Jar 文件的下载信息，要求对应依赖版本已存在。
    /// 失败则抛出异常，不需要下载则返回 Nothing。
    /// </summary>
        public static ModNet.NetFile DlClientJarGet(ModMinecraft.McInstance Instance, bool ReturnNothingOnFileUseable)
        {
            // 获取底层继承版本
            try
            {
                while (!string.IsNullOrEmpty(Instance.InheritName))
                    Instance = new ModMinecraft.McInstance(Instance.InheritName);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "获取底层继承版本失败");
            }
            // 检查 Json 是否标准
            if (Instance.JsonObject["downloads"] is null || Instance.JsonObject["downloads"]["client"] is null || Instance.JsonObject["downloads"]["client"]["url"] is null)
            {
                throw new Exception("底层版本 " + Instance.Name + " 中无 jar 文件下载信息");
            }
            // 检查文件
            var Checker = new FileChecker() { MinSize = 1024L, ActualSize = (long)(Instance.JsonObject["downloads"]["client"]["size"] ?? -1), Hash = (string)Instance.JsonObject["downloads"]["client"]["sha1"] };
            if (ReturnNothingOnFileUseable && Checker.Check(Instance.PathVersion + Instance.Name + ".jar") is null)
                return null; // 通过校验
                             // 返回下载信息
            string JarUrl = (string)Instance.JsonObject["downloads"]["client"]["url"];
            return new ModNet.NetFile(DlSourceLauncherOrMetaGet(JarUrl), Instance.PathVersion + Instance.Name + ".jar", Checker);
        }

        /// <summary>
    /// 返回某 Minecraft 版本对应的原版主 AssetIndex 文件的下载信息，要求对应依赖版本已存在。
    /// 若未找到，则会返回 Legacy 资源文件或 Nothing。
    /// </summary>
        public static ModNet.NetFile DlClientAssetIndexGet(ModMinecraft.McInstance Instance)
        {
            // 获取底层继承版本
            while (!string.IsNullOrEmpty(Instance.InheritName))
                Instance = new ModMinecraft.McInstance(Instance.InheritName);
            // 获取信息
            var IndexInfo = ModMinecraft.McAssetsGetIndex(Instance, true, true);
            string IndexAddress = ModMinecraft.McFolderSelected + @"assets\indexes\" + IndexInfo["id"].ToString() + ".json";
            Logger.Info($"版本 {Instance.Name} 对应的资源文件索引为 {IndexInfo["id"]}");
            string IndexUrl = (string)(IndexInfo["url"] ?? "");
            if (string.IsNullOrEmpty(IndexUrl))
            {
                return null;
            }
            else
            {
                return new ModNet.NetFile(DlSourceLauncherOrMetaGet(IndexUrl), IndexAddress, new FileChecker() { IsJson = true });
            }
        }

        /// <summary>
    /// 构造补全某 Minecraft 版本的所有文件的加载器列表。失败会抛出异常。
    /// </summary>
        public static List<ModLoader.LoaderBase> DlClientFix(ModMinecraft.McInstance Instance, bool CheckAssetsHash, bool DownloadAssetIndexInBackground)
        {
            var Loaders = new List<ModLoader.LoaderBase>();

            #region 下载支持库文件
            var LoadersLib = new List<ModLoader.LoaderBase>() { new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析缺失支持库文件", (Task) => Task.Output = ModMinecraft.McLibNetFilesFromInstance(Instance)) { ProgressWeight = 1d }, new ModNet.LoaderDownload("下载支持库文件", new List<ModNet.NetFile>()) { ProgressWeight = 15d } };
            // 构造加载器
            Loaders.Add(new ModLoader.LoaderCombo<string>("下载支持库文件（主加载器）", LoadersLib) { Block = false, Show = false, ProgressWeight = 16d });
            #endregion

            #region 下载资源文件
            if (Coerce.ToBool(ModMinecraft.ShouldIgnoreFileCheck(Instance)))
            {
                Logger.Warn("设置要求跳过所有 Assets 检查，文件可能不是最新的", LogBehavior.None);
            }
            else
            {
                var LoadersAssets = new List<ModLoader.LoaderBase>();
                // 获取资源文件索引地址
                string TempAddressMain = null;
                string RealAddressMain = null;
                // 允许后台更新索引
                // 强制要求重新下载
                LoadersAssets.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("分析资源文件索引地址", (Task) => { try { var IndexFile = DlClientAssetIndexGet(Instance); if (IndexFile is null) { Task.Output = new List<ModNet.NetFile>(); Logger.Warn($"未找到版本 {Instance.Name} 的合适的资源索引下载地址，游戏 assets 可能缺失"); return; } string IndexBackgroundFile = ModBase.PathTemp + @"Cache\" + IndexFile.LocalName + "_background"; if (FileUtils.Exists(IndexBackgroundFile) && FileUtils.GetInfo(IndexBackgroundFile).LastWriteTime.Date > FileUtils.GetInfo(IndexFile.LocalPath).LastWriteTime.Date) { Logger.Info($"发现后台更新的资源索引缓存文件：{IndexBackgroundFile}"); FileUtils.Move(IndexBackgroundFile, IndexFile.LocalPath); } if (DownloadAssetIndexInBackground) { if (IndexFile.Check.Check(IndexFile.LocalPath) is null) { Task.Output = new List<ModNet.NetFile>(); } else { Task.Output = new List<ModNet.NetFile>() { IndexFile }; } } else { RealAddressMain = IndexFile.LocalPath; TempAddressMain = ModBase.PathTemp + @"Cache\" + IndexFile.LocalName; IndexFile.LocalPath = TempAddressMain; Task.Output = new List<ModNet.NetFile>() { IndexFile }; } } catch (Exception ex) { throw new Exception("分析资源文件索引地址失败", ex); } }) { ProgressWeight = 0.5d, Show = false });
                // 下载资源文件索引
                LoadersAssets.Add(new ModNet.LoaderDownload("下载资源文件索引", new List<ModNet.NetFile>()) { ProgressWeight = 2d });
                // 获取资源文件地址
                LoadersAssets.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("获取资源文件列表", Task =>
        {
            if (TempAddressMain is not null)
                FileUtils.Move(TempAddressMain, RealAddressMain);
            ModLoader.LoaderBase argProgressFeed = Task;
            Task.Output = ModMinecraft.McAssetsFixList(Instance, CheckAssetsHash, ref argProgressFeed);
            Task = (ModLoader.LoaderTask<string, List<ModNet.NetFile>>)argProgressFeed;
        })
                {
                    ProgressWeight = 0.01d
                });
                // 下载资源文件
                LoadersAssets.Add(new ModNet.LoaderDownload("下载资源文件", new List<ModNet.NetFile>()) { ProgressWeight = 25d });
                // 构造加载器
                Loaders.Add(new ModLoader.LoaderCombo<string>("下载资源文件（主加载器）", LoadersAssets) { Block = false, Show = false, ProgressWeight = 27d });

                // 后台独立更新索引
                if (DownloadAssetIndexInBackground)
                {
                    string RealAddressBg = null;
                    string TempAddressBg = null;
                    var LoadersAssetsUpdate = new List<ModLoader.LoaderBase>();
                    LoadersAssetsUpdate.Add(new ModLoader.LoaderTask<string, List<ModNet.NetFile>>("后台分析资源文件索引地址", (Task) =>
        {
            var IndexFile = DlClientAssetIndexGet(Instance);
            if (IndexFile is null)
            {
                Logger.Warn($"未找到版本 {Instance.Name} 的合适的资源索引下载地址，游戏 assets 可能缺失");
                Task.Cancel();
                throw new OperationCanceledException();
            }
            RealAddressBg = IndexFile.LocalPath;
            TempAddressBg = ModBase.PathTemp + @"Cache\" + IndexFile.LocalName + "_background";
            IndexFile.LocalPath = TempAddressBg;
            Task.Output = new List<ModNet.NetFile>() { IndexFile };
            // 检查是否需要更新：每天只更新一次
            var LastUpdate = FileUtils.GetInfo(RealAddressBg).LastWriteTime.Date;
            if (FileUtils.Exists(RealAddressBg) && Math.Abs((LastUpdate - DateTime.Now.Date).TotalDays) < 1d)
            {
                Logger.Info($"无需更新资源文件索引，上次更新于 {LastUpdate}");
                Task.Cancel();
            }
        }));
                    LoadersAssetsUpdate.Add(new ModNet.LoaderDownload("后台下载资源文件索引", new List<ModNet.NetFile>()));
                    var Updater = new ModLoader.LoaderCombo<string>("后台更新资源文件索引", LoadersAssetsUpdate);
                    Logger.Info("开始后台检查资源文件索引");
                    Updater.Start();
                }
            }
            #endregion

            return Loaders;
        }

        #endregion

        #region DlClientList | Minecraft 客户端 版本列表

        /// <summary>
    /// 所有正式版的 Minecraft Drop 序数。
    /// 若从未完成过获取，返回 Nothing；否则必定存在元素，且从高到低排列。
    /// </summary>
        public static List<int> AllDrops
        {
            get
            {
                if (_AllDrops is null)
                {
                    _AllDrops = Settings.Get<string>("CacheDrops").ToString().Split(",", true).Select(d => (int)Math.Round(ModBase.Val(d))).ToList();
                }
                return _AllDrops.Any() ? _AllDrops : null; // 不要将 _AllDrops 再设为 Nothing，以防止反复获取设置尝试初始化
            }
            set
            {
                _AllDrops = value;
                Settings.Set("CacheDrops", value.Join(','));
            }
        }
        private static List<int> _AllDrops = null;

        // 主加载器
        public struct DlClientListResult
        {
            /// <summary>
        /// 数据来源名称，如“Mojang”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的 Json 数据。
        /// </summary>
            public JObject Value;
        }
        /// <summary>
    /// Minecraft 客户端 版本列表，主加载器。
    /// 若要求镜像源必须包含某个版本，则将该版本 ID 作为输入（#5195）。
    /// </summary>
        public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListLoader = new ModLoader.LoaderTask<string, DlClientListResult>("DlClientList Main", DlClientListMain);
        private static void DlClientListMain(ModLoader.LoaderTask<string, DlClientListResult> Loader)
        {
            // 只使用官方源
            DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>(DlClientListMojangLoader, 60) }, Loader.IsForceRestarting);
            // 提取所有 Drop 序数
            var Drops = new List<int>();
            foreach (JObject Version in Loader.Output.Value["versions"])
            {
                string Id = Version["id"].ToString();
                if (Id.Contains("-"))
                    continue;
                int Drop = McVersion.VersionToDrop(Id);
                if (Drop == 209)
                    continue;
                Drops.Add(Drop);
            }
            AllDrops = Drops.Distinct().OrderByDescending(d => d).ToList();
        }

        // 各个下载源的分加载器
        /// <summary>
    /// Minecraft 客户端 版本列表，Mojang 官方源加载器。
    /// </summary>
        public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListMojangLoader = new ModLoader.LoaderTask<string, DlClientListResult>("DlClientList Mojang", DlClientListMojangMain);
        // MC 更新提示
        private static bool _DlClientListMojangMain_IsHinted = false;
        private static void DlClientListMojangMain(ModLoader.LoaderTask<string, DlClientListResult> Loader)
        {
            long StartTime = ModBase.GetTimeMs();
            JObject Json = (JObject)ModNet.NetRequestByClientRetry("https://launchermeta.mojang.com/mc/game/version_manifest.json", RequireJson: true).DeserializeJson();
            try
            {
                JArray Versions = (JArray)Json["versions"];
                if (Versions.Count < 200)
                    throw new Exception("获取到的版本列表长度不足（" + Json.ToString() + "）");
                Logger.Info($"Mojang 官方源加载耗时：{ModBase.GetTimeMs() - StartTime}ms");
                // 返回
                Loader.Output = new DlClientListResult() { IsOfficial = true, SourceName = "Mojang 官方源", Value = Json };
                string Version;
                // 快照版
                Version = (string)Json["latest"]["snapshot"];
                if (Settings.Get<bool>("ToolUpdateSnapshot") && !string.IsNullOrEmpty(Settings.Get<string>("ToolUpdateSnapshotLast")) && (Settings.Get<string>("ToolUpdateSnapshotLast") ?? "") != (Version ?? "") && !_DlClientListMojangMain_IsHinted)
                {
                    _DlClientListMojangMain_IsHinted = true;
                    ModMinecraft.McDownloadClientUpdateHint(Version, Json);
                }
                Settings.Set("ToolUpdateSnapshotLast", Version ?? "Nothing");
                // 正式版
                Version = (string)Json["latest"]["release"];
                if (Settings.Get<bool>("ToolUpdateRelease") && !string.IsNullOrEmpty(Settings.Get<string>("ToolUpdateReleaseLast")) && (Settings.Get<string>("ToolUpdateReleaseLast") ?? "") != (Version ?? "") && !_DlClientListMojangMain_IsHinted)
                {
                    _DlClientListMojangMain_IsHinted = true;
                    ModMinecraft.McDownloadClientUpdateHint(Version, Json);
                }
                Settings.Set("ToolUpdateReleaseLast", Version);
            }
            catch (Exception ex)
            {
                throw new Exception("Minecraft 官方源版本列表解析失败", ex);
            }
        }

        /// <summary>
    /// 获取某个版本的 Json 下载地址，若失败则返回 Nothing。必须在工作线程执行。
    /// </summary>
        public static object DlClientListGet(string Id)
        {
            try
            {
                // 确认版本格式标准
                Id = Id.Replace("_", "-"); // 1.7.10_pre4 在版本列表中显示为 1.7.10-pre4
                if (Id != "1.0" && Id.EndsWithF(".0"))
                    Id = Coerce.Left(Id, Id.Length - 2); // OptiFine 1.8 的下载会触发此问题，显示版本为 1.8.0
                                                          // 获取 Minecraft 版本列表
                switch (DlClientListLoader.State)
                {
                    case ModBase.LoadState.Finished:
                        {
                            // 从当前的结果获取目标版本…
                            foreach (JObject Version in DlClientListLoader.Output.Value["versions"])
                            {
                                if ((string)Version["id"] == Id)
                                    return Version["url"].ToString();
                            }
                            // …如果没有，则重新尝试获取（在版本刚更新时可能出现这种情况，#5195）
                            DlClientListLoader.WaitForExit(Id, IsForceRestart: true);
                            break;
                        }
                    case ModBase.LoadState.Loading:
                        {
                            DlClientListLoader.WaitForExit(Id);
                            break;
                        }
                    case ModBase.LoadState.Failed:
                    case ModBase.LoadState.Canceled:
                    case ModBase.LoadState.Waiting:
                        {
                            DlClientListLoader.WaitForExit(Id, IsForceRestart: true);
                            break;
                        }
                }
                // 重新查找版本
                foreach (JObject Version in DlClientListLoader.Output.Value["versions"])
                {
                    if ((string)Version["id"] == Id)
                        return Version["url"].ToString();
                }
                Logger.Warn($"未发现版本 {Id} 的 json 下载地址，版本列表返回为：{ModBase.CrLf}{DlClientListLoader.Output.Value}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"获取版本 {Id} 的 json 下载地址失败");
                return null;
            }
        }

        #endregion

        #region DlOptiFineList | OptiFine 版本列表

        public struct DlOptiFineListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的数据。
        /// </summary>
            public List<DlOptiFineListEntry> Value;
        }

        public class DlOptiFineListEntry
        {
            /// <summary>
        /// 显示名称，已去除 HD_U 字样，如“1.12.2 C8”。
        /// </summary>
            public string DisplayName;
            /// <summary>
        /// 原始文件名称，如“preview_OptiFine_1.11_HD_U_E1_pre.jar”。
        /// </summary>
            public string FileName;
            /// <summary>
        /// 对应的版本名称，如“1.13.2-OptiFine_HD_U_E6”。
        /// </summary>
            public string InstanceName;
            /// <summary>
        /// 是否为测试版。
        /// </summary>
            public bool IsPreview;
            /// <summary>
        /// 对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
            public string Inherit
            {
                get
                {
                    return _inherit;
                }
                set
                {
                    if (value.EndsWithF(".0"))
                        value = Coerce.Left(value, value.Length - 2);
                    _inherit = value;
                }
            }
            private string _inherit;
            /// <summary>
        /// 发布时间，格式为“yyyy/mm/dd”。OptiFine 源无此数据。
        /// </summary>
            public string ReleaseTime;
            /// <summary>
        /// 需要的最低 Forge 版本。空字符串为无限制，Nothing 为不兼容，“28.1.56” 表示版本号，“1161” 表示版本号的最后一位。
        /// </summary>
            public string RequiredForgeVersion;
        }

        /// <summary>
    /// OptiFine 版本列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListLoader = new ModLoader.LoaderTask<int, DlOptiFineListResult>("DlOptiFineList Main", DlOptiFineListMain);
        private static void DlOptiFineListMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
        {
            // 只使用官方源
            DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>(DlOptiFineListOfficialLoader, 60) }, Loader.IsForceRestarting);
        }

        /// <summary>
    /// OptiFine 版本列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListOfficialLoader = new ModLoader.LoaderTask<int, DlOptiFineListResult>("DlOptiFineList Official", DlOptiFineListOfficialMain);
        private static void DlOptiFineListOfficialMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
        {
            string Result = ModNet.NetRequestByClientRetry("https://optifine.net/downloads", Encoding: Encoding.Default);
            if (Result.Length < 200)
                throw new Exception("获取到的版本列表长度不足（" + Result + "）");
            try
            {
                // 获取所有版本信息
                var Forge = Result.RegexSearch("(?<=colForge'>)[^<]*").ToList();
                var ReleaseTime = Result.RegexSearch("(?<=colDate'>)[^<]+").ToList();
                var Name = Result.RegexSearch("(?<=OptiFine_)[0-9A-Za-z_.]+(?=.jar\")").ToList();
                if (!(ReleaseTime.Count == Name.Count))
                    throw new Exception("版本与发布时间数据无法对应");
                if (!(Forge.Count == Name.Count))
                    throw new Exception("版本与 Forge 兼容数据无法对应");
                if (ReleaseTime.Count < 10)
                    throw new Exception("获取到的版本数量不足（" + Result + "）");
                // 转化为列表输出
                var Versions = new List<DlOptiFineListEntry>();
                for (int i = 0, loopTo = ReleaseTime.Count - 1; i <= loopTo; i++)
                {
                    Name[i] = Name[i].Replace("_", " ");
                    var Entry = new DlOptiFineListEntry()
                    {
                        DisplayName = Name[i].Replace("HD U ", "").Replace(".0 ", " "),
                        ReleaseTime = new[] { ReleaseTime[i].Split(".")[2], ReleaseTime[i].Split(".")[1], ReleaseTime[i].Split(".")[0] }.Join('/'),
                        IsPreview = Name[i].ContainsIgnoreCase("pre"),
                        Inherit = Name[i].ToString().Split(" ")[0],
                        FileName = (Name[i].ContainsIgnoreCase("pre") ? "preview_" : "") + "OptiFine_" + Name[i].Replace(" ", "_") + ".jar",
                        RequiredForgeVersion = Forge[i].Replace("Forge ", "").Replace("#", "")
                    };
                    if (Entry.RequiredForgeVersion.Contains("N/A"))
                        Entry.RequiredForgeVersion = null;
                    Entry.InstanceName = Entry.Inherit + "-OptiFine_" + Name[i].AfterFirst(" ").Replace(" ", "_");
                    Versions.Add(Entry);
                }
                Loader.Output = new DlOptiFineListResult() { IsOfficial = true, SourceName = "OptiFine 官方源", Value = Versions };
            }
            catch (Exception ex)
            {
                throw new Exception("OptiFine 官方源版本列表解析失败（" + Result + "）", ex);
            }
        }


        #endregion

        #region DlForgeList | Forge Minecraft 版本列表

        public struct DlForgeListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的数据。
        /// </summary>
            public List<string> Value;
        }

        /// <summary>
    /// Forge 版本列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListLoader = new ModLoader.LoaderTask<int, DlForgeListResult>("DlForgeList Main", DlForgeListMain);
        private static void DlForgeListMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
        {
            // 只使用官方源
            DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>(DlForgeListOfficialLoader, 60) }, Loader.IsForceRestarting);
        }

        /// <summary>
    /// Forge 版本列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListOfficialLoader = new ModLoader.LoaderTask<int, DlForgeListResult>("DlForgeList Official", DlForgeListOfficialMain);
        private static void DlForgeListOfficialMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
        {
            string Result = ModNet.NetRequestByClientRetry("https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html", Encoding: Encoding.Default, Accept: "text/html", SimulateBrowserHeaders: true);
            if (Result.Length < 200)
                throw new Exception("获取到的版本列表长度不足（" + Result + "）");
            // 获取所有版本信息
            var Names = Result.RegexSearch("(?<=a href=\"index_)[0-9.]+((_pre|[_-]snapshot[_-]?)[0-9]?)?(?=.html)").ToList();
            Names.Add("1.2.4"); // 1.2.4 不会被匹配上
            if (Names.Count < 10)
                throw new Exception("获取到的版本数量不足（" + Result + "）");
            Loader.Output = new DlForgeListResult() { IsOfficial = true, SourceName = "Forge 官方源", Value = Names };
        }


        #endregion

        #region DlForgeVersion | Forge 版本列表

        public abstract class DlForgelikeEntry : IComparable<DlForgelikeEntry>
        {

            public bool IsNeoForge;
            /// <summary>
        /// 加载器名称。Forge 或 NeoForge。
        /// </summary>
            public abstract string LoaderName { get; }
            /// <summary>
        /// 文件扩展名。不以小数点开头。
        /// </summary>
            public abstract string FileExtension { get; }
            /// <summary>
        /// 标准化后的版本号，仅可用于比较与排序，且 NeoForge 项不包含 alpha/beta 编号！
        /// 格式：Major.Minor.Build.Revision
        /// Forge：如 “50.1.9.0”（最后一位固定为 0）、“14.22.1.2478”（Legacy）。
        /// NeoForge：如 “20.4.30.0”（最后一位固定为 0）、“19.47.1.99”（Legacy：第一位固定为 19）。
        /// </summary>
            public Version Version;
            /// <summary>
        /// 可读的非格式化版本名。
        /// Forge：如 “50.1.9”、“14.22.1.2478”（Legacy）。
        /// NeoForge：如 “20.4.30-beta”、“47.1.99”（Legacy）。
        /// </summary>
            public string VersionName;
            /// <summary>
        /// 对应的 Minecraft 版本，如 1.12.2、26.1-snapshot-1。
        /// </summary>
            public string Inherit;

            public int CompareTo(DlForgelikeEntry other)
            {
                if (Version != other.Version)
                {
                    return Version.CompareTo(other.Version);
                }
                else
                {
                    return McUtils.CompareVersion(VersionName, other.VersionName);
                }
            }
        }

        public class DlForgeVersionEntry : DlForgelikeEntry
        {

            /// <summary>
        /// 发布时间，格式为“yyyy/MM/dd HH:mm”。
        /// </summary>
            public string ReleaseTime;
            /// <summary>
        /// 文件的 MD5 或 SHA1（BMCLAPI 的老版本是 MD5，新版本是 SHA1；官方源总是 MD5）。
        /// </summary>
            public string Hash = null;
            /// <summary>
        /// 是否为推荐版本。
        /// </summary>
            public bool IsRecommended;
            /// <summary>
        /// 安装类型。有 installer、client、universal 三种。
        /// </summary>
            public string Category;
            /// <summary>
        /// 用于下载的文件版本名。可能在 Version 的基础上添加了分支。
        /// </summary>
            public string FileVersion;

            /// <summary>
        /// 加载器名称。Forge 或 NeoForge。
        /// </summary>
            public override string LoaderName
            {
                get
                {
                    return "Forge";
                }
            }
            /// <summary>
        /// 文件扩展名。不以小数点开头。
        /// </summary>
            public override string FileExtension
            {
                get
                {
                    return Category == "installer" ? "jar" : "zip";
                }
            }

            public DlForgeVersionEntry(string Version, string Branch, string Inherit)
            {
                // 司马版本的特殊处理
                if (Version == "11.15.1.2318" || Version == "11.15.1.1902" || Version == "11.15.1.1890")
                    Branch = "1.8.9";
                if (Branch is null && Inherit == "1.7.10" && Coerce.ToDouble(Version.Split(".")[3]) >= 1300d)
                    Branch = "1.7.10";
                // 为 DlForgelikeEntry 提供所有信息
                IsNeoForge = false;
                VersionName = Version;
                this.Version = new Version(Version);
                this.Inherit = Inherit;
                FileVersion = Version + (Branch is null ? "" : "-" + Branch);
            }

        }

        /// <summary>
    /// Forge 版本列表，主加载器。
    /// </summary>
        public static void DlForgeVersionMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
        {
            var DlForgeVersionOfficialLoader = new ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>("DlForgeVersion Official", DlForgeVersionOfficialMain);
            // 只使用官方源
            DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>>() { new KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>(DlForgeVersionOfficialLoader, 60) }, Loader.IsForceRestarting);
        }

        /// <summary>
    /// Forge 版本列表，官方源。
    /// </summary>
        public static void DlForgeVersionOfficialMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
        {
            string Result;
            try
            {
                Result = ModNet.NetRequestByLoader("https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_" + Loader.Input.Replace("-", "_") + ".html", SimulateBrowserHeaders: true); // 兼容 Forge 1.7.10-pre4，#4057
            }
            catch (Exception ex)
            {
                if (ex.GetDisplay(false).Contains("(404)"))
                {
                    throw new Exception("无");
                }
                else
                {
                    throw;
                }
            }
            if (Result.Length < 1000)
                throw new Exception("获取到的版本列表长度不足（" + Result + "）");
            var Versions = new List<DlForgeVersionEntry>();
            try
            {
                // 分割版本信息
                string[] VersionCodes = Result.Substring(0, Result.LastIndexOfF("</table>")).Split("<td class=\"download-version");
                // 获取所有版本信息
                for (int i = 1, loopTo = VersionCodes.Count() - 1; i <= loopTo; i++)
                {
                    string VersionCode = VersionCodes[i];
                    try
                    {
                        // 基础信息获取
                        string Name = VersionCode.RegexSeek(@"(?<=[^(0-9)]+)[0-9\.]+");
                        bool IsRecommended = VersionCode.Contains("fa promo-recommended");
                        string Inherit = Loader.Input;
                        // 分支获取
                        string Branch = VersionCode.RegexSeek($"(?<=-{Name}-)[^-\"]+(?=-[a-z]+.[a-z]{{3}})");
                        if (string.IsNullOrWhiteSpace(Branch))
                            Branch = null;
                        // 发布时间获取
                        string ReleaseTimeOriginal = VersionCode.RegexSeek("(?<=\"download-time\" title=\")[^\"]+");
                        string[] ReleaseTimeSplit = ReleaseTimeOriginal.Split(" -:".ToCharArray()); // 原格式："2021-02-15 03:24:02"
                        var ReleaseDate = new DateTime(Coerce.ToInt(ReleaseTimeSplit[0]), Coerce.ToInt(ReleaseTimeSplit[1]), Coerce.ToInt(ReleaseTimeSplit[2]), Coerce.ToInt(ReleaseTimeSplit[3]), Coerce.ToInt(ReleaseTimeSplit[4]), Coerce.ToInt(ReleaseTimeSplit[5]), 0, DateTimeKind.Utc); // 年月日
                                                                                                                                                                                                                                                                                                                                                     // 时分秒
                                                                                                                                                                                                                                                                                                                                                     // 以 UTC 时间作为标准
                        string ReleaseTime = ReleaseDate.ToLocalTime().ToString("yyyy'/'MM'/'dd HH':'mm"); // 时区与格式转换
                                                                                                           // 分类与 MD5 获取
                        string MD5;
                        string Category;
                        if (VersionCode.Contains("classifier-installer\""))
                        {
                            // 类型为 installer.jar，支持范围 ~753 (~ 1.6.1 部分), 738~684 (1.5.2 全部)
                            VersionCode = VersionCode.Substring(VersionCode.IndexOfF("installer.jar"));
                            MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                            Category = "installer";
                        }
                        else if (VersionCode.Contains("classifier-universal\""))
                        {
                            // 类型为 universal.zip，支持范围 751~449 (1.6.1 部分), 682~183 (1.5.1 ~ 1.3.2 部分)
                            VersionCode = VersionCode.Substring(VersionCode.IndexOfF("universal.zip"));
                            MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                            Category = "universal";
                        }
                        else if (VersionCode.Contains("client.zip"))
                        {
                            // 类型为 client.zip，支持范围 182~ (1.3.2 部分 ~)
                            VersionCode = VersionCode.Substring(VersionCode.IndexOfF("client.zip"));
                            MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                            Category = "client";
                        }
                        else
                        {
                            // 没有任何下载（1.6.4 有一部分这种情况）
                            continue;
                        }
                        // 添加进列表
                        Versions.Add(new DlForgeVersionEntry(Name, Branch, Inherit) { Category = Category, IsRecommended = IsRecommended, Hash = MD5.ReplaceLineEndings(""), ReleaseTime = ReleaseTime });
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Forge 官方源版本信息提取失败（" + VersionCode + "）", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Forge 官方源版本列表解析失败（" + Result + "）", ex);
            }
            if (!Versions.Any())
                throw new Exception("无");
            Loader.Output = Versions;
        }


        #endregion

        #region DlNeoForgeList | NeoForge 版本列表

        public struct DlNeoForgeListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 所有版本的列表。已经按从新到老排序。
        /// </summary>
            public List<DlNeoForgeListEntry> Value;
        }

        public class DlNeoForgeListEntry : DlForgelikeEntry
        {

            /// <summary>
        /// 是否是 Beta 版。
        /// </summary>
            public bool IsBeta;
            /// <summary>
        /// API 使用的原始版本字符串，如 “20.4.30-beta”、“1.20.1-47.1.99”（Legacy）。
        /// </summary>
            public string ApiName;
            /// <summary>
        /// 文件在官网的基础地址，不包含后缀。
        /// </summary>
            public string UrlBase
            {
                get
                {
                    string PackageName = Inherit == "1.20.1" ? "forge" : "neoforge";
                    return $"https://maven.neoforged.net/releases/net/neoforged/{PackageName}/{ApiName}/{PackageName}-{ApiName}";
                }
            }

            /// <summary>
        /// 加载器名称。Forge 或 NeoForge。
        /// </summary>
            public override string LoaderName
            {
                get
                {
                    return "NeoForge";
                }
            }
            /// <summary>
        /// 文件扩展名。不以小数点开头。
        /// </summary>
            public override string FileExtension
            {
                get
                {
                    return "jar";
                }
            }

            public DlNeoForgeListEntry(string ApiName)
            {
                IsNeoForge = true;
                this.ApiName = ApiName;
                IsBeta = ApiName.ContainsIgnoreCase("beta") || ApiName.ContainsIgnoreCase("alpha");
                if (ApiName.Contains("1.20.1")) // 1.20.1-47.1.99
                {
                    VersionName = ApiName.Replace("1.20.1-", "");
                    Version = new Version("19." + VersionName);
                    Inherit = "1.20.1";
                }
                else if (ApiName.StartsWithF("0.")) // 0.25w14craftmine.3-beta
                {
                    VersionName = ApiName;
                    string[] Segments = ApiName.BeforeFirst("-").Split('.');
                    Version = new Version(0, 0, Coerce.ToInt(Segments.Last()));
                    Inherit = Segments[1];
                }
                else // 20.4.30-beta；26.1.0.0-alpha.1+snapshot-1
                {
                    VersionName = ApiName;
                    Version = new Version(ApiName.BeforeFirst("-"));
                    if (Version.Major >= 24)
                    {
                        Inherit = $"{Version.Major}.{Version.Minor}.{Version.Build}";
                    }
                    else
                    {
                        Inherit = $"1.{Version.Major}.{Version.Minor}";
                    }
                    if (Inherit.EndsWithF(".0"))
                        Inherit = Coerce.Left(Inherit, Inherit.Length - 2);
                    if (VersionName.Contains("+"))
                        Inherit += "-" + VersionName.AfterFirst("+");
                }
            }
        }

        /// <summary>
    /// NeoForge 版本列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListLoader = new ModLoader.LoaderTask<int, DlNeoForgeListResult>("DlNeoForgeList Main", DlNeoForgeListMain);
        private static void DlNeoForgeListMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> Loader)
        {
            // 只使用官方源
            DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>(DlNeoForgeListOfficialLoader, 60) }, Loader.IsForceRestarting);
        }

        /// <summary>
    /// NeoForge 版本列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListOfficialLoader = new ModLoader.LoaderTask<int, DlNeoForgeListResult>("DlNeoForgeList Official", DlNeoForgeListOfficialMain);
        private static void DlNeoForgeListOfficialMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> Loader)
        {
            // 获取版本列表 JSON
            string ResultLatest = ModNet.NetRequestByClientRetry("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", RequireJson: true);
            string ResultLegacy = ModNet.NetRequestByClientRetry("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge", RequireJson: true);
            if (ResultLatest.Length < 100 || ResultLegacy.Length < 100)
                throw new Exception("获取到的版本列表长度不足（" + ResultLatest + "）");
            // 解析
            try
            {
                Loader.Output = new DlNeoForgeListResult()
                {
                    IsOfficial = true,
                    SourceName = "NeoForge 官方源",
                    Value = GetNeoForgeEntries(ResultLatest, ResultLegacy)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("NeoForge 官方源版本列表解析失败（" + ResultLatest + ModBase.CrLf + ModBase.CrLf + ResultLegacy + "）", ex);
            }
        }


        private static List<DlNeoForgeListEntry> GetNeoForgeEntries(string LatestJson, string LatestLegacyJson)
        {
            var VersionNames = (LatestLegacyJson + LatestJson).RegexSearch(@"(?<="")(1\.20\.1-)?\d+\.[^\.]+\.\d+(\.\d+)?(-(beta|alpha)(\.\d+)?)?(\+snapshot-\d+)?(?="")");
            var Versions = VersionNames.Where(name => name != "47.1.82").Select(name => new DlNeoForgeListEntry(name)).OrderByDescending(a => a).ToList(); // 这个版本虽然在版本列表中，但不能下载
            if (!Versions.Any())
                throw new Exception("无");
            return Versions;
        }

        #endregion

        #region DlLiteLoaderList | LiteLoader 版本列表

        public struct DlLiteLoaderListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的数据。
        /// </summary>
            public List<DlLiteLoaderListEntry> Value;
            /// <summary>
        /// 官方源的失败原因。若没有则为 Nothing。
        /// </summary>
            public Exception OfficialError;
        }

        public class DlLiteLoaderListEntry
        {
            /// <summary>
        /// 实际的文件名，如“liteloader-installer-1.12-00-SNAPSHOT.jar”。
        /// </summary>
            public string FileName;
            /// <summary>
        /// 是否为测试版。
        /// </summary>
            public bool IsPreview;
            /// <summary>
        /// 对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
            public string Inherit;
            /// <summary>
        /// 是否为 1.7 及更早的远古版。
        /// </summary>
            public bool IsLegacy;
            /// <summary>
        /// 发布时间，格式为“yyyy/mm/dd HH:mm”。
        /// </summary>
            public string ReleaseTime;
            /// <summary>
        /// 文件的 MD5。
        /// </summary>
            public string MD5;
            /// <summary>
        /// 对应的 Json 项。
        /// </summary>
            public JToken JsonToken;
        }

        /// <summary>
    /// LiteLoader 版本列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListLoader = new ModLoader.LoaderTask<int, DlLiteLoaderListResult>("DlLiteLoaderList Main", DlLiteLoaderListMain);
        private static void DlLiteLoaderListMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
        {
            // 只使用官方源
            DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>(DlLiteLoaderListOfficialLoader, 60) }, Loader.IsForceRestarting);
        }

        /// <summary>
    /// LiteLoader 版本列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListOfficialLoader = new ModLoader.LoaderTask<int, DlLiteLoaderListResult>("DlLiteLoaderList Official", DlLiteLoaderListOfficialMain);
        private static void DlLiteLoaderListOfficialMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
        {
            JObject Result = (JObject)ModNet.NetRequestByClientRetry("https://dl.liteloader.com/versions/versions.json", RequireJson: true).DeserializeJson();
            try
            {
                JObject Json = (JObject)Result["versions"];
                var Versions = new List<DlLiteLoaderListEntry>();
                foreach (KeyValuePair<string, JToken> Pair in Json)
                {
                    if (Pair.Key.StartsWithF("1.6") || Pair.Key.StartsWithF("1.5"))
                        continue;
                    var RealEntry = (Pair.Value["artefacts"] ?? Pair.Value["snapshots"])["com.mumfrey:liteloader"]["latest"];
                    Versions.Add(new DlLiteLoaderListEntry()
                    {
                        Inherit = Pair.Key,
                        IsLegacy = Coerce.ToDouble(Pair.Key.Split(".")[1]) < 8d,
                        IsPreview = RealEntry["stream"].ToString().Lower() == "snapshot",
                        FileName = "liteloader-installer-" + Pair.Key + (Pair.Key == "1.8" || Pair.Key == "1.9" ? ".0" : "") + "-00-SNAPSHOT.jar",
                        MD5 = (string)RealEntry["md5"],
                        ReleaseTime = ModBase.GetLocalTime(ModBase.GetDate((int)RealEntry["timestamp"])).ToString("yyyy'/'MM'/'dd HH':'mm"),
                        JsonToken = RealEntry
                    });
                }
                Loader.Output = new DlLiteLoaderListResult() { IsOfficial = true, SourceName = "LiteLoader 官方源", Value = Versions };
            }
            catch (Exception ex)
            {
                throw new Exception("LiteLoader 官方源版本列表解析失败（" + Result.ToString() + "）", ex);
            }
        }


        #endregion

        #region DlFabricList | Fabric 列表

        public struct DlFabricListResult
        {
            /// <summary>
        /// 数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
            public string SourceName;
            /// <summary>
        /// 是否为官方的实时数据。
        /// </summary>
            public bool IsOfficial;
            /// <summary>
        /// 获取到的数据。
        /// </summary>
            public JObject Value;
        }

        /// <summary>
    /// Fabric 列表，主加载器。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListLoader = new ModLoader.LoaderTask<int, DlFabricListResult>("DlFabricList Main", DlFabricListMain);
        private static void DlFabricListMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
        {
            // 只使用官方源
            DlSourceLoader(Loader, new List<KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>>() { new KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>(DlFabricListOfficialLoader, 60) }, Loader.IsForceRestarting);
        }

        /// <summary>
    /// Fabric 列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListOfficialLoader = new ModLoader.LoaderTask<int, DlFabricListResult>("DlFabricList Official", DlFabricListOfficialMain);
        private static void DlFabricListOfficialMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
        {
            JObject Result = (JObject)ModNet.NetRequestByClientRetry("https://meta.fabricmc.net/v2/versions", RequireJson: true).DeserializeJson();
            try
            {
                var Output = new DlFabricListResult() { IsOfficial = true, SourceName = "Fabric 官方源", Value = Result };
                if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                    throw new Exception("获取到的列表缺乏必要项");
                Loader.Output = Output;
            }
            catch (Exception ex)
            {
                throw new Exception("Fabric 官方源版本列表解析失败（" + Result.ToString() + "）", ex);
            }
        }


        /// <summary>
    /// Fabric API 列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, List<ResourceVersion>> DlFabricApiLoader = new ModLoader.LoaderTask<int, List<ResourceVersion>>("Fabric API List Loader", (Task) => Task.Output = ResourceVersion.FromProjectId("fabric-api", ResourcePlatforms.Modrinth, LoadDependencies: false));

        /// <summary>
    /// OptiFabric 列表，官方源。
    /// </summary>
        public static ModLoader.LoaderTask<int, List<ResourceVersion>> DlOptiFabricLoader = new ModLoader.LoaderTask<int, List<ResourceVersion>>("OptiFabric List Loader", (Task) => Task.Output = ResourceVersion.FromProjectId("322385", ResourcePlatforms.CurseForge, LoadDependencies: false));

        #endregion

        #region DlMod | Mod 镜像源请求

        /// <summary>
    /// 请求 Modrinth / CurseForge 官方 API，返回 JToken。
    /// 调用 NetRequest，会进行重试。
    /// </summary>
        public static JToken DlModRequest(string Url, HttpMethod Method = null, string Content = null, string ContentType = null)
        {
            // 只使用 Modrinth / CurseForge 官方 API
            var Urls = new List<(string Url, int Timeout)>() { (Url, Url.Contains("modrinth") ? 20 : 10), (Url, 30) }; // Modrinth 至少 20s，要不然返回不过来
            var Errors = new List<Exception>();
            foreach (var Source in Urls)
            {
                try
                {
                    return ModNet.NetRequestByClient(Source.Url, Method, Content, ContentType, Timeout: Source.Timeout * 1000, Encoding: Encoding.UTF8, RequireJson: true).DeserializeJson();
                }
                catch (Exception ex)
                {
                    Errors.Add(ex);
                }
            }
            throw new AggregateException(Errors);
        }

        #endregion

        #region DlSource | 下载地址（只使用官方源）



        /// <summary>
    /// 下载 Assets 文件。
    /// </summary>
        public static IEnumerable<string> DlSourceAssetsGet(string Original)
        {
            Original = Original.Replace("http://resources.download.minecraft.net", "https://resources.download.minecraft.net");
            return new[] { Original };
        }
        /// <summary>
    /// 下载 Libraries 文件。
    /// </summary>
        public static IEnumerable<string> DlSourceLibraryGet(string Original)
        {
            return new[] { Original }; // 只使用官方源（Mojang、Forge、Fabric、NeoForge 各自的官方 Maven）
        }
        /// <summary>
    /// 下载 Launcher 或 Meta 文件。
    /// 不应使用它来获取版本列表（因为它只使用文件下载源设置来决定源顺序）。
    /// </summary>
        public static IEnumerable<string> DlSourceLauncherOrMetaGet(string Original)
        {
            if (Original is null)
                throw new Exception("无对应的 json 下载地址");
            return new[] { Original };
        }


        // Loader 自动切换
        private static void DlSourceLoader<InputType, OutputType>(ModLoader.LoaderTask<InputType, OutputType> MainLoader, List<KeyValuePair<ModLoader.LoaderTask<InputType, OutputType>, int>> LoaderList, bool IsForceRestart = false)
        {
            int WaitCycle = 0;
            while (true)
            {
                // 检查状态
                bool BeforeLoadersAllFailed = true;
                foreach (var SubLoader in LoaderList)
                {
                    if (WaitCycle == 0) // 判断是否可以不加载，直接使用已经加载好的结果
                    {
                        if (IsForceRestart)
                            continue; // 强制刷新，不行
                        if (SubLoader.Key.Input is null ^ MainLoader.Input is null || SubLoader.Key.Input is not null && !SubLoader.Key.Input.Equals(MainLoader.Input))
                            continue; // 父子加载器的输入不一样，也不行
                    }
                    if (SubLoader.Key.State != ModBase.LoadState.Failed)
                        BeforeLoadersAllFailed = false;
                    if (SubLoader.Key.State == ModBase.LoadState.Finished)
                    {
                        // 检查加载器成功
                        MainLoader.Output = SubLoader.Key.Output;
                        DlSourceLoaderCancel(LoaderList);
                        return;
                    }
                    else if (BeforeLoadersAllFailed)
                    {
                        // 此前的加载器全部失败，直接启动后续加载器
                        if (WaitCycle < SubLoader.Value * 100)
                            WaitCycle = SubLoader.Value * 100;
                    }
                }
                // 第一轮时：既然不直接使用已经加载好的结果，那就启动第一个加载器
                if (WaitCycle == 0)
                {
                    LoaderList.First().Key.Start(MainLoader.Input, IsForceRestart);
                    foreach (var Loader in LoaderList.Skip(1))
                        Loader.Key.State = ModBase.LoadState.Waiting; // 将其他源标记为未启动，以确保可以切换下载源（#184）
                }
                // 检查加载器失败或超时
                for (int i = 0, loopTo = LoaderList.Count - 1; i <= loopTo; i++)
                {
                    if (WaitCycle != LoaderList[i].Value * 100)
                        continue;
                    if (i < LoaderList.Count - 1 && !LoaderList.All(l => l.Key.State == ModBase.LoadState.Failed))
                    {
                        // 若还有下一个源，则启动下一个源
                        LoaderList[i + 1].Key.Start(MainLoader.Input, IsForceRestart);
                    }
                    else
                    {
                        // 若没有，则失败
                        Exception ErrorInfo = null;
                        for (int ii = 0, loopTo1 = LoaderList.Count - 1; ii <= loopTo1; ii++)
                        {
                            LoaderList[ii].Key.Input = default; // 重置输入，以免以同样的输入“重试加载”时直接失败
                            if (LoaderList[ii].Key.Error is not null)
                            {
                                if (ErrorInfo is null || LoaderList[ii].Key.Error.Message == "无")
                                {
                                    ErrorInfo = LoaderList[ii].Key.Error;
                                }
                            }
                        }
                        if (ErrorInfo is null)
                            ErrorInfo = new TimeoutException("下载源连接超时");
                        DlSourceLoaderCancel(LoaderList);
                        throw ErrorInfo;
                    }
                    break;
                }
                // 计时
                Thread.Sleep(10);
                WaitCycle += 1;
                // 检查父加载器中断
                if (MainLoader.IsCanceled)
                {
                    DlSourceLoaderCancel(LoaderList);
                    return;
                }
            }
        }
        private static void DlSourceLoaderCancel<InputType, OutputType>(List<KeyValuePair<ModLoader.LoaderTask<InputType, OutputType>, int>> LoaderList)
        {
            foreach (var Loader in LoaderList)
            {
                if (Loader.Key.State == ModBase.LoadState.Loading)
                    Loader.Key.Cancel();
            }
        }

        #endregion

    }
}