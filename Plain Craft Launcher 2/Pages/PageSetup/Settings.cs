using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MeloongCore;
using MeloongCore.Extensions;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{

    public class Settings
    {

        public static readonly Dictionary<string, Setting> Entries = new List<Setting>() { new Setting("Identify", "", Source: Sources.Registry), new Setting("WindowHeight", 550), new Setting("WindowWidth", 900), new Setting("AprilYear", 0, Source: Sources.Registry, Encrypted: true), new Setting("HintDownloadThread", false, Source: Sources.Registry), new Setting("HintNotice", 0, Source: Sources.Registry), new Setting("HintDownload", 0, Source: Sources.Registry), new Setting("HintHide", false, Source: Sources.Registry), new Setting("HintHandInstall", false, Source: Sources.Registry), new Setting("HintBuy", false, Source: Sources.Registry), new Setting("HintClearRubbish", 0, Source: Sources.Registry), new Setting("HintUpdateMod", false, Source: Sources.Registry), new Setting("HintCustomCommand", false, Source: Sources.Registry), new Setting("HintCustomWarn", false, Source: Sources.Registry), new Setting("HintMoreAdvancedSetup", false, Source: Sources.Registry), new Setting("HintIndieSetup", false, Source: Sources.Registry), new Setting("HintExportConfig", false, Source: Sources.Registry), new Setting("HintSnapshot", false, Source: Sources.Registry), new Setting("SystemEulaVersion", 0, Source: Sources.Registry), new Setting("SystemCount", 0, Source: Sources.Registry, Encrypted: true), new Setting("SystemLaunchCount", 0, Source: Sources.Registry, Encrypted: true), new Setting("SystemLastVersionReg", 0, Source: Sources.Registry, Encrypted: true), new Setting("SystemHighestSavedBetaVersionReg", 0, Source: Sources.Registry, Encrypted: true), new Setting("SystemHighestBetaVersionReg", 0, Source: Sources.Registry, Encrypted: true), new Setting("SystemHighestAlphaVersionReg", 0, Source: Sources.Registry, Encrypted: true), new Setting("SystemHelpVersion", 0, Source: Sources.Registry), new Setting("SystemDebugMode", false, Source: Sources.Registry, OnChanged: _ => Logger.Instance.MinLevel = (LogLevel)Conversions.ToInteger(ModBase.ModeDebug ? LogLevel.Trace : LogLevel.Info)), new Setting("SystemDebugAnim", 9, Source: Sources.Registry), new Setting("SystemDebugDelay", false, Source: Sources.Registry), new Setting("SystemDebugSkipCopy", false, Source: Sources.Registry), new Setting("SystemSystemCache", "", Source: Sources.Registry), new Setting("SystemSystemUpdate", 0), new Setting("SystemSystemActivity", 0), new Setting("CacheDrops", "", Source: Sources.Registry), new Setting("CacheConfig", 0, Source: Sources.Registry), new Setting("CacheExportConfig", "", Source: Sources.Registry), new Setting("CacheSavedPageUrl", "", Source: Sources.Registry), new Setting("CacheSavedPageVersion", "", Source: Sources.Registry), new Setting("CacheMsOAuthRefresh", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsAccess", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsProfileJson", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsUuid", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsName", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsV2Migrated", false, Source: Sources.Registry), new Setting("CacheMsV2OAuthRefresh", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsV2Access", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsV2ProfileJson", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsV2Uuid", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsV2Name", "", Source: Sources.Registry, Encrypted: true), new Setting("CacheMsV2Expires", 0L, Source: Sources.Registry, Encrypted: true), new Setting("CacheDownloadFolder", "", Source: Sources.Registry), new Setting("CacheJavaListVersion", 0, Source: Sources.Registry), new Setting("LoginRemember", true, Source: Sources.Registry, Encrypted: true), new Setting("LoginLegacyName", "", Source: Sources.Registry, Encrypted: true), new Setting("LoginMsJson", "{}", Source: Sources.Registry, Encrypted: true), new Setting("LoginType", ModLaunch.McLoginType.Legacy, Source: Sources.Registry), new Setting("LoginPageType", 0), new Setting("LaunchSkinID", "", Source: Sources.Registry, OnChanged: _ => PageLaunchLeft.SkinLegacy.Start()), new Setting("LaunchSkinType", 0, Source: Sources.Registry, OnChanged: (_) => PageSetupLaunch.UpdateSkinType()), new Setting("LaunchSkinSlim", false, Source: Sources.Registry), new Setting("LaunchFolderSelect", ""), new Setting("LaunchFolders", "", Source: Sources.Registry), new Setting("LaunchArgumentTitle", ""), new Setting("LaunchArgumentInfo", "PCL"), new Setting("LaunchArgumentJavaSelect", "", Source: Sources.Registry), new Setting("LaunchArgumentJavaAll", "[]", Source: Sources.Registry), new Setting("LaunchArgumentIndie", 0), new Setting("LaunchArgumentIndieV2", 4), new Setting("LaunchArgumentVisible", 5, Source: Sources.Registry), new Setting("LaunchArgumentPriority", 1, Source: Sources.Registry), new Setting("LaunchArgumentWindowWidth", 854), new Setting("LaunchArgumentWindowHeight", 480), new Setting("LaunchArgumentWindowType", 1), new Setting("LaunchArgumentRam", false, Source: Sources.Registry), new Setting("LaunchAdvanceJvm", "-XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=True -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True"), new Setting("LaunchAdvanceGame", ""), new Setting("LaunchAdvanceRun", ""), new Setting("LaunchAdvanceRunWait", true), new Setting("LaunchAdvanceDisableJLW", false), new Setting("LaunchAdvanceDisableLUA", false), new Setting("LaunchAdvanceGraphicCard", true, Source: Sources.Registry), new Setting("LaunchAdvanceGC", 4), new Setting("LaunchRamType", 0, OnChanged: (_) => PageSetupLaunch.UpdateRamType()), new Setting("LaunchRamCustom", 15), new Setting("ToolHelpChinese", true, Source: Sources.Registry), new Setting("ToolDownloadThread", 63, Source: Sources.Registry), new Setting("ToolDownloadSpeed", 42, Source: Sources.Registry, OnChanged: (_) => ModNet.UpdateNetTaskSpeedLimitHigh(Conversions.ToInteger(_))), new Setting("ToolDownloadSource", 1, Source: Sources.Registry), new Setting("ToolDownloadVersion", 1, Source: Sources.Registry), new Setting("ToolDownloadTranslate", 0, Source: Sources.Registry), new Setting("ToolDownloadTranslateV2", 1, Source: Sources.Registry), new Setting("ToolDownloadCert", false, Source: Sources.Registry, OnChanged: (_) => ModNet.ShouldValidateSslCertificateOnLogin(Conversions.ToBoolean(_))), new Setting("ToolDownloadMod", 2, Source: Sources.Registry), new Setting("ToolModLocalNameStyle", 0, Source: Sources.Registry), new Setting("ToolUpdateRelease", false, Source: Sources.Registry), new Setting("ToolUpdateSnapshot", false, Source: Sources.Registry), new Setting("ToolUpdateReleaseLast", "", Source: Sources.Registry), new Setting("ToolUpdateSnapshotLast", "", Source: Sources.Registry), new Setting("UiLauncherTransparent", 600, OnChanged: Value => { if (ModMain.FrmMain is not null) ModMain.FrmMain.Opacity = Conversions.ToInteger(Value) / 1000d + 0.4d; }), new Setting("UiLauncherHue", 180), new Setting("UiLauncherSat", 80), new Setting("UiLauncherDelta", 90), new Setting("UiLauncherLight", 20), new Setting("UiLauncherTheme", 0, OnChanged: (_) => ModSecret.ThemeRefresh()), new Setting("UiLauncherLogo", true), new Setting("UiLauncherEmail", false), new Setting("UiBackgroundColorful", true, OnChanged: _ => ModSecret.ThemeRefresh()), new Setting("UiBackgroundOpacity", 1000, OnChanged: FormMain.UpdateBackgroundAndTitleBar), new Setting("UiBackgroundBlur", 0, OnChanged: FormMain.UpdateBackgroundAndTitleBar), new Setting("UiBackgroundSuit", 0, OnChanged: FormMain.UpdateBackgroundAndTitleBar), new Setting("UiCustomType", 0, OnChanged: (_) => PageSetupUI.OnMainPageTypeChanged()), new Setting("UiCustomPreset", 0), new Setting("UiCustomNet", ""), new Setting("UiLogoType", 1, OnChanged: FormMain.UpdateBackgroundAndTitleBar), new Setting("UiLogoText", "", OnChanged: FormMain.UpdateBackgroundAndTitleBar), new Setting("UiLogoLeft", false, OnChanged: FormMain.UpdateBackgroundAndTitleBar), new Setting("UiHiddenPageDownload", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenPageSetup", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenPageOther", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenFunctionSelect", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenFunctionModUpdate", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenFunctionInstanceModify", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenFunctionHidden", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenSetupLaunch", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenSetupUi", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenSetupSystem", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenOtherHelp", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenOtherFeedback", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenOtherVote", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenOtherAbout", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("UiHiddenOtherTest", false, OnChanged: (_) => PageSetupUI.HiddenRefresh()), new Setting("VersionAdvanceJvm", "", Source: Sources.Instance), new Setting("VersionAdvanceGame", "", Source: Sources.Instance), new Setting("VersionAdvanceAssets", 0, Source: Sources.Instance), new Setting("VersionAdvanceAssetsV2", false, Source: Sources.Instance), new Setting("VersionAdvanceRun", "", Source: Sources.Instance), new Setting("VersionAdvanceRunWait", true, Source: Sources.Instance), new Setting("VersionAdvanceDisableJLW", false, Source: Sources.Instance), new Setting("VersionAdvanceDisableLUA", false, Source: Sources.Instance), new Setting("VersionAdvanceDisableModUpdate", false, Source: Sources.Instance), new Setting("VersionAdvanceGC", 0, Source: Sources.Instance), new Setting("VersionRamType", 2, Source: Sources.Instance, OnChanged: (_) => PageInstanceSetup.OnVersionRamTypeChanged(Conversions.ToInteger(_))), new Setting("VersionRamCustom", 15, Source: Sources.Instance), new Setting("VersionRamOptimize", 0, Source: Sources.Instance), new Setting("VersionArgumentTitle", "", Source: Sources.Instance), new Setting("VersionArgumentInfo", "", Source: Sources.Instance), new Setting("VersionArgumentIndie", -1, Source: Sources.Instance), new Setting("VersionArgumentIndieV2", false, Source: Sources.Instance), new Setting("VersionArgumentJavaSelect", "使用全局设置", Source: Sources.Instance), new Setting("VersionArgumentJavaV2", 0, Source: Sources.Instance), new Setting("VersionArgumentJavaRange", "", Source: Sources.Instance), new Setting("VersionServerEnter", "", Source: Sources.Instance), new Setting("VersionServerLogin", 0, Source: Sources.Instance, OnChanged: (_) => PageInstanceSetup.OnVersionServerLoginChanged(Conversions.ToInteger(_))) }.ToDictionary(e => e.Key); // {UserName: OAuthToken, ...}
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          // 避免与 PCL1 设置冲突（UiLauncherOpacity）
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          // 不传入参数

        public enum Sources
        {
            Normal,
            Registry,
            Instance
        }
        public class Setting
        {
            public string Key;
            public bool Encrypted;
            public object DefaultValue;
            public Sources Source;
            /// <summary>
        /// 当设置项的值实际被改变时触发，参数为新值。
        /// </summary>
            public Action<object> OnChanged;
            /// <summary>
        /// 该设置的值的实际类型。
        /// </summary>
            public Type Type;
            public Setting(string Key, object Value, Sources Source = Sources.Normal, bool Encrypted = false, Action<object> OnChanged = null)
            {
                try
                {
                    this.Key = Key;
                    DefaultValue = Value;
                    this.Encrypted = Encrypted;
                    this.Source = Source;
                    Type = (Value ?? "").GetType();
                    this.OnChanged = OnChanged;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "初始化设置项失败"); // #5095 的 fallback
                }
            }

            /// <summary>
        /// 读取过的设置的缓存，若从未读取过则为 Nothing。
        /// 若为版本独立设置，键为版本路径；否则，键为空字符串。
        /// </summary>
            public ConcurrentDictionary<string, object> ValueCache = new ConcurrentDictionary<string, object>();
            public object GetCache(ModMinecraft.McInstance Instance)
            {
                string Key = Source == Sources.Instance ? Instance.PathVersion : "";
                object Result = null;
                return ValueCache.TryGetValue(Key, out Result) ? Result : null;
            }
            public void Set(object Value, ModMinecraft.McInstance Instance)
            {
                string Key = Source == Sources.Instance ? Instance.PathVersion : "";
                ValueCache[Key] = Value;
            }

            /// <summary>
        /// 立即将当前的 Value 写入对应的注册表或文件。
        /// </summary>
            public void Save(ModMinecraft.McInstance Instance = null)
            {
                string Value = Conversions.ToString(GetCache(Instance));
                Logger.Trace($"保存设置：{Key} {(Encrypted ? "" : "= " + Value)}{(Instance is null ? "" : $"（实例：{Instance?.PathVersion}）")}");
                if (Encrypted)
                {
                    try
                    {
                        if (Value is null)
                            Value = "";
                        Value = SecretProtector.Protect(Value);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"加密设置失败：{Key}");
                    }
                }
                switch (Source)
                {
                    case Sources.Normal:
                        {
                            ModBase.WriteIni("Setup", Key, Value);
                            break;
                        }
                    case Sources.Registry:
                        {
                            try
                            {
                                RegistryUtils.Write($@"HKEY_CURRENT_USER\Software\{ModSecret.RegFolder}", Key, Value);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn(ex, $"写入注册表失败（{Key} → {Value}）", LogBehavior.Toast);
                            }

                            break;
                        }
                    case Sources.Instance:
                        {
                            if (Instance is null)
                                throw new Exception($"保存版本独立设置 {Key} 时未提供目标版本");
                            ModBase.WriteIni(Instance.PathVersion + @"PCL\Setup.ini", Key, Value);
                            break;
                        }
                }
            }

        }

        /// <summary>
    /// 改变某个设置项的值。
    /// </summary>
        public static void Set(string Key, object Value, ModMinecraft.McInstance Instance = null)
        {
            Setting Entry = null;
            if (!Entries.TryGetValue(Key, out Entry))
                throw new KeyNotFoundException("未找到设置项：" + Key);
            try
            {
                // 判断是否一致
                Value = Conversion.CTypeDynamic(Value, Entry.Type);
                if (Entry.GetCache(Instance) is null)
                    Get(Key, Instance);
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(Entry.GetCache(Instance), Value, false)))
                    return;
                // 设置新值
                Entry.Set(Value, Instance);
                Entry.Save(Instance);
                // 触发改变事件（必须在保存之后，以保证 VersionServerLogin 之类的在事件中读取到的是最新的值）
                if (Entry.OnChanged is not null)
                    Entry.OnChanged.Invoke(Value);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"设置设置项时出错（{Key}, {Value}）");
            }
        }
        /// <summary>
    /// 写入某个未经加密的设置项。
    /// 若该设置项经过了加密，则会抛出异常。
    /// </summary>
        public static void SetSafe(string Key, object Value, ModMinecraft.McInstance Instance = null)
        {
            Setting Entry = null;
            if (!Entries.TryGetValue(Key, out Entry))
                throw new KeyNotFoundException("未找到设置项：" + Key);
            if (Entry.Encrypted)
                throw new InvalidOperationException("禁止写入加密设置项：" + Key);
            Set(Key, Value, Instance);
        }
        // 对部分设置强制赋值
        private static Dictionary<string, object> _Get_ForcedSettings = new Dictionary<string, object>();

        /// <summary>
    /// 获取某个设置项的值。
    /// </summary>
        public static object Get(string Key, ModMinecraft.McInstance Instance = null)
        {
            Setting Entry = null;
            if (!Entries.TryGetValue(Key, out Entry))
                throw new KeyNotFoundException("未找到设置项：" + Key);
            var Value = Entry.GetCache(Instance);
            if (Value is not null)
                return Value;
            object ForcedSetting = null;
            if (_Get_ForcedSettings.TryGetValue(Key, out ForcedSetting))
            {
                Entry.Set(ForcedSetting, Instance);
                return ForcedSetting;
            }
            // 正常读取
            try
            {
                string GotValue = null; // 先用 String 储存，避免类型转换
                // 加密項：用一個不可能是密文的標記作為讀取默認值，以判斷“未設置過”
                string DefaultValue = Conversions.ToString(Entry.Encrypted ? EncryptedUnsetMarker : Entry.DefaultValue);
                switch (Entry.Source)
                {
                    case Sources.Normal:
                        {
                            GotValue = ModBase.ReadIni("Setup", Key, DefaultValue);
                            break;
                        }
                    case Sources.Registry:
                        {
                            GotValue = Conversions.ToString(RegistryUtils.TryRead($@"HKEY_CURRENT_USER\Software\{ModSecret.RegFolder}", Key, DefaultValue));
                            break;
                        }
                    case Sources.Instance:
                        {
                            if (Instance is null)
                            {
                                throw new Exception($"读取版本设置 {Key} 时未提供目标版本");
                            }
                            else
                            {
                                GotValue = ModBase.ReadIni(Instance.PathVersion + @"PCL\Setup.ini", Key, DefaultValue);
                            }

                            break;
                        }
                }
                if (Entry.Encrypted)
                {
                    if ((GotValue ?? "") == (DefaultValue ?? ""))
                    {
                        GotValue = Conversions.ToString(Entry.DefaultValue);
                    }
                    else
                    {
                        try
                        {
                            GotValue = SecretProtector.Unprotect(GotValue, s => DesDecrypt(s, "PCL" + ModBase.Identify), out bool IsLegacy);
                            if (IsLegacy)
                            {
                                // 舊版 DES 密文：解密成功後立即以 DPAPI 重新保存
                                Entry.Set(GotValue, Instance);
                                Entry.Save(Instance);
                                Logger.Info($"已將加密設置遷移到 DPAPI：{Key}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, $"解密设置失败：{Key}");
                            GotValue = Conversions.ToString(Entry.DefaultValue);
                            Entry.Set(Entry.DefaultValue, Instance);
                            Entry.Save(Instance);
                        }
                    }
                }
                Entry.Set(Conversion.CTypeDynamic(GotValue, Entry.Type), Instance);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"读取设置失败：{Key}", LogBehavior.Toast);
                Entry.Set(Conversion.CTypeDynamic(Entry.DefaultValue, Entry.Type), Instance);
            }
            return Entry.GetCache(Instance);
        }
        public static T Get<T>(string Key, ModMinecraft.McInstance Instance = null)
        {
            return Conversions.ToGenericParameter<T>(Get(Key, Instance));
        }
        /// <summary>
    /// 获取某个未经加密的设置项的值。
    /// 若该设置项经过了加密，则会抛出异常。
    /// </summary>
        public static object GetSafe(string Key, ModMinecraft.McInstance Instance = null)
        {
            Setting Entry = null;
            if (!Entries.TryGetValue(Key, out Entry))
                throw new KeyNotFoundException("未找到设置项：" + Key);
            if (Entry.Encrypted)
                throw new InvalidOperationException("禁止读取加密设置项：" + Key);
            return Get(Key, Instance);
        }

        /// <summary>
    /// 初始化某个设置项的值。
    /// </summary>
        public static void Reset(string Key, ModMinecraft.McInstance Instance = null)
        {
            Setting Entry = null;
            if (!Entries.TryGetValue(Key, out Entry))
                throw new KeyNotFoundException("未找到设置项：" + Key);
            try
            {
                // 判断是否一致
                if (Entry.GetCache(Instance) is null)
                    Get(Key, Instance);
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(Entry.GetCache(Instance), Entry.DefaultValue, false)))
                    return;
                // 设置新值
                Entry.Set(Entry.DefaultValue, Instance);
                switch (Entry.Source)
                {
                    case Sources.Normal:
                        {
                            ModBase.DeleteIniKey("Setup", Key);
                            break;
                        }
                    case Sources.Registry:
                        {
                            try
                            {
                                RegistryUtils.Delete($@"HKEY_CURRENT_USER\Software\{ModSecret.RegFolder}", Key);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn(ex, $"删除注册表失败（{Key}）", LogBehavior.Toast);
                            }

                            break;
                        }
                    case Sources.Instance:
                        {
                            if (Instance is null)
                                throw new Exception($"重置版本设置 {Key} 时未提供目标版本");
                            ModBase.DeleteIniKey(Instance.PathVersion + @"PCL\Setup.ini", Key);
                            break;
                        }
                }
                // 触发改变事件
                if (Entry.OnChanged is not null)
                    Entry.OnChanged.Invoke(Entry.DefaultValue);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"重置设置项时出错（{Key}）");
            }
        }

        /// <summary>
    /// 获取某个设置项的默认值。
    /// </summary>
        public static string GetDefault(string Key)
        {
            return Conversions.ToString(Entries[Key].DefaultValue);
        }

        /// <summary>
    /// 对应的注册表或文件是否已经储存了某个设置项。
    /// 若改成了和默认值一样的，在 2.12.3- 会储存更改，在 2.12.4+ 不会储存更改。
    /// </summary>
        public static bool HasSaved(string Key, ModMinecraft.McInstance Instance = null)
        {
            switch (Entries[Key].Source)
            {
                case Sources.Normal:
                    {
                        return ModBase.HasIniKey("Setup", Key);
                    }
                case Sources.Registry:
                    {
                        try
                        {
                            return RegistryUtils.Has($@"HKEY_CURRENT_USER\Software\{ModSecret.RegFolder}", Key);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, $"判断注册表 {Key} 是否存在时出错");
                            return false;
                        } // Source.Instance

                        break;
                    }

                default:
                    {
                        if (Instance is null)
                            throw new Exception($"判断版本设置 {Key} 是否存在时未提供目标版本");
                        return ModBase.HasIniKey(Instance.PathVersion + @"PCL\Setup.ini", Key);
                    }
            }
        }

        #region DES 加密

        private const string EncryptedUnsetMarker = "\u0001unset"; // 加密項未設置時的讀取默認值
        private static readonly byte[] desInitialVector = Encoding.UTF8.GetBytes("95168702");

        /// <summary>
    /// 使用 DES 对称加密算法加密字符串。
    /// </summary>
        private static string DesEncrypt(string sourceString, string key = null)
        {
            key = key is null ? "@;$ Abv2" : key.GetStableHashCode().ToString().EnsureLength('X', 8).Substring(0, 8);
            byte[] btKey = Encoding.UTF8.GetBytes(key);
            using (var des = new DESCryptoServiceProvider())
            {
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, des.CreateEncryptor(btKey, desInitialVector), CryptoStreamMode.Write))
                    {
                        byte[] inData = Encoding.UTF8.GetBytes(sourceString);
                        cs.Write(inData, 0, inData.Length);
                        cs.FlushFinalBlock();
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }

        /// <summary>
    /// 使用 DES 对称加密算法解密字符串。
    /// </summary>
        private static string DesDecrypt(string encryptedString, string key = null)
        {
            key = key is null ? "@;$ Abv2" : key.GetStableHashCode().ToString().EnsureLength('X', 8).Substring(0, 8);
            byte[] btKey = Encoding.UTF8.GetBytes(key);
            using (var des = new DESCryptoServiceProvider())
            {
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, des.CreateDecryptor(btKey, desInitialVector), CryptoStreamMode.Write))
                    {
                        byte[] inData = Convert.FromBase64String(encryptedString);
                        cs.Write(inData, 0, inData.Length);
                        cs.FlushFinalBlock();
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
        }

        #endregion

    }
}