# -*- coding: utf-8 -*-
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
data = json.load(open(os.path.join(HERE, "after.json"), encoding="utf-8-sig"))
parts = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]
cases = []
for p in parts: cases.extend(data.get(p, []) or [])

def has_options(q): return bool(re.search(r'(^|\n)\s*[A-D][\.．、]\s', q or ""))

print("===== equilibrium 已介入 (gatewayUsedEngine=True) 的结论分布 =====")
rows = [r for r in cases if r.get("taskType")=="equilibrium" and r.get("gatewayUsedEngine")]
from collections import Counter
concl_counter = Counter((r.get("gatewayConclusion") or "")[:24] for r in rows)
for k,v in concl_counter.most_common():
    print(f"  x{v}  结论前缀: {k!r}")
print(f"  总计介入: {len(rows)}")
print()
print("===== equilibrium 未介入 (Ok=null) 且题干含'电离/水解/平衡/移动'的题（候选放宽匹配）=====")
cand = [r for r in cases if r.get("taskType")=="equilibrium" and not r.get("gatewayUsedEngine")]
for r in cand:
    q = r.get("question") or ""
    if any(w in q for w in ["电离","水解","平衡","移动","促进","抑制","稀释"]):
        print(f"  [{r.get('id')}] choice={has_options(q)} 纯对={r.get('pureLlmCorrect')} 网对={r.get('gatewayCorrect')} 期望={(r.get('expected') or '')[:30]}")
        print(f"      Q: {q.replace(chr(10),' ')[:95]}")
