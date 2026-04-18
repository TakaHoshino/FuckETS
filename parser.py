import json
from pathlib import Path

from utils import strip_html_tags, format_text_with_paragraphs


def parse_part_a(json_path: Path, output_lines: list):
    try:
        with open(json_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        raw_value = data['info']['value']
        clean_text = format_text_with_paragraphs(raw_value)
        output_lines.append(clean_text)
    except Exception as e:
        output_lines.append(f"[PartA 解析错误] {e}")


def parse_part_b(json_path: Path, output_lines: list):
    try:
        with open(json_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        questions = data.get('info', {}).get('question', [])
        if not questions:
            output_lines.append("未找到问题列表（info.question）")
            return
        for idx, qa in enumerate(questions, start=1):
            ask = strip_html_tags(qa.get('ask', ''))
            std_list = qa.get('std', [])
            answers = [strip_html_tags(item.get('value', '')) for item in std_list[:3]]

            output_lines.append(f"\n【问题 {idx}】 {ask}")
            output_lines.append("  候选答案：")
            for i, ans in enumerate(answers, start=1):
                lines = ans.splitlines()
                if not lines:
                    output_lines.append(f"    {i}. (空)")
                else:
                    output_lines.append(f"    {i}. {lines[0]}")
                    for line in lines[1:]:
                        output_lines.append(f"       {line}")
            output_lines.append("")
    except Exception as e:
        output_lines.append(f"[PartB 解析错误] {e}")


def parse_part_c(json_path: Path, output_lines: list):
    try:
        with open(json_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        raw_value = data['info']['value']
        clean_text = format_text_with_paragraphs(raw_value)
        output_lines.append(clean_text)
    except Exception as e:
        output_lines.append(f"[PartC 解析错误] {e}")