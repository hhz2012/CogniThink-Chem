# -*- coding: utf-8 -*-
"""提取 after.json 中 ion_check / equilibrium / calculation 的明细，用于设计门控与引擎扩展。"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
data = json.load(open(os.path.join(HERE, "after.json"), encoding="utf-8-sig"))
parts = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]
cases = []
for p in parts:
    cases.extend(data.get(p, []) or [])

# 打印一条 case 的所有字段，确认字段名
print("CASE KEYS:", sorted(cases[0].keys()))
print()

def has_options(q):
    if not q: return False
    return bool(re.search(r'(^|\n)\s*[A-D][\.．、]\s', q))

for task in ["ion_check", "equilibrium", "calculation"]:
    rows = [r for r in cases if r.get("taskType") == task]
    print(f"\n########## {task} (n={len(rows)}) ##########")
    for r in rows:
        q = r.get("question") or ""
        qone = q.replace("\n", " ")[:70]
        concl = (r.get("gatewayConclusion") or "") or ("(无结论字段)" if "gatewayConclusion" not in r else "")
        # try alternate field
        if "gatewayConclusion" not in r:
            concl = r.get("gatewayOk")  # placeholder
        ga = (r.get("gatewayAnswer") or "").replace("\n", " ")[:55]
        choice = has_options(q)
        mark = []
        if r.get("pureLlmCorrect"): mark.append("纯对")
        if r.get("gatewayCorrect"): mark.append("网对")
        if r.get("gatewayUsedEngine"): mark.append("引擎介入")
        else: mark.append("未介入")
        print(f"[{r.get('id')}] {'/'.join(mark)} choice={choice}")
        print(f"    Q: {qone}")
        print(f"    期望: {(r.get('expected') or '')[:60]}")
        if "gatewayConclusion" in r:
            print(f"    引擎结论: {(r.get('gatewayConclusion') or '')[:60]}")
        print(f"    网关答: {ga}")
