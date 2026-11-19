# -*- coding: utf-8 -*-
"""用仅文件规则（无内存补丁）复测 24 t2t + 5 pdf，全部通过后把 t2t 写入 tier2_questions.json。"""
import json
import re
import probe_chain as pc
from build_t2t import Q

RULES_PATH = r'D:\github\CogniThink-Chem\data\highschool\transformation-rules.json'
TIER2_PATH = r'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\tier2_questions.json'
PDF_PATH = r'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json'

# 仅用文件里的规则（模拟 C# RuleStore 运行时加载），覆盖 probe_chain 的 RULES
file_rules = json.load(open(RULES_PATH, encoding='utf-8'))['steps']
pc.RULES = file_rules
print(f'file-only rules: {len(file_rules)}')

def verify(questions, label):
    ok, bad = 0, []
    for q in questions:
        d, info = pc.decide(q['question'], q['options'])
        exp = q['answer']
        if d == exp:
            ok += 1
        else:
            bad.append((q['id'], exp, d, info))
        print(f"  {q['id']:10s} exp={exp} got={str(d):5} {'OK' if d==exp else 'FAIL'} {info}")
    print(f'[{label}] PASS {ok}/{len(questions)}')
    return bad

# pdf 子集的“期望值”是校验器应返回的字母（None=保守不答，红线要求），不是题库答案
PDF_EXPECT = {'pdf-013': 'D', 'pdf-015': None, 'pdf-016': 'D', 'pdf-017': None, 'pdf-144': None}
PDF = json.load(open(PDF_PATH, encoding='utf-8'))
pdf_subset = []
for id_ in ['pdf-013','pdf-015','pdf-016','pdf-017','pdf-144']:
    it = next(x for x in PDF if x['id'] == id_)
    pdf_subset.append({'id': id_, 'question': it['question'], 'options': it.get('options') or {},
                       'answer': PDF_EXPECT[id_]})

bad = verify(pdf_subset, 'pdf subset') + verify(Q, 't2t')
if bad:
    print('\n!!! VERIFICATION FAILED, not writing tier2_questions.json')
    for b in bad: print('BAD', b)
    raise SystemExit(1)

# 写入 tier2_questions.json：先剔除旧的 t2t-*（上一会话遗留，已针对旧规则集），再写入刚验证通过的 Q
data = json.load(open(TIER2_PATH, encoding='utf-8'))
before = len(data)
data = [x for x in data if not x['id'].startswith('t2t-')]
removed = before - len(data)
for q in Q:
    data.append({
        'id': q['id'],
        'question': q['question'],
        'options': q['options'],
        'answer': q['answer'],
        'answerKeywords': [q['answer']],
        'type': 'choice',
        'note': 'Tier 2-B 物质转化链选择题（TransformationChainValidator 确定性求解，不靠 LLM）',
    })
with open(TIER2_PATH, 'w', encoding='utf-8') as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write('\n')
print(f'\nwrote t2t: removed_old={removed} added_new={len(Q)} total_now={len(data)}')
