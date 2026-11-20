# -*- coding: utf-8 -*-
"""Final verification: dump exercise pages for unresolved Qs + search answers for
distinctive phrases to lock pdf-143/145/090 answers."""
import json, glob, os, sys

PARSED = r"pdfs/questions_parsed.json"
parsed = json.load(open(PARSED, encoding="utf-8"))
pmeta = {x["id"]: x for x in parsed}

def load_ex_page(pi):
    for f in sorted(glob.glob(r"pdfs/_extract/exercise/page_*.json")):
        j = json.load(open(f, encoding="utf-8"))
        if j.get("page_idx") == pi: return j
    return None

for qid in ["pdf-090","pdf-136","pdf-138","pdf-141","pdf-142","pdf-143","pdf-145"]:
    m = pmeta.get(qid, {})
    sp, num = m.get("src_page"), m.get("num")
    print(f"\n##### {qid} src_page={sp} num={num}")
    j = load_ex_page(sp)
    if not j: print("  exercise page not found"); continue
    for it in j.get("questions", []) or []:
        if str(it.get("num")) == str(num):
            print(f"  Q: {it.get('question')}")
            print(f"  options: {json.dumps(it.get('options'), ensure_ascii=False)}")

# search answer explanations for distinctive phrases
AN = []
for f in sorted(glob.glob(r"pdfs/_extract/answer/page_*.json")):
    j = json.load(open(f, encoding="utf-8")); pi=j.get("page_idx")
    for a in j.get("answers",[]) or []:
        if isinstance(a,dict): a["_page"]=pi; AN.append(a)

for kw in ["红色花瓣","Br2的CCl4","烧瓶壁发热","98%","Na2S2O3 + H2SO4 =","S2O3","70%的硫酸","漂白草帽","饱和CuSO4","2.3 g","2.3g"]:
    print(f"\n===== search '{kw}' =====")
    n=0
    for a in AN:
        blob=(a.get("explanation") or "")+" "+str(a.get("answer") or "")
        if kw in blob:
            print(f"  page={a['_page']} num={a.get('num')} ans={str(a.get('answer'))!r}: {str(a.get('explanation'))[:220]!r}")
            n+=1
            if n>=4: break
    if n==0: print("  (none)")
