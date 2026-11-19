# -*- coding: utf-8 -*-
import json

with open(r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json", encoding="utf-8-sig") as f:
    items = json.load(f)

targets = ["pdf-051", "pdf-467", "pdf-468", "pdf-471", "pdf-472"]
by_id = {x.get("id"): x for x in items}

for tid in targets:
    x = by_id.get(tid)
    if not x:
        print(f"!! {tid} not found"); continue
    print(f"\n========== {tid} ==========")
    print(f"  type         = {x.get('type')!r}")
    print(f"  answer       = {x.get('answer')!r}")
    print(f"  answerKeywords = {x.get('answerKeywords')!r}")
    q = (x.get("question") or "")
    print(f"  question(len={len(q)}):")
    print("    " + q.replace("\n", "\n    "))
