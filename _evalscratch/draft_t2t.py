# -*- coding: utf-8 -*-
"""Draft >=20 t2t chain questions; verify each resolves to expected unique
answer via the mirror (verify_t2t). Iterate until all pass.
"""
import sys, re
sys.path.insert(0, r"d:\github\CogniThink-Chem\_evalscratch")
from verify_t2t import decide, render

Q = []
def q(id, ans, stem, opts):
    Q.append({"id": id, "answer": ans, "question": stem, "type":"choice",
              "options": {k: v for k, v in opts.items()}, "answerKeywords":[ans]})

# ===== 能实现 (correct direction, unique feasible) =====
q("t2t-01","A","下列物质间的转化能实现的是（ ）",{
  "A":"Na --(H2O)--> NaOH --(CO2)--> Na2CO3",
  "B":"S --(O2,点燃)--> SO3 --(H2O)--> H2SO4",
  "C":"Fe --(H2O,高温)--> Fe2O3 --(CO,高温)--> Fe",
  "D":"NO --(H2O)--> HNO3 --(Cu)--> NO",
})
q("t2t-02","B","下列转化能实现的是（ ）",{
  "A":"SiO2 --(H2O)--> H2SiO3 --(NaOH)--> Na2SiO3",
  "B":"Al --(NaOH,H2O)--> NaAlO2 --(CO2,H2O)--> Al(OH)3",
  "C":"N2 --(O2,点燃)--> NO2 --(H2O)--> HNO3",
  "D":"Cu --(稀硫酸)--> CuSO4 --(NaOH)--> Cu(OH)2",
})
q("t2t-03","C","下列物质转化能实现的是（ ）",{
  "A":"Fe2O3 --(H2O)--> Fe(OH)3 --(HCl)--> FeCl3",
  "B":"MgCl2溶液 --(蒸干)--> 无水MgCl2 --(熔融电解)--> Mg",
  "C":"CaCO3 --(高温)--> CaO --(H2O)--> Ca(OH)2",
  "D":"Na2CO3 --(△)--> NaOH --(CO2)--> Na2CO3",
})
q("t2t-04","D","下列转化能实现的是（ ）",{
  "A":"CuO --(H2O)--> Cu(OH)2 --(H2SO4)--> CuSO4",
  "B":"SO3 --(H2O)--> H2SO3 --(O2)--> H2SO4",
  "C":"Fe --(Cl2)--> FeCl2 --(NaOH)--> Fe(OH)2",
  "D":"N2 --(H2,高温,高压,催化剂)--> NH3 --(O2,催化剂,△)--> NO",
})
q("t2t-05","A","下列物质间转化能实现的是（ ）",{
  "A":"CO2 --(NaOH)--> Na2CO3 --(Ca(OH)2)--> NaOH",
  "B":"CO2 --(C,高温)--> CO --(H2O)--> H2CO3",
  "C":"Si --(O2)--> SiO2 --(H2O)--> H2SiO3",
  "D":"Al --(O2)--> Al2O3 --(H2O)--> Al(OH)3",
})
q("t2t-06","B","下列转化能实现的是（ ）",{
  "A":"Fe(OH)3 --(△)--> Fe2O3 --(H2O)--> Fe(OH)3",
  "B":"NaHCO3 --(△)--> Na2CO3 --(HCl)--> CO2",
  "C":"H2SO4 --(Cu,△)--> SO2 --(H2O)--> H2SO4",
  "D":"NaCl --(电解)--> NaOH --(△)--> Na2O",
})
q("t2t-07","C","下列物质转化能实现的是（ ）",{
  "A":"NH3 --(O2,催化剂,△)--> NO --(H2O)--> HNO3",
  "B":"H2O2 --(MnO2)--> O2 --(Fe)--> Fe2O3",
  "C":"Fe --(Cl2,点燃)--> FeCl3 --(NaOH)--> Fe(OH)3",
  "D":"CaO --(HCl)--> CaCl2 --(NaOH)--> Ca(OH)2",
})
q("t2t-08","D","下列转化能实现的是（ ）",{
  "A":"S --(O2,点燃)--> SO3 --(H2O)--> H2SO4",
  "B":"N2 --(O2,放电)--> NO2 --(H2O)--> HNO3",
  "C":"Fe2O3 --(Al,高温)--> Fe --(H2O,高温)--> Fe2O3",
  "D":"Cu --(O2,△)--> CuO --(H2SO4)--> CuSO4",
})
q("t2t-09","A","下列物质间转化能实现的是（ ）",{
  "A":"Fe(OH)3 --(△)--> Fe2O3 --(CO,高温)--> Fe",
  "B":"H2SiO3 --(△)--> SiO2 --(H2O)--> H2SiO3",
  "C":"NH4Cl --(Ca(OH)2,△)--> NH3 --(H2O)--> NH4NO3",
  "D":"BaCl2 --(Na2SO4)--> BaSO4 --(HCl)--> BaCl2",
})
q("t2t-10","B","下列转化能实现的是（ ）",{
  "A":"KClO3 --(MnO2,△)--> O2 --(Fe)--> Fe3O4",
  "B":"CH4 --(O2,点燃)--> CO2 --(Mg,点燃)--> C",
  "C":"Fe --(HCl)--> FeCl3 --(NaOH)--> Fe(OH)3",
  "D":"Al2O3 --(NaOH)--> NaAlO2 --(HCl)--> AlCl3",
})
q("t2t-11","C","下列物质转化能实现的是（ ）",{
  "A":"Na2O2 --(H2O)--> NaOH --(△)--> Na2O",
  "B":"CO --(O2,点燃)--> CO2 --(H2O)--> H2CO3",
  "C":"Na --(O2,点燃)--> Na2O2 --(CO2)--> Na2CO3",
  "D":"SiO2 --(C,高温)--> Si --(HCl)--> SiCl4",
})
q("t2t-12","D","下列转化能实现的是（ ）",{
  "A":"FeSO4 --(NaOH)--> Fe(OH)2 --(△)--> FeO",
  "B":"Cu --(浓H2SO4,△)--> CuSO4 --(NaOH)--> Cu(OH)2 --(H2O)--> CuO",
  "C":"Mg --(N2,点燃)--> Mg3N2 --(H2O)--> Mg(OH)2",
  "D":"FeCl3 --(Fe)--> FeCl2 --(Cl2)--> FeCl3",
})

# ===== 不能实现 (wrong direction, unique broken) =====
q("t2t-13","B","下列物质间的转化不能实现的是（ ）",{
  "A":"Na --(H2O)--> NaOH --(CO2)--> Na2CO3",
  "B":"S --(O2,点燃)--> SO3 --(H2O)--> H2SO4",
  "C":"Fe --(Cl2,点燃)--> FeCl3 --(Fe)--> FeCl2",
  "D":"CaCO3 --(高温)--> CaO --(H2O)--> Ca(OH)2",
})
q("t2t-14","C","下列转化不能实现的是（ ）",{
  "A":"N2 --(H2,高温,高压,催化剂)--> NH3 --(HCl)--> NH4Cl",
  "B":"Al --(NaOH,H2O)--> NaAlO2 --(CO2,H2O)--> Al(OH)3",
  "C":"NO --(H2O)--> HNO3 --(NaOH)--> NaNO3",
  "D":"CO2 --(Mg,点燃)--> C --(O2)--> CO2",
})
q("t2t-15","D","下列物质转化不能实现的是（ ）",{
  "A":"Fe2O3 --(CO,高温)--> Fe --(Cl2)--> FeCl3",
  "B":"Cu --(O2,△)--> CuO --(H2)--> Cu",
  "C":"NaCl --(电解)--> Cl2 --(NaOH)--> NaClO",
  "D":"MgCl2溶液 --(蒸干)--> 无水MgCl2 --(电解)--> Mg",
})
q("t2t-16","A","下列转化不能实现的是（ ）",{
  "A":"SiO2 --(H2O)--> H2SiO3 --(NaOH)--> Na2SiO3",
  "B":"Na --(O2,常温)--> Na2O --(H2O)--> NaOH",
  "C":"NaHCO3 --(△)--> Na2CO3 --(Ca(OH)2)--> NaOH",
  "D":"Cl2 --(H2,点燃)--> HCl --(MnO2,△)--> Cl2",
})
q("t2t-17","B","下列物质间转化不能实现的是（ ）",{
  "A":"Al2O3 --(电解)--> Al --(O2)--> Al2O3",
  "B":"FeCl3 --(过量NaOH)--> NaAlO2 --(CO2)--> Al(OH)3",
  "C":"SO2 --(O2,催化剂,△)--> SO3 --(H2O)--> H2SO4",
  "D":"NH3 --(O2,催化剂,△)--> NO --(O2)--> NO2",
})
q("t2t-18","C","下列转化不能实现的是（ ）",{
  "A":"Fe --(HCl)--> FeCl2 --(NaOH)--> Fe(OH)2",
  "B":"CuSO4 --(NaOH)--> Cu(OH)2 --(△)--> CuO",
  "C":"Fe2O3 --(H2O)--> Fe(OH)3 --(HCl)--> FeCl3",
  "D":"Na --(O2,常温)--> Na2O --(H2O)--> NaOH",
})
q("t2t-19","D","下列物质转化不能实现的是（ ）",{
  "A":"CaO --(H2O)--> Ca(OH)2 --(CO2)--> CaCO3",
  "B":"H2SO3 --(△)--> SO2 --(H2O)--> H2SO3",
  "C":"KClO3 --(MnO2,△)--> O2 --(H2)--> H2O",
  "D":"Na2SiO3 --(HCl)--> H2SiO3 --(H2O)--> Na2SiO3",
})
q("t2t-20","A","下列转化不能实现的是（ ）",{
  "A":"Cu --(稀硫酸)--> CuSO4 --(NaOH)--> Cu(OH)2",
  "B":"Fe --(稀硫酸)--> FeSO4 --(NaOH)--> Fe(OH)2",
  "C":"Mg --(稀盐酸)--> MgCl2 --(NaOH)--> Mg(OH)2",
  "D":"Al --(稀盐酸)--> AlCl3 --(NaOH)--> Al(OH)3",
})
# bare 3-substance format (pdf-015 style)
q("t2t-21","B","甲、乙、丙有如图所示的转化关系（“→”表示反应一步实现）。下列各组物质按照甲、乙、丙的顺序不符合要求的是（ ）",{
  "A":"Na、NaOH、NaCl",
  "B":"KOH、K2CO3、KNO3",
  "C":"CaO、Ca(OH)2、CaCO3",
  "D":"Fe、FeCl2、Fe(OH)2",
})
q("t2t-22","C","下列物质间的转化不能实现的是（ ）",{
  "A":"C --(O2)--> CO --(O2)--> CO2",
  "B":"NH4HCO3 --(△)--> NH3 --(HCl)--> NH4Cl",
  "C":"NH3 --(O2,点燃)--> N2 --(O2)--> NO2",
  "D":"CaCl2 --(Na2CO3)--> CaCO3 --(HCl)--> CO2",
})
q("t2t-23","D","下列转化能实现的是（ ）",{
  "A":"NO --(H2O)--> HNO3 --(Cu)--> NO",
  "B":"Fe --(H2SO4)--> Fe2(SO4)3 --(NaOH)--> Fe(OH)3",
  "C":"SiO2 --(H2O)--> H2SiO3 --(△)--> SiO2",
  "D":"Al --(Cl2)--> AlCl3 --(NH3·H2O)--> Al(OH)3",
})
q("t2t-24","A","下列物质间转化能实现的是（ ）",{
  "A":"Fe(OH)2 --(O2,H2O)--> Fe(OH)3 --(△)--> Fe2O3",
  "B":"CO2 --(H2O)--> H2CO3 --(NaOH)--> Na2CO3",
  "C":"Cu --(S,△)--> CuS --(H2SO4)--> CuSO4",
  "D":"S --(H2,△)--> H2S --(O2)--> SO3",
})

print("=== t2t draft verification ===")
fails = 0
for x in Q:
    raw, opts = render(x)
    letter, info = decide(raw, opts)
    exp = x["answer"]
    status = "OK" if letter == exp else ("NULL" if letter is None else "MISMATCH")
    if status != "OK":
        fails += 1
    print(f"[{status}] {x['id']} expected={exp} got={letter} ({info})")
print(f"\ntotal={len(Q)} fails={fails}")
