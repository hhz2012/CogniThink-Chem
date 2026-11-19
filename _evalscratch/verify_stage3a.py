# -*- coding: utf-8 -*-
import json, os
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
NEW = SCRATCH + r"\after_stage3a.json"
OLD = SCRATCH + r"\after_stage2d.json"

def load(p):
    with open(p, encoding="utf-8-sig") as f:
        return json.load(f)

def cases(data):
    out = {}
    for key in ("bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"):
        for c in data.get(key, []):
            c["_part"] = key
            out[c["id"]] = c
    return out

print("=== file check ===")
for p in (NEW, OLD):
    print(os.path.basename(p), "exists=", os.path.exists(p), "size=", os.path.getsize(p) if os.path.exists(p) else "-")

new = load(NEW); old = load(OLD)
cn = cases(new); co = cases(old)

print("\n=== top-line summary ===")
for k in ("totalCases","pureLlmCorrect","gatewayCorrect","gatewayTruthCorrect",
          "gatewayEngineUsed","engineSolved","engineCoverage","enginePrecision","harmCount"):
    print(f"  {k:22} stage2={old.get(k)!r:>10}  stage3a={new.get(k)!r:>10}")

print("\n=== pdf-089 (must flip False->True, answer contains balanced eq) ===")
o, n = co["pdf-089"], cn["pdf-089"]
print("  stage2 : correct=", o.get("gatewayCorrect"), "taskType=", o.get("taskType"),
      "usedEngine=", o.get("gatewayUsedEngine"), "adopted=", o.get("gatewayTruthAdopted"))
print("  stage3a: correct=", n.get("gatewayCorrect"), "taskType=", n.get("taskType"),
      "usedEngine=", n.get("gatewayUsedEngine"), "adopted=", n.get("gatewayTruthAdopted"))
print("  stage3a gatewayAnswer=", repr(n.get("gatewayAnswer"))[:200])
kw = "5NO2^- + 2MnO4^- + 6H^+ = 5NO3^- + 2Mn^2+ + 3H2O"
print("  answer contains expected keyword:", kw in (n.get("gatewayAnswer") or ""))

print("\n=== pdf-164 (must NOT be falsely adopted; no regression, no harm) ===")
o, n = co["pdf-164"], cn["pdf-164"]
print("  stage2 : correct=", o.get("gatewayCorrect"), "adopted=", o.get("gatewayTruthAdopted"), "pureLlmCorrect=", o.get("pureLlmCorrect"))
print("  stage3a: correct=", n.get("gatewayCorrect"), "adopted=", n.get("gatewayTruthAdopted"), "pureLlmCorrect=", n.get("pureLlmCorrect"),
      "taskType=", n.get("taskType"), "usedEngine=", n.get("gatewayUsedEngine"))
print("  stage3a gatewayAnswer=", repr(n.get("gatewayAnswer"))[:120])

print("\n=== regression: 56 stage2 correct must stay correct (lost=0) ===")
stage2_correct = {i for i,c in co.items() if c.get("gatewayCorrect")}
still = [i for i in stage2_correct if cn.get(i,{}).get("gatewayCorrect")]
lost = [i for i in stage2_correct if not cn.get(i,{}).get("gatewayCorrect")]
print("  stage2 correct count:", len(stage2_correct))
print("  still correct in stage3a:", len(still))
print("  LOST (regression):", lost)
gained = [i for i,c in cn.items() if c.get("gatewayCorrect") and not co.get(i,{}).get("gatewayCorrect")]
print("  GAINED (newly correct in stage3a):", gained)

print("\n=== balance taskType breakdown (stage3a) ===")
bal = [c for c in cn.values() if c.get("taskType") == "balance"]
print("  balance-classified count:", len(bal))
for c in sorted(bal, key=lambda x: x["id"]):
    print(f"    {c['id']:10} correct={c.get('gatewayCorrect')} adopted={c.get('gatewayTruthAdopted')} "
          f"usedEngine={c.get('gatewayUsedEngine')} part={c['_part']} ans={c.get('gatewayAnswer')[:50]!r}")

print("\n=== pdf-051 false-positive guard (must not flip due to balance介入) ===")
if "pdf-051" in co and "pdf-051" in cn:
    o, n = co["pdf-051"], cn["pdf-051"]
    print("  stage2 : taskType=", o.get("taskType"), "correct=", o.get("gatewayCorrect"),
          "usedEngine=", o.get("gatewayUsedEngine"), "adopted=", o.get("gatewayTruthAdopted"))
    print("  stage3a: taskType=", n.get("taskType"), "correct=", n.get("gatewayCorrect"),
          "usedEngine=", n.get("gatewayUsedEngine"), "adopted=", n.get("gatewayTruthAdopted"))
    print("  stage3a gatewayAnswer=", repr(n.get("gatewayAnswer"))[:160])
else:
    print("  pdf-051 not found")

print("\n=== harmCount / precision final ===")
print("  harmCount stage3a =", new.get("harmCount"), "(must be 0)")
print("  enginePrecision stage3a =", new.get("enginePrecision"))
print("  engineSolved stage3a =", new.get("engineSolved"))
