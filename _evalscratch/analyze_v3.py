import json

rows = json.load(open(r'D:\github\CogniThink-Chem\_evalscratch\engine_dump_v3.json', encoding='utf-8-sig'))
N = len(rows)
gains = [r for r in rows if r['delta']=='GAIN']
losses = [r for r in rows if r['delta']=='LOSS']
unch = [r for r in rows if r['delta']=='unchanged']
exc = [r for r in rows if r['threw']]
# harm = engine adopted but not correct (and would count against precision)
harms = [r for r in rows if r['newAdopted'] and not r['newCorrect']]
struct = [r for r in rows if r['structuredHit']]

print(f"total={N}  gains={len(gains)}  losses={len(losses)}  unchanged={len(unch)}  exceptions={len(exc)}  structHits={len(struct)}")
print(f"HARM (adopted but not correct): {len(harms)}")
for h in harms:
    print(f"   HARM {h['id']} taskType={h['newTaskType']} final={h['finalResult'][:80]!r}")
print()
print("=== structHits ===")
for r in struct:
    print(f"  {r['id']} delta={r['delta']} baselineCorrect={r['baselineCorrect']} newCorrect={r['newCorrect']} taskType={r['newTaskType']}")
print()
print("=== losses (should be empty) ===")
for r in losses:
    print(f"  {r['id']} taskType={r['newTaskType']} final={r['finalResult'][:80]!r}")
print()
print("=== exceptions: baselineCorrect? (none should be, all preserved unchanged) ===")
exc_baseline_correct = [r for r in exc if r['baselineCorrect']]
print(f"  exceptions that were baseline-correct: {len(exc_baseline_correct)}")
for r in exc_baseline_correct:
    print(f"    {r['id']} taskType={r['baselineTaskType']}")
# sample exception messages
msgs = {}
for r in exc:
    m = (r.get('exception') or '')[:60]
    msgs[m] = msgs.get(m,0)+1
print("  exception message buckets:")
for m,c in sorted(msgs.items(), key=lambda x:-x[1]):
    print(f"    [{c}] {m}")
print()
# the 5 data-pollution gains: confirm newCorrect and show finalResult tail
print("=== 5 data-pollution gains (key-corrected) ===")
for r in gains:
    if r['id'] in ('pdf-008','pdf-154','pdf-186','pdf-326','pdf-502'):
        print(f"  {r['id']} taskType={r['newTaskType']} newCorrect={r['newCorrect']} final={r['finalResult'][:90]!r}")
print()
# gains by newTaskType bucket
from collections import Counter
print("=== gains by newTaskType ===")
for tt,c in Counter(r['newTaskType'] for r in gains).most_common():
    print(f"  {tt}: {c}")
