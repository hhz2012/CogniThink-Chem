# -*- coding: utf-8 -*-
"""after3: 提取 both-wrong 且引擎未介入 的 calc/eq 题目全文，找引擎扩展点。"""
import json, os
HERE = os.path.dirname(os.path.abspath(__file__))
d = json.load(open(os.path.join(HERE,"after3.json"),encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(d.get(p,[]) or [])

for tt in ("equilibrium","calculation"):
    sub=[r for r in rows if r.get("taskType")==tt and not r.get("gatewayCorrect") and not r.get("pureLlmCorrect") and not r.get("gatewayUsedEngine")]
    print(f"\n########## {tt}: both-wrong 未介入 n={len(sub)} ##########")
    for r in sub:
        print(f"\n--- [{r.get('id')}] ok={r.get('gatewayOk')} ---")
        print(f"Q: {(r.get('question') or '')}")
        print(f"期望: {r.get('expected')}")
        print(f"纯LLM: {(r.get('pureLlmAnswer') or '')[:90].replace(chr(10),' ')}")
