"""
utils.py — 通用工具函数
"""
import os
import re
from pathlib import Path
from datetime import datetime


def strip_html_tags(text: str) -> str:
    if not isinstance(text, str):
        return text
    clean = re.compile(r'<[^>]+>')
    return re.sub(clean, '', text)


def split_html_paragraphs(html_text: str) -> list:
    """将 HTML 按 <p> 标签拆分为段落列表"""
    if not html_text:
        return []
    paragraphs = re.findall(r'<p>(.*?)</p>', html_text, re.DOTALL)
    if not paragraphs:
        return [html_text]
    clean = re.compile(r'<[^>]+>')
    return [re.sub(clean, '', p).strip() for p in paragraphs if p.strip()]


def format_text_with_paragraphs(text: str) -> str:
    """段落间插入两个换行，提升可读性"""
    paras = split_html_paragraphs(text)
    if not paras:
        return text
    return "\n\n".join(paras)


def get_creation_time(path: Path) -> str:
    try:
        ctime = os.path.getctime(path)
        return datetime.fromtimestamp(ctime).strftime('%Y-%m-%d %H:%M:%S')
    except Exception:
        return "无法获取时间"


def find_chinese_font() -> str | None:
    possible_paths = [
        "C:/Windows/Fonts/simsun.ttc",
        "C:/Windows/Fonts/simhei.ttf",
        "C:/Windows/Fonts/msyh.ttc",
        "/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf",
        "/System/Library/Fonts/PingFang.ttc"
    ]
    for path in possible_paths:
        if os.path.exists(path):
            return path
    return None


# ------------------------------------------------------------------
# 行分类（app.py 与 pdf_generator.py 共用）
# ------------------------------------------------------------------
RE_PART_HEADING = re.compile(r'^[\【\[]\s*Part[A-C]\s*[\】\]]|^Part[A-C]\s*[：:]')
RE_QUESTION     = re.compile(r'^[\【\[]\s*问题\s*\d+\s*[\】\]]|^问题\s*\d+\s*[：:]')
RE_ANSWER_START = re.compile(r'^\s*\d+\.')


def classify_line(line: str) -> str:
    """将一行文本归类为样式标签。
    返回: 'title' | 'part_heading' | 'question' | 'answer_candidate' | 'info' | 'normal'
    """
    stripped = line.strip()
    if not stripped:
        return 'normal'

    if line.startswith("作业文件夹："):
        return 'title'
    if RE_PART_HEADING.match(stripped):
        return 'part_heading'
    if RE_QUESTION.match(stripped):
        return 'question'
    if stripped.startswith("候选答案：") or RE_ANSWER_START.match(stripped):
        return 'answer_candidate'
    if stripped.startswith("[完成]") or stripped.startswith("警告：") or stripped.startswith("错误："):
        return 'info'
    return 'normal'
