# -*- coding: utf-8 -*-
import json, io, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
PATH = r"D:\github\CogniThink-Chem\_evalscratch\after7.json"
data = json.load(open(PATH, encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
cs = cases(data)
bw = [c for c in cs if (not c.get('gatewayCorrect')) and (not c.get('pureLlmCorrect'))]
bw_ce = [c for c in bw if c.get('taskType') in ('calculation','equilibrium')]
print(f"bothWrong calc/eq 总数: {len(bw_ce)}")
for c in bw_ce:
    print("="*80)
    print(f"[{c['id']}] task={c.get('taskType')} 介入={c.get('gatewayUsedEngine')}")
    print(f"Q: {c.get('question','')[:260]}")
    print(f"expected: {c.get('expected','')[:120]}")
    print(f"conclusion: {c.get('gatewayConclusion','')[:80]}")
