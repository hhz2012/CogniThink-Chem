# -*- coding: utf-8 -*-
"""Scan FULL bank (pdf + tier2) with the NA mirror. Any non-null decision that
disagrees with gold = false positive (must be zero). Nulls are safe.
"""
import sys, json
sys.path.insert(0, r"d:\github\CogniThink-Chem\_evalscratch")
from verify_t2n import decide, render

ROOT = r"d:\github\CogniThink-Chem"
pdf = json.load(open(ROOT+r"\src\CogniThink.TutorBrain.Api\data\pdf_questions.json", encoding="utf-8"))
pdf_items = pdf if isinstance(pdf,list) else pdf.get("items",[])
t2 = json.load(open(ROOT+r"\src\CogniThink.TutorBrain.Api\data\tier2_questions.json", encoding="utf-8"))

def gold(x):
    a=(x.get("answer") or "").strip()
    for ch in a:
        if ch in "ABCD": return ch
    for k in (x.get("answerKeywords") or []):
        k=k.strip()
        if len(k)==1 and k in "ABCD": return k
    return ""

mismatch=[]; null=0; solved=[]
for src,items in [("pdf",pdf_items),("tier2",t2)]:
    for x in items:
        if (x.get("type") or "choice")!="choice": continue
        opts=x.get("options") or {}
        if len(opts)<2: continue
        q,o=render(x)
        letter,info=decide(q,o)
        g=gold(x)
        if letter is None: null+=1
        elif g and letter!=g: mismatch.append((src,x.get("id"),g,letter,info[:60],x.get("question","")[:50]))
        elif letter==g: solved.append((src,x.get("id")))

print(f"solved(correct)={len(solved)}  null(safe)={null}  MISMATCH(false positive)={len(mismatch)}")
for m in mismatch: print("  MISMATCH",m)
print("--- solved ---")
for s in solved: print("  ",s)
