# -*- coding: utf-8 -*-
"""模拟 c=1000ρω/M 计算规则 与 Ksp 放宽，统计候选新增介入题。"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
data = json.load(open(os.path.join(HERE, "after.json"), encoding="utf-8-sig"))
parts = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]
cases = []
for p in parts: cases.extend(data.get(p, []) or [])

def has_options(q): return bool(re.search(r'(^|\n)\s*[A-D][\.．、]\s', q or ""))

calc = [r for r in cases if r.get("taskType")=="calculation"]
eq = [r for r in cases if r.get("taskType")=="equilibrium"]

print("===== calc: c=1000ρω/M 候选（含 质量分数+密度+物质的量浓度，且未介入）=====")
n=0
for r in calc:
    q = r.get("question") or ""
    if ("质量分数" in q or "w=" in q or "ω=" in q or "溶质质量分数" in q) and ("密度" in q or "ρ=" in q) and ("物质的量浓度" in q or "量浓度" in q or "浓度为" in q) and not r.get("gatewayUsedEngine"):
        n+=1
        print(f"  [{r.get('id')}] choice={has_options(q)} 纯对={r.get('pureLlmCorrect')} 网对={r.get('gatewayCorrect')} 期望={(r.get('expected') or '')[:45]}")
        print(f"      Q: {q.replace(chr(10),' ')[:110]}")
print(f"  c=1000ρω/M 候选数: {n}")

print()
print("===== calc: 稀释/浓度 候选（c1V1=c2V2 等，未介入）=====")
n=0
for r in calc:
    q = r.get("question") or ""
    if ("稀释" in q or "加水稀释" in q) and ("mol/L" in q or "mol·L" in q) and not r.get("gatewayUsedEngine"):
        n+=1
        print(f"  [{r.get('id')}] choice={has_options(q)} 期望={(r.get('expected') or '')[:40]} | Q: {q.replace(chr(10),' ')[:80]}")
print(f"  稀释候选数: {n}")

print()
print("===== equilibrium: Ksp 候选（含 ≥2 个 Ksp 数值，未介入）=====")
n=0
ksp_pat = re.compile(r'[Kk][_ ]?sp[\s\(（]?[A-Za-z0-9]*[\)）]?\s*[=＝]\s*([0-9]+\.?[0-9]*)\s*[×x\*]\s*10\s*[\^⁻-]?\s*([0-9]+)')
for r in eq:
    q = r.get("question") or ""
    nums = re.findall(r'10\s*[\^⁻-]?\s*[0-9]+', q)
    if len(nums) >= 2 and ("Ksp" in q or "溶度积" in q or "沉淀" in q) and not r.get("gatewayUsedEngine"):
        n+=1
        print(f"  [{r.get('id')}] choice={has_options(q)} 纯对={r.get('pureLlmCorrect')} 期望={(r.get('expected') or '')[:35]} | nums={nums[:4]}")
print(f"  Ksp 候选数(≥2个10^-n): {n}")
