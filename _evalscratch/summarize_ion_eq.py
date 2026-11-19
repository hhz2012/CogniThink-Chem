# -*- coding: utf-8 -*-
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
data = json.load(open(os.path.join(HERE, "after.json"), encoding="utf-8-sig"))
parts = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]
cases = []
for p in parts: cases.extend(data.get(p, []) or [])

def has_options(q):
    return bool(re.search(r'(^|\n)\s*[A-D][\.．、]\s', q or ""))

for task in ["ion_check", "equilibrium"]:
    rows = [r for r in cases if r.get("taskType") == task]
    print(f"\n===== {task} (n={len(rows)}) =====")
    for is_choice in [False, True]:
        sub = [r for r in rows if has_options(r.get("question")) == is_choice]
        if not sub: continue
        po = sum(1 for r in sub if r.get("pureLlmCorrect"))
        go = sum(1 for r in sub if r.get("gatewayCorrect"))
        print(f"  [{'choice' if is_choice else '非choice'}] n={len(sub)} 纯LLM对={po} 网关对={go}")
        hurts = [r for r in sub if r.get("pureLlmCorrect") and not r.get("gatewayCorrect")]
        wins = [r for r in sub if r.get("gatewayCorrect") and not r.get("pureLlmCorrect")]
        print(f"     帮倒忙(纯对/网错)={len(hurts)}  独赢(网对/纯错)={len(wins)}")
        for r in hurts:
            print(f"     HURT [{r.get('id')}] 期望={(r.get('expected') or '')[:35]} | 结论={(r.get('gatewayConclusion') or '')[:30]} | 网关答={(r.get('gatewayAnswer') or '').replace(chr(10),' ')[:40]}")
        for r in wins:
            print(f"     WIN  [{r.get('id')}] 期望={(r.get('expected') or '')[:35]} | 结论={(r.get('gatewayConclusion') or '')[:30]} | 网关答={(r.get('gatewayAnswer') or '').replace(chr(10),' ')[:40]}")
