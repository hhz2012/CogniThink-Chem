# -*- coding: utf-8 -*-
"""after4: (1) 核查网关对->错是否为 taskType 漂移; (2) reaction_check 证据路径帮倒忙 vs 网关独有, 找安全门控维度。"""
import json, os, re, collections
HERE = os.path.dirname(os.path.abspath(__file__))
def load(p): return json.load(open(os.path.join(HERE,p),encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
a3={r["id"]:r for r in cases(load("after3.json"))}
a4={r["id"]:r for r in cases(load("after4.json"))}

print("=== 网关 对->错 (after3->after4): 是否 taskType/结论漂移 ===")
for qid in a3:
    if a3[qid].get("gatewayCorrect") and not a4.get(qid,{}).get("gatewayCorrect"):
        c3=a3[qid]; c4=a4[qid]
        drift = (c3.get("taskType")!=c4.get("taskType")) or (c3.get("gatewayConclusion")!=c4.get("gatewayConclusion")) or (c3.get("gatewayOk")!=c4.get("gatewayOk"))
        print(f"  [{qid}] drift={drift} | a3 task={c3.get('taskType')} ok={c3.get('gatewayOk')} 纯对={c3.get('pureLlmCorrect')} | a4 task={c4.get('taskType')} ok={c4.get('gatewayOk')} 纯对={c4.get('pureLlmCorrect')}")
print()

print("=== after4: reaction_check 证据路径 帮倒忙 vs 网关独有 ===")
def is_choice(q): return bool(re.search(r"(^|\n)\s*[A-D][\.．、]\s", q or ""))
def nonconcrete(c):
    c=c or ""
    return any(k in c for k in ["已匹配","相关反应","相关颜色","已查到","已解析出"]) or not any(k in c for k in ["能","不能","共存","不共存","可以","不可以","正确","错误"])
rc=[r for r in a4.values() if r.get("taskType")=="reaction_check"]
evid=[r for r in rc if nonconcrete(r.get("gatewayConclusion"))]
hurt=[r for r in evid if r.get("pureLlmCorrect") and not r.get("gatewayCorrect")]
gwonly=[r for r in evid if r.get("gatewayCorrect") and not r.get("pureLlmCorrect")]
print(f"evidence 路径 n={len(evid)}: 帮倒忙(纯对网错)={len(hurt)} 网关独有(网对纯错)={len(gwonly)} 净={len(gwonly)-len(hurt)}")
print(f"  帮倒忙 choice? {[(r['id'],is_choice(r.get('question'))) for r in hurt]}")
print(f"  网关独有 choice? {[(r['id'],is_choice(r.get('question'))) for r in gwonly]}")
print()
print("帮倒忙明细:")
for r in hurt:
    print(f"  [{r['id']}] choice={is_choice(r.get('question'))} 结论={(r.get('gatewayConclusion') or '')[:30]}")
    print(f"     Q: {(r.get('question') or '')[:90].replace(chr(10),' ')}")
    print(f"     纯答: {(r.get('pureLlmAnswer') or '')[:70].replace(chr(10),' ')}")
print("网关独有明细(前8):")
for r in gwonly[:8]:
    print(f"  [{r['id']}] choice={is_choice(r.get('question'))} 结论={(r.get('gatewayConclusion') or '')[:30]} | 纯答={(r.get('pureLlmAnswer') or '')[:50].replace(chr(10),' ')}")
