# -*- coding: utf-8 -*-
"""提取基线中 calculation/equilibrium 的错题，供引擎扩展选题。"""
import json
data = json.load(open(r"d:\github\CogniThink-Chem\_evalscratch\baseline.json", encoding="utf-8-sig"))
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows = []
for p in parts: rows += data.get(p,[]) or []

for tt in ("calculation","equilibrium"):
    sub = [r for r in rows if r.get("taskType")==tt and not r.get("gatewayCorrect")]
    engaged = [r for r in sub if r.get("gatewayUsedEngine")]
    print(f"\n===== {tt}: 网关答错 {len(sub)} 题，其中引擎介入 {len(engaged)} =====")
    for r in sub:
        print(f"  [{r.get('id')}] engine={r.get('gatewayUsedEngine')} ok={r.get('gatewayOk')}")
        print(f"    Q: {(r.get('question') or '')[:110]}")
        print(f"    期望: {r.get('expected')}")
        ga = (r.get('gatewayAnswer') or '')[:70].replace("\n"," ")
        print(f"    网关: {ga}")
