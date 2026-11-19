# -*- coding: utf-8 -*-
"""Append the 24 verified t2t- chain questions into tier2_questions.json.
Reuses draft_t2t.Q (already 24/24 verified against the mirror) so the shipped
questions are byte-for-byte the ones that passed.
"""
import sys, json, io
sys.path.insert(0, r"d:\github\CogniThink-Chem\_evalscratch")
import draft_t2t

PATH = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\tier2_questions.json"
items = json.load(open(PATH, encoding="utf-8"))
existing = {it.get("id") for it in items}

added = 0
for q in draft_t2t.Q:
    if q["id"] in existing:
        continue
    items.append({
        "id": q["id"],
        "question": q["question"],
        "options": q["options"],
        "answer": q["answer"],
        "answerKeywords": [q["answer"]],
        "type": "choice",
        "note": "Tier 2-B 物质转化链选择题（规则库查表，TransformationChainValidator 确定性求解）",
    })
    existing.add(q["id"])
    added += 1

with io.open(PATH, "w", encoding="utf-8") as f:
    json.dump(items, f, ensure_ascii=False, indent=2)
    f.write("\n")

print(f"appended {added} t2t items; total now {len(items)}")
