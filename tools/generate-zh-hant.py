"""產生繁體介面用的轉換表：PCLCS/Lang/zh-Hant.txt。

介面原文是簡體（大陸用詞）。選繁體時，啟動器在顯示文字前先換詞、再逐字換成繁體，
用詞與 Sapling 的繁體一致（港澳台都看得懂：網絡、軟件、窗口，加上資料夾、預設、記憶體等介面用語）。
- 字：OpenCC 的簡繁字全收（經過 s2tw：裡、著、為、啟等通行字形），以後新增的文字也轉得到。
- 詞：只收 PCL 實際用到的 OpenCC 詞條，加上下面人工確認過的啟動器用語，人工詞條優先。

用法（需要 pip install opencc-python-reimplemented）：
    python tools/generate-zh-hant.py
介面文字改了很多時重新產生一次，再跑 PCL.Tests。
"""

import re
from pathlib import Path

import opencc

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "PCLCS" / "Lang" / "zh-Hant.txt"
SOURCES = [ROOT / "Plain Craft Launcher 2", ROOT / "PCLCS"]
DICTIONARY = Path(opencc.__file__).parent / "dictionary"

# 人工確認的用語：簡體原文 -> 繁體。比 OpenCC 優先，長的詞先換。
# 用詞與 Sapling 的繁體原文一致：港澳台都看得懂，避開只有台灣用的詞（寫「網絡」「軟件」「窗口」「打開」「查看」），
# 軟件介面的常用詞則用港台通用的寫法（資料夾、檔案、預設、設定、登入、帳號、介面、匯入、搜尋、建立、捷徑）。
TERMS = {
    "“": "「", "”": "」", "‘": "『", "’": "』",
    "文件夹": "資料夾", "文件": "檔案", "配置文件": "設定檔",
    "默认值": "預設值", "默认": "預設",
    "内存": "記憶體", "服务器": "伺服器", "程序": "程式",
    "登录": "登入", "退出登录": "登出", "设置": "設定",
    "账号": "帳號", "帐号": "帳號", "账户": "帳戶", "用户名": "用戶名",
    "界面": "介面", "屏幕": "螢幕", "全屏": "全螢幕", "信息": "資訊",
    "图标": "圖示", "链接": "連結", "硬链接": "硬連結",
    "导入": "匯入", "导出": "匯出", "搜索": "搜尋", "创建": "建立",
    "快捷方式": "捷徑", "分辨率": "解析度", "显卡": "顯示卡", "质量": "品質",
    "加载器": "載入器", "加载": "載入", "视频": "影片", "反馈": "回報",
    "局域网": "區域網絡", "二维码": "QR 碼", "关键词": "關鍵字",
    "数据包": "資料包", "数据": "資料", "自定义": "自訂", "高级": "進階",
    "鼠标": "滑鼠", "硬盘": "硬碟", "在线": "線上", "禁用": "停用",
    "实时": "即時", "通过": "透過", "预览": "預覽", "刷新": "重新整理",
    "点击": "按一下", "双击": "按兩下",
    # 「支持」在「支持正版」「支持 PCL」裡是表態，不能換成「支援」，維持原字。
    "支持": "支持",
    # Minecraft 社群的慣用說法
    "存档": "存檔", "游玩": "遊玩", "整合包": "整合包", "光影": "光影", "材质": "材質", "资源包": "資源包",
    "启动器": "啟動器", "正版": "正版", "离线": "離線", "皮肤": "皮膚",
}

CJK = re.compile(r"[㐀-鿿豈-﫿“”‘’]+")


def read_keys(name):
    keys = []
    for line in (DICTIONARY / name).read_text(encoding="utf-8").splitlines():
        if line and not line.startswith("#"):
            keys.append(line.split("\t", 1)[0])
    return keys


def corpus():
    runs = set()
    for base in SOURCES:
        for path in base.rglob("*"):
            if path.suffix in {".cs", ".xaml"} and "obj" not in path.parts and "bin" not in path.parts:
                runs.update(CJK.findall(path.read_text(encoding="utf-8", errors="ignore")))
    return "\n".join(sorted(runs))


def main():
    s2t = opencc.OpenCC("s2tw")  # 只換台灣通行的字形（裡、著、為、啟），不換台灣用詞
    used = corpus()

    characters = {}
    for key in read_keys("STCharacters.txt"):
        if len(key) == 1:
            value = s2t.convert(key)
            if len(value) == 1 and value != key:
                characters[key] = value

    def by_characters(text):
        return "".join(characters.get(c, c) for c in text)

    candidates = {k for k in read_keys("STPhrases.txt") if len(k) > 1 and k in used}
    phrases = {}
    for key in candidates:
        value = s2t.convert(key)
        if value != by_characters(key):
            phrases[key] = value
    phrases.update(TERMS)

    lines = [
        "# 由 tools/generate-zh-hant.py 產生，不要手動修改。",
        "# 字形與詞條資料來自 OpenCC（Apache-2.0）的 s2tw 轉換（只換字形），另有與 Sapling 一致的人工用語。",
        "# 每行一組：簡體<Tab>繁體。單字是逐字轉換，多字是優先轉換的詞。",
    ]
    lines += [f"{k}\t{v}" for k, v in sorted(characters.items())]
    lines += [f"{k}\t{v}" for k, v in sorted(phrases.items(), key=lambda kv: (-len(kv[0]), kv[0]))]
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"{OUT.relative_to(ROOT)}: {len(characters)} 字, {len(phrases)} 詞")


if __name__ == "__main__":
    main()
