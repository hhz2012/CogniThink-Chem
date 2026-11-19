# -*- coding: utf-8 -*-
"""45 道引擎错题：互斥主因 + 次要缺失能力 精确统计。"""
import json, os, re
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
data=json.load(open(HERE+"\\engine_wrong_full.json",encoding="utf-8"))
CHART_RE=re.compile(r"如图|下图|上图|图所示|图像|曲线|下表|表格|表中|流程图|流程如下|晶胞|装置图|实验装置|如图所示|见图|据图|坐标|滴定曲线|变化如图|关系图")
PLACEHOLDER=["已匹配到相关反应","已查到相关颜色","已解析出相关物质与离子","已解析出相关物质","已查到溶解性","没有明显的反应证据","未找到直接匹配的反应"]
def is_chart(o): return bool(CHART_RE.search(o.get("question") or ""))
def is_placeholder(o):
    gw=o.get("gateway_conclusion") or ""
    return any(p in gw for p in PLACEHOLDER)
def is_choice(o): return bool(re.match(r"关键词:\s*[A-D]", o.get("expected") or ""))
def is_judge(o): return ("√" in (o.get("expected") or "")) or ("×" in (o.get("expected") or ""))
def needs_eq(o): return bool(re.search(r"配平|书写.*方程式|离子方程式|化学方程式|写出.*方程式", o.get("question") or ""))

n=len(data)
r2=[o for o in data if not is_placeholder(o)]          # 给了具体结论
r5=[o for o in data if is_placeholder(o) and is_chart(o)]# 占位+缺图
r1=[o for o in data if is_placeholder(o) and not is_chart(o)] # 占位+不缺图
print(f"引擎介入但答错 共 {n} 题")
print(f"  R2 给出具体结论但答错/答非所问 : {len(r2)}  -> {[o['id'] for o in r2]}")
print(f"  R5 占位 + 缺图/缺数据(天花板)  : {len(r5)}")
print(f"  R1 占位 + 不缺图(能力缺失)    : {len(r1)}")
print()
# R1 内部：缺什么能力
print("=== R1（32题，不缺图）缺失能力分解（可多选）===")
print(f"  选择题(需逐选项判断)   : {sum(1 for o in r1 if is_choice(o))}")
print(f"  判断题(需逐项正误)     : {sum(1 for o in r1 if is_judge(o))}")
print(f"  配平/书写方程式         : {sum(1 for o in r1 if needs_eq(o))}")
print(f"  题干短(<200字,疑截断)  : {sum(1 for o in r1 if len(o.get('question') or '')<200)}")
print(f"  其余(推理/知识)        : {sum(1 for o in r1 if not is_choice(o) and not is_judge(o) and not needs_eq(o))}")
print()
# R5 列表
print(f"R5 缺图题号: {sorted(o['id'] for o in r5)}")
# 各占位串分布
from collections import Counter
ph=Counter()
for o in data:
    gw=o.get("gateway_conclusion") or ""
    hit=[p for p in PLACEHOLDER if p in gw]
    ph[hit[0] if hit else "(具体结论)"]+=1
print("\n=== 引擎结论串分布 ===")
for k,v in ph.most_common(): print(f"  {k!r:30s} {v}")
