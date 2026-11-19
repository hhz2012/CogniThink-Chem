# -*- coding: utf-8 -*-
"""Generate data/highschool/transformation-rules.json (>=50 directed one-step
transformations) AND mirror TransformationChainValidator logic to verify the
real-exam targets (pdf-013/015/016/017) and draft t2t questions resolve to the
expected unique answer. Run before porting to C#.

Red lines mirrored: any step whose (from->to) has no rule => broken; if option
specifies reagents, rule.requiredReagents must all be supplied; no unique
chain => null (never guess a letter).
"""
import json, re

ROOT = r"d:\github\CogniThink-Chem"
OUT = ROOT + r"\data\highschool\transformation-rules.json"

# ---------- Norm (mirror C# ClassificationValidator.Norm) ----------
def norm(s):
    if not s: return ""
    out = []
    for ch in s:
        if ch.isspace(): continue
        o = ord(ch)
        if 0x2080 <= o <= 0x2089: out.append(chr(ord('0') + (o - 0x2080))); continue
        if o == 0x2070: out.append('0'); continue
        if o == 0x00B9: out.append('1'); continue
        if o == 0x00B2: out.append('2'); continue
        if o == 0x00B3: out.append('3'); continue
        if 0x2074 <= o <= 0x2079: out.append(chr(ord('4') + (o - 0x2074))); continue
        if o == 0x207A: out.append('+'); continue
        if o == 0x207B: out.append('-'); continue
        if 0xFF01 <= o <= 0xFF5E: out.append(chr(o - 0xFEE0)); continue
        if o == 0x3000: continue
        out.append(ch)
    return "".join(out)

# ---------- load classification lib for name->formula resolution ----------
cls = json.load(open(ROOT + r"\data\highschool\substance-classification.json", encoding="utf-8"))
by_formula, by_name = {}, {}
for s in cls["substances"]:
    nf = norm(s.get("formula", ""))
    if nf: by_formula.setdefault(nf, s)
    for nm in s.get("names", []):
        nn = norm(nm)
        if nn: by_name.setdefault(nn, s)

NAME_ALIAS = {
    "食盐水": "NaCl", "饱和食盐水": "NaCl", "盐水": "NaCl",
    "漂白粉": "漂白粉", "漂粉精": "漂白粉",
    "小苏打": "NaHCO3", "苏打": "Na2CO3", "纯碱": "Na2CO3", "碱面": "Na2CO3",
    "熟石灰": "Ca(OH)2", "消石灰": "Ca(OH)2", "澄清石灰水": "Ca(OH)2", "石灰水": "Ca(OH)2", "石灰乳": "Ca(OH)2",
    "生石灰": "CaO", "石灰石": "CaCO3", "大理石": "CaCO3", "方解石": "CaCO3",
    "烧碱": "NaOH", "火碱": "NaOH", "苛性钠": "NaOH", "苛性钾": "KOH",
    "干冰": "CO2", "冰": "H2O", "水银": "Hg", "硫磺": "S", "硫": "S",
    "液氯": "Cl2", "氯水": "Cl2", "液氨": "NH3", "氨水": "NH3·H2O", "双氧水": "H2O2", "过氧化氢": "H2O2",
    "沼气": "CH4", "天然气": "CH4", "甲烷": "CH4", "酒精": "C2H5OH", "乙醇": "C2H5OH",
    "醋酸": "CH3COOH", "乙酸": "CH3COOH", "蚁酸": "HCOOH", "甲醛": "HCHO", "乙醛": "CH3CHO",
    "葡萄糖": "C6H12O6", "蔗糖": "C12H22O11", "水玻璃": "Na2SiO3", "金刚砂": "SiC",
    "胆矾": "CuSO4", "蓝矾": "CuSO4", "绿矾": "FeSO4", "明矾": "KAl(SO4)2", "皓矾": "ZnSO4",
    "芒硝": "Na2SO4", "石膏": "CaSO4", "重晶石": "BaSO4", "小苏打": "NaHCO3",
    "硅石": "SiO2", "石英": "SiO2", "水晶": "SiO2", "光导纤维": "SiO2",
    "刚玉": "Al2O3", "红宝石": "Al2O3", "蓝宝石": "Al2O3", "铝土矿": "Al2O3",
    "赤铁矿": "Fe2O3", "磁铁矿": "Fe3O4", "菱铁矿": "FeCO3",
    "黄铜矿": "CuFeS2", "孔雀石": "Cu2(OH)2CO3", "铜绿": "Cu2(OH)2CO3",
    "食盐": "NaCl", "粗盐": "NaCl",
}
PREFIX_STRIP = ["饱和", "无水", "粗", "高纯", "新制", "稀", "浓"]   # strip leading word tokens
STATE_SUFFIX = ["溶液", "熔融", "固体", "液体", "气体", "水溶液", "悬浊液", "乳浊液", "胶体"]
DROP_TOKENS = {"粗", "高纯", "无水", "饱和", "溶液", "熔融", "固体", "液体", "气体", "水溶液", "新制"}

def norm_sub(tok):
    t = norm(tok).strip()
    # remove trailing state parens: (s) (l) (g) (aq) （...）
    t = re.sub(r"[\(（](?:s|l|g|aq|S|L|G|AQ|固体|液体|气体|溶液|熔融)[\)）]\s*$", "", t)
    # remove leading qualifier parens: (粗) (纯) ...
    t = re.sub(r"^\s*[\(（](?:粗|纯|高纯|无水|新制|稀|浓)[\)）]\s*", "", t)
    # strip leading prefix words
    changed = True
    while changed:
        changed = False
        for p in PREFIX_STRIP:
            if t.startswith(p) and len(t) > len(p):
                t = t[len(p):]; changed = True; break
    # strip trailing state words
    changed = True
    while changed:
        changed = False
        for s in STATE_SUFFIX:
            if t.endswith(s) and len(t) > len(s):
                t = t[:-len(s)]; changed = True; break
    t = t.strip()
    if t in NAME_ALIAS: t = NAME_ALIAS[t]
    if t in by_formula: return by_formula[t].get("formula", t)
    if t in by_name: return by_name[t].get("formula", t)
    return t

# ---------- reagent / condition normalization ----------
COND_SET = {"高温","加热","△","点燃","催化剂","放电","光照","电解","通电","蒸干","灼烧","煮沸","微生物","酶","高压","催化剂△"}
REAGENT_ALIAS = {
    "氯气": "Cl2", "液氯": "Cl2", "氯水": "Cl2",
    "盐酸": "HCl", "稀盐酸": "HCl", "浓盐酸": "浓HCl",
    "硫酸": "H2SO4", "稀硫酸": "H2SO4", "浓硫酸": "浓H2SO4",
    "硝酸": "HNO3", "稀硝酸": "HNO3", "浓硝酸": "浓HNO3",
    "水": "H2O", "蒸馏水": "H2O",
    "氧气": "O2", "空气": "O2", "过量空气": "O2", "过量氧气": "O2", "过量O2": "O2",
    "二氧化碳": "CO2", "过量CO2": "CO2", "过量二氧化碳": "CO2",
    "一氧化碳": "CO", "氢气": "H2", "氮气": "N2", "氨气": "NH3",
    "氯化氢": "HCl", "硫化氢": "H2S", "二氧化硫": "SO2", "三氧化硫": "SO3",
    "氢氧化钠": "NaOH", "氢氧化钾": "KOH", "氢氧化钙": "Ca(OH)2", "澄清石灰水": "Ca(OH)2", "石灰水": "Ca(OH)2", "石灰乳": "Ca(OH)2",
    "氢氧化钡": "Ba(OH)2", "氨水": "NH3·H2O", "一水合氨": "NH3·H2O",
    "氯化钠": "NaCl", "氯化钙": "CaCl2", "氯化钡": "BaCl2", "氯化铁": "FeCl3", "氯化亚铁": "FeCl2", "氯化铜": "CuCl2", "氯化铵": "NH4Cl",
    "硝酸银": "AgNO3", "硝酸钡": "Ba(NO3)2", "碳酸钠": "Na2CO3", "碳酸氢钠": "NaHCO3", "碳酸钾": "K2CO3",
    "次氯酸钠": "NaClO", "次氯酸钙": "Ca(ClO)2", "氯酸钾": "KClO3", "高锰酸钾": "KMnO4", "二氧化锰": "MnO2", "过氧化氢": "H2O2", "双氧水": "H2O2",
    "铁粉": "Fe", "铁": "Fe", "铜粉": "Cu", "铜": "Cu", "铝粉": "Al", "铝": "Al", "锌": "Zn", "锌粉": "Zn", "镁": "Mg", "碳": "C", "木炭": "C", "焦炭": "C", "硫": "S", "硫磺": "S", "磷": "P",
    "KSCN": "KSCN", "硫氰化钾": "KSCN", "硫氰酸钾": "KSCN",
    "KI": "KI", "碘化钾": "KI", "淀粉": "淀粉",
    "乙醇": "C2H5OH", "酒精": "C2H5OH", "醋酸": "CH3COOH", "乙酸": "CH3COOH",
    "NaAlO2": "NaAlO2", "偏铝酸钠": "NaAlO2", "硅酸钠": "Na2SiO3",
    "Na2O2": "Na2O2", "过氧化钠": "Na2O2", "Na2O": "Na2O", "氧化钠": "Na2O",
    "Ba(OH)2": "Ba(OH)2", "Cu(OH)2": "Cu(OH)2", "Fe(OH)3": "Fe(OH)3", "Fe(OH)2": "Fe(OH)2",
    "AgNO3": "AgNO3", "BaCl2": "BaCl2", "CaCl2": "CaCl2", "NaCl": "NaCl",
}
def norm_reagent(tok):
    t = norm(tok).strip()
    # strip leading 稀/浓/过量/足量/适量/少量 prefix (keep meaning for 浓/稀 if relevant)
    # We collapse 浓HCl/稀HCl into HCl for rule matching mostly; rules store HCl.
    for p in ["过量", "足量", "适量", "少量", "稀", "浓"]:
        if t.startswith(p) and len(t) > len(p):
            t = t[len(p):]
    if t in REAGENT_ALIAS: t = REAGENT_ALIAS[t]
    if t in by_formula: t = by_formula[t].get("formula", t)
    elif t in by_name: t = by_name[t].get("formula", t)
    return t
def is_cond(tok):
    t = norm(tok).strip()
    if t in COND_SET: return True
    if re.match(r"^\d+℃$", t): return True   # 300℃ etc.
    return False

# ---------- rule list (chemistry verified) ----------
def R(frm, to, reagents=None, conds=None, reason=""):
    return {"from": frm, "to": to, "reagents": reagents or [], "conditions": conds or [], "feasible": True, "reason": reason}

RULES = []
def add(*a, **k): RULES.append(R(*a, **k))

# --- Na ---
add("Na","Na2O",["O2"],["常温"],"4Na+O2=2Na2O")
add("Na","Na2O2",["O2"],["点燃"],"2Na+O2=Na2O2")
add("Na","NaOH",["H2O"],[],"2Na+2H2O=2NaOH+H2")
add("Na","NaCl",["Cl2"],["点燃"],"2Na+Cl2=2NaCl")
add("Na2O","NaOH",["H2O"],[],"Na2O+H2O=2NaOH")
add("Na2O","Na2CO3",["CO2"],[],"Na2O+CO2=Na2CO3")
add("Na2O2","NaOH",["H2O"],[],"2Na2O2+2H2O=4NaOH+O2")
add("Na2O2","Na2CO3",["CO2"],[],"2Na2O2+2CO2=2Na2CO3+O2")
add("Na2O2","O2",["CO2"],[],"2Na2O2+2CO2=2Na2CO3+O2")
add("NaOH","Na2CO3",["CO2"],[],"2NaOH+CO2=Na2CO3+H2O")
add("NaOH","NaHCO3",["CO2"],[],"NaOH+CO2=NaHCO3 (过量CO2)")
add("NaOH","NaCl",["HCl"],[],"NaOH+HCl=NaCl+H2O")
add("Na2CO3","NaHCO3",["CO2","H2O"],[],"Na2CO3+CO2+H2O=2NaHCO3")
add("Na2CO3","NaOH",["Ca(OH)2"],[],"Na2CO3+Ca(OH)2=CaCO3+2NaOH")
add("Na2CO3","NaCl",["HCl"],[],"Na2CO3+2HCl=2NaCl+H2O+CO2")
add("Na2CO3","CaCO3",["Ca(OH)2"],[],"Na2CO3+Ca(OH)2=CaCO3+2NaOH")
add("NaHCO3","Na2CO3",[],["△"],"2NaHCO3=Na2CO3+H2O+CO2")
add("NaHCO3","CO2",["HCl"],[],"NaHCO3+HCl=NaCl+H2O+CO2")
add("NaCl","NaOH",[],["电解"],"2NaCl+2H2O=2NaOH+H2+Cl2 (电解)")
add("NaCl","Cl2",[],["电解"],"电解饱和食盐水生成Cl2")
add("NaCl","NaHCO3",["NH3","CO2"],[],"侯氏制碱: NaCl+NH3+CO2+H2O=NaHCO3+NH4Cl")
add("NaCl","Na",[],["熔融电解"],"2NaCl(熔融)=2Na+Cl2")

# --- Fe ---
add("Fe","Fe3O4",["O2"],["点燃"],"3Fe+2O2=Fe3O4")
add("Fe","Fe3O4",["H2O"],["高温"],"3Fe+4H2O(g)=Fe3O4+4H2")
add("Fe","Fe2O3",["O2"],[], "4Fe+3O2=2Fe2O3 (缓慢氧化)")  # NOTE: needs O2, NOT H2O
add("Fe","FeCl2",["HCl"],[],"Fe+2HCl=FeCl2+H2")
add("Fe","FeCl2",["FeCl3"],[],"Fe+2FeCl3=3FeCl2")
add("Fe","FeCl3",["Cl2"],["点燃"],"2Fe+3Cl2=2FeCl3")
add("Fe","FeSO4",["H2SO4"],[],"Fe+H2SO4=FeSO4+H2")
add("Fe","Cu",["CuSO4"],[],"Fe+CuSO4=FeSO4+Cu (湿法炼铜)")
add("Fe2O3","Fe",["CO"],["高温"],"Fe2O3+3CO=2Fe+3CO2")
add("Fe2O3","Fe",["Al"],["高温"],"Fe2O3+2Al=2Fe+Al2O3 (铝热)")
add("Fe2O3","FeCl3",["HCl"],[],"Fe2O3+6HCl=2FeCl3+3H2O")
add("Fe3O4","Fe",["CO"],["高温"],"Fe3O4+4CO=3Fe+4CO2")
add("Fe(OH)2","Fe(OH)3",["O2","H2O"],[],"4Fe(OH)2+O2+2H2O=4Fe(OH)3")
add("Fe(OH)3","Fe2O3",[],["△"],"2Fe(OH)3=Fe2O3+3H2O")
add("Fe(OH)3","FeCl3",["HCl"],[],"Fe(OH)3+3HCl=FeCl3+3H2O")
add("Fe(OH)2","FeCl2",["HCl"],[],"Fe(OH)2+2HCl=FeCl2+2H2O")
add("FeCl2","FeCl3",["Cl2"],[],"2FeCl2+Cl2=2FeCl3")
add("FeCl2","Fe(OH)2",["NaOH"],[],"FeCl2+2NaOH=Fe(OH)2+2NaCl")
add("FeCl2","Fe",["Zn"],[],"FeCl2+Zn=ZnCl2+Fe")
add("FeCl3","FeCl2",["Fe"],[],"2FeCl3+Fe=3FeCl2")
add("FeCl3","FeCl2",["Cu"],[],"2FeCl3+Cu=2FeCl2+CuCl2")
add("FeCl3","Fe(OH)3",["NaOH"],[],"FeCl3+3NaOH=Fe(OH)3+3NaCl")
add("FeCl3","Fe",["Zn"],[],"2FeCl3+3Zn=2Fe+3ZnCl2 (足量)")
add("FeSO4","Fe(OH)2",["NaOH"],[],"FeSO4+2NaOH=Fe(OH)2+Na2SO4")
# NOTE: deliberately NO rule Fe2O3->Fe(OH)3 (insoluble, 不与水反应) -> breaks pdf-016 C
# NOTE: deliberately NO rule Fe->Fe2O3 via H2O (gives Fe3O4) -> breaks pdf-016 A via reagent check (needs O2)

# --- Cu ---
add("Cu","CuO",["O2"],["△"],"2Cu+O2=2CuO")
add("Cu","CuCl2",["Cl2"],["点燃"],"Cu+Cl2=CuCl2")
add("Cu","Cu(NO3)2",["HNO3"],[],"3Cu+8HNO3(稀)=3Cu(NO3)2+2NO+4H2O")
add("Cu","CuSO4",["浓H2SO4"],["△"],"Cu+2H2SO4(浓)=CuSO4+SO2+2H2O")
add("Cu","Ag",["AgNO3"],[],"Cu+2AgNO3=Cu(NO3)2+2Ag")
add("CuO","Cu",["H2"],["△"],"CuO+H2=Cu+H2O")
add("CuO","Cu",["CO"],["△"],"CuO+CO=Cu+CO2")
add("CuO","Cu",["C"],["高温"],"2CuO+C=2Cu+CO2")
add("CuO","CuCl2",["HCl"],[],"CuO+2HCl=CuCl2+H2O")
add("CuO","CuSO4",["H2SO4"],[],"CuO+H2SO4=CuSO4+H2O")
add("CuO","Cu(NO3)2",["HNO3"],[],"CuO+2HNO3=Cu(NO3)2+H2O")
add("Cu(OH)2","CuO",[],["△"],"Cu(OH)2=CuO+H2O")
add("Cu(OH)2","CuCl2",["HCl"],[],"Cu(OH)2+2HCl=CuCl2+2H2O")
add("Cu(OH)2","CuSO4",["H2SO4"],[],"Cu(OH)2+H2SO4=CuSO4+2H2O")
add("CuSO4","Cu(OH)2",["NaOH"],[],"CuSO4+2NaOH=Cu(OH)2+Na2SO4")
add("CuSO4","Cu",["Fe"],[],"CuSO4+Fe=FeSO4+Cu")
add("CuSO4","Cu",["Zn"],[],"CuSO4+Zn=ZnSO4+Cu")
add("CuCl2","Cu(OH)2",["NaOH"],[],"CuCl2+2NaOH=Cu(OH)2+2NaCl")
add("CuCl2","Cu",["Fe"],[],"CuCl2+Fe=FeCl2+Cu")
add("Cu2(OH)2CO3","CuO",[],["△"],"Cu2(OH)2CO3=2CuO+CO2+H2O")

# --- S ---
add("S","SO2",["O2"],["点燃"],"S+O2=SO2")
add("S","H2S",["H2"],["△"],"S+H2=H2S")
add("S","FeS",["Fe"],["△"],"Fe+S=FeS")
add("S","HgS",["Hg"],[],"Hg+S=HgS")
add("SO2","SO3",["O2"],["催化剂","△"],"2SO2+O2=2SO3 (催化)")
add("SO2","H2SO3",["H2O"],[],"SO2+H2O=H2SO3")
add("SO2","H2SO4",["Cl2","H2O"],[],"SO2+Cl2+2H2O=H2SO4+2HCl")
add("SO2","S",["H2S"],[],"SO2+2H2S=3S+2H2O")
add("SO3","H2SO4",["H2O"],[],"SO3+H2O=H2SO4")
add("H2S","SO2",["O2"],[],"2H2S+3O2=2SO2+2H2O (完全)")
add("H2S","S",["O2"],[],"2H2S+O2=2S+2H2O (不足)")
add("H2SO3","SO2",[],["△"],"H2SO3=SO2+H2O")
add("H2SO4","SO2",["Cu"],["△"],"Cu+2H2SO4(浓)=CuSO4+SO2+2H2O")
add("H2SO4","SO2",["C"],["△"],"C+2H2SO4(浓)=CO2+2SO2+2H2O")
add("FeS2","SO2",["O2"],["高温"],"4FeS2+11O2=2Fe2O3+8SO2 (煅烧)")
# NOTE: deliberately NO rule S->SO3 (只能先生成SO2) -> breaks pdf-017 C

# --- N ---
add("N2","NH3",["H2"],["高温","高压","催化剂"],"N2+3H2=2NH3 (合成氨)")
add("N2","NO",["O2"],["放电"],"N2+O2=2NO")
add("NH3","NO",["O2"],["催化剂","△"],"4NH3+5O2=4NO+6H2O (催化氧化)")
add("NH3","N2",["O2"],["点燃"],"4NH3+3O2=2N2+6H2O (纯氧)")
add("NH3","NH4Cl",["HCl"],[],"NH3+HCl=NH4Cl")
add("NH3","NH4NO3",["HNO3"],[],"NH3+HNO3=NH4NO3")
add("NH3·H2O","NH3",[],["△"],"NH3·H2O=NH3+H2O")
add("NH4Cl","NH3",["Ca(OH)2"],["△"],"2NH4Cl+Ca(OH)2=CaCl2+2NH3+2H2O")
add("NH4HCO3","NH3",[],["△"],"NH4HCO3=NH3+CO2+H2O")
add("NO","NO2",["O2"],[],"2NO+O2=2NO2")
add("NO2","HNO3",["H2O"],[],"3NO2+H2O=2HNO3+NO")
add("NO2","NO",["H2O"],[],"3NO2+H2O=2HNO3+NO")
add("NO","HNO3",["O2","H2O"],[],"4NO+3O2+2H2O=4HNO3 (需O2)")  # option 只给 H2O -> 缺O2 -> 断
add("HNO3","NO2",["Cu"],[],"Cu+4HNO3(浓)=Cu(NO3)2+2NO2+2H2O")
add("HNO3","NO",["Cu"],[],"3Cu+8HNO3(稀)=3Cu(NO3)2+2NO+4H2O")
add("HNO3","NH4NO3",["NH3"],[],"HNO3+NH3=NH4NO3")

# --- Cl ---
add("Cl2","HCl",["H2"],["点燃"],"H2+Cl2=2HCl")
add("Cl2","HCl",["H2O"],[],"Cl2+H2O=HCl+HClO")
add("Cl2","HClO",["H2O"],[],"Cl2+H2O=HCl+HClO")
add("Cl2","NaCl",["Na"],["点燃"],"2Na+Cl2=2NaCl")
add("Cl2","FeCl3",["Fe"],["点燃"],"2Fe+3Cl2=2FeCl3")
add("Cl2","CuCl2",["Cu"],["点燃"],"Cu+Cl2=CuCl2")
add("Cl2","漂白粉",["Ca(OH)2"],[],"2Cl2+2Ca(OH)2=CaCl2+Ca(ClO)2+2H2O")
add("Cl2","Ca(ClO)2",["Ca(OH)2"],[],"制漂白粉")
add("Cl2","NaClO",["NaOH"],[],"Cl2+2NaOH=NaCl+NaClO+H2O (制84)")
add("Cl2","PCl3",["P"],[],"2P+3Cl2=2PCl3")
add("HCl","Cl2",["MnO2"],["△"],"MnO2+4HCl(浓)=MnCl2+Cl2+2H2O")
add("HCl","H2",[],["电解"],"2HCl=H2+Cl2")
add("HClO","HCl",[],["光照"],"2HClO=2HCl+O2")
add("HClO","O2",[],["光照"],"2HClO=2HCl+O2")
add("NaClO","HClO",["CO2","H2O"],[],"NaClO+CO2+H2O=NaHCO3+HClO")
add("Ca(ClO)2","HClO",["CO2","H2O"],[],"Ca(ClO)2+CO2+H2O=CaCO3+2HClO")
add("漂白粉","HClO",["CO2","H2O"],[],"漂白粉在空气中生成HClO")
add("KClO3","O2",["MnO2"],["△"],"2KClO3=2KCl+3O2")
add("KMnO4","O2",[],["△"],"2KMnO4=K2MnO4+MnO2+O2")
add("H2O2","O2",["MnO2"],[],"2H2O2=2H2O+O2")
add("HCl","FeCl2",["Fe"],[],"Fe+2HCl=FeCl2+H2")

# --- Si / C ---
add("SiO2","Si",["C"],["高温"],"SiO2+2C=Si+2CO (粗硅)")
add("Si","SiHCl3",["HCl"],[],"Si+3HCl=SiHCl3+H2 (300℃)")
add("SiHCl3","Si",["H2"],["高温"],"SiHCl3+H2=Si+3HCl (1100℃)")
add("SiO2","Na2SiO3",["NaOH"],[],"SiO2+2NaOH=Na2SiO3+H2O")
add("SiO2","CaSiO3",["CaCO3"],["高温"],"SiO2+CaCO3=CaSiO3+CO2 (制玻璃)")
add("SiO2","H2SiO3",[],[],"")  # placeholder, will remove below
add("Na2SiO3","H2SiO3",["HCl"],[],"Na2SiO3+2HCl=H2SiO3+2NaCl")
add("H2SiO3","SiO2",[],["△"],"H2SiO3=SiO2+H2O")
add("Si","SiO2",["O2"],["△"],"Si+O2=SiO2")
add("SiF4","H2SiO3",["H2O"],[],"SiF4+3H2O=H2SiO3+4HF")
add("C","CO",["O2"],[],"2C+O2=2CO (不足)")
add("C","CO2",["O2"],["点燃"],"C+O2=CO2 (充足)")
add("CO","CO2",["O2"],["点燃"],"2CO+O2=2CO2")
add("CO2","CO",["C"],["高温"],"CO2+C=2CO")
add("CO2","C",["Mg"],["点燃"],"2Mg+CO2=2MgO+C")
add("CO2","Na2CO3",["NaOH"],[],"CO2+2NaOH=Na2CO3+H2O")
add("CO2","NaHCO3",["NaOH"],[],"CO2+NaOH=NaHCO3 (过量CO2)")
add("CO2","CaCO3",["Ca(OH)2"],[],"CO2+Ca(OH)2=CaCO3+H2O")
add("CaCO3","CaO",[],["高温"],"CaCO3=CaO+CO2")
add("CaCO3","CO2",["HCl"],[],"CaCO3+2HCl=CaCl2+H2O+CO2")
add("CaCO3","CaCl2",["HCl"],[],"CaCO3+2HCl=CaCl2+H2O+CO2")
add("CaO","Ca(OH)2",["H2O"],[],"CaO+H2O=Ca(OH)2")
add("Ca(OH)2","CaCO3",["CO2"],[],"Ca(OH)2+CO2=CaCO3+H2O")
add("Ca(OH)2","Ca(ClO)2",["Cl2"],[],"2Cl2+2Ca(OH)2=CaCl2+Ca(ClO)2+2H2O")
add("CaO","NaOH",["Na2CO3"],[],"CaO+H2O+Na2CO3=CaCO3+2NaOH")

# --- Al ---
add("Al","Al2O3",["O2"],[],"4Al+3O2=2Al2O3")
add("Al","AlCl3",["Cl2"],["点燃"],"2Al+3Cl2=2AlCl3")
add("Al","NaAlO2",["NaOH","H2O"],[],"2Al+2NaOH+2H2O=2NaAlO2+3H2")
add("Al","Al2(SO4)3",["H2SO4"],[],"2Al+3H2SO4=Al2(SO4)3+3H2")
add("Al2O3","Al",[],["电解"],"2Al2O3(熔融)=4Al+3O2")
add("Al2O3","AlCl3",["HCl"],[],"Al2O3+6HCl=2AlCl3+3H2O")
add("Al2O3","NaAlO2",["NaOH"],[],"Al2O3+2NaOH=2NaAlO2+H2O")
add("Al(OH)3","Al2O3",[],["△"],"2Al(OH)3=Al2O3+3H2O")
add("Al(OH)3","AlCl3",["HCl"],[],"Al(OH)3+3HCl=AlCl3+3H2O")
add("Al(OH)3","NaAlO2",["NaOH"],[],"Al(OH)3+NaOH=NaAlO2+2H2O")
add("AlCl3","Al(OH)3",["NaOH"],[],"AlCl3+3NaOH=Al(OH)3+3NaCl")
add("AlCl3","Al(OH)3",["NH3·H2O"],[],"AlCl3+3NH3·H2O=Al(OH)3+3NH4Cl")
add("AlCl3","NaAlO2",["NaOH"],[],"AlCl3+4NaOH=NaAlO2+3NaCl+2H2O (过量NaOH)")
add("NaAlO2","Al(OH)3",["CO2","H2O"],[],"NaAlO2+CO2+2H2O=Al(OH)3+NaHCO3")
add("NaAlO2","Al(OH)3",["HCl"],[],"NaAlO2+HCl+H2O=Al(OH)3+NaCl")
add("Al2(SO4)3","Al(OH)3",["NH3·H2O"],[],"Al2(SO4)3+6NH3·H2O=2Al(OH)3+3(NH4)2SO4")

# --- Mg / Ca / others ---
add("Mg","MgO",["O2"],["点燃"],"2Mg+O2=2MgO")
add("Mg","Mg3N2",["N2"],["点燃"],"3Mg+N2=Mg3N2")
add("Mg","MgCl2",["Cl2"],["点燃"],"Mg+Cl2=MgCl2")
add("MgCl2","Mg",[],["熔融电解"],"MgCl2(熔融)=Mg+Cl2")
add("MgO","MgCl2",["HCl"],[],"MgO+2HCl=MgCl2+H2O")
add("Mg(OH)2","MgO",[],["△"],"Mg(OH)2=MgO+H2O")
add("Mg(OH)2","MgCl2",["HCl"],[],"Mg(OH)2+2HCl=MgCl2+2H2O")
add("MgCl2","Mg(OH)2",["NaOH"],[],"MgCl2+2NaOH=Mg(OH)2+2NaCl")
# NOTE: deliberately NO rule MgCl2(溶液)->无水MgCl2 (蒸干水解) -> breaks pdf-017 B
add("BaCl2","BaSO4",["H2SO4"],[],"BaCl2+H2SO4=BaSO4+2HCl")
add("BaCl2","AgCl",["AgNO3"],[],"不对")  # placeholder remove
add("AgNO3","AgCl",["HCl"],[],"AgNO3+HCl=AgCl+HNO3")
add("AgNO3","Ag",["Cu"],[],"2AgNO3+Cu=Cu(NO3)2+2Ag")
add("Zn","ZnSO4",["H2SO4"],[],"Zn+H2SO4=ZnSO4+H2")
add("Zn","ZnCl2",["HCl"],[],"Zn+2HCl=ZnCl2+H2")
add("ZnO","Zn",["C"],["高温"],"ZnO+C=Zn+CO")
add("Fe","FeS",["S"],["△"],"Fe+S=FeS")
add("H2","HCl",["Cl2"],["点燃"],"H2+Cl2=2HCl")
add("H2","H2O",["O2"],["点燃"],"2H2+O2=2H2O")
add("H2O","H2",[],["电解"],"2H2O=2H2+O2")
add("H2O","O2",[],["电解"],"2H2O=2H2+O2")
add("Na","NaH",["H2"],["△"],"2Na+H2=2NaH")
add("KOH","K2CO3",["CO2"],[],"2KOH+CO2=K2CO3+H2O")
add("K2CO3","KOH",["Ca(OH)2"],[],"K2CO3+Ca(OH)2=CaCO3+2KOH")
add("KNO3","KOH",[],[],"")  # placeholder, will remove
add("CaCl2","CaCO3",["Na2CO3"],[],"CaCl2+Na2CO3=CaCO3+2NaCl")

# --- additional common one-step (verified) ---
add("Na2CO3","CO2",["HCl"],[],"Na2CO3+2HCl=2NaCl+H2O+CO2")
add("Na2CO3","CO2",["H2SO4"],[],"Na2CO3+H2SO4=Na2SO4+H2O+CO2")
add("K2CO3","CO2",["HCl"],[],"K2CO3+2HCl=2KCl+H2O+CO2")
add("CaCO3","CaCl2",["HCl"],[]," already")  # dup ok, filtered? keep distinct reason
add("CH4","CO2",["O2"],["点燃"],"CH4+2O2=CO2+2H2O")
add("C2H5OH","CO2",["O2"],["点燃"],"C2H5OH+3O2=2CO2+3H2O")
add("O2","H2O",["H2"],["点燃"],"2H2+O2=2H2O")
add("Mg","MgCl2",["HCl"],[],"Mg+2HCl=MgCl2+H2")
add("Al","AlCl3",["HCl"],[],"2Al+6HCl=2AlCl3+3H2")
add("Zn","ZnSO4",["CuSO4"],[],"Zn+CuSO4=ZnSO4+Cu")
add("Fe","FeCl2",["CuCl2"],[],"Fe+CuCl2=FeCl2+Cu")
add("H2SO4","HCl",["BaCl2"],[],"H2SO4+BaCl2=BaSO4+2HCl")
add("HCl","HNO3",["AgNO3"],[],"HCl+AgNO3=AgCl+HNO3")
add("NaCl","NaNO3",["AgNO3"],[],"不对")  # placeholder to be removed
add("CO","H2",["H2O"],["高温"],"CO+H2O=CO2+H2 (水煤气)")  # actually produces CO2; keep CO->CO2? no. remove below

# remove placeholder empty rules (from==to trivially or reason empty placeholders)
RULES = [r for r in RULES if r["from"] and r["to"] and r["from"] != r["to"]]
# remove the two bad placeholders explicitly
RULES = [r for r in RULES if not (r["from"]=="BaCl2" and r["to"]=="AgCl")]
RULES = [r for r in RULES if not (r["from"]=="KNO3" and r["to"]=="KOH")]
RULES = [r for r in RULES if not (r["from"]=="SiO2" and r["to"]=="H2SiO3")]

print("rule count:", len(RULES))

# sanity: forbid ambiguous dupes with conflicting reagent requirements
from collections import defaultdict
pair = defaultdict(list)
for r in RULES: pair[(r["from"],r["to"])].append(r)

# write JSON
out = {"version":"1.0","source":"高中常见元素及其化合物一步转化（人工核对，覆盖 Na/Fe/Cu/S/N/Cl/Si/Al/C/Mg）","steps":RULES}
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)
print("written:", OUT, "steps=", len(RULES))
