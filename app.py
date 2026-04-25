import os
import re
import threading
from pathlib import Path
from datetime import datetime

import tkinter as tk
from tkinter import ttk, scrolledtext, messagebox

from utils import get_creation_time
from parser import parse_part_a, parse_part_b, parse_part_c
from pdf_generator import generate_pdf, REPORTLAB_AVAILABLE


class EtsParserApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("FuckETS")
        self.root.geometry("950x750")
        self.root.resizable(True, True)

        self.folders: list = []
        self.selected_folder: Path | None = None
        self.current_output: str = ""

        self.create_widgets()
        self.scan_folders()

    # ------------------------------------------------------------------
    # 界面构建
    # ------------------------------------------------------------------
    def create_widgets(self):
        # 顶部工具栏
        top_frame = ttk.Frame(self.root, padding="5")
        top_frame.pack(fill=tk.X)

        ttk.Button(top_frame, text="重新扫描", command=self.scan_folders).pack(side=tk.LEFT, padx=5)
        self.status_label = ttk.Label(top_frame, text="就绪", relief=tk.SUNKEN, anchor=tk.W)
        self.status_label.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=5)

        # 文件夹列表
        list_frame = ttk.LabelFrame(self.root, text="作业文件夹列表", padding="5")
        list_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        columns = ("名称", "创建时间")
        self.tree = ttk.Treeview(list_frame, columns=columns, show="headings", height=8)
        self.tree.heading("名称", text="文件夹名称")
        self.tree.heading("创建时间", text="创建时间")
        self.tree.column("名称", width=180)
        self.tree.column("创建时间", width=200)
        self.tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        scrollbar = ttk.Scrollbar(list_frame, orient=tk.VERTICAL, command=self.tree.yview)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.tree.configure(yscrollcommand=scrollbar.set)
        self.tree.bind('<<TreeviewSelect>>', self.on_folder_select)

        # 操作按钮
        btn_frame = ttk.Frame(self.root, padding="5")
        btn_frame.pack(fill=tk.X)

        self.parse_btn = ttk.Button(
            btn_frame, text="解析所选文件夹",
            command=self.parse_folder, state=tk.DISABLED,
        )
        self.parse_btn.pack(side=tk.LEFT, padx=5)

        self.save_pdf_btn = ttk.Button(
            btn_frame, text="保存为 PDF",
            command=self.save_as_pdf, state=tk.DISABLED,
        )
        self.save_pdf_btn.pack(side=tk.LEFT, padx=5)

        # 结果区域
        result_frame = ttk.LabelFrame(self.root, text="解析结果", padding="5")
        result_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        self.result_text = scrolledtext.ScrolledText(
            result_frame, wrap=tk.WORD, font=("微软雅黑", 10), bg="#fefefe"
        )
        self.result_text.pack(fill=tk.BOTH, expand=True)

        # 文本样式
        self.result_text.tag_configure("title",            font=("微软雅黑", 12, "bold"), foreground="#2c3e50", spacing3=8)
        self.result_text.tag_configure("part_heading",     font=("微软雅黑", 11, "bold"), foreground="#2980b9", spacing1=6, spacing3=6)
        self.result_text.tag_configure("question",         font=("微软雅黑", 10, "bold"), foreground="#c0392b", lmargin1=20)
        self.result_text.tag_configure("answer_candidate", lmargin1=40, foreground="#27ae60")
        self.result_text.tag_configure("normal",           font=("微软雅黑", 10), lmargin1=20)
        self.result_text.tag_configure("info",             font=("微软雅黑", 9, "italic"), foreground="#7f8c8d")

        # 底部状态栏
        bottom_frame = ttk.Frame(self.root, padding="2")
        bottom_frame.pack(fill=tk.X, side=tk.BOTTOM)

        self.bottom_label = ttk.Label(bottom_frame, text="", relief=tk.SUNKEN)
        self.bottom_label.pack(fill=tk.X)

        if not REPORTLAB_AVAILABLE:
            self.save_pdf_btn.config(state=tk.DISABLED)
            self.bottom_label.config(
                text="提示：未安装 reportlab，PDF 保存功能不可用。请运行 'pip install reportlab' 安装。"
            )

    # ------------------------------------------------------------------
    # 扫描文件夹
    # ------------------------------------------------------------------
    def scan_folders(self):
        self.status_label.config(text="正在扫描...")
        self.root.update_idletasks()

        for item in self.tree.get_children():
            self.tree.delete(item)
        self.folders.clear()
        self.selected_folder = None
        self.parse_btn.config(state=tk.DISABLED)
        self.save_pdf_btn.config(state=tk.DISABLED)
        self.result_text.delete(1.0, tk.END)

        appdata = os.environ.get('APPDATA')
        if not appdata:
            messagebox.showerror("错误", "未找到 APPDATA 环境变量")
            self.status_label.config(text="错误：未找到 APPDATA")
            return

        base_dir = Path(appdata) / "ETS"
        if not base_dir.exists() or not base_dir.is_dir():
            messagebox.showerror("错误", f"目录 {base_dir} 不存在")
            self.status_label.config(text=f"目录不存在: {base_dir}")
            return

        try:
            for item in base_dir.iterdir():
                if item.is_dir() and item.name.isdigit():
                    ctime = get_creation_time(item)
                    self.folders.append((item.name, item, ctime))
        except Exception as e:
            messagebox.showerror("扫描错误", str(e))
            self.status_label.config(text="扫描出错")
            return

        if not self.folders:
            self.status_label.config(text="未找到作业文件夹")
            messagebox.showinfo("提示", "未找到作业名称的文件夹")
            return

        for name, path, ctime in self.folders:
            self.tree.insert("", tk.END, values=(name, ctime), tags=(str(path),))
        self.status_label.config(text=f"扫描完成，找到 {len(self.folders)} 个文件夹")

    # ------------------------------------------------------------------
    # 选择文件夹
    # ------------------------------------------------------------------
    def on_folder_select(self, event):
        selection = self.tree.selection()
        if not selection:
            self.selected_folder = None
            self.parse_btn.config(state=tk.DISABLED)
            return

        folder_name = self.tree.item(selection[0], "values")[0]
        self.selected_folder = next(
            (path for name, path, _ in self.folders if name == folder_name), None
        )
        self.parse_btn.config(state=tk.NORMAL if self.selected_folder else tk.DISABLED)

    # ------------------------------------------------------------------
    # 解析
    # ------------------------------------------------------------------
    def parse_folder(self):
        if not self.selected_folder:
            return
        self.parse_btn.config(state=tk.DISABLED)
        self.save_pdf_btn.config(state=tk.DISABLED)
        self.result_text.delete(1.0, tk.END)
        self.result_text.insert(tk.END, f"[作业文件夹] {self.selected_folder.name}\n", "title")
        self.result_text.insert(tk.END, "[处理中] 解析进行中，请稍候...\n", "info")
        self.status_label.config(text="解析中...")
        self.root.update_idletasks()

        threading.Thread(target=self._parse_worker, daemon=True).start()

    def _parse_worker(self):
        output_lines: list[str] = []
        folder_name = self.selected_folder.name
        output_lines.append(f"作业文件夹：{folder_name}")

        content_pattern = re.compile(r'^content_(\d+)$')
        matches: list[tuple[int, Path]] = []

        try:
            for subdir in self.selected_folder.iterdir():
                if subdir.is_dir():
                    m = content_pattern.match(subdir.name)
                    if m:
                        matches.append((int(m.group(1)), subdir))
        except Exception as e:
            output_lines.append(f"读取子文件夹出错：{e}")
            self._update_result(output_lines, folder_name)
            return

        if len(matches) < 3:
            output_lines.append(f"警告：只找到 {len(matches)} 个子文件夹，期望 3 个")
        matches.sort(key=lambda x: x[0])

        part_parsers = [
            ('PartA', parse_part_a),
            ('PartB', parse_part_b),
            ('PartC', parse_part_c),
        ]
        for idx, (part_name, parser_fn) in enumerate(part_parsers):
            if idx < len(matches):
                folder = matches[idx][1]
                output_lines.append(f"\n【{part_name}】 子文件夹：{folder.name}")
                json_file = folder / "content.json"
                if not json_file.exists():
                    output_lines.append(f"错误：未找到 {json_file}")
                    continue
                parser_fn(json_file, output_lines)
            else:
                output_lines.append(f"\n【{part_name}】 未找到对应的子文件夹")

        complete_time = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
        output_lines.append(f"\n[完成] 解析完成时间：{complete_time}")
        self._update_result(output_lines, folder_name)

    def _update_result(self, output_lines: list[str], folder_name: str):
        self.current_output = "\n".join(output_lines)

        def update_gui():
            self.result_text.delete(1.0, tk.END)
            self.result_text.insert(tk.END, f"[作业文件夹] {folder_name}\n", "title")

            for line in output_lines:
                if line.startswith(f"作业文件夹：{folder_name}"):
                    continue

                stripped = line.strip()
                if not stripped:
                    self.result_text.insert(tk.END, "\n", "normal")
                    continue

                if re.match(r'^[\【\[]\s*Part[A-C]\s*[\】\]]', stripped) or re.match(r'^Part[A-C]\s*[：:]', stripped):
                    tag = "part_heading"
                elif re.match(r'^[\【\[]\s*问题\s*\d+\s*[\】\]]', stripped) or re.match(r'^问题\s*\d+\s*[：:]', stripped):
                    tag = "question"
                elif stripped.startswith("候选答案：") or re.match(r'^\s*\d+\.', stripped):
                    tag = "answer_candidate"
                elif stripped.startswith("[完成]") or stripped.startswith("警告：") or stripped.startswith("错误："):
                    tag = "info"
                else:
                    tag = "normal"

                self.result_text.insert(tk.END, line + "\n", tag)

            self.result_text.see(tk.END)
            self.parse_btn.config(state=tk.NORMAL)
            self.save_pdf_btn.config(state=tk.NORMAL)
            self.status_label.config(text="解析完成")

        self.root.after(0, update_gui)

    # ------------------------------------------------------------------
    # 保存 PDF
    # ------------------------------------------------------------------
    def save_as_pdf(self):
        if not self.current_output:
            messagebox.showwarning("警告", "没有可保存的内容，请先解析一个文件夹。")
            return
        if not REPORTLAB_AVAILABLE:
            messagebox.showerror("错误", "未安装 reportlab 库，无法生成 PDF。\n请运行: pip install reportlab")
            return

        from tkinter import filedialog
        filename = filedialog.asksaveasfilename(
            defaultextension=".pdf",
            filetypes=[("PDF files", "*.pdf")],
            title="保存 PDF 文件",
        )
        if not filename:
            return

        self.status_label.config(text="正在生成 PDF...")
        self.root.update_idletasks()

        def generate():
            success = generate_pdf(self.current_output, filename)

            def done():
                if success:
                    self.status_label.config(text=f"PDF 已保存: {os.path.basename(filename)}")
                    messagebox.showinfo("成功", f"PDF 已保存至:\n{filename}")
                else:
                    self.status_label.config(text="PDF 生成失败")
                    messagebox.showerror("错误", "PDF 生成失败，请检查 reportlab 安装和字体配置。")

            self.root.after(0, done)

        threading.Thread(target=generate, daemon=True).start()