# -*- coding: utf-8 -*-
"""模拟两个候选杠杆在 after3 上的净收益。
A. reaction_check 证据路径(非具体结论) + 选择题 -> 回退 pureAnswer
B. both-wrong 选择题中，LLM 其实答出了正确字母但被判错(漏判)
"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
d = json.load(open(os.path.join(HERE,"after3.json"),encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(d.get(p,[]) or [])

def is_pdf(r): return (r.get("id") or "").startswith("pdf-")
def is_choice_q(q):
    q=q or ""; return bool(re.search(r"(^|\n)\s*[A-D][\.．、]\s", q))
def exp_letter(r):
    """从 expected 解析选择题答案字母(若首关键词为单字母 A-D)"""
    e=r.get("expected") or ""
    if e.startswith("关键词:"):
        rest=e[len("关键词:"):].strip()
        first=rest.split(",")[0].strip()
        if len(first)==1 and first in "ABCD": return first.lower()
    return None
def contains_standalone(text, letter):
    t=(text or "").lower()
    return bool(re.search(rf"(?<![a-z]){letter}(?![a-z])", t))
def is_concrete(c):
    c=c or ""
    if any(k in c for k in ["已匹配","相关反应","相关颜色","已查到","已解析出"]): return False
    return any(k in c for k in ["能","不能","共存","不共存","可以","不可以","正确","错误"])

# ===== A. evidence-path + choice 模拟门控 =====
rc=[r for r in rows if r.get("taskType")=="reaction_check"]
evid=[r for r in rc if not is_concrete(r.get("gatewayConclusion"))]
evid_choice=[r for r in evid if is_choice_q(r.get("question"))]
evid_nonchoice=[r for r in evid if not is_choice_q(r.get("question"))]
def cnt(rows,k): return sum(1 for r in rows if r.get(k))
print("=== A. reaction_check 证据路径 门控模拟 ===")
print(f"evidence 总 {len(evid)}: 网关对{cnt(evid,'gatewayCorrect')} 纯对{cnt(evid,'pureLlmCorrect')}")
print(f"  其中选择题 {len(evid_choice)}: 网关对{cnt(evid_choice,'gatewayCorrect')} 纯对{cnt(evid_choice,'pureLlmCorrect')} -> 门控净收益 {cnt(evid_choice,'pureLlmCorrect')-cnt(evid_choice,'gatewayCorrect'):+d}")
print(f"  其中非选择 {len(evid_nonchoice)}: 网关对{cnt(evid_nonchoice,'gatewayCorrect')} 纯对{cnt(evid_nonchoice,'pureLlmCorrect')}")
# 全部 evidence 门控(对照)
print(f"  全部 evidence 门控净收益: {cnt(evid,'pureLlmCorrect')-cnt(evid,'gatewayCorrect'):+d}")
print()

# evidence+choice 中：网关错但纯对 的题(门控可救) vs 网关对但纯错(门控会丢)
save=[r for r in evid_choice if not r.get("gatewayCorrect") and r.get("pureLlmCorrect")]
lose=[r for r in evid_choice if r.get("gatewayCorrect") and not r.get("pureLlmCorrect")]
print(f"  门控可救(网错纯对): {len(save)} {[r.get('id') for r in save]}")
print(f"  门控会丢(网对纯错): {len(lose)} {[r.get('id') for r in lose]}")
print()

# ===== B. 漏判探测：both-wrong 选择题，LLM 答出字母但判错 =====
bw=[r for r in rows if not r.get("pureLlmCorrect") and not r.get("gatewayCorrect")]
bw_choice=[r for r in bw if is_choice_q(r.get("question"))]
print(f"=== B. both-wrong 选择题 {len(bw_choice)} 漏判探测 ===")
leak_pure=[]; leak_gw=[]
for r in bw_choice:
    L=exp_letter(r)
    if not L: continue
    pa=r.get("pureLlmAnswer") or ""; ga=r.get("gatewayAnswer") or ""
    if contains_standalone(pa, L): leak_pure.append(r)
    if contains_standalone(ga, L): leak_gw.append(r)
print(f"  有明确单字母期望的 both-wrong 选择题: {sum(1 for r in bw_choice if exp_letter(r))}")
print(f"  纯LLM答出正确字母但判错(漏判): {len(leak_pure)}  {[r.get('id') for r in leak_pure][:20]}")
print(f"  网关答出正确字母但判错(漏判): {len(leak_gw)}  {[r.get('id') for r in leak_gw][:20]}")
print()
# 展示几个漏判样例(纯LLM答案前60字)
print("  -- 纯LLM漏判样例 --")
for r in leak_pure[:8]:
    print(f"   [{r.get('id')}] 期望={r.get('expected')[:20]} | 纯答={(r.get('pureLlmAnswer') or '')[:55]}")
