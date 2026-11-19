import json
BASE=r"D:\github\CogniThink-Chem\_evalscratch\\"
with open(BASE+"after_stage2.json",encoding="utf-8-sig") as f:
    data=json.load(f)
flat={}
for k,v in data.items():
    if isinstance(v,list):
        for c in v:
            if c.get("id") is not None: flat[c["id"]]=c
for cid in ["pdf-054","pdf-055","pdf-351","pdf-361","pdf-331","pdf-360","pdf-387"]:
    c=flat.get(cid)
    print("="*70)
    print(cid, "taskType=",c.get("taskType"),"gatewayCorrect=",c.get("gatewayCorrect"),"truthAdopted=",c.get("gatewayTruthAdopted"))
    print(repr(c.get("question")))
