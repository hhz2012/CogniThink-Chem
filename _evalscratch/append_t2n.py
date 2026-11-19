# -*- coding: utf-8 -*-
"""Append the 20 verified t2n- NA questions into tier2_questions.json."""
import sys, json, io
sys.path.insert(0, r"d:\github\CogniThink-Chem\_evalscratch")
import draft_t2n

PATH = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\tier2_questions.json"
items = json.load(open(PATH, encoding="utf-8"))
existing = {it.get("id") for it in items}
added = 0
for q in draft_t2n.Q:
    if q["id"] in existing: continue
    items.append({
        "id": q["id"], "question": q["question"], "options": q["options"],
        "answer": q["answer"], "answerKeywords": [q["answer"]], "type": "choice",
        "note": "Tier 2-C NA 粒子计数选择题（事实库查表，NaChoiceValidator 保守确定性求解）",
    })
    existing.add(q["id"]); added += 1
with io.open(PATH, "w", encoding="utf-8") as f:
    json.dump(items, f, ensure_ascii=False, indent=2); f.write("\n")
print(f"appended {added} t2n items; total now {len(items)}")
