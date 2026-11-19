# -*- coding: utf-8 -*-
import json
from collections import Counter

SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"

def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)

data = load(SCRATCH + r"\after_stage3b.json")
parts = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]
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

tt_correct = Counter()
tt_total = Counter()
for c in cases:
    tt = c.get("taskType") or "?"
    tt_total[tt] += 1
    if c.get("gatewayCorrect"):
        tt_correct[tt] += 1

# TR-9.1 carryover
eq_ans = 0
for c in cases:
    ga = (c.get("gatewayAnswer") or "").strip()
    pa = (c.get("pureLlmAnswer") or "").strip()
    if ga and ga == pa:
        eq_ans += 1

print("=== 表格行（阶段3B / after_stage3b）===")
print(f"总={N} 纯LLM={pure} 网关={gw} 介入(used)={used} 真值解出(solved)={solved} "
      f"总体网关%={gw*100/N:.1f} PDF网关%={pdf_gw*100/len(pdf):.1f} "
      f"手工网关={man_gw}/{len(man)} 手工纯={man_pure} 帮倒忙={harm}")
print(f"calc+eq介入(correct)={tt_correct.get('calculation',0)+tt_correct.get('equilibrium',0)} "
      f"(calc {tt_correct.get('calculation',0)} + eq {tt_correct.get('equilibrium',0)})")
print(f"ion正确(tt=ion_check)={tt_correct.get('ion_check',0)}  balance={tt_correct.get('balance',0)}  "
      f"reaction_check={tt_correct.get('reaction_check',0)}")
print("per-taskType correct/total:")
for tt in sorted(tt_total):
    print(f"  {tt:18} {tt_correct[tt]:>3}/{tt_total[tt]:<3}")

print("\n=== AC-12 专项（choice）===")
choice = [c for c in cases if (c.get("taskType") or "") == "choice"]
print(f"choice 总数 = {len(choice)}")
ch_used = [c for c in choice if c.get("gatewayUsedEngine")]
ch_truth = [c for c in choice if c.get("gatewayTruthAdopted")]
ch_correct = [c for c in choice if c.get("gatewayCorrect")]
print(f"choice 介入(gatewayUsedEngine=true) = {len(ch_used)}")
print(f"choice 真值采用(gatewayTruthAdopted=true) = {len(ch_truth)}")
print(f"choice gatewayCorrect = {len(ch_correct)}")
# 断言正确率：truthAdopted 且 correct 的占 truthAdopted 的比例（严格剔除口径）
if ch_truth:
    ch_truth_correct = [c for c in ch_truth if c.get("gatewayCorrect")]
    print(f"choice 真值采用且正确 = {len(ch_truth_correct)}/{len(ch_truth)} = {len(ch_truth_correct)*100/len(ch_truth):.1f}%")
else:
    print("choice 真值采用=0，无法计算正确率（AC-12 不达标）")
    ch_truth_correct = []

print("\nchoice 介入明细（id | ans | gwCorrect | truthAdopted | gwAnswer）:")
for c in sorted(ch_used, key=lambda x: x["id"]):
    ga = (c.get("gatewayAnswer") or "").replace("\n", " ")[:60]
    print(f"  {c['id']:10} exp={str(c.get('expected')):4} gwCorr={c.get('gatewayCorrect')} "
          f"truth={c.get('gatewayTruthAdopted')} used={c.get('gatewayUsedEngine')} | {ga}")

print("\n=== 回归核对（vs after_stage3a）===")
prev = load(SCRATCH + r"\after_stage3a.json")
prev_correct = set()
for p in parts:
    for c in prev.get(p, []):
        if c.get("gatewayCorrect"):
            prev_correct.add(c["id"])
cur_correct = set(c["id"] for c in cases if c.get("gatewayCorrect"))
lost = sorted(prev_correct - cur_correct)
gained = sorted(cur_correct - prev_correct)
print(f"stage3a 正确数={len(prev_correct)}  stage3b 正确数={len(cur_correct)}")
print(f"lost（之前对现在错，必须=0）={len(lost)}  {lost if lost else ''}")
print(f"gained（之前错现在对）={len(gained)}  {gained}")

print("\n=== 红线核对 ===")
print(f"帮倒忙 harmCount = {harm} （必须 0）")
print(f"TR-9.1 gatewayAnswer==纯LLM回答 = {eq_ans} （必须 0）")
print(f"引擎精确率 enginePrecision = {solved}/{used} = {solved*100/used:.1f}%" if used else "used=0")
