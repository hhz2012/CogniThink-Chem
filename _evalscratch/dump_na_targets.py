# -*- coding: utf-8 -*-
import json, re
ROOT = r"d:\github\CogniThink-Chem"
pdf = json.load(open(ROOT + r"\src\CogniThink.TutorBrain.Api\data\pdf_questions.json", encoding="utf-8"))
items = pdf if isinstance(pdf, list) else pdf.get("items", [])

ids = ["pdf-026","pdf-027","pdf-093","pdf-419","pdf-421","pdf-422","pdf-423","pdf-424","pdf-425","pdf-427"]
want = [x for x in items if x.get("id") in ids]
# also any question whose stem mentions 阿伏伽德罗 / NA
for x in items:
    if x not in want and re.search(r"阿伏伽德罗|阿伏加德罗|设N_A|设NA|用NA表示", x.get("question","")):
        want.append(x)

print(f"count={len(want)}")
for x in want:
    print("="*72)
    print("ID:", x.get("id"), "| answer:", repr(x.get("answer")), "| type:", x.get("type"))
    print("Q:", x.get("question",""))
    for k,v in (x.get("options") or {}).items():
        print(f"  {k}. {v}")
