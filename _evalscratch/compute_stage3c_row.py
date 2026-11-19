# -*- coding: utf-8 -*-
import json

SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
PARTS = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def collect(data):
    cases = []
    for p in PARTS:
        for c in data.get(p, []):
            c = dict(c)
            c["_part"] = p
            cases.append(c)
    return cases


data = load(SCRATCH + r"\after_stage3c.json")
cases = collect(data)

print("=== summary fields ===")
for k in ["totalCases", "pureLlmCorrect", "gatewayCorrect", "gatewayTruthCorrect",
          "gatewayUsedEngine", "engineSolved", "engineCoverage", "enginePrecision",
          "harmCount", "pdfPureLlmCorrect", "pdfGatewayCorrect", "pdfGatewayTruthCorrect"]:
    print(f"  {k} = {data.get(k)}")

print("\n=== partitions ===")
for p in PARTS:
    print(f"  {p} = {len(data.get(p, []))}")

print("\n=== per-taskType (truth adopted & correct) ===")
from collections import defaultdict
tt_total = defaultdict(int)
tt_correct = defaultdict(int)
tt_used = defaultdict(int)
for c in cases:
    tt = c.get("taskType") or "?"
    tt_total[tt] += 1
    if c.get("gatewayTruthAdopted") and c.get("gatewayCorrect"):
        tt_correct[tt] += 1
    if c.get("gatewayUsedEngine"):
        tt_used[tt] += 1
for tt in sorted(tt_total):
    print(f"  {tt:20} correct/total = {tt_correct[tt]}/{tt_total[tt]}   used={tt_used[tt]}")

print("\n=== 手工 vs PDF ===")
manual = [c for c in cases if not str(c.get("id", "")).startswith("pdf-")]
pdf = [c for c in cases if str(c.get("id", "")).startswith("pdf-")]
man_gw = sum(1 for c in manual if c.get("gatewayCorrect"))
man_pure = sum(1 for c in manual if c.get("pureLlmCorrect"))
pdf_gw = sum(1 for c in pdf if c.get("gatewayCorrect"))
pdf_pure = sum(1 for c in pdf if c.get("pureLlmCorrect"))
pdf_truth = sum(1 for c in pdf if c.get("gatewayTruthAdopted") and c.get("gatewayCorrect"))
print(f"  manual total={len(manual)}  gatewayCorrect={man_gw}  pureLlmCorrect={man_pure}")
print(f"  pdf    total={len(pdf)}  gatewayCorrect={pdf_gw}  pureLlmCorrect={pdf_pure}  truth={pdf_truth}")

# ion正确 / calc+eq介入 (match compare.txt historical definitions)
ion_correct = sum(1 for c in cases if c.get("taskType") == "ion_check" and c.get("gatewayTruthAdopted") and c.get("gatewayCorrect"))
calc_eq_used = sum(1 for c in cases if c.get("taskType") in ("calculation", "equilibrium") and c.get("gatewayUsedEngine"))
print(f"\n  ion正确 (ion_check truth&correct) = {ion_correct}")
print(f"  calc+eq介入 (calc/eq usedEngine) = {calc_eq_used}")

# percentages
n = len(cases)
gw_correct = sum(1 for c in cases if c.get("gatewayCorrect"))
print(f"\n  总体网关% = {gw_correct}/{n} = {gw_correct/n*100:.1f}")
print(f"  PDF网关% = {pdf_gw}/{len(pdf)} = {pdf_gw/len(pdf)*100:.1f}  (truth {pdf_truth}/{len(pdf)} = {pdf_truth/len(pdf)*100:.1f})")
