"""汇总 vl_cache3 的判断结果，输出统计与 markdown 报告。"""
import json, glob, os
from collections import Counter, defaultdict

ROOT = r"d:\github\CogniThink-Chem"
CACHE = os.path.join(ROOT, r"pdfs\_extract\vl_cache3")
Q_PATH = os.path.join(ROOT, r"src\CogniThink.TutorBrain.Api\data\pdf_questions.json")
REPORT = os.path.join(ROOT, r"pdfs\_extract\image_meaning_report.md")
JSON_OUT = os.path.join(ROOT, r"pdfs\_extract\image_meaning_report.json")

qdata = {x["id"]: x for x in json.load(open(Q_PATH, encoding="utf-8"))}

rows = []  # per-image rows
per_q = {}
errors = []
for f in sorted(glob.glob(os.path.join(CACHE, "*.json"))):
    d = json.load(open(f, encoding="utf-8"))
    qid = d.get("id") or os.path.basename(f)[:-5]
    if "error" in d or "judgments" not in d:
        errors.append(qid); continue
    q = qdata.get(qid, {})
    judg = d["judgments"]
    images = d.get("images") or q.get("images") or []
    meaningful_flags = []
    for j in judg:
        idx = j.get("index")
        rel = images[idx] if idx is not None and idx < len(images) else (images[0] if images else "")
        m = j.get("meaningful")
        meaningful_flags.append(bool(m))
        rows.append({
            "id": qid, "image": rel, "meaningful": bool(m),
            "type": j.get("type", "未知"), "reason": j.get("reason", ""),
            "question": (q.get("question") or "")[:80],
            "page": rel,
        })
    n_mean = sum(meaningful_flags)
    per_q[qid] = {"n_img": len(images), "n_mean": n_mean, "all_mean": n_mean == len(images) and len(images) > 0, "none_mean": n_mean == 0}

n_img = len(rows)
n_mean = sum(1 for r in rows if r["meaningful"])
n_not = n_img - n_mean
by_type = Counter(r["type"] for r in rows)
mean_by_type = defaultdict(lambda: [0, 0])
for r in rows:
    mean_by_type[r["type"]][0 if r["meaningful"] else 1] += 1

# per-question stats
n_q = len(per_q)
q_all_mean = sum(1 for v in per_q.values() if v["all_mean"])
q_none_mean = sum(1 for v in per_q.values() if v["none_mean"])
q_partial = n_q - q_all_mean - q_none_mean

lines = []
lines.append("# 题库配图解题意义分析报告\n")
lines.append(f"- 带图题目数：**{n_q}**（错误/未完成：{len(errors)}）")
lines.append(f"- 图片总数：**{n_img}**")
lines.append(f"- 对解题**有意义**的图片：**{n_mean}**（{n_mean/n_img*100:.1f}%）")
lines.append(f"- 对解题**无意义**的图片：**{n_not}**（{n_not/n_img*100:.1f}%）\n")
lines.append("## 按题目维度\n")
lines.append(f"- 全部图片都有意义的题：{q_all_mean}")
lines.append(f"- 全部图片都无意义的题：{q_none_mean}")
lines.append(f"- 部分图片有意义的题：{q_partial}\n")
lines.append("## 按图片类型（有意义 / 无意义）\n")
lines.append("| 类型 | 有意义 | 无意义 | 有意义占比 |")
lines.append("|---|---|---|---|")
for t, (m, nm) in sorted(mean_by_type.items(), key=lambda x: -(x[1][0]+x[1][1])):
    tot = m + nm
    lines.append(f"| {t} | {m} | {nm} | {m/tot*100:.0f}% |")
lines.append("")

lines.append("## 无意义图片清单（建议评估是否保留）\n")
lines.append("| 题目ID | 图片 | 类型 | 理由 | 题干摘要 |")
lines.append("|---|---|---|---|---|")
for r in rows:
    if not r["meaningful"]:
        lines.append(f"| {r['id']} | {r['image']} | {r['type']} | {r['reason']} | {r['question']} |")
lines.append("")

lines.append("## 有意义图片的典型样例（每类前若干）\n")
seen = defaultdict(int)
for r in rows:
    if r["meaningful"] and seen[r["type"]] < 3:
        seen[r["type"]] += 1
        lines.append(f"- **{r['id']}** [{r['type']}] {r['image']} — {r['reason']}")
lines.append("")

if errors:
    lines.append("## 未完成/出错的题目\n")
    lines.append(", ".join(errors))

with open(REPORT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))

json.dump({"rows": rows, "per_q": per_q, "stats": {
    "questions": n_q, "images": n_img, "meaningful": n_mean, "not_meaningful": n_not,
    "questions_all_meaningful": q_all_mean, "questions_none_meaningful": q_none_mean,
    "questions_partial": q_partial, "by_type": {t: {"meaningful": v[0], "not": v[1]} for t, v in mean_by_type.items()},
    "errors": errors,
}}, open(JSON_OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

print("questions:", n_q, "images:", n_img, "meaningful:", n_mean, "not:", n_not)
print("report:", REPORT)
print("json:", JSON_OUT)
