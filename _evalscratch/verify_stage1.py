# -*- coding: utf-8 -*-
"""阶段1 AC-9 验证：严格剔除口径落地检查。"""
import json, io, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

p = r"D:\github\CogniThink-Chem\_evalscratch\after_stage1.json"
with open(p, encoding="utf-8-sig") as f:
    data = json.load(f)

def norm(s): return "".join((s or "").split())

print("=== 顶层汇总字段 ===")
for k in ["totalCases","pureLlmCorrect","gatewayCorrect","gatewayTruthCorrect",
          "gatewayEngineUsed","engineSolved","engineCoverage","enginePrecision","harmCount","llmAvailable"]:
    print(f"  {k}: {data.get(k)}")

print("\n=== 分区数量 ===")
for k in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    print(f"  {k}: {len(data.get(k,[]))}")

cases = []
for key in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    for c in data.get(key, []):
        cases.append(c)
print(f"  合计 case: {len(cases)}")

# TR-9.1: gatewayCorrect 中回答==纯LLM回答 = 0
violations = []
for c in cases:
    if c.get("gatewayCorrect") and norm(c.get("gatewayAnswer")) == norm(c.get("pureLlmAnswer")) and (c.get("pureLlmAnswer") or ""):
        violations.append((c.get("id"), c.get("taskType")))
print(f"\n=== TR-9.1: gatewayCorrect 且 回答==纯LLM回答 ===")
print(f"  数量: {len(violations)}  (要求 = 0)")
for v in violations[:10]:
    print(f"    {v}")

# gatewayTruthCorrect 核算
gw_correct = sum(1 for c in cases if c.get("gatewayCorrect"))
gw_truth_field = data.get("gatewayTruthCorrect")
print(f"\n=== gatewayTruthCorrect 核算 ===")
print(f"  汇总字段 gatewayTruthCorrect: {gw_truth_field}")
print(f"  逐题 gatewayCorrect 求和: {gw_correct}")
print(f"  预期 ≈ 56 ± 5")

# engineSolved / precision 核算
solved = sum(1 for c in cases if c.get("gatewayTruthAdopted"))
print(f"\n=== 引擎覆盖 ===")
print(f"  汇总 engineSolved: {data.get('engineSolved')}  (逐题 gatewayTruthAdopted 求和: {solved})")
print(f"  汇总 engineCoverage: {data.get('engineCoverage')}")
print(f"  汇总 enginePrecision: {data.get('enginePrecision')}")
print(f"  汇总 harmCount(帮倒忙): {data.get('harmCount')}  (要求 = 0)")

# 逐题 harm 核算
harm = sum(1 for c in cases if c.get("gatewayTruthAdopted") and not c.get("gatewayCorrect") and c.get("pureLlmCorrect"))
print(f"  逐题核算帮倒忙: {harm}")

# gatewayTruthCorrect 按 taskType 分布（引擎真值解出的题）
from collections import Counter
print(f"\n=== 引擎真值解出且判正 按 taskType ===")
cnt = Counter(c.get("taskType") for c in cases if c.get("gatewayCorrect"))
for k,v in cnt.most_common():
    print(f"  {k}: {v}")

# 抽查：gatewayCorrect 但回答为空的（不应有）
empty_correct = sum(1 for c in cases if c.get("gatewayCorrect") and not (c.get("gatewayAnswer") or "").strip())
print(f"\n=== 抽查 ===")
print(f"  gatewayCorrect 但回答为空: {empty_correct} (应=0)")
print(f"  pureLlmCorrect 总数: {sum(1 for c in cases if c.get('pureLlmCorrect'))}")
