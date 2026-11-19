# -*- coding: utf-8 -*-
import json
d=json.load(open(r"d:\github\CogniThink-Chem\_evalscratch\after5.json",encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(d.get(p,[]) or [])
m={r["id"]:r for r in rows}
for qid in ["pdf-022","pdf-023","pdf-108","pdf-410"]:
    r=m.get(qid)
    if not r: 
        print(qid,"NOT FOUND"); continue
    print("="*60); print(qid, "task=",r.get("taskType"),"gwOk=",r.get("gatewayOk"),"gwCorrect=",r.get("gatewayCorrect"),"pureCorrect=",r.get("pureLlmCorrect"))
    print("expected:", (r.get("expected") or "")[:80])
    print("Q[:200]:", (r.get("question") or "")[:200].replace(chr(10)," "))
