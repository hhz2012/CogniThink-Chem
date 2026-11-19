import json, re
BASE=r"D:\github\CogniThink-Chem\_evalscratch\\"
with open(BASE+"after_stage2c.json",encoding="utf-8-sig") as f:
    data=json.load(f)
flat={}
for k,v in data.items():
    if isinstance(v,list):
        for c in v:
            if c.get("id") is not None: flat[c["id"]]=c

choice_re = re.compile(r"(^|\n)\s*[A-D][\.．、]\s")
arrow = re.compile(r"→|═|⇌|->|={2,}|(?<![<>])=(?!=)")
placeholder = re.compile(r"\bX\b|\bW\b|\bY\b|\bZ\b")
write_kw = re.compile(r"配平|书写|方程式|化学方程式|离子方程式")

# Find ALL non-choice questions with a complete equation + write/balance intent
rows=[]
for cid,c in flat.items():
    q=str(c.get("question") or "")
    if choice_re.search(q): continue   # skip choice (stage3B)
    if not write_kw.search(q): continue
    if not arrow.search(q): continue
    complete = not placeholder.search(q)
    rows.append((cid,c.get("taskType"),complete,c.get("gatewayCorrect"),c.get("pureLlmCorrect"),q,c.get("expected")))

print(f"non-choice questions with write/balance intent AND equation: {len(rows)}")
print(f"  complete (no X/W): {sum(1 for r in rows if r[2])}")
for cid,tt,comp,gc,pc,q,exp in sorted(rows,key=lambda x:str(x[0])):
    print("="*70)
    print(f"{cid} tt={tt} complete={comp} gw={gc} pure={pc}")
    print("Q:", q[:300].replace("\n"," "))
    print("EXP:", str(exp)[:200])
