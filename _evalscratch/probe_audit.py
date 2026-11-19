# -*- coding: utf-8 -*-
import json
TSV = r"D:\github\CogniThink-Chem\_evalscratch\choice_feasibility_audit.tsv"
PDF = r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
with open(PDF, encoding="utf-8") as f: items=json.load(f)
opts_by_id={x["id"]:x.get("options",{}) for x in items}
q_by_id={x["id"]:x.get("question","") for x in items}

rows=[]
with open(TSV, encoding="utf-8") as f:
    hdr=f.readline().rstrip("\n").split("\t")
    for line in f:
        if line.strip(): rows.append(dict(zip(hdr,line.rstrip("\n").split("\t"))))

# 1. dump parseFail option texts (sample)
print("="*70)
print("PARSEFAIL option equations (sample 50):")
print("="*70)
n=0
for r in rows:
    for part in r.get("opts","").split("|"):
        if ":" not in part: continue
        seg=part.split("/")
        if len(seg)==4 and seg[2]=="parseFail":
            L=part.split(":")[0]
            txt=opts_by_id.get(r["id"],{}).get(L,"")
            print(f"  {r['id']:9}[{L}] (ask={r.get('askDir')} ans={r.get('answer')}): {txt}")
            n+=1
            if n>=50: break
    if n>=50: break
print(f"  ... showing {n}")

# count parseFail total
total_pf=0
for r in rows:
    for part in r.get("opts","").split("|"):
        if ":" in part and len(part.split("/"))==4 and part.split("/")[2]=="parseFail": total_pf+=1
print(f"\nTOTAL parseFail options: {total_pf}")

# 2. dump balanced options
print("\n"+"="*70)
print("BALANCED option equations:")
print("="*70)
for r in rows:
    for part in r.get("opts","").split("|"):
        if ":" not in part: continue
        seg=part.split("/")
        if len(seg)==4 and seg[2]=="balanced":
            L=part.split(":")[0]
            print(f"  {r['id']:9}[{L}] (ans={r.get('answer')} ask={r.get('askDir')}): {opts_by_id.get(r['id'],{}).get(L,'')}")

# 3. infeasible rule hits
print("\n"+"="*70)
print("INFEASIBLE rule hits:")
print("="*70)
for r in rows:
    for part in r.get("opts","").split("|"):
        if ":" not in part: continue
        seg=part.split("/")
        if len(seg)==4 and seg[3]!="unknown":
            L=part.split(":")[0]
            print(f"  {r['id']:9}[{L}] rule={seg[3]:30} (ans={r.get('answer')} ask={r.get('askDir')}): {opts_by_id.get(r['id'],{}).get(L,'')}")

# 4. all-ion-group questions: show coexist verdicts
print("\n"+"="*70)
print("ION-GROUP questions (>=2 options type ion) and their coexist verdicts:")
print("="*70)
for r in rows:
    ion_letters=[p.split(":")[0] for p in r.get("opts","").split("|") if ":" in p and len(p.split("/"))==4 and p.split("/")[0].endswith("ion")]
    if len(ion_letters)>=2:
        print(f"\n  {r['id']:9} ask={r.get('askDir')} ans={r.get('answer')} pureLlm={r.get('pureLlmCorrect')}")
        print(f"    Q: {(q_by_id.get(r['id'],'') or '')[:120].replace(chr(10),' ')}")
        for p in r.get("opts","").split("|"):
            if ":" in p:
                seg=p.split("/")
                L=p.split(":")[0]
                print(f"    [{L}] {seg[0].split(':')[1] if ':' in seg[0] else seg[0]:4}/{seg[1]:9}/{seg[2]:10}/{seg[3]:15} :: {opts_by_id.get(r['id'],{}).get(L,'')[:90]}")
