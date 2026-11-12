using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf;
using MeloongCore.Wpf.Extensions;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class PageInstanceOverall
    {
        private void BtnManageShortcut_Click(object sender, MouseButtonEventArgs e) => ModShortcut.CreateDesktopShortcut(PageInstanceLeft.Instance);

        #region 事件

        private void OnBtnDisplayDesc_Click(object sender, MouseButtonEventArgs e) => BtnDisplayDesc_Click(sender, e);
        private void OnBtnDisplayRename_Click(object sender, MouseButtonEventArgs e) => BtnDisplayRename_Click(sender, e);
        private void ItemDisplayLogoCustom_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ComboDisplayLogo_SelectionChanged();
        private void OnComboDisplayLogo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ComboDisplayLogo_SelectionChanged();
        private void OnBtnDisplayStar_Click(object sender, MouseButtonEventArgs e) => BtnDisplayStar_Click(sender, e);
        private void OnBtnManageCheck_Click(object sender, MouseButtonEventArgs e) => BtnManageCheck_Click(sender, e);
        private void OnBtnManageDelete_Click(object sender, MouseButtonEventArgs e) => BtnManageDelete_Click(sender, e);

        #endregion


        private bool IsLoad = false;

        public PageInstanceOverall()
        {
            this.Loaded += PageSetupLaunch_Loaded;
            this.InitializeComponent();

        }
        private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
        {

            // 重复加载部分
            this.PanBack.ScrollToHome();

            // 更新设置
            Reload();

            // 非重复加载部分
            if (IsLoad)
                return;
            IsLoad = true;
            this.PanDisplay.TriggerForceResize();

        }

        public MyListItem InstanceHeaderItem;
        /// <summary>
    /// 确保当前页面上的信息已正确显示。
    /// </summary>
        private void Reload()
        {
            ModAnimation.AniControlEnabled += 1;

            // 刷新设置项目
            this.ComboDisplayType.SelectedIndex = Coerce.ToInt(ModBase.ReadIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "DisplayType", ((int)ModMinecraft.McInstanceCardType.Auto).ToString()));
            this.BtnDisplayStar.Text = PageInstanceLeft.Instance.IsStar ? "从收藏夹中移除" : "加入收藏夹";
            this.BtnFolderMods.Visibility = PageInstanceLeft.Instance.Modable ? Visibility.Visible : Visibility.Collapsed;
            // 刷新版本显示
            this.PanDisplayItem.Children.Clear();
            InstanceHeaderItem = PageInstanceLeft.Instance.ToListItem().Init();
            InstanceHeaderItem.IsHitTestVisible = false;
            // 在版本描述后附上游玩时间
            try
            {
                string IniPath = PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini";
                long Seconds = Coerce.ToLong(ModBase.ReadIni(IniPath, "PlayTimeSeconds", "0"));
                long LastTicks = Coerce.ToLong(ModBase.ReadIni(IniPath, "LastPlayed", "0"));
                string Played = PlayTime.Describe(TimeSpan.FromSeconds(Seconds), LastTicks > 0 ? new DateTime(LastTicks) : null, DateTime.Now);
                if (Played != "")
                    InstanceHeaderItem.Info = string.IsNullOrEmpty(InstanceHeaderItem.Info) ? Played : InstanceHeaderItem.Info + "；" + Played;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取游玩时间失败");
            }
            this.PanDisplayItem.Children.Add(InstanceHeaderItem);
            RefreshSaves();
            ModMain.FrmMain.PageNameRefresh();
            // 刷新版本图标
            this.ComboDisplayLogo.SelectedIndex = 0;
            string Logo = ModBase.ReadIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "Logo", "");
            bool LogoCustom = Coerce.ToBool(ModBase.ReadIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "LogoCustom", "False"));
            if (LogoCustom)
            {
                foreach (MyComboBoxItem Selection in this.ComboDisplayLogo.Items)
                {
                    if (Coerce.Equal(Selection.Tag, Logo) || Coerce.Equal(Selection.Tag, @"PCL\Logo.png") && Logo.EndsWithF(@"PCL\Logo.png"))
                    {
                        this.ComboDisplayLogo.SelectedItem = Selection;
                        break;
                    }
                }
            }

            ModAnimation.AniControlEnabled -= 1;
        }

        #region 卡片：个性化

        // 版本分类
        private void ComboDisplayType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(IsLoad && ModAnimation.AniControlEnabled == 0))
                return;
            if (this.ComboDisplayType.SelectedIndex != 1)
            {
                // 改为不隐藏
                try
                {
                    // 若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                    ModBase.WriteIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "DisplayType", this.ComboDisplayType.SelectedIndex.ToString());
                    PageInstanceLeft.Instance.DisplayType = (ModMinecraft.McInstanceCardType)Coerce.ToInt(ModBase.ReadIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "DisplayType", ((int)ModMinecraft.McInstanceCardType.Auto).ToString()));
                    ModMain.FrmInstanceLeft.RefreshModDisabled();

                    ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "InstanceCache", ""); // 要求刷新缓存
                    ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"修改版本分类失败（{PageInstanceLeft.Instance.Name}）");
                }
                Reload(); // 更新 “打开 Mod 文件夹” 按钮
            }
            else
            {
                // 改为隐藏
                try
                {
                    if (!Settings.Get<bool>("HintHide"))
                    {
                        if (ModMain.MyMsgBox("确认要从版本列表中隐藏该版本吗？隐藏该版本后，它将不再出现于 PCL 显示的版本列表中。" + ModBase.CrLf + "此后，在版本列表页面按下 F11 才可以查看被隐藏的版本。", "隐藏版本提示", Button2: "取消") != 1)
                        {
                            this.ComboDisplayType.SelectedIndex = 0;
                            return;
                        }
                        Settings.Set("HintHide", true);
                    }
                    ModBase.WriteIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "DisplayType", ((int)ModMinecraft.McInstanceCardType.Hidden).ToString());
                    ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "InstanceCache", ""); // 要求刷新缓存
                    ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"隐藏版本 {PageInstanceLeft.Instance.Name} 失败");
                }
            }
        }

        // 更改描述
        private void BtnDisplayDesc_Click(object sender, EventArgs e)
        {
            try
            {
                string OldInfo = ModBase.ReadIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "CustomInfo");
                string NewInfo = ModMain.MyMsgBoxInput("更改描述", "修改版本的描述文本，留空则使用 PCL 的默认描述。", OldInfo, new System.Collections.ObjectModel.Collection<ValidateType>(), "默认描述");
                if (NewInfo is not null && (OldInfo ?? "") != (NewInfo ?? ""))
                    ModBase.WriteIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "CustomInfo", NewInfo);
                PageInstanceLeft.Instance = new ModMinecraft.McInstance(PageInstanceLeft.Instance.Name).Load();
                Reload();
                ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"版本 {PageInstanceLeft.Instance.Name} 描述更改失败", LogBehavior.Alert);
            }
        }

        // 重命名版本
        private void BtnDisplayRename_Click(object sender, EventArgs e)
        {
            try
            {
                if (ModMinecraft.McInstanceLock.IsLocked(PageInstanceLeft.Instance))
                {
                    ModMain.Hint("该版本正在被修改，请稍后再重命名！", ModMain.HintType.Red);
                    return;
                }
                // 确认输入的新名称
                string OldName = PageInstanceLeft.Instance.Name;
                // 修改此部分的同时修改快速安装的版本名检测*
                string NewName = ModMain.MyMsgBoxInput("重命名版本", "", OldName, new System.Collections.ObjectModel.Collection<ValidateType>() { new ValidateFolderName(ModMinecraft.McFolderSelected + "versions", IgnoreList: new List<string>() { OldName }) });
                if (string.IsNullOrWhiteSpace(NewName))
                    return;
                var NewInstance = PageInstanceLeft.Instance.Rename(NewName);
                // 刷新与提示
                ModMain.Hint("重命名成功！", ModMain.HintType.Green);
                PageInstanceLeft.Instance = NewInstance;
                if (!(ModMinecraft.McInstanceSelected == null) && ModMinecraft.McInstanceSelected.Equals(PageInstanceLeft.Instance))
                    ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "Version", NewName);
                Reload();
                ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "重命名版本失败", LogBehavior.Alert);
            }
        }

        // 版本图标
        private void ComboDisplayLogo_SelectionChanged()
        {
            if (!(IsLoad && ModAnimation.AniControlEnabled == 0))
                return;
            // 选择 自定义 时修改图片
            try
            {
                if (object.ReferenceEquals(this.ComboDisplayLogo.SelectedItem, this.ItemDisplayLogoCustom))
                {
                    string FileName = Dialogs.SelectFile("选择图片", false, filter: new[] { (new[] { "png", "jpeg", "jpg", "gif", "webp" }, "常用图片文件") }).FirstOrDefault();
                    if (string.IsNullOrEmpty(FileName))
                    {
                        Reload(); // 还原选项
                        return;
                    }
                    FileUtils.Copy(FileName, PageInstanceLeft.Instance.PathVersion + @"PCL\Logo.png");
                }
                else
                {
                    FileUtils.Delete(PageInstanceLeft.Instance.PathVersion + @"PCL\Logo.png");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"更改自定义版本图标失败（{PageInstanceLeft.Instance.Name}）");
            }
            // 进行更改
            try
            {
                string NewLogo = Coerce.ToStr(this.ComboDisplayLogo.SelectedItem.Fe().Tag);
                ModBase.WriteIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "Logo", NewLogo);
                ModBase.WriteIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "LogoCustom", (!string.IsNullOrEmpty(NewLogo)).ToString());
                // 刷新显示
                ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "InstanceCache", ""); // 要求刷新缓存
                PageInstanceLeft.Instance = new ModMinecraft.McInstance(PageInstanceLeft.Instance.Name).Load();
                Reload();
                ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"更改版本图标失败（{PageInstanceLeft.Instance.Name}）");
            }
        }

        // 收藏夹
        private void BtnDisplayStar_Click(object sender, EventArgs e)
        {
            try
            {
                ModBase.WriteIni(PageInstanceLeft.Instance.PathVersion + @"PCL\Setup.ini", "IsStar", (!PageInstanceLeft.Instance.IsStar).ToString());
                PageInstanceLeft.Instance = new ModMinecraft.McInstance(PageInstanceLeft.Instance.Name).Load();
                Reload();
                ModMinecraft.McInstanceListForceRefresh = true;
                ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"版本 {PageInstanceLeft.Instance.Name} 收藏状态更改失败", LogBehavior.Alert);
            }
        }

        #endregion

        #region 卡片：快捷方式

        // 版本文件夹
        private void BtnFolderVersion_Click(object sender, MouseButtonEventArgs e)
        {
            OpenInstanceFolder(PageInstanceLeft.Instance);
        }
        public static void OpenInstanceFolder(ModMinecraft.McInstance Instance)
        {
            ModBase.OpenExplorer(Instance.PathVersion);
        }

        // 存档文件夹
        private void BtnFolderSaves_Click(object sender, MouseButtonEventArgs e)
        {
            string Folder = PageInstanceLeft.Instance.PathIndie + @"saves\";
            DirectoryUtils.Create(Folder);
            ModBase.OpenExplorer(Folder);
        }

        // Mod 文件夹
        private void BtnFolderMods_Click(object sender, MouseButtonEventArgs e)
        {
            string Folder = PageInstanceLeft.Instance.PathIndie + @"mods\";
            DirectoryUtils.Create(Folder);
            ModBase.OpenExplorer(Folder);
        }

        // 截图文件夹
        private void BtnFolderScreen_Click(object sender, MouseButtonEventArgs e)
        {
            string Folder = PageInstanceLeft.Instance.PathIndie + @"screenshots\";
            DirectoryUtils.Create(Folder);
            ModBase.OpenExplorer(Folder);
        }

        #endregion

        #region 卡片：管理

        // 导出启动脚本
        private void BtnManageScript_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 弹窗要求指定脚本的保存位置
                string SavePath = Dialogs.SaveFile("选择脚本保存位置", "启动 " + PageInstanceLeft.Instance.Name + ".bat", filter: new[] { ("bat", "批处理文件") });
                if (SavePath is null)
                    return;
                // 检查中断（等玩家选完弹窗指不定任务就结束了呢……）
                if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading)
                {
                    ModMain.Hint("请在当前启动任务结束后再试！", ModMain.HintType.Red);
                    return;
                }
                // 生成脚本
                if (ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions() { SaveBatch = SavePath, Instance = PageInstanceLeft.Instance }))
                {
                    if (Settings.Get<ModLaunch.McLoginType>("LoginType") == ModLaunch.McLoginType.Legacy)
                    {
                        ModMain.Hint("正在导出启动脚本……");
                    }
                    else
                    {
                        ModMain.Hint("正在导出启动脚本……（注意，使用脚本启动可能会导致登录失效！）");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"导出启动脚本失败（{PageInstanceLeft.Instance.Name}）", LogBehavior.Alert);
            }
        }

        // 补全文件
        private void BtnManageCheck_Click(object sender, EventArgs e)
        {
            try
            {
                if (ModMinecraft.McInstanceLock.IsLocked(PageInstanceLeft.Instance))
                {
                    ModMain.Hint("该版本正在被修改，请稍后再补全文件！", ModMain.HintType.Red);
                    return;
                }
                // 忽略文件检查提示
                if (Coerce.ToBool(ModMinecraft.ShouldIgnoreFileCheck(PageInstanceLeft.Instance)))
                {
                    ModMain.Hint("请先关闭 [版本设置 → 设置 → 高级启动选项 → 关闭文件校验]，然后再尝试补全文件！", ModMain.HintType.Blue);
                    return;
                }
                // 重复任务检查
                foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
                {
                    if ((OngoingLoader.Name ?? "") != (PageInstanceLeft.Instance.Name + " 文件补全" ?? ""))
                        continue;
                    ModMain.Hint("正在处理中，请稍候！", ModMain.HintType.Red);
                    return;
                }
                // 启动
                var Loader = new ModLoader.LoaderCombo<string>(PageInstanceLeft.Instance.Name + " 文件补全", ModDownload.DlClientFix(PageInstanceLeft.Instance, true, false));
                Loader.OnStateChanged = new Action<ModLoader.LoaderBase>(_ => { switch (Loader.State) { case ModBase.LoadState.Finished: { ModMain.Hint(Loader.Name + "成功！", ModMain.HintType.Green); break; } case ModBase.LoadState.Failed: { ModMain.Hint(Loader.Name + "失败：" + Loader.Error.GetDisplay(false), ModMain.HintType.Red); break; } case ModBase.LoadState.Canceled: { ModMain.Hint(Loader.Name + "已取消！", ModMain.HintType.Blue); break; } } });
                Loader.Start(PageInstanceLeft.Instance.Name);
                ModLoader.LoaderTaskbarAdd(Loader);
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                ModMain.FrmMain.BtnExtraDownload.Ribble();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"尝试补全文件失败（{PageInstanceLeft.Instance.Name}）", LogBehavior.Alert);
            }
        }

        // 删除版本
        // 修改此代码时，同时修改 PageSelectRight 中的代码
        private void BtnManageDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (ModMinecraft.McInstanceLock.IsLocked(PageInstanceLeft.Instance))
                {
                    ModMain.Hint("该版本正在被修改，请稍后再删除！", ModMain.HintType.Red);
                    return;
                }
                bool IsShiftPressed = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift);
                bool IsHintIndie = PageInstanceLeft.Instance.State != ModMinecraft.McInstance.McInstanceState.Error && (PageInstanceLeft.Instance.PathIndie ?? "") != (ModMinecraft.McFolderSelected ?? "");
                var SavesFolder = DirectoryUtils.GetInfo(PageInstanceLeft.Instance.PathIndie + @"saves\");
                var SaveEntries = IsHintIndie && SavesFolder.Exists ? SavesFolder.EnumerateDirectories().OrderByDescending(Save => Save.LastWriteTime).Select(Save => $"{Save.Name}（上次修改：{StringUtils.FormatTimeSpan(Save.LastWriteTime - DateTime.Now, false)}）").ToList() : new List<string>();
                switch (ModMain.MyMsgBox($"你确定要{(IsShiftPressed ? "永久" : "")}删除版本 {PageInstanceLeft.Instance.Name} 吗？" + (IsHintIndie ? ModBase.CrLf + "该版本对应的存档、资源包、Mod 等文件也将被一并删除！" + (SaveEntries.Any() ? ModBase.CrLf + ModBase.CrLf + "这会删除以下存档：" + ModBase.CrLf + "· " + SaveEntries.Join(ModBase.CrLf + "· ") : "") : ""), "版本删除确认", Button2: "取消", IsWarn: IsHintIndie || IsShiftPressed))
                {
                    case 1:
                        {
                            PageInstanceLeft.Instance.ResetSettingsCache();
                            DirectoryUtils.Delete(PageInstanceLeft.Instance.PathVersion, !IsShiftPressed);
                            ModMain.Hint("已删除版本 " + PageInstanceLeft.Instance.Name + "！", ModMain.HintType.Green);
                            break;
                        }
                    case 2:
                        {
                            return;
                        }
                }
                ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected, ModLoader.LoaderFolderRunType.ForceRun, MaxDepth: 1, ExtraPath: @"versions\");
                ModMain.FrmMain.PageBack();
            }
            catch (OperationCanceledException ex)
            {
                Logger.Warn(ex, $"删除版本 {PageInstanceLeft.Instance.Name} 被主动取消");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"删除版本 {PageInstanceLeft.Instance.Name} 失败", LogBehavior.Alert);
            }
        }

        #endregion


        #region 存档与服务器

        /// <summary>
        /// 列出最近的单人世界与游戏里保存的服务器：点击直接进入，世界可以一键备份。
        /// </summary>
        private void RefreshSaves()
        {
            this.PanSaves.Children.Clear();
            var Instance = PageInstanceLeft.Instance;
            List<SavedWorld> Worlds;
            List<SavedServer> Servers;
            try
            {
                Worlds = SavedGames.RecentWorlds(Instance.PathIndie, 5);
                Servers = SavedGames.ReadServers(Instance.PathIndie).Take(5).ToList();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "读取存档与服务器失败");
                this.CardSaves.Visibility = Visibility.Collapsed;
                return;
            }
            bool CanQuickPlay = Instance.ReleaseTime > new DateTime(2023, 4, 4);
            foreach (var World in Worlds)
            {
                var Item = new MyListItem
                {
                    Title = World.Folder,
                    Info = "单人世界，上次游玩：" + PlayTime.FormatLastPlayed(World.LastPlayed, DateTime.Now),
                    Logo = "pack://application:,,,/images/Blocks/Grass.png",
                    Type = MyListItem.CheckType.Clickable,
                    Height = 42d,
                    Tag = World.Folder
                };
                Item.Click += (sender, e) => LaunchInto(Instance, World.Folder, null);
                var BtnBackup = new MyIconButton { Logo = ModBase.Logo.IconButtonSave, LogoScale = 1.05d, ToolTip = "备份这个世界" };
                BtnBackup.Click += (sender, e) => BackupWorld(Instance, World.Folder);
                var BtnDelete = new MyIconButton { Logo = ModBase.Logo.IconButtonDelete, LogoScale = 1.1d, ToolTip = "删除这个世界" };
                BtnDelete.Click += (sender, e) => DeleteWorld(Instance, World.Folder);
                Item.Buttons = new[] { BtnBackup, BtnDelete };
                this.PanSaves.Children.Add(Item);
            }
            foreach (var Server in Servers)
            {
                var Item = new MyListItem
                {
                    Title = string.IsNullOrWhiteSpace(Server.Name) ? Server.Address : Server.Name,
                    Info = "服务器：" + Server.Address,
                    Logo = "pack://application:,,,/images/Blocks/CommandBlock.png",
                    Type = MyListItem.CheckType.Clickable,
                    Height = 42d
                };
                Item.Click += (sender, e) => LaunchInto(Instance, null, Server.Address);
                this.PanSaves.Children.Add(Item);
            }
            this.LabSavesHint.Visibility = Worlds.Any() && !CanQuickPlay ? Visibility.Visible : Visibility.Collapsed;
            this.CardSaves.Visibility = Worlds.Any() || Servers.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void LaunchInto(ModMinecraft.McInstance Instance, string WorldFolder, string ServerAddress)
        {
            ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions { Instance = Instance, WorldFolder = WorldFolder, ServerIp = ServerAddress });
        }

        /// <summary>
        /// 删除世界（取自 Sapling）：确认后移到回收站，误删时还能找回。
        /// </summary>
        private void DeleteWorld(ModMinecraft.McInstance Instance, string Folder)
        {
            if (ModMain.MyMsgBox($"确定要删除世界 {Folder} 吗？" + ModBase.CrLf + "世界会被移到回收站，需要时可以从回收站还原。", "删除世界", "删除", "取消", IsWarn: true) != 1)
                return;
            ModBase.RunInBackground(() =>
            {
                try
                {
                    string Path = Instance.PathIndie + @"saves\" + Folder;
                    DirectoryUtils.Delete(Path, toRecycleBin: true);
                    ModMain.Hint($"已删除世界 {Folder}", ModMain.HintType.Green);
                    ModBase.RunInUi(() => { if (PageInstanceLeft.Instance == Instance) RefreshSaves(); });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "删除世界失败", LogBehavior.Alert);
                }
            }, "Delete World");
        }

        private void BackupWorld(ModMinecraft.McInstance Instance, string Folder)
        {
            ModMain.Hint("正在备份世界 " + Folder + "……");
            ModBase.RunInBackground(() =>
            {
                try
                {
                    string Name = SavedGames.BackupWorld(Instance.PathIndie, Folder, DateTime.Now);
                    ModMain.Hint("已备份为 " + Name + "。要还原时，在游戏里打开这个世界即可。", ModMain.HintType.Green);
                    ModBase.RunInUi(() => { if (PageInstanceLeft.Instance == Instance) RefreshSaves(); });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "备份世界失败", LogBehavior.Alert);
                }
            }, "Backup World");
        }

        #endregion

    }
}