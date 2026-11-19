# -*- coding: utf-8 -*-
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
data = json.load(open(os.path.join(HERE, "after.json"), encoding="utf-8-sig"))
parts = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]
cases = []
for p in parts: cases.extend(data.get(p, []) or [])

manual = [r for r in cases if not (r.get("id") or "").startswith("pdf-")]
print(f"手工题总数: {len(manual)}  网关对={sum(1 for r in manual if r.get('gatewayCorrect'))}  纯对={sum(1 for r in manual if r.get('pureLlmCorrect'))}")
print()
for task in ["ion_check","equilibrium","calculation"]:
    rows = [r for r in manual if r.get("taskType")==task]
    print(f"--- 手工 {task} (n={len(rows)}) ---")
    for r in rows:
        choice = bool(re.search(r'(^|\n)\s*[A-D][\.．、]\s', r.get("question") or ""))
        print(f"  [{r.get('id')}] choice={choice} 网对={r.get('gatewayCorrect')} 纯对={r.get('pureLlmCorrect')} 介入={r.get('gatewayUsedEngine')} 结论={(r.get('gatewayConclusion') or '')[:34]!r}")
