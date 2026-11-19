# -*- coding: utf-8 -*-
"""探测 both-wrong 中因归一化/分词导致的关键词漏判（非选择题）。
独立复刻一个更鲁棒的归一化，找出 answer 实际命中 expected 关键词但被判错的题。"""
import json, os, re, unicodedata
HERE = os.path.dirname(os.path.abspath(__file__))
d = json.load(open(os.path.join(HERE,"after3.json"),encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(d.get(p,[]) or [])

SUB={'₀':'0','₁':'1','₂':'2','₃':'3','₄':'4','₅':'5','₆':'6','₇':'7','₈':'8','₉':'9',
     '⁰':'0','¹':'1','²':'2','³':'3','⁴':'4','⁵':'5','⁶':'6','⁷':'7','⁸':'8','⁹':'9',
     '⁺':'+','⁻':'-','－':'-','−':'-','﹣':'-','～':'~','〜':'~',
     '↑':'↑','↓':'↓','⇌':'⇌','→':'→','←':'←','＝':'=','≡':'=',
     '．':'.','。':'.','（':'(','）':')','【':'[','】':']','“':'"','”':'"','‘':"'",'’':"'"}
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
    t=''.join(out)
    # 统一箭头: 各种 → 统一为 ">" 方向标记不便, 保留 ↑↓ 原字
    return t.lower()

def keywords_of(expected):
    e=expected or ""
    if e.startswith("关键词:"):
        e=e[len("关键词:"):]
    # 去前缀方向 "肯定/能" 等不含关键词时返回空
    parts=re.split(r"[,，;；、]", e)
    kws=[p.strip() for p in parts if p.strip()]
    return kws

def is_choice_q(q):
    return bool(re.search(r"(^|\n)\s*[A-D][\.．、]\s", q or ""))

bw=[r for r in rows if not r.get("pureLlmCorrect") and not r.get("gatewayCorrect")]
print(f"both-wrong 总 {len(bw)}")
leak=[]
for r in bw:
    kws=keywords_of(r.get("expected"))
    # 只看有实质关键词(>=2字符)且不是单字母的(单字母已查过)
    long_kws=[k for k in kws if len(norm(k))>=2]
    if not long_kws: continue
    pa=norm(r.get("pureLlmAnswer") or "")
    ga=norm(r.get("gatewayAnswer") or "")
    hit_p=[k for k in long_kws if norm(k) in pa]
    hit_g=[k for k in long_kws if norm(k) in ga]
    if hit_p or hit_g:
        leak.append((r,hit_p,hit_g))
print(f"潜在关键词漏判(both-wrong 中 answer 含某长关键词): {len(leak)}")
for r,hp,hg in leak[:40]:
    print(f"\n[{r.get('id')}] task={r.get('taskType')} 期望={(r.get('expected') or '')[:50]}")
    print(f"   纯命中: {hp[:3]}  网关命中: {hg[:3]}")
    print(f"   Q: {(r.get('question') or '')[:60]}")
    print(f"   纯答: {(r.get('pureLlmAnswer') or '')[:80].replace(chr(10),' ')}")
