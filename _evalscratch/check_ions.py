# -*- coding: utf-8 -*-
import json, io
p = r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\chemistry\ions.json"
d = json.load(io.open(p, encoding="utf-8-sig"))
print("type:", type(d).__name__)
if isinstance(d, dict):
    print("keys:", list(d.keys()))
    for k in ("cations","anions","gases"):
        v = d.get(k)
        if isinstance(v, list):
            hits = [x for x in v if any(s in str(x) for s in ["Cr2O7","MnO4","CrO4","AlO2","Cu(NH3)","ClO","SO3","HSO3","HCO3","S2O3","SCN","NO2","FeO2"])]
            print(f"  {k}: total={len(v)} hits={hits}")
elif isinstance(d, list):
    print("len:", len(d), "first:", d[0] if d else None)
