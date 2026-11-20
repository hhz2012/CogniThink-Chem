# -*- coding: utf-8 -*-
import json, re
from collections import Counter

PDF = r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json"
data = json.load(open(PDF, encoding="utf-8"))
by_id = {x.get("id"): x for x in data}

AUDIT = r"_evalscratch/choice_audit_full.txt"
unsolved = {}
in_list = False
for line in open(AUDIT, encoding="utf-8"):
    if line.startswith("--- UNSOLVED LIST"):
        in_list = True; continue
    if not in_list: continue
    m = re.match(r"^(pdf-\d+)\s+\[([^\]]*)\]", line)
    if m:
        unsolved[m.group(1)] = [t.strip() for t in m.group(2).split(",") if t.strip()]

cands = [x for x in data if x.get("type")=="choice" and x.get("imageAssets") and x.get("id") in unsolved]
print("unsolved choice with assets:", len(cands))

def probe(asset):
    t = asset.get("type")
    p = asset.get("payload") or {}
    if t == "molecule":
        return f"valid={p.get('valid')} smiles={'Y' if p.get('smiles') else 'N'} name={p.get('name','')!r}"
    if t == "curve":
        # try common shapes
        keys = list(p.keys())
        n = None
        for k in ("points","data","series","values"):
            if k in p:
                v=p[k]
                n = len(v) if isinstance(v,(list,dict)) else v
        return f"keys={keys} n={n}"
    if t == "graph":
        keys = list(p.keys())
        nn = len(p.get("nodes",[])) if isinstance(p.get("nodes"),list) else None
        ne = len(p.get("edges",[])) if isinstance(p.get("edges"),list) else None
        return f"keys={keys} nodes={nn} edges={ne}"
    return f"keys={list(p.keys())}"

print("\n=== per-candidate asset probe ===")
for x in sorted(cands, key=lambda z: z.get("id","")):
    parts = []
    for a in x["imageAssets"]:
        parts.append(f"{a.get('type')}:{probe(a)}")
    print(f"{x['id']}\t" + " | ".join(parts))

# overall payload validity
print("\n=== validity summary ===")
for t in ("molecule","curve","graph"):
    tot=valid=0
    for x in cands:
        for a in x["imageAssets"]:
            if a.get("type")!=t: continue
            tot+=1
            p=a.get("payload") or {}
            if t=="molecule":
                if p.get("valid") and p.get("smiles"): valid+=1
            elif t=="curve":
                if any(k in p for k in ("points","data","series","values")): valid+=1
            elif t=="graph":
                if isinstance(p.get("nodes"),list) and p["nodes"]: valid+=1
    print(f"{t}: total={tot} payload-valid={valid}")
