# -*- coding: utf-8 -*-
import json, re
PDF = r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
with open(PDF, encoding="utf-8") as f: items=json.load(f)
with open(r"D:\github\CogniThink-Chem\_evalscratch\after_stage3a.json", encoding="utf-8-sig") as f: ev=json.load(f)
meta={}
for p in ("bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"):
    for c in ev.get(p,[]): meta[c["id"]]=c

SEP=re.compile(r'={2,}|→|⇌|═|≡|⟶|⇒|[-—–]{1,}[^-—–>]*[-—–]{1,}>')
FORM=re.compile(r'[A-Za-z]')
def has_sep(t):
    if SEP.search(t or ""): return True
    if '=' in t:
        i=t.index('=')
        return bool(FORM.search(t[:i])) and bool(FORM.search(t[i+1:]))
    return False
def ask_dir(q):
    q=q or ""
    if any(w in q for w in ["不能大量共存","不能共存","无法大量共存"]): return "wrong"
    if any(w in q for w in ["能大量共存","可以大量共存","大量共存","能共存"]): return "correct"
    if any(w in q for w in ["错误的是","不正确","错误的有"]): return "wrong"
    if any(w in q for w in ["正确的是","正确的有"]): return "correct"
    if "错误" in q: return "wrong"
    if "正确" in q: return "correct"
    return "?"

eq_items=[x for x in items if x.get("type")=="choice" and x.get("options")
          and any(has_sep(v) for v in x["options"].values())]
print(f"equation-choice total: {len(eq_items)}")
byask={}
for x in eq_items:
    a=ask_dir(x.get("question"))
    byask.setdefault(a,[]).append(x)
for k,v in byask.items(): print(f"  ask={k}: {len(v)}")

def show(group, title):
    print("\n"+"#"*80)
    print(f"# {title}  (n={len(group)})")
    print("#"*80)
    for x in group:
        cid=x["id"]; ans=(x.get("answer") or "").strip()
        pl=meta.get(cid,{}).get("pureLlmCorrect")
        print(f"\n--- {cid}  answer={ans}  ask={ask_dir(x.get('question'))}  pureLlmCorrect={pl}")
        q=(x.get("question") or "").replace("\n"," ")
        print(f"  Q: {q[:140]}")
        for k in sorted(x["options"].keys()):
            v=x["options"][k]
            star=" <<<ANSWER" if k==ans else ""
            eqflag=" [EQ]" if has_sep(v) else ""
            print(f"    [{k}]{eqflag}{star}: {v}")

show([x for x in eq_items if ask_dir(x.get("question"))=="wrong"], "ask=WRONG equation-choice (need exactly 1 infeasible)")
show([x for x in eq_items if ask_dir(x.get("question"))=="correct"], "ask=CORRECT equation-choice (need 3 infeasible)")
show([x for x in eq_items if ask_dir(x.get("question"))=="?"], "ask=? equation-choice")
