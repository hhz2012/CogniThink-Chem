# -*- coding: utf-8 -*-
import json
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
with open(SCRATCH + r"\after_stage2d.json", encoding="utf-8-sig") as f:
    data = json.load(f)

parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
cases = []
for p in parts:
    for c in data.get(p, []):
        c["_part"] = p
        cases.append(c)

bal = [c for c in cases if c.get("taskType") == "balance"]
print("total balance-classified:", len(bal))
adopted = [c for c in bal if c.get("gatewayTruthAdopted")]
print("balance truthAdopted (stage2):", len(adopted))
correct = [c for c in bal if c.get("gatewayCorrect")]
print("balance gatewayCorrect (stage2):", len(correct))
print("usedEngine True:", sum(1 for c in bal if c.get("gatewayUsedEngine")))
print("---- list balance cases ----")
for c in bal:
    print(f"{c['id']:12} part={c['_part']:24} correct={c.get('gatewayCorrect')} adopted={c.get('gatewayTruthAdopted')} usedEngine={c.get('gatewayUsedEngine')} ans={c.get('gatewayAnswer')[:40]!r}")
