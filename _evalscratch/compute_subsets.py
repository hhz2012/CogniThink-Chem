# -*- coding: utf-8 -*-
import json, io, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
with open(r"D:\github\CogniThink-Chem\_evalscratch\after_stage1.json", encoding="utf-8-sig") as f:
    d = json.load(f)
cases=[]
for k in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    for c in d.get(k,[]): cases.append(c)
pdf=[c for c in cases if str(c.get("id","")).startswith("pdf-")]
hand=[c for c in cases if not str(c.get("id","")).startswith("pdf-")]
def acc(sub,k): return sum(1 for c in sub if c.get(k))
print(f"PDF  n={len(pdf)}  pure={acc(pdf,'pureLlmCorrect')}  gateway(strict)={acc(pdf,'gatewayCorrect')}")
print(f"手工 n={len(hand)} pure={acc(hand,'pureLlmCorrect')} gateway(strict)={acc(hand,'gatewayCorrect')}")
# 手工题中引擎真值解出的
hand_solved = sum(1 for c in hand if c.get("gatewayTruthAdopted"))
hand_correct = acc(hand,"gatewayCorrect")
print(f"手工 engineSolved={hand_solved} 其中正确={hand_correct}")
# 手工错题列表（stage1 判错的手工题）
hand_wrong=[c for c in hand if not c.get("gatewayCorrect")]
print(f"手工判错 {len(hand_wrong)} 题（口径变更后，原靠 evidence-LLM/pureAnswer 的现在不计正确）:")
from collections import Counter
print("  按 taskType:", Counter(c.get("taskType") for c in hand_wrong))
