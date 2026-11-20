# -*- coding: utf-8 -*-
"""Recover pdf-255 missing options from the actual PDF page image (page 90),
verified against the note's A/B/C/D descriptions. Only touches 'options'.
"""
import json
PDF = r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json"
data = json.load(open(PDF, encoding="utf-8"))
for q in data:
    if q["id"] == "pdf-255":
        assert q["type"] == "choice" and q["answer"] == "C", (q["type"], q["answer"])
        assert not q.get("options"), q.get("options")
        q["options"] = {
            "A": "L₄ 处与 L₅ 处反应①的平衡常数 K 相等",
            "B": "反应②的焓变 ΔH₂＞0",
            "C": "L₆ 处的 H₂O 的体积分数大于 L₅ 处",
            "D": "混合气从起始到通过 L₁ 处，CO 的生成速率小于 CH₃OH 的生成速率",
        }
        print("patched pdf-255 options:", q["options"])
        break
with open(PDF, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("done")
