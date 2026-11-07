using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf.Extensions;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PCLCS;

namespace PCL
{
    public static class LocalResourceLoaders
    {

        // 加载 Mod 列表
        public static ModLoader.LoaderTask<string, List<LocalResourceFile>> LocalResourceLoader = new ModLoader.LoaderTask<string, List<LocalResourceFile>>("LocalResourceLoader", LocalResourceFileLoad);
        private static Func<string, bool> _LocalResourceFileLoad_IsModFile = (FullName) =>
{
if (FullName is null)
    return false;
if (new[] { "disabled", "old" }.Contains(PathUtils.GetExtension(FullName)))
    FullName = PathUtils.GetFileNameWithoutExtension(FullName);
return new[] { "jar", "zip", "litemod" }.Contains(PathUtils.GetExtension(FullName));
};
        private static void LocalResourceFileLoad(ModLoader.LoaderTask<string, List<LocalResourceFile>> Loader)
        {
            try
            {
                ModBase.RunInUiWait(() => { if (ModMain.FrmInstanceMod is not null) ModMain.FrmInstanceMod.Load.ShowProgress = false; });

                // 等待 Mod 更新完成
                if (PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input))
                {
                    Logger.Info($"等待 Mod 更新完成后才能继续加载 Mod 列表：{Loader.Input}");
                    try
                    {
                        ModBase.RunInUiWait(() => { if (ModMain.FrmInstanceMod is not null) ModMain.FrmInstanceMod.Load.Text = "正在更新 Mod"; });
                        while (PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input))
                        {
                            if (Loader.IsCanceled)
                                return;
                            Thread.Sleep(100);
                        }
                    }
                    finally
                    {
                        ModBase.RunInUiWait(() => { if (ModMain.FrmInstanceMod is not null) ModMain.FrmInstanceMod.Load.Text = "正在加载 Mod 列表"; });
                    }
                    ModMain.FrmInstanceMod.LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
                }

                // 获取 Mod 文件夹下的可用文件列表
                var ModFileList = new List<string>();
                if (DirectoryUtils.Exists(Loader.Input))
                {
                    foreach (var File in DirectoryUtils.EnumerateFiles(Loader.Input, true))
                    {
                        if (!PathUtils.IsSame(PathUtils.RemoveLastPart(File), Loader.Input))
                        {
                            // 仅当 Forge 1.13- 且文件夹名与版本号相同时，才加载该子文件夹下的 Mod
                            if (!(PageInstanceLeft.Instance is not null && PageInstanceLeft.Instance.Version.ModLoaders.HasFlagF(ModLoaders.Forge) && PageInstanceLeft.Instance.Version.Vanilla.Major < 13 && (PathUtils.GetLastPart(PathUtils.RemoveLastPart(File)) ?? "") == ($"1.{PageInstanceLeft.Instance.Version.Vanilla.Major}.{PageInstanceLeft.Instance.Version.Vanilla.Build}" ?? "")))
                            {
                                continue;
                            }
                        }
                        if (_LocalResourceFileLoad_IsModFile(File))
                            ModFileList.Add(File);
                    }
                }

                // 获取本地文件缓存
                string CachePath = ModBase.PathTemp + @"Cache\LocalMod.json";
                var Cache = new JObject();
                try
                {
                    string CacheContent = FileUtils.TryReadAsString(CachePath);
                    if (!string.IsNullOrWhiteSpace(CacheContent))
                    {
                        Cache = (JObject)CacheContent.DeserializeJson();
                        if (!Cache.ContainsKey("version") || Cache["version"].ToObject<int>() != Versions.LocalModCacheVersion)
                        {
                            Logger.Warn($"本地 Mod 信息缓存版本已过期，将弃用这些缓存信息");
                            Cache = new JObject();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "读取本地 Mod 信息缓存失败，已重置");
                    Cache = new JObject();
                }
                Cache["version"] = Versions.LocalModCacheVersion;

                // 加载 Mod 列表
                var ModList = new List<LocalResourceFile>();
                foreach (var ModFile in ModFileList)
                {
                    if (Loader.IsCanceled)
                        return;
                    var ModEntry = new LocalResourceFile(ModFile);
                    var DumpMod = ModList.FirstOrDefault(m => (m.EnabledName ?? "") == (ModEntry.EnabledName ?? "")); // 存在两个文件，名称相同，但一个启用一个禁用
                    if (DumpMod is not null)
                    {
                        var DisabledMod = DumpMod.IsEnabled ? ModEntry : DumpMod;
                        Logger.Warn($"重复的 Mod 文件：{DumpMod.File.Name} 与 {ModEntry.File.Name}，已忽略 {DisabledMod.File.Name}");
                        if (ReferenceEquals(DisabledMod, ModEntry))
                        {
                            continue;
                        }
                        else
                        {
                            ModList.Remove(DisabledMod);
                        }
                    }
                    ModList.Add(ModEntry);
                }
                Logger.Info($"共发现 {ModList.Count} 个 Mod");

                // 排序
                ModList = ModList.OrderBy(m => m.File.Name).ToList();

                // 回设
                if (Loader.IsCanceled)
                    return;
                Loader.Output = ModList;

                // 开始联网加载
                // TODO: 添加信息获取中提示
                LocalResourceOnlineLoader.Start((ModList, Cache, PageInstanceLeft.Instance.Version), IsForceRestart: true);
            }

            catch (Exception ex)
            {
                Logger.Warn(ex, "Mod 列表加载失败");
                throw;
            }
        }

        // 联网加载 Mod 详情
        public static ModLoader.LoaderTask<(List<LocalResourceFile> ModList, JObject Cache, McVersion Target), int> LocalResourceOnlineLoader = new ModLoader.LoaderTask<(List<LocalResourceFile> ModList, JObject Cache, McVersion Target), int>("LocalResourceOnlineLoader", LocalResourceOnlineLoad);
        private static void LocalResourceOnlineLoad(ModLoader.LoaderTask<(List<LocalResourceFile> ModList, JObject Cache, McVersion Target), int> Loader)
        {
            var Mods = new List<LocalResourceFile>();
            var Cache = Loader.Input.Cache;
            var ModLoaders = Loader.Input.Target.ModLoaders;
            // 读取缓存，获取需要更新的 Mod 列表
            foreach (LocalResourceFile ModEntry in Loader.Input.ModList)
            {
                if (Loader.IsCanceled)
                    return;
                string CacheKey = ModEntry.ModrinthHash + Loader.Input.Target.VanillaName + ((int)ModLoaders).ToString();
                if (Cache.ContainsKey(CacheKey))
                {
                    ModEntry.FromJson((JObject)Cache[CacheKey]);
                    // 如果缓存中的信息在 6 小时以内更新过，则无需重新获取
                    if (ModEntry.OnlineDataLoaded && DateTime.Now - Cache[CacheKey]["Project"]["CacheTime"].ToObject<DateTime>() < new TimeSpan(6, 0, 0))
                        continue;
                }
                Mods.Add(ModEntry);
            }
            Logger.Info($"有 {Mods.Where(m => m.Project is null).Count()} 个 Mod 需要联网获取信息，{Mods.Where(m => m.Project is not null).Count()} 个 Mod 需要更新信息");
            if (!Mods.Any())
                return;
            // 获取作为检查目标的版本
            // 此处不应向下扩展检查的 MC 小版本，例如 Mod 在更新 1.16.5 后，对早期的 1.16.2 版本发布了修补补丁，这会导致 PCL 将 1.16.5 版本的 Mod 降级到 1.16.2
            var TargetMcVersion = Loader.Input.Target;
            string VanillaVersion = TargetMcVersion.VanillaName;
            // 开始网络获取
            Logger.Info($"正在检查 Mod 信息的版本：{VanillaVersion}，加载器 {ModLoaders}");
            int EndedThreadCount = 0;
            bool IsFailed = false;
            var CurrentTaskThread = Thread.CurrentThread;
            // 从 Modrinth 获取信息
            ModBase.RunInNewThread(() =>
        {
            try
            {
                // 步骤 1：获取 Hash 与对应的工程 ID
                var ModrinthHashes = Mods.Select(m => m.ModrinthHash).ToList();
                JObject ModrinthVersion = (JObject)ModDownload.DlModRequest("https://api.modrinth.com/v2/version_files", HttpMethod.Post,
                    $"{{\"hashes\": [\"{ModrinthHashes.Join("\",\"")}\"], \"algorithm\": \"sha1\"}}", "application/json");
                Logger.Info($"从 Modrinth 获取到 {ModrinthVersion.Count} 个本地 Mod 的对应信息");
                // 步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                if (ModrinthVersion.Count == 0)
                    return;
                var ModrinthMapping = new Dictionary<string, List<LocalResourceFile>>();
                var ModrinthLoaders = new Dictionary<LocalResourceFile, List<string>>();
                foreach (var Entry in Mods)
                {
                    if (!ModrinthVersion.ContainsKey(Entry.ModrinthHash))
                        continue;
                    if ((string)ModrinthVersion[Entry.ModrinthHash]["files"][0]["hashes"]["sha1"] != Entry.ModrinthHash)
                        continue;
                    string ProjectId = ModrinthVersion[Entry.ModrinthHash]["project_id"].ToString();
                    if (ResourceProject.Cache.ContainsKey(ProjectId) && Entry.Project is null)
                        Entry.Project = ResourceProject.Cache[ProjectId]; // 读取已加载的缓存，加快结果出现速度
                    if (!ModrinthMapping.ContainsKey(ProjectId))
                        ModrinthMapping[ProjectId] = new List<LocalResourceFile>();
                    ModrinthMapping[ProjectId].Add(Entry);
                    // 记录对应的 ProjectVersion
                    var File = ResourceVersion.FromPlatformJson((JObject)ModrinthVersion[Entry.ModrinthHash], PCLCS.ResourceTypes.Mod);
                    ModrinthLoaders[Entry] = ModrinthVersion[Entry.ModrinthHash]["loaders"]
                        .Select(l => l.ToString().Lower()).Distinct().OrderBy(l => l).ToList();
                    if (Entry.ProjectVersion is null || Entry.ProjectVersion.ReleaseDate < File.ReleaseDate)
                    {
                        Entry.ProjectVersion = File;
                    }
                    else
                    {
                        Entry.ProjectVersion.Version = File.Version; // 使用来自 Modrinth 的版本号
                    }
                }
                if (Loader.IsCanceledWithThread(CurrentTaskThread))
                    return;
                Logger.Info($"需要从 Modrinth 获取 {ModrinthMapping.Count} 个本地 Mod 的工程信息");
                // 步骤 3：获取工程信息
                if (!ModrinthMapping.Any())
                    return;
                JArray ModrinthProject = (JArray)ModDownload.DlModRequest(
                    $"https://api.modrinth.com/v2/projects?ids=[\"{ModrinthMapping.Keys.Join("\",\"")}\"]");
                foreach (var ProjectJson in ModrinthProject)
                {
                    var Project = new ResourceProject((JObject)ProjectJson);
                    foreach (var Entry in ModrinthMapping[Project.Id])
                        Entry.Project = Project;
                }
                Logger.Info($"已从 Modrinth 获取本地 Mod 信息，继续获取更新信息");
                // 步骤 4：获取更新信息
                var ModsToUpdate = ModrinthMapping.SelectMany(l => l.Value).ToList();
                if (!ModsToUpdate.Any())
                    return;
                string UpdateData = ModsToUpdate
                    .Select(m => $"{{\"hash\": \"{m.ModrinthHash}\", \"loaders\": [\"{ModrinthLoaders[m].Join("\",\"")}\"], \"game_versions\": [\"{VanillaVersion}\"]}}")
                    .Join(",");
                JObject ModrinthUpdate = (JObject)ModDownload.DlModRequest("https://api.modrinth.com/v2/version_files/update_individual", HttpMethod.Post,
                    $"{{\"algorithm\": \"sha1\", \"hashes\": [{UpdateData}]}}", "application/json");
                foreach (var Entry in Mods)
                {
                    if (!ModrinthUpdate.ContainsKey(Entry.ModrinthHash) || Entry.ProjectVersion is null)
                        continue;
                    var UpdateFile = ResourceVersion.FromPlatformJson((JObject)ModrinthUpdate[Entry.ModrinthHash], PCLCS.ResourceTypes.Mod);
                    if (!UpdateFile.DownloadAvailable)
                        continue;
                    var UpdateLoaders = ModrinthUpdate[Entry.ModrinthHash]["loaders"]
                        .Select(l => l.ToString().Lower()).Distinct().OrderBy(l => l).ToList();
                    if (!UpdateLoaders.SequenceEqual(ModrinthLoaders[Entry]))
                        continue;
                    Logger.Trace(() => $"本地文件 {Entry.ProjectVersion.FileName} 在 Modrinth 上的最新版为 {UpdateFile.FileName}");
                    if (Entry.ProjectVersion.ReleaseDate >= UpdateFile.ReleaseDate || (Entry.ProjectVersion.Hash ?? "") == (UpdateFile.Hash ?? ""))
                        continue;
                    // 设置更新日志与更新文件
                    if (Entry.UpdateFile is not null && (UpdateFile.Hash ?? "") == (Entry.UpdateFile.Hash ?? "")) // 合并
                    {
                        Entry.ChangelogUrls.Add($"https://modrinth.com/mod/{ModrinthUpdate[Entry.ModrinthHash]["project_id"]}/changelog?g={VanillaVersion}");
                        UpdateFile.DownloadUrls.AddRange(Entry.UpdateFile.DownloadUrls); // 合并下载源
                        Entry.UpdateFile = UpdateFile; // 优先使用 Modrinth 的文件
                    }
                    else if (Entry.UpdateFile is null || UpdateFile.ReleaseDate >= Entry.UpdateFile.ReleaseDate) // 替换
                    {
                        Entry.ChangelogUrls = new List<string>() { $"https://modrinth.com/mod/{ModrinthUpdate[Entry.ModrinthHash]["project_id"]}/changelog?g={VanillaVersion}" };
                        Entry.UpdateFile = UpdateFile;
                    }
                }
                Logger.Info($"从 Modrinth 获取本地 Mod 信息结束");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "从 Modrinth 获取本地 Mod 信息失败");
                IsFailed = true;
            }
            finally
            {
                EndedThreadCount += 1;
            }
        }, "Mod List Detail Loader Modrinth");
            // 从 CurseForge 获取信息
            ModBase.RunInNewThread(() =>
        {
            try
            {
                // 步骤 1：获取 Hash 与对应的工程 ID
                var Hashes = new List<uint>();
                foreach (var Entry in Mods)
                {
                    Hashes.Add(Entry.CurseForgeHash);
                    if (Loader.IsCanceledWithThread(CurrentTaskThread))
                        return;
                }
                JContainer CurseForgeRaw = (JContainer)ModDownload.DlModRequest("https://api.curseforge.com/v1/fingerprints/432", HttpMethod.Post, $"{{\"fingerprints\": [{Hashes.Join(",")}]}}", "application/json")["data"]["exactMatches"];
                Logger.Info($"从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地 Mod 的对应信息");
                if (!CurseForgeRaw.Any())
                    return;

                // 步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
                var ProjectIdToLocalFiles = new Dictionary<int, List<LocalResourceFile>>();
                var CurseForgeLoaders = new Dictionary<LocalResourceFile, ModLoaders>();
                foreach (var Project in CurseForgeRaw)
                {
                    string ProjectId = Project["id"].ToString();
                    uint Hash = (uint)Project["file"]["fileFingerprint"];
                    foreach (var Entry in Mods)
                    {
                        if (Entry.CurseForgeHash != Hash)
                            continue;
                        if (ResourceProject.Cache.ContainsKey(ProjectId) && Entry.Project is null)
                            Entry.Project = ResourceProject.Cache[ProjectId]; // 读取已加载的缓存，加快结果出现速度
                        ProjectIdToLocalFiles.AddIntoValueCollection(Conversions.ToInteger(ProjectId), Entry);
                        // 记录对应的 ProjectVersion
                        var File = ResourceVersion.FromPlatformJson((JObject)Project["file"], ResourceTypes.Mod);
                        CurseForgeLoaders[Entry] = File.ModLoaders;
                        if (Entry.ProjectVersion is null || Entry.ProjectVersion.ReleaseDate < File.ReleaseDate)
                            Entry.ProjectVersion = File;
                    }
                }
                if (Loader.IsCanceledWithThread(CurrentTaskThread))
                    return;
                Logger.Info($"需要从 CurseForge 获取 {ProjectIdToLocalFiles.Count} 个本地 Mod 的工程信息");
                if (!ProjectIdToLocalFiles.Any())
                    return;

                // 步骤 3：获取工程信息
                var CurseForgeProject = ModDownload.DlModRequest("https://api.curseforge.com/v1/mods", HttpMethod.Post, $"{{\"modIds\": [{ProjectIdToLocalFiles.Keys.Join(",")}]}}", "application/json")["data"];
                var UpdateFileIds = new Dictionary<int, List<LocalResourceFile>>(); // FileId -> 本地 Mod 文件列表
                var FileIdToProjectSlug = new Dictionary<int, string>();
                foreach (var ProjectJson in CurseForgeProject)
                {
                    if (ProjectJson["isAvailable"] is not null && !ProjectJson["isAvailable"].ToObject<bool>())
                        continue;
                    // 设置 Entry 中的工程信息
                    var Project = new ResourceProject((JObject)ProjectJson);
                    foreach (var LocalFile in ProjectIdToLocalFiles[Conversions.ToInteger(Project.Id)]) // 倒查防止 CurseForge 返回的内容有漏
                    {
                        if ((LocalFile.Project?.Platform) is { } arg1 && arg1 == ResourcePlatforms.Modrinth)
                        {
                            LocalFile.Project = LocalFile.Project; // 再次触发修改事件
                        }
                        else
                        {
                            LocalFile.Project = Project;
                        }
                        // 查找可能有版本更新的文件列表
                        var LatestFiles = ProjectJson["latestFilesIndexes"].Where(Entry =>
        {
            // Mod Loader 匹配
                            var EntryLoader = Entry["modLoader"] is null ? PCLCS.ModLoaders.None : Resource.FromCurseForgeModLoaderType(Entry["modLoader"].ToObject<int>());
                            if (CurseForgeLoaders[LocalFile] == PCLCS.ModLoaders.None)
                            {
                                if (EntryLoader != PCLCS.ModLoaders.None)
                                    return false;
                            }
                            else if (!CurseForgeLoaders[LocalFile].HasFlagF(EntryLoader))
                            {
                                return false;
                            }
            // MC 版本匹配
                            string EntryVersion = (string)Entry["gameVersion"];
                            return EntryVersion is not null && (EntryVersion ?? "") == (VanillaVersion ?? "");
                        }).MaxByAll(Entry => Entry["gameVersion"].ToString(), Comparer<string>.Create((x, y) => McUtils.CompareVersion(x, y))); // 只保留最新的 MC 版本
                        foreach (var Entry in LatestFiles)
                        {
                            int FileId = Entry["fileId"].ToObject<int>();
                            if (!UpdateFileIds.ContainsKey(FileId))
                                UpdateFileIds[FileId] = new List<LocalResourceFile>();
                            UpdateFileIds[FileId].Add(LocalFile);
                            FileIdToProjectSlug[FileId] = Project.Slug;
                        }
                    }
                }
                Logger.Info($"已从 CurseForge 获取本地 Mod 信息，需要获取 {UpdateFileIds.Count} 个用于检查更新的文件信息");
                if (!UpdateFileIds.Any())
                    return;

                // 步骤 4：获取更新文件信息
                var CurseForgeFiles = ModDownload.DlModRequest("https://api.curseforge.com/v1/mods/files", HttpMethod.Post, $"{{\"fileIds\": [{UpdateFileIds.Keys.Join(",")}]}}", "application/json")["data"];
                var UpdateFiles = new Dictionary<LocalResourceFile, ResourceVersion>();
                foreach (var FileJson in CurseForgeFiles)
                {
                    var File = ResourceVersion.FromPlatformJson((JObject)FileJson, ResourceTypes.Mod);
                    if (!File.DownloadAvailable)
                        continue;
                    foreach (LocalResourceFile Entry in UpdateFileIds[Conversions.ToInteger(File.Id)])
                    {
                        if (File.ModLoaders != CurseForgeLoaders[Entry])
                        {
                            Logger.Warn($"从 CurseForge 获取到的更新文件 {File.FileName} 加载器为 {File.ModLoaders}，与本地文件 {Entry.File.Name} 的加载器 {CurseForgeLoaders[Entry]} 不一致，已忽略", LogBehavior.None);
                            continue;
                        }
                        if (UpdateFiles.ContainsKey(Entry) && UpdateFiles[Entry].ReleaseDate >= File.ReleaseDate)
                            continue;
                        UpdateFiles[Entry] = File;
                    }
                }
                foreach (var Pair in UpdateFiles)
                {
                    var Entry = Pair.Key;
                    var UpdateFile = Pair.Value;
                    Logger.Trace(() => $"本地文件 {Entry.ProjectVersion.FileName} 在 CurseForge 上的最新版为 {UpdateFile.FileName}");
                    if (Entry.ProjectVersion.ReleaseDate >= UpdateFile.ReleaseDate || (Entry.ProjectVersion.Hash ?? "") == (UpdateFile.Hash ?? ""))
                        continue;
                    // 设置更新日志与更新文件
                    if (Entry.UpdateFile is not null && (UpdateFile.Hash ?? "") == (Entry.UpdateFile.Hash ?? "")) // 合并
                    {
                        Entry.ChangelogUrls.Add($"https://www.curseforge.com/minecraft/mc-mods/{FileIdToProjectSlug[Conversions.ToInteger(UpdateFile.Id)]}/files/{UpdateFile.Id}");
                        Entry.UpdateFile.DownloadUrls.AddRange(UpdateFile.DownloadUrls); // 合并下载源
                    }
                    else if (Entry.UpdateFile is null || UpdateFile.ReleaseDate > Entry.UpdateFile.ReleaseDate) // 替换
                    {
                        Entry.ChangelogUrls = new List<string>() { $"https://www.curseforge.com/minecraft/mc-mods/{FileIdToProjectSlug[Conversions.ToInteger(UpdateFile.Id)]}/files/{UpdateFile.Id}" };
                        Entry.UpdateFile = UpdateFile;
                    }
                }

                Logger.Info($"从 CurseForge 获取 Mod 更新信息结束");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "从 CurseForge 获取本地 Mod 信息失败");
                IsFailed = true;
            }
            finally
            {
                EndedThreadCount += 1;
            }
        }, "Mod List Detail Loader CurseForge");
            // 等待线程结束
            while (EndedThreadCount != 2)
            {
                Thread.Sleep(10);
                if (Loader.IsCanceled)
                    return;
            }
            // 保存缓存
            Mods = Mods.Where(m => m.Project is not null).ToList();
            Logger.Info($"联网获取本地 Mod 信息完成，为 {Mods.Count} 个 Mod 更新缓存");
            if (!Mods.Any())
                return;
            if (Loader.IsCanceled)
                return;
            foreach (var Entry in Mods)
            {
                Entry.OnlineDataLoaded = !IsFailed;
                Cache[Entry.ModrinthHash + VanillaVersion + ((int)ModLoaders).ToString()] = Entry.ToJson();
            }
            FileUtils.Write(ModBase.PathTemp + @"Cache\LocalMod.json", Cache.ToString(ModBase.ModeDebug ? Formatting.Indented : Formatting.None));
            // 刷新边栏
            if (Loader.IsCanceled)
                return;
            if ((ModMain.FrmInstanceMod?.Filter) is { } arg2 && arg2 == PageInstanceMod.FilterType.CanUpdate)
            {
                ModBase.RunInUi(() => ModMain.FrmInstanceMod?.RefreshUI()); // 同步 “可更新” 列表 (#4677)
            }
            else
            {
                ModBase.RunInUi(() => ModMain.FrmInstanceMod?.RefreshBars());
            }
        }

    }
}