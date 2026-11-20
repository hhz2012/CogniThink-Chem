# -*- coding: utf-8 -*-
"""Analyze engine_dump.json vs baselines to verify Tier3 (T1/T2/T3) engine gains:
zero regression, zero new harm, 100% precision, and target-question coverage."""
import json

ROOT = r"d:\github\CogniThink-Chem"
EVAL = ROOT + r"\_evalscratch"

def load(p):
    return json.load(open(EVAL + "\\" + p, encoding="utf-8"))

rows = load("engine_dump.json")
base6 = load("after_stage6_concept.json")   # immediate baseline (stage6 concept done)
tier2 = load("after_tier2.json")             # pre-stage6 baseline

def case_map(snap):
    m = {}
    for cat in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        for c in snap.get(cat, []):
            m[c["id"]] = c
    return m

b6 = case_map(base6)
t2 = case_map(tier2)

# T1/T2/T3 target ids
def ids(path):
    return {q["id"] for q in load("task_inputs\\" + path)}
T1, T2, T3 = ids("T1.json"), ids("T2.json"), ids("T3.json")
targets = T1 | T2 | T3

# ---- current-code aggregates from engine_dump ----
n = len(rows)
solved_now = correct_now = harm_now = 0
gains = losses = unchanged = exc = 0
gain_rows, loss_rows = [], []
threw_rows = []
for r in rows:
    na = bool(r.get("newAdopted"))
    nc = bool(r.get("newCorrect"))
    if na: solved_now += 1
    if nc: correct_now += 1
    # harm: engine adopted a truth that is wrong, while pure LLM would have been right
    plc = bool(b6.get(r["id"], {}).get("pureLlmCorrect"))
    if na and not nc and plc:
        harm_now += 1
    d = r.get("delta")
    if d == "GAIN":
        gains += 1; gain_rows.append(r)
    elif d == "LOSS":
        losses += 1; loss_rows.append(r)
    elif d == "unchanged":
        unchanged += 1
    if r.get("threw"):
        exc += 1; threw_rows.append(r)

precision = (solved_now - harm_now) / solved_now if solved_now else 1.0

print("="*70)
print("CURRENT CODE (engine_dump.json) vs after_stage6_concept baseline")
print("="*70)
print(f"total={n}  gains={gains}  losses={losses}  unchanged={unchanged}  threw(exceptions)={exc}")
print(f"engineSolved_now={solved_now}  correct_now={correct_now}  harm_now={harm_now}  precision={precision:.4f}")
print(f"baseline stage6_concept: engineSolved={base6['engineSolved']} correct={base6['gatewayCorrect']} harm={base6['harmCount']} prec={base6['enginePrecision']}")
print(f"delta vs stage6_concept:  solved +{solved_now-base6['engineSolved']}  correct +{correct_now-base6['gatewayCorrect']}  harm {harm_now-base6['harmCount']:+d}")
print()
print("baseline tier2:           engineSolved=%d correct=%d harm=%d prec=%s" % (
    tier2['engineSolved'], tier2['gatewayCorrect'], tier2['harmCount'], tier2['enginePrecision']))
print(f"delta vs tier2 (stage6+T123 total): solved +{solved_now-tier2['engineSolved']}  correct +{correct_now-tier2['gatewayCorrect']}  harm {harm_now-tier2['harmCount']:+d}")

# ---- LOSS detail ----
print()
print("="*70)
print("LOSS DETAIL (baseline correct -> current wrong)  [%d]" % losses)
print("="*70)
for r in loss_rows:
    cid = r["id"]
    b = b6.get(cid, {})
    t2c = t2.get(cid, {})
    tag = []
    if cid in T1: tag.append("T1")
    if cid in T2: tag.append("T2")
    if cid in T3: tag.append("T3")
    ttype = r.get("newTaskType") or b.get("taskType") or "?"
    # classify regression type
    if r.get("newAdopted") and not r.get("newCorrect"):
        rtype = "ADOPTED_WRONG (false-positive / harm-candidate)"
    elif (not r.get("newAdopted")) and r.get("baselineAdopted"):
        rtype = "ADOPT_LOST (engine no longer adopts)"
    else:
        rtype = "other"
    fr = (r.get("finalResult") or "")[:60].replace("\n"," ")
    print(f"  {cid:14s} tt={ttype:18s} {rtype}")
    print(f"        baseCorr={r.get('baselineCorrect')} newCorr={r.get('newCorrect')} baseAdopt={r.get('baselineAdopted')} newAdopt={r.get('newAdopted')} threw={r.get('threw')} pureLlm={b.get('pureLlmCorrect')} [{','.join(tag) or '-'}]")
    print(f"        finalResult='{fr}'")

# ---- GAIN coverage vs T1/T2/T3 targets ----
print()
print("="*70)
print("GAIN COVERAGE vs T1/T2/T3 targets  [%d gains]" % gains)
print("="*70)
gain_ids = {r["id"] for r in gain_rows}
def cov(name, s):
    hit = gain_ids & s
    print(f"  {name}: target={len(s)}  gained={len(hit)}  miss={len(s-hit)}")
    if s - hit:
        print(f"      miss_ids={sorted(s-hit)}")
cov("T1", T1); cov("T2", T2); cov("T3", T3)
# gains NOT in any target (extra / unexpected)
extra = gain_ids - targets
print(f"  gains outside T1/T2/T3 targets: {len(extra)}  {sorted(extra)}")

# ---- exceptions: which taskTypes throw ----
print()
print("="*70)
print("THREW/EXCEPTIONS breakdown (preserved baseline, no delta)  [%d]" % exc)
print("="*70)
from collections import Counter
tc = Counter(r.get("newTaskType") or r.get("baselineTaskType") or "?" for r in threw_rows)
for k,v in tc.most_common():
    print(f"  {k:20s} {v}")

# ---- sanity: every baseline-correct case must stay correct OR be a documented loss ----
base_correct_ids = {cid for cid,c in b6.items() if c.get("gatewayCorrect")}
now_correct_ids = {r["id"] for r in rows if r.get("newCorrect")}
regressed = base_correct_ids - now_correct_ids
print()
print("baseline-correct count:", len(base_correct_ids), " still-correct:", len(base_correct_ids & now_correct_ids), " regressed:", len(regressed))
print("regressed ids (should equal LOSS_IDS):", sorted(regressed))

# write compact result
out = {
    "total": n, "gains": gains, "losses": losses, "unchanged": unchanged, "exceptions": exc,
    "engineSolved_now": solved_now, "correct_now": correct_now, "harm_now": harm_now,
    "precision": round(precision,4),
    "baseline_stage6_concept": {"engineSolved": base6['engineSolved'], "correct": base6['gatewayCorrect'], "harm": base6['harmCount']},
    "baseline_tier2": {"engineSolved": tier2['engineSolved'], "correct": tier2['gatewayCorrect'], "harm": tier2['harmCount']},
    "delta_vs_stage6": {"solved": solved_now-base6['engineSolved'], "correct": correct_now-base6['gatewayCorrect'], "harm": harm_now-base6['harmCount']},
    "delta_vs_tier2": {"solved": solved_now-tier2['engineSolved'], "correct": correct_now-tier2['gatewayCorrect'], "harm": harm_now-tier2['harmCount']},
    "loss_ids": sorted(regressed),
    "gain_ids": sorted(gain_ids),
    "T1": {"target": len(T1), "gained": len(gain_ids & T1)},
    "T2": {"target": len(T2), "gained": len(gain_ids & T2)},
    "T3": {"target": len(T3), "gained": len(gain_ids & T3)},
    "gains_outside_targets": sorted(extra),
}
json.dump(out, open(EVAL + "\\analyze_tier3_result.json", "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("\nWROTE analyze_tier3_result.json")
