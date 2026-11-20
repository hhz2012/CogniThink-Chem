# -*- coding: utf-8 -*-
"""隔离 AC-18（Tier 2-C / NA 校验器）的评估产物：after_tier2a.json（Tier 2-A 基线）
+ _na_dump.json（仅 NaChoiceValidator 采纳的题）-> after_tier2c.json。

与 merge_tier2b.py 同构，但只覆盖「NaChoiceValidator 采纳」的题（实测仅 20 道 t2n，
不触碰任何基线题），从而把 Tier 2-C 的净增从 Tier 2-B 中干净分离出来。
非 NA 题与 pureLlm 列原样复用基线（Tier 2-C 为加法性改动，非 choice 路径不变）。
"""
import json, copy

ROOT = r"d:\github\CogniThink-Chem\_evalscratch"
BASE = json.load(open(ROOT + r"\after_tier2a.json", encoding="utf-8-sig"))

raw = open(ROOT + r"\_na_dump.json", encoding="utf-8").read()
dec = json.JSONDecoder()
i = 0
head, i = dec.raw_decode(raw, i)
while i < len(raw) and raw[i] in " \n\r\t":
    i += 1
tail, i = dec.raw_decode(raw, i)
DUMP = {c["id"]: c for c in tail["cases"]}
print("dump head", head)

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
    return c.get(k) is True or str(c.get(k)) == "True"


new_cases = {cid: copy.deepcopy(c) for cid, c in Cb.items()}

# 覆盖：仅当 NaChoice 采纳时（实测仅 t2n；基线无 t2n，故对现有题实际不触发）
for cid, dc in DUMP.items():
    if cid not in new_cases:
        continue
    if not b(dc, "gatewayTruthAdopted"):
        continue
    c = new_cases[cid]
    c["taskType"] = "choice"
    c["gatewayAnswer"] = dc.get("finalResult") or c.get("gatewayAnswer")
    c["gatewayUsedEngine"] = "True"
    c["gatewayTruthAdopted"] = "True"
    c["gatewayCorrect"] = "True" if b(dc, "gatewayCorrect") else "False"
    c["gatewayConclusion"] = dc.get("finalResult") or c.get("gatewayConclusion")
    c["gatewayOk"] = "True"

# flip 报告（现有题：基线 vs 合并）
added, lost, regress, improve = [], [], [], []
for cid, base in Cb.items():
    nc = new_cases[cid]
    ba, na = b(base, "gatewayTruthAdopted"), b(nc, "gatewayTruthAdopted")
    bc, nc_ = b(base, "gatewayCorrect"), b(nc, "gatewayCorrect")
    if na and not ba: added.append(cid)
    if ba and not na: lost.append(cid)
    if bc and not nc_: regress.append(cid)
    if nc_ and not bc: improve.append(cid)
print("\n=== flip report (baseline -> merged, existing cases only) ===")
print(f"  newly adopted (expect 0, NA 不碰基线题): {len(added)} {added}")
print(f"  lost adopted (expect 0): {len(lost)} {lost}")
print(f"  correct->wrong (expect 0): {len(regress)} {regress}")
print(f"  wrong->correct: {len(improve)} {improve}")

# 新增题：仅 t2n（基线没有的）
tier2 = json.load(open(r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\tier2_questions.json", encoding="utf-8"))
t2items = tier2 if isinstance(tier2, list) else tier2.get("items", [])
t2meta = {x["id"]: x for x in t2items}
added_new = []
for cid, dc in DUMP.items():
    if cid in new_cases:
        continue
    if not b(dc, "gatewayTruthAdopted"):
        continue  # 仅纳入 NA 采纳的新题（隔离 AC-18：t2t 等非 NA 题不进入本快照）
    meta = t2meta.get(cid, {})
    new_cases[cid] = {
        "id": cid,
        "question": meta.get("question", ""),
        "expected": dc.get("expectedAnswer", ""),
        "note": "",
        "pureLlmAnswer": "",
        "pureLlmCorrect": "False",
        "taskType": "choice",
        "gatewayConclusion": dc.get("finalResult") or "",
        "gatewayOk": "True",
        "gatewayAnswer": dc.get("finalResult") or "",
        "gatewayCorrect": "True" if b(dc, "gatewayCorrect") else "False",
        "gatewayUsedEngine": "True",
        "gatewayTruthAdopted": "True" if b(dc, "gatewayTruthAdopted") else "False",
    }
    added_new.append(cid)
print(f"\nbrand-new cases added (t2n): {len(added_new)} {sorted(added_new)}")

# 重算聚合（镜像 BuildSummary）
both, gw_only, pure_only, both_wrong = [], [], [], []
n = pure = gw_correct = gw_truth = gw_used = adopted = harm = 0
for cid, c in new_cases.items():
    n += 1
    pc, gc, ga, gu = b(c, "pureLlmCorrect"), b(c, "gatewayCorrect"), b(c, "gatewayTruthAdopted"), b(c, "gatewayUsedEngine")
    if pc and gc: both.append(c)
    elif gc and not pc: gw_only.append(c)
    elif pc and not gc: pure_only.append(c)
    else: both_wrong.append(c)
    pure += pc; gw_correct += gc; gw_truth += gc; gw_used += gu; adopted += ga
    if ga and not gc and pc: harm += 1

out = copy.deepcopy(BASE)
out["totalCases"] = n
out["pureLlmCorrect"] = pure
out["gatewayCorrect"] = gw_correct
out["gatewayTruthCorrect"] = gw_truth
out["gatewayEngineUsed"] = gw_used
out["engineSolved"] = adopted
out["engineCoverage"] = round(adopted / n, 4) if n else 0.0
out["enginePrecision"] = round(gw_correct / adopted, 4) if adopted else 0.0
out["harmCount"] = harm
out["pureLlmAccuracy"] = round(pure / n, 4) if n else 0.0
out["gatewayAccuracy"] = round(gw_correct / n, 4) if n else 0.0
out["bothCorrect"] = len(both); out["gatewayOnlyCorrect"] = len(gw_only)
out["pureLlmOnlyCorrect"] = len(pure_only); out["bothWrong"] = len(both_wrong)
out["bothCorrectCases"] = both; out["gatewayOnlyCorrectCases"] = gw_only
out["pureLlmOnlyCorrectCases"] = pure_only; out["bothWrongCases"] = both_wrong

with open(ROOT + r"\after_tier2c.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=1)

print("\n=== after_tier2c aggregates ===")
for k in ["totalCases", "pureLlmCorrect", "gatewayCorrect", "engineSolved",
          "engineCoverage", "enginePrecision", "harmCount", "pureLlmAccuracy", "gatewayAccuracy"]:
    print(f"  {k} = {out[k]}")
print(f"  buckets both/gwOnly/pureOnly/bothWrong = {len(both)}/{len(gw_only)}/{len(pure_only)}/{len(both_wrong)}")
print("\nwrote", ROOT + r"\after_tier2c.json")
