# -*- coding: utf-8 -*-
"""探测 PDF 页的文本层与图形对象，判断是否原生矢量、能否定位图。"""
import pypdfium2 as pdfium
from collections import Counter

PDF = r"d:\github\CogniThink-Chem\pdfs\2026一化儿100讲（基础+中档）习题册.pdf"

def probe(idx):
    pdf = pdfium.PdfDocument(PDF)
    page = pdf[idx]
    print(f"=== page {idx} ===")
    print(f"page size (pt): {page.get_size()}")
    # 文本层
    tp = page.get_textpage()
    nchar = tp.count_chars()
    print(f"text chars: {nchar}")
    txt = tp.get_text_range()
    print(f"text preview: {txt[:120]!r}")
    # 文本行 bbox
    try:
        lines = tp.get_lines()
        print(f"text lines: {len(lines)}")
        for ln in lines[:4]:
            print(f"   line bbox={ln.bbox} text={ln.get_text()!r}")
    except Exception as e:
        print(f"get_lines err: {e}")
    # 页面对象
    try:
        objs = list(page.get_objects())
        kinds = Counter(o.type for o in objs)
        print(f"page objects: {len(objs)} -> {dict(kinds)}")
        # type: 1=text? actually pdfium constants
        for o in objs[:8]:
            try:
                print(f"   obj type={o.type} bbox={o.get_pos()}")
            except Exception:
                print(f"   obj type={o.type}")
    except Exception as e:
        print(f"get_objects err: {e}")
    tp.close()

for p in [7, 42]:
    probe(p)
