# -*- coding: utf-8 -*-
import json, sys
io = sys.stdout.reconfigure(encoding='utf-8') if hasattr(sys.stdout, 'reconfigure') else None

path = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
with open(path, encoding='utf-8') as f:
    data = json.load(f)

cases = data if isinstance(data, list) else data.get('cases') or data.get('questions') or list(data.values())
print("total cases:", len(cases))
targets = {'pdf-051','pdf-468','pdf-471','pdf-472','pdf-467'}
for c in cases:
    cid = c.get('id') or c.get('caseId') or c.get('pdfId') or ''
    if cid in targets:
        print("="*80)
        print("ID:", cid)
        for k in ('type','taskType','answer','answerKeywords','expectedAffirmative'):
            if k in c:
                v = c[k]
                if isinstance(v,str):
                    print(f"  {k}: {v!r}")
                else:
                    print(f"  {k}: {v}")
        q = c.get('question') or ''
        print("  question repr:")
        print(repr(q))
        print("  question text:")
        print(q)
