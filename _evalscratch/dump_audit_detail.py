# -*- coding: utf-8 -*-
import json, csv

TSV = r"D:\github\CogniThink-Chem\_evalscratch\choice_feasibility_audit.tsv"
PDF = r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"

rows = {}
with open(TSV, encoding="utf-8") as f:
    hdr = f.readline().rstrip("\n").split("\t")
    for line in f:
        if not line.strip(): continue
        r = dict(zip(hdr, line.rstrip("\n").split("\t")))
        rows[r["id"]] = r

with open(PDF, encoding="utf-8") as f:
    items = json.load(f)
by_id = {it["id"]: it for it in items}

TARGETS = ["pdf-040","pdf-041","pdf-042","pdf-043","pdf-046","pdf-049","pdf-059",
           "pdf-102","pdf-110","pdf-092"]

out = []
for tid in TARGETS:
    r = rows.get(tid)
    it = by_id.get(tid)
    if not r or not it:
        out.append(f"===== {tid}: MISSING =====")
        continue
    out.append(f"===== {tid} =====")
    out.append(f"  answer={r['answer']}  askDir={r['askDir']}  pureLlmCorrect={r['pureLlmCorrect']}")
    out.append(f"  Q: {(it.get('question') or '')[:300]}")
    opts = it.get("options") or {}
    # parse optStr:  A:eq/coexist/balanced/infeasible|B:...
    parts = r.get("opts","").split("|")
    verdicts = {}
    for p in parts:
        if ":" in p:
            L, rest = p.split(":", 1)
            seg = rest.split("/")
            if len(seg) == 4:
                verdicts[L] = seg
    for L in sorted(opts.keys()):
        v = verdicts.get(L, ["?","?","?","?"])
        out.append(f"  [{L}] type={v[0]} coexist={v[1]} balanced={v[2]} infeasible={v[3]}")
        out.append(f"      text: {opts[L]}")
    out.append(f"  >> combinedDecided={r['combinedDecided']} combinedPred={r.get('combinedPred','')} combinedCorrect={r['combinedCorrect']}")
    out.append(f"  >> coexist={r['coexistDecided']}/{r.get('coexistPred','')} conserv={r['conservDecided']}/{r.get('conservPred','')} fease={r['feaseDecided']}/{r.get('feasePred','')}")
    out.append("")

print("\n".join(out))
