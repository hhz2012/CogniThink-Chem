# -*- coding: utf-8 -*-
import json
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
def load(p):
    with open(p, encoding="utf-8-sig") as f: return json.load(f)
a = load(SCRATCH + r"\after_stage3a.json")
b = load(SCRATCH + r"\after_stage3b.json")
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
def idx(d):
    m={}
    for p in parts:
        for c in d.get(p,[]): m[c["id"]]=c
    return m
A=idx(a); B=idx(b)

print("=== 回归题 stage3a -> stage3b ===")
for qid in ["reaction-5","trap-react-1","trap-react-3"]:
    ca=A.get(qid); cb=B.get(qid)
    print(f"\n--- {qid} ---")
    for tag,c in [("3a",ca),("3b",cb)]:
        if not c: print(f"  {tag}: MISSING"); continue
        print(f"  {tag}: taskType={c.get('taskType')} gwCorr={c.get('gatewayCorrect')} ok={c.get('gatewayOk')} usedEng={c.get('gatewayUsedEngine')} truthAdopt={c.get('gatewayTruthAdopted')}")
        print(f"       expected={str(c.get('expected'))[:80]}")
        print(f"       gwAnswer={str(c.get('gatewayAnswer'))[:120]}")
        print(f"       note={str(c.get('note'))[:120]}")

print("\n=== pdf-110 为何未介入 ===")
for tag,c in [("3a",A.get("pdf-110")),("3b",B.get("pdf-110"))]:
    if not c: print(f"  {tag}: MISSING"); continue
    print(f"  {tag}: taskType={c.get('taskType')} gwCorr={c.get('gatewayCorrect')} ok={c.get('gatewayOk')} usedEng={c.get('gatewayUsedEngine')} truthAdopt={c.get('gatewayTruthAdopted')}")
    print(f"       expected={str(c.get('expected'))[:80]}")
    print(f"       gwAnswer={str(c.get('gatewayAnswer'))[:160]}")
    print(f"       gwConclusion={str(c.get('gatewayConclusion'))[:200]}")
    print(f"       note={str(c.get('note'))[:160]}")
    print(f"       question(head)={str(c.get('question'))[:300]}")
