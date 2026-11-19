# -*- coding: utf-8 -*-
"""Draft >=18 t2n NA questions; every option is a cleanly computable
particle-count claim so the conservative mirror can decide a unique answer.
Verify each resolves to expected via verify_t2n.decide.
"""
import sys
sys.path.insert(0, r"d:\github\CogniThink-Chem\_evalscratch")
from verify_t2n import decide, render

Q=[]
def q(id, ans, stem, opts):
    Q.append({"id":id,"answer":ans,"question":stem,"type":"choice",
              "options":{k:v for k,v in opts.items()},"answerKeywords":[ans]})

STEM_C = "设 NA 为阿伏加德罗常数的值。下列说法正确的是（ ）"
STEM_W = "设 NA 为阿伏加德罗常数的值。下列说法错误的是（ ）"

# ===== 正确方向 (exactly one TRUE) =====
q("t2n-01","A",STEM_C,{
 "A":"1 mol H2O 中含有的电子数为 10NA",
 "B":"1 mol H2O 中含有的中子数为 10NA",
 "C":"1 mol CO2 中含有的原子数为 2NA",
 "D":"1 mol NH3 中含有的质子数为 7NA",
})
q("t2n-02","B",STEM_C,{
 "A":"18 g H2O 中含有的中子数为 10NA",
 "B":"44 g CO2 中含有的 σ 键数目为 2NA",
 "C":"16 g O2 中含有的 π 键数目为 2NA",
 "D":"28 g N2 中含有的共价键数目为 2NA",
})
q("t2n-03","C",STEM_W,{
 "A":"1 mol CH4 中含有的电子数为 10NA",
 "B":"1 mol CO2 中含有的质子数为 22NA",
 "C":"1 mol H2O 中含有的中子数为 10NA",
 "D":"1 mol NaCl 中含有的离子数为 2NA",
})
q("t2n-04","D",STEM_C,{
 "A":"标准状况下，22.4 L CO2 中含有的原子数为 2NA",
 "B":"标准状况下，11.2 L O2 中含有的 π 键数目为 NA",
 "C":"标准状况下，22.4 L SO3 中含有的电子数为 40NA",
 "D":"标准状况下，22.4 L N2 中含有的共价键数目为 3NA",
})
q("t2n-05","B",STEM_W,{
 "A":"1 mol H2O 晶体中含有的氢键数目为 2NA",
 "B":"1 mol H2O 中含有的 σ 键数目为 4NA",
 "C":"1 mol CO2 中含有的 π 键数目为 2NA",
 "D":"1 mol C2H2 中含有的 σ 键数目为 3NA",
})
q("t2n-06","C",STEM_C,{
 "A":"2 g H2 中含有的原子数为 NA",
 "B":"16 g CH4 中含有的电子数为 8NA",
 "C":"17 g NH3 中含有的质子数为 10NA",
 "D":"36 g H2O 中含有的中子数为 20NA",
})
q("t2n-07","A",STEM_W,{
 "A":"1 mol Na2O2 晶体中含有的离子数为 4NA",
 "B":"1 mol NaCl 中含有的离子数为 2NA",
 "C":"1 mol NaOH 中含有的离子数为 2NA",
 "D":"1 mol CaCO3 中含有的离子数为 2NA",
})
q("t2n-08","B",STEM_C,{
 "A":"26 g C2H2 中含有的 π 键数目为 3NA",
 "B":"26 g C2H2 中含有的 σ 键数目为 3NA",
 "C":"58 g C4H10 中含有的共价键数目为 12NA",
 "D":"16 g O2 中含有的 π 键数目为 NA",
})
q("t2n-09","D",STEM_W,{
 "A":"标准状况下，22.4 L H2 中含有的质子数为 2NA",
 "B":"标准状况下，11.2 L CO2 中含有的 σ 键数目为 NA",
 "C":"1 mol H2S 中含有的原子数为 3NA",
 "D":"标准状况下，22.4 L SO2 中含有的 π 键数目为 2NA",
})
q("t2n-10","A",STEM_C,{
 "A":"1 mol Ne 中含有的电子数为 10NA",
 "B":"1 mol He 中含有的中子数为 0NA",
 "C":"1 mol Ar 中含有的电子数为 8NA",
 "D":"1 mol Cl2 中含有的质子数为 17NA",
})
q("t2n-11","C",STEM_W,{
 "A":"44 g CO2 中含有的电子数为 22NA",
 "B":"44 g CO2 中含有的中子数为 22NA",
 "C":"44 g CO2 中含有的原子数为 2NA",
 "D":"44 g CO2 中含有的 σ 键数目为 2NA",
})
q("t2n-12","D",STEM_C,{
 "A":"0.5 mol H2O 中含有的中子数为 5NA",
 "B":"0.5 mol CO2 中含有的 π 键数目为 2NA",
 "C":"0.5 mol CH4 中含有的原子数为 2NA",
 "D":"0.5 mol H2O 中含有的电子数为 5NA",
})
q("t2n-13","B",STEM_W,{
 "A":"1 mol H2O2 中含有的 σ 键数目为 3NA",
 "B":"1 mol H2O2 中含有的 π 键数目为 NA",
 "C":"1 mol O3 中含有的原子数为 3NA",
 "D":"1 mol N2O 中含有的原子数为 3NA",
})
q("t2n-14","A",STEM_C,{
 "A":"1 mol KO2 晶体中含有的离子数为 2NA",
 "B":"1 mol KO2 晶体中含有的离子数为 3NA",
 "C":"1 mol Na2O2 晶体中含有的离子数为 4NA",
 "D":"1 mol CaO 中含有的离子数为 3NA",
})
q("t2n-15","C",STEM_W,{
 "A":"56 g Fe 中含有的电子数为 26NA",
 "B":"64 g Cu 中含有的质子数为 29NA",
 "C":"27 g Al 中含有的中子数为 13NA",
 "D":"24 g Mg 中含有的原子数为 NA",
})
q("t2n-16","B",STEM_C,{
 "A":"1 mol C2H6 中含有的共价键数目为 6NA",
 "B":"1 mol C2H4 中含有的 σ 键数目为 5NA",
 "C":"1 mol C2H4 中含有的 π 键数目为 2NA",
 "D":"1 mol C6H6 中含有的 σ 键数目为 6NA",
})
q("t2n-17","A",STEM_W,{
 "A":"标准状况下，22.4 L H2O 中含有的分子数为 NA",
 "B":"标准状况下，22.4 L CO2 中含有的电子数为 22NA",
 "C":"标准状况下，11.2 L O2 中含有的原子数为 NA",
 "D":"标准状况下，22.4 L N2 中含有的中子数为 14NA",
})
q("t2n-18","C",STEM_C,{
 "A":"1 mol NO 中含有的质子数为 14NA",
 "B":"1 mol NO2 中含有的电子数为 22NA",
 "C":"1 mol CO 中含有的共价键数目为 3NA",
 "D":"1 mol N2O 中含有的中子数为 20NA",
})
q("t2n-19","B",STEM_W,{
 "A":"1 mol HCl 中含有的质子数目为 18NA",
 "B":"1 mol NaCl 中含有的中子数目为 28NA",
 "C":"1 mol NaCl 中含有的电子数目为 28NA",
 "D":"1 mol NaCl 中含有的质子数目为 28NA",
})
q("t2n-20","D",STEM_C,{
 "A":"1 mol Ca(OH)2 中含有的离子数为 2NA",
 "B":"1 mol Ca(OH)2 中含有的原子数为 4NA",
 "C":"1 mol Ca(OH)2 中含有的质子数目为 36NA",
 "D":"1 mol Ca(OH)2 中含有的电子数目为 38NA",
})

print("=== t2n draft verification ===")
fails=0
for x in Q:
    raw,opts = render(x)
    letter,info = decide(raw,opts)
    exp=x["answer"]
    status="OK" if letter==exp else ("NULL" if letter is None else "MISMATCH")
    if status!="OK": fails+=1
    print(f"[{status}] {x['id']} expected={exp} got={letter} ({info[:80]})")
print(f"\ntotal={len(Q)} fails={fails}")
