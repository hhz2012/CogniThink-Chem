# -*- coding: utf-8 -*-
"""Mirror NaChoiceValidator: conservative per-option particle-count checking
for NA (Avogadro-constant) multiple-choice questions.

Truth source: data/highschool/na-particle-facts.json.
Rule: an option is parsed only if it is a clean
  "[qty] [unit] [substance] ... [particle] ... [k] NA"
claim. If it involves redox electron transfer, reactions, isotope nuclides not
in the lib, concentrations without volume, etc. -> None (unknown).
A question is decided only if ALL four options are True/False (no None) and the
candidate set is unique. Otherwise -> None (safe, never guess).
"""
import json, re
ROOT = r"d:\github\CogniThink-Chem"

def norm(s):
    if not s: return ""
    out=[]
    for ch in s:
        if ch.isspace(): continue
        o=ord(ch)
        if 0x2080<=o<=0x2089: out.append(chr(ord('0')+(o-0x2080))); continue
        if o==0x2070: out.append('0'); continue
        if o==0x00B9: out.append('1'); continue
        if o==0x00B2: out.append('2'); continue
        if o==0x00B3: out.append('3'); continue
        if 0x2074<=o<=0x2079: out.append(chr(ord('4')+(o-0x2074))); continue
        if o==0x207A: out.append('+'); continue
        if o==0x207B: out.append('-'); continue
        if 0xFF01<=o<=0xFF5E: out.append(chr(o-0xFEE0)); continue
        if o==0x3000: continue
        out.append(ch)
    return "".join(out)

data = json.load(open(ROOT+r"\data\highschool\na-particle-facts.json", encoding="utf-8"))
FACTS = {norm(r["formula"]): r for r in data["formulas"]}

# substance name -> formula aliases (display normalization)
SUB_ALIAS = {"水":"H2O","双氧水":"H2O2","过氧化氢":"H2O2","氨气":"NH3","氨":"NH3",
  "甲烷":"CH4","乙烷":"C2H6","乙烯":"C2H4","乙炔":"C2H2","苯":"C6H6",
  "二氧化碳":"CO2","一氧化碳":"CO","二氧化硫":"SO2","三氧化硫":"SO3",
  "氯化氢":"HCl","硫化氢":"H2S","一氧化氮":"NO","二氧化氮":"NO2",
  "氯气":"Cl2","氧气":"O2","氮气":"N2","臭氧":"O3","氢气":"H2",
  "食盐":"NaCl","氯化钠":"NaCl","烧碱":"NaOH","氢氧化钠":"NaOH",
  "纯碱":"Na2CO3","苏打":"Na2CO3","小苏打":"NaHCO3","碳酸钙":"CaCO3",
  "生石灰":"CaO","熟石灰":"Ca(OH)2","消石灰":"Ca(OH)2","二氧化氯":"ClO2"}

NA_RE = re.compile(r"([\d.]+)\s*(?:N_A|NA|N A|Nₐ)")
DISQUALIFY = re.compile(r"转移电子|转移的电子|转移|充分反应|完全反应|电解|反应|生成|密度之比|质量之比|体积之比|分子数之比|物质的量之比")

STP_RE = re.compile(r"标准状况|标况|0\s*℃|0℃|273\s*K|101\s*kPa|1\.01×10\^5")

def clean_formula(tok):
    t = norm(tok).strip()
    t = re.sub(r"_", "", t)            # H_2O -> H2O
    t = re.sub(r"\^[0-9]*[+-]+$", "", t)  # strip trailing charge ^2- / ^+
    t = re.sub(r"[+-]+$", "", t)       # strip trailing sign (already superscript-normalized)
    if t in SUB_ALIAS: t = SUB_ALIAS[t]
    return t

def lookup(tok):
    f = clean_formula(tok)
    return FACTS.get(f)

def detect_particle(text):
    """Return (field_key, is_ion_specific, ion_token) or None."""
    t = text
    if "σ" in t or "西格玛" in t or "sigma" in t.lower(): return ("sigmaBonds", False, None)
    if "π" in t or "派键" in t or "pi" in t.lower(): return ("piBonds", False, None)
    if "共价键" in t: return ("covalentBonds", False, None)
    if "氢键" in t: return ("hBonds", False, None)
    if re.search(r"离子(?:的)?数目|离子(?:的)?数|离子总数", t): return ("ions", False, None)
    if "分子" in t: return ("molecules", False, None)   # 1 mol 任何物质 = NA 个分子/式单元
    if "电子" in t: return ("electrons", False, None)
    if "质子" in t: return ("protons", False, None)
    if "中子" in t: return ("neutrons", False, None)
    if "原子" in t: return ("atoms", False, None)
    # specific ion token (e.g. CO3^2-, NH4+, H+, OH-, ClO-, F-)
    m = re.search(r"([A-Za-z0-9]{1,6}\^?[0-9]*[+-])", t)
    if m: return ("ion_specific", True, clean_formula(m.group(1)))
    return None

def parse_quantity(text):
    """Return (n_moles_or_None, is_gas_stp, is_solution, substance_or_None, reason)."""
    # disqualify redox / reaction / ratio claims
    if DISQUALIFY.search(text): return (None, False, False, None, "redox/reaction/ratio claim")
    # gas at STP: "标准状况下，22.4 L CO2 ..."
    stp = bool(STP_RE.search(text))
    # solution: "1 L 0.1 mol/L ..." or "1 L 1 mol·L⁻¹ ..."
    sol = re.search(r"([\d.]+)\s*L\s+([\d.]+)\s*mol(?:·L|/L|·L⁻¹|·L-1|Ｌ⁻¹)⁻?¹?-?1?", text)
    if sol:
        V = float(sol.group(1)); c = float(sol.group(2))
        rest = text[sol.end():]
        mtok = re.search(r"([A-Za-z0-9()·]{2,12})", rest)
        if not mtok: return (None, False, True, None, "no solute")
        sub = lookup(mtok.group(1))
        if sub is None: return (None, False, True, None, "solute not in lib")
        return (V*c, False, True, sub, f"solution n={V*c} mol")
    # gas: "<V> L <substance>" ; only meaningful with STP
    mg = re.search(r"([\d.]+)\s*L\s+([A-Za-z0-9()·]{2,12})", text)
    if mg:
        V = float(mg.group(1)); sub = lookup(mg.group(2))
        if sub is None: return (None, True, False, None, "gas not in lib")
        if not stp: return (None, True, False, None, "gas volume without STP")
        if sub.get("stpState") != "gas":
            return ("TRAP_SOLID", True, False, sub, f"{sub['formula']} is {sub['stpState']} at STP")
        return (V/22.4, True, False, sub, f"gas n={V/22.4} mol")
    # mol: "<n> mol <substance>"
    mm = re.search(r"([\d.]+)\s*mol\b(?!.*L)", text)
    if not mm:
        mm = re.search(r"([\d.]+)\s*mol\s+([A-Za-z0-9()·]{2,12})", text)
    if mm:
        n = float(mm.group(1))
        if mm.lastindex and mm.lastindex >= 2:
            sub = lookup(mm.group(2))
        else:
            mt = re.search(r"mol\s+([A-Za-z0-9()·]{2,12})", text)
            sub = lookup(mt.group(1)) if mt else None
        if sub is None: return (None, False, False, None, "substance not in lib")
        return (n, False, False, sub, f"n={n} mol")
    # mass: "<m> g <substance>"
    mgr = re.search(r"([\d.]+)\s*g\s+([A-Za-z0-9()·]{2,12})", text)
    if mgr:
        m = float(mgr.group(1)); sub = lookup(mgr.group(2))
        if sub is None: return (None, False, False, None, "substance not in lib")
        return (m/sub["molarMass"], False, False, sub, f"mass n={m/sub['molarMass']:.3f} mol")
    return (None, False, False, None, "no quantity parse")

def parse_option(text):
    """Return (True/False/None, reason)."""
    # k coefficient from NA claim
    m_na = NA_RE.search(text)
    if m_na:
        k = float(m_na.group(1))
    elif re.search(r"N_A|NA|N A", text):
        k = 1.0
    else:
        return (None, "no NA claim")
    particle = detect_particle(text)
    if particle is None: return (None, "no particle type")
    field, ion_specific, ion_tok = particle
    n, isgas, issol, sub, qreason = parse_quantity(text)
    if n is None: return (None, qreason)
    if n == "TRAP_SOLID":
        return (False, f"{sub['formula']} is {sub['stpState']} at STP; 22.4L≠1mol → claim false")
    # field applicability guard
    if field in ("sigmaBonds","piBonds","covalentBonds") and not sub.get("molecular"):
        return (None, f"bond claim on non-molecular {sub['formula']}")
    if field == "ions" and sub.get("ions",0) <= 0:
        return (None, f"total-ion claim on non-ionic {sub['formula']}")
    if field == "hBonds" and sub.get("hBonds",0) <= 0:
        return (None, f"h-bond claim without data")
    # solution hydrolysis trap: specific ion that hydrolyzes -> nominal claim is false
    if issol and ion_specific and ion_tok and ion_tok in [clean_formula(x) for x in sub.get("hydrolyzableIons",[])]:
        return (False, f"{ion_tok} hydrolyzes -> actual < nominal {k}NA → false")
    if issol and field == "ions":
        return (None, "total-ion in solution needs speciation -> unknown")
    # compute expected per-formula count
    if ion_specific:
        # count of that specific ion per formula — only support if unambiguous via lib ions? skip exact
        return (None, f"specific ion {ion_tok} count not modeled exactly")
    per = 1 if field == "molecules" else sub.get(field, 0)
    expected = n * per
    ok = abs(expected - k) < 1e-6
    return (ok, f"{sub['formula']} n={n:.4f} × {field}={per} = {expected:.4f} vs claimed {k}")

def ask_dir(stem):
    if re.search(r"错误|不正确", stem): return "wrong"
    if re.search(r"正确", stem): return "correct"
    return "?"

def is_na_question(stem):
    return bool(re.search(r"N_A|NA|N A|阿伏伽德罗|阿伏加德罗|Nₐ", stem))

def decide(raw, opts):
    stem = raw
    if not is_na_question(stem): return None, "not NA question"
    ad = ask_dir(stem)
    if ad not in ("correct","wrong"): return None, f"askdir={ad}"
    verdicts = {}
    for L in sorted(opts.keys()):
        v, rsn = parse_option(opts[L])
        if v is None: return None, f"opt {L} unknown: {rsn}"
        verdicts[L] = v
    letters = sorted(verdicts.keys())
    cands = [l for l in letters if verdicts[l]] if ad=="correct" else [l for l in letters if not verdicts[l]]
    if len(cands) != 1: return None, f"not unique {cands} ({verdicts})"
    return cands[0], "ok"

def render(x):
    q = x["question"]
    if x.get("type")=="choice" and x.get("options"):
        q = q + "\n" + "\n".join(f"{k}. {x['options'][k]}" for k in sorted(x["options"].keys()))
    return q, {k:v for k,v in (x.get("options") or {}).items()}

if __name__ == "__main__":
    pdf = json.load(open(ROOT+r"\src\CogniThink.TutorBrain.Api\data\pdf_questions.json", encoding="utf-8"))
    items = pdf if isinstance(pdf,list) else pdf.get("items",[])
    for tid in ["pdf-026","pdf-093","pdf-421","pdf-422","pdf-423","pdf-424","pdf-425","pdf-427","pdf-419"]:
        x = next((i for i in items if i.get("id")==tid), None)
        if not x: continue
        q,o = render(x)
        L,info = decide(q,o)
        print(f"[{tid}] ans={x.get('answer')} got={L} ({info})")
        for kk in sorted(o.keys()):
            v,rsn = parse_option(o[kk])
            print(f"    {kk}: {v}  {rsn[:70]}")
