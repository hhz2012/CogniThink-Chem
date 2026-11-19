import json, re
BASE=r"D:\github\CogniThink-Chem\_evalscratch\\"
with open(BASE+"after_stage2c.json",encoding="utf-8-sig") as f:
    data=json.load(f)
flat={}
for k,v in data.items():
    if isinstance(v,list):
        for c in v:
            if c.get("id") is not None: flat[c["id"]]=c

# An equation-like substring: has arrow/= separator and formulas on both sides with element letters
EQ_RE = re.compile(r"([A-Za-z0-9+\-\^\(\)\[\]·\s]+?)\s*(?:→|═|=|⇌|->)\s*([A-Za-z0-9+\-\^\(\)\[\]·\s]+)")
has_arrow = re.compile(r"→|═|⇌|->|(?<![<>])=(?!=)")

cands=[]
for cid,c in flat.items():
    q=c.get("question") or ""
    tt=c.get("taskType")
    # genuine balance taskType OR explicit 配平
    is_bal = (tt=="balance") or ("配平" in q)
    if not is_bal: continue
    # does question contain an equation (arrow/= separating two formula sides)?
    m = has_arrow.search(q)
    has_eq = bool(m)
    cands.append((cid, tt, has_eq, c.get("gatewayCorrect"), c.get("pureLlmCorrect"), q))

print(f"balance candidates: {len(cands)}")
print(f"  with equation in question: {sum(1 for x in cands if x[2])}")
print(f"  taskType==balance: {sum(1 for x in cands if x[1]=='balance')}")
print()
for cid,tt,he,gc,pc,q in sorted(cands, key=lambda x:str(x[0])):
    tag = "EQ" if he else "noEQ"
    print(f"{cid:10} tt={tt:14} {tag} gw={gc} pure={pc}  Q={q[:95]!r}")
