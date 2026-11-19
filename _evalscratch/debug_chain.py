# -*- coding: utf-8 -*-
import json, re
PDF = json.load(open(r'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json', encoding='utf-8'))
def get(id_):
    for it in PDF:
        if it['id']==id_: return it
    raise KeyError(id_)
for id_ in ['pdf-013','pdf-015','pdf-016','pdf-017','pdf-144']:
    it = get(id_)
    print('='*70)
    print(id_, 'answer=', repr(it.get('answer')))
    print('Q:', it['question'][:200])
    opts = it.get('options') or {}
    for L in ['A','B','C','D']:
        if L in opts:
            print(f'  [{L}] {opts[L]!r}')
