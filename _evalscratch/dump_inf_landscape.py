# -*- coding: utf-8 -*-
import json, csv
from collections import defaultdict

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

def parse_opts(r):
    verdicts = {}
    for p in r.get("opts","").split("|"):
        if ":" in p:
            L, rest = p.split(":", 1)
            seg = rest.split("/")
            if len(seg) == 4:
                verdicts[L] = {"type":seg[0],"coexist":seg[1],"balanced":seg[2],"inf":seg[3]}
    return verdicts

# 只看单字母答案
single = {k:v for k,v in rows.items() if v["answer"] in {"A","B","C","D"}}

print("="*90)
print("所有出现 >=1 条 infeasible 规则命中的题目（按 askDir 分组）")
print("="*90)
by_ask = defaultdict(list)
for qid, r in single.items():
    v = parse_opts(r)
    infs = {L:vv["inf"] for L,vv in v.items() if vv["inf"] != "unknown"}
    if infs:
        by_ask[r["askDir"]].append((qid, r, v, infs))

for ask in ["wrong","correct","?"]:
    print(f"\n----- askDir={ask}  (共 {len(by_ask[ask])} 题有命中) -----")
    for qid, r, v, infs in sorted(by_ask[ask]):
        it = by_id.get(qid, {})
        opts = it.get("options") or {}
        letters = sorted(opts.keys())
        bal_count = sum(1 for L in letters if v.get(L,{}).get("balanced")=="balanced")
        unbal_count = sum(1 for L in letters if v.get(L,{}).get("balanced")=="unbalanced")
        pf_count = sum(1 for L in letters if v.get(L,{}).get("balanced")=="parseFail")
        n_eq = sum(1 for L in letters if v.get(L,{}).get("type")=="eq")
        tag = ""
        if ask == "wrong" and len(infs) == 1:
            L = list(infs.keys())[0]
            tag = f"  <<< 唯一不可行={L} 答案={r['answer']} {'CORRECT' if L==r['answer'] else 'WRONG'} >>>"
        elif ask == "correct" and len(infs) == 3 and n_eq == 4:
            rest = [L for L in letters if L not in infs]
            L = rest[0] if rest else "?"
            tag = f"  <<< 3不可行 剩余={L} 答案={r['answer']} {'CORRECT' if L==r['answer'] else 'WRONG'} >>>"
        elif ask == "wrong" and len(infs) == 2:
            tag = "  (2命中，近)"
        elif ask == "correct" and len(infs) == 2:
            tag = "  (2命中，需再1条)"
        print(f"  {qid} ans={r['answer']} ask={r['askDir']} eq={n_eq} bal={bal_count} unbal={unbal_count} pf={pf_count} inf={len(infs)}{tag}")
        for L in letters:
            vv = v.get(L, {})
            mark = f"[{vv.get('inf','?')[:18]}]" if vv.get("inf")!="unknown" else ""
            print(f"      {L}: {vv.get('type')}/{vv.get('balanced')} {mark}  {opts[L][:55]}")
