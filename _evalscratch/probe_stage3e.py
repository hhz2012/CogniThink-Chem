# -*- coding: utf-8 -*-
import json, urllib.request, sys, time
io = sys.stdout.reconfigure(encoding='utf-8') if hasattr(sys.stdout,'reconfigure') else None
URL = "http://localhost:5080/api/gateway/process"
DATA = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"

SUBS = {'₀':'0','₁':'1','₂':'2','₃':'3','₄':'4','₅':'5','₆':'6','₇':'7','₈':'8','₉':'9'}
SUPS = {'⁰':'0','¹':'1','²':'2','³':'3','⁴':'4','⁵':'5','⁶':'6','⁷':'7','⁸':'8','⁹':'9','⁺':'+','⁻':'-'}
def norm(s):
    out=[]
    for ch in s:
        if ch in SUBS: out.append(SUBS[ch])
        elif ch in SUPS: out.append(SUPS[ch])
        elif ch in '－−﹣': out.append('-')
        elif 0xFF01<=ord(ch)<=0xFF5E: out.append(chr(ord(ch)-0xFEE0))
        else: out.append(ch)
    return ''.join(out).lower()

def process(text):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(URL, data=body, headers={"Content-Type":"application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=180) as r:
        return json.loads(r.read().decode("utf-8"))

data = json.load(open(DATA, encoding="utf-8"))
qm = { (c.get("id") or ""): c for c in data }
def qtext(cid):
    return qm[cid].get("question") or ""

cases = [
    ("d9-1", "用酸性高锰酸钾溶液除去甲烷中的乙烯，对吗？",
     lambda fr: ("不对" in fr) and ("co2" in norm(fr)) and ("溴水" in fr)),
    ("pdf-539", qtext("pdf-539"),
     lambda fr: ("gan + oh- + 3h2o → [ga(oh)4]- + nh3" in norm(fr))),
    ("pdf-456", qtext("pdf-456"),
     lambda fr: ("化学方程式" in fr) and ("na2so3" in norm(fr)) and ("so2" in norm(fr)) and ("h2so4" in norm(fr))),
    ("pdf-467", qtext("pdf-467"),
     lambda fr: fr.strip() == "×" or ("×" in fr and "√" not in fr)),
]

PLACEHOLDERS = {"已匹配到相关反应", "已解析出相关物质与离子"}
allpass = True
for cid, text, check in cases:
    t0=time.time()
    try:
        d = process(text)
    except Exception as e:
        print(f"[FAIL] {cid}: request ERR {e}"); allpass=False; continue
    er = d.get("engine_response") or {}
    eq = d.get("engine_request") or {}
    fr = er.get("final_result") or ""
    tt = eq.get("task_type")
    is_ph = fr.strip() in PLACEHOLDERS
    ok = (tt == "reaction_check") and (not is_ph) and (check(fr) if callable(check) else True)
    if not ok: allpass = False
    print("="*70)
    print(f"[{'PASS' if ok else 'FAIL'}] {cid}  task_type={tt}  ok={er.get('ok')}  ({time.time()-t0:.1f}s)")
    print(f"  final_result={fr!r}")
    if not ok:
        print(f"  (placeholder={is_ph})")

print("="*70)
print("ALL_PASS=" + str(allpass))
