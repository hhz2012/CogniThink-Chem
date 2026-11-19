# -*- coding: utf-8 -*-
import json, io, sys, collections
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
PATH = r"D:\github\CogniThink-Chem\_evalscratch\after5.json"
data = json.load(open(PATH, encoding="utf-8-sig"))

def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out

cs = cases(data)
print("total cases:", len(cs))

# 帮倒忙: pure对 网关错 引擎介入
hurt = [c for c in cs if c.get("pureLlmCorrect") and not c.get("gatewayCorrect") and c.get("gatewayUsedEngine")]
print(f"\n=== 帮倒忙 (pure对/网错/介入) n={len(hurt)} ===")
for c in hurt:
    print(f"\n[{c['id']}] task={c.get('taskType')} 介入={c.get('gatewayUsedEngine')}")
    print(f"  Q: {c.get('question','')[:140]}")
    print(f"  expected: {c.get('expected','')[:90]}")
    print(f"  conclusion: {c.get('gatewayConclusion','')[:120]}")
    print(f"  gatewayAnswer: {c.get('gatewayAnswer','')[:200]}")
    print(f"  pureLlmAnswer: {c.get('pureLlmAnswer','')[:200]}")

# gateway only: 网关对 pure错 介入
gwonly = [c for c in cs if c.get("gatewayCorrect") and not c.get("pureLlmCorrect") and c.get("gatewayUsedEngine")]
print(f"\n=== gatewayOnlyCorrect 且引擎介入 n={len(gwonly)} ===")
for c in gwonly:
    print(f"  [{c['id']}] task={c.get('taskType')} 结论={(c.get('gatewayConclusion') or '')[:70]}")

# bothWrong calc/eq
bw = [c for c in cs if (not c.get('gatewayCorrect')) and (not c.get('pureLlmCorrect'))]
bw_ce = [c for c in bw if c.get('taskType') in ('calculation','equilibrium')]
print(f"\n=== bothWrong calc/eq n={len(bw_ce)} ===")
for c in bw_ce:
    print(f"  [{c['id']}] task={c.get('taskType')} 介入={c.get('gatewayUsedEngine')} | Q={c.get('question','')[:100]} | exp={c.get('expected','')[:60]}")

print(f"\n=== bothWrong 总数 n={len(bw)} taskType分布 ===")
for k,v in collections.Counter(c.get('taskType') for c in bw).most_common():
    print(f"  {k}: {v}")

# pureLlmOnly (gateway wrong, pure right) regardless of engine — includes non-engine too
ponly = [c for c in cs if c.get("pureLlmCorrect") and not c.get("gatewayCorrect")]
print(f"\n=== pureLlmOnly (pure对/网错, 全部) n={len(ponly)} ===")
for c in ponly:
    print(f"  [{c['id']}] task={c.get('taskType')} 介入={c.get('gatewayUsedEngine')} 结论={(c.get('gatewayConclusion') or '')[:50]} | exp={c.get('expected','')[:40]}")
