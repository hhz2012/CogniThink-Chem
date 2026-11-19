# -*- coding: utf-8 -*-
"""模拟：将 PDF 答案按分隔符拆分为多个关键词后重新判分，量化 wrong->right 翻转数。
复刻 C# IsAnswerCorrect（PDF 路径）+ NormalizeChemicalNotation。"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))

# ---- 复刻 C# NormalizeChemicalNotation ----
SUB={'₀':'0','₁':'1','₂':'2','₃':'3','₄':'4','₅':'5','₆':'6','₇':'7','₈':'8','₉':'9',
     '⁰':'0','¹':'1','²':'2','³':'3','⁴':'4','⁵':'5','⁶':'6','⁷':'7','⁸':'8','⁹':'9',
     '⁺':'+','⁻':'-','－':'-','−':'-','﹣':'-','～':'~','〜':'~','·':'·'}
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
    """按 , ， ; ； 、 以及空白+序号 拆分答案为关键词（去空、去首尾空白）。"""
    if not answer: return []
    # 先去掉 (1) (2) ① ② 等序号前缀（保留内容）
    s=re.sub(r"[（(]\s*\d+\s*[）)]\s*"," ", answer)
    parts=re.split(r"[,，;；、]", s)
    kws=[]
    for p in parts:
        p=p.strip().strip(".").strip()
        if p: kws.append(p)
    return kws

def standalone_letter(text, letter):
    t=(text or "").lower()
    return bool(re.search(rf"(?<![a-z]){letter}(?![a-z])", t))

def is_choice_letter(ans):
    t=(ans or "").strip()
    return t if (len(t)==1 and t in "ABCD") else None

def score(answer, type_, kw_answer):
    if not answer: return False
    lower=norm(answer).lower()
    L=is_choice_letter(kw_answer)
    if type_=="choice" and L:
        if standalone_letter(lower, L.lower()): return True
    kws=split_keywords(kw_answer)
    for k in kws:
        if norm(k).lower() in lower:
            return True
    return False

# ---- 加载数据 ----
pdf=json.load(open(r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8"))
pdfmap={x["id"]:x for x in pdf}
after=json.load(open(os.path.join(HERE,"after3.json"),encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(after.get(p,[]) or [])

def is_pdf(r): return (r.get("id") or "").startswith("pdf-")
pdfrows=[r for r in rows if is_pdf(r)]

flip_p=[]; flip_g=[]; same_right=0; same_wrong=0
for r in pdfrows:
    x=pdfmap.get(r["id"])
    if not x: continue
    ans=x.get("answer") or ""; typ=x.get("type") or ""
    new_p=score(r.get("pureLlmAnswer") or "", typ, ans)
    new_g=score(r.get("gatewayAnswer") or "", typ, ans)
    old_p=r.get("pureLlmCorrect"); old_g=r.get("gatewayCorrect")
    if not old_p and new_p: flip_p.append(r["id"])
    if not old_g and new_g: flip_g.append(r["id"])
    if old_p and new_p: same_right+=1
    if not old_p and not new_p: same_wrong+=1

print(f"PDF 题数 {len(pdfrows)}")
print(f"纯LLM: 原对 {sum(1 for r in pdfrows if r.get('pureLlmCorrect'))} -> 模拟对 {sum(1 for r in pdfrows if r.get('pureLlmCorrect'))+len(flip_p)}  (翻对 {len(flip_p)})")
print(f"网关:  原对 {sum(1 for r in pdfrows if r.get('gatewayCorrect'))} -> 模拟对 {sum(1 for r in pdfrows if r.get('gatewayCorrect'))+len(flip_g)}  (翻对 {len(flip_g)})")
# 确认无 right->wrong (monotonic)
rw_p=sum(1 for r in pdfrows if r.get('pureLlmCorrect') and not score(r.get('pureLlmAnswer') or '', pdfmap[r['id']].get('type') or '', pdfmap[r['id']].get('answer') or ''))
rw_g=sum(1 for r in pdfrows if r.get('gatewayCorrect') and not score(r.get('gatewayAnswer') or '', pdfmap[r['id']].get('type') or '', pdfmap[r['id']].get('answer') or ''))
print(f"单调检查: 纯 对->错 {rw_p}  网 对->错 {rw_g}  (应为0)")
print()
print(f"翻对样例(纯LLM, 前30): {flip_p[:30]}")
print(f"翻对样例(网关, 前30): {flip_g[:30]}")
