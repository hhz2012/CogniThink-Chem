# -*- coding: utf-8 -*-
import json, re
PDF = r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
with open(PDF, encoding="utf-8") as f:
    items = json.load(f)

# load stage3a to know which are choice-classified by gateway + pureLlm correctness
with open(r"D:\github\CogniThink-Chem\_evalscratch\after_stage3a.json", encoding="utf-8-sig") as f:
    ev = json.load(f)
meta = {}
for p in ("bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"):
    for c in ev.get(p, []):
        meta[c["id"]] = (c.get("taskType"), c.get("pureLlmCorrect"), c.get("gatewayCorrect"))

EQUATION_RE = re.compile(r'[=→⇌]|[-—–]{1,}[^-—–>]*[-—–]{1,}>')
def has_eq(t):
    return bool(EQUATION_RE.search(t or ""))

eq_items = [x for x in items if x.get("type")=="choice" and x.get("options") and any(has_eq(v) for v in x["options"].values())]
print("choice items with >=1 eq option:", len(eq_items))
shown = 0
for x in eq_items:
    cid = x["id"]
    tt, pl, gw = meta.get(cid, ("?", None, None))
    if tt != "choice":
        continue
    print("\n=====", cid, "| answer=", x.get("answer"), "| pureLlmCorrect=", pl, "| gwCorrect=", gw)
    print("Q:", (x.get("question") or "")[:120])
    for k in sorted(x["options"].keys()):
        v = x["options"][k]
        flag = "EQ" if has_eq(v) else "  "
        print(f"   [{flag}] {k}. {v[:130]}")
    shown += 1
    if shown >= 22:
        break
print("\n(shown", shown, "of", len(eq_items), ")")
