"""
pdf_generator.py — 将解析结果输出为 PDF 文件
"""
import re

try:
    from reportlab.lib import colors
    from reportlab.lib.enums import TA_CENTER
    from reportlab.lib.pagesizes import A4
    from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
    from reportlab.lib.units import mm
    from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    REPORTLAB_AVAILABLE = True
except ImportError:
    REPORTLAB_AVAILABLE = False

from utils import find_chinese_font


def generate_pdf(output_text: str, filename: str) -> bool:
    """将文本内容渲染为带样式的 PDF，返回是否成功。"""
    if not REPORTLAB_AVAILABLE:
        return False

    font_path = find_chinese_font()
    if font_path:
        try:
            pdfmetrics.registerFont(TTFont('ChineseFont', font_path))
            font_name = 'ChineseFont'
        except Exception:
            font_name = "Helvetica"
    else:
        font_name = "Helvetica"

    doc = SimpleDocTemplate(
        filename,
        pagesize=A4,
        leftMargin=20 * mm,
        rightMargin=20 * mm,
        topMargin=20 * mm,
        bottomMargin=20 * mm,
    )
    styles = getSampleStyleSheet()

    style_title = ParagraphStyle(
        'TitleCN', parent=styles['Title'],
        fontName=font_name, fontSize=24, leading=28,
        spaceAfter=8, textColor=colors.darkblue,
    )
    style_part_heading = ParagraphStyle(
        'PartHeadingCN', parent=styles['Heading2'],
        fontName=font_name, fontSize=22, leading=26,
        spaceBefore=6, spaceAfter=4, textColor=colors.blue,
    )
    style_question = ParagraphStyle(
        'QuestionCN', parent=styles['Normal'],
        fontName=font_name, fontSize=20, leading=24,
        leftIndent=20, textColor=colors.red,
    )
    style_answer = ParagraphStyle(
        'AnswerCN', parent=styles['Normal'],
        fontName=font_name, fontSize=20, leading=24,
        leftIndent=40, textColor=colors.green,
    )
    style_normal = ParagraphStyle(
        'NormalCN', parent=styles['Normal'],
        fontName=font_name, fontSize=20, leading=24,
        leftIndent=20,
    )
    style_info = ParagraphStyle(
        'InfoCN', parent=styles['Normal'],
        fontName=font_name, fontSize=19, leading=22,
        textColor=colors.gray, alignment=TA_CENTER,
    )

    story = []
    for line in output_text.splitlines():
        stripped = line.strip()
        if not stripped:
            story.append(Spacer(1, 4))
            continue

        if line.startswith("作业文件夹："):
            story.append(Paragraph(line, style_title))
        elif re.match(r'^[\【\[]\s*Part[A-C]\s*[\】\]]', stripped) or re.match(r'^Part[A-C]\s*[：:]', stripped):
            story.append(Paragraph(line, style_part_heading))
        elif re.match(r'^[\【\[]\s*问题\s*\d+\s*[\】\]]', stripped) or re.match(r'^问题\s*\d+\s*[：:]', stripped):
            story.append(Paragraph(line, style_question))
        elif stripped.startswith("候选答案：") or re.match(r'^\s*\d+\.', stripped):
            story.append(Paragraph(line, style_answer))
        elif stripped.startswith("[完成]") or stripped.startswith("警告：") or stripped.startswith("错误："):
            story.append(Paragraph(line, style_info))
        else:
            story.append(Paragraph(line, style_normal))

        story.append(Spacer(1, 2))

    try:
        doc.build(story)
        return True
    except Exception as e:
        print(f"PDF 生成错误：{e}")
        return False