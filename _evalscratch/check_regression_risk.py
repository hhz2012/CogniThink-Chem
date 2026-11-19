import json, re
BASE=r"D:\github\CogniThink-Chem\_evalscratch\\"
with open(BASE+"after_stage1.json",encoding="utf-8-sig") as f:
    s1=json.load(f)
with open(BASE+"after_stage2.json",encoding="utf-8-sig") as f:
    r1=json.load(f)
def flatten(d):
    o={}
    for k,v in d.items():
        if isinstance(v,list):
            for c in v:
                if c.get("id") is not None: o[c["id"]]=c
    return o
f1=flatten(s1); fr1=flatten(r1)
CHOICE_RE=re.compile(r"(^|\n)\s*[A-D][\.．、]\s")

# 56 correct in stage1
corr1=[cid for cid,c in f1.items() if c.get("gatewayCorrect") and c.get("gatewayTruthAdopted")]
print(f"stage1 correct count={len(corr1)}")
# how many have option block
blk=[cid for cid in corr1 if CHOICE_RE.search(f1[cid].get("question") or "")]
print(f"of which have option block (would be reclassified to choice by fix): {len(blk)}")
for cid in blk:
    c=f1[cid]
    print(f"  {cid} taskType={c.get('taskType')} q={c.get('question','')[:80]!r}")

# Also check: of all 648, which correct-by-engine (stage1) are choice-regex but taskType != choice?
print("\n--- all stage1 engine-correct with option block but taskType!=choice (regression risk if fix applied) ---")
risk=[cid for cid in corr1 if cid in blk and f1[cid].get("taskType")!="choice"]
print(f"risk count={len(risk)}")
for cid in risk:
    print(f"  {cid} tt={f1[cid].get('taskType')}")

# Confirm: applying det chain to fallback wouldn't change the 56. Simulate:
# For each correct id, what would ClassifyTaskDeterministic return? We approximate with python port.
def classify_det(q):
    text=q or ""; lower=text.lower()
    if CHOICE_RE.search(text): return "choice"
    if "共存" in lower: return "ion_check"
    if any(k in lower for k in ["配平","平了吗","系数为","化学计量数"]): return "balance"
    if any(k in lower for k in ["平衡","转化率","勒夏特列","ksp","电离","水解","溶解平衡"]): return "equilibrium"
    if any(k in lower for k in ["物质的量","浓度","体积","mol","ph","质量分数","稀释","ka","计算","求"]): return "calculation"
    if any(k in lower for k in ["能否反应","是否反应","方程式对吗","正确吗","对吗","反应能否","能否发生"]): return "reaction_check"
    if any(k in lower for k in ["结构式","电子式","价键"]): return "structure_verify"
    if any(k in lower for k in ["vsepr","杂化","构型","空间构型","价层电子对"]): return "vsepr"
    if "滴定" in lower: return "titration"
    if any(k in lower for k in ["什么是","定义","概念","区别","为什么","原理","是指"]): return "concept"
    return "unknown"

print("\n--- simulate: would det chain change taskType of any of the 56 correct? ---")
changed=[]
for cid in corr1:
    cur=f1[cid].get("taskType")
    det=classify_det(f1[cid].get("question") or "")
    if det!="unknown" and det!=cur:
        changed.append((cid,cur,det))
print(f"would change: {len(changed)}")
for cid,cur,det in changed:
    print(f"  {cid}: {cur} -> {det}  q={f1[cid].get('question','')[:70]!r}")
