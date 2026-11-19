# -*- coding: utf-8 -*-
import json, io, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
PATH = r"D:\github\CogniThink-Chem\_evalscratch\after5.json"
data = json.load(open(PATH, encoding="utf-8-sig"))
print("TOP KEYS:", list(data.keys()))
for k,v in data.items():
    if isinstance(v, list):
        print(f"  list {k}: len={len(v)} first_keys={list(v[0].keys()) if v else None}")
    else:
        print(f"  scalar {k}: {v}")
