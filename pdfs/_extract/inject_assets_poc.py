# -*- coding: utf-8 -*-
"""把 PoC 的 imageAssets 注入题库 JSON：复制 SVG 到 data/vector_svg，给对应题目加 imageAssets 字段。"""
import json, os, shutil

ROOT = r"d:\github\CogniThink-Chem"
DATA = os.path.join(ROOT, "src", "CogniThink.TutorBrain.Api", "data")
VEC = os.path.join(DATA, "vector_svg")
os.makedirs(VEC, exist_ok=True)
OUT = os.path.join(ROOT, "pdfs", "_extract", "vectorize", "out")
Q = os.path.join(DATA, "pdf_questions.json")

assets = json.load(open(os.path.join(OUT, "imageAssets_poc.json"), encoding="utf-8"))
amap = {a["id"]: a for a in assets}

qdata = json.load(open(Q, encoding="utf-8"))
for item in qdata:
    a = amap.get(item["id"])
    if not a: continue
    # 复制 svg 到 data/vector_svg
    svg_name = os.path.basename(a["svg"])
    shutil.copy(a["svg"], os.path.join(VEC, svg_name))
    item["imageAssets"] = [{
        "type": a["type"],
        "source": a["source"],
        "svg": f"vector_svg/{svg_name}",
        "payload": a["payload"],
    }]
json.dump(qdata, open(Q, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("injected imageAssets for:", list(amap.keys()))
# 验证
d = json.load(open(Q, encoding="utf-8"))
for qid in amap:
    x = next(q for q in d if q["id"] == qid)
    print(qid, "->", "imageAssets" in x, "types:", [ia["type"] for ia in x.get("imageAssets", [])])
