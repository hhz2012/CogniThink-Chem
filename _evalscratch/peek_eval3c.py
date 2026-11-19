# -*- coding: utf-8 -*-
import json, sys
sys.stdout.reconfigure(encoding='utf-8')

path = r"d:\github\CogniThink-Chem\_evalscratch\after_stage3c.json"
with open(path, encoding='utf-8') as f:
    data = json.load(f)

# gather all case buckets
buckets = {}
for k,v in data.items():
    if isinstance(v, list) and v and isinstance(v[0], dict) and 'question' in v[0]:
        buckets[k] = v
print("buckets:", {k:len(v) for k,v in buckets.items()})

targets = {
    'pdf-051': '判断下列离子方程式书写的正误',
    'pdf-468': '用氢氟酸雕刻玻璃',
    'pdf-471': '盛放强碱溶液不能用磨口玻璃塞',
    'pdf-472': '用纯碱制备普通玻璃的主要反应',
    'pdf-467': 'SiO2可与氢氟酸反应,故SiO2为碱性氧化物',
}
for cid, needle in targets.items():
    found=None; bucket=None
    for bk, lst in buckets.items():
        for r in lst:
            if needle in (r.get('question') or ''):
                found=r; bucket=bk; break
        if found: break
    print("="*70)
    print(cid, "->", bucket or "NOT FOUND")
    if found:
        for k in ('id','taskType','gatewayCorrect','pureLlmCorrect','gatewayTruthAdopted',
                  'gatewayUsedEngine','gatewayOk','gatewayAnswer','gatewayConclusion','expected','note'):
            if k in found:
                print(f"  {k}: {found[k]!r}")
