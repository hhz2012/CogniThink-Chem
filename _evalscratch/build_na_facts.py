# -*- coding: utf-8 -*-
"""Build data/highschool/na-particle-facts.json.

Numeric fields (molarMass / electrons / protons / neutrons / atoms) are
COMPUTED by a formula parser from the most-common isotope masses, to avoid
arithmetic errors. Structural fields (sigmaBonds / piBonds / covalentBonds /
ions / stpState / hydrolyzableIons / molecular) are hardcoded and MUST be
chemically verified — they are the truth source for NaChoiceValidator.

All chemistry facts live in the emitted JSON; this script only computes.
"""
import json, io, re

ROOT = r"d:\github\CogniThink-Chem"
OUT = ROOT + r"\data\highschool\na-particle-facts.json"

# element -> (atomic number Z, most-common mass number A)
ELEM = {
    "H":(1,1),"He":(2,4),"Li":(3,7),"Be":(4,9),"B":(5,11),"C":(6,12),"N":(7,14),
    "O":(8,16),"F":(9,19),"Ne":(10,20),"Na":(11,23),"Mg":(12,24),"Al":(13,27),
    "Si":(14,28),"P":(15,31),"S":(16,32),"Cl":(17,35),"Ar":(18,40),"K":(19,39),
    "Ca":(20,40),"Mn":(25,55),"Fe":(26,56),"Cu":(29,64),"Zn":(30,65),"Br":(35,80),
    "Ag":(47,108),"I":(53,127),"Ba":(56,137),
}

def parse_formula(f):
    """Return dict element->count, supporting parentheses and nesting."""
    tokens = re.findall(r"([A-Z][a-z]?|\(|\)|\d+)", f)
    stack = [{}]
    i = 0
    while i < len(tokens):
        t = tokens[i]
        if t == "(":
            stack.append({})
        elif t == ")":
            top = stack.pop()
            mult = 1
            if i+1 < len(tokens) and tokens[i+1].isdigit():
                mult = int(tokens[i+1]); i += 1
            for e,c in top.items():
                stack[-1][e] = stack[-1].get(e,0) + c*mult
        elif t.isdigit():
            # should have been consumed; ignore stray
            pass
        else:
            cnt = 1
            if i+1 < len(tokens) and tokens[i+1].isdigit():
                cnt = int(tokens[i+1]); i += 1
            stack[-1][t] = stack[-1].get(t,0) + cnt
        i += 1
    return stack[0]

def computed(f):
    d = parse_formula(f)
    p=n=atoms=0; mm=0.0
    for e,c in d.items():
        Z,A = ELEM[e]
        p += Z*c; n += (A-Z)*c; atoms += c; mm += A*c
    return {"formula":f,"molarMass":mm,"protons":p,"neutrons":n,
            "electrons":p,"atoms":atoms}   # neutral: electrons==protons

# Each entry: formula + structural overrides. Numeric fields auto-filled.
# molecular=True  -> sigma/pi/covalent bonds are meaningful
# ionic ions>0    -> ion count per formula unit is meaningful
# hydrolyzableIons-> ions that hydrolyze (使溶液中实际浓度 < 名义)
# stpState         -> gas / solid / liquid at 0℃,101kPa (for 22.4L trap)
SUBSTANCES = [
    # --- 单质 / 气体 ---
    ("H2",       dict(molecular=True,sigmaBonds=1,piBonds=0,covalentBonds=1,stpState="gas")),
    ("O2",       dict(molecular=True,sigmaBonds=1,piBonds=1,covalentBonds=2,stpState="gas")),
    ("N2",       dict(molecular=True,sigmaBonds=1,piBonds=2,covalentBonds=3,stpState="gas")),
    ("Cl2",      dict(molecular=True,sigmaBonds=1,piBonds=0,covalentBonds=1,stpState="gas")),
    ("O3",       dict(molecular=True,sigmaBonds=2,piBonds=1,covalentBonds=3,stpState="gas")),
    ("He",       dict(molecular=False,stpState="gas")),
    ("Ne",       dict(molecular=False,stpState="gas")),
    ("Ar",       dict(molecular=False,stpState="gas")),
    ("Fe",       dict(molecular=False,stpState="solid")),
    ("Cu",       dict(molecular=False,stpState="solid")),
    ("Al",       dict(molecular=False,stpState="solid")),
    ("Zn",       dict(molecular=False,stpState="solid")),
    ("Mg",       dict(molecular=False,stpState="solid")),
    ("C",        dict(molecular=False,stpState="solid")),   # 石墨/金刚石，^12C
    ("S",        dict(molecular=False,stpState="solid")),
    ("P",        dict(molecular=False,stpState="solid")),
    # --- 氢化物 / 氧化物 / 气态分子 ---
    ("H2O",      dict(molecular=True,sigmaBonds=2,piBonds=0,covalentBonds=2,stpState="liquid",hBonds=2.0)),
    ("H2O2",     dict(molecular=True,sigmaBonds=3,piBonds=0,covalentBonds=3,stpState="liquid")),
    ("NH3",      dict(molecular=True,sigmaBonds=3,piBonds=0,covalentBonds=3,stpState="gas")),
    ("CH4",      dict(molecular=True,sigmaBonds=4,piBonds=0,covalentBonds=4,stpState="gas")),
    ("C2H6",     dict(molecular=True,sigmaBonds=7,piBonds=0,covalentBonds=7,stpState="gas")),
    ("C2H4",     dict(molecular=True,sigmaBonds=5,piBonds=1,covalentBonds=6,stpState="gas")),
    ("C2H2",     dict(molecular=True,sigmaBonds=3,piBonds=2,covalentBonds=5,stpState="gas")),
    ("C4H10",    dict(molecular=True,sigmaBonds=13,piBonds=0,covalentBonds=13,stpState="gas")),  # 丁烷/异丁烷
    ("C6H6",     dict(molecular=True,sigmaBonds=12,piBonds=1,covalentBonds=12,stpState="liquid")),
    ("HCl",      dict(molecular=True,sigmaBonds=1,piBonds=0,covalentBonds=1,stpState="gas")),
    ("H2S",      dict(molecular=True,sigmaBonds=2,piBonds=0,covalentBonds=2,stpState="gas")),
    ("CO",       dict(molecular=True,sigmaBonds=1,piBonds=2,covalentBonds=3,stpState="gas")),
    ("CO2",      dict(molecular=True,sigmaBonds=2,piBonds=2,covalentBonds=4,stpState="gas")),
    ("SO2",      dict(molecular=True,sigmaBonds=2,piBonds=1,covalentBonds=3,stpState="gas")),
    ("SO3",      dict(molecular=True,sigmaBonds=3,piBonds=1,covalentBonds=4,stpState="solid")),  # 标况固态，22.4L 陷阱
    ("NO",       dict(molecular=True,sigmaBonds=1,piBonds=1,covalentBonds=2,stpState="gas")),
    ("NO2",      dict(molecular=True,sigmaBonds=2,piBonds=1,covalentBonds=3,stpState="gas")),
    ("N2O",      dict(molecular=True,sigmaBonds=2,piBonds=2,covalentBonds=4,stpState="gas")),
    ("ClO2",     dict(molecular=True,sigmaBonds=2,piBonds=1,covalentBonds=3,stpState="gas")),
    ("OF2",      dict(molecular=True,sigmaBonds=2,piBonds=0,covalentBonds=2,stpState="gas")),
    # --- 含氧酸 ---
    ("H2SO4",    dict(molecular=True,stpState="liquid")),
    ("HNO3",     dict(molecular=True,stpState="liquid")),
    ("HClO4",    dict(molecular=True,stpState="liquid")),
    # --- 离子化合物 ---
    ("NaCl",     dict(molecular=False,ions=2,stpState="solid")),
    ("NaOH",     dict(molecular=False,ions=2,stpState="solid")),
    ("Na2CO3",   dict(molecular=False,ions=3,stpState="solid",hydrolyzableIons=["CO3^2-"])),
    ("NaHCO3",   dict(molecular=False,ions=2,stpState="solid",hydrolyzableIons=["HCO3^-"])),
    ("Na2O2",    dict(molecular=False,ions=3,stpState="solid")),   # 2Na+ + O2^2-
    ("KO2",      dict(molecular=False,ions=2,stpState="solid")),   # K+ + O2^-
    ("NaClO",    dict(molecular=False,ions=2,stpState="solid",hydrolyzableIons=["ClO^-"])),
    ("NaF",      dict(molecular=False,ions=2,stpState="solid",hydrolyzableIons=["F^-"])),
    ("NH4Cl",    dict(molecular=False,ions=2,stpState="solid",hydrolyzableIons=["NH4^+"])),
    ("NH4NO3",   dict(molecular=False,ions=2,stpState="solid",hydrolyzableIons=["NH4^+"])),
    ("CaCO3",    dict(molecular=False,ions=2,stpState="solid",hydrolyzableIons=["CO3^2-"])),
    ("CaO",      dict(molecular=False,ions=2,stpState="solid")),
    ("Ca(OH)2",  dict(molecular=False,ions=3,stpState="solid")),
    ("KCl",      dict(molecular=False,ions=2,stpState="solid")),
    ("KNO3",     dict(molecular=False,ions=2,stpState="solid")),
    ("AgNO3",    dict(molecular=False,ions=2,stpState="solid")),
    ("BaSO4",    dict(molecular=False,ions=2,stpState="solid")),
]

out = []
for f, overrides in SUBSTANCES:
    rec = computed(f)
    rec.update(overrides)
    # defaults
    rec.setdefault("molecular", False)
    rec.setdefault("sigmaBonds", 0)
    rec.setdefault("piBonds", 0)
    rec.setdefault("covalentBonds", 0)
    rec.setdefault("ions", 0)
    rec.setdefault("hBonds", 0.0)
    rec.setdefault("hydrolyzableIons", [])
    rec.setdefault("stpState", "solid")
    rec.setdefault("names", [])
    rec.setdefault("note", "")
    out.append(rec)

# sanity cross-checks (verified chemistry) — fail loudly if drift
checks = {
    "H2O":  dict(protons=10,neutrons=8,atoms=3,sigmaBonds=2),
    "CO2":  dict(protons=22,neutrons=22,atoms=3,sigmaBonds=2,piBonds=2),
    "Na2O2":dict(ions=3,protons=38,neutrons=40,atoms=4),
    "KO2":  dict(ions=2),
    "SO3":  dict(stpState="solid"),
    "C2H2": dict(sigmaBonds=3,covalentBonds=5),
    "C4H10":dict(sigmaBonds=13,covalentBonds=13),
    "CH4":  dict(atoms=5,protons=10),
    "NH4Cl":dict(hydrolyzableIons=["NH4^+"]),
    "Na2CO3":dict(ions=3,hydrolyzableIons=["CO3^2-"]),
    "Ca(OH)2":dict(ions=3,atoms=5,protons=38,neutrons=36),
}
idx = {r["formula"]:r for r in out}
for f, exp in checks.items():
    for k,v in exp.items():
        got = idx[f][k]
        assert got == v, f"{f}.{k} = {got!r}, expected {v!r}"
assert len(out) >= 40, f"only {len(out)} formulas"
print(f"formulas={len(out)}  OK (cross-checks passed)")
print("sample:", json.dumps(idx["CO2"], ensure_ascii=False))

data = {"version":"1.0","source":"computed + high-school verified structural facts",
        "note":"最常见同位素；e/p/n/atoms/molarMass 由公式解析器计算；键/离子/物态/水解手工核准。",
        "formulas":out}
with io.open(OUT, "w", encoding="utf-8") as fh:
    json.dump(data, fh, ensure_ascii=False, indent=2)
    fh.write("\n")
print("written", OUT)
