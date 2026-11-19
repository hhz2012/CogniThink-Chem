# -*- coding: utf-8 -*-
"""分析 90 道 evidence-LLM 题：纯 LLM 是否答对 + 题型/问法分布，确定阶段 3 潜在收益上限。"""
import json, io, sys, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

with open(r"D:\github\CogniThink-Chem\_evalscratch\after8b.json", encoding="utf-8-sig") as f:
    data = json.load(f)

cases = []
for key in ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]:
    for c in data.get(key, []):
        cases.append(c)

def norm(s): return "".join((s or "").split())
def is_concrete(concl):
    if not concl: return False
    if "已匹配" in concl or "相关反应" in concl: return False
    return any(w in concl for w in ["能","不能","共存","不共存","可以","不可以","正确","错误"])
def is_relevant(concl, q):
    if not concl: return False
    if "共存" in concl and "共存" not in (q or ""): return False
    return True

evidence = []
for c in cases:
    if not c.get("gatewayCorrect"): continue
    if not c.get("gatewayUsedEngine"): continue
    gwa = c.get("gatewayAnswer") or ""
    pla = c.get("pureLlmAnswer") or ""
    if norm(gwa) == norm(pla) and pla: continue
    tt = c.get("taskType") or "?"
    concl = c.get("gatewayConclusion") or ""
    if tt in ("reaction_check","balance") and is_relevant(concl, c.get("question") or "") and not is_concrete(concl):
        evidence.append(c)

print(f"evidence-LLM 题数: {len(evidence)}")
pure_right = sum(1 for c in evidence if c.get("pureLlmCorrect"))
pure_wrong = len(evidence) - pure_right
print(f"  其中纯 LLM 答对: {pure_right}  → 阶段1后变为 pureLlmOnly（不影响网关真值）")
print(f"  其中纯 LLM 答错: {pure_wrong}  → 阶段1后变为 bothWrong（丢掉 evidence-LLM 增益，阶段3需补回）")
print(f"\n按 taskType:")
from collections import Counter
print(Counter(c.get("taskType") for c in evidence))
print(f"\n按 isPdf:")
print(f"  PDF: {sum(1 for c in evidence if str(c.get('id','')).startswith('pdf-'))}")
print(f"  手工: {sum(1 for c in evidence if not str(c.get('id','')).startswith('pdf-'))}")
print(f"\n纯 LLM 答错的 {pure_wrong} 题（阶段3 优先目标）id+问法前30字：")
for c in evidence:
    if not c.get("pureLlmCorrect"):
        print(f"  {c.get('id')} [{c.get('taskType')}] {(c.get('question') or '')[:30].replace(chr(10),' ')}")
