# -*- coding: utf-8 -*-
import json, re, sys
from collections import Counter

PDF = r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json"
AUDIT = r"_evalscratch/choice_audit_full.txt"

data = json.load(open(PDF, encoding="utf-8"))
by_id = {x.get("id"): x for x in data}

# parse audit unsolved list
unsolved = {}  # id -> tags
in_list = False
for line in open(AUDIT, encoding="utf-8"):
    if line.startswith("--- UNSOLVED LIST"):
        in_list = True
        continue
    if not in_list:
        continue
    m = re.match(r"^(pdf-\d+)\s+\[([^\]]*)\]", line)
    if m:
        unsolved[m.group(1)] = [t.strip() for t in m.group(2).split(",") if t.strip()]

print("=== total ===")
print("pdf questions:", len(data), "choice:", sum(1 for x in data if x.get("type")=="choice"))
print("audit unsolved:", len(unsolved))

# inventory: choice with assets
choice_assets = [x for x in data if x.get("type")=="choice" and x.get("imageAssets")]
print("\n=== choice WITH imageAssets:", len(choice_assets), "===")
ac = Counter()
for x in choice_assets:
    for a in x["imageAssets"]:
        ac[a.get("type")] += 1
print("asset types:", dict(ac))

unsolved_with_assets = [x for x in choice_assets if x.get("id") in unsolved]
print("\n=== unsolved choice WITH assets (increment candidates):", len(unsolved_with_assets), "===")
print("asset type breakdown among these:")
bc = Counter()
for x in unsolved_with_assets:
    for a in x["imageAssets"]:
        bc[a.get("type")] += 1
print(" ", dict(bc))

def short(s, n=90):
    s = re.sub(r"\s+", " ", s or "")
    return s[:n]

print("\n--- unsolved+assets detail (id | assetTypes | tags | ans | stem | opts) ---")
for x in sorted(unsolved_with_assets, key=lambda z: z.get("id","")):
    atypes = ",".join(sorted(set(a.get("type","?") for a in x["imageAssets"])))
    tags = ",".join(unsolved.get(x["id"],[]))
    opts = x.get("options") or {}
    ostr = " || ".join(f"{k}:{short(v,50)}" for k,v in sorted(opts.items()))
    print(f"{x['id']}\t[{atypes}]\t<{tags}>\tans={x.get('answer')}\t{short(x.get('question'),110)}\t{ostr}")

# OCR残缺: NOLETTER / OPTS0
print("\n\n=== OCR残缺 (NOLETTER / OPTS0) ===")
ocr_ids = [i for i,t in unsolved.items() if any(g in t for g in ("NOLETTER","OPTS0"))]
print("count:", len(ocr_ids))
for i in sorted(ocr_ids):
    x = by_id.get(i)
    if not x: 
        print(i, "(not in json)"); continue
    tags = ",".join(unsolved.get(i,[]))
    opts = x.get("options") or {}
    print(f"\n--- {i} <{tags}> ans={x.get('answer')!r} type={x.get('type')} opts={list(opts.keys())} ---")
    print("  Q:", short(x.get("question"), 200))
    for k,v in sorted(opts.items()):
        print(f"    {k}: {short(v,120)}")
