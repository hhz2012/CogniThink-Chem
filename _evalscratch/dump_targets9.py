# -*- coding: utf-8 -*-
import json, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

path = r'd:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json'
with open(path, 'r', encoding='utf-8') as f:
    data = json.load(f)

targets = ['pdf-029','pdf-065','pdf-067','pdf-079','pdf-254','pdf-283','pdf-305','pdf-316','pdf-394']

# Schema: union of top-level keys across all records with counts
from collections import Counter
key_counter = Counter()
for r in data:
    key_counter.update(r.keys())

lines = []
lines.append(f"=== SCHEMA KEYS (camelCase) across all {len(data)} records ===")
for k in sorted(key_counter):
    lines.append(f"{k:<18} count={key_counter[k]}")

lines.append("")
lines.append("=== TARGET RECORDS (verbatim) ===")
by_id = {r['id']: r for r in data}
for tid in targets:
    lines.append("")
    lines.append(f"########## {tid} ##########")
    rec = by_id.get(tid)
    if rec is None:
        lines.append("  (NOT FOUND)")
        continue
    lines.append(f"id: {rec.get('id')}")
    lines.append(f"type: {rec.get('type')}")
    lines.append(f"answer: {rec.get('answer')}")
    lines.append(f"question: {rec.get('question')}")
    kw = rec.get('answerKeywords')
    lines.append("answerKeywords:")
    if kw is None:
        lines.append("  (null)")
    else:
        for w in kw:
            lines.append(f"  - {w}")
    opts = rec.get('options')
    lines.append("options:")
    if opts is None:
        lines.append("  (null)")
    else:
        for k, v in opts.items():
            lines.append(f"  {k}: {v}")
    lines.append(f"note: {rec.get('note')}")

out = '\n'.join(lines)
out_path = r'd:\github\CogniThink-Chem\_evalscratch\dump_targets9_out.txt'
with open(out_path, 'w', encoding='utf-8') as f:
    f.write(out)
print("WROTE", out_path)
