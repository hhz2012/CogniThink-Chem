# -*- coding: utf-8 -*-
import json, os
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
def load(p):
    with open(p, encoding="utf-8-sig") as f: return json.load(f)
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
def idx(d):
    m={}
    for p in parts:
        for c in d.get(p,[]): m[c["id"]]=c
    return m

files = {
 "stage2d": SCRATCH + r"\after_stage2d.json",
 "stage3a": SCRATCH + r"\after_stage3a.json",
 "stage3b": SCRATCH + r"\after_stage3b.json",
}
D = {k: load(v) for k,v in files.items() if os.path.exists(v)}
I = {k: idx(v) for k,v in D.items()}

qids = ["reaction-5","trap-react-1","trap-react-3"]
print("=== 三阶段对比 ===")
for qid in qids:
    print(f"\n----- {qid} -----")
    for k in ["stage2d","stage3a","stage3b"]:
        c = I.get(k,{}).get(qid)
        if not c: print(f"  {k}: MISSING"); continue
        print(f"  {k}: tt={c.get('taskType')} gwCorr={c.get('gatewayCorrect')} ok={c.get('gatewayOk')} used={c.get('gatewayUsedEngine')} adopt={c.get('gatewayTruthAdopted')} pureCorr={c.get('pureLlmCorrect')}")
        print(f"       concl={str(c.get('gatewayConclusion'))[:140]}")
        print(f"       gwAns={str(c.get('gatewayAnswer'))[:140]}")

# stage2d reaction_check correct set
print("\n=== stage2d reaction_check 正确列表 ===")
rc = [c for c in I["stage2d"].values() if c.get("taskType")=="reaction_check" and c.get("gatewayCorrect")]
print(f"stage2d reaction_check correct = {len(rc)}: {sorted(c['id'] for c in rc)}")

# stage3b reaction_check
rc3b = [c for c in I["stage3b"].values() if c.get("taskType")=="reaction_check" and c.get("gatewayCorrect")]
print(f"stage3b reaction_check correct = {len(rc3b)}: {sorted(c['id'] for c in rc3b)}")
