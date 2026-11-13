# 第三方授權與致謝

Plain Craft Launcher (PCL) 樹苗修改版基於 PCL 修改，融合了 Sapling 的功能與設計，並使用以下第三方內容。啟動器的「設定 → 關於」頁面也列出了這些內容。

## 基礎與融合的項目

| 項目 | 用在哪裡 | 授權 |
|---|---|---|
| [Plain Craft Launcher 2](https://github.com/Meloong-Git/PCL)（龍騰貓躍，© 成都瓜皮龍科技有限公司） | 本修改版的基礎，包括程式碼、介面、圖示與字型 | 《PCL 分發有限許可》與《PCL 存儲庫合理使用指南》，見 [LICENCE](LICENCE) |
| Sapling（樹苗）（PZW152，已停止維護） | 首頁主角卡、繼續玩、遊戲日誌、一鍵開始、截圖、清理、Quilt 安裝等功能與介面設計 | 與本修改版為同一作者，由作者授權用於本修改版 |
| [PCL CE](https://github.com/PCL-Community/PCL-CE)（PCL Community） | Quilt 圖示（`Images/Blocks/Quilt.png`）；部分功能參考了它的做法 | Apache-2.0，全文見 [LICENSES/Apache-2.0.txt](LICENSES/Apache-2.0.txt) |

## 隨程式分發的元件

| 元件 | 用途 | 版權 | 授權 |
|---|---|---|---|
| [MeloongCore](https://github.com/Meloong-Git/MeloongCore) | 通用工具庫 | Meloong-Git contributors | Apache-2.0 |
| [Newtonsoft.Json](https://www.newtonsoft.com/json) | JSON 讀寫 | © 2007 James Newton-King | MIT |
| [Ookii.Dialogs.Wpf](https://github.com/ookii-dialogs/ookii-dialogs-wpf) | 檔案與資料夾選擇對話框 | © 2009 Sven Groot (Ookii.org) | BSD-3-Clause |
| [ThrottleDebounce](https://github.com/Aldaviva/ThrottleDebounce) | 限制事件觸發頻率 | © Ben Hutchison | Apache-2.0 |
| [Imazen.WebP](https://github.com/imazen/libwebp-net) | 讀取 WebP 圖片 | © 2012 Imazen | MIT |
| [libwebp](https://chromium.googlesource.com/webm/libwebp) | WebP 解碼 | © Google Inc. | BSD-3-Clause |
| [Java Launch Wrapper](https://github.com/00ll00/java_launch_wrapper) | 修正 Java 18 以前的版本在中文路徑下無法啟動的問題 | © 2022 00ll00 | MIT |
| [LWJGL Unsafe Agent](https://github.com/HMCL-dev/lwjgl-unsafe-agent)（HMCL） | 修正 LWJGL 3.4.1 的一個效能問題 | © Glavo | Apache-2.0 |
| [Transformer Discovery Service](https://github.com/HMCL-dev/HMCL/tree/main/minecraft/libraries/HMCLTransformerDiscoveryService)（HMCL） | 部分版本需要的 Transformer 服務，在安裝時釋放到遊戲的支持庫中；作為獨立元件分發 | © 2020 huangyuhui and contributors | GPL-3.0-or-later，可依 GPL 取得原始碼、修改及再分發 |
| [Forge Install Bootstrapper](https://github.com/bangbang93/forge-install-bootstrapper) | 安裝 Forge | bangbang93 | 原項目沒有聲明授權；隨 PCL 原版一同分發，致謝見關於頁 |
| [OpenCC](https://github.com/BYVoid/OpenCC) | 繁體轉換表的字詞資料（`PCLCS/Lang/zh-Hant.txt`） | Carbo Kuo (BYVoid) and contributors | Apache-2.0 |
| [iconmonstr](https://iconmonstr.com/) | 部分介面圖示 | © iconmonstr (Alexander Kahlkopf) | [iconmonstr License](https://iconmonstr.com/license/) |
| [Iconfont](https://www.iconfont.cn/) | 部分介面圖示 | © ALIMAMA MUX | Iconfont 使用條款 |

## 只在編譯時使用

| 元件 | 用途 | 授權 |
|---|---|---|
| [PolySharp](https://github.com/Sergio0694/PolySharp) | 在 .NET Framework 上使用新版 C# 語法（編譯時產生的程式碼） | MIT |
| [TUnit](https://github.com/thomhurst/TUnit) | 單元測試 | MIT |
| [OpenCC Python](https://github.com/BYVoid/OpenCC) | `tools/generate-zh-hant.py` 產生繁體轉換表 | Apache-2.0 |

## 商標

Minecraft 是 Mojang Synergies AB 的商標。本修改版非 Minecraft 官方產品，未經 Mojang 或 Microsoft 批准，也不與 Mojang 或 Microsoft 關聯。

樹苗修改版自己新增的程式碼與從 Sapling 移植的功能由 PZW152 編寫。
