# -*- coding: utf-8 -*-
import json, io
p = r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\chemistry\ions.json"
d = json.load(io.open(p, encoding="utf-8-sig"))
print("count:", len(d))
formulas = [x.get("formula") for x in d if isinstance(x, dict)]
print("all formulas:")
for f in formulas:
    print("  ", repr(f))
print()
targets = ["Cr2O7^2-", "Cr2O7", "MnO4^-", "MnO4", "CrO4^2-", "AlO2^-", "AlO2", "Cu(NH3)", "ClO^-", "ClO", "SO3^2-", "HSO3^-", "S2O3^2-", "SCN^-", "NO2^-", "FeO2^-", "MnO4^2-", "H2PO4^-", "HPO4^2-", "PO4^3-"]
print("target presence:")
for t in targets:
    print(f"  {t}: {'YES' if t in formulas else 'no'}")
