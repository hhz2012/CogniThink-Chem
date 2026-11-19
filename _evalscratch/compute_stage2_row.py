import json
BASE=r"D:\github\CogniThink-Chem\_evalscratch\\"
def load(n):
    with open(BASE+n,encoding="utf-8-sig") as f: return json.load(f)
def flatten(d):
    o={}
    for k,v in d.items():
        if isinstance(v,list):
            for c in v:
                if c.get("id") is not None: o[c["id"]]=c
    return o

for name in ["after_stage1.json","after_stage2c.json","after_stage2d.json"]:
    d=load(name); f=flatten(d)
    total=len(f)
    pure=sum(1 for c in f.values() if c.get("pureLlmCorrect"))
    gw=sum(1 for c in f.values() if c.get("gatewayCorrect"))
    eng=sum(1 for c in f.values() if c.get("gatewayUsedEngine"))
    harm=d.get("harmCount")
    # subsets
    pdf=[c for c in f.values() if str(c.get("id","")).startswith("pdf-")]
    manual=[c for c in f.values() if not str(c.get("id","")).startswith("pdf-")]
    pdf_gw=sum(1 for c in pdf if c.get("gatewayCorrect"))
    man_gw=sum(1 for c in manual if c.get("gatewayCorrect"))
    man_pure=sum(1 for c in manual if c.get("pureLlmCorrect"))
    ion_ok=sum(1 for c in f.values() if c.get("taskType")=="ion_check" and c.get("gatewayCorrect"))
    calceq_used=sum(1 for c in f.values() if c.get("taskType") in ("calculation","equilibrium") and c.get("gatewayUsedEngine"))
    print(f"{name:22} 总{total} 纯{pure} 网关{gw} 介入{eng} 网关%{gw/total*100:.1f} PDF网关{pdf_gw}/{len(pdf)}={pdf_gw/len(pdf)*100:.1f} 手工{man_gw}/{len(manual)} 手工纯{man_pure} ion正确{ion_ok} calc+eq介入{calceq_used} 帮倒忙{harm}")
    # truth distribution
    from collections import Counter
    cnt=Counter(c.get("taskType") for c in f.values() if c.get("gatewayCorrect") and c.get("gatewayTruthAdopted"))
    print("    真值解出分布:", dict(cnt))
