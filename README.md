<p align="center"><img src="logo.png" width="96" alt="PCL 樹苗修改版"></p>

<h1 align="center">Plain Craft Launcher (PCL) 樹苗修改版</h1>

<p align="center">由 PZW152 基于 <a href="https://github.com/Meloong-Git/PCL">Plain Craft Launcher 2</a> 独立进行二次创作的第三方修改版，并非 PCL 官方版本。</p>

---

PCL 是龙腾猫跃开发的 Minecraft Java 版启动器。樹苗修改版保留 PCL2 的界面语言与使用方式——单个 exe、放进文件夹就能用，不需要安装——并融合了作者另一个启动器 [Sapling（樹苗）](https://github.com/PZW152/sapling) 的功能与想法。

如果你喜欢 PCL，请[赞助 PCL 的原作者龙腾猫跃](https://meloong.com/afd/a/LTCat)。

## 与 PCL 原版的区别

**更快、更稳**

- 整个程序从 VB.NET 改写为 C#，启动流程、下载与网络请求改为异步执行，可以随时取消
- 正常启动时只做快速的文件检查；游戏崩溃后的下一次启动才完整校验
- 网络不可用时，版本列表等内容沿用上一次成功获取的结果，已安装的版本照常启动
- 相同的 Mod 文件在实例之间以硬链接共享，不重复占用空间
- 只使用 Mojang、Forge、Fabric、NeoForge、Modrinth、CurseForge 的官方下载源

**更好用**

- 微软登录支持扫描二维码
- 实例概览显示游玩时间，可以一键进入最近玩过的存档或服务器，并备份存档
- 从 Prism Launcher、MultiMC 导入实例
- 新游戏的默认设置：语言、视距、音量等
- 整合包安装前先列出内容，来自 Modrinth 与 GitHub 以外的下载会特别提示
- 游戏退出后，从游戏日志中抹去登录令牌，分享日志更安全
- 导出 / 导入启动器设置
- 崩溃分析修正了多处取不到 Mod 名称的问题

**外观**

- 五种蓝色主题（PCL 的使用指南要求界面主色调为蓝色），以及可在蓝色范围内调整的自定义主题
- 新的图标：PCL 蓝底上的樹苗

## 下载

到 [Releases](https://github.com/PZW152/PCL-Sapling/releases) 下载最新版本，放进一个文件夹后直接打开即可。

需要 Windows 10 或 Windows 11（系统自带的 .NET Framework 4.8）。

## 从源代码构建

```bash
git clone --recursive https://github.com/PZW152/PCL-Sapling.git
cd PCL-Sapling
dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" -c Release
dotnet run --project PCL.Tests/PCL.Tests.csproj -c Debug
```

微软登录与 CurseForge 需要你自己的密钥：在仓库根目录创建 `BuildSecrets.local.props`（已被 git 忽略），或设置环境变量 `PCL_MS_CLIENT_ID`、`PCL_CURSEFORGE_API_KEY`。

## 许可与署名

- 本项目基于 PCL 修改，遵循 PCL 的《PCL 分发有限许可》与《PCL 存储库合理使用指南》，见 [LICENCE](LICENCE)。
- PCL 由龙腾猫跃开发，版权归成都瓜皮龙科技有限公司所有。启动器的「关于」页面在首位给出龙腾猫跃的署名与赞助链接。
- 本修改版与 PCL 官方无关。遇到问题请在本仓库反馈，不要打扰 PCL 原作者。
