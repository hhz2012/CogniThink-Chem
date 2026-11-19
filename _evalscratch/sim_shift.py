# -*- coding: utf-8 -*-
"""离线模拟 TryJudgeIonizationShift 触发（当前 vs 放宽），预测新增介入数。"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
data = json.load(open(os.path.join(HERE, "after.json"), encoding="utf-8-sig"))
parts = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]
cases = []
for p in parts: cases.extend(data.get(p, []) or [])

def has_options(q): return bool(re.search(r'(^|\n)\s*[A-D][\.．、]\s', q or ""))

def match_current(raw):
    isIon = any(w in raw for w in ["电离平衡","电离","CH3COOH","醋酸","NH3·H2O","氨水","水解平衡","水解"])
    if not isIon: return None
    if not any(w in raw for w in ["移动","方向","程度","促进","抑制"]): return None
    if raw.count("加水")>0 or "稀释" in raw: return "正向/增大"
    if any(w in raw for w in ["加","加入","通入"]) and any(w in raw for w in ["HCl","盐酸","H+","硫酸"]): return "逆向/减小"
    if any(w in raw for w in ["加","加入"]) and any(w in raw for w in ["NaOH","OH-","氢氧化钠"]): return "正向/增大"
    if any(w in raw for w in ["升温","加热","升高温度"]): return "正向/增大"
    if "加" in raw and any(w in raw for w in ["盐","同离子"]): return "逆向/减小"
    return None

def match_broad(raw):
    isIon = any(w in raw for w in ["电离平衡","电离","CH3COOH","醋酸","NH3·H2O","NH₃·H₂O","氨水","水解平衡","水解","弱电解质"])
    if not isIon: return None
    if not any(w in raw for w in ["移动","方向","程度","促进","抑制","电离程度","水解程度"]): return None
    if any(w in raw for w in ["加水","稀释","加蒸馏水","冲稀"]): return "正向/增大"
    add = any(w in raw for w in ["加","加入","通入","滴加","加入少量","滴入","注入"])
    if add and any(w in raw for w in ["HCl","盐酸","H+","H⁺","硫酸","稀盐酸","稀硫酸","浓盐酸"]): return "逆向/减小"
    if add and any(w in raw for w in ["NaOH","OH-","OH⁻","氢氧化钠","KOH","氢氧化钾","石灰水","澄清石灰水","强碱"]): return "正向/增大"
    if any(w in raw for w in ["升温","加热","升高温度","温度升高","煮沸"]): return "正向/增大"
    if add and (any(w in raw for w in ["盐","同离子"]) or
                any(w in raw for w in ["醋酸钠","乙酸钠","氯化铵","硫化钠","亚硫酸钠","碳酸钠","醋酸铵","氟化钠","氯化钠"]) or
                re.search(r'加入\s*[A-Za-z0-9\u4e00-\u9fa5]{1,6}(?:钠|铵|钾|盐)', raw)):
        return "逆向/减小"
    return None

eq = [r for r in cases if r.get("taskType")=="equilibrium"]
cur_new = []; broad_new = []
for r in eq:
    q = r.get("question") or ""
    c = match_current(q); b = match_broad(q)
    if c and not r.get("gatewayUsedEngine"): cur_new.append(r)  # shouldn't happen
    if b and not r.get("gatewayUsedEngine"): broad_new.append((r, b))
print(f"equilibrium 总数={len(eq)}, 当前已介入={sum(1 for r in eq if r.get('gatewayUsedEngine'))}")
print(f"放宽后【新增匹配】(当前未介入) 的题数: {len(broad_new)}")
print()
for r, verdict in broad_new:
    ch = has_options(r.get("question"))
    print(f"  [{r.get('id')}] choice={ch} 纯对={r.get('pureLlmCorrect')} 网对={r.get('gatewayCorrect')} -> {verdict}")
    print(f"      期望={(r.get('expected') or '')[:40]}")
    qone = (r.get("question") or "").replace("\n"," ")[:90]
    print(f"      Q: {qone}")
