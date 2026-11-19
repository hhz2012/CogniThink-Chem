# -*- coding: utf-8 -*-
"""扫描 after5 计算题，找可用简单公式求解的单空题候选（AC-6 扩展）。"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
d=json.load(open(os.path.join(HERE,"after5.json"),encoding="utf-8-sig"))
parts=["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows=[]
for p in parts: rows.extend(d.get(p,[]) or [])
calc=[r for r in rows if r.get("taskType")=="calculation"]
print(f"calculation 总 {len(calc)}: 网关对{sum(1 for r in calc if r.get('gatewayCorrect'))} 介入{sum(1 for r in calc if r.get('gatewayUsedEngine'))}")

def has(q,*ks): return any(k in (q or "") for k in ks)
cand=[]
for r in calc:
    q=r.get("question") or ""; exp=r.get("expected") or ""
    flags=[]
    if has(q,"pH"): flags.append("pH")
    if has(q,"1000","ρ","质量分数","ω"): flags.append("c=1000ρω/M")
    if has(q,"稀释") and re.search(r"\d",q): flags.append("dilution")
    if has(q,"标况","标准状况","22.4"): flags.append("gas22.4")
    if has(q,"物质的量浓度") and re.search(r"mol",q): flags.append("c=n/V")
    if has(q,"摩尔质量","g/mol") and has(q,"质量","g"): flags.append("n=m/M")
    if flags and not r.get("gatewayCorrect"):
        cand.append((r,flags))
print(f"当前网关错 且 命中可解模式: {len(cand)}")
for r,flags in cand[:40]:
    print(f"\n[{r.get('id')}] flags={flags} 介入={r.get('gatewayUsedEngine')} 纯对={r.get('pureLlmCorrect')}")
    print(f"  期望: {exp[:70]}")
    print(f"  Q: {q[:120].replace(chr(10),' ')}")
