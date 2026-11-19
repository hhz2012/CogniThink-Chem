# -*- coding: utf-8 -*-
"""渲染指定 PDF 页为 PNG，供查看与后续裁剪。"""
import os, sys, json
import pypdfium2 as pdfium

PDF = r"d:\github\CogniThink-Chem\pdfs\2026一化儿100讲（基础+中档）习题册.pdf"
REPORT = r"d:\github\CogniThink-Chem\pdfs\_extract\image_report.json"
OUT = r"d:\github\CogniThink-Chem\pdfs\_extract\page_renders"
os.makedirs(OUT, exist_ok=True)

def main():
    pages = json.load(open(REPORT, encoding="utf-8"))["pages_with_images"]
    pdf = pdfium.PdfDocument(PDF)
    scale = int(sys.argv[1]) if len(sys.argv) > 1 else 3
    # 仅渲染传入的页码子集（用于样本），否则渲染全部
    subset = [int(x) for x in sys.argv[2:]] if len(sys.argv) > 2 else None
    targets = subset if subset else pages
    for p in targets:
        out = os.path.join(OUT, f"page_{p:03d}.png")
        if os.path.exists(out):
            continue
        img = pdf[p].render(scale=scale).to_pil()
        img.save(out)
        print(f"rendered page {p}: {img.size} -> {out}")
    print("done")

if __name__ == "__main__":
    main()
