# -*- coding: utf-8 -*-
import json, os, collections, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import analyze_baseline as a

def load(p): return json.load(open(os.path.join(HERE, p), encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
def is_pdf(r): return (r.get("id") or "").startswith("pdf-")
def sub(rows,key):
    if not rows: return 0,0.0
    ok=sum(1 for r in rows if r.get(key)); return ok, ok/len(rows)*100

def summary(name,d):
    cs=cases(d); pdf=[r for r in cs if is_pdf(r)]; hand=[r for r in cs if not is_pdf(r)]
    print(f"=== {name} (total {d.get('totalCases')}) ===")
    print(f"  总体: 纯LLM {d.get('pureLlmCorrect')}({d.get('pureLlmAccuracy')}) 网关 {d.get('gatewayCorrect')}({d.get('gatewayAccuracy')}) 引擎介入 {d.get('gatewayEngineUsed')}")
    for label,rows in [("PDF",pdf),("手工",hand)]:
        po,pa=sub(rows,"pureLlmCorrect"); go,ga=sub(rows,"gatewayCorrect"); eu=sum(1 for r in rows if r.get("gatewayUsedEngine"))
        print(f"  {label}({len(rows)}): 纯LLM {po}({pa:.1f}%) 网关 {go}({ga:.1f}%) 引擎介入 {eu}")
    by=collections.defaultdict(list)
    for r in cs: by[r.get("taskType") or "?"].append(r)
    print("  -- by taskType --")
    for t,rows in sorted(by.items(),key=lambda x:-len(x[1])):
        eu=sum(1 for r in rows if r.get("gatewayUsedEngine")); go,ga=sub(rows,"gatewayCorrect"); po,pa=sub(rows,"pureLlmCorrect")
        print(f"    {t:18s} n={len(rows):3d} 介入={eu:3d} 网关 {go:3d}({ga:4.1f}%) 纯LLM {po:3d}({pa:4.1f}%)")
    print()

summary("AFTER (门控前)", load("after.json"))
summary("AFTER2 (门控后)", load("after2.json"))

# 逐题翻转：after -> after2
ca={r.get("id"):r for r in cases(load("after.json"))}
cb={r.get("id"):r for r in cases(load("after2.json"))}
print("=== 逐题网关对错变化 (after -> after2) ===")
flipped_to_right=[]; flipped_to_wrong=[]
for qid in ca:
    a1=ca[qid].get("gatewayCorrect"); a2=cb.get(qid,{}).get("gatewayCorrect")
    if a1!=a2:
        if a2: flipped_to_right.append(qid)
        else: flipped_to_wrong.append(qid)
print(f"  网错->网对: {len(flipped_to_right)}  {flipped_to_right}")
print(f"  网对->网错: {len(flipped_to_wrong)}  {flipped_to_wrong}")
print()
print("=== AFTER2 帮倒忙 (纯对/网错/引擎介入) ===")
hurts=[r for r in cases(load("after2.json")) if r.get("pureLlmCorrect") and not r.get("gatewayCorrect") and r.get("gatewayUsedEngine")]
print(f"  共 {len(hurts)} 题:")
print("  ", dict(collections.Counter(r.get("taskType") for r in hurts)))
for r in hurts:
    print(f"  [{r.get('id')}] task={r.get('taskType')} 期望={(r.get('expected') or '')[:35]} | 结论={(r.get('gatewayConclusion') or '')[:28]}")

# AC 核对
d=load("after2.json"); cs=cases(d); pdf=[r for r in cs if is_pdf(r)]; hand=[r for r in cs if not is_pdf(r)]
print()
print("=== AC 核对 (AFTER2) ===")
print(f"  AC-3 PDF纯LLM≥280: {sum(1 for r in pdf if r.get('pureLlmCorrect'))} (>=280 -> {sum(1 for r in pdf if r.get('pureLlmCorrect'))>=280})")
pg,pa=sub(pdf,"gatewayCorrect"); pp,ppa=sub(pdf,"pureLlmCorrect")
og,oga=sub(cs,"gatewayCorrect"); op,opa=sub(cs,"pureLlmCorrect")
print(f"  AC-4 总体网关≥纯LLM: {oga:.1f}% vs {opa:.1f}% -> {oga>=opa}")
print(f"  AC-4 PDF网关≥纯LLM: {pa:.1f}% vs {ppa:.1f}% -> {pa>=ppa}  (网关{pg} vs 纯{pp})")
hg=sum(1 for r in hand if r.get("gatewayCorrect")); hp=sum(1 for r in hand if r.get("pureLlmCorrect"))
print(f"  AC-7 手工网关100%: {hg}/103 -> {hg==103}; 手工纯LLM≥95: {hp} -> {hp>=95}")
eqcalc=sum(1 for r in cs if r.get("taskType") in ("calculation","equilibrium") and r.get("gatewayUsedEngine"))
print(f"  AC-6 calc+eq介入≥26: {eqcalc} -> {eqcalc>=26}")
