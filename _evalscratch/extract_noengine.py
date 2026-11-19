# -*- coding: utf-8 -*-
"""提取 calc/eq 中引擎未介入(Ok=null) 且纯LLM答对的题——候选扩展点。"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
data = json.load(open(os.path.join(HERE, "after.json"), encoding="utf-8-sig"))
parts = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]
cases = []
for p in parts: cases.extend(data.get(p, []) or [])

for task in ["calculation", "equilibrium"]:
    rows = [r for r in cases if r.get("taskType") == task and not r.get("gatewayUsedEngine") and r.get("pureLlmCorrect")]
    print(f"\n===== {task}: 未介入且纯LLM答对 (n={len(rows)}) =====")
    for r in rows[:40]:
        q = (r.get("question") or "").replace("\n", " ")[:110]
        print(f"[{r.get('id')}] 期望={(r.get('expected') or '')[:50]}")
        print(f"    Q: {q}")
