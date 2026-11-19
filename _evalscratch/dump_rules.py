# -*- coding: utf-8 -*-
import json
d = json.load(open(r'D:\github\CogniThink-Chem\data\highschool\transformation-rules.json', encoding='utf-8'))['steps']
lines = []
for r in d:
    lines.append(f"{r['from']} -> {r['to']}  regs={r.get('reagents')} conds={r.get('conditions')} feasible={r.get('feasible')}")
open(r'D:\github\CogniThink-Chem\_evalscratch\rules_dump.txt', 'w', encoding='utf-8').write('\n'.join(lines))
print('dumped', len(d))
