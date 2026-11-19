# -*- coding: utf-8 -*-
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
pdf=json.load(open(r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8"))
pdfmap={x["id"]:x for x in pdf}
after=json.load(open(os.path.join(HERE,"after3.json"),encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(after.get(p,[]) or [])
rmap={r["id"]:r for r in rows}
for qid in ["pdf-100","pdf-148","pdf-197","pdf-220","pdf-280","pdf-406"]:
    x=pdfmap[qid]; r=rmap[qid]
    print("="*60)
    print(f"{qid} type={x.get('type')} answer={repr(x.get('answer'))}")
    print(f"  纯LLM答: {(r.get('pureLlmAnswer') or '')[:120]}")
    print(f"  网关答: {(r.get('gatewayAnswer') or '')[:120]}")
    # standalone letter check
    for L in "ABCD":
        if re.search(rf"(?<![a-z]){L.lower()}(?![a-z])", (r.get('pureLlmAnswer') or '').lower()):
            print(f"  >> 纯LLM答中含独立字母 {L}")
