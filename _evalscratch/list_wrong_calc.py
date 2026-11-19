# -*- coding: utf-8 -*-
"""列出 after5 所有网关错的计算题：id + 期望关键词 + 题干前90字，
人工判断哪些是单公式可解的数值题（AC-6 候选）。"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
d=json.load(open(os.path.join(HERE,"after5.json"),encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(d.get(p,[]) or [])
calc=[r for r in rows if r.get("taskType")=="calculation" and not r.get("gatewayCorrect")]
print(f"网关错的 calculation: {len(calc)}")
def has(q,*ks): return any(k in (q or "") for k in ks)
for r in calc:
    q=r.get("question") or ""
    tags=[]
    if has(q,"质量分数","ω","1000") and has(q,"密度","ρ","g/cm","g·cm"): tags.append("c=1000ρω/M")
    if has(q,"稀释","配制") and re.search(r"\d+\s*(?:mL|L)",q): tags.append("稀释c1V1=c2V2")
    if has(q,"pH") and re.search(r"mol/L|mol·L",q): tags.append("强酸pH")
    if has(q,"标况","标准状况","22.4"): tags.append("气体22.4")
    print(f"\n[{r.get('id')}] tags={tags} 纯对={r.get('pureLlmCorrect')} 介入={r.get('gatewayUsedEngine')}")
    print(f"  期望: {(r.get('expected') or '')[:75]}")
    print(f"  Q: {q[:95].replace(chr(10),' ')}")
