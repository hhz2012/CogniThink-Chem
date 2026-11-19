# -*- coding: utf-8 -*-
"""生成抽检对比页：原图 PNG vs 重绘 SVG，按类型抽样。输出 out/preview_batch.html。"""
import json, os
from collections import defaultdict

ROOT = r"d:\github\CogniThink-Chem"
DATA = os.path.join(ROOT, "src", "CogniThink.TutorBrain.Api", "data")
OUT = os.path.join(ROOT, "pdfs", "_extract", "vectorize", "out")
BATCH = os.path.join(OUT, "imageAssets_batch.json")
HTML = os.path.join(OUT, "preview_batch.html")

assets = json.load(open(BATCH, encoding="utf-8"))
by_type = defaultdict(list)
for a in assets:
    by_type[a["type"]].append(a)

# 每类抽前 5 个 + 最后 1 个，覆盖头尾
sample = []
for t, lst in by_type.items():
    take = lst[:5]
    if len(lst) > 5:
        take.append(lst[-1])
    sample.extend(take)

def svg_inline(path):
    if not path or not os.path.exists(path) or os.path.getsize(path) == 0:
        return '<div style="color:#c00;padding:20px">(no svg)</div>'
    with open(path, encoding="utf-8", errors="ignore") as f:
        return f.read()

def orig_img(src):
    p = os.path.join(DATA, src.replace("/", os.sep))
    if not os.path.exists(p):
        return '<div style="color:#c00">(original missing)</div>'
    import base64
    with open(p, "rb") as f:
        b64 = base64.b64encode(f.read()).decode()
    return f'<img src="data:image/png;base64,{b64}" style="max-width:320px;max-height:320px;border:1px solid #ddd"/>'

rows = []
for a in sample:
    payload = a.get("payload", {})
    extra = ""
    if a["type"] == "molecule":
        extra = f'<div style="font-size:12px;color:#555">valid={payload.get("valid")} smiles={payload.get("smiles","")[:60]}</div>'
    elif a["type"] == "curve":
        extra = f'<div style="font-size:12px;color:#555">x={payload.get("xLabel","")} y={payload.get("yLabel","")} series={len(payload.get("series",[]))}</div>'
    elif a["type"] == "graph":
        extra = f'<div style="font-size:12px;color:#555">nodes={len(payload.get("nodes",[]))} edges={len(payload.get("edges",[]))}</div>'
    rows.append(f"""<div style="border:1px solid #eee;padding:8px;margin:6px;display:inline-block;vertical-align:top;width:340px">
      <div style="font-weight:bold">{a['key']}</div>
      <div style="display:flex;gap:6px">
        <div><div style="font-size:11px;color:#888">ORIGINAL</div>{orig_img(a['source'])}</div>
        <div><div style="font-size:11px;color:#888">REDRAWN SVG</div><div style="width:320px;height:320px;overflow:auto;border:1px solid #ddd">{svg_inline(a.get('svg'))}</div></div>
      </div>
      {extra}
    </div>""")

html = f"""<html><head><meta charset="utf-8"><title>Batch Spot Check</title></head>
<body><h2>Batch Spot Check ({len(sample)} sampled of {len(assets)})</h2>
<div>{''.join(rows)}</div></body></html>"""
open(HTML, "w", encoding="utf-8").write(html)
print("preview ->", HTML)
print("sampled:", [a["key"] for a in sample])
