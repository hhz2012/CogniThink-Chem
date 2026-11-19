# -*- coding: utf-8 -*-
import json
TSV = r"D:\github\CogniThink-Chem\_evalscratch\choice_feasibility_audit.tsv"
rows=[]
with open(TSV, encoding="utf-8") as f:
    hdr=f.readline().rstrip("\n").split("\t")
    for line in f:
        if not line.strip(): continue
        rows.append(dict(zip(hdr,line.rstrip("\n").split("\t"))))

def b(v): return v=="True" or v=="Y"
def rb(r,k): return b(r.get(k,""))

single = [r for r in rows if r["answer"] in "ABCD" and len(r["answer"])==1]
print(f"total choice rows: {len(rows)}; single-letter answer: {len(single)}")

def show_path(name, dec_key, pred_key, ok_key):
    dec = [r for r in single if rb(r, dec_key)]
    correct = [r for r in dec if rb(r, ok_key)]
    wrong = [r for r in dec if not rb(r, ok_key)]
    pl_right = [r for r in dec if rb(r, "pureLlmCorrect")]
    pl_wrong = [r for r in dec if not rb(r, "pureLlmCorrect")]
    gain = [r for r in correct if not rb(r, "pureLlmCorrect")]
    harm_risk = [r for r in wrong if rb(r, "pureLlmCorrect")]
    print(f"\n=== {name} ===")
    print(f"  decidable: {len(dec)}; correct: {len(correct)}; wrong: {len(wrong)}; precision={len(correct)}/{len(dec)}")
    print(f"  of decidable: pureLlm already correct={len(pl_right)}, pureLlm wrong={len(pl_wrong)}")
    print(f"  GAIN (engine correct & pureLlm wrong): {len(gain)}")
    print(f"  HARM RISK (engine wrong & pureLlm correct): {len(harm_risk)}")
    for r in dec:
        tag = "OK" if rb(r, ok_key) else "WRONG"
        print(f"    {r['id']:10} ans={r['answer']} pred={r.get(pred_key,''):2} [{tag}] ask={r.get('askDir','?'):7} pureLlm={r.get('pureLlmCorrect','')}")
    return dec, correct, wrong

c_dec,c_cor,c_wro = show_path("COEXIST (ion groups all-4)","coexistDecided","coexistPred","coexistCorrect")
v_dec,v_cor,v_wro = show_path("CONSERVATION","conservDecided","conservPred","conservCorrect")
f_dec,f_cor,f_wro = show_path("FEASIBILITY","feaseDecided","feasePred","feaseCorrect")

# combinedDecided 存的是路径名(coexist/fease/conserv)，非空即采用
comb_dec = [r for r in single if r.get("combinedDecided","") != ""]
comb_cor = [r for r in comb_dec if rb(r,"combinedCorrect")]
comb_wro = [r for r in comb_dec if not rb(r,"combinedCorrect")]
print(f"\n=== COMBINED (any path) ===")
print(f"  decidable: {len(comb_dec)}; correct: {len(comb_cor)}; wrong: {len(comb_wro)}")
gain=[r for r in comb_cor if not rb(r,"pureLlmCorrect")]
harm=[r for r in comb_wro if rb(r,"pureLlmCorrect")]
print(f"  GAIN={len(gain)}  HARM_RISK={len(harm)}")
for r in comb_dec:
    tag="OK" if rb(r,"combinedCorrect") else "WRONG"
    print(f"    {r['id']:10} ans={r['answer']} pred={r.get('combinedPred',''):2} [{tag}] via={r.get('combinedDecided','')} ask={r.get('askDir','?')}")

# infeasible rule firing distribution
from collections import Counter
rule_ctr=Counter()
for r in single:
    for part in r.get("opts","").split("|"):
        if ":" in part:
            seg=part.split("/")
            if len(seg)==4 and seg[3]!="unknown":
                rule_ctr[seg[3].split(":")[0]]+=1
print("\n=== infeasible rule firing counts (per option across all single-answer choices) ===")
for k,v in rule_ctr.most_common(): print(f"  {k}: {v}")

# how many equation options are balanced vs unbalanced (sanity)
bal=unbal=pfail=noeq=0
for r in single:
    for part in r.get("opts","").split("|"):
        if ":" in part:
            seg=part.split("/")
            if len(seg)==4:
                if seg[2]=="balanced": bal+=1
                elif seg[2]=="unbalanced": unbal+=1
                elif seg[2]=="parseFail": pfail+=1
                else: noeq+=1
print(f"\nequation options: balanced={bal}, unbalanced={unbal}, parseFail={pfail}, no-eq={noeq}")

# combined precision bar for AC-12
if comb_dec:
    print(f"\nAC-12 check (combined): intervention={len(comb_dec)} (need>=5), correct={len(comb_cor)} precision={len(comb_cor)/len(comb_dec)*100:.0f}% (need>=60%)")
