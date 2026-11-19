import json, re
BASE=r"D:\github\CogniThink-Chem\_evalscratch\\"
with open(BASE+"after_stage2c.json",encoding="utf-8-sig") as f:
    data=json.load(f)
flat={}
for k,v in data.items():
    if isinstance(v,list):
        for c in v:
            if c.get("id") is not None: flat[c["id"]]=c

# balance questions: taskType balance OR question contains 配平/书写/化学方程式
def is_balance(c):
    q=c.get("question") or ""
    if c.get("taskType")=="balance": return True
    if any(k in q for k in ["配平","书写","化学方程式","离子方程式"]): return True
    return False

bs=[c for c in flat.values() if is_balance(c)]
print(f"balance-related questions: {len(bs)}")
for c in sorted(bs, key=lambda x:str(x.get("id"))):
    print("="*70)
    print(f"id={c.get('id')} taskType={c.get('taskType')} gwCorrect={c.get('gatewayCorrect')} pureCorrect={c.get('pureLlmCorrect')} usedEngine={c.get('gatewayUsedEngine')} truthAdopted={c.get('gatewayTruthAdopted')}")
    print("Q:", repr(c.get("question")))
    print("expected:", repr(c.get("expected")))
    print("note:", repr(c.get("note")))
