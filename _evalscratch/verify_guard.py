# -*- coding: utf-8 -*-
import json, io, sys, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
def load(p): return json.load(open(p, encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out

a5 = {r['id']:r for r in cases(load(r"D:\github\CogniThink-Chem\_evalscratch\after5.json"))}
a6 = {r['id']:r for r in cases(load(r"D:\github\CogniThink-Chem\_evalscratch\after6.json"))}

LETTER = re.compile(r'(?<![A-Za-z])[A-Da-d](?![A-Za-z])')
def letters(s):
    return set(m.group(0).upper() for m in LETTER.finditer(s or ""))

ids = ['pdf-105','pdf-249','pdf-300','pdf-408',   # 帮倒忙 -> 预期修复(guard fallback)
       'pdf-045','pdf-087','pdf-409',            # after5 gatewayOnly -> after6 错, 查是否 guard 误伤
       'pdf-224','d9-1']                          # 其余 after5 gatewayOnly reaction_check, 应保持对

print(f"{'id':10} {'a5gw':5}{'a5pu':5}{'a6gw':5}{'a6pu':5} {'gw==pu':7} {'gwL':4}{'puL':4} {'a6task':14} a6结论")
for qid in ids:
    b=a5.get(qid,{}); a=a6.get(qid,{})
    gwa=a.get('gatewayAnswer',''); pua=a.get('pureLlmAnswer','')
    eq = (gwa.strip()==pua.strip()) if (gwa or pua) else False
    print(f"{qid:10} {str(b.get('gatewayCorrect')):5}{str(b.get('pureLlmCorrect')):5}{str(a.get('gatewayCorrect')):5}{str(a.get('pureLlmCorrect')):5} {str(eq):7} {len(letters(gwa)):<4}{len(letters(pua)):<4} {(a.get('taskType') or ''):14} {(a.get('gatewayConclusion') or '')[:24]}")

# 全量统计: after6 中 guard 触发条件(选择题 & 网关字母<纯字母 & 引擎介入)的题数与对错
print("\n=== after6 全量: 满足 guard 触发条件的题 ===")
import collections
tri=[]
for r in cases(load(r"D:\github\CogniThink-Chem\_evalscratch\after6.json")):
    if not r.get('gatewayUsedEngine'): continue
    q=r.get('question','')
    isch = bool(re.search(r'(^|\n)\s*[A-D][\.．、]\s', q))
    if not isch: continue
    gl=len(letters(r.get('gatewayAnswer'))); pl=len(letters(r.get('pureLlmAnswer')))
    if gl < pl:
        tri.append(r)
print(f"触发数={len(tri)}; 其中网关对={sum(1 for r in tri if r.get('gatewayCorrect'))}, 网关错={sum(1 for r in tri if not r.get('gatewayCorrect'))}")
print(f"  触发后 gatewayAnswer==pureLlmAnswer 的: {sum(1 for r in tri if (r.get('gatewayAnswer') or '').strip()==(r.get('pureLlmAnswer') or '').strip())}")
for r in tri:
    gwa=r.get('gatewayAnswer',''); pua=r.get('pureLlmAnswer','')
    eq=(gwa.strip()==pua.strip()) if (gwa or pua) else False
    print(f"  [{r['id']}] gw={r.get('gatewayCorrect')} pu={r.get('pureLlmCorrect')} gw==pu={eq} gwL={len(letters(gwa))} puL={len(letters(pua))} task={r.get('taskType')}")
