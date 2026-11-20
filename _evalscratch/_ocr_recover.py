# -*- coding: utf-8 -*-
"""Task 42 recovery probe.
For each of the 39 broken pdf_questions (NOLETTER/OPTS0), recover:
  - the exercise entry (src_page,num) -> correct question + options
  - candidate correct answer entries by content match (question vs explanation)
Prints a report so an author can verify and apply only high-confidence fixes.
"""
import json, glob, re, os
from collections import defaultdict

PDF = r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json"
PARSED = r"pdfs/questions_parsed.json"
EX_DIR = r"pdfs/_extract/exercise"
AN_DIR = r"pdfs/_extract/answer"

data = json.load(open(PDF, encoding="utf-8"))
by_id = {x["id"]: x for x in data}

# parsed keeps src_page/ans_page/num
parsed = json.load(open(PARSED, encoding="utf-8"))
pmeta = {x["id"]: x for x in parsed}  # may have been renumbered; id is stable after dedup

def load_items(d, key):
    out = []
    for f in sorted(glob.glob(os.path.join(d, "page_*.json"))):
        j = json.load(open(f, encoding="utf-8"))
        pi = j.get("page_idx")
        for it in j.get(key, []) or []:
            if isinstance(it, dict):
                it2 = dict(it); it2["_page"] = pi; out.append(it2)
    return out

ex_items = load_items(EX_DIR, "questions")
an_items = load_items(AN_DIR, "answers")
ex_by_pn = defaultdict(list)
for e in ex_items:
    ex_by_pn[(e["_page"], str(e.get("num")))].append(e)
an_by_pn = defaultdict(list)
for a in an_items:
    an_by_pn[(a["_page"], str(a.get("num")))].append(a)
# also global answer text for content search
an_global = an_items

VALID = set("ABCD")
def broken_ids():
    ids = []
    for x in data:
        if x.get("type") != "choice": continue
        ans = str(x.get("answer") or "").strip()
        opts = x.get("options") or {}
        noletter = (not ans) or (len(set(re.findall(r"[A-D]", ans))) == 0)
        opts0 = (not opts) or (len(opts) == 0)
        if noletter or opts0:
            ids.append(x["id"])
    return sorted(ids)

def norm(s): return re.sub(r"\s+", "", s or "")

def content_match_score(qtext, atext):
    # overlap of distinctive Chinese words / chemical tokens
    q = norm(qtext); a = norm(atext)
    if not q or not a: return 0.0
    # char 4-gram overlap
    qg = set(q[i:i+4] for i in range(max(0,len(q)-3)))
    ag = set(a[i:i+4] for i in range(max(0,len(a)-3)))
    if not qg: return 0.0
    return len(qg & ag) / max(1, len(qg))

def find_answer_candidates(qtext, topn=4):
    scored = []
    for a in an_global:
        expl = a.get("explanation") or ""
        ans = str(a.get("answer") or "")
        blob = expl + " " + ans
        s = content_match_score(qtext, blob)
        if s > 0.04:
            scored.append((s, a))
    scored.sort(key=lambda t: -t[0])
    return scored[:topn]

print("RECOVERY REPORT for 39 broken questions")
print("="*100)
for qid in broken_ids():
    x = by_id[qid]
    m = pmeta.get(qid, {})
    sp, num = m.get("src_page"), m.get("num")
    ap = m.get("ans_page")
    cur_ans = str(x.get("answer") or "")
    cur_opts = x.get("options") or {}
    print(f"\n##### {qid}  src_page={sp} num={num}  cur_type={x.get('type')!r}  cur_answer={cur_ans!r}  opts_keys={list(cur_opts.keys())}")
    # exercise entry
    eeshits = ex_by_pn.get((sp, str(num)), [])
    if eeshits:
        e = eeshits[0]
        eq = e.get("question") or ""
        eo = e.get("options") or {}
        same_q = (norm(eq)[:60] == norm(x.get("question"))[:60])
        print(f"  EXERCISE(page {sp},num {num}): same_stem={same_q}")
        print(f"    Q: {eq[:240]}")
        if eo:
            for k in sorted(eo.keys()): print(f"    opt {k}: {eo[k][:140]}")
        else:
            print(f"    opt: (EMPTY in exercise too)")
    else:
        print(f"  EXERCISE: NOT FOUND by (page={sp},num={num})")
    # current (misaligned) answer
    if ap is not None and ap >= 0:
        ahits = an_by_pn.get((ap, str(num)), [])
        if ahits:
            a = ahits[0]
            print(f"  CUR_ANSWER(page {ap},num {num}): ans={str(a.get('answer'))!r}  expl={str(a.get('explanation'))[:160]!r}")
    # candidate correct answers by content
    cands = find_answer_candidates(x.get("question") or "")
    print(f"  CANDIDATE answers by content overlap:")
    for s, a in cands:
        print(f"    [score={s:.3f}] page={a['_page']} num={a.get('num')} ans={str(a.get('answer'))!r}  expl={str(a.get('explanation'))[:170]!r}")
