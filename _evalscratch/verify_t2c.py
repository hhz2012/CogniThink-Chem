# -*- coding: utf-8 -*-
"""Mirror ClassificationValidator logic in Python to verify all t2c questions
solve correctly against data/highschool/substance-classification.json BEFORE
the slow C# build. Reports any null / mismatched answer.
"""
import json, re, unicodedata

ROOT = r"d:\github\CogniThink-Chem"

# ---------- Norm (mirror C# ClassificationValidator.Norm) ----------
def norm(s):
    if not s: return ""
    out = []
    for ch in s:
        if ch.isspace(): continue
        c = ch
        o = ord(c)
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
        out.append(c)
    return "".join(out)

# ---------- load fact library ----------
data = json.load(open(ROOT + r"\data\highschool\substance-classification.json", encoding="utf-8"))
subs = data["substances"]
by_formula = {}
by_name = {}
tags = set()
for s in subs:
    nf = norm(s.get("formula",""))
    if nf: by_formula.setdefault(nf, s)
    for nm in s.get("names", []):
        nn = norm(nm)
        if nn: by_name.setdefault(nn, s)
    for t in s.get("tags", []):
        tags.add(norm(t))
allotrope = {}
isotope = {}
for g, members in data.get("allotropeGroups", {}).items():
    for m in members: allotrope[norm(m)] = g
for g, members in data.get("isotopeGroups", {}).items():
    for m in members: isotope[norm(m)] = g

def find_sub(tok):
    if not tok: return None
    if tok in by_formula: return by_formula[tok]
    if tok in by_name: return by_name[tok]
    return None

def has_tag(sub, cat):
    return any(norm(t) == cat for t in sub.get("tags", []))

def split_members(rest):
    parts = re.split(r"[、，,/／；;和与跟]", rest)
    return [norm(p) for p in parts if norm(p)]

def ask_dir(stem):
    if re.search(r"错误|不正确|不属于|不互为", stem): return "wrong"
    if re.search(r"正确|分类正确|属于|均为|同素异形体|同位素|同分异构体", stem): return "correct"
    return "?"

def extract_stem_cat(stem):
    m = re.search(r"(?:不)?(?:属于|均为|为)\s*(?P<cat>[^的是。\s，]{1,10})", stem)
    if not m: return None
    cat = norm(m.group("cat"))
    return cat if cat in tags else None

def extract_stem_rel(stem):
    if "同素异形体" in stem: return "同素异形体"
    if "同位素" in stem: return "同位素"
    if "同分异构体" in stem: return "同分异构体"
    return None

def is_relation(cat): return cat in ("同素异形体","同位素","同分异构体")

def eval_tag_checks(members, cat):
    parts = []
    all_ok = True
    for tok in members:
        sub = find_sub(tok)
        if sub is None: return None, ""
        h = has_tag(sub, cat)
        if not h: all_ok = False
        parts.append(f"{sub.get('formula') or sub.get('names',['?'])[0]}:{'是' if h else '否'}{cat}")
    return all_ok, f"「{cat}」({';'.join(parts)})"

def eval_relation(relation, members):
    if relation == "同素异形体": group = allotrope
    elif relation == "同位素": group = isotope
    else: return None, ""
    keys = []
    for tok in members:
        known = find_sub(tok) is not None or tok in allotrope or tok in isotope
        if not known: return None, ""
        keys.append(group.get(tok, ""))
    all_ok = len(keys) > 0 and all(k for k in keys) and len(set(keys)) == 1
    return all_ok, f"「{relation}」({'、'.join(members)})"

def eval_option(text, stem_cat, stem_rel):
    t = text.strip()
    if not t: return None
    # P2: cat：members
    m2 = re.match(r"^(?P<cat>[^：:]{1,10})[：:]\s*(?P<rest>.+)$", t)
    if m2:
        cat = norm(m2.group("cat"))
        members = split_members(m2.group("rest"))
        if not members: return None
        if is_relation(cat): return eval_relation(cat, members)
        if cat not in tags: return None
        return eval_tag_checks(members, cat)
    # P3: members 是/为 cat
    m3 = re.match(r"^(?P<rest>.+?)[是为]\s*(?P<cat>[^是为。；，]+?)\s*$", t)
    if m3:
        cat = norm(m3.group("cat"))
        members = split_members(m3.group("rest"))
        if not members: return None
        if is_relation(cat): return eval_relation(cat, members)
        if cat not in tags: return None
        return eval_tag_checks(members, cat)
    # P4: bare members, cat from stem
    if stem_cat is not None:
        members = split_members(t)
        if members:
            return eval_tag_checks(members, stem_cat)
    # P5: relation from stem
    if stem_rel is not None:
        members = split_members(t)
        if members:
            return eval_relation(stem_rel, members)
    return None

def decide(raw):
    # crude option split: lines starting with A./B./C./D.
    lines = raw.split("\n")
    stem_lines, opts = [], {}
    for ln in lines:
        m = re.match(r"^\s*([A-D])\s*[.．、]\s*(.+)$", ln)
        if m: opts[m.group(1)] = m.group(2).strip()
        else: stem_lines.append(ln)
    stem = "".join(stem_lines)
    if len(opts) < 2: return None, "opts<2"
    ad = ask_dir(stem)
    if ad not in ("correct","wrong"): return None, f"askdir={ad}"
    stem_cat = extract_stem_cat(stem)
    stem_rel = extract_stem_rel(stem)
    letters = sorted(opts.keys())
    correct = {}
    for L in letters:
        r = eval_option(opts[L], stem_cat, stem_rel)
        if r is None: return None, f"opt {L} unparseable"
        ok, _ = r
        correct[L] = ok
    cands = [l for l in letters if correct[l]] if ad=="correct" else [l for l in letters if not correct[l]]
    if len(cands) != 1: return None, f"not unique: {cands}"
    return cands[0], "ok"

# ---------- load tier2 questions & render like GenTier2Questions ----------
qd = json.load(open(ROOT + r"\src\CogniThink.TutorBrain.Api\data\tier2_questions.json", encoding="utf-8"))
items = qd["items"] if isinstance(qd, dict) and "items" in qd else qd
fails = 0
for x in items:
    if not x["id"].startswith("t2c-"): continue
    q = x["question"]
    if x.get("type") == "choice" and x.get("options"):
        rendered = "\n".join(f"{k}. {x['options'][k]}" for k in sorted(x["options"].keys()))
        q = q + "\n" + rendered
    ans = x.get("answer","")
    letter, info = decide(q)
    exp = re.search(r"[A-D]", ans)
    exp_l = exp.group(0) if exp else "?"
    status = "OK" if letter == exp_l else ("NULL" if letter is None else "MISMATCH")
    if status != "OK":
        fails += 1
        print(f"[{status}] {x['id']} expected={exp_l} got={letter} ({info})")
        print(f"   stem: {x['question']}")
    else:
        print(f"[OK]   {x['id']} -> {letter}")
print(f"\nTotal fails: {fails}")
