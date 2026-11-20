# -*- coding: utf-8 -*-
import json
QP = r"pdfs/questions_parsed.json"
raw = open(QP, encoding="utf-8").read()
print("questions_parsed.json size:", len(raw))
try:
    d = json.loads(raw)
except Exception as e:
    print("parse err:", e); 
    # maybe list of lines / other shape
    import re
    print(raw[:500])
    raise SystemExit
print("top type:", type(d).__name__)
if isinstance(d, dict):
    print("keys:", list(d.keys())[:20])
elif isinstance(d, list):
    print("len:", len(d), "first keys:", list(d[0].keys()) if d and isinstance(d[0],dict) else d[0])

# find by id-ish
def find(qid):
    if isinstance(d, list):
        for x in d:
            if isinstance(x,dict) and (x.get("id")==qid or str(x.get("id",""))==qid or qid in str(x.get("id",""))):
                return x
    return None

for qid in ["pdf-141","pdf-226","pdf-255","pdf-135","pdf-009","pdf-117"]:
    x = find(qid)
    print(f"\n--- {qid} ---")
    if x is None:
        print("  NOT FOUND"); continue
    print("  keys:", list(x.keys()))
    for k,v in x.items():
        s = json.dumps(v, ensure_ascii=False) if not isinstance(v,str) else v
        print(f"  {k}: {s[:200]}")
