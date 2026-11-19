# -*- coding: utf-8 -*-
"""Dump NA-style pdf questions to understand option format for NaChoiceValidator."""
import json, re
ROOT = r"d:\github\CogniThink-Chem"
pdf = json.load(open(ROOT + r"\src\CogniThink.TutorBrain.Api\data\pdf_questions.json", encoding="utf-8"))
items = pdf if isinstance(pdf, list) else pdf.get("items", [])

TARGETS = ["pdf-026","pdf-093","pdf-419","pdf-421","pdf-422","pdf-423","pdf-424","pdf-425","pdf-427"]
hits = []
for x in items:
    q = x.get("question","") + " " + " ".join((x.get("options") or {}).values())
    if x.get("id") in TARGETS or re.search(r"N_A|NA|阿伏伽德罗|阿伏加德罗|物质的量|mol", q):
        hits.append(x)

print(f"total NA-ish hits: {len(hits)}")
for x in hits[:30]:
    print("="*70)
    print("ID:", x.get("id"), "| answer:", x.get("answer"), "| type:", x.get("type"))
    print("Q:", x.get("question","")[:160])
    for k,v in (x.get("options") or {}).items():
        print(f"  {k}. {v}")
