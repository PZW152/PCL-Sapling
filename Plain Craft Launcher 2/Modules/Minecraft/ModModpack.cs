using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf;
using MeloongCore.Wpf.Extensions;
using Newtonsoft.Json.Linq;
using PCLCS;

namespace PCL
{

    public static class ModModpack
    {

        // 触发整合包安装的外部接口
        /// <summary>
    /// 弹窗要求选择一个整合包文件并进行安装。
    /// </summary>
        public static void ModpackInstall()
        {
            string File = Dialogs.SelectFile("选择整合包压缩文件", false, filter: new[] { (new[] { "rar", "zip", "mrpack" }, "整合包文件") }).FirstOrDefault(); // 选择整合包文件
            if (string.IsNullOrEmpty(File))
                return;
            ModBase.RunInThread(() => { try { ModpackInstall(File); } catch (Exception ex) { ex.ThrowIfCanceled(); Logger.Error(ex, "手动安装整合包失败", LogBehavior.Alert); } });
        }
        /// <summary>
    /// 弹窗要求选择 Prism Launcher / MultiMC 的实例文件夹，并把它导入为 PCL 的版本。
    /// 原文件夹不会被修改：会先打包为临时整合包，再按 MMC 整合包的流程安装。
    /// </summary>
        public static void ImportOtherLauncherInstance()
        {
            string Default = null;
            foreach (string Candidate in new[] { Path.Combine(Paths.AppData, "PrismLauncher", "instances"), Path.Combine(Paths.AppData, "MultiMC", "instances") })
            {
                if (DirectoryUtils.Exists(Candidate))
                {
                    Default = Candidate;
                    break;
                }
            }
            string Folder = Dialogs.SelectFolder("选择 Prism Launcher 或 MultiMC 的实例文件夹", false, Default).FirstOrDefault();
            if (string.IsNullOrEmpty(Folder))
                return;
            if (!InstanceImport.IsMmcInstance(Folder))
            {
                ModMain.MyMsgBox("这个文件夹不是 Prism Launcher 或 MultiMC 的实例。" + ModBase.CrLf + "实例文件夹里应该有 mmc-pack.json 和 instance.cfg。" + ModBase.CrLf + ModBase.CrLf +
                    "如果是 PCL、HMCL 等启动器的游戏，请在“文件夹列表”中直接添加它的 .minecraft 文件夹，无需导入。", "无法导入");
                return;
            }
            string Name = InstanceImport.GetDisplayName(Folder);
            var Validate = new ValidateFolderName(ModMinecraft.McFolderSelected + "versions");
            if (!string.IsNullOrEmpty(Validate.Validate(Name)))
                Name = ModMain.MyMsgBoxInput("输入版本名称", "", Name, new Collection<ValidateType>() { Validate });
            if (string.IsNullOrEmpty(Name))
                return;
            ModBase.RunInThread(() =>
            {
                try
                {
                    ModMain.Hint("正在读取实例 " + Name + "……");
                    string Zip = ModMain.RequestTaskTempFolder() + "import.zip";
                    InstanceImport.PackFolder(Folder, Zip);
                    ModpackInstall(Zip, Name);
                }
                catch (Exception ex)
                {
                    ex.ThrowIfCanceled();
                    Logger.Error(ex, "导入其他启动器的实例失败", LogBehavior.Alert);
                }
            });
        }
        /// <summary>
    /// 构建并启动安装给定的整合包文件的加载器，并返回该加载器。若失败则抛出异常。
    /// 必须在工作线程执行。
    /// </summary>
        public static ModLoader.LoaderCombo<string> ModpackInstall(string File, string InstanceName = null, string Logo = null)
        {
            Logger.Info($"整合包安装请求：{File ?? "null"}");
            ZipArchive Archive = null;
            string ArchiveBaseFolder = "";
            try
            {
                // 字符校验
                string TargetFolder = $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\";
                if (TargetFolder.Contains("!") || TargetFolder.Contains(";"))
                {
                    ModMain.Hint("游戏路径中不能含有感叹号或分号：" + TargetFolder, ModMain.HintType.Red);
                    throw new OperationCanceledException();
                }
                // 获取整合包种类与关键 Json
                int PackType = -1;
                do
                {
                    try
                    {
                        Archive = FileUtils.OpenZip(File);
                        // 从根目录判断整合包类型
                        if (Archive.GetEntry("mcbbs.packmeta") is not null)
                        {
                            PackType = 3;
                            break;
                        } // MCBBS 整合包（优先于 manifest.json 判断）
                        if (Archive.GetEntry("mmc-pack.json") is not null)
                        {
                            PackType = 2;
                            break;
                        } // MMC 整合包（优先于 manifest.json 判断，#4194）
                        if (Archive.GetEntry("modrinth.index.json") is not null)
                        {
                            PackType = 4;
                            break;
                        } // Modrinth 整合包
                        if (Archive.GetEntry("manifest.json") is not null)
                        {
                            JObject Json = (JObject)Archive.GetEntry("manifest.json").Open().ReadString(Encoding.UTF8).DeserializeJson();
                            if (Json["addons"] is null)
                            {
                                PackType = 0;
                                break; // CurseForge 整合包
                            }
                            else
                            {
                                PackType = 3;
                                break;
                            } // MCBBS 整合包
                        }
                        if (Archive.GetEntry("modpack.json") is not null)
                        {
                            PackType = 1;
                            break;
                        } // HMCL 整合包
                        if (Archive.GetEntry("modpack.zip") is not null || Archive.GetEntry("modpack.mrpack") is not null)
                        {
                            PackType = 9;
                            break;
                        } // 带启动器的压缩包
                          // 从一级目录判断整合包类型
                        bool exitTry = false;
                        foreach (var Entry in Archive.Entries)
                        {
                            string[] PathSplits = Entry.FullName.Split("/");
                            ArchiveBaseFolder = PathSplits[0] + "/";
                            // 确定为一级目录下
                            if (PathSplits.Count() != 2)
                                continue;
                            // 判断是否为关键文件
                            if (PathSplits[1] == "mcbbs.packmeta")
                            {
                                PackType = 3;
                                exitTry = true;
                                break;
                            } // MCBBS 整合包（优先于 manifest.json 判断）
                            if (PathSplits[1] == "mmc-pack.json")
                            {
                                PackType = 2;
                                exitTry = true;
                                break;
                            } // MMC 整合包（优先于 manifest.json 判断，#4194）
                            if (PathSplits[1] == "modrinth.index.json")
                            {
                                PackType = 4;
                                exitTry = true;
                                break;
                            } // Modrinth 整合包
                            if (PathSplits[1] == "manifest.json")
                            {
                                JObject Json = (JObject)Entry.Open().ReadString(Encoding.UTF8).DeserializeJson();
                                if (Json["addons"] is null)
                                {
                                    PackType = 0;
                                    exitTry = true;
                                    break; // CurseForge 整合包
                                }
                                else
                                {
                                    PackType = 3;
                                    ArchiveBaseFolder = "overrides/";
                                    exitTry = true;
                                    break;
                                } // MCBBS 整合包
                            }
                            if (PathSplits[1] == "modpack.json")
                            {
                                PackType = 1;
                                exitTry = true;
                                break;
                            } // HMCL 整合包
                            if (PathSplits[1] == "modpack.zip" || PathSplits[1] == "modpack.mrpack")
                            {
                                PackType = 9;
                                exitTry = true;
                                break;
                            } // 带启动器的压缩包
                        }

                        if (exitTry)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.GetDisplay(true).Contains("Error.WinIOError"))
                        {
                            throw new Exception("打开整合包文件失败", ex);
                        }
                        else if (PathUtils.GetExtension(File) == "rar")
                        {
                            throw new Exception("PCL 无法处理 rar 格式的压缩包，请在解压后重新压缩为 zip 格式再试", ex);
                        }
                        else
                        {
                            throw new Exception("打开整合包文件失败，文件可能损坏或为不支持的压缩包格式", ex);
                        }
                    }
                }
                while (false);
                // 执行对应的安装方法
                switch (PackType)
                {
                    case 0:
                        {
                            Logger.Info("整合包种类：CurseForge");
                            return InstallPackCurseForge(File, Archive, ArchiveBaseFolder, InstanceName, Logo);
                        }
                    case 1:
                        {
                            Logger.Info("整合包种类：HMCL");
                            return InstallPackHMCL(File, Archive, ArchiveBaseFolder);
                        }
                    case 2:
                        {
                            Logger.Info("整合包种类：MMC");
                            return InstallPackMMC(File, Archive, ArchiveBaseFolder, InstanceName);
                        }
                    case 3:
                        {
                            Logger.Info("整合包种类：MCBBS");
                            return InstallPackMCBBS(File, Archive, ArchiveBaseFolder, InstanceName);
                        }
                    case 4:
                        {
                            Logger.Info("整合包种类：Modrinth");
                            return InstallPackModrinth(File, Archive, ArchiveBaseFolder, InstanceName, Logo);
                        }
                    case 9:
                        {
                            Logger.Info("整合包种类：带启动器的压缩包");
                            return InstallPackLauncherPack(File, Archive, ArchiveBaseFolder);
                        }

                    default:
                        {
                            Logger.Info("整合包种类：未能识别，假定为压缩包");
                            return InstallPackCompress(File, Archive);
                        }
                }
            }
            finally
            {
                if (Archive is not null)
                    Archive.Dispose();
            }
        }

        private static void ExtractModpackFiles(string InstallTemp, string FileAddress, CancellationToken c = default, ProgressProvider p = null)
        {
            DirectoryUtils.Delete(InstallTemp);
            c.ThrowIfCancellationRequested();
            FileUtils.ExtractToDirectory(FileAddress, InstallTemp, c, p);
        }
        /// <summary>
    /// 从整合包的 override 目录复制文件，同时设置 PCL 的配置文件与版本隔离。
    /// 对路径末尾是否为 \ 没有要求。
    /// </summary>
        private static void CopyOverrideDirectory(string OverridesFolder, string VersionFolder, ModLoader.LoaderBase Loader)
        {
            OverridesFolder = PathUtils.AddSlashSuffix(OverridesFolder);
            VersionFolder = PathUtils.AddSlashSuffix(VersionFolder);
            // 复制文件
            if (DirectoryUtils.Exists(OverridesFolder))
            {
                Logger.Info($"处理整合包覆写文件夹：{OverridesFolder} → {VersionFolder}");
                DirectoryUtils.Copy(OverridesFolder, VersionFolder);
            }
            else
            {
                Logger.Info($"整合包中没有覆写文件夹：{OverridesFolder}");
            }
            // 设置 ini
            string OverridesIni = $@"{OverridesFolder}PCL\Setup.ini";
            string VersionIni = $@"{VersionFolder}PCL\Setup.ini";
            if (FileUtils.Exists(OverridesIni))
            {
                ModBase.WriteIni(OverridesIni, "VersionArgumentIndie", 1.ToString()); // 开启版本隔离
                ModBase.WriteIni(OverridesIni, "VersionArgumentIndieV2", true.ToString());
                ModBase.WriteIni(OverridesIni, "IsStar", false.ToString());
                FileUtils.Copy(OverridesIni, VersionIni); // 覆写已有的 ini
            }
            else
            {
                ModBase.WriteIni(VersionIni, "VersionArgumentIndie", 1.ToString()); // 开启版本隔离
                ModBase.WriteIni(VersionIni, "VersionArgumentIndieV2", true.ToString());
            }
            ModBase.IniClearCache(VersionIni); // 重置缓存，避免被安装过程中写入的 ini 覆盖
        }
        /// <summary>
    /// 弹窗提示整合包中存在不兼容的加载器。
    /// 如果取消，则抛出 CancelledException。
    /// </summary>
        private static void NotifyIncompatibleLoader(string LoaderName)
        {
            if (ModMain.MyMsgBox($"整合包中存在不兼容的加载器：{LoaderName}{ModBase.CrLf}如果你知道如何手动安装它，可以先选择跳过，然后在整合包下载结束后手动安装。", "不兼容的加载器", "取消", $"不安装 {LoaderName} 并继续") == 1)
                throw new OperationCanceledException();
        }

        #region 不同类型整合包的安装方法

        // CurseForge
        private static ModLoader.LoaderCombo<string> InstallPackCurseForge(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder, string InstanceName = null, string Logo = null)
        {

            // 读取 Json 文件
            JObject Json;
            try
            {
                Json = (JObject)Archive.GetEntry(ArchiveBaseFolder + "manifest.json").Open().ReadString().DeserializeJson();
            }
            catch (Exception ex)
            {
                throw new Exception("CurseForge 整合包安装信息存在问题", ex);
            }
            if (Json["minecraft"] is null || Json["minecraft"]["version"] is null)
                throw new Exception("CurseForge 整合包未提供 Minecraft 版本信息");

            // 获取版本名
            if (InstanceName is null)
            {
                InstanceName = (string)(Json["name"] ?? "");
                var Validate = new ValidateFolderName(ModMinecraft.McFolderSelected + "versions");
                if (!string.IsNullOrEmpty(Validate.Validate(InstanceName)))
                    InstanceName = "";
                if (string.IsNullOrEmpty(InstanceName))
                    InstanceName = ModMain.MyMsgBoxInput("输入版本名称", "", "", new Collection<ValidateType>() { Validate });
                if (string.IsNullOrEmpty(InstanceName))
                    throw new OperationCanceledException();
            }
            string VersionFolder = PathUtils.AddSlashSuffix(Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName) + @"\");

            // 获取 Mod API 版本信息
            var TargetVersion = new McVersion(Json["minecraft"]["version"].ToString());
            foreach (var Entry in (IEnumerable<JToken>)Json["minecraft"]["modLoaders"] ?? Array.Empty<JToken>())
            {
                string Id = (Entry[(object)"id"] ?? "").ToString().Lower();
                if (Id.StartsWithF("forge-"))
                {
                    // Forge 指定
                    if (Id.Contains("recommended"))
                        throw new Exception("该整合包版本过老，已不支持进行安装！");
                    Logger.Info($"整合包 Forge 版本：{Id}");
                    TargetVersion.ModLoaderVersions[ModLoaders.Forge] = Id.Replace("forge-", "");
                }
                else if (Id.StartsWithF("neoforge-"))
                {
                    // NeoForge 指定
                    Logger.Info($"整合包 NeoForge 版本：{Id}");
                    TargetVersion.ModLoaderVersions[ModLoaders.NeoForge] = Id.Replace("neoforge-", "");
                }
                else if (Id.StartsWithF("fabric-"))
                {
                    // Fabric 指定
                    Logger.Info($"整合包 Fabric 版本：{Id}");
                    TargetVersion.ModLoaderVersions[ModLoaders.Fabric] = Id.Replace("fabric-", "");
                }
                else if (Id.StartsWithF("quilt-"))
                {
                    // Quilt 指定
                    Logger.Info($"整合包 Quilt 版本：{Id}");
                    TargetVersion.ModLoaderVersions[ModLoaders.Quilt] = Id.Replace("quilt-", "");
                }
                else
                {
                    NotifyIncompatibleLoader(Id);
                }
            }
            // 解压
            string InstallTemp = ModMain.RequestTaskTempFolder();
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            string OverrideHome = (string)(Json["overrides"] ?? "");
            if (!string.IsNullOrEmpty(OverrideHome))
            {
                string OverridesFolder = InstallTemp + ArchiveBaseFolder + (OverrideHome == "." || OverrideHome == "./" ? "" : OverrideHome); // #5613
                if (!PathUtils.IsParentOf(InstallTemp, OverridesFolder))
                    throw new Exception("整合包的 overrides 路径超出了解压目录，请向整合包作者反馈此问题！");
                InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task.CreateCancellationToken(), Task.CreateSyncProgressProvider(ToPercentage: 0.6d));
            CopyOverrideDirectory(OverridesFolder, VersionFolder, Task);
        })
                {
                    ProgressWeight = FileUtils.GetInfo(FileAddress).Length / 1024d / 1024d / 6d,
                    Block = false
                }); // 每 6M 需要 1s
            }
            // 获取 Mod 列表
            var ModList = new List<int>();
            var ModOptionalList = new List<int>();
            foreach (var ModEntry in (IEnumerable<JToken>)Json["files"] ?? Array.Empty<JToken>())
            {
                if (ModEntry["projectID"] is null || ModEntry["fileID"] is null)
                {
                    ModMain.Hint("某项 Mod 缺少必要信息，已跳过：" + ModEntry.ToString());
                    continue;
                }
                ModList.Add((int)ModEntry["fileID"]);
                if (ModEntry["required"] is not null && !ModEntry["required"].ToObject<bool>())
                    ModOptionalList.Add((int)ModEntry["fileID"]);
            }
            if (ModList.Any())
            {
                var ModDownloadLoaders = new List<ModLoader.LoaderBase>();
                // 获取 Mod 下载信息
                ModDownloadLoaders.Add(new ModLoader.LoaderTask<int, JArray>("获取 Mod 下载信息", (Task) =>
        {
            Task.Output = (JArray)ModDownload.DlModRequest("https://api.curseforge.com/v1/mods/files", HttpMethod.Post, "{\"fileIds\": [" + ModList.Join(",") + "]}", "application/json")["data"];
            if (ModList.Count == Task.Output.Count)
                return;
            // 如果文件已被删除，则 API 会跳过那一项，导致返回的列表数量小于请求的数量。后面的都是对此的错误处理。
            // 获取缺失的 fileId 列表
            var MissingIds = ModList.Except(Task.Output.Select(j => j["id"].ToObject<int>())).ToList();
            Logger.Info($"缺失的 fileIds：{MissingIds.Join("、")}");
            // 将 fileId 映射到 projectId
            var Missings = MissingIds.Select<int, (int ProjectId, int FileId)>(id => (((JObject)Json["files"].AsEnumerable().First(f => f["fileID"].ToObject<int>() == id))["projectID"].ToObject<int>(), id)).ToList();
            Logger.Info($"缺失项：{Missings.Join("、")}");
            // 尝试获取对应的工程信息
            var Projects = new List<ResourceProject>();
            try
            {
                Projects = ResourceProject.FromProjectIds(Missings.Select(e => e.ProjectId.ToString()), ResourcePlatforms.CurseForge).ToList();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "获取缺失的工程信息失败");
            }
            var MissingDesc = new List<string>();
            foreach (var Missing in Missings)
            {
                var Project = Projects.FirstOrDefault(p => Coerce.ToDouble(p.Id) == Missing.ProjectId);
                if (Project is null)
                {
                    MissingDesc.Add($"未知 CurseForge 项目 (项目 ID {Missing.ProjectId}，文件 ID {Missing.FileId})");
                }
                else
                {
                    MissingDesc.Add($"{Project.TranslatedName} (文件 ID {Missing.FileId})");
                }
            }
            if (ModMain.MyMsgBox($"未找到该整合包所需的以下内容，它们可能已被原作者删除：{ModBase.CrLf}- {MissingDesc.Join(ModBase.CrLf + "- ")}{ModBase.CrLf}{ModBase.CrLf}是否继续安装？缺失这些内容可能导致整合包出现异常！", "整合包内容缺失", "继续", "取消", IsWarn: true) == 2)
            {
                Task.Cancel();
            }
        })
                {
                    ProgressWeight = ModList.Count / 10d
                }); // 每 10 Mod 需要 1s
                    // 构造 NetFile
                ModDownloadLoaders.Add(new ModLoader.LoaderTask<JArray, List<ModNet.NetFile>>("构造 Mod 下载信息", (Task) =>
        {
            var FileList = new Dictionary<int, ModNet.NetFile>();
            foreach (var ModJson in Task.Input)
            {
                int Id = ModJson["id"].ToObject<int>();
                // 跳过重复的 Mod（疑似 CurseForge Bug）
                if (FileList.ContainsKey(Id))
                    continue;
                // 可选 Mod 提示
                if (ModOptionalList.Contains(Id))
                {
                    if (ModMain.MyMsgBox("是否要下载整合包中的可选文件 " + ModJson["displayName"].ToString() + "？", "下载可选文件", "是", "否") == 2)
                    {
                        continue;
                    }
                }
                // 根据 modules 和文件名后缀判断资源类型
                string TargetFolder;
                ResourceTypes Type;
                if (ModJson["modules"].Any()) // modules 可能返回 null（#1006）
                {
                    var ModuleNames = ((JArray)ModJson["modules"]).Select(l => l["name"].ToString()).ToList();
                    if (ModuleNames.Contains("META-INF") || ModuleNames.Contains("mcmod.info") || ModuleNames.Contains("fabric.mod.json") || (ModJson?["FileName"]?.ToString().EndsWithF(".jar", true)).GetValueOrDefault())
                    {
                        TargetFolder = "mods";
                        Type = ResourceTypes.Mod;
                    }
                    else if (ModuleNames.Contains("pack.mcmeta"))
                    {
                        TargetFolder = "resourcepacks";
                        Type = ResourceTypes.ResourcePack;
                    }
                    else if (ModuleNames.Contains("shaders"))
                    {
                        TargetFolder = "shaderpacks";
                        Type = ResourceTypes.Shader;
                    }
                    else
                    {
                        TargetFolder = "saves";
                        Type = ResourceTypes.Map;
                    }
                }
                else
                {
                    TargetFolder = "mods";
                    Type = ResourceTypes.Mod;
                }
                // 建立 CompFile
                var File = ResourceVersion.FromPlatformJson((JObject)ModJson, Type);
                if (!File.DownloadAvailable)
                    continue;
                // 实际的添加
                FileList.Add(Id, File.ToNetFile(PathUtils.AddSlashSuffix(Path.Combine(VersionFolder, TargetFolder)), ResourceVersion.DownloadReason.ModPack, TargetVersion.VanillaName, TargetVersion.ModLoaders));
                Task.Progress += 1d / (1 + ModList.Count);
            }
            Task.Output = FileList.Values.ToList();
        })
                {
                    ProgressWeight = ModList.Count / 200d,
                    Show = false
                }); // 每 200 Mod 需要 1s
                    // 下载 Mod 文件
                ModDownloadLoaders.Add(new ModNet.LoaderDownload("下载 Mod", new List<ModNet.NetFile>()) { ProgressWeight = ModList.Count * 1.5d }); // 每个 Mod 需要 1.5s
                                                                                                                                                   // 构造加载器
                InstallLoaders.Add(new ModLoader.LoaderCombo<int>("下载 Mod（主加载器）", ModDownloadLoaders) { Show = false, ProgressWeight = ModDownloadLoaders.Sum(l => l.ProgressWeight) });
            }

            // 构造加载器
            var Target = new McComponents(TargetVersion.VanillaName, TargetVersion.ModLoaderVersions);
            var MergeLoaders = ModDownloadLib.McInstallLoadersGet(new ModDownloadLib.McInstallRequest() { NewInstanceName = InstanceName, InstanceFolder = VersionFolder, Components = Target });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            Loaders.Add(new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderTask<string, string>("最终整理文件", (Task) =>
        {
            // 设置图标
            if (Logo is not null && FileUtils.Exists(Logo))
            {
                FileUtils.Copy(Logo, VersionFolder + @"PCL\Logo.png");
                ModBase.WriteIni(VersionFolder + @"PCL\Setup.ini", "Logo", @"PCL\Logo.png");
                ModBase.WriteIni(VersionFolder + @"PCL\Setup.ini", "LogoCustom", "True");
                Logger.Info($"已设置整合包 Logo：{Logo}");
            }
            // 解压地图文件
            string SaveFolder = $@"{VersionFolder}saves\";
            foreach (var MapFile in DirectoryUtils.EnumerateFiles(SaveFolder, searchPattern: "*.zip"))
            {
                FileUtils.ExtractToDirectory(MapFile, SaveFolder, Task.CreateCancellationToken());
                FileUtils.Delete(MapFile);
            }
            // 删除原始整合包文件
            foreach (string FileToDelete in new[] { VersionFolder + "原始整合包.zip", VersionFolder + "原始整合包.mrpack" })
            {
                if (FileUtils.Exists(FileToDelete))
                {
                    Logger.Info($"删除原始整合包文件：{FileToDelete}");
                    FileUtils.Delete(FileToDelete);
                }
            }
            if (FileUtils.Exists(FileAddress) && PathUtils.GetFileNameWithoutExtension(FileAddress) == "modpack")
            {
                Logger.Info($"删除安装整合包文件：{FileAddress}");
                FileUtils.Delete(FileAddress);
            }
        })
            {
                ProgressWeight = 0.1d,
                Show = false
            });

            // 重复任务检查
            string LoaderName = "CurseForge 整合包安装：" + InstanceName + " ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Red);
                throw new OperationCanceledException();
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            Loader.Start(VersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            return Loader;
        }

        // Modrinth
        private static ModLoader.LoaderCombo<string> InstallPackModrinth(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder, string InstanceName = null, string Logo = null)
        {

            // 读取 Json 文件
            JObject Json;
            try
            {
                Json = (JObject)Archive.GetEntry(ArchiveBaseFolder + "modrinth.index.json").Open().ReadString().DeserializeJson();
            }
            catch (Exception ex)
            {
                throw new Exception("Modrinth 整合包安装信息存在问题", ex);
            }
            if (Json["dependencies"] is null || Json["dependencies"]["minecraft"] is null)
                throw new Exception("Modrinth 整合包未提供 Minecraft 版本信息");
            // 安装前检查：下载来源与附带的可执行文件
            var Review = ModpackReview.ReviewModrinth(Json, Archive.Entries.Select(e => e.FullName));
            if (Review.HasRisk)
            {
                var Warning = new StringBuilder("这个整合包有需要留意的地方：" + ModBase.CrLf);
                if (Review.UntrustedHosts.Any())
                {
                    Warning.Append(ModBase.CrLf + "它会从 Modrinth、GitHub、CurseForge 以外的网站下载文件：" + ModBase.CrLf);
                    foreach (string Host in Review.UntrustedHosts.Take(8))
                        Warning.Append(" - " + Host + ModBase.CrLf);
                    if (Review.UntrustedHosts.Count > 8)
                        Warning.Append($" - ……等共 {Review.UntrustedHosts.Count} 个" + ModBase.CrLf);
                }
                if (Review.ExecutableFiles.Any())
                {
                    Warning.Append(ModBase.CrLf + "它直接附带了程序或脚本文件：" + ModBase.CrLf);
                    foreach (string Entry in Review.ExecutableFiles.Take(8))
                        Warning.Append(" - " + Entry + ModBase.CrLf);
                    if (Review.ExecutableFiles.Count > 8)
                        Warning.Append($" - ……等共 {Review.ExecutableFiles.Count} 个" + ModBase.CrLf);
                }
                Warning.Append(ModBase.CrLf + "这些内容没有经过资源站的审核。如果你不确定这个整合包的来源是否可靠，请不要安装。");
                Logger.Warn($"整合包安全检查：非受信下载源 {Review.UntrustedHosts.Count} 个，可执行文件 {Review.ExecutableFiles.Count} 个");
                if (ModMain.MyMsgBox(Warning.ToString(), "整合包安全提醒", "取消", "仍然安装", IsWarn: true) != 2)
                    throw new OperationCanceledException();
            }
            Logger.Info($"整合包安全检查：需下载 {Review.DownloadCount} 个文件，附带 {Review.BundledJarCount} 个 jar");
            // 获取 Mod API 版本信息
            // TODO: 改为使用类似 InstallPackMMC 中，一个版本名一个字典的方式来存储它，不要用 Unknown
            var TargetVersion = new McVersion("Unknown");
            foreach (JProperty Entry in (IEnumerable<JToken>)Json["dependencies"] ?? Array.Empty<JToken>())
            {
                switch (Entry.Name.Lower() ?? "")
                {
                    case "minecraft":
                        {
                            TargetVersion.VanillaName = Entry.Value.ToString();
                            break;
                        }
                    case "forge": // eg. 14.23.5.2859 / 1.19-41.1.0
                        {
                            TargetVersion.ModLoaderVersions[ModLoaders.Forge] = Entry.Value.ToString();
                            Logger.Info($"整合包 Forge 版本：{TargetVersion.ModLoaderVersions[ModLoaders.Forge]}");
                            break;
                        }
                    case "neoforge":
                    case "neo-forge": // eg. 20.6.98-beta
                        {
                            TargetVersion.ModLoaderVersions[ModLoaders.NeoForge] = Entry.Value.ToString();
                            Logger.Info($"整合包 NeoForge 版本：{TargetVersion.ModLoaderVersions[ModLoaders.NeoForge]}");
                            break;
                        }
                    case "fabric-loader": // eg. 0.14.14
                        {
                            TargetVersion.ModLoaderVersions[ModLoaders.Fabric] = Entry.Value.ToString();
                            Logger.Info($"整合包 Fabric 版本：{TargetVersion.ModLoaderVersions[ModLoaders.Fabric]}");
                            break;
                        }
                    case "quilt-loader": // eg. 0.29.2
                        {
                            TargetVersion.ModLoaderVersions[ModLoaders.Quilt] = Entry.Value.ToString();
                            Logger.Info($"整合包 Quilt 版本：{TargetVersion.ModLoaderVersions[ModLoaders.Quilt]}");
                            break;
                        }

                    default:
                        {
                            NotifyIncompatibleLoader(Entry.Name);
                            break;
                        }
                }
            }
            // 获取版本名
            if (InstanceName is null)
            {
                InstanceName = (string)(Json["name"] ?? "");
                var Validate = new ValidateFolderName(ModMinecraft.McFolderSelected + "versions");
                if (!string.IsNullOrEmpty(Validate.Validate(InstanceName)))
                    InstanceName = "";
                if (string.IsNullOrEmpty(InstanceName))
                    InstanceName = ModMain.MyMsgBoxInput("输入版本名称", "", "", new Collection<ValidateType>() { Validate });
                if (string.IsNullOrEmpty(InstanceName))
                    throw new OperationCanceledException();
            }
            string VersionFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName) + @"\";
            // 解压
            string InstallTemp = ModMain.RequestTaskTempFolder();
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task.CreateCancellationToken(), Task.CreateSyncProgressProvider(ToPercentage: 0.5d));
            CopyOverrideDirectory(InstallTemp + ArchiveBaseFolder + "overrides", VersionFolder, Task);
            CopyOverrideDirectory(InstallTemp + ArchiveBaseFolder + "client-overrides", VersionFolder, Task);
        })
            {
                ProgressWeight = FileUtils.GetInfo(FileAddress).Length / 1024d / 1024d / 6d,
                Block = false
            }); // 每 6M 需要 1s
                // 获取下载文件列表
            var FileList = new List<ModNet.NetFile>();
            foreach (var File in (IEnumerable<JToken>)Json["files"] ?? Array.Empty<JToken>())
            {
                // 检查是否需要该文件
                if (File["env"] is not null)
                {
                    switch (File["env"]["client"].ToString() ?? "")
                    {
                        case "optional":
                            {
                                if (ModMain.MyMsgBox("是否要下载可选文件 " + PathUtils.GetLastPart(File["path"].ToString()) + "？", "下载可选文件", "是", "否") == 2)
                                {
                                    continue;
                                }

                                break;
                            }
                        case "unsupported":
                            {
                                continue;
                            }
                    }
                }
                // 添加下载文件
                var Urls = File["downloads"].SelectMany(t => ResourceVersion.ParseCurseForgeDownloadUrls(t.ToString())).ToList();
                Urls = ResourceVersion.ParseModrinthTrackArguments(Urls, ResourceVersion.DownloadReason.ModPack, TargetVersion.VanillaName, TargetVersion.ModLoaders).ToList(); // Modrinth 来源追踪信息
                Urls = Urls.Distinct().ToList();
                string TargetPath = Path.Combine(VersionFolder, File["path"].ToString().TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!PathUtils.IsParentOf(VersionFolder, TargetPath))
                {
                    ModMain.MyMsgBox($"整合包的文件路径超出了版本文件夹，请向整合包作者反馈此问题！{ModBase.CrLf}目标：{Path.GetFullPath(TargetPath)}{ModBase.CrLf}错误的文件：{TargetPath}", "文件路径校验失败", IsWarn: true);
                    throw new OperationCanceledException();
                }
                FileList.Add(new ModNet.NetFile(Urls, TargetPath, new FileChecker() { Hash = File["hashes"]["sha1"].ToString() }, true)); // Modrinth 整合包不应校验文件大小（他们官方自己说的）
            }
            if (FileList.Any())
            {
                InstallLoaders.Add(new ModNet.LoaderDownload("下载额外文件", FileList) { ProgressWeight = FileList.Count * 1.5d }); // 每个 Mod 需要 1.5s
            }

            // 构造加载器
            var Target = new McComponents(TargetVersion.VanillaName, TargetVersion.ModLoaderVersions);
            var MergeLoaders = ModDownloadLib.McInstallLoadersGet(new ModDownloadLib.McInstallRequest()
            {
                NewInstanceName = InstanceName,
                InstanceFolder = VersionFolder,
                Components = Target
            });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            Loaders.Add(new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderTask<string, string>("最终整理文件", (Task) =>
        {
            // 设置图标
            if (Logo is not null && FileUtils.Exists(Logo))
            {
                FileUtils.Copy(Logo, VersionFolder + @"PCL\Logo.png");
                ModBase.WriteIni(VersionFolder + @"PCL\Setup.ini", "Logo", @"PCL\Logo.png");
                ModBase.WriteIni(VersionFolder + @"PCL\Setup.ini", "LogoCustom", "True");
                Logger.Info($"已设置整合包 Logo：{Logo}");
            }
            // 删除原始整合包文件
            foreach (string FileToDelete in new[] { VersionFolder + "原始整合包.zip", VersionFolder + "原始整合包.mrpack" })
            {
                if (FileUtils.Exists(FileToDelete))
                {
                    Logger.Info($"删除原始整合包文件：{FileToDelete}");
                    FileUtils.Delete(FileToDelete);
                }
            }
            if (FileUtils.Exists(FileAddress) && PathUtils.GetFileNameWithoutExtension(FileAddress) == "modpack")
            {
                Logger.Info($"删除安装整合包文件：{FileAddress}");
                FileUtils.Delete(FileAddress);
            }
        })
            {
                ProgressWeight = 0.1d,
                Show = false
            });

            // 重复任务检查
            string LoaderName = $"Modrinth 整合包安装：{InstanceName} ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Red);
                throw new OperationCanceledException();
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            Loader.Start(VersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            return Loader;
        }

        // HMCL
        private static ModLoader.LoaderCombo<string> InstallPackHMCL(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder)
        {
            // 读取 Json 文件
            JObject Json;
            try
            {
                Json = (JObject)Archive.GetEntry(ArchiveBaseFolder + "modpack.json").Open().ReadString(Encoding.UTF8).DeserializeJson();
            }
            catch (Exception ex)
            {
                throw new Exception("HMCL 整合包安装信息存在问题", ex);
            }
            // 获取版本名
            string InstanceName = (string)(Json["name"] ?? "");
            var Validate = new ValidateFolderName(ModMinecraft.McFolderSelected + "versions");
            if (!string.IsNullOrEmpty(Validate.Validate(InstanceName)))
                InstanceName = "";
            if (string.IsNullOrEmpty(InstanceName))
                InstanceName = ModMain.MyMsgBoxInput("输入版本名称", "", "", new Collection<ValidateType>() { Validate });
            if (string.IsNullOrEmpty(InstanceName))
                throw new OperationCanceledException();
            string VersionFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName) + @"\";
            // 解压
            string InstallTemp = ModMain.RequestTaskTempFolder();
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task.CreateCancellationToken(), Task.CreateSyncProgressProvider(ToPercentage: 0.6d));
            CopyOverrideDirectory(InstallTemp + ArchiveBaseFolder + "minecraft", VersionFolder, Task);
        })
            {
                ProgressWeight = FileUtils.GetInfo(FileAddress).Length / 1024d / 1024d / 6d,
                Block = false
            }); // 每 6M 需要 1s
                // 构造游戏本体安装加载器
            if (Json["gameVersion"] is null)
                throw new Exception("该 HMCL 整合包未提供游戏版本信息，无法安装！");
            var Target = new McComponents(Json["gameVersion"].ToString(), new Dictionary<ModLoaders, string>());
            var MergeLoaders = ModDownloadLib.McInstallLoadersGet(new ModDownloadLib.McInstallRequest()
            {
                NewInstanceName = InstanceName,
                InstanceFolder = VersionFolder,
                Components = Target
            });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>() { new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) }, new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) } };
            // 重复任务检查
            string LoaderName = "HMCL 整合包安装：" + InstanceName + " ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Red);
                throw new OperationCanceledException();
            }
            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            Loader.Start(VersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            return Loader;
        }

        // MMC
        private static ModLoader.LoaderCombo<string> InstallPackMMC(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder, string RequestedName = null)
        {
            // 读取 Json 文件
            JObject PackJson;
            string PackInstance;
            try
            {
                PackJson = (JObject)Archive.GetEntry(ArchiveBaseFolder + "mmc-pack.json").Open().ReadString(Encoding.UTF8).DeserializeJson();
                PackInstance = Archive.GetEntry(ArchiveBaseFolder + "instance.cfg").Open().ReadString(Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("MMC 整合包安装信息存在问题", ex);
            }
            // 获取版本名
            // instance.cfg 可能使用 Windows 换行，名称末尾的 \r 要去掉，否则名称不合法
            string PackName = (PackInstance.RegexSeek(@"(?<=\nname\=)[^\n]+") ?? "").Trim();
            string InstanceName = string.IsNullOrEmpty(RequestedName) ? PackName : RequestedName;
            var Validate = new ValidateFolderName(ModMinecraft.McFolderSelected + "versions");
            if (!string.IsNullOrEmpty(Validate.Validate(InstanceName)))
                InstanceName = ModMain.MyMsgBoxInput("输入版本名称", "", PackName, new Collection<ValidateType>() { Validate });
            if (string.IsNullOrEmpty(InstanceName))
                throw new OperationCanceledException();
            string VersionFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName) + @"\";
            // 解压
            string InstallTemp = ModMain.RequestTaskTempFolder();
            string SetupFile = VersionFolder + @"PCL\Setup.ini";
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task.CreateCancellationToken(), Task.CreateSyncProgressProvider(ToPercentage: 0.55d));
            // Prism 优先使用 minecraft/ 而非 .minecraft/（#9272）
            string MmcGameRoot;
            if (DirectoryUtils.Exists(InstallTemp + ArchiveBaseFolder + ".minecraft") && !DirectoryUtils.Exists(InstallTemp + ArchiveBaseFolder + "minecraft"))
            {
                MmcGameRoot = InstallTemp + ArchiveBaseFolder + ".minecraft";
            }
            else
            {
                MmcGameRoot = InstallTemp + ArchiveBaseFolder + "minecraft";
            }
            CopyOverrideDirectory(MmcGameRoot, VersionFolder, Task);
            // 读取 MMC 设置文件（#2655）
            try
            {
                string MmcSetupFile = InstallTemp + ArchiveBaseFolder + "instance.cfg";
                if (FileUtils.Exists(MmcSetupFile))
                {
                    // 将其中的等号替换为冒号，以符合 ini 文件格式
                    var Lines = new List<string>();
                    foreach (var Line in FileUtils.ReadAsLines(MmcSetupFile, true))
                    {
                        if (!Line.Contains("="))
                            continue;
                        Lines.Add(Line.BeforeFirst("=") + ":" + Line.AfterFirst("="));
                    }
                    FileUtils.Write(MmcSetupFile, Lines.Join(ModBase.CrLf));
                    // 读取文件
                    if (Coerce.ToBool(ModBase.ReadIni(MmcSetupFile, "OverrideCommands", false.ToString())))
                    {
                        string PreLaunchCommand = ModBase.ReadIni(MmcSetupFile, "PreLaunchCommand");
                        if (!string.IsNullOrEmpty(PreLaunchCommand))
                        {
                            PreLaunchCommand = PreLaunchCommand.Replace(@"\""", "\"").Replace("$INST_JAVA", "{java}java.exe").Replace(@"$INST_MC_DIR\", "{minecraft}").Replace("$INST_MC_DIR", "{minecraft}").Replace(@"$INST_DIR\", "{verpath}").Replace("$INST_DIR", "{verpath}").Replace("$INST_ID", "{name}").Replace("$INST_NAME", "{name}");
                            ModBase.WriteIni(SetupFile, "VersionAdvanceRun", PreLaunchCommand);
                            Logger.Info($"迁移 MultiMC 版本独立设置：启动前执行命令：{PreLaunchCommand}");
                        }
                    }
                    if (Coerce.ToBool(ModBase.ReadIni(MmcSetupFile, "JoinServerOnLaunch", false.ToString())))
                    {
                        string ServerAddress = ModBase.ReadIni(MmcSetupFile, "JoinServerOnLaunchAddress").Replace(@"\""", "\"");
                        ModBase.WriteIni(SetupFile, "VersionServerEnter", ServerAddress);
                        Logger.Info($"迁移 MultiMC 版本独立设置：自动进入服务器：{ServerAddress}");
                    }
                    string Logo = ModBase.ReadIni(MmcSetupFile, "iconKey", "");
                    if (!string.IsNullOrEmpty(Logo) && FileUtils.Exists($"{InstallTemp}{ArchiveBaseFolder}{Logo}.png"))
                    {
                        ModBase.WriteIni(SetupFile, "LogoCustom", true.ToString());
                        ModBase.WriteIni(SetupFile, "Logo", @"PCL\Logo.png");
                        FileUtils.Copy($"{InstallTemp}{ArchiveBaseFolder}{Logo}.png", VersionFolder + @"PCL\Logo.png");
                        Logger.Info($"迁移 MultiMC 版本独立设置：版本图标（{Logo}.png）");
                    }
                    // JVM 参数
                    string JvmArgs = ModBase.ReadIni(MmcSetupFile, "JvmArgs", "");
                    if (!string.IsNullOrEmpty(JvmArgs))
                    {
                        if (Coerce.ToBool(ModBase.ReadIni(MmcSetupFile, "OverrideJavaArgs", false.ToString())))
                        {
                            ModBase.WriteIni(SetupFile, "VersionAdvanceJvm", JvmArgs);
                            Logger.Info($"迁移 MultiMC 版本独立设置：JVM 参数（覆盖）：{JvmArgs}");
                        }
                        else
                        {
                            JvmArgs += " " + Settings.Get<string>("LaunchAdvanceJvm");
                            ModBase.WriteIni(SetupFile, "VersionAdvanceJvm", JvmArgs);
                            Logger.Info($"迁移 MultiMC 版本独立设置：JVM 参数（追加）：{JvmArgs}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"读取 MMC 配置文件失败（{InstallTemp}{ArchiveBaseFolder}instance.cfg）");
            }
        })
            {
                ProgressWeight = FileUtils.GetInfo(FileAddress).Length / 1024d / 1024d / 6d,
                Block = false
            }); // 每 6M 需要 1s
                // 构造版本安装请求
            if (PackJson["components"] is null)
                throw new Exception("该 MMC 整合包未提供游戏版本信息，无法安装！");
            string MinecraftVersion = null;
            var ModLoaderVersions = new Dictionary<ModLoaders, string>();
            foreach (JObject Component in PackJson["components"])
            {
                if (!Component.ContainsKey("uid"))
                    continue;
                string UID = Component["uid"].ToString();
                switch (UID ?? "")
                {
                    case "net.minecraft":
                        {
                            MinecraftVersion = (string)Component["version"];
                            break;
                        }
                    case "net.minecraftforge":
                        {
                            ModLoaderVersions[ModLoaders.Forge] = (string)Component["version"];
                            break;
                        }
                    case "net.neoforged":
                        {
                            ModLoaderVersions[ModLoaders.NeoForge] = (string)Component["version"];
                            break;
                        }
                    case "net.fabricmc.fabric-loader":
                        {
                            ModLoaderVersions[ModLoaders.Fabric] = (string)Component["version"];
                            break;
                        }
                    case "org.quiltmc.quilt-loader":
                        {
                            ModLoaderVersions[ModLoaders.Quilt] = (string)Component["version"];
                            break;
                        }

                    default:
                        {
                            if (UID.StartsWithF("org.lwjgl")) // #8210
                            {
                                Logger.Info($"已跳过 LWJGL 项：{UID}");
                            }
                            else
                            {
                                NotifyIncompatibleLoader(UID);
                            }

                            break;
                        }
                }
            }
            // 构造加载器
            var Target = new McComponents(MinecraftVersion, ModLoaderVersions);
            var MergeLoaders = ModDownloadLib.McInstallLoadersGet(new ModDownloadLib.McInstallRequest()
            {
                NewInstanceName = InstanceName,
                InstanceFolder = VersionFolder,
                Components = Target
            });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            Loaders.Add(new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });

            // 重复任务检查
            string LoaderName = "MMC 整合包安装：" + InstanceName + " ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Red);
                throw new OperationCanceledException();
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            Loader.Start(VersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            return Loader;
        }

        // MCBBS
        private static ModLoader.LoaderCombo<string> InstallPackMCBBS(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder, string InstanceName = null)
        {
            // 读取 Json 文件
            JObject Json;
            try
            {
                var Entry = Archive.GetEntry(ArchiveBaseFolder + "mcbbs.packmeta") ?? Archive.GetEntry(ArchiveBaseFolder + "manifest.json");
                Json = (JObject)Entry.Open().ReadString(Encoding.UTF8).DeserializeJson();
            }
            catch (Exception ex)
            {
                throw new Exception("MCBBS 整合包安装信息存在问题", ex);
            }
            // 获取版本名
            if (InstanceName is null)
            {
                InstanceName = (string)(Json["name"] ?? "");
                var Validate = new ValidateFolderName(ModMinecraft.McFolderSelected + "versions");
                if (!string.IsNullOrEmpty(Validate.Validate(InstanceName)))
                    InstanceName = "";
                if (string.IsNullOrEmpty(InstanceName))
                    InstanceName = ModMain.MyMsgBoxInput("输入版本名称", "", "", new Collection<ValidateType>() { Validate });
                if (string.IsNullOrEmpty(InstanceName))
                    throw new OperationCanceledException();
            }
            string VersionFolder = Path.Combine(ModMinecraft.McFolderSelected, "versions", InstanceName) + @"\";
            // 解压
            string InstallTemp = ModMain.RequestTaskTempFolder();
            string SetupFile = VersionFolder + @"PCL\Setup.ini";
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            InstallLoaders.Add(new ModLoader.LoaderTask<string, int>("解压整合包文件", (Task) =>
        {
            ExtractModpackFiles(InstallTemp, FileAddress, Task.CreateCancellationToken(), Task.CreateSyncProgressProvider(ToPercentage: 0.6d));
            CopyOverrideDirectory(InstallTemp + ArchiveBaseFolder + "overrides", VersionFolder, Task);
            // JVM 参数
            if (Json["launchInfo"] is not null)
            {
                JObject LaunchInfo = (JObject)Json["launchInfo"];
                if (LaunchInfo.ContainsKey("javaArgument"))
                    ModBase.WriteIni(SetupFile, "VersionAdvanceJvm", string.Join(" ", LaunchInfo["javaArgument"]));
                if (LaunchInfo.ContainsKey("launchArgument"))
                    ModBase.WriteIni(SetupFile, "VersionAdvanceGame", string.Join(" ", LaunchInfo["launchArgument"]));
            }
        })
            {
                ProgressWeight = FileUtils.GetInfo(FileAddress).Length / 1024d / 1024d / 6d,
                Block = false
            }); // 每 6M 需要 1s
                // 构造加载器
            if (Json["addons"] is null)
                throw new Exception("该 MCBBS 整合包未提供游戏版本附加信息，无法安装！");
            var Addons = new Dictionary<string, string>();
            foreach (var Entry in Json["addons"])
                Addons.Add((string)Entry["id"], (string)Entry["version"]);
            if (!Addons.ContainsKey("game"))
                throw new Exception("该 MCBBS 整合包未提供游戏版本信息，无法安装！");
            var ModLoaderVersions = new Dictionary<ModLoaders, string>();
            if (Addons.ContainsKey("quilt"))
                ModLoaderVersions[ModLoaders.Quilt] = Addons["quilt"];
            if (Addons.ContainsKey("forge"))
                ModLoaderVersions[ModLoaders.Forge] = Addons["forge"];
            if (Addons.ContainsKey("neoforge"))
                ModLoaderVersions[ModLoaders.NeoForge] = Addons["neoforge"];
            if (Addons.ContainsKey("fabric"))
                ModLoaderVersions[ModLoaders.Fabric] = Addons["fabric"];
            JObject OptiFineEntry = null;
            if (Addons.ContainsKey("optifine"))
            {
                string MinecraftVersion = Addons["game"];
                if (MinecraftVersion.EndsWithF(".0"))
                    MinecraftVersion = MinecraftVersion.Substring(0, MinecraftVersion.Length - 2);
                string OptiFineFileSuffix = "_HD_" + Addons["optifine"].AfterLast("HD_") + ".jar";
                // 从官方源拉取 OptiFine 列表
                if (ModDownload.DlOptiFineListLoader.State != ModBase.LoadState.Finished)
                    ModDownload.DlOptiFineListLoader.WaitForExit();
                var Entry = ModDownload.DlOptiFineListLoader.Output.Value.FirstOrDefault(v => (v.Inherit ?? "") == (MinecraftVersion ?? "") && v.FileName.EndsWithF(OptiFineFileSuffix));
                if (Entry is null)
                    throw new Exception($"OptiFine 列表中没有整合包所需的 OptiFine {Addons["optifine"]}（Minecraft {Addons["game"]}）");
                OptiFineEntry = JObject.FromObject(Entry);
            }
            var Target = new McComponents(Addons["game"], ModLoaderVersions, OptiFineEntry);
            var MergeLoaders = ModDownloadLib.McInstallLoadersGet(new ModDownloadLib.McInstallRequest()
            {
                NewInstanceName = InstanceName,
                InstanceFolder = VersionFolder,
                Components = Target
            });
            // 构造总加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            Loaders.Add(new ModLoader.LoaderCombo<string>("整合包安装", InstallLoaders) { Show = false, Block = false, ProgressWeight = InstallLoaders.Sum(l => l.ProgressWeight) });
            Loaders.Add(new ModLoader.LoaderCombo<string>("游戏安装", MergeLoaders) { Show = false, ProgressWeight = MergeLoaders.Sum(l => l.ProgressWeight) });

            // 重复任务检查
            string LoaderName = "MCBBS 整合包安装：" + InstanceName + " ";
            if (ModLoader.LoaderTaskbar.Any(l => (l.Name ?? "") == (LoaderName ?? "")))
            {
                ModMain.Hint("该整合包正在安装中！", ModMain.HintType.Red);
                throw new OperationCanceledException();
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders) { OnStateChanged = ModDownloadLib.McInstallState };
            // If Archive IsNot Nothing Then Archive.Dispose() '解除占用，以免在加载器中触发 “正由另一进程使用，因此该进程无法访问此文件”
            Loader.Start(VersionFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            return Loader;
        }

        // 带启动器的压缩包
        private static ModLoader.LoaderCombo<string> InstallPackLauncherPack(string FileAddress, ZipArchive Archive, string ArchiveBaseFolder)
        {
            // 获取解压路径
            ModMain.MyMsgBox("接下来请选择一个空文件夹，它会被安装到这个文件夹里。", "安装", "继续", ForceWait: true);
            string TargetFolder = Dialogs.SelectFolder("选择安装目标（必须是一个空文件夹）", false).FirstOrDefault();
            if (TargetFolder is null)
                throw new OperationCanceledException();
            if (!DirectoryUtils.IsEmpty(TargetFolder))
            {
                ModMain.Hint("请选择一个空文件夹作为安装目标！", ModMain.HintType.Red);
                throw new OperationCanceledException();
            }
            // 解压
            var Loader = new ModLoader.LoaderCombo<string>("解压压缩包", new[] {
                        new ModLoader.LoaderTask<string, int>("解压压缩包", (Task) =>
                {
                ExtractModpackFiles(TargetFolder, FileAddress, Task.CreateCancellationToken(), Task.CreateSyncProgressProvider(ToPercentage: 0.9d));
                ModBase.OpenExplorer(TargetFolder);
                Thread.Sleep(400); // 避免文件争用
                // 查找解压后的 exe 文件
                string Launcher = null;
                foreach (var ExeFile in DirectoryUtils.EnumerateFiles(TargetFolder, searchPattern: "*.exe"))
                    {
                    var Info = FileVersionInfo.GetVersionInfo(ExeFile);
                    Logger.Info($"文件 {ExeFile} 的产品名标识为 {Info.ProductName}");
                    if (Info.ProductName is not null && Info.ProductName.StartsWith("Plain Craft Launcher") && Info.ProductName != "Plain Craft Launcher Admin Manager")
                        {
                        Launcher = ExeFile;
                        Logger.Info($"发现整合包附带的 PCL 启动器：{ExeFile}");
                        }
                                            else if ((Info.ProductName.ContainsIgnoreCase("Launcher") || Info.ProductName.Contains("启动")) && !(Info.ProductName == "Plain Craft Launcher Admin Manager"))
                        {
                        if (Launcher is null)
                            {
                            Launcher = ExeFile;
                            Logger.Info($"发现整合包附带的疑似第三方启动器：{ExeFile}");
                        }
                    }
                }
                Task.Progress = 0.95d;
                // 尝试使用附带的启动器打开
                if (Launcher is not null)
                    {
                    Logger.Info($"找到压缩包中附带的启动器：{Launcher}");
                    if (ModMain.MyMsgBox($"整合包里似乎自带了启动器，是否换用它继续安装？{ModBase.CrLf}即将打开：{Launcher}", "换用整合包启动器？", "换用", "不换用") == 1)
                        {
                        ModBase.StartProcess(Launcher, "--wait"); // 要求等待已有的 PCL 退出
                        Logger.Info("为换用整合包中的启动器启动，强制结束程序");
                        ModMain.FrmMain.EndProgram(false);
                        return;
                    }
                    }
                else
                    {
                    Logger.Info("未找到压缩包中附带的启动器");
                }
                // 加入文件夹列表
                string InstanceName = PathUtils.GetLastPart(TargetFolder);
                DirectoryUtils.Create(TargetFolder + @".minecraft\");
                                                        PageSelectLeft.AddFolder(TargetFolder + @".minecraft\" + ArchiveBaseFolder.Replace("/", @"\").TrimStart('\\'), InstanceName, false); // 格式例如：包裹文件夹\.minecraft\（最短为空字符串）
                // 调用 modpack 文件进行安装
                string ModpackFile = DirectoryUtils.EnumerateFiles(TargetFolder, true, "modpack.*").First();
                Logger.Info($"调用 modpack 文件继续安装：{ModpackFile}");
                ModpackInstall(ModpackFile);
            })
        });
            Loader.Start(TargetFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
            return Loader;
        }

        // 普通压缩包
        private static ModLoader.LoaderCombo<string> InstallPackCompress(string FileAddress, ZipArchive Archive)
        {
            // 尝试定位 .minecraft 文件夹：寻找形如 “/versions/XXX/XXX.json” 的路径
            Match Match = null;
            var Regex = new Regex(@"^.*\/(?=versions\/(?<ver>[^\/]+)\/(\k<ver>)\.json$)", RegexOptions.IgnoreCase);
            foreach (var Entry in Archive.Entries)
            {
                var EntryMatch = Regex.Match("/" + Entry.FullName);
                if (EntryMatch.Success)
                {
                    Match = EntryMatch;
                    break;
                }
            }
            if (Match is null)
                throw new Exception("文件结构不匹配，这可能不是 Minecraft 客户端压缩包？"); // 没有匹配
            string ArchiveBaseFolder = Match.Value.Replace("/", @"\").TrimStart('\\'); // 格式例如：包裹文件夹\.minecraft\（最短为空字符串）
            Logger.Info($"检测到压缩包的 .minecraft 根目录：{ArchiveBaseFolder}，命中的版本名：{Match.Groups[1].Value}");
            // 获取解压路径
            ModMain.MyMsgBox("接下来请选择一个空文件夹，它会被安装到这个文件夹里。", "安装", "继续", ForceWait: true);
            string TargetFolder = Dialogs.SelectFolder("选择安装目标（必须是一个空文件夹）", false).FirstOrDefault();
            if (TargetFolder is null)
                throw new OperationCanceledException();
            if (TargetFolder.Contains("!") || TargetFolder.Contains(";"))
            {
                ModMain.Hint("Minecraft 文件夹路径中不能含有感叹号或分号！", ModMain.HintType.Red);
                throw new OperationCanceledException();
            }
            if (!DirectoryUtils.IsEmpty(TargetFolder))
            {
                ModMain.Hint("请选择一个空文件夹作为安装目标！", ModMain.HintType.Red);
                throw new OperationCanceledException();
            }
            // 解压
            var Loader = new ModLoader.LoaderCombo<string>("解压压缩包", new[] {
                        new ModLoader.LoaderTask<string, int>("解压压缩包", (Task) =>
                {
                ExtractModpackFiles(TargetFolder, FileAddress, Task.CreateCancellationToken(), Task.CreateSyncProgressProvider(ToPercentage: 0.95d));
                // 加入文件夹列表
                PageSelectLeft.AddFolder(TargetFolder + ArchiveBaseFolder, PathUtils.GetLastPart(TargetFolder), false);
                Thread.Sleep(400); // 避免文件争用
                ModBase.RunInUi(() => ModMain.FrmMain.PageChange((FormMain.PageStackData)FormMain.PageType.InstanceSelect));
            })
        })
            {
                OnStateChanged = ModDownloadLib.McInstallState
            };
            Loader.Start(TargetFolder);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
            return Loader;
        }

        #endregion

    }
}