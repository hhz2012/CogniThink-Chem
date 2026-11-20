# -*- coding: utf-8 -*-
# Build after_tier3.json: patch after_stage6_concept.json with V3 (faithful real-eval) engine outcomes.
# Mirrors build_after_stage6_concept.ps1 metric formulas. Preserves pureLlmCorrect + all question fields.
import json, copy

REPO = r'D:\github\CogniThink-Chem'
base = json.load(open(REPO + r'\_evalscratch\after_stage6_concept.json', encoding='utf-8-sig'))
v3 = {r['id']: r for r in json.load(open(REPO + r'\_evalscratch\engine_dump_v3.json', encoding='utf-8-sig'))}

BUCKETS = ['bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases']

clone = copy.deepcopy(base)
patched = 0
missing_in_v3 = []
for b in BUCKETS:
    rows = clone.get(b) or []
    for r in rows:
        cid = r.get('id')
        v = v3.get(cid)
        if v is None:
            missing_in_v3.append(cid)
            continue
        r['taskType'] = v['newTaskType']
        r['gatewayConclusion'] = v['finalResult'] or ''
        r['gatewayAnswer'] = v['finalResult'] if v['newAdopted'] else ''
        r['gatewayUsedEngine'] = bool(v['newUsedEngine'])
        r['gatewayTruthAdopted'] = bool(v['newAdopted'])
        r['gatewayCorrect'] = bool(v['newCorrect'])
        r['gatewayOk'] = bool(v['newAdopted'])
        patched += 1

# re-bucket
buckets = {b: [] for b in BUCKETS}
for b in BUCKETS:
    for r in (clone.get(b) or []):
        pc = bool(r.get('pureLlmCorrect'))
        gc = bool(r.get('gatewayCorrect'))
        if pc and gc: buckets['bothCorrectCases'].append(r)
        elif (not pc) and gc: buckets['gatewayOnlyCorrectCases'].append(r)
        elif pc and (not gc): buckets['pureLlmOnlyCorrectCases'].append(r)
        else: buckets['bothWrongCases'].append(r)
for b in BUCKETS:
    clone[b] = buckets[b]

all_rows = []
for b in BUCKETS: all_rows += clone[b]
total = len(all_rows)
gwCorr = sum(1 for r in all_rows if r.get('gatewayCorrect'))
pCorr = sum(1 for r in all_rows if r.get('pureLlmCorrect'))
gwUsed = sum(1 for r in all_rows if r.get('gatewayUsedEngine'))
adopted = sum(1 for r in all_rows if r.get('gatewayTruthAdopted'))
harm = sum(1 for r in all_rows if r.get('gatewayTruthAdopted') and not r.get('gatewayCorrect') and r.get('pureLlmCorrect'))
engineSolved = adopted
precision = gwCorr / engineSolved if engineSolved else 0.0

clone['totalCases'] = total
clone['gatewayCorrect'] = gwCorr
clone['gatewayEngineUsed'] = gwUsed
clone['gatewayTruthCorrect'] = gwCorr
clone['pureLlmCorrect'] = pCorr
clone['harmCount'] = harm
clone['engineSolved'] = engineSolved
clone['enginePrecision'] = round(precision, 6)
clone['gatewayAccuracy'] = round(gwCorr/total, 6) if total else 0
clone['pureLlmAccuracy'] = round(pCorr/total, 6) if total else 0
clone['engineCoverage'] = round(engineSolved/total, 6) if total else 0
clone['bothCorrect'] = len(buckets['bothCorrectCases'])
clone['gatewayOnlyCorrect'] = len(buckets['gatewayOnlyCorrectCases'])
clone['pureLlmOnlyCorrect'] = len(buckets['pureLlmOnlyCorrectCases'])
clone['bothWrong'] = len(buckets['bothWrongCases'])

out = REPO + r'\_evalscratch\after_tier3.json'
with open(out, 'w', encoding='utf-8') as f:
    json.dump(clone, f, ensure_ascii=False, indent=2)

print(f"patched={patched} missing_in_v3={missing_in_v3}")
print(f"totalCases={total}")
print(f"engineSolved(adopted)={engineSolved}")
print(f"gatewayTruthCorrect={gwCorr}")
print(f"harmCount={harm}")
print(f"enginePrecision={precision}")
print(f"gatewayEngineUsed={gwUsed}")
print(f"gatewayAccuracy={gwCorr/total:.6f}")
print(f"pureLlmCorrect={pCorr}")
print(f"buckets: both={clone['bothCorrect']} gwOnly={clone['gatewayOnlyCorrect']} llmOnly={clone['pureLlmOnlyCorrect']} bothWrong={clone['bothWrong']}")

# delta vs stage6 baseline
bs = json.load(open(REPO + r'\_evalscratch\after_stage6_concept.json', encoding='utf-8-sig'))
print("\n--- delta vs after_stage6_concept ---")
print(f"  engineSolved: {bs.get('engineSolved')} -> {engineSolved}  (+{engineSolved - (bs.get('engineSolved') or 0)})")
print(f"  gatewayCorrect: {bs.get('gatewayCorrect')} -> {gwCorr}  (+{gwCorr - (bs.get('gatewayCorrect') or 0)})")
print(f"  harmCount: {bs.get('harmCount')} -> {harm}")
print(f"  enginePrecision: {bs.get('enginePrecision')} -> {precision}")
print(f"wrote: {out}")
