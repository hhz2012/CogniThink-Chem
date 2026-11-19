import json, re
BASE=r"D:\github\CogniThink-Chem\_evalscratch\\"
with open(BASE+"after_stage2c.json",encoding="utf-8-sig") as f:
    data=json.load(f)
flat={}
for k,v in data.items():
    if isinstance(v,list):
        for c in v:
            if c.get("id") is not None: flat[c["id"]]=c

has_arrow = re.compile(r"→|═|⇌|->|(?<![<>])=(?!=)")
bal_kw = re.compile(r"配平|平了吗|系数|化学计量数|计量数|未配平")
# placeholder unknowns that make equation unbalanceable by atom conservation alone
placeholder = re.compile(r"\bX\b|\bW\b|\bY\b|\bZ\b|下列|未知|字母|符号")

rows=[]
for cid,c in flat.items():
    q=c.get("question") or ""
    if bal_kw.search(q) and has_arrow.search(q):
        complete = not placeholder.search(q)
        rows.append((cid, c.get("taskType"), complete, c.get("gatewayCorrect"), c.get("pureLlmCorrect"), q, c.get("expected")))

print(f"questions with balance-keyword AND equation: {len(rows)}")
print(f"  of which 'complete' (no X/W placeholder): {sum(1 for r in rows if r[2])}")
print()
for cid,tt,comp,gc,pc,q,exp in sorted(rows,key=lambda x:str(x[0])):
    print("="*72)
    print(f"{cid} tt={tt} complete={comp} gw={gc} pure={pc}")
    print("Q:", q)
    print("expected:", repr(exp))
