# -*- coding: utf-8 -*-
"""Dump full option text for equation-choice and ion-choice questions, with classification."""
import json, re

PDF = r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
with open(PDF, encoding="utf-8") as f:
    items = json.load(f)

with open(r"D:\github\CogniThink-Chem\_evalscratch\after_stage3a.json", encoding="utf-8-sig") as f:
    ev = json.load(f)
meta = {}
for p in ("bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"):
    for c in ev.get(p, []):
        meta[c["id"]] = c

# option regex from rendered question text (fallback) ; we prefer options dict
SEP = re.compile(r'[=→⇌⟶]|[-—–]{1,}[^-—–>]*[-—–]{1,}>')
FORMULA = re.compile(r'[A-Z][a-z]?\d*')
# ion token: element(s)+digits then charge sign, e.g. Na+, SO4^2-, Fe3+, OH-
ION = re.compile(r'[A-Z][a-z]?\d*(?:\^?[0-9]*[+\-])')

def has_eq(t):
    if not SEP.search(t or ""): return False
    for sep in ['→','⇌','=','⟶']:
        if sep in t:
            i = t.index(sep)
            return bool(FORMULA.search(t[:i])) and bool(FORMULA.search(t[i+1:]))
    return False

def ion_tokens(t):
    return ION.findall(t or "")

def ask_dir(q):
    q = q or ""
    wrong = any(w in q for w in ["错误的是","不正确","错误的有","下列说法错误","叙述错误","不能","不可能","不可以"])
    correct = any(w in q for w in ["正确的是","正确的有","下列说法正确","叙述正确","可以","可能"])
    # precedence: explicit 错误/不正确 wins
    if any(w in q for w in ["错误的是","不正确","错误的有"]): return "wrong"
    if any(w in q for w in ["正确的是","正确的有"]): return "correct"
    if "错误" in q: return "wrong"
    if "正确" in q: return "correct"
    return "?"

eq_items = []
ion_items = []
for x in items:
    if x.get("type") != "choice" or not x.get("options"): continue
    opts = x["options"]
    eq_opts = [k for k,v in opts.items() if has_eq(v)]
    ion_opts = [k for k,v in opts.items() if len(ion_tokens(v)) >= 2]
    if eq_opts:
        eq_items.append((x, eq_opts, ion_opts))
    if ion_opts:
        ion_items.append((x, eq_opts, ion_opts))

def pct(a,b): return f"{a}/{b}" if b else "0/0"

print("="*80)
print(f"TOTAL choice items: {sum(1 for x in items if x.get('type')=='choice' and x.get('options'))}")
print(f"  with >=1 equation option: {len(eq_items)}")
print(f"  with >=1 ion-group option: {len(ion_items)}")
# overlap
eq_ids = {x[0]['id'] for x in eq_items}
ion_ids = {x[0]['id'] for x in ion_items}
print(f"  overlap (both eq and ion options): {len(eq_ids & ion_ids)}")
print(f"  eq-only: {len(eq_ids - ion_ids)}; ion-only: {len(ion_ids - eq_ids)}")

# pure LLM correctness on these
def pl(x): return meta.get(x["id"],{}).get("pureLlmCorrect")
eq_pl_right = sum(1 for x,_,_ in eq_items if pl(x))
ion_pl_right = sum(1 for x,_,_ in ion_items if pl(x))
print(f"  eq-choice pureLlmCorrect: {pct(eq_pl_right,len(eq_items))}")
print(f"  ion-choice pureLlmCorrect: {pct(ion_pl_right,len(ion_items))}")

# clean coexistence: all 4 options are ion groups (no equation), ideal for coexistence dispatch
clean_ion = [x for x,eo,_ in ion_items if not eo]
print(f"  ion-choice with NO equation option (clean ion groups): {len(clean_ion)}")
clean_ion_all4 = [x for x in clean_ion if len(x['options'])==4 and all(len(ion_tokens(v))>=2 for v in x['options'].values())]
print(f"    of which all 4 options are ion groups: {len(clean_ion_all4)}")

print("\n" + "="*80)
print("DETAIL: equation-choice questions (full options)")
print("="*80)
for x, eq_opts, ion_opts in eq_items:
    cid = x["id"]
    m = meta.get(cid,{})
    print(f"\n----- {cid}  answer={x.get('answer')}  ask={ask_dir(x.get('question'))}  pureLlmCorrect={pl(x)}  eqOpts={eq_opts} ionOpts={ion_opts}")
    q = (x.get("question") or "").replace("\n"," ")
    print(f"  Q: {q[:160]}")
    for k in sorted(x["options"].keys()):
        v = x["options"][k]
        tag = []
        if has_eq(v): tag.append("EQ")
        if len(ion_tokens(v))>=2: tag.append("ION")
        if not tag: tag.append("oth")
        print(f"    [{k}] ({','.join(tag)}): {v}")

print("\n" + "="*80)
print("DETAIL: ion-choice questions with NO equation option (clean)")
print("="*80)
for x in clean_ion:
    cid = x["id"]
    m = meta.get(cid,{})
    print(f"\n----- {cid}  answer={x.get('answer')}  ask={ask_dir(x.get('question'))}  pureLlmCorrect={pl(x)}")
    q = (x.get("question") or "").replace("\n"," ")
    print(f"  Q: {q[:160]}")
    for k in sorted(x["options"].keys()):
        v = x["options"][k]
        print(f"    [{k}] (ions={ion_tokens(v)}): {v}")
