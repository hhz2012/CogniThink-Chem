# -*- coding: utf-8 -*-
"""量化 split 后 1-char 关键词导致的潜在误匹配(假阳性)。"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
SUB={'₀':'0','₁':'1','₂':'2','₃':'3','₄':'4','₅':'5','₆':'6','₇':'7','₈':'8','₉':'9',
     '⁰':'0','¹':'1','²':'2','³':'3','⁴':'4','⁵':'5','⁶':'6','⁷':'7','⁸':'8','⁹':'9',
     '⁺':'+','⁻':'-','－':'-','−':'-','﹣':'-','～':'~','〜':'~'}
def norm(s):
    if not s: return ""
    out=[]
    for ch in s:
        if ch in SUB: out.append(SUB[ch])
        else:
            o=ord(ch)
            if 0xFF01<=o<=0xFF5E: out.append(chr(o-0xFEE0))
            elif o==0x3000: out.append(' ')
            else: out.append(ch)
    return ''.join(out)
def split_keywords(answer):
    if not answer: return []
    s=re.sub(r"[（(]\s*\d+\s*[）)]\s*"," ", answer)
    parts=re.split(r"[,，;；、]", s)
    return [p.strip().strip(".").strip() for p in parts if p.strip().strip(".")]
def standalone(text, letter):
    return bool(re.search(rf"(?<![a-z]){letter.lower()}(?![a-z])", (text or "").lower()))
def score(answer, typ, ans, drop1char=False):
    if not answer: return False
    lower=norm(answer).lower()
    t=(ans or "").strip()
    if typ=="choice" and len(t)==1 and t in "ABCD":
        if standalone(lower,t): return True
    for k in split_keywords(ans):
        kn=norm(k).lower()
        if drop1char and len(kn)==1 and kn.isalpha(): continue
        if kn in lower: return True
    return False

pdf=json.load(open(r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8"))
pdfmap={x["id"]:x for x in pdf}
after=json.load(open(os.path.join(HERE,"after3.json"),encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(after.get(p,[]) or [])
pdfrows=[r for r in rows if (r.get("id") or "").startswith("pdf-")]

for label,key in [("纯LLM","pureLlmAnswer"),("网关","gatewayAnswer")]:
    base_v1=[]; base_v2=[]
    for r in pdfrows:
        x=pdfmap.get(r["id"]); 
        if not x: continue
        old=r.get(key.replace("Answer","Correct"))
        v1=score(r.get(key) or "", x.get("type") or "", x.get("answer") or "", drop1char=False)
        v2=score(r.get(key) or "", x.get("type") or "", x.get("answer") or "", drop1char=True)
        if not old and v1: base_v1.append(r["id"])
        if not old and v2: base_v2.append(r["id"])
    only_v1=set(base_v1)-set(base_v2)
    print(f"{label}: V1翻对 {len(base_v1)}  V2(去1字字母)翻对 {len(base_v2)}  仅V1(依赖1字字母) {len(only_v1)}: {sorted(only_v1)[:20]}")
