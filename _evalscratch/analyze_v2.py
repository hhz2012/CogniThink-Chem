# -*- coding: utf-8 -*-
import json
EVAL = r"d:\github\CogniThink-Chem\_evalscratch"
def load(p): return json.load(open(EVAL + "\\" + p, encoding="utf-8"))
rows = load("engine_dump_v2.json")
b6 = load("after_stage6_concept.json")
tier2 = load("after_tier2.json")
def cmap(s):
    m = {}
    for cat in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        for c in s.get(cat, []): m[c["id"]] = c
    return m
bm, tm = cmap(b6), cmap(tier2)
solved = correct = harm = 0
losses, harms = [], []
for r in rows:
    na = bool(r.get("newAdopted")); nc = bool(r.get("newCorrect"))
    if na: solved += 1
    if nc: correct += 1
    plc = bool(bm.get(r["id"], {}).get("pureLlmCorrect"))
    if na and not nc and plc:
        harm += 1; harms.append(r["id"])
    if r.get("delta") == "LOSS": losses.append(r["id"])
prec = (solved - harm) / solved if solved else 1.0
print("=== v2 (修复后 + 方法学修正) vs after_stage6_concept ===")
print("total=%d  solved=%d  correct=%d  harm=%d  precision=%.4f" % (len(rows), solved, correct, harm, prec))
print("baseline stage6: solved=%d correct=%d harm=%d prec=%s" % (b6["engineSolved"], b6["gatewayCorrect"], b6["harmCount"], b6["enginePrecision"]))
print("baseline tier2 : solved=%d correct=%d harm=%d prec=%s" % (tier2["engineSolved"], tier2["gatewayCorrect"], tier2["harmCount"], tier2["enginePrecision"]))
print("delta vs stage6: solved +%d  correct +%d  harm %+d" % (solved - b6["engineSolved"], correct - b6["gatewayCorrect"], harm - b6["harmCount"]))
print("delta vs tier2 : solved +%d  correct +%d  harm %+d" % (solved - tier2["engineSolved"], correct - tier2["gatewayCorrect"], harm - tier2["harmCount"]))
print("LOSSES:", losses)
print("HARMS:", harms)
for cid in losses:
    r = [x for x in rows if x["id"] == cid][0]
    b = bm.get(cid, {})
    print("---", cid, "---")
    print("  ", {k: r.get(k) for k in ["entryTaskType","baselineTaskType","newTaskType","baselineCorrect","newCorrect","baselineAdopted","newAdopted","delta","threw"]})
    print("  finalResult:", repr((r.get("finalResult") or "")[:120]))
