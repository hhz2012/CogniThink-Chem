# -*- coding: utf-8 -*-
"""Task 42 inventory: enumerate broken choice questions in pdf_questions.json.
NOLETTER = type==choice but answer has no valid option letter(s).
OPTS0    = type==choice but options empty.
Dumps full text for classification. Authoritative source = pdf_questions.json.
"""
import json, re, sys
from collections import Counter

PDF = r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json"
data = json.load(open(PDF, encoding="utf-8"))

VALID = set("ABCD")
def answer_letters(ans):
    if not ans: return set()
    s = str(ans).strip()
    # strip separators
    chars = set(re.findall(r"[A-D]", s))
    return chars

def is_noletter(x):
    ans = x.get("answer")
    if x.get("type") != "choice": return False
    if not ans or not str(ans).strip(): return True
    return len(answer_letters(ans)) == 0

def is_opts0(x):
    if x.get("type") != "choice": return False
    opts = x.get("options")
    if not opts: return True
    if isinstance(opts, dict): return len(opts) == 0
    return True

broken = []
for x in data:
    nl = is_noletter(x); o0 = is_opts0(x)
    if nl or o0:
        broken.append((x, nl, o0))

nl_ids = sorted(x["id"] for x,n,o in broken if n)
o0_ids = sorted(x["id"] for x,n,o in broken if o)
both = sorted(set(nl_ids) & set(o0_ids))
print(f"total pdf_questions: {len(data)}")
print(f"choice: {sum(1 for x in data if x.get('type')=='choice')}")
print(f"NOLETTER: {len(nl_ids)}  OPTS0: {len(o0_ids)}  both: {len(both)}  union: {len(set(nl_ids)|set(o0_ids))}")
print(f"NOLETTER ids: {nl_ids}")
print(f"OPTS0 ids: {o0_ids}")

def short(s, n=160):
    s = re.sub(r"\s+", " ", s or "")
    return s[:n]

print("\n" + "="*80)
print("DETAIL (union, sorted)")
print("="*80)
for x, nl, o0 in sorted(broken, key=lambda t: t[0].get("id","")):
    tags = []
    if nl: tags.append("NOLETTER")
    if o0: tags.append("OPTS0")
    opts = x.get("options") or {}
    print(f"\n### {x['id']}  <{','.join(tags)}>  type={x.get('type')!r}  answer={x.get('answer')!r}  answerKeywords={x.get('answerKeywords')!r}  note={x.get('note')!r}")
    print(f"  Q: {short(x.get('question'), 400)}")
    if isinstance(opts, dict) and opts:
        for k in sorted(opts.keys()):
            print(f"    {k}: {short(opts[k], 160)}")
    else:
        print(f"    (options: {opts!r})")
