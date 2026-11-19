# -*- coding: utf-8 -*-
"""Scan the FULL question bank with the t2t mirror. Any non-null decision that
disagrees with the gold answer = false positive (must be zero). Nulls are safe.
Also count how many pdf questions the mirror newly solves (reward, not required).
"""
import sys, json
sys.path.insert(0, r"d:\github\CogniThink-Chem\_evalscratch")
from verify_t2t import decide, render

ROOT = r"d:\github\CogniThink-Chem"
pdf = json.load(open(ROOT + r"\src\CogniThink.TutorBrain.Api\data\pdf_questions.json", encoding="utf-8"))
pdf_items = pdf if isinstance(pdf, list) else pdf.get("items", [])
t2 = json.load(open(ROOT + r"\src\CogniThink.TutorBrain.Api\data\tier2_questions.json", encoding="utf-8"))

def gold(x):
    a = (x.get("answer") or "").strip()
    # letter extraction: first standalone A-D
    for ch in a:
        if ch in "ABCD": return ch
    kws = x.get("answerKeywords") or []
    for k in kws:
        k = k.strip()
        if len(k) == 1 and k in "ABCD": return k
    return ""

mismatch = []
null = 0
solved = []
for src, items in [("pdf", pdf_items), ("tier2", t2)]:
    for x in items:
        if (x.get("type") or "choice") != "choice": 
            continue
        opts = x.get("options") or {}
        if len(opts) < 2: continue
        q, o = render(x)
        letter, info = decide(q, o)
        g = gold(x)
        if letter is None:
            null += 1
        elif g and letter != g:
            mismatch.append((src, x.get("id"), g, letter, info, x.get("question","")[:40]))
        elif letter == g:
            solved.append((src, x.get("id")))

print(f"solved(correct)={len(solved)}  null(safe)={null}  MISMATCH(false positive)={len(mismatch)}")
for m in mismatch:
    print("  MISMATCH", m)
print("--- solved list (first 40) ---")
for s in solved[:40]:
    print("  ", s)
