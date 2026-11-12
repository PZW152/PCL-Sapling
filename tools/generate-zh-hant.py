"""產生繁體介面用的轉換表：PCLCS/Lang/zh-Hant.txt。

介面原文是簡體（大陸用詞）。選繁體時，啟動器在顯示文字前先換詞、再逐字換成繁體，
用詞與 Sapling 的繁體一致（OpenCC s2twp：資料夾、網路、預設、記憶體……），港澳台都看得懂。
- 字：OpenCC 的簡繁字全收（經過 s2twp 的台灣字形），以後新增的文字也轉得到。
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
TERMS = {
    "“": "「", "”": "」", "‘": "『", "’": "』",
    "账号": "帳號", "账户": "帳戶", "帐号": "帳號",
    "反馈": "回報",
    "快捷方式": "捷徑",
    "数据包": "資料包",
    "局域网": "區域網路",
    "二维码": "QR 碼",
    "关键词": "關鍵字",
    "皮肤": "皮膚",
    "整合包": "整合包",
    # 「支持」在「支持正版」「支持 PCL」裡是表態，不能換成「支援」，維持原字。
    "支持": "支持",
    # Minecraft 社群的慣用說法
    "存档": "存檔", "光影": "光影", "材质": "材質", "资源包": "資源包",
    "启动器": "啟動器", "正版": "正版", "离线": "離線",
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
    s2twp = opencc.OpenCC("s2twp")
    t2s = opencc.OpenCC("t2s")
    used = corpus()

    characters = {}
    for key in read_keys("STCharacters.txt"):
        if len(key) == 1:
            value = s2twp.convert(key)
            if len(value) == 1 and value != key:
                characters[key] = value

    def by_characters(text):
        return "".join(characters.get(c, c) for c in text)

    candidates = {k for k in read_keys("STPhrases.txt") if len(k) > 1 and k in used}
    for key in read_keys("TWPhrases.txt"):
        simplified = t2s.convert(key)
        if len(simplified) > 1 and simplified in used:
            candidates.add(simplified)
    phrases = {}
    for key in candidates:
        value = s2twp.convert(key)
        if value != by_characters(key):
            phrases[key] = value
    phrases.update(TERMS)

    lines = [
        "# 由 tools/generate-zh-hant.py 產生，不要手動修改。",
        "# 字形與詞條資料來自 OpenCC（Apache-2.0）的 s2twp 轉換，另有人工確認的啟動器用語。",
        "# 每行一組：簡體<Tab>繁體。單字是逐字轉換，多字是優先轉換的詞。",
    ]
    lines += [f"{k}\t{v}" for k, v in sorted(characters.items())]
    lines += [f"{k}\t{v}" for k, v in sorted(phrases.items(), key=lambda kv: (-len(kv[0]), kv[0]))]
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"{OUT.relative_to(ROOT)}: {len(characters)} 字, {len(phrases)} 詞")


if __name__ == "__main__":
    main()
