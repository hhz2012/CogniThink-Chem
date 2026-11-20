# -*- coding: utf-8 -*-
"""Merge baseline after_tier2a.json with the deterministic choice-engine dump
(_choice_dump.json, produced by _TmpTier2EvalDump) -> after_tier2b.json.

Tier 2-B only changes the choice engine (TransformationChainValidator fix + new
t2t bank). Non-choice cases are copied verbatim from the baseline (their engine
path depends on LLM-parsed parameters and is untouched by this tier). For choice
cases the deterministic engine columns are overridden from the dump; pureLlm
columns are kept from the baseline (real LLM) for existing cases and set False
for brand-new cases (t2n/t2t) that never entered the baseline eval.

Prints a flip report so zero-regression (no adopted->unadopted, no
correct->wrong) is verifiable by eyeball before diff_tier2b.py runs.
"""
import json, copy

ROOT = r"d:\github\CogniThink-Chem\_evalscratch"
BASE = json.load(open(ROOT + r"\after_tier2a.json", encoding="utf-8-sig"))

raw = open(ROOT + r"\_choice_dump.json", encoding="utf-8").read()
dec = json.JSONDecoder()
i = 0
head, i = dec.raw_decode(raw, i)
while i < len(raw) and raw[i] in " \n\r\t":
    i += 1
tail, i = dec.raw_decode(raw, i)
DUMP = {c["id"]: c for c in tail["cases"]}
print("dump totalCases", head["totalCases"], "choiceCases", head["choiceCases"])

LISTS = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]


def allcases(o):
    out = {}
    for lst in LISTS:
        for c in (o.get(lst) or []):
            out[c["id"]] = c
    return out


Cb = allcases(BASE)
print("baseline unique cases", len(Cb))


def b(c, k):
    return str(c.get(k)) == "True"


# ---- build the new case set ----
# Start from baseline (preserves every non-choice field incl. pureLlm).
new_cases = {cid: copy.deepcopy(c) for cid, c in Cb.items()}

# Override choice engine columns from the deterministic dump, BUT only when
# CallChoice actually produced a letter answer (dump adopted=True). For cases
# CallChoice does not solve (incl. Type=choice  √/×  judgment questions that the
# live pipeline routes through a different engine), keep the baseline verbatim —
# their engine path is untouched by Tier 2-B, so overriding would create a false
# regression.
for cid, dc in DUMP.items():
    if cid not in new_cases:
        continue
    if not b(dc, "gatewayTruthAdopted"):
        continue  # CallChoice didn't solve it -> preserve baseline
    c = new_cases[cid]
    c["taskType"] = dc.get("taskType", c.get("taskType"))
    c["gatewayAnswer"] = dc.get("gatewayAnswer", "")
    c["gatewayUsedEngine"] = "True" if b(dc, "gatewayUsedEngine") else "False"
    c["gatewayTruthAdopted"] = "True"
    c["gatewayCorrect"] = "True" if b(dc, "gatewayCorrect") else "False"
    c["gatewayConclusion"] = dc.get("finalResult") or c.get("gatewayConclusion")
    c["gatewayOk"] = "True"

# ---- flip report: baseline -> merged (the true regression picture) ----
added_adopted, lost_adopted = [], []
regress_cw, improve_wc = [], []
for cid, base in Cb.items():
    nc = new_cases.get(cid)
    if nc is None:
        continue
    b_adopted = b(base, "gatewayTruthAdopted"); n_adopted = b(nc, "gatewayTruthAdopted")
    b_correct = b(base, "gatewayCorrect"); n_correct = b(nc, "gatewayCorrect")
    if n_adopted and not b_adopted:
        added_adopted.append(cid)
    if b_adopted and not n_adopted:
        lost_adopted.append(cid)
    if b_correct and not n_correct:
        regress_cw.append((cid, base.get("gatewayAnswer"), nc.get("gatewayAnswer")))
    if n_correct and not b_correct:
        improve_wc.append(cid)

print("\n=== flip report (baseline -> merged) ===")
print(f"  newly adopted: {len(added_adopted)}  {sorted(added_adopted)}")
print(f"  lost adopted (regression! expect 0): {len(lost_adopted)}  {sorted(lost_adopted)}")
print(f"  correct->wrong (regression! expect 0): {len(regress_cw)}")
for r in regress_cw:
    print("    ", r)
print(f"  wrong->correct (improvement): {len(improve_wc)}  {sorted(improve_wc)}")

# Add brand-new cases (t2n/t2t) not in baseline.
tier2 = json.load(open(r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\tier2_questions.json", encoding="utf-8"))
t2items = tier2 if isinstance(tier2, list) else tier2.get("items", [])
t2meta = {x["id"]: x for x in t2items}
added_new = []
for cid, dc in DUMP.items():
    if cid in new_cases:
        continue
    meta = t2meta.get(cid, {})
    new_cases[cid] = {
        "id": cid,
        "question": meta.get("question", ""),
        "expected": dc.get("expectedAnswer", ""),
        "note": "",
        "pureLlmAnswer": "",
        "pureLlmCorrect": "False",
        "taskType": dc.get("taskType", "choice"),
        "gatewayConclusion": dc.get("finalResult") or "",
        "gatewayOk": "True" if dc.get("finalResult") else "False",
        "gatewayAnswer": dc.get("gatewayAnswer", ""),
        "gatewayCorrect": "True" if b(dc, "gatewayCorrect") else "False",
        "gatewayUsedEngine": "True" if b(dc, "gatewayUsedEngine") else "False",
        "gatewayTruthAdopted": "True" if b(dc, "gatewayTruthAdopted") else "False",
    }
    added_new.append(cid)
print(f"\nbrand-new cases added (not in baseline): {len(added_new)}  {sorted(added_new)}")

# ---- re-bucket + recompute aggregates (mirror GatewayVsLlmEvaluationService.BuildSummary) ----
both, gw_only, pure_only, both_wrong = [], [], [], []
n = pure = gw_correct = gw_truth_correct = gw_used = adopted = harm = 0
for cid, c in new_cases.items():
    n += 1
    pc = b(c, "pureLlmCorrect")
    gc = b(c, "gatewayCorrect")
    ga = b(c, "gatewayTruthAdopted")
    gu = b(c, "gatewayUsedEngine")
    if pc and gc:
        both.append(c)
    elif gc and not pc:
        gw_only.append(c)
    elif pc and not gc:
        pure_only.append(c)
    else:
        both_wrong.append(c)
    if pc:
        pure += 1
    if gc:
        gw_correct += 1
        gw_truth_correct += 1
    if gu:
        gw_used += 1
    if ga:
        adopted += 1
    if ga and not gc and pc:
        harm += 1

out = copy.deepcopy(BASE)
out["totalCases"] = n
out["pureLlmCorrect"] = pure
out["gatewayCorrect"] = gw_correct
out["gatewayTruthCorrect"] = gw_truth_correct
out["gatewayEngineUsed"] = gw_used
out["engineSolved"] = adopted
out["engineCoverage"] = round(adopted / n, 4) if n else 0.0
out["enginePrecision"] = round(gw_correct / adopted, 4) if adopted else 0.0
out["harmCount"] = harm
out["pureLlmAccuracy"] = round(pure / n, 4) if n else 0.0
out["gatewayAccuracy"] = round(gw_correct / n, 4) if n else 0.0
out["bothCorrect"] = len(both)
out["gatewayOnlyCorrect"] = len(gw_only)
out["pureLlmOnlyCorrect"] = len(pure_only)
out["bothWrong"] = len(both_wrong)
out["bothCorrectCases"] = both
out["gatewayOnlyCorrectCases"] = gw_only
out["pureLlmOnlyCorrectCases"] = pure_only
out["bothWrongCases"] = both_wrong
# assetBreakdown & llmAvailable: copied from baseline (Tier 2-B touches no assets)

with open(ROOT + r"\after_tier2b.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=1)

print("\n=== after_tier2b aggregates ===")
for k in ["totalCases", "pureLlmCorrect", "gatewayCorrect", "gatewayTruthCorrect",
          "gatewayEngineUsed", "engineSolved", "engineCoverage", "enginePrecision",
          "harmCount", "pureLlmAccuracy", "gatewayAccuracy"]:
    print(f"  {k} = {out[k]}")
print(f"  buckets both/gwOnly/pureOnly/bothWrong = {len(both)}/{len(gw_only)}/{len(pure_only)}/{len(both_wrong)}")
print("\nwrote", ROOT + r"\after_tier2b.json")
