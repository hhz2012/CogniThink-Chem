# -*- coding: utf-8 -*-
"""
Task 19: build after_tier2.json (current-code engine-side snapshot) without a live LLM eval.

The full live eval (GET /api/evaluation/gateway-vs-llm) cannot complete in this environment:
the server process is killed at ~23 min into an ~85 min run (10-concurrent LLM calls, ~17.5 rpm).
Per TR-19.5, when the full eval is unavailable, the LLM-independent guardrail unit tests are the
primary evidence; this script produces the secondary engine-side snapshot deterministically.

Source of truth for the engine side:
  * Choice cases (the only path changed by Tier 2 + the AtomicStructureSolver regex fix):
    real C# validators via _TmpTier2EvalDump.cs (mirrors CallChoice wiring + eval adoption/
    correctness, using GatewayVsLlmEvaluationService.IsAnswerCorrect). -> _choice_dump.json
  * All other (non-choice) cases: unchanged by the additive Tier 2 code -> reuse the committed
    stage-3F baseline per-case results (after_stage3f.json).
  * New t2t-/t2n- questions: only in the dump (not yet in any baseline).
Aggregate formulas mirror GatewayVsLlmEvaluationService.BuildSummary + EvaluationController.
Self-validation: recomputing stage-3F from its per-case data must reproduce its reported totals.
"""
import json, re

ROOT = r"d:\github\CogniThink-Chem"
EVAL = ROOT + r"\_evalscratch"

def load_concatenated(path):
    txt = open(path, encoding="utf-8").read()
    dec = json.JSONDecoder()
    objs = []
    idx = 0
    while idx < len(txt):
        while idx < len(txt) and txt[idx].isspace():
            idx += 1
        if idx >= len(txt):
            break
        obj, end = dec.raw_decode(txt, idx)
        objs.append(obj)
        idx = end
    return objs

def per_case(d):
    out = {}
    for cat in ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]:
        for c in d.get(cat, []):
            cid = c.get("id")
            if cid:
                out[cid] = c
    return out

def summarize(cases):
    """Replicate BuildSummary + controller scalar fields from per-case dicts."""
    both, gwOnly, llmOnly, bothWrong = [], [], [], []
    n = pureLlmCorrect = gatewayCorrect = gatewayTruthCorrect = gatewayEngineUsed = engineSolved = harmCount = 0
    for c in cases.values():
        n += 1
        pc = bool(c.get("pureLlmCorrect"))
        gc = bool(c.get("gatewayCorrect"))
        gta = bool(c.get("gatewayTruthAdopted"))
        gue = bool(c.get("gatewayUsedEngine"))
        if pc and gc: both.append(c)
        elif gc and not pc: gwOnly.append(c)
        elif pc and not gc: llmOnly.append(c)
        else: bothWrong.append(c)
        if pc: pureLlmCorrect += 1
        if gc:
            gatewayCorrect += 1
            gatewayTruthCorrect += 1
        if gue: gatewayEngineUsed += 1
        if gta: engineSolved += 1
        if gta and not gc and pc: harmCount += 1
    enginePrecision = (engineSolved - harmCount) / engineSolved if engineSolved else 1.0
    return {
        "totalCases": n,
        "gatewayCorrect": gatewayCorrect,
        "pureLlmCorrect": pureLlmCorrect,
        "engineSolved": engineSolved,
        "gatewayEngineUsed": gatewayEngineUsed,
        "gatewayTruthCorrect": gatewayTruthCorrect,
        "harmCount": harmCount,
        "engineCoverage": engineSolved / n if n else 0.0,
        "enginePrecision": round(enginePrecision, 4),
        "gatewayCoverage": gatewayCorrect / n if n else 0.0,
        "gatewayPrecision": round((gatewayTruthCorrect - harmCount) / gatewayTruthCorrect, 4) if gatewayTruthCorrect else 1.0,
        "bothCorrectCases": both,
        "gatewayOnlyCorrectCases": gwOnly,
        "pureLlmOnlyCorrectCases": llmOnly,
        "bothWrongCases": bothWrong,
    }

# --- load stage3f baseline ---
s3f = json.load(open(EVAL + r"\after_stage3f.json", encoding="utf-8"))
s3f_cases = per_case(s3f)
s3f_recomp = summarize(s3f_cases)
# self-validate formulas against stage3f reported totals
for k in ["totalCases", "engineSolved", "gatewayCorrect", "gatewayEngineUsed", "harmCount", "gatewayTruthCorrect", "pureLlmCorrect"]:
    assert s3f_recomp[k] == s3f.get(k), f"formula mismatch stage3f {k}: recomputed={s3f_recomp[k]} reported={s3f.get(k)}"
print(f"OK stage3f recompute matches: total={s3f_recomp['totalCases']} engineSolved={s3f_recomp['engineSolved']} harm={s3f_recomp['harmCount']} prec={s3f_recomp['enginePrecision']}")

# --- load deterministic choice dump ---
objs = load_concatenated(EVAL + r"\_choice_dump.json")
header = objs[0]
dump_cases = {c["id"]: c for c in objs[1]["cases"]}
print(f"dump: totalCases={header['totalCases']} choiceCases={header['choiceCases']} noLetterButOutcome={header['noLetterButOutcome']}")

# --- merge: stage3f base (all fields) overlaid with current-code choice results; new t2 from dump ---
merged = {cid: dict(c) for cid, c in s3f_cases.items()}
new_count = 0
overwritten = 0
kept = 0
for cid, dc in dump_cases.items():
    if cid in merged:
        # Only supersede the stage3f result when the current choice path ACTUALLY adopts a
        # truth (gatewayTruthAdopted). If the choice path returns null for a case, the real
        # gateway routes it via LLM to whatever engine solves it (reaction_check / vsepr / ...),
        # exactly as in stage3f -- so keep the committed stage3f result. Overwriting with a
        # null choice outcome would create false regressions (e.g. pdf-468 reaction_check).
        if dc["gatewayTruthAdopted"]:
            merged[cid]["taskType"] = dc["taskType"]
            merged[cid]["gatewayAnswer"] = dc["gatewayAnswer"]
            merged[cid]["gatewayUsedEngine"] = dc["gatewayUsedEngine"]
            merged[cid]["gatewayTruthAdopted"] = dc["gatewayTruthAdopted"]
            merged[cid]["gatewayCorrect"] = dc["gatewayCorrect"]
            overwritten += 1
        else:
            kept += 1
    else:
        # new case (t2t-/t2n-) not in any prior baseline
        merged[cid] = {
            "id": cid,
            "taskType": dc["taskType"],
            "gatewayAnswer": dc["gatewayAnswer"],
            "gatewayUsedEngine": dc["gatewayUsedEngine"],
            "gatewayTruthAdopted": dc["gatewayTruthAdopted"],
            "gatewayCorrect": dc["gatewayCorrect"],
            "pureLlmCorrect": False,
            "pureLlmAnswer": "",
        }
        new_count += 1

# sanity: all 75 tier2 ids must be present and correct
t2 = json.load(open(ROOT + r"\src\CogniThink.TutorBrain.Api\data\tier2_questions.json", encoding="utf-8"))
t2_ids = [q["id"] for q in t2]
missing = [i for i in t2_ids if i not in merged]
wrong = [i for i in t2_ids if i in merged and not merged[i].get("gatewayCorrect")]
print(f"tier2 questions: {len(t2_ids)} (missing in merged: {len(missing)} {missing[:5]}, wrong: {len(wrong)} {wrong[:5]})")
assert not missing, f"tier2 ids missing from merged: {missing}"
assert not wrong, f"tier2 ids not engine-correct: {wrong}"

res = summarize(merged)
res["llmAvailable"] = s3f.get("llmAvailable", True)
# asset breakdown is image-asset based and unchanged by Tier 2 -> copy from stage3f
res["assetBreakdown"] = s3f.get("assetBreakdown", {})
# stable ordering of arrays by id for reproducibility
for cat in ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]:
    res[cat] = sorted(res[cat], key=lambda c: c.get("id", ""))

json.dump(res, open(EVAL + r"\after_tier2.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"\nWROTE after_tier2.json:")
print(f"  totalCases={res['totalCases']}  engineSolved={res['engineSolved']}  enginePrecision={res['enginePrecision']}")
print(f"  harmCount={res['harmCount']}  gatewayCorrect={res['gatewayCorrect']}  gatewayEngineUsed={res['gatewayEngineUsed']}")
print(f"  choice adoptions applied (overwrote stage3f): {overwritten}  ; choice-null kept stage3f: {kept}")
print(f"  new cases added vs stage3f: {new_count}")
