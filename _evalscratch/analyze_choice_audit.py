# -*- coding: utf-8 -*-
from collections import Counter
import json
rows=[]
with open(r"D:\github\CogniThink-Chem\_evalscratch\choice_audit.tsv", encoding="utf-8") as f:
    hdr=f.readline().rstrip("\n").split("\t")
    for line in f:
        rows.append(dict(zip(hdr,line.rstrip("\n").split("\t"))))

print("columns:", hdr)
total=len(rows)
decidable=[r for r in rows if r["decided"]]
correct=[r for r in decidable if r["correct"]=="True"]
print(f"\ntotal choice: {total}")
print(f"#valid dist: {Counter(len(r['valid']) for r in rows)}")
print(f"#invalid dist: {Counter(len(r['invalid']) for r in rows)}")
print(f"#unparsable dist: {Counter(len(r['unparsable']) for r in rows)}")
all_parsed=[r for r in rows if len(r["unparsable"])==0]
print(f"all 4 options parsed (extractable+parseable): {len(all_parsed)}/{total}")
print(f"with >=1 equation option parsed (valid or invalid nonempty): {sum(1 for r in rows if r['valid'] or r['invalid'])}")
print(f"\nconservation-UNIQUELY decidable: {len(decidable)}  (correct={len(correct)})")
for r in decidable:
    print("  ", r["id"], "ans=",r["answer"],"askWrong=",r["askWrong"],"valid=",r["valid"],"invalid=",r["invalid"],"decided=",r["decided"],"correct=",r["correct"])

with open(r"D:\github\CogniThink-Chem\_evalscratch\after_stage3a.json", encoding="utf-8-sig") as f:
    ev=json.load(f)
meta={}
for p in ("bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"):
    for c in ev.get(p,[]): meta[c["id"]]=c
pl_right=sum(1 for r in decidable if meta.get(r["id"],{}).get("pureLlmCorrect"))
print(f"\ndecidable where pureLlm already correct: {pl_right}/{len(decidable)}")
