import json, os
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
def load(p): return json.load(open(os.path.join(HERE,p),encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
def is_pdf(r): return (r.get("id") or "").startswith("pdf-")
for name in ["after7.json","after8.json","after8b.json"]:
    d=load(name); cs=cases(d)
    pdf=[r for r in cs if is_pdf(r)]
    hand=[r for r in cs if not is_pdf(r)]
    tot=d.get("totalCases")
    pg=sum(1 for r in pdf if r.get("gatewayCorrect"))
    pp=sum(1 for r in pdf if r.get("pureLlmCorrect"))
    hg=sum(1 for r in hand if r.get("gatewayCorrect"))
    hp=sum(1 for r in hand if r.get("pureLlmCorrect"))
    ion=[r for r in cs if r.get("taskType")=="ion_check"]; igo=sum(1 for r in ion if r.get("gatewayCorrect"))
    eqcalc=sum(1 for r in cs if r.get("taskType") in ("calculation","equilibrium") and r.get("gatewayUsedEngine"))
    hurts=sum(1 for r in cs if r.get("pureLlmCorrect") and not r.get("gatewayCorrect") and r.get("gatewayUsedEngine"))
    gw=d.get("gatewayCorrect"); pl=d.get("pureLlmCorrect"); eng=d.get("gatewayEngineUsed")
    print(f"{name}: 总{tot} 纯{pl} 网{gw} 介入{eng} 总体{gw/tot*100:.1f}% PDF网{pg}/{len(pdf)}={pg/len(pdf)*100:.1f}% PDF纯{pp} 手工{hg}/{len(hand)} 手工纯{hp} ion{igo}/{len(ion)} calc+eq{egcalc if False else eqcalc} 帮倒忙{hurts}")
