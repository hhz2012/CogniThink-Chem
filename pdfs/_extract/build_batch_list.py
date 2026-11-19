# -*- coding: utf-8 -*-
"""从 image_meaning_report.json 筛选前三类（曲线/流程/结构）的有意义图，生成批量处理清单。"""
import json, os
ROOT = r"d:\github\CogniThink-Chem"
RPT = os.path.join(ROOT, "pdfs", "_extract", "image_meaning_report.json")
OUT = os.path.join(ROOT, "pdfs", "_extract", "vectorize", "batch_list.json")

TYPE_MAP = {
    "curve":    ["坐标曲线图"],
    "graph":    ["转化关系图", "工艺流程图"],
    "molecule": ["物质结构式", "键线式", "分子结构式", "分子结构图", "球棍模型", "空间填充模型"],
}
name_to_vtype = {n: v for v, names in TYPE_MAP.items() for n in names}

data = json.load(open(RPT, encoding="utf-8"))
rows = data["rows"]
items = []
seen = set()
for r in rows:
    if not r["meaningful"]: continue
    vt = name_to_vtype.get(r["type"])
    if not vt: continue
    key = (r["id"], r["image"])
    if key in seen: continue
    seen.add(key)
    items.append({"id": r["id"], "type": vt, "img": r["image"], "src_type": r["type"]})

# 按 vector type 统计
from collections import Counter
c = Counter(it["type"] for it in items)
print("total target images:", len(items))
print("by vector type:", dict(c))
print("unique questions:", len(set(it["id"] for it in items)))
# 检查是否有一题多图且不同类型
from collections import defaultdict
qtypes = defaultdict(set)
for it in items: qtypes[it["id"]].add(it["type"])
multi = {k:v for k,v in qtypes.items() if len(v)>1}
print("questions with multiple asset types:", len(multi))
for k,v in list(multi.items())[:5]: print("  ",k,v)

json.dump(items, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("list ->", OUT)
