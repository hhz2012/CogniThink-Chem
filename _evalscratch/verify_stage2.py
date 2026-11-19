import json, re

BASE = r"D:\github\CogniThink-Chem\_evalscratch\\"

def load(name):
    with open(BASE+name, encoding="utf-8-sig") as f:
        return json.load(f)

def flatten(data):
    """id -> case dict, across all list partitions."""
    out = {}
    for k,v in data.items():
        if isinstance(v, list):
            for c in v:
                cid = c.get("id")
                if cid is not None:
                    out[cid] = c
    return out

CHOICE_RE = re.compile(r"(^|\n)\s*[A-D][\.．、]\s")

s1 = load("after_stage1.json")
r1 = load("after_stage2c.json")   # post-fix run1
r2 = load("after_stage2d.json")   # post-fix run2
pre1 = load("after_stage2.json")  # pre-fix run1 (for drift comparison)
pre2 = load("after_stage2b.json") # pre-fix run2

f1 = flatten(s1); fr1 = flatten(r1); fr2 = flatten(r2)
fp1 = flatten(pre1); fp2 = flatten(pre2)
print(f"stage1 cases={len(f1)}  postfix-run1={len(fr1)}  postfix-run2={len(fr2)}")

def summary(d, label):
    print(f"\n--- {label} ---")
    for k in ["totalCases","pureLlmCorrect","gatewayCorrect","gatewayTruthCorrect",
              "gatewayEngineUsed","engineSolved","engineCoverage","enginePrecision","harmCount",
              "bothCorrect","gatewayOnlyCorrect","pureLlmOnlyCorrect","bothWrong"]:
        print(f"  {k} = {d.get(k)}")

summary(s1,"STAGE1 baseline")
summary(r1,"STAGE2 run1")
summary(r2,"STAGE2 run2")

# ---- TR-10.1: taskType consistency run1 vs run2 (post-fix) ----
common = sorted(set(fr1)&set(fr2))
same=0; diff=[]
for cid in common:
    a=fr1[cid].get("taskType"); b=fr2[cid].get("taskType")
    if a==b: same+=1
    else: diff.append((cid,a,b))
pct = same/len(common)*100 if common else 0
print(f"\n[TR-10.1] taskType consistency POST-FIX runC vs runD: {same}/{len(common)} = {pct:.2f}%  (need >=99%)")
if diff:
    print("  mismatches (id, runC, runD):")
    for d in diff[:30]:
        print("   ", d)

# pre-fix consistency (for留痕 comparison)
pcommon=sorted(set(fp1)&set(fp2))
psame=0; pdiff=[]
for cid in pcommon:
    a=fp1[cid].get("taskType"); b=fp2[cid].get("taskType")
    if a==b: psame+=1
    else: pdiff.append((cid,a,b))
ppct=psame/len(pcommon)*100 if pcommon else 0
print(f"[TR-10.1] taskType consistency PRE-FIX  runA vs runB: {psame}/{len(pcommon)} = {ppct:.2f}%  (reference)")
if pdiff:
    print("  pre-fix mismatches:")
    for d in pdiff[:30]: print("   ", d)

# ---- TR-10.2: choice questions (option block present) classified as choice ----
def choice_rate(flat, label):
    has_block=[cid for cid,c in flat.items() if c.get("question") and CHOICE_RE.search(c["question"])]
    as_choice=[cid for cid in has_block if flat[cid].get("taskType")=="choice"]
    pct = len(as_choice)/len(has_block)*100 if has_block else 0
    print(f"\n[TR-10.2] {label}: questions with option block={len(has_block)}, classified choice={len(as_choice)} ({pct:.2f}%)  (need >=95%)")
    mis=[cid for cid in has_block if flat[cid].get("taskType")!="choice"]
    if mis:
        print("  NOT classified as choice (first 10):")
        for cid in mis[:10]:
            print(f"    id={cid} taskType={flat[cid].get('taskType')} q={flat[cid].get('question','')[:60]!r}")
    return has_block, as_choice

choice_rate(fr1,"run1")
choice_rate(fr2,"run2")

# ---- TR-10.3: no regression of engine-truth correct ids ----
def correct_ids(flat):
    return set(cid for cid,c in flat.items() if c.get("gatewayCorrect") and c.get("gatewayTruthAdopted"))

c1=correct_ids(f1); cr1=correct_ids(fr1); cr2=correct_ids(fr2)
print(f"\n[TR-10.3] engine-truth correct ids: stage1={len(c1)} run1={len(cr1)} run2={len(cr2)}")
lost = c1 - cr1
gained = cr1 - c1
print(f"  lost from stage1->run1: {len(lost)}")
for cid in sorted(lost)[:20]:
    c=f1[cid]
    print(f"    id={cid} taskType={c.get('taskType')} q={c.get('question','')[:70]!r}")
print(f"  gained stage1->run1: {len(gained)}")
for cid in sorted(gained)[:20]:
    c=fr1[cid]
    print(f"    id={cid} taskType={c.get('taskType')} q={c.get('question','')[:70]!r}")

# per-taskType correct counts
from collections import Counter
def by_type(flat, label):
    cnt=Counter()
    for cid,c in flat.items():
        if c.get("gatewayCorrect") and c.get("gatewayTruthAdopted"):
            cnt[c.get("taskType")]+=1
    print(f"  {label}: {dict(cnt)}")
print("\nper-taskType engine-truth correct:")
by_type(f1,"stage1")
by_type(fr1,"run1")
by_type(fr2,"run2")

# ---- harm check (帮倒忙 = engine solved but wrong & pure LLM right) ----
def harm_ids(flat):
    out=[]
    for cid,c in flat.items():
        if c.get("gatewayUsedEngine") and not c.get("gatewayCorrect") and c.get("pureLlmCorrect"):
            out.append(cid)
    return out
h1=harm_ids(f1); hr1=harm_ids(fr1)
print(f"\n[harm] stage1={len(h1)} run1={len(hr1)} (top-level harmCount run1={r1.get('harmCount')})")

# ---- verdict ----
print("\n===== VERDICT =====")
print(f"TR-10.1 consistency {pct:.2f}% >= 99% ? {'PASS' if pct>=99 else 'FAIL'}")
# recompute run1 choice rate for verdict
hb1=[cid for cid,c in fr1.items() if c.get("question") and CHOICE_RE.search(c["question"])]
ac1=[cid for cid in hb1 if fr1[cid].get("taskType")=="choice"]
cr1_pct=len(ac1)/len(hb1)*100 if hb1 else 0
print(f"TR-10.2 choice recall {cr1_pct:.2f}% >= 95% ? {'PASS' if cr1_pct>=95 else 'FAIL'}")
print(f"TR-10.3 no loss (lost={len(lost)}) ? {'PASS' if len(lost)==0 else 'FAIL'}")
print(f"harm run1={len(hr1)} == 0 ? {'PASS' if len(hr1)==0 else 'FAIL'}")
