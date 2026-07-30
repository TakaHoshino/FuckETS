"""
pdf_generator.py — 将解析结果输出为 PDF 文件
"""
import xml.sax.saxutils as saxutils

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

from utils import find_chinese_font, classify_line


def generate_pdf(output_text: str, filename: str) -> tuple[bool, str]:
    """将文本内容渲染为带样式的 PDF，返回 (是否成功, 错误信息)。"""
    if not REPORTLAB_AVAILABLE:
        return False, "未安装 reportlab 库"

    font_path = find_chinese_font()
    if not font_path:
        return False, "未找到中文字体文件，无法生成 PDF"
    try:
        pdfmetrics.registerFont(TTFont('ChineseFont', font_path))
        font_name = 'ChineseFont'
    except Exception:
        return False, f"加载中文字体失败: {font_path}"

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

    style_map = {
        'title': style_title,
        'part_heading': style_part_heading,
        'question': style_question,
        'answer_candidate': style_answer,
        'info': style_info,
        'normal': style_normal,
    }

    def escape_for_reportlab(text: str) -> str:
        """转义 XML 特殊字符，同时将换行符转换为 <br/> 供 ReportLab 渲染。"""
        return saxutils.escape(text).replace('\n', '<br/>')

    story = []
    for line in output_text.splitlines():
        stripped = line.strip()
        if not stripped:
            story.append(Spacer(1, 4))
            continue

        category = classify_line(line)
        story.append(Paragraph(escape_for_reportlab(line), style_map.get(category, style_normal)))
        story.append(Spacer(1, 2))

    try:
        doc.build(story)
        return True, ""
    except Exception as e:
        return False, f"PDF 生成错误：{e}"