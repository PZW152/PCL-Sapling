using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf;
using MeloongCore.Wpf.Extensions;

namespace PCL
{
    public class CrashAnalyzer
    {

        // 构造函数
        private string TempFolder;
        private ModMinecraft.McInstance TargetInstance;
        public CrashAnalyzer(ModMinecraft.McInstance TargetInstance = null)
        {
            this.TargetInstance = TargetInstance;
            // 构建文件结构
            TempFolder = ModMain.RequestTaskTempFolder();
            DirectoryUtils.Create(TempFolder + @"Temp\");
            DirectoryUtils.Create(TempFolder + @"Report\");
            Logger.Info($"崩溃分析暂存文件夹：{TempFolder}");
        }

        // 1：准备用于分析的 Log 文件
        private List<KeyValuePair<string, string[]>> AnalyzeRawFiles = new List<KeyValuePair<string, string[]>>(); // 暂存的日志文件：文件完整路径 -> 文件内容
                                                                                                                   /// <summary>
    /// 将可用于分析的日志存储到 AnalyzeRawFiles。
    /// </summary>
    /// <param name="LatestLog">从 PCL 捕获到的最后 200 行程序输出。</param>
        public void Collect(string VersionPathIndie, IList<string> LatestLog = null)
        {
            Logger.Info("步骤 1：收集日志文件");

            // 简单收集可能的日志文件路径
            var PossibleLogs = new List<string>();
            try
            {
                PossibleLogs.AddRange(DirectoryUtils.EnumerateFiles(VersionPathIndie + @"crash-reports\", true));
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "收集 Minecraft 崩溃日志文件夹下的日志失败");
            }
            try
            {
                foreach (var File in DirectoryUtils.GetInfo(VersionPathIndie).Parent.Parent.EnumerateFiles())
                {
                    if ((File.Extension ?? "") != ".log")
                        continue;
                    PossibleLogs.Add(PathUtils.RemoveExtendedPrefix(File.FullName));
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "收集 Minecraft 主文件夹下的日志失败");
            }
            try
            {
                foreach (var File in DirectoryUtils.GetInfo(VersionPathIndie).EnumerateFiles())
                {
                    if ((File.Extension ?? "") != ".log")
                        continue;
                    PossibleLogs.Add(PathUtils.RemoveExtendedPrefix(File.FullName));
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "收集 Minecraft 隔离文件夹下的日志失败");
            }
            PossibleLogs.Add(VersionPathIndie + @"logs\latest.log"); // Minecraft 日志
            PossibleLogs.Add(VersionPathIndie + @"logs\debug.log"); // Minecraft Debug 日志
            PossibleLogs = PossibleLogs.Distinct().ToList();

            // 确定最新的日志文件
            var RightLogs = new List<string>();
            foreach (var LogFile in PossibleLogs)
            {
                try
                {
                    var Info = FileUtils.GetInfo(LogFile);
                    if (!Info.Exists)
                        continue;
                    double Time = Math.Abs((Info.LastWriteTime - DateTime.Now).TotalMinutes);
                    if (Time < 3d && Info.Length > 0L)
                    {
                        RightLogs.Add(LogFile);
                        Logger.Info($"可能可用的日志文件：{LogFile}（{Math.Round(Time, 1)} 分钟）");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"确认崩溃日志时间失败（{LogFile}）");
                }
            }
            if (!RightLogs.Any())
                Logger.Info("未发现可能可用的日志文件");

            // 将可能可用的日志文件导出
            foreach (var FilePath in RightLogs)
            {
                try
                {
                    AnalyzeRawFiles.Add(new KeyValuePair<string, string[]>(FilePath, FileUtils.ReadAsLines(FilePath)));
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"读取可能的崩溃日志文件失败（{FilePath}）");
                }
            }
            if (LatestLog is not null && LatestLog.Any())
            {
                string RawOutput = LatestLog.Join(ModBase.CrLf);
                Logger.Info($"以下为游戏输出的最后一段内容：{ModBase.CrLf}{RawOutput}");
                FileUtils.Write(TempFolder + "RawOutput.log", RawOutput);
                AnalyzeRawFiles.Add(new KeyValuePair<string, string[]>(TempFolder + "RawOutput.log", LatestLog.ToArray()));
                LatestLog.Clear();
            }

            Logger.Info($"步骤 1：收集日志文件完成，收集到 {AnalyzeRawFiles.Count} 个文件");
        }
        /// <summary>
    /// 从文件路径直接导入日志文件或崩溃报告压缩包。
    /// </summary>
        public void Import(string FilePath)
        {
            Logger.Info("步骤 1：自主导入日志文件");

            // 尝试视作压缩包解压
            bool Extracted = false;
            try
            {
                var Info = FileUtils.GetInfo(FilePath);
                if (Info.Exists && Info.Length > 0L && !(PathUtils.GetExtension(FilePath) == "jar"))
                {
                    FileUtils.ExtractToDirectory(FilePath, TempFolder + @"Temp\");
                    Logger.Info($"已解压导入的日志文件：{FilePath}");
                    Extracted = true;
                }
            }
            catch
            {
            }
            // 并非压缩包
            if (!Extracted)
            {
                FileUtils.Copy(FilePath, TempFolder + @"Temp\" + PathUtils.GetLastPart(FilePath));
                Logger.Info($"已复制导入的日志文件：{FilePath}");
            }

            // 导入其中的日志文件
            foreach (FileInfo TargetFile in DirectoryUtils.GetInfo(TempFolder + @"Temp\").EnumerateFiles())
            {
                try
                {
                    if (!TargetFile.Exists || TargetFile.Length == 0L)
                        continue;
                    string Ext = TargetFile.Extension.Lower();
                    if (Ext == ".log" || Ext == ".txt")
                    {
                        AnalyzeRawFiles.Add(new KeyValuePair<string, string[]>(PathUtils.RemoveExtendedPrefix(TargetFile.FullName), FileUtils.ReadAsLines(TargetFile.FullName)));
                    }
                    else
                    {
                        FileUtils.Delete(TargetFile.FullName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "导入单个日志文件失败");
                }
            }

            Logger.Info($"步骤 1：自主导入日志文件，收集到 {AnalyzeRawFiles.Count} 个文件");
        }

        // 2：确认实际用于分析的 Log 文本
        private enum AnalyzeFileType
        {
            HsErr,
            MinecraftLog,
            ExtraLogFile,
            ExtraReportFile,
            CrashReport
        }
        private KeyValuePair<string, string[]>? DirectFile = default; // 在弹窗中选择直接打开的文件
                                                                      /// <summary>
    /// 从 AnalyzeRawFiles 中提取实际有用的文本片段存储到 AnalyzeFiles，并整理可用于生成报告的文件。
    /// 返回是否有足够信息可用于分析。
    /// </summary>
        public bool Prepare()
        {
            Logger.Info("步骤 2：准备日志文本");

            // 对日志文件进行分类
            DirectFile = default;
            var AllFiles = new List<KeyValuePair<AnalyzeFileType, KeyValuePair<string, string[]>>>();
            foreach (var LogFile in AnalyzeRawFiles)
            {
                string MatchName = PathUtils.GetLastPart(LogFile.Key).Lower();
                AnalyzeFileType TargetType;
                if (MatchName.StartsWithF("hs_err"))
                {
                    TargetType = AnalyzeFileType.HsErr;
                    if (DirectFile is null)
                        DirectFile = LogFile;
                }
                else if (MatchName.StartsWithF("crash-"))
                {
                    TargetType = AnalyzeFileType.CrashReport;
                    if (DirectFile is null)
                        DirectFile = LogFile;
                }
                else if (MatchName == "latest.log" || MatchName == "latest log.txt" || MatchName == "debug.log" || MatchName == "debug log.txt" || MatchName == "游戏崩溃前的输出.txt" || MatchName == "rawoutput.log")
                {
                    TargetType = AnalyzeFileType.MinecraftLog;
                    DirectFile = LogFile;
                }
                else if (MatchName == "启动器日志.txt" || MatchName == "PCL2 启动器日志.txt" || MatchName == "PCL 启动器日志.txt" || MatchName == "log1.txt")
                {
                    if (LogFile.Value.Any(s => s.Contains("以下为游戏输出的最后一段内容")))
                    {
                        TargetType = AnalyzeFileType.MinecraftLog;
                        if (DirectFile is null)
                            DirectFile = LogFile;
                    }
                    else
                    {
                        TargetType = AnalyzeFileType.ExtraLogFile;
                    }
                }
                else if (PathUtils.GetExtension(MatchName) == "log")
                {
                    TargetType = AnalyzeFileType.ExtraLogFile;
                }
                else if (PathUtils.GetExtension(MatchName) == "txt")
                {
                    TargetType = AnalyzeFileType.ExtraReportFile;
                }
                else
                {
                    Logger.Info($"{MatchName} 分类为 Ignore");
                    continue;
                }
                if (LogFile.Value.Any())
                {
                    AllFiles.Add(new KeyValuePair<AnalyzeFileType, KeyValuePair<string, string[]>>(TargetType, LogFile));
                    Logger.Info($"{MatchName} 分类为 {TargetType}");
                }
                else
                {
                    Logger.Info($"{MatchName} 由于内容为空跳过");
                }
            }

            // 若只有额外日志，则将它们视作 Minecraft 日志
            if (AllFiles.Any() && AllFiles.All(p => p.Key == AnalyzeFileType.ExtraLogFile))
            {
                Logger.Info("由于仅发现了额外日志，将它们视作 Minecraft 日志进行分析");
                AllFiles = AllFiles.Select(p => new KeyValuePair<AnalyzeFileType, KeyValuePair<string, string[]>>(AnalyzeFileType.MinecraftLog, p.Value)).ToList();
            }

            // 将分类后的文件分别写入
            foreach (var SelectType in new[] { AnalyzeFileType.MinecraftLog, AnalyzeFileType.HsErr, AnalyzeFileType.ExtraLogFile, AnalyzeFileType.CrashReport })
            {
                // 获取该种类的所有文件 {文件路径 -> 文件内容行}
                var SelectedFiles = new List<KeyValuePair<string, string[]>>();
                foreach (var File in AllFiles)
                {
                    if (SelectType == File.Key)
                        SelectedFiles.Add(File.Value);
                }
                if (!SelectedFiles.Any())
                    continue;
                try
                {
                    // 根据文件类别判断
                    switch (SelectType)
                    {
                        case AnalyzeFileType.HsErr:
                        case AnalyzeFileType.CrashReport:
                            {
                                // 获取文件的修改日期
                                var DatedFiles = new SortedList<DateTime, KeyValuePair<string, string[]>>();
                                foreach (var File in SelectedFiles)
                                {
                                    try
                                    {
                                        DatedFiles.Add(FileUtils.GetInfo(File.Key).LastWriteTime, File);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Warn(ex, "获取日志文件修改时间失败");
                                        DatedFiles.Add(new DateTime(1900, 1, 1), File);
                                    }
                                }
                                // 输出最新的文件
                                var NewestFile = DatedFiles.Last().Value;
                                OutputFiles.Add(NewestFile.Key);
                                if (SelectType == AnalyzeFileType.HsErr)
                                {
                                    LogHs = GetHeadTailLines(NewestFile.Value, 200, 100);
                                    Logger.Info($"输出报告：{NewestFile.Key}，作为虚拟机错误信息");
                                    Logger.Info($"导入分析：{NewestFile.Key}，作为虚拟机错误信息");
                                }
                                else
                                {
                                    LogCrash = GetHeadTailLines(NewestFile.Value, 300, 700);
                                    Logger.Info($"输出报告：{NewestFile.Key}，作为 Minecraft 崩溃报告");
                                    Logger.Info($"导入分析：{NewestFile.Key}，作为 Minecraft 崩溃报告");
                                }

                                break;
                            }
                        case AnalyzeFileType.MinecraftLog:
                            {
                                LogMc = "";
                                LogMcDebug = "";
                                // 创建文件名词典
                                var FileNameDict = new Dictionary<string, KeyValuePair<string, string[]>>();
                                foreach (var SelectedFile in SelectedFiles)
                                {
                                    FileNameDict[PathUtils.GetLastPart(SelectedFile.Key).Lower()] = SelectedFile;
                                    OutputFiles.Add(SelectedFile.Key);
                                    Logger.Info($"输出报告：{SelectedFile.Key}，作为 Minecraft 或启动器日志");
                                }
                                // 选择一份最佳的来自启动器的游戏日志
                                foreach (string FileName in new[] { "rawoutput.log", "启动器日志.txt", "log1.txt", "游戏崩溃前的输出.txt", "PCL2 启动器日志.txt", "PCL 启动器日志.txt" })
                                {
                                    if (!FileNameDict.ContainsKey(FileName))
                                        continue;
                                    var CurrentLog = FileNameDict[FileName];
                                    // 截取 “以下为游戏输出的最后一段内容” 后的内容
                                    bool HasLauncherMark = false;
                                    foreach (var Line in CurrentLog.Value)
                                    {
                                        if (HasLauncherMark)
                                        {
                                            LogMc += Line + ModBase.Lf;
                                        }
                                        else if (Line.Contains("以下为游戏输出的最后一段内容"))
                                        {
                                            HasLauncherMark = true;
                                            Logger.Info("找到 PCL 输出的游戏实时日志头");
                                        }
                                    }
                                    // 导入后 500 行
                                    if (!HasLauncherMark)
                                        LogMc += GetHeadTailLines(CurrentLog.Value, 0, 500);
                                    LogMc = LogMc.TrimEnd(ModBase.CrLf.ToCharArray());
                                    Logger.Info($"导入分析：{CurrentLog.Key}，作为启动器日志");
                                    break;
                                }
                                // 选择一份最佳的 Minecraft Log
                                foreach (string FileName in new[] { "latest.log", "latest log.txt", "debug.log", "debug log.txt" })
                                {
                                    if (!FileNameDict.ContainsKey(FileName))
                                        continue;
                                    var CurrentLog = FileNameDict[FileName];
                                    LogMc += GetHeadTailLines(CurrentLog.Value, 1500, 500);
                                    Logger.Info($"导入分析：{CurrentLog.Key}，作为 Minecraft 日志");
                                    break;
                                }
                                // 查找 Debug Log
                                foreach (string FileName in new[] { "debug.log", "debug log.txt" })
                                {
                                    if (!FileNameDict.ContainsKey(FileName))
                                        continue;
                                    var CurrentLog = FileNameDict[FileName];
                                    LogMcDebug += GetHeadTailLines(CurrentLog.Value, 1000, 0);
                                    Logger.Info($"导入分析：{CurrentLog.Key}，作为 Minecraft Debug 日志");
                                    break;
                                }
                                // 兜底
                                if (string.IsNullOrEmpty(LogMc))
                                {
                                    if (!string.IsNullOrEmpty(LogMcDebug)) // 如果没有找到 Minecraft 日志，则使用 Debug 日志作为兜底
                                    {
                                        LogMc = LogMcDebug;
                                    }
                                    else if (FileNameDict.Any()) // 如果都没有找到，则使用第一个文件
                                    {
                                        var CurrentLog = FileNameDict.First().Value;
                                        LogMc += GetHeadTailLines(CurrentLog.Value, 1500, 500);
                                        Logger.Info($"导入分析：{CurrentLog.Key}，作为兜底日志");
                                    }
                                    else
                                    {
                                        LogMc = null;
                                        throw new Exception("无法找到匹配的 Minecraft Log");
                                    }
                                }
                                if (string.IsNullOrEmpty(LogMcDebug))
                                    LogMcDebug = null;
                                break;
                            }
                        case AnalyzeFileType.ExtraLogFile:
                        case AnalyzeFileType.ExtraReportFile:
                            {
                                // 全部丢过去
                                foreach (var SelectedFile in SelectedFiles)
                                {
                                    OutputFiles.Add(SelectedFile.Key);
                                    Logger.Info($"输出报告：{SelectedFile.Key}，不用作分析");
                                }

                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "分类处理日志文件时出错");
                }
            }

            // 结束
            bool Result = LogMc is not null || LogHs is not null || LogCrash is not null;
            if (Result)
            {
                Logger.Info($"{("[Crash] 步骤 2：准备日志文本完成，找到" + (LogMc is null ? "" : "游戏日志、") + (LogMcDebug is null ? "" : "游戏 Debug 日志、") + (LogHs is null ? "" : "虚拟机日志、") + (LogCrash is null ? "" : "崩溃日志、")).TrimEnd('、')}用作分析");
            }
            else
            {
                Logger.Info("步骤 2：准备日志文本完成，没有任何可供分析的日志");
            }
            return Result;
        }
        /// <summary>
    /// 输出字符串的前后某些行，并统一行尾为 vbLf (正则 \n)、删除空行和重复行。
    /// </summary>
        private string GetHeadTailLines(string[] Raw, int HeadLines, int TailLines)
        {
            if (Raw.Length <= HeadLines + TailLines)
                return Raw.Distinct().Join(ModBase.Lf);
            var Lines = new List<string>();
            int RealHeadLines = 0;
            int ViewedLines;
            var loopTo = Raw.Length - 1;
            for (ViewedLines = 0; ViewedLines <= loopTo; ViewedLines++)
            {
                if (Lines.Contains(Raw[ViewedLines]))
                    continue;
                RealHeadLines += 1;
                Lines.Add(Raw[ViewedLines]);
                if (RealHeadLines >= HeadLines)
                    break;
            }
            int RealTailLines = 0;
            for (int i = Raw.Length - 1, loopTo1 = ViewedLines; i >= loopTo1; i -= 1)
            {
                if (Lines.Contains(Raw[i]))
                    continue;
                RealTailLines += 1;
                Lines.Insert(RealHeadLines, Raw[i]);
                if (RealTailLines >= TailLines)
                    break;
            }
            var Result = new StringBuilder();
            foreach (var Line in Lines)
            {
                if (string.IsNullOrEmpty(Line))
                    continue;
                Result.Append(Line);
                Result.Append(ModBase.Lf);
            }
            return Result.ToString();
        }

        // 3：根据文本分析崩溃原因
        private string LogMc = null;
        private string LogMcDebug = null;
        private string LogHs = null;
        private string LogCrash = null;
        private string LogAll;
        // 日志规则匹配，以及其找到的可能导致崩溃的原因与附加信息
        private CrashLogRules Rules;
        private Dictionary<CrashReason, List<string>> CrashReasons = new Dictionary<CrashReason, List<string>>();
        /// <summary>
    /// 根据 AnalyzeLogs 与可能的版本信息分析崩溃原因。
    /// </summary>
        public void Analyze()
        {
            Logger.Info("步骤 3：分析崩溃原因");
            Rules = new CrashLogRules(LogMc, LogMcDebug, LogHs, LogCrash, AnalyzeModName, Text => Logger.Info(Text));
            CrashReasons = Rules.Reasons;
            LogAll = Rules.LogAll;

            // 按优先级依次分析，一旦找到原因就停止
            void FindReasons()
            {
                // 1. 精准日志匹配，中/高优先级
                Rules.Crit1();
                if (CrashReasons.Any())
                    return;
                Rules.Crit2();
                if (CrashReasons.Any())
                    return;

                // 2. 堆栈分析
                if (LogAll.Contains("orge") || LogAll.Contains("abric") || LogAll.Contains("uilt") || LogAll.Contains("iteloader"))
                {
                    var Keywords = new List<string>();
                    // 崩溃日志
                    if (LogCrash is not null)
                    {
                        Logger.Info("开始进行崩溃日志堆栈分析");
                        Keywords.AddRange(CrashLogRules.StackKeywords(LogCrash.BeforeFirst("System Details"), Text => Logger.Info(Text)));
                    }
                    // Minecraft 日志
                    if (LogMc is not null)
                    {
                        var Fatals = LogMc.RegexSearch(@"/FATAL] .+?(?=[\n]+\[)").ToList();
                        if (LogMc.Contains("Unreported exception thrown!"))
                            Fatals.Add(LogMc.Between("Unreported exception thrown!", "at oolloo.jlw.Wrapper"));
                        Logger.Info($"开始进行 Minecraft 日志堆栈分析，发现 {Fatals.Count} 个报错项");
                        foreach (var Fatal in Fatals)
                            Keywords.AddRange(CrashLogRules.StackKeywords(Fatal, Text => Logger.Info(Text)));
                    }
                    // 虚拟机日志
                    if (LogHs is not null)
                    {
                        Logger.Info("开始进行虚拟机堆栈分析");
                        string StackLogs = LogHs.Between("T H R E A D", "Registers:");
                        Keywords.AddRange(CrashLogRules.StackKeywords(StackLogs, Text => Logger.Info(Text)));
                    }
                    // Mod 名称分析
                    if (Keywords.Any())
                    {
                        var Names = AnalyzeModName(Keywords);
                        if (Names is null)
                        {
                            Rules.Append(CrashReason.堆栈分析发现关键字, Keywords);
                        }
                        else
                        {
                            Rules.Append(CrashReason.堆栈分析发现Mod名称, Names);
                        }
                        return;
                    }
                }
                else
                {
                    Logger.Info("可能并未安装 Mod，不进行堆栈分析");
                }

                // 3. 精准日志匹配，低优先级
                Rules.Crit3();
            }
            FindReasons();

            // 输出到日志
            if (!CrashReasons.Any())
            {
                Logger.Info("步骤 3：分析崩溃原因完成，未找到可能的原因");
            }
            else
            {
                Logger.Info($"步骤 3：分析崩溃原因完成，找到 {CrashReasons.Count} 条可能的原因");
                foreach (var Reason in CrashReasons)
                    Logger.Info($"- {Reason.Key}{(Reason.Value.Any() ? "（" + Reason.Value.Join('；') + "）" : "")}");
            }
        }
        
        // 具体的分析代码
        /// <summary>
    /// 根据 Mod 关键词尝试获取实际的 Mod 名称。
    /// 若失败则返回 Nothing。
    /// </summary>
        private List<string> AnalyzeModName(IEnumerable<string> Keywords)
        {
            var ModFileNames = new List<string>();

            // 预处理关键词（分割括号）
            var RealKeywords = new List<string>();
            foreach (var Keyword in Keywords)
            {
                foreach (var SubKeyword in Keyword.Split("("))
                    RealKeywords.Add(SubKeyword.Trim(" )".ToCharArray()));
            }
            Keywords = RealKeywords;

            // 从崩溃报告获取 Mod 信息
            if (LogCrash is not null && LogCrash.Contains("A detailed walkthrough of the error"))
            {
                string Details = LogCrash.Replace("A detailed walkthrough of the error", "¨");
                bool IsFabricDetail = Details.Contains("Fabric Mods"); // 是否为 Fabric 信息格式
                if (IsFabricDetail)
                {
                    Details = Details.Replace("Fabric Mods", "¨");
                    Logger.Info("崩溃报告中检测到 Fabric Mod 信息格式");
                }
                Details = Details.AfterLast("¨");

                // [Forge] 获取所有包含 .jar 的行
                // [Fabric] 获取所有包含 Mod 信息的行
                var ModNameLines = new List<string>();
                foreach (var Line in Details.Split(ModBase.Lf))
                {
                    if (Line.ContainsIgnoreCase(".jar") && Line.Length - Line.Replace(".jar", "").Length == 4 || IsFabricDetail && Line.StartsWithF(ModBase.Tab + ModBase.Tab) && !Line.RegexCheck(@"\t\tfabric[\w-]*: Fabric")) // 只有一个 .jar
                        ModNameLines.Add(Line);
                }
                Logger.Info($"崩溃报告中找到 {ModNameLines.Count} 个可能的 Mod 项目行");

                // 获取 Mod ID 与关键词的匹配行
                var HintLines = new List<string>();
                foreach (string KeyWord in Keywords)
                {
                    foreach (string ModString in ModNameLines)
                    {
                        string RealModString = ModString.Lower().Replace("_", "");
                        if (!RealModString.Contains(KeyWord.Lower().Replace("_", "")))
                            continue;
                        if (RealModString.Contains("minecraft.jar") || RealModString.Contains(" forge-") || RealModString.Contains(" mixin-"))
                            continue;
                        HintLines.Add(ModString.Trim(ModBase.CrLf.ToCharArray()));
                        break;
                    }
                }
                HintLines = HintLines.Distinct().ToList();
                Logger.Info($"崩溃报告中找到 {HintLines.Count} 个可能的崩溃 Mod 匹配行");
                foreach (string ModLine in HintLines)
                    Logger.Info($"- {ModLine}");

                // 从 Mod 匹配行中提取 .jar 文件的名称
                foreach (string Line in HintLines)
                {
                    string Name;
                    if (IsFabricDetail)
                    {
                        Name = Line.RegexSeek(@"(?<=: )[^\n]+(?= [^\n]+)");
                    }
                    else
                    {
                        Name = Line.RegexSeek(@"(?<=\()[^\t]+.jar(?=\))|(?<=(\t\t)|(\| ))[^\t\|]+.jar", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }
                    if (Name is not null)
                        ModFileNames.Add(Name);
                }

            }

            // 从 debug.log 获取 Mod 信息
            if (LogMcDebug is not null)
            {

                // Forge: Found valid mod file YungsBetterStrongholds-1.20-Forge-4.0.1.jar with {betterstrongholds} mods - versions {1.20-Forge-4.0.1}
                var ModNameLines = LogMcDebug.RegexSearch("(?<=valid mod file ).*", System.Text.RegularExpressions.RegexOptions.Multiline);
                Logger.Info($"Debug 信息中找到 {ModNameLines.Count()} 个可能的 Mod 项目行");

                // 获取 Mod ID 与关键词的匹配行
                var HintLines = new List<string>();
                foreach (string KeyWord in Keywords)
                {
                    foreach (string ModString in ModNameLines)
                    {
                        if (ModString.Contains($"{{{KeyWord}}}"))
                            HintLines.Add(ModString);
                    }
                }
                HintLines = HintLines.Distinct().ToList();
                Logger.Info($"Debug 信息中找到 {HintLines.Count} 个可能的崩溃 Mod 匹配行");
                foreach (string ModLine in HintLines)
                    Logger.Info($"- {ModLine}");

                // 从 Mod 匹配行中提取 .jar 文件的名称
                foreach (string Line in HintLines)
                {
                    string Name;
                    Name = Line.RegexSeek(".*(?= with)");
                    if (Name is not null)
                        ModFileNames.Add(Name);
                }

            }

            // 输出
            ModFileNames = ModFileNames.Distinct().ToList();
            if (!ModFileNames.Any())
            {
                return null;
            }
            else
            {
                Logger.Info($"找到 {ModFileNames.Count} 个可能的崩溃 Mod 文件名");
                foreach (string ModFileName in ModFileNames)
                    Logger.Info($"- {ModFileName}");
                return ModFileNames;
            }
        }
        /// <summary>
    /// 尝试从关键字获取 Mod 名称，若失败则返回原关键字。
    /// </summary>
        private List<string> TryAnalyzeModName(string Keyword)
        {
            var RawList = new List<string>() { Keyword ?? "" };
            if (string.IsNullOrEmpty(Keyword))
                return RawList;
            return AnalyzeModName(RawList) ?? RawList;
        }
        /// <summary>
    /// 尝试从关键字获取 Mod 名称，若失败则返回原关键字。
    /// </summary>
        private List<string> TryAnalyzeModName(IEnumerable<string> Keywords)
        {
            if (!Keywords.Any())
                return Keywords.ToList();
            return AnalyzeModName(Keywords) ?? Keywords.ToList();
        }

        // 4：根据原因输出信息
        private List<string> OutputFiles = new List<string>();
        /// <summary>
    /// 弹出崩溃弹窗，并指导导出崩溃报告。
    /// </summary>
        public void Output(bool IsManualAnalyze, List<string> ExtraFiles = null)
        {
            // 弹窗提示
            ModMain.FrmMain.ShowWindowToTop();
            string Detail = GetAnalyzeResult(IsManualAnalyze);
            // 弹窗选择：查看输出
            switch (ModMain.MyMsgBox(Detail, IsManualAnalyze ? "错误报告分析结果" : "Minecraft 出现错误", "确定", IsManualAnalyze || DirectFile is null ? "" : "查看输出", IsManualAnalyze ? "" : "导出错误报告", Button2Action: IsManualAnalyze || DirectFile is null ? (Action)null : new Action(() => { if (FileUtils.Exists(DirectFile.Value.Key)) { ModBase.StartProcess(DirectFile.Value.Key); } else { string FilePath = ModBase.PathTemp + "Crash.txt"; FileUtils.Write(FilePath, DirectFile.Value.Value.Join(ModBase.CrLf)); ModBase.StartProcess(FilePath); } })))
            {
                case 3:
                    {
                        // 弹窗选择：导出错误报告
                        string FileAddress = null;
                        try
                        {
                            // 获取文件路径
                            ModBase.RunInUiWait(() => FileAddress = Dialogs.SaveFile("选择保存位置", "错误报告-" + DateTime.Now.ToString("G").Replace("/", "-").Replace(":", ".").Replace(" ", "_") + ".zip", filter: new[] { ("zip", "Minecraft 错误报告") }));
                            if (FileAddress is null)
                                return;
                            FileUtils.Delete(FileAddress);
                            // 输出诊断信息
                            ModMain.FeedbackInfo();
                            Logger.Instance.Flush();
                            // 复制文件
                            if (ExtraFiles is not null)
                                OutputFiles.AddRange(ExtraFiles);
                            foreach (var OutputFile in OutputFiles)
                            {
                                string FileName = PathUtils.GetLastPart(OutputFile);
                                Encoding FileEncoding = null;
                                switch (FileName ?? "")
                                {
                                    case "LatestLaunch.bat":
                                        {
                                            FileName = "启动脚本.bat";
                                            break;
                                        }
                                    case "Log1.txt":
                                        {
                                            FileName = "PCL 启动器日志.txt";
                                            FileEncoding = Encoding.UTF8;
                                            break;
                                        }
                                    case "RawOutput.log":
                                        {
                                            FileName = "游戏崩溃前的输出.txt";
                                            FileEncoding = Encoding.UTF8;
                                            break;
                                        }
                                }
                                if (!FileUtils.Exists(OutputFile))
                                    continue;
                                string FileContent = FileUtils.ReadAsString(OutputFile, FileEncoding);
                                FileContent = ModMinecraft.FilterUserName(ModMinecraft.FilterAccessToken(FileContent, Coerce.ToChar(FileName == "启动脚本.bat" ? "F" : "*")), '*');
                                FileUtils.Write(TempFolder + @"Report\" + FileName, FileContent, FileEncoding);
                                Logger.Info($"导出错误报告文件：{FileName}");
                            }
                            // 导出报告
                            FileUtils.CreateZipFromDirectory(FileAddress, TempFolder + @"Report\");
                            DirectoryUtils.Delete(TempFolder + @"Report\");
                            ModMain.Hint("错误报告已导出！", ModMain.HintType.Green);
                            ModBase.OpenExplorer(FileAddress);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "导出错误报告失败");
                            return;
                        }

                        break;
                    }
            }
        }
        /// <summary>
    /// 获取崩溃分析的结果描述。
    /// </summary>
        private string GetAnalyzeResult(bool IsHandAnalyze)
        {

            // 没有结果的处理
            if (!CrashReasons.Any())
            {
                if (IsHandAnalyze)
                {
                    return "很抱歉，PCL 无法确定错误原因。";
                }
                else
                {
                    return $"很抱歉，你的游戏出现了一些问题……{ModBase.CrLf}如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。";
                }
            }

            // 根据不同原因判断
            var Results = new List<string>();
            foreach (var Reason in CrashReasons)
            {
                var Additional = Reason.Value;
                switch (Reason.Key)
                {
                    case CrashReason.Java虚拟机参数有误:
                        {
                            Results.Add(@"由于 Java 参数有误，导致游戏无法继续运行。\n请检查高级选项中的 Java 虚拟机参数和游戏参数设置是否有误。");
                            break;
                        }
                    case CrashReason.Mod文件被解压:
                        {
                            Results.Add(@"由于 Mod 文件被解压了，导致游戏无法继续运行。\n直接把整个 Mod 文件放进 Mod 文件夹中即可，若解压就会导致游戏出错。\n\n请删除 Mod 文件夹中已被解压的 Mod，然后再启动游戏。");
                            break;
                        }
                    case CrashReason.内存不足:
                        {
                            Results.Add(@"Minecraft 内存不足，导致其无法继续运行。\n这很可能是因为电脑内存不足、游戏分配的内存不足，或是配置要求过高。\n\n请在启动设置中增加为游戏分配的内存，并删除配置要求较高的材质、Mod、光影。\n如果依然不奏效，请在开始游戏前尽量关闭其他软件，或者……换台电脑？\h");
                            break;
                        }
                    case CrashReason.使用OpenJ9:
                        {
                            Results.Add(@"游戏因为使用 OpenJ9 而崩溃了。\n请在版本设置的 Java 选项中改用非 OpenJ9 的 Java，然后再启动游戏。");
                            break;
                        }
                    case CrashReason.使用JDK:
                        {
                            Results.Add(@"游戏似乎因为使用 JDK，或 Java 版本过高而崩溃了。\n请在版本设置的 Java 选项中改用 Java 8，然后再启动游戏。\n如果你没有安装 Java 8，可以从网络中下载、安装一个。");
                            break;
                        }
                    case CrashReason.Java版本过高:
                        {
                            Results.Add(@"游戏似乎因为你所使用的 Java 版本过高而崩溃了。\n请在版本设置的 Java 选项中改用较低版本的 Java，然后再启动游戏。\n如果没有，可以从网络中下载、安装一个。");
                            break;
                        }
                    case CrashReason.Java版本不兼容:
                        {
                            Results.Add(@"游戏不兼容你当前使用的 Java。\n请在版本设置的 Java 选项中改用合适版本的 Java，然后再启动游戏。\n如果你没有安装任何 Mod 并且因此崩溃，请向 PCL 提交反馈。");
                            break;
                        }
                    case CrashReason.Mod名称包含特殊字符:
                        {
                            Results.Add(@"由于有 Mod 的名称包含特殊字符，导致游戏崩溃。\n请尝试修改 Mod 文件名，让它只包含英文字母、数字、减号（-）、下划线（_）和小数点，然后再启动游戏。");
                            break;
                        }
                    case CrashReason.MixinBootstrap缺失:
                        {
                            Results.Add(@"由于缺失 MixinBootstrap，导致游戏崩溃。\n请尝试安装 MixinBootstrap。若安装后依然崩溃，可以尝试在文件名前添加英文感叹号。");
                            break;
                        }
                    case CrashReason.使用32位Java导致JVM无法分配足够多的内存:
                        {
                            if (Environment.Is64BitOperatingSystem)
                            {
                                Results.Add(@"你似乎正在使用 32 位 Java，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n请在启动设置的 Java 选择一项中改用 64 位的 Java 再启动游戏，然后再启动游戏。\n如果你没有安装 64 位的 Java，你可以从网络中下载、安装一个。");
                            }
                            else
                            {
                                Results.Add(@"你正在使用 32 位的操作系统，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n你或许只能重装 64 位的操作系统来解决此问题。\n如果你的电脑内存在 2GB 以内，那或许只能换台电脑了……\h");
                            }

                            break;
                        }
                    case CrashReason.Mod缺少前置或MC版本错误:
                        {
                            if (Additional.Any())
                            {
                                Results.Add(@"由于未安装正确的前置 Mod，导致游戏退出。\n缺失的依赖项：\n - " + Additional.Join(@"\n - ") + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                            }
                            else
                            {
                                Results.Add(@"由于未安装正确的前置 Mod，导致游戏退出。\n请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                            }

                            break;
                        }
                    case CrashReason.堆栈分析发现关键字:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add("你的游戏遇到了一些问题，PCL 为此找到了一个可疑的关键词：" + Additional.First() + @"。\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h");
                            }
                            else
                            {
                                Results.Add(@"你的游戏遇到了一些问题，PCL 为此找到了以下可疑的关键词：\n - " + Additional.Join(", ") + @"\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h");
                            }

                            break;
                        }
                    case CrashReason.堆栈分析发现Mod名称:
                    case CrashReason.怀疑Mod导致游戏崩溃:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add("PCL 怀疑名为 " + Additional.First() + @" 的 Mod 导致了游戏出错，但不能完全确定。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                            }
                            else
                            {
                                Results.Add(@"PCL 怀疑以下 Mod 导致了游戏出错，但不能完全确定：\n - " + Additional.Join(@"\n - ") + @"\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                            }

                            break;
                        }
                    case CrashReason.确定Mod导致游戏崩溃:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add("名为 " + Additional.First() + @" 的 Mod 导致了游戏出错。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                            }
                            else
                            {
                                Results.Add(@"以下 Mod 导致了游戏出错：\n - " + Additional.Join(@"\n - ") + @"\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                            }

                            break;
                        }
                    case CrashReason.ModMixin失败:
                        {
                            if (Additional.Count == 0)
                            {
                                Results.Add(@"部分 Mod 注入失败，导致游戏出错。\n这一般代表着部分 Mod 与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试逐步禁用 Mod，然后观察游戏是否还会崩溃，以此定位导致崩溃的 Mod。\n\e\h");
                            }
                            else if (Additional.IsSingle())
                            {
                                Results.Add("名为 " + Additional.First() + @" 的 Mod 注入失败，导致游戏出错。\n这一般代表着它与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                            }
                            else
                            {
                                Results.Add(@"以下 Mod 导致了游戏出错：\n - " + Additional.Join(@"\n - ") + @"\n这一般代表着它们与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                            }

                            break;
                        }
                    case CrashReason.Mod配置文件导致游戏崩溃:
                        {
                            if (Additional[1] is null)
                            {
                                Results.Add("名为 " + Additional.First() + @" 的 Mod 导致了游戏出错。\n\e\h");
                            }
                            else
                            {
                                Results.Add("名为 " + Additional.First() + @" 的 Mod 导致了游戏出错：\n其配置文件 " + Additional[1] + " 存在异常，无法读取。");
                            }

                            break;
                        }
                    case CrashReason.Mod初始化失败:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add("名为 " + Additional.First() + @" 的 Mod 初始化失败，导致游戏无法继续加载。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                            }
                            else
                            {
                                Results.Add(@"以下 Mod 初始化失败，导致游戏出错：\n - " + Additional.Join(@"\n - ") + @"\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                            }

                            break;
                        }
                    case CrashReason.特定方块导致崩溃:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add("游戏似乎因为方块 " + Additional.First() + @" 出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是该方块导致出错，你或许需要使用一些方式删除此方块。\n - 若仍然出错，问题就可能来自其他原因……\h");
                            }
                            else
                            {
                                Results.Add(@"游戏似乎因为世界中的某些方块出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是某些方块导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\h");
                            }

                            break;
                        }
                    case CrashReason.Mod重复安装:
                        {
                            if (Additional.Count >= 2)
                            {
                                Results.Add(@"你重复安装了多个相同的 Mod：\n - " + Additional.Join(@"\n - ") + @"\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。");
                            }
                            else
                            {
                                Results.Add(@"你可能重复安装了多个相同的 Mod，导致游戏出错。\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。\e\h");
                            }

                            break;
                        }
                    case CrashReason.特定实体导致崩溃:
                        {
                            if (Additional.IsSingle())
                            {
                                if (Additional.First().Contains("minecraft:player"))
                                {
                                    if (Additional.First().Contains(" "))
                                    {
                                        Results.Add($@"游戏因为位于 {Additional.First().AfterFirst(" ")} 的玩家实体导致了崩溃。\h");
                                    }
                                    else
                                    {
                                        Results.Add($@"游戏因为玩家实体导致了崩溃。\h");
                                    }
                                }
                                else
                                {
                                    Results.Add($@"游戏因为实体 {Additional.First()} 导致了崩溃。\h");
                                }
                            }
                            else
                            {
                                Results.Add($@"游戏因为下列实体导致了崩溃：{Additional.Join('、')}\h");
                            }

                            break;
                        }
                    case CrashReason.OptiFine与Forge不兼容:
                        {
                            Results.Add(@"由于 OptiFine 与当前版本的 Forge 不兼容，导致了游戏崩溃。\n\n请前往 OptiFine 官网（https://optifine.net/downloads）查看 OptiFine 所兼容的 Forge 版本，并严格按照对应版本重新安装游戏。");
                            break;
                        }
                    case CrashReason.ShadersMod与OptiFine同时安装:
                        {
                            Results.Add(@"无需同时安装 OptiFine 和 Shaders Mod，OptiFine 已经集成了 Shaders Mod 的功能。\n在删除 Shaders Mod 后，游戏即可正常运行。");
                            break;
                        }
                    case CrashReason.低版本Forge与高版本Java不兼容:
                        {
                            Results.Add(@"由于低版本 Forge 与当前 Java 不兼容，导致了游戏崩溃。\n\n请尝试以下解决方案：\n - 更新 Forge 到 36.2.26 或更高版本\n - 换用版本低于 1.8.0.320 的 Java");
                            break;
                        }
                    case CrashReason.版本Json中存在多个Forge:
                        {
                            Results.Add(@"可能由于其他启动器修改了 Forge 版本，当前版本的文件存在异常，导致了游戏崩溃。\n请尝试重新全新安装 Forge，而非使用其他启动器修改 Forge 版本。");
                            break;
                        }
                    case CrashReason.玩家手动触发调试崩溃:
                        {
                            Results.Add(@"* 事实上，你的游戏没有任何问题，这是你自己触发的崩溃。\n* 你难道没有更重要的事要做吗？");
                            break;
                        }
                    case CrashReason.Mod需要Java11:
                        {
                            Results.Add(@"你所安装的部分 Mod 似乎需要使用 Java 11 启动。\n请在启动设置的 Java 选择一项中改用 Java 11，然后再启动游戏。\n如果你没有安装 Java 11，你可以从网络中下载、安装一个。");
                            break;
                        }
                    case CrashReason.极短的程序输出:
                        {
                            Results.Add($@"程序返回了以下信息：\n{Additional.First()}\n\h");
                            break;
                        }
                    case CrashReason.OptiFine导致无法加载世界: // https://www.minecraftforum.net/forums/support/java-edition-support/3051132-exception-ticking-world
                        {
                            Results.Add(@"你所使用的 OptiFine 可能导致了你的游戏出现问题。\n\n该问题只在特定 OptiFine 版本中出现，你可以尝试更换 OptiFine 的版本。\h");
                            break;
                        }
                    case CrashReason.显卡驱动不支持导致无法设置像素格式:
                    case CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION:
                    case CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION:
                    case CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION:
                    case CrashReason.显卡不支持OpenGL:
                        {
                            if (LogAll.Contains("hd graphics "))
                            {
                                Results.Add(@"你的显卡驱动存在问题，或未使用独立显卡，导致游戏无法正常运行。\n\n如果你的电脑存在独立显卡，请使用独立显卡而非 Intel 核显启动 PCL 与 Minecraft。\n如果问题依然存在，请尝试升级你的显卡驱动到最新版本，或回退到出厂版本。\n如果还是不行，还可以尝试使用 8.0.51 或更低版本的 Java。\h");
                            }
                            else
                            {
                                Results.Add(@"你的显卡驱动存在问题，导致游戏无法正常运行。\n\n请尝试升级你的显卡驱动到最新版本，或回退到出厂版本，然后再启动游戏。\n如果还是不行，可以尝试使用 8.0.51 或更低版本的 Java。\n如果问题依然存在，那么你可能需要换个更好的显卡……\h");
                            }

                            break;
                        }
                    case CrashReason.材质过大或显卡配置不足:
                        {
                            Results.Add(@"你所使用的材质分辨率过高，或显卡配置不足，导致游戏无法继续运行。\n\n如果你正在使用高清材质，请将它移除。\n如果你没有使用材质，那么你可能需要更新显卡驱动，或者换个更好的显卡……\h");
                            break;
                        }
                    case CrashReason.NightConfig的Bug:
                        {
                            Results.Add(@"由于 Night Config 存在问题，导致了游戏崩溃。\n你可以尝试安装 Night Config Fixes 模组，这或许能解决此问题。\h");
                            break;
                        }
                    case CrashReason.光影或资源包导致OpenGL1282错误:
                        {
                            Results.Add(@"你所使用的光影或材质导致游戏出现了一些问题……\n\n请尝试删除你所添加的这些额外资源。\h");
                            break;
                        }
                    case CrashReason.Mod过多导致超出ID限制:
                        {
                            Results.Add(@"你所安装的 Mod 过多，超出了游戏的 ID 限制，导致了游戏崩溃。\n请尝试安装 JEID 等修复 Mod，或删除部分大型 Mod。");
                            break;
                        }
                    case CrashReason.文件或内容校验失败:
                        {
                            Results.Add(@"部分文件或内容校验失败，导致游戏出现了问题。\n\n请尝试删除游戏（包括 Mod）并重新下载，或尝试在重新下载时使用 VPN。\h");
                            break;
                        }
                    case CrashReason.Forge安装不完整:
                        {
                            Results.Add(@"由于安装的 Forge 文件丢失，导致游戏无法正常运行。\n请重新安装一次相同版本的 Forge，然后再启动游戏。\n在打包游戏时删除 libraries 文件夹可能导致此错误。\h");
                            break;
                        }
                    case CrashReason.Fabric报错:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add(@"Fabric 提供了以下错误信息：\n" + Additional.First() + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                            }
                            else
                            {
                                Results.Add(@"Fabric 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                            }

                            break;
                        }
                    case CrashReason.Mod互不兼容:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add(@"你所安装的 Mod 不兼容：\n" + Additional.First() + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                            }
                            else
                            {
                                Results.Add(@"你所安装的 Mod 不兼容，Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                            }

                            break;
                        }
                    case CrashReason.Mod加载器报错:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add(@"Mod 加载器提供了以下错误信息：\n" + Additional.First() + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                            }
                            else
                            {
                                Results.Add(@"Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                            }

                            break;
                        }
                    case CrashReason.Fabric报错并给出解决方案:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add(@"Fabric 提供了以下解决方案：\n" + Additional.First() + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                            }
                            else
                            {
                                Results.Add(@"Fabric 可能已经提供了解决方案，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                            }

                            break;
                        }
                    case CrashReason.Forge报错:
                        {
                            if (Additional.IsSingle())
                            {
                                Results.Add(@"Forge 提供了以下错误信息：\n" + Additional.First() + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                            }
                            else
                            {
                                Results.Add(@"Forge 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                            }

                            break;
                        }
                    case CrashReason.没有可用的分析文件:
                        {
                            Results.Add(@"你的游戏出现了一些问题，但 PCL 未能找到相关记录文件，因此无法进行分析。\h");
                            break;
                        }

                    default:
                        {
                            Results.Add("PCL 获取到了没有详细信息的错误原因（" + ((int)CrashReasons.First().Key).ToString() + @"），请向 PCL 作者提交反馈以获取详情。\h");
                            break;
                        }
                }
            }

            return Results.Join(@"\n\n此外，").Replace(@"\n", ModBase.CrLf).Replace(@"\h", "").Replace(@"\e", IsHandAnalyze ? "" : ModBase.CrLf + "你可以查看错误报告了解错误具体是如何发生的。").ReplaceLineEndings(ModBase.CrLf).Trim(ModBase.CrLf.ToCharArray()) + (!Results.Any(r => r.EndsWithF(@"\h")) || IsHandAnalyze ? "" : ModBase.CrLf + "如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。");
        }

    }
}