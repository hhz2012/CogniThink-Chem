# -*- coding: utf-8 -*-
import json
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
with open(SCRATCH + r"\after_stage3b.json", encoding="utf-8-sig") as f:
    data = json.load(f)
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
m={}
for p in parts:
    for c in data.get(p,[]): m[c["id"]]=c
for qid in ["reaction-5","trap-react-1","trap-react-3","pdf-110"]:
    c=m.get(qid)
    print("="*70)
    print(qid, "taskType=", c.get("taskType"), "expected=", str(c.get("expected"))[:60])
    print("QUESTION:")
    print(c.get("question"))
