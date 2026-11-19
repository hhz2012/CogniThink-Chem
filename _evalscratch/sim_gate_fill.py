# -*- coding: utf-8 -*-
"""验证门控：PDF 填空题 + reaction_check + 非具体结论 -> 回退 pureAnswer。
在 after3 与 after4 上分别计算净收益，确保非负且不影响手工题。"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
def load(p): return json.load(open(os.path.join(HERE,p),encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
def is_pdf(r): return (r.get("id") or "").startswith("pdf-")
def is_choice(q): return bool(re.search(r"(^|\n)\s*[A-D][\.．、]\s", q or ""))
def nonconcrete(c):
    c=c or ""
    return any(k in c for k in ["已匹配","相关反应","相关颜色","已查到","已解析出"]) or not any(k in c for k in ["能","不能","共存","不共存","可以","不可以","正确","错误"])

for name in ["after3","after4"]:
    d=load(name+".json"); cs=cases(d)
    # 候选：PDF + 非选择 + reaction_check + 非具体结论
    cand=[r for r in cs if is_pdf(r) and r.get("taskType")=="reaction_check" and not is_choice(r.get("question")) and nonconcrete(r.get("gatewayConclusion"))]
    hurt=[r for r in cand if r.get("pureLlmCorrect") and not r.get("gatewayCorrect")]
    lose=[r for r in cand if r.get("gatewayCorrect") and not r.get("pureLlmCorrect")]
    tie_both=[r for r in cand if r.get("gatewayCorrect") and r.get("pureLlmCorrect")]
    print(f"=== {name}: 候选(PDF填+reaction非具体) n={len(cand)} ===")
    print(f"  帮倒忙(门控可救) {len(hurt)}: {[r['id'] for r in hurt]}")
    print(f"  网关独有(门控会丢) {len(lose)}: {[r['id'] for r in lose]}")
    print(f"  都对(不变) {len(tie_both)}  都错(不变) {len(cand)-len(hurt)-len(lose)-len(tie_both)}")
    print(f"  网关闭环净收益(对纯LLM, PDF): {len(hurt)-len(lose):+d}")
    # 整体：门控后 gateway 是否 >= pure （仅看这部分影响）
    print()
# 确认手工 d9-1 不在候选(保护AC-7)
print("手工 d9-1 是否落入候选(PDF):", "d9-1" in [r['id'] for r in cases(load('after4.json')) if is_pdf(r)])
