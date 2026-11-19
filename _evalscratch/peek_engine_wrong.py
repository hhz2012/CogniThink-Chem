# -*- coding: utf-8 -*-
import json
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
data=json.load(open(HERE+"\\engine_wrong_full.json",encoding="utf-8"))
by={o["id"]:o for o in data}
for q in ["pdf-044","pdf-294","pdf-045","pdf-051","pdf-116","pdf-134","pdf-164","pdf-527","pdf-414","pdf-475","pdf-085"]:
    o=by.get(q)
    if not o: continue
    print("="*72)
    print(f"[{q}] task={o['task']} tags={o['tags']}")
    print(f"EXPECTED: {o['expected'][:300]}")
    print(f"QUESTION: {o['question'][:600]}")
    print(f"GW_CONCL: {o['gateway_conclusion'][:400]}")
    dv=o.get("derivation")
    if isinstance(dv,list): dv=" | ".join(str(x) for x in dv)
    print(f"DERIV   : {(str(dv) or '')[:500]}")
    print()
