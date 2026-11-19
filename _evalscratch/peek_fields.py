# -*- coding: utf-8 -*-
import json, os
HERE = os.path.dirname(os.path.abspath(__file__))
d = json.load(open(os.path.join(HERE,"after3.json"),encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(d.get(p,[]) or [])
# show keys of first case
print("KEYS:", sorted(rows[0].keys()))
print()
# show one choice case fully
for r in rows:
    exp=r.get("expected") or ""
    if exp.startswith("关键词: ") and exp[4:5] in "ABCD":
        print(json.dumps(r, ensure_ascii=False, indent=2)[:1500])
        break
