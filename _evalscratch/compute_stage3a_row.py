# -*- coding: utf-8 -*-
import json
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
with open(SCRATCH + r"\after_stage3a.json", encoding="utf-8-sig") as f:
    data = json.load(f)
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
cases = []
for p in parts:
    for c in data.get(p, []):
        c["_part"] = p
        cases.append(c)

def is_pdf(c): return c["id"].startswith("pdf-")
def is_manual(c): return not is_pdf(c)

N = len(cases)
pure = sum(1 for c in cases if c.get("pureLlmCorrect"))
gw = sum(1 for c in cases if c.get("gatewayCorrect"))
used = sum(1 for c in cases if c.get("gatewayUsedEngine"))
solved = sum(1 for c in cases if c.get("gatewayTruthAdopted") and c.get("gatewayCorrect"))
harm = data.get("harmCount")

pdf = [c for c in cases if is_pdf(c)]
man = [c for c in cases if is_manual(c)]
pdf_gw = sum(1 for c in pdf if c.get("gatewayCorrect"))
man_gw = sum(1 for c in man if c.get("gatewayCorrect"))
man_pure = sum(1 for c in man if c.get("pureLlmCorrect"))

# per-taskType correct (engine truth)
from collections import Counter
tt_correct = Counter()
tt_total = Counter()
for c in cases:
    tt = c.get("taskType") or "?"
    tt_total[tt] += 1
    if c.get("gatewayCorrect"):
        tt_correct[tt] += 1

# answer == pureLlmAnswer check (TR-9.1 carryover)
eq_ans = 0
for c in cases:
    ga = (c.get("gatewayAnswer") or "").strip()
    pa = (c.get("pureLlmAnswer") or "").strip()
    if ga and ga == pa:
        eq_ans += 1

print(f"总={N} 纯LLM={pure} 网关={gw} 介入={used} 总体网关%={gw*100/N:.1f} PDF网关%={pdf_gw*100/len(pdf):.1f} 手工网关={man_gw}/{len(man)} 手工纯={man_pure} 帮倒忙={harm}")
print("ion正确(tt=ion_check correct):", tt_correct.get("ion_check",0))
calceq = tt_correct.get("calculation",0)+tt_correct.get("equilibrium",0)
print("calc+eq介入(correct):", calceq, "(calc",tt_correct.get("calculation",0),"+ eq",tt_correct.get("equilibrium",0),")")
print("balance correct:", tt_correct.get("balance",0))
print("reaction_check correct:", tt_correct.get("reaction_check",0))
print("TR-9.1 carryover: gatewayAnswer==pureLlmAnswer count =", eq_ans, "(must be 0)")
print("\nper-taskType correct/total:")
for tt in sorted(tt_total):
    print(f"  {tt:18} {tt_correct[tt]:>3}/{tt_total[tt]:<3}")
