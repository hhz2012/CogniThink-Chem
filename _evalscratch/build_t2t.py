# -*- coding: utf-8 -*-
"""构建并验证 t2t 转化链题库：每题每步必须在规则库内，答案唯一。"""
from probe_chain import decide, RULES

# 待入库的不可行规则（化学上明确不能一步实现）—— 先在探针里验证题目，再落盘
EXTRA_INFEASIBLE = [
    {"from": "SiO2", "to": "H2SiO3", "reagents": ["H2O"], "conditions": [], "feasible": False, "reason": "SiO2 不溶于水，不能直接与水反应生成硅酸"},
    {"from": "CuO", "to": "Cu(OH)2", "reagents": ["H2O"], "conditions": [], "feasible": False, "reason": "CuO 不溶于水，不能直接与水反应生成 Cu(OH)2"},
    {"from": "Al2O3", "to": "Al(OH)3", "reagents": ["H2O"], "conditions": [], "feasible": False, "reason": "Al2O3 不溶于水，不能直接与水反应生成 Al(OH)3"},
    {"from": "MgO", "to": "Mg(OH)2", "reagents": ["H2O"], "conditions": [], "feasible": False, "reason": "MgO 与水难直接化合生成 Mg(OH)2"},
    {"from": "CaCO3", "to": "Ca(OH)2", "reagents": ["H2O"], "conditions": [], "feasible": False, "reason": "CaCO3 不溶于水，需先高温分解为 CaO 再与水反应"},
    {"from": "Cu", "to": "Cu(OH)2", "reagents": ["H2O"], "conditions": [], "feasible": False, "reason": "Cu 与水不反应"},
    {"from": "Fe", "to": "Fe(OH)3", "reagents": ["H2O"], "conditions": [], "feasible": False, "reason": "Fe 与水不能直接生成 Fe(OH)3"},
    {"from": "Al", "to": "Al(OH)3", "reagents": ["H2O"], "conditions": [], "feasible": False, "reason": "Al 与水常温下不能直接生成 Al(OH)3"},
    {"from": "Fe", "to": "FeCl3", "reagents": ["HCl"], "conditions": [], "feasible": False, "reason": "Fe 与 HCl 反应生成 FeCl2，非 FeCl3"},
    {"from": "Cu", "to": "CuCl2", "reagents": ["HCl"], "conditions": [], "feasible": False, "reason": "Cu 与 HCl 不反应"},
    {"from": "Ag", "to": "AgNO3", "reagents": ["HCl"], "conditions": [], "feasible": False, "reason": "Ag 与 HCl 不反应"},
    {"from": "Fe2O3", "to": "FeSO4", "reagents": ["H2SO4"], "conditions": [], "feasible": False, "reason": "Fe2O3 与 H2SO4 反应生成 Fe2(SO4)3，非 FeSO4"},
    {"from": "BaSO4", "to": "BaCl2", "reagents": ["HNO3"], "conditions": [], "feasible": False, "reason": "BaSO4 不溶于硝酸"},
    {"from": "AgCl", "to": "AgNO3", "reagents": ["HNO3"], "conditions": [], "feasible": False, "reason": "AgCl 不溶于硝酸"},
    {"from": "N2", "to": "NO2", "reagents": ["O2"], "conditions": ["放电"], "feasible": False, "reason": "N2 与 O2 放电生成 NO，不能直接生成 NO2"},
    {"from": "FeCl3", "to": "Fe", "reagents": ["Cu"], "conditions": [], "feasible": False, "reason": "Cu 只能将 FeCl3 还原为 FeCl2，不能置换出 Fe"},
    {"from": "Na", "to": "NaOH", "reagents": ["O2"], "conditions": [], "feasible": False, "reason": "Na 与 O2 反应生成 Na2O/Na2O2，非 NaOH"},
    {"from": "H2SO4(稀)", "to": "SO2", "reagents": ["Cu"], "conditions": ["△"], "feasible": False, "reason": "稀硫酸与 Cu 不反应，需浓硫酸"},
    {"from": "SO2", "to": "SO3", "reagents": ["O2"], "conditions": [], "feasible": False, "reason": "SO2 与 O2 需催化剂、加热才能生成 SO3"},
    {"from": "NH3", "to": "NO", "reagents": ["O2"], "conditions": [], "feasible": False, "reason": "NH3 催化氧化需催化剂、加热才能生成 NO"},
]
RULES.extend(EXTRA_INFEASIBLE)

# 待入库的可行补步（题库尚未覆盖但化学正确）
EXTRA_FEASIBLE = [
    {"from": "H2SiO3", "to": "Na2SiO3", "reagents": ["NaOH"], "conditions": [], "feasible": True, "reason": "H2SiO3+2NaOH=Na2SiO3+2H2O"},
]
RULES.extend(EXTRA_FEASIBLE)

# ===================== 候选 t2t 题 =====================
# 格式与 PdfQuestionItem 对齐：id/question/options/answer
Q = []
def add(qid, stem, opts, ans):
    Q.append({"id": qid, "question": stem, "options": opts, "answer": ans})

STEM_OK = "下列物质间转化能实现的是"
STEM_NO = "下列物质间转化不能实现的是"

# --- 能实现 (ask=correct)：A 全可行，其余含不可行步 ---
add("t2t-01", STEM_OK, {
    "A": "Na --(O2,常温)--> Na2O --(H2O)--> NaOH --(CO2)--> Na2CO3",
    "B": "SiO2 --(H2O)--> H2SiO3 --(NaOH)--> Na2SiO3",
    "C": "Fe --(HCl)--> FeCl3 --(NaOH)--> Fe(OH)3",
    "D": "S --(O2,点燃)--> SO3 --(H2O)--> H2SO4",
}, "A")

add("t2t-02", STEM_OK, {
    "A": "Fe --(Cl2,点燃)--> FeCl3 --(NaOH)--> Fe(OH)3 --(△)--> Fe2O3",
    "B": "Fe2O3 --(H2O)--> Fe(OH)3 --(HCl)--> FeCl3",
    "C": "Fe --(H2O,高温)--> Fe2O3 --(CO,高温)--> Fe",
    "D": "FeCl3 --(Cu)--> Fe --(HCl)--> FeCl2",
}, "A")

add("t2t-03", STEM_OK, {
    "A": "SiO2 --(C,高温)--> Si --(O2,△)--> SiO2 --(NaOH)--> Na2SiO3",
    "B": "Al2O3 --(H2O)--> Al(OH)3 --(NaOH)--> NaAlO2",
    "C": "MgO --(H2O)--> Mg(OH)2 --(HCl)--> MgCl2",
    "D": "CuO --(H2O)--> Cu(OH)2 --(H2SO4)--> CuSO4",
}, "A")

add("t2t-04", STEM_OK, {
    "A": "N2 --(H2,高温,高压,催化剂)--> NH3 --(HCl)--> NH4Cl --(Ca(OH)2,△)--> NH3",
    "B": "N2 --(O2,放电)--> NO2 --(H2O)--> HNO3",
    "C": "NH3 --(O2)--> NO --(O2,H2O)--> HNO3",
    "D": "NO --(H2O)--> HNO3 --(Cu)--> NO2",
}, "A")

add("t2t-05", STEM_OK, {
    "A": "CaCO3 --(高温)--> CaO --(H2O)--> Ca(OH)2 --(CO2)--> CaCO3",
    "B": "CaCO3 --(H2O)--> Ca(OH)2 --(CO2)--> CaCO3",
    "C": "Ca(OH)2 --(CO2)--> CaCO3 --(H2O)--> Ca(OH)2",
    "D": "CaO --(H2O)--> Ca(OH)2 --(CO2)--> CaCO3 --(H2O)--> Ca(OH)2",
}, "A")

add("t2t-06", STEM_OK, {
    "A": "S --(O2,点燃)--> SO2 --(O2,催化剂,△)--> SO3 --(H2O)--> H2SO4",
    "B": "S --(O2,点燃)--> SO3 --(H2O)--> H2SO4",
    "C": "SO2 --(O2)--> SO3 --(H2O)--> H2SO4",
    "D": "H2SO4(稀) --(Cu,△)--> SO2 --(O2,催化剂,△)--> SO3",
}, "A")

add("t2t-07", STEM_OK, {
    "A": "Cl2 --(NaOH)--> NaClO --(CO2,H2O)--> HClO --(光照)--> HCl",
    "B": "Fe --(HCl)--> FeCl3 --(NaOH)--> Fe(OH)3",
    "C": "Cu --(HCl)--> CuCl2 --(NaOH)--> Cu(OH)2",
    "D": "SiO2 --(H2O)--> H2SiO3 --(NaOH)--> Na2SiO3",
}, "A")

add("t2t-08", STEM_OK, {
    "A": "Cu --(O2,△)--> CuO --(H2,△)--> Cu --(Cl2,点燃)--> CuCl2 --(NaOH)--> Cu(OH)2",
    "B": "Cu --(HCl)--> CuCl2 --(NaOH)--> Cu(OH)2 --(△)--> CuO",
    "C": "Cu --(H2O)--> Cu(OH)2 --(H2SO4)--> CuSO4 --(Fe)--> Cu",
    "D": "CuO --(H2O)--> Cu(OH)2 --(HCl)--> CuCl2 --(Fe)--> Cu",
}, "A")

add("t2t-09", STEM_OK, {
    "A": "Al --(O2)--> Al2O3 --(NaOH)--> NaAlO2 --(CO2,H2O)--> Al(OH)3 --(△)--> Al2O3",
    "B": "Al --(H2O)--> Al(OH)3 --(NaOH)--> NaAlO2",
    "C": "Al2O3 --(H2O)--> Al(OH)3 --(HCl)--> AlCl3",
    "D": "Al --(H2O)--> Al(OH)3 --(HCl)--> AlCl3",
}, "A")

add("t2t-10", STEM_OK, {
    "A": "Mg --(O2,点燃)--> MgO --(HCl)--> MgCl2 --(NaOH)--> Mg(OH)2 --(△)--> MgO",
    "B": "MgO --(H2O)--> Mg(OH)2 --(HCl)--> MgCl2",
    "C": "MgCl2 溶液 --(蒸干)--> 无水MgCl2 --(熔融电解)--> Mg",
    "D": "MgO --(H2O)--> Mg(OH)2 --(△)--> MgO",
}, "A")

# --- 不能实现 (ask=wrong)：正确项含不可行步，其余全可行 ---
add("t2t-11", STEM_NO, {
    "A": "Na --(O2,点燃)--> Na2O2 --(CO2)--> Na2CO3",
    "B": "Cu --(H2O)--> Cu(OH)2 --(H2SO4)--> CuSO4",
    "C": "CaCO3 --(高温)--> CaO --(H2O)--> Ca(OH)2",
    "D": "Cl2 --(NaOH)--> NaClO --(CO2,H2O)--> HClO",
}, "B")

add("t2t-12", STEM_NO, {
    "A": "N2 --(H2,高温,高压,催化剂)--> NH3 --(HCl)--> NH4Cl",
    "B": "NH3 --(O2,催化剂,△)--> NO --(O2,H2O)--> HNO3",
    "C": "N2 --(O2,放电)--> NO2 --(H2O)--> HNO3",
    "D": "NH4Cl --(Ca(OH)2,△)--> NH3 --(HNO3)--> NH4NO3",
}, "C")

add("t2t-13", STEM_NO, {
    "A": "SiO2 --(C,高温)--> Si --(O2,△)--> SiO2",
    "B": "Na2SiO3 --(HCl)--> H2SiO3 --(△)--> SiO2",
    "C": "SiO2 --(H2O)--> H2SiO3 --(NaOH)--> Na2SiO3",
    "D": "Si --(O2,△)--> SiO2 --(NaOH)--> Na2SiO3",
}, "C")

add("t2t-14", STEM_NO, {
    "A": "Fe --(Cl2,点燃)--> FeCl3 --(NaOH)--> Fe(OH)3",
    "B": "Fe --(HCl)--> FeCl2 --(NaOH)--> Fe(OH)2 --(O2,H2O)--> Fe(OH)3",
    "C": "Fe2O3 --(H2SO4)--> FeSO4 --(NaOH)--> Fe(OH)2",
    "D": "Fe(OH)3 --(△)--> Fe2O3 --(CO,高温)--> Fe",
}, "C")

add("t2t-15", STEM_NO, {
    "A": "S --(O2,点燃)--> SO2 --(H2O)--> H2SO3",
    "B": "SO2 --(O2,催化剂,△)--> SO3 --(H2O)--> H2SO4",
    "C": "S --(O2,点燃)--> SO3 --(H2O)--> H2SO4",
    "D": "H2SO3 --(△)--> SO2 --(H2S)--> S",
}, "C")

add("t2t-16", STEM_NO, {
    "A": "NaCl --(电解)--> Cl2 --(H2,点燃)--> HCl",
    "B": "HCl --(MnO2,△)--> Cl2 --(NaOH)--> NaClO",
    "C": "NaClO --(CO2,H2O)--> HClO --(光照)--> HCl",
    "D": "Cu --(HCl)--> CuCl2 --(NaOH)--> Cu(OH)2",
}, "D")

add("t2t-17", STEM_NO, {
    "A": "NH3 --(O2,催化剂,△)--> NO --(O2)--> NO2 --(H2O)--> HNO3",
    "B": "NO --(O2)--> NO2 --(H2O)--> HNO3",
    "C": "NO --(H2O)--> HNO3 --(Cu)--> NO2",
    "D": "HNO3 --(Cu)--> NO2 --(H2O)--> HNO3",
}, "C")

add("t2t-18", STEM_NO, {
    "A": "NaHCO3 --(△)--> Na2CO3 --(HCl)--> CO2",
    "B": "CO2 --(NaOH)--> Na2CO3 --(Ca(OH)2)--> CaCO3",
    "C": "CaCO3 --(H2O)--> Ca(OH)2 --(CO2)--> CaCO3",
    "D": "CaCO3 --(高温)--> CaO --(H2O)--> Ca(OH)2",
}, "C")

add("t2t-19", STEM_NO, {
    "A": "BaCl2 --(H2SO4)--> BaSO4",
    "B": "AgNO3 --(HCl)--> AgCl",
    "C": "BaSO4 --(HNO3)--> BaCl2",
    "D": "AgNO3 --(Cu)--> Ag",
}, "C")

add("t2t-20", STEM_NO, {
    "A": "H2O --(电解)--> H2 --(Cl2,点燃)--> HCl",
    "B": "H2 --(O2,点燃)--> H2O --(电解)--> O2",
    "C": "KMnO4 --(△)--> O2 --(H2,点燃)--> H2O",
    "D": "Na --(O2)--> NaOH --(HCl)--> NaCl",
}, "D")

add("t2t-21", STEM_OK, {
    "A": "FeSO4 --(NaOH)--> Fe(OH)2 --(O2,H2O)--> Fe(OH)3 --(HCl)--> FeCl3",
    "B": "Fe --(HCl)--> FeCl3 --(NaOH)--> Fe(OH)3",
    "C": "Fe2O3 --(H2SO4)--> FeSO4 --(NaOH)--> Fe(OH)2",
    "D": "FeCl3 --(Cu)--> Fe --(H2SO4)--> FeSO4",
}, "A")

add("t2t-22", STEM_OK, {
    "A": "CH4 --(O2,点燃)--> CO2 --(Ca(OH)2)--> CaCO3 --(高温)--> CaO",
    "B": "S --(O2,点燃)--> SO3 --(H2O)--> H2SO4",
    "C": "MgO --(H2O)--> Mg(OH)2 --(HCl)--> MgCl2",
    "D": "Al2O3 --(H2O)--> Al(OH)3 --(NaOH)--> NaAlO2",
}, "A")

add("t2t-23", STEM_NO, {
    "A": "AgCl --(HNO3)--> AgNO3 --(Cu)--> Ag",
    "B": "AgNO3 --(HCl)--> AgCl",
    "C": "AgNO3 --(Cu)--> Ag",
    "D": "BaCl2 --(H2SO4)--> BaSO4",
}, "A")

add("t2t-24", STEM_NO, {
    "A": "K2FeO4 --(H2O)--> H2 --(O2,点燃)--> H2O",
    "B": "Na2O2 --(H2O)--> NaOH --(CO2)--> Na2CO3",
    "C": "Na2O2 --(CO2)--> O2 --(H2,点燃)--> H2O",
    "D": "NaOH --(CO2)--> NaHCO3 --(△)--> Na2CO3",
}, "A")
# A: K2FeO4->H2[H2O] 不可行(已有规则)，但 H2->H2O 可行。所以 A 含不可行步 → 正确(不能实现)。但 K2FeO4 作为起始物质，规则里 K2FeO4->H2 不可行。匹配需 from=K2FeO4。OK。

# ===================== 验证 =====================
if __name__ == '__main__':
    print("=== t2t 验证 ===")
    ok = 0; bad = []
    for q in Q:
        d, info = decide(q["question"], q["options"])
        exp = q["answer"]
        flag = "OK" if d == exp else "FAIL"
        if d == exp:
            ok += 1
        else:
            bad.append((q["id"], exp, d, info))
        print(f"{q['id']:8s} exp={exp} got={str(d):5} {flag} {info}")
    print(f"\nPASS {ok}/{len(Q)}")
    for b in bad:
        print("BAD:", b)
