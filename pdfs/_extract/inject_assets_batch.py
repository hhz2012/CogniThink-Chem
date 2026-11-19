# -*- coding: utf-8 -*-
"""把批量重绘的 imageAssets 注入题库：
- 复制 SVG 到 data/vector_svg/{key}.svg
- 按 id 合并多个 asset，写入 pdf_questions.json 和 questions_parsed.json
- 清理 vector_svg 下未被引用的孤儿 SVG
"""
import json, os, shutil
from collections import defaultdict

ROOT = r"d:\github\CogniThink-Chem"
DATA = os.path.join(ROOT, "src", "CogniThink.TutorBrain.Api", "data")
VEC = os.path.join(DATA, "vector_svg")
OUT = os.path.join(ROOT, "pdfs", "_extract", "vectorize", "out")
Q = os.path.join(DATA, "pdf_questions.json")
QP = os.path.join(ROOT, "pdfs", "questions_parsed.json")
BATCH = os.path.join(OUT, "imageAssets_batch.json")
VERIFY = os.path.join(OUT, "graph_verify_report.json")

os.makedirs(VEC, exist_ok=True)

# ---- 第二道保险：独立 VL 二分类校验，防止 graph 幻觉（非转化图被臆造成流程图）----
# verify_graph.py 对全部 graph 原图做与提取解耦的独立判定，is_flow=false 一律不注入。
# 报告缺失时回退到仅依赖提示词守卫（graph_empty），但打印警告提醒先跑 verify。
verify_by_source = {}
if os.path.exists(VERIFY):
    rep = json.load(open(VERIFY, encoding="utf-8"))
    verify_by_source = {e.get("img"): e.get("is_flow") for e in rep if isinstance(e, dict)}
    print(f"loaded graph verify report: {len(verify_by_source)} entries")
else:
    print("WARNING: graph_verify_report.json not found; verify gate disabled (guard only). "
          "Run verify_graph.py first to enforce the second safeguard.")

assets = json.load(open(BATCH, encoding="utf-8"))
by_id = defaultdict(list)
referenced_svgs = set()
skipped = {"molecule_invalid": 0, "graph_empty": 0, "graph_notflow": 0, "curve_empty": 0}
for a in assets:
    p = a.get("payload", {})
    # molecule 仅注入 RDKit 校验通过的；无效 SMILES 留给运行时回退位图给 VL
    if a["type"] == "molecule" and not p.get("valid"):
        skipped["molecule_invalid"] += 1
        continue
    # graph 无节点（非转化图或提取失败）→ 不注入，回退位图
    if a["type"] == "graph" and not p.get("nodes"):
        skipped["graph_empty"] += 1
        continue
    # graph 独立二分类校验 is_flow=false → 幻觉/非转化图（价类图/散点图/装置图等），
    # 即使守卫漏判产生了节点也不注入，强制回退位图
    if a["type"] == "graph" and verify_by_source:
        v = verify_by_source.get(a["source"])
        if v is False:
            skipped["graph_notflow"] += 1
            continue
        if v is None:
            print(f"WARNING: graph source not in verify report, passed by fallback: {a['source']}")
    # curve 无任何数据点 → 不注入，回退位图
    if a["type"] == "curve":
        maxp = max((len(s.get("points", [])) for s in p.get("series", [])), default=0)
        if maxp == 0:
            skipped["curve_empty"] += 1
            continue
    key = a["key"]
    svg_dst_name = f"{key}.svg"
    svg_rel = None
    src_svg = a.get("svg", "")
    if src_svg and os.path.exists(src_svg) and os.path.getsize(src_svg) > 0:
        shutil.copy(src_svg, os.path.join(VEC, svg_dst_name))
        svg_rel = f"vector_svg/{svg_dst_name}"
        referenced_svgs.add(svg_dst_name)
    by_id[a["id"]].append({
        "type": a["type"],
        "source": a["source"],
        "svg": svg_rel,
        "payload": a["payload"],
    })

def inject(path):
    data = json.load(open(path, encoding="utf-8"))
    n = 0; cleared = 0
    for item in data:
        qid = item.get("id")
        if qid in by_id:
            item["imageAssets"] = by_id[qid]
            n += 1
        elif "imageAssets" in item:
            # 清除上一轮注入但本轮被过滤掉的残留资产，避免陈旧/幻觉数据残留
            del item["imageAssets"]; cleared += 1
    json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    return n, cleared

n_q, c_q = inject(Q)
n_qp, c_qp = inject(QP) if os.path.exists(QP) else (0, 0)

# 清理孤儿 SVG
orphans = []
for fn in os.listdir(VEC):
    if fn.endswith(".svg") and fn not in referenced_svgs:
        os.remove(os.path.join(VEC, fn))
        orphans.append(fn)

print(f"skipped (fallback to bitmap VL): {skipped}")
print(f"questions updated in pdf_questions.json: {n_q} (stale cleared: {c_q})")
print(f"questions updated in questions_parsed.json: {n_qp} (stale cleared: {c_qp})")
print(f"total assets injected: {sum(len(v) for v in by_id.values())}")
print(f"svg copied: {len(referenced_svgs)}")
print(f"orphan svg removed: {len(orphans)}", orphans)

# 验证
d = json.load(open(Q, encoding="utf-8"))
with_assets = [q for q in d if q.get("imageAssets")]
print(f"verify: total={len(d)}, withImageAssets={len(with_assets)}")
from collections import Counter
tc = Counter()
for q in with_assets:
    for ia in q["imageAssets"]:
        tc[ia["type"]] += 1
print("asset types:", dict(tc))
multi = [q["id"] for q in with_assets if len(q["imageAssets"]) > 1]
print(f"questions with >1 asset: {len(multi)}", multi[:10])
