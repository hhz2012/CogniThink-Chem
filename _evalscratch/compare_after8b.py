# -*- coding: utf-8 -*-
"""对比 after8 与 after8b（仅修复 SplitPdfAnswerKeywords 多空格/换行拆分），核对 AC-6 正确率子项与无回归。"""
import json, os, collections
HERE = os.path.dirname(os.path.abspath(__file__))
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
    print(f"  总体: 纯LLM {d.get('pureLlmCorrect')} 网关 {d.get('gatewayCorrect')} 引擎介入 {d.get('gatewayEngineUsed')}")
    eqcalc=sum(1 for r in cs if r.get("taskType") in ("calculation","equilibrium") and r.get("gatewayUsedEngine"))
    by=collections.defaultdict(lambda:[0,0,0])
    for r in cs:
        t=r.get("taskType") or "?"
        by[t][0]+=1
        if r.get("gatewayUsedEngine"): by[t][1]+=1
        if r.get("gatewayCorrect"): by[t][2]+=1
    calc=by.get("calculation",[0,0,0]); eq=by.get("equilibrium",[0,0,0])
    print(f"  calc 介入={calc[1]}(对{calc[2]})  eq 介入={eq[1]}(对{eq[2]})  calc+eq介入={eqcalc}  -> AC-6数 {'PASS' if eqcalc>=26 else 'FAIL'}")
    hg=sum(1 for r in hand if r.get("gatewayCorrect")); hp=sum(1 for r in hand if r.get("pureLlmCorrect"))
    print(f"  手工 网关 {hg}/103  纯LLM {hp}")
    print()

summary("AFTER8 (扩展求解器)", load("after8.json"))
summary("AFTER8b (修复多关键词拆分)", load("after8b.json"))

ca={r.get("id"):r for r in cases(load("after8.json"))}
cb={r.get("id"):r for r in cases(load("after8b.json"))}
for label,key in [("网关","gatewayCorrect"),("纯LLM","pureLlmCorrect")]:
    tr=[]; tw=[]
    for qid in ca:
        a=ca[qid].get(key); b=cb.get(qid,{}).get(key)
        if a!=b: (tr if b else tw).append(qid)
    print(f"=== {label} 对错变化 (after8 -> after8b) ===")
    print(f"  错->对 {len(tr)}: {tr}")
    print(f"  对->错 {len(tw)}: {tw}")
print()

print("=== 6 个新候选在 after8b 的状态 ===")
cands=["pdf-022","pdf-023","pdf-243","pdf-259","pdf-273","pdf-257"]
for qid in cands:
    r=cb.get(qid,{})
    print(f"  [{qid}] task={r.get('taskType')} 介入={r.get('gatewayUsedEngine')} 网关对={r.get('gatewayCorrect')} 纯对={r.get('pureLlmCorrect')} 结论={(r.get('gatewayConclusion') or '')[:60]}")
new_hit=sum(1 for qid in cands if cb.get(qid,{}).get("gatewayUsedEngine"))
new_ok=sum(1 for qid in cands if cb.get(qid,{}).get("gatewayCorrect"))
print(f"  候选 介入={new_hit}/6  网关正确={new_ok}/6  -> 新增正确率 {new_ok/new_hit*100:.0f}% (目标≥70%) {'PASS' if new_ok/new_hit>=0.7 else 'FAIL'}")
print()

for name,d in [("AFTER8",load("after8.json")),("AFTER8b",load("after8b.json"))]:
    hurts=[r for r in cases(d) if r.get("pureLlmCorrect") and not r.get("gatewayCorrect") and r.get("gatewayUsedEngine")]
    print(f"=== {name} 帮倒忙 (纯对/网错/引擎介入): {len(hurts)} ===")
    for r in hurts:
        print(f"  [{r.get('id')}] task={r.get('taskType')} 结论={(r.get('gatewayConclusion') or '')[:40]}")
print()

d=load("after8b.json"); cs=cases(d); pdf=[r for r in cs if is_pdf(r)]; hand=[r for r in cs if not is_pdf(r)]
print("=== AC 核对 (AFTER8b) ===")
pg,pa=sub(pdf,"gatewayCorrect"); pp,ppa=sub(pdf,"pureLlmCorrect")
og,oga=sub(cs,"gatewayCorrect"); op,opa=sub(cs,"pureLlmCorrect")
eqcalc=sum(1 for r in cs if r.get("taskType") in ("calculation","equilibrium") and r.get("gatewayUsedEngine"))
hg=sum(1 for r in hand if r.get("gatewayCorrect")); hp=sum(1 for r in hand if r.get("pureLlmCorrect"))
ion=[r for r in cs if r.get("taskType")=="ion_check"]; igo,iga=sub(ion,"gatewayCorrect")
print(f"  AC-6 calc+eq介入≥26: {eqcalc} -> {eqcalc>=26}")
print(f"  AC-6 新增介入正确率≥70%: {new_ok}/{new_hit} -> {new_ok/new_hit>=0.7}")
print(f"  AC-4 总体网关≥纯LLM: {oga:.1f}% vs {opa:.1f}% -> {oga>=opa}")
print(f"  AC-7 手工网关100%: {hg}/103 -> {hg==103}; 手工纯LLM≥95: {hp} -> {hp>=95}")
print(f"  ion_check 网关: {igo}/{len(ion)} ({iga:.1f}%)  (after7 为 38/39)")
print(f"  bothCorrect={d.get('bothCorrect')} gatewayOnly={d.get('gatewayOnlyCorrect')} pureOnly={d.get('pureLlmOnlyCorrect')} bothWrong={d.get('bothWrong')}")
