import json, re
BASE=r"D:\github\CogniThink-Chem\_evalscratch\\"
with open(BASE+"after_stage2c.json",encoding="utf-8-sig") as f:
    data=json.load(f)
flat={}
for k,v in data.items():
    if isinstance(v,list):
        for c in v:
            if c.get("id") is not None: flat[c["id"]]=c

eq_in_expected = re.compile(r"(=|→|═|⇌).*[A-Za-z]")
has_arrow = re.compile(r"→|═|⇌|->|(?<![<>])=(?!=)")
placeholder = re.compile(r"\bX\b|\bW\b|\bY\b|\bZ\b")

rows=[]
for cid,c in flat.items():
    exp=str(c.get("expected") or "")
    q=str(c.get("question") or "")
    if eq_in_expected.search(exp) and has_arrow.search(q):
        complete = not placeholder.search(q)
        rows.append((cid, c.get("taskType"), complete, c.get("gatewayCorrect"), c.get("pureLlmCorrect"), q, exp))

print(f"questions: expected has equation AND question has equation: {len(rows)}")
print(f"  complete (no X/W placeholder): {sum(1 for r in rows if r[2])}")
from collections import Counter
print("  taskType distribution:", Counter(r[1] for r in rows))
print()
for cid,tt,comp,gc,pc,q,exp in sorted(rows,key=lambda x:str(x[0])):
    print("="*72)
    print(f"{cid} tt={tt} complete={comp} gw={gc} pure={pc}")
    print("Q:", q[:160].replace("\n"," "))
    print("EXP:", exp[:160])
