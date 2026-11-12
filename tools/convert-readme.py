"""
把 README.md（简体）转换为 README.zh-Hant.md（繁體）。
用词与启动器的繁体界面一致：先套用 generate-zh-hant.py 的词表，再用 OpenCC s2tw 转换字形。
用法：python tools/convert-readme.py
"""
import re
import sys
from pathlib import Path

import opencc

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))
TERMS = __import__("generate-zh-hant").TERMS  # 与界面共用同一份词表

# README 里不适合套用的词：引号换成「」会破坏 Markdown 链接文字，原样保留
SKIP = {"“", "”", "‘", "’"}


def convert(text: str) -> str:
    converter = opencc.OpenCC("s2tw")
    keys = sorted((k for k in TERMS if k not in SKIP and TERMS[k] != k), key=len, reverse=True)
    pattern = re.compile("|".join(re.escape(k) for k in keys))
    out = []
    # 只转换 CJK 文字，网址与代码保持原样
    for part in re.split(r"(`[^`]*`|```[\s\S]*?```|https?://\S+?(?=[)\s\"]))", text):
        if part.startswith("`") or part.startswith("http"):
            out.append(part)
            continue
        # 词表里的词先换成占位，转换字形后再放回，避免被 OpenCC 改掉
        pos = 0
        for m in pattern.finditer(part):
            out.append(converter.convert(part[pos:m.start()]))
            out.append(TERMS[m.group(0)])
            pos = m.end()
        out.append(converter.convert(part[pos:]))
    return "".join(out)


def main():
    text = (ROOT / "README.md").read_text(encoding="utf-8")
    result = convert(text)
    result = result.replace('簡體中文 · <a href="README.zh-Hant.md">繁體中文</a>', '<a href="README.md">简体中文</a> · 繁體中文')
    result = result.replace("docs/screenshot-home.png", "docs/screenshot-home-hant.png")
    result = result.replace("掃描QR 碼", "掃描 QR 碼").replace("登入支持", "登入支援")
    (ROOT / "README.zh-Hant.md").write_text(result, encoding="utf-8")
    print("已產生 README.zh-Hant.md")


if __name__ == "__main__":
    main()
