# -*- coding: utf-8 -*-
import json, re
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
with open(SCRATCH + r"\after_stage3a.json", encoding="utf-8-sig") as f:
    data = json.load(f)
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
cases = []
for p in parts:
    for c in data.get(p, []):
        c["_part"] = p
        cases.append(c)

choices = [c for c in cases if c.get("taskType") == "choice"]
print("total choice-classified:", len(choices))
print("keys of a choice case:", sorted(choices[0].keys()))

OPT_RE = re.compile(r'(?:^|\n)\s*([A-D])[\.．、]\s*(.*?)(?=\n\s*[A-D][\.．、]|$)', re.S)
# equation-ish: contains a reaction separator and a chemical formula
EQUATION_RE = re.compile(r'[=→⇌]|[-—–]{1,}[^-—–>]*[-—–]{1,}>')
FORMULA_RE = re.compile(r'[A-Z][a-z]?\d*')
ION_RE = re.compile(r'[A-Z][a-z]?\d*[0-9]*[+\-]')

def parse_options(q):
    opts = {}
    for m in OPT_RE.finditer(q or ""):
        opts[m.group(1)] = m.group(2).strip()
    return opts

def has_equation(text):
    if not EQUATION_RE.search(text): return False
    # both sides of first separator need a formula
    for sep in ['→','⇌','=']:
        if sep in text:
            i = text.index(sep)
            return bool(FORMULA_RE.search(text[:i])) and bool(FORMULA_RE.search(text[i+1:]))
    return False

def ion_count(text):
    return len(ION_RE.findall(text))

eq_choice = []      # choice with >=1 equation option
ion_choice = []     # choice with >=1 ion-group option (>=2 ions)
multi_eq = 0
for c in choices:
    opts = parse_options(c.get("question"))
    c["_opts"] = opts
    eq_opts = [k for k,v in opts.items() if has_equation(v)]
    ion_opts = [k for k,v in opts.items() if ion_count(v) >= 2]
    if eq_opts:
        eq_choice.append((c["id"], len(eq_opts), eq_opts, c.get("_part"), c.get("pureLlmCorrect"), c.get("gatewayCorrect")))
    if ion_opts:
        ion_choice.append((c["id"], len(ion_opts), ion_opts, c.get("_part")))
    if len(eq_opts) >= 2:
        multi_eq += 1

print("\n=== choice with >=1 equation option ===", len(eq_choice))
for row in eq_choice[:60]:
    print(f"  {row[0]:10} eqOpts={row[1]} {row[2]} part={row[3]} pureCorrect={row[4]} gwCorrect={row[5]}")
print("  ...(showing first 60); total =", len(eq_choice), " with >=2 eq options:", multi_eq)

print("\n=== choice with >=1 ion-group option (>=2 ions) ===", len(ion_choice))
for row in ion_choice[:30]:
    print(f"  {row[0]:10} ionOpts={row[1]} {row[2]} part={row[3]}")

# Ask-direction: 选正确 vs 选错误
ask_correct = sum(1 for c in choices if any(w in (c.get("question") or "") for w in ["正确的是","正确的有","正确"]))
ask_wrong = sum(1 for c in choices if any(w in (c.get("question") or "") for w in ["错误的是","不正确","错误的有","错误"]))
print(f"\n问法含'正确': {ask_correct}; 含'错误/不正确': {ask_wrong}; total choice {len(choices)}")

# of the equation-choice candidates, how many does pure LLM already get right? (potential gain if engine helps)
eq_pure_right = sum(1 for r in eq_choice if r[4])
eq_gw_right = sum(1 for r in eq_choice if r[5])
print(f"\nequation-choice candidates: pureLlmCorrect={eq_pure_right}/{len(eq_choice)}, gatewayCorrect(stage3a)={eq_gw_right}/{len(eq_choice)}")
