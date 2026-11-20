# -*- coding: utf-8 -*-
"""Recover missing options for pdf-226 (page 80 image) and pdf-141 (page 47 image),
verified against source page images / chemistry. Only touches 'options'."""
import json
PDF = r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json"
data = json.load(open(PDF, encoding="utf-8"))

PATCHES = {
    "pdf-226": {
        "A": "环六糊精属于寡糖",
        "B": "非极性分子均可被环六糊精包合形成超分子",
        "C": "图 2 中甲氧基暴露在反应环境中",
        "D": "可用萃取法分离环六糊精和氯代苯甲醚",
    },
    "pdf-141": {
        "A": "M 与 Y 反应中，M 起氧化作用",
        "B": "金属钠着火可以用 X 扑灭",
        "C": "将 W 气体通入紫色石蕊溶液，溶液先变红后褪色",
        "D": "工业上用水吸收 Z 制备 M",
    },
}

for q in data:
    qid = q["id"]
    if qid in PATCHES:
        print(f"--- {qid} (type={q['type']}, answer={q['answer']!r}) ---")
        print("  old options:", q.get("options"))
        q["options"] = PATCHES[qid]
        print("  new options:", q["options"])

with open(PDF, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("done")
