# -*- coding: utf-8 -*-
"""阶段2 离线安全原型：复刻 ClassifyTaskDeterministic，检验对 stage1 的影响。"""
import json, io, sys, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

with open(r"D:\github\CogniThink-Chem\_evalscratch\after_stage1.json", encoding="utf-8-sig") as f:
    data = json.load(f)
cases=[]
for k in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    for c in data.get(k,[]): cases.append(c)

def classify(text):
    t = text or ""
    tl = t.lower()
    # 1. 选择题：行首 A./B． 选项块
    if re.search(r"(^|\n)\s*[A-D][\.．、]\s", t):
        return "choice"
    if "共存" in tl: return "ion_check"
    if "配平" in tl or "平了吗" in tl or "系数为" in tl or "化学计量数" in tl: return "balance"
    if any(w in tl for w in ["平衡","转化率","勒夏特列","ksp","电离","水解","溶解平衡"]): return "equilibrium"
    if any(w in tl for w in ["物质的量","浓度","体积","mol","ph","质量分数","稀释","ka","计算","求"]): return "calculation"
    if any(w in tl for w in ["能否反应","是否反应","方程式对吗","正确吗","对吗","反应能否","能否发生"]): return "reaction_check"
    if any(w in tl for w in ["结构式","电子式","价键"]): return "structure_verify"
    if any(w in tl for w in ["vsepr","杂化","构型","空间构型","价层电子对"]): return "vsepr"
    if "滴定" in tl: return "titration"
    if any(w in tl for w in ["什么是","定义","概念","区别","为什么","原理","是指"]): return "concept"
    return "unknown"

from collections import Counter
det_dist = Counter()
unknown_ids = []
for c in cases:
    d = classify(c.get("question") or "")
    det_dist[d]+=1
    if d=="unknown": unknown_ids.append(c.get("id"))

print("=== 规则链分类分布（648题）===")
for k,v in det_dist.most_common():
    print(f"  {k}: {v}")
print(f"  unknown（保留LLM，可能漂移）: {len(unknown_ids)}  ({len(unknown_ids)/648*100:.1f}%)")

# ① 56 道 stage1 引擎正确：规则链是否给出同 taskType（保住引擎介入）
correct56 = [c for c in cases if c.get("gatewayCorrect")]
keep = sum(1 for c in correct56 if classify(c.get("question") or "") == c.get("taskType"))
change = [(c.get("id"), c.get("taskType"), classify(c.get("question") or "")) for c in correct56 if classify(c.get("question") or "") != c.get("taskType")]
print(f"\n=== ① stage1 引擎正确 {len(correct56)} 题：规则链同 taskType ===")
print(f"  保住: {keep}  改变: {len(change)}")
for x in change[:20]:
    print(f"    {x[0]}: stage1={x[1]} -> 规则={x[2]}")

# ② 选择题识别率：以'题干含行首选项块'为 ground-truth proxy
choice_gt = [c for c in cases if re.search(r"(^|\n)\s*[A-D][\.．、]\s", c.get("question") or "")]
choice_det = [c for c in choice_gt if classify(c.get("question") or "") == "choice"]
print(f"\n=== ② 选择题识别（ground-truth: 题干含选项块）===")
print(f"  ground-truth 选择题数: {len(choice_gt)}")
print(f"  规则识别为 choice: {len(choice_det)}  ({len(choice_det)/max(len(choice_gt),1)*100:.1f}%)")

# ③ 有多少 choice_gt 被规则分到了非 choice（误杀）
miskill = [(c.get("id"), classify(c.get("question") or "")) for c in choice_gt if classify(c.get("question") or "")!="choice"]
print(f"  选择题被误分到非choice: {len(miskill)}")
for x in miskill[:10]: print(f"    {x[0]} -> {x[1]}")
