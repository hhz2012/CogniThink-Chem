# -*- coding: utf-8 -*-
"""Compare after_stage4.json (baseline) against a fresh tier2 run.
Proves: zero regression on previously-engine-correct cases, no NEW harm from
Tier 2 validators, and all new t2c/t2t/t2n questions solved correctly.

The four per-case arrays partition all cases (bothCorrect + gatewayOnlyCorrect
+ pureLlmOnlyCorrect + bothWrong == totalCases), so we can reconstruct the full
per-case engine outcome.
"""
import json, sys
ROOT = r"d:\github\CogniThink-Chem\_evalscratch"
BASE = ROOT + r"\after_stage4.json"
NEW  = ROOT + r"\after_tier2.json"

def load(p):
    return json.load(open(p, encoding="utf-8"))

def per_case(d):
    out = {}
    for cat in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        for c in d.get(cat, []):
            cid = c.get("id")
            if not cid: continue
            out[cid] = {
                "cat": cat,
                "gwCorrect": c.get("gatewayCorrect"),
                "gwUsedEngine": c.get("gatewayUsedEngine"),
                "gwTruthAdopted": c.get("gwTruthAdopted") or c.get("gatewayTruthAdopted"),
                "gwAnswer": c.get("gatewayAnswer"),
                "taskType": c.get("taskType"),
            }
    return out

def main():
    base = load(BASE); new = load(NEW)
    bc = per_case(base); nc = per_case(new)
    print(f"baseline totalCases={base.get('totalCases')}  new totalCases={new.get('totalCases')}")
    print(f"baseline engineSolved={base.get('engineSolved')} harm={base.get('harmCount')} prec={base.get('enginePrecision')}")
    print(f"new      engineSolved={new.get('engineSolved')} harm={new.get('harmCount')} prec={new.get('enginePrecision')}")

    # 1) previously engine-correct must remain correct (no regression)
    base_correct = {i for i,v in bc.items() if v["gwUsedEngine"] and v["gwCorrect"]}
    regressed = []
    for i in base_correct:
        v = nc.get(i)
        if v is None or not (v["gwUsedEngine"] and v["gwCorrect"]):
            regressed.append(i)
    print(f"\n[regression] previously engine-correct={len(base_correct)}, now regressed/missing={len(regressed)}")
    for i in regressed[:20]: print("   ", i, bc[i], "->", nc.get(i))

    # 2) no NEW harm: tier2 engine-harm ids minus baseline engine-harm ids must be empty
    def harm_ids(m): return {i for i,v in m.items() if v["gwUsedEngine"] and not v["gwCorrect"]}
    bh = harm_ids(bc); nh = harm_ids(nc)
    new_harm = nh - bh
    print(f"[harm] baseline harm={len(bh)}  tier2 harm={len(nh)}  NEW harm={len(new_harm)}")
    for i in sorted(new_harm): print("   NEW HARM:", i, nc[i])

    # 3) new tier2 questions all correct
    new_tier2 = [i for i in nc if i.startswith(("t2c-","t2t-","t2n-"))]
    wrong_tier2 = [i for i in new_tier2 if not (nc[i]["gwUsedEngine"] and nc[i]["gwCorrect"])]
    print(f"\n[tier2 new] count={len(new_tier2)}  engine-correct={len(new_tier2)-len(wrong_tier2)}  wrong/unresolved={len(wrong_tier2)}")
    for i in wrong_tier2: print("   ", i, nc[i])

    # 4) newly-solved pdf questions (engine-correct in tier2 but not in baseline)
    new_correct = {i for i,v in nc.items() if v["gwUsedEngine"] and v["gwCorrect"]}
    newly = new_correct - base_correct
    print(f"\n[newly solved] count={len(newly)} (incl. new tier2 + newly-solved pdf)")
    for i in sorted(newly)[:40]: print("   ", i)

    ok = (len(regressed)==0 and len(new_harm)==0 and len(wrong_tier2)==0)
    print("\nRESULT:", "PASS" if ok else "FAIL")
    sys.exit(0 if ok else 1)

if __name__ == "__main__":
    main()
