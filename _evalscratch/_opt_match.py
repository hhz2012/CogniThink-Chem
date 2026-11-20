# -*- coding: utf-8 -*-
"""Match answer explanations to each unresolved question by OPTION text overlap."""
import json, glob, re
from collections import Counter

PDF = r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json"
data = json.load(open(PDF, encoding="utf-8"))
by_id = {x["id"]: x for x in data}

AN = []
for f in sorted(glob.glob(r"pdfs/_extract/answer/page_*.json")):
    j = json.load(open(f, encoding="utf-8"))
    pi = j.get("page_idx")
    for a in j.get("answers", []) or []:
        if isinstance(a, dict): a["_page"] = pi; AN.append(a)

UNRES = ["pdf-090","pdf-136","pdf-138","pdf-141","pdf-142","pdf-143","pdf-145"]

def norm(s): return re.sub(r"\s+", "", s or "")
def grams(s, n=4):
    s = norm(s); return set(s[i:i+n] for i in range(max(0,len(s)-n+1)))

def qblob(x):
    parts = [x.get("question") or ""]
    opts = x.get("options") or {}
    if isinstance(opts, dict):
        for k in sorted(opts.keys()): parts.append(opts[k])
    return " ".join(parts)

for qid in UNRES:
    x = by_id[qid]
    qb = qblob(x)
    qg = grams(qb)
    print(f"\n##### {qid}  type={x.get('type')} cur_answer={str(x.get('answer'))!r}")
    print(f"  Q: {norm(x.get('question'))[:120]}")
    opts = x.get("options") or {}
    for k in sorted(opts.keys()): print(f"    {k}: {norm(opts[k])[:90]}")
    scored = []
    for a in AN:
        ag = grams((a.get("explanation") or "") + " " + str(a.get("answer") or ""))
        inter = len(qg & ag)
        if inter > 0:
            scored.append((inter, a))
    scored.sort(key=lambda t: -t[0])
    for s, a in scored[:3]:
        print(f"  >> [overlap={s}] page={a['_page']} num={a.get('num')} ans={str(a.get('answer'))!r}  {norm(a.get('explanation'))[:200]}")
    if not scored: print("  (no overlap)")
