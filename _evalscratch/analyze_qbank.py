# -*- coding: utf-8 -*-
"""分析 pdf_questions.json 题库的内容分布，估算引擎可覆盖的题量。"""
import json, re, collections

PATH = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
d = json.load(open(PATH, encoding="utf-8"))
print("total", len(d))
print("types", collections.Counter(x.get("type") for x in d))

def has(text, *kws):
    return any(k in text for k in kws)

buckets = collections.Counter()
samples = collections.defaultdict(list)
for x in d:
    q = x.get("question","") + " " + (x.get("note",""))
    t = x.get("type")
    b = []
    if has(q, "共存", "大量共存"): b.append("ion_coexist")
    if has(q, "物质的量", "浓度", "mol", "pH", "Ksp", "Ka", "求", "计算"): b.append("calculation")
    if has(q, "平衡", "转化率", "电离", "水解", "勒夏特列", "溶解平衡"): b.append("equilibrium")
    if has(q, "方程式", "配平", "能否反应", "反应", "离子方程式"): b.append("reaction")
    if has(q, "结构", "电子式", "价键", "杂化", "VSEPR", "构型"): b.append("structure")
    if has(q, "滴定"): b.append("titration")
    if has(q, "鉴别", "检验", "区分", "推断"): b.append("identification")
    if has(q, "分类", "氧化物", "电解质", "胶体", "分散系", "同素异形体", "同位素", "混合物"): b.append("concept_classify")
    if not b: b.append("other_concept")
    for k in b: buckets[k]+=1
    if len(samples[b[0]])<3: samples[b[0]].append((x["id"], x["question"][:60]))

print("\n=== 内容桶（可重叠）===")
for k,v in buckets.most_common():
    print(f"{k:20s} {v}")

print("\n=== 各桶样本 ===")
for k in samples:
    print(f"--- {k} ---")
    for sid,sq in samples[k]:
        print(f"  {sid}: {sq}")

# 缺选项的选择题（在备份里）
bk = json.load(open(r"d:\github\CogniThink-Chem\pdfs\questions_parsed.json", encoding="utf-8"))
miss = [x for x in bk if x.get("type")=="choice" and not x.get("options")]
print(f"\n=== 备份中缺 options 的选择题 {len(miss)} 道 ===")
for x in miss[:8]:
    print(f"  {x['id']}: {x['question'][:70]}")
    print(f"     answer={x.get('answer')} kw={x.get('answer_keywords')}")
