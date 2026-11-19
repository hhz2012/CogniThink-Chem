# -*- coding: utf-8 -*-
"""把探针验证通过的新增规则写入 data/highschool/transformation-rules.json。
去重键 = (from, to, sorted(reagents), sorted(conditions))，已存在则不覆盖（防止矛盾）。
"""
import json
from probe_chain import TEST_RULES
from build_t2t import EXTRA_INFEASIBLE, EXTRA_FEASIBLE

PATH = r'D:\github\CogniThink-Chem\data\highschool\transformation-rules.json'
data = json.load(open(PATH, encoding='utf-8'))
steps = data['steps']

def key(r):
    return (r['from'], r['to'],
            tuple(sorted(r.get('reagents') or [])),
            tuple(sorted(r.get('conditions') or [])))

existing = {key(r) for r in steps}
added, skipped = 0, 0
for r in list(TEST_RULES) + list(EXTRA_INFEASIBLE) + list(EXTRA_FEASIBLE):
    k = key(r)
    if k in existing:
        skipped += 1
        continue
    existing.add(k)
    steps.append(r)
    added += 1

data['steps'] = steps
with open(PATH, 'w', encoding='utf-8') as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write('\n')

print(f'added={added} skipped={skipped} total_now={len(steps)}')
