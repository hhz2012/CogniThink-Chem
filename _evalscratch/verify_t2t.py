# -*- coding: utf-8 -*-
"""Mirror TransformationChainValidator on the generated rules. Verify pdf
targets + t2t questions resolve to expected unique answer (or safe null).
"""
import json, re
ROOT = r"d:\github\CogniThink-Chem"

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

cls = json.load(open(ROOT + r"\data\highschool\substance-classification.json", encoding="utf-8"))
by_formula, by_name = {}, {}
for s in cls["substances"]:
    nf = norm(s.get("formula", ""))
    if nf: by_formula.setdefault(nf, s)
    for nm in s.get("names", []):
        nn = norm(nm)
        if nn: by_name.setdefault(nn, s)

NAME_ALIAS = {"食盐水":"NaCl","饱和食盐水":"NaCl","盐水":"NaCl","漂白粉":"漂白粉","小苏打":"NaHCO3","苏打":"Na2CO3","纯碱":"Na2CO3","熟石灰":"Ca(OH)2","消石灰":"Ca(OH)2","澄清石灰水":"Ca(OH)2","石灰水":"Ca(OH)2","石灰乳":"Ca(OH)2","生石灰":"CaO","石灰石":"CaCO3","大理石":"CaCO3","烧碱":"NaOH","火碱":"NaOH","苛性钠":"NaOH","苛性钾":"KOH","干冰":"CO2","冰":"H2O","水银":"Hg","硫磺":"S","硫":"S","液氯":"Cl2","氯水":"Cl2","液氨":"NH3","氨水":"NH3·H2O","双氧水":"H2O2","过氧化氢":"H2O2","沼气":"CH4","天然气":"CH4","甲烷":"CH4","酒精":"C2H5OH","乙醇":"C2H5OH","醋酸":"CH3COOH","乙酸":"CH3COOH","葡萄糖":"C6H12O6","蔗糖":"C12H22O11","水玻璃":"Na2SiO3","金刚砂":"SiC","胆矾":"CuSO4","蓝矾":"CuSO4","绿矾":"FeSO4","明矾":"KAl(SO4)2","芒硝":"Na2SO4","石膏":"CaSO4","重晶石":"BaSO4","硅石":"SiO2","石英":"SiO2","水晶":"SiO2","光导纤维":"SiO2","刚玉":"Al2O3","铝土矿":"Al2O3","赤铁矿":"Fe2O3","磁铁矿":"Fe3O4","食盐":"NaCl","粗盐":"NaCl"}
PREFIX_STRIP = ["饱和","无水","粗","高纯","新制","稀","浓"]
STATE_SUFFIX = ["溶液","熔融","固体","液体","气体","水溶液","悬浊液","乳浊液","胶体"]
def norm_sub(tok):
    t = norm(tok).strip()
    t = re.sub(r"[\(（](?:s|l|g|aq|S|L|G|AQ|固体|液体|气体|溶液|熔融)[\)）]\s*$", "", t)
    t = re.sub(r"^\s*[\(（](?:粗|纯|高纯|无水|新制|稀|浓)[\)）]\s*", "", t)
    ch = True
    while ch:
        ch = False
        for p in PREFIX_STRIP:
            if t.startswith(p) and len(t) > len(p):
                t = t[len(p):]; ch = True; break
    ch = True
    while ch:
        ch = False
        for s in STATE_SUFFIX:
            if t.endswith(s) and len(t) > len(s):
                t = t[:-len(s)]; ch = True; break
    t = t.strip()
    if t in NAME_ALIAS: t = NAME_ALIAS[t]
    if t in by_formula: return by_formula[t].get("formula", t)
    if t in by_name: return by_name[t].get("formula", t)
    return t

COND_SET = {"高温","加热","△","点燃","催化剂","放电","光照","电解","通电","蒸干","灼烧","煮沸","微生物","酶","高压"}
REAGENT_ALIAS = {"氯气":"Cl2","液氯":"Cl2","氯水":"Cl2","盐酸":"HCl","稀盐酸":"HCl","浓盐酸":"浓HCl","硫酸":"H2SO4","稀硫酸":"H2SO4","浓硫酸":"浓H2SO4","硝酸":"HNO3","稀硝酸":"HNO3","浓硝酸":"浓HNO3","水":"H2O","蒸馏水":"H2O","氧气":"O2","空气":"O2","过量空气":"O2","过量氧气":"O2","二氧化碳":"CO2","过量CO2":"CO2","过量二氧化碳":"CO2","一氧化碳":"CO","氢气":"H2","氮气":"N2","氨气":"NH3","氯化氢":"HCl","硫化氢":"H2S","二氧化硫":"SO2","三氧化硫":"SO3","氢氧化钠":"NaOH","氢氧化钾":"KOH","氢氧化钙":"Ca(OH)2","澄清石灰水":"Ca(OH)2","石灰水":"Ca(OH)2","石灰乳":"Ca(OH)2","氢氧化钡":"Ba(OH)2","氨水":"NH3·H2O","一水合氨":"NH3·H2O","氯化钠":"NaCl","氯化钙":"CaCl2","氯化钡":"BaCl2","氯化铁":"FeCl3","氯化亚铁":"FeCl2","氯化铜":"CuCl2","氯化铵":"NH4Cl","硝酸银":"AgNO3","硝酸钡":"Ba(NO3)2","碳酸钠":"Na2CO3","碳酸氢钠":"NaHCO3","碳酸钾":"K2CO3","次氯酸钠":"NaClO","次氯酸钙":"Ca(ClO)2","氯酸钾":"KClO3","高锰酸钾":"KMnO4","二氧化锰":"MnO2","过氧化氢":"H2O2","双氧水":"H2O2","铁粉":"Fe","铁":"Fe","铜粉":"Cu","铜":"Cu","铝粉":"Al","铝":"Al","锌":"Zn","锌粉":"Zn","镁":"Mg","碳":"C","木炭":"C","焦炭":"C","硫":"S","硫磺":"S","磷":"P","KSCN":"KSCN","硫氰化钾":"KSCN","硫氰酸钾":"KSCN","KI":"KI","碘化钾":"KI","淀粉":"淀粉","乙醇":"C2H5OH","酒精":"C2H5OH","醋酸":"CH3COOH","乙酸":"CH3COOH","NaAlO2":"NaAlO2","偏铝酸钠":"NaAlO2","硅酸钠":"Na2SiO3","Na2O2":"Na2O2","过氧化钠":"Na2O2","Na2O":"Na2O","氧化钠":"Na2O"}
ACID_CONC = {"稀硫酸":"稀H2SO4","浓硫酸":"浓H2SO4","稀盐酸":"稀HCl","浓盐酸":"浓HCl",
             "稀硝酸":"稀HNO3","浓硝酸":"浓HNO3","稀H2SO4":"稀H2SO4","浓H2SO4":"浓H2SO4",
             "稀HCl":"稀HCl","浓HCl":"浓HCl","稀HNO3":"稀HNO3","浓HNO3":"浓HNO3"}
def norm_reagent(tok):
    t = norm(tok).strip()
    if t in ACID_CONC: return ACID_CONC[t]
    for p in ["过量","足量","适量","少量"]:   # preserve 浓/稀; strip 过量等
        if t.startswith(p) and len(t) > len(p):
            t = t[len(p):]
    if t in REAGENT_ALIAS: t = REAGENT_ALIAS[t]
    if t in by_formula: t = by_formula[t].get("formula", t)
    elif t in by_name: t = by_name[t].get("formula", t)
    return t
def _conc(r):
    if r.startswith("浓"): return "浓"
    if r.startswith("稀"): return "稀"
    return ""
def _base(r):
    return re.sub(r"^(浓|稀)", "", r)
def reag_satisfies(req_set, opt_set):
    """Every required reagent must be supplied; concentration qualifier is
    required only when the rule specifies it (generic HCl accepts any)."""
    for r in req_set:
        br, cr = _base(r), _conc(r)
        if not any(_base(o) == br and (cr == "" or _conc(o) == cr) for o in opt_set):
            return False
    return True
def is_cond(tok):
    t = norm(tok).strip()
    if t in COND_SET: return True
    if re.match(r"^\d+℃$", t): return True
    return False

# load rules, build index (from,to) -> list of rule
rules_data = json.load(open(ROOT + r"\data\highschool\transformation-rules.json", encoding="utf-8"))
RULES = rules_data["steps"]
idx = {}
for r in RULES:
    key = (norm(r["from"]), norm(r["to"]))
    idx.setdefault(key, []).append(r)
def norm_rea_list(lst): return set(norm_reagent(x) for x in lst)
def norm_cond_list(lst): return set(norm(x) for x in lst if is_cond(x))

def split_ann(ann):
    if not ann: return set(), set()
    toks = re.split(r"[、，,;；/／\s]+", ann)
    toks = [t for t in toks if t]
    reag, cond = set(), set()
    for t in toks:
        if is_cond(t): cond.add(norm(t))
        else: reag.add(norm_reagent(t))
    return reag, cond

# connector: optional annotation (balanced parens/brackets) + arrow
CONN_RE = re.compile(
    r'--?\s*(\((?:[^()]|\([^()]*\))*\)|\[(?:[^\[\]]|\[[^\[\]]*\])*\])\s*--?>'
    r'|-{1,2}>'
    r'|->'
    r'|→'
)

def _strip_ann_parens(ann):
    if not ann: return ""
    a = ann.strip()
    if (a.startswith("(") and a.endswith(")")) or (a.startswith("[") and a.endswith("]")) \
       or (a.startswith("（") and a.endswith("）")):
        a = a[1:-1]
    return a.strip()

def parse_chain(text):
    """Return list of (from, reag_set, cond_set, to) or None."""
    conns = list(CONN_RE.finditer(text))
    if conns:
        subs = []
        anns = []
        prev = 0
        for m in conns:
            subs.append(text[prev:m.start()])
            anns.append(_strip_ann_parens(m.group(1)))
            prev = m.end()
        subs.append(text[prev:])
        steps = []
        for i in range(len(conns)):
            s0 = re.split(r"[：:]", subs[i])[-1]   # strip leading "label："
            s1 = re.split(r"[：:]", subs[i+1])[-1]
            frm = norm_sub(s0)
            to = norm_sub(s1)
            reag, cond = split_ann(anns[i])
            if frm and to:
                steps.append((frm, reag, cond, to))
        return steps if len(steps) >= 1 else None
    # bare 3-substance format: "C、CO、CO2"
    if "、" in text or "," in text:
        toks = re.split(r"[、，,]", text)
        toks = [norm_sub(t) for t in toks if t.strip()]
        toks = [t for t in toks if t]
        if len(toks) >= 2:
            return [(toks[i], set(), set(), toks[i+1]) for i in range(len(toks)-1)]
    return None

def step_feasible(frm, reag, cond, to):
    """True=feasible, False=broken, None=unknown(-> whole chain null)."""
    matches = idx.get((frm, to), [])
    if not matches:
        return False  # no rule for this pair -> broken
    if not reag and not cond:
        # truly bare arrow (e.g. 3-substance "甲、乙、丙", conditions omitted): pair existence suffices.
        return True
    if not reag:
        # condition-only arrow (e.g. "--(△)-->"): must match a rule that consumes NO reagent
        # (decomposition / condition-driven). A rule needing a reagent is not satisfied by △ alone
        # (prevents false-feasible like Na2CO3 --(△)--> NaOH, which actually needs Ca(OH)2).
        for r in matches:
            if not norm_rea_list(r.get("reagents", [])):
                return True
        return False
    # reagent-bearing arrow -> require a matching rule that explicitly consumes reagents (non-empty
    # required set) and supplies all of them. A rule needing no reagent does NOT satisfy a
    # reagent-bearing option (prevents false-feasible like Cu(OH)2 --(H2O)--> CuO).
    for r in matches:
        req_r = norm_rea_list(r.get("reagents", []))
        if not req_r:
            continue
        if reag_satisfies(req_r, reag):
            return True
    return False

def chain_ok(steps):
    res = []
    for (frm, reag, cond, to) in steps:
        f = step_feasible(frm, reag, cond, to)
        if f is None:
            return None
        res.append(f)
    return all(res)

def ask_dir_t(stem):
    if re.search(r"不能实现|无法实现|不能一步|不符合|错误|不正确", stem): return "wrong"
    if re.search(r"能实现|可以实现|能够实现|一步实现|符合|正确", stem): return "correct"
    return "?"

def decide(raw, opts):
    """opts: dict letter->text. Returns (letter or None, info)."""
    # determine ask dir from stem (text before first option) - approximate
    stem = raw
    ad = ask_dir_t(stem)
    if ad not in ("correct","wrong"):
        return None, f"askdir={ad}"
    chain_results = {}
    for L, txt in opts.items():
        steps = parse_chain(txt)
        if not steps:
            return None, f"opt {L} not chain"
        ok = chain_ok(steps)
        if ok is None:
            return None, f"opt {L} unknown"
        chain_results[L] = ok
    letters = sorted(chain_results.keys())
    if ad == "correct":
        cands = [l for l in letters if chain_results[l]]
    else:
        cands = [l for l in letters if not chain_results[l]]
    if len(cands) != 1:
        return None, f"not unique: {cands} (results={chain_results})"
    return cands[0], "ok"

# ---------- load a question dict and render ----------
def render(x):
    q = x["question"]
    if x.get("type") == "choice" and x.get("options"):
        q = q + "\n" + "\n".join(f"{k}. {x['options'][k]}" for k in sorted(x["options"].keys()))
    return q, {k: v for k, v in (x.get("options") or {}).items()}

# ---------- test pdf targets ----------
def run_pdf_targets():
    pdf = json.load(open(ROOT + r"\src\CogniThink.TutorBrain.Api\data\pdf_questions.json", encoding="utf-8"))
    pdf_items = pdf if isinstance(pdf, list) else pdf.get("items", [])
    targets = {"pdf-013":"D"