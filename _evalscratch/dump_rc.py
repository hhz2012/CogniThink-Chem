# -*- coding: utf-8 -*-
import json
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
with open(SCRATCH + r"\after_stage3b.json", encoding="utf-8-sig") as f:
    data = json.load(f)
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
m={}
for p in parts:
    for c in data.get(p,[]):
        if c.get("taskType")=="reaction_check": m[c["id"]]=c
for qid in sorted(m):
    c=m[qid]
    print(f"{qid:14} corr={c.get('gatewayCorrect')} exp={str(c.get('expected'))[:30]:30} | {c.get('question')}")
