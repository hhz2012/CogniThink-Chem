# -*- coding: utf-8 -*-
"""抽样 stage3c 未解出 PDF 题（去图表），按 taskType 与问法类别输出可读样本，供研判可规则化性。"""
import json, re, os
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
data=json.load(open(os.path.join(HERE,"stage3c_unsolved_pdf.json"),encoding="utf-8-sig"))
data=[o for o in data if not o.get("chart")]  # 去图表
print(f"去图表未解出 PDF: {len(data)}\n")

def clean(s): return re.sub(r"\s+"," ",(s or "")).strip()

# 1) 按 taskType 各取 6 条
from collections import defaultdict
by_tt=defaultdict(list)
for o in data: by_tt[o.get("taskType") or "(none)"].append(o)
print("############ 按 taskType 抽样（各≤6，优先纯LLM答对=靠LLM的题）############")
for tt,lst in sorted(by_tt.items(),key=lambda x:-len(x[1])):
    lst_s=sorted(lst,key=lambda o:(not o.get("llm_ok"), o.get("id","")))
    print(f"\n===== {tt} (共{len(lst)}) =====")
    for o in lst_s[:6]:
        q=clean(o["question"])[:110]
        print(f"  [{o['id']}] llm_ok={o['llm_ok']} used={o['used']} | Q: {q}")
        print(f"        expected: {clean(o['expected'])[:70]}")
        print(f"        gw: {clean(o.get('gw_answer') or '')[:70]}")

# 2) choice 题专门看：选项是否存在、问法方向、引擎为何没解
print("\n\n############ choice 题深度抽样（387→去图表后）############")
ch=by_tt.get("choice",[])
print(f"choice 去图表: {len(ch)}")
# 统计 choice 题干里是否渲染了 A/B/C/D 选项
opt_re=re.compile(r"(^|\n)\s*[A-D][\.．、]\s")
has_opt=sum(1 for o in ch if opt_re.search(o.get("question") or ""))
print(f"  choice 题干含渲染选项的: {has_opt}/{len(ch)}")
# 取 12 条有选项的
shown=0
for o in ch:
    if shown>=12: break
    q=o.get("question") or ""
    if not opt_re.search(q): continue
    print(f"\n  --- [{o['id']}] llm_ok={o['llm_ok']} ---")
    print("  Q:", clean(q)[:260])
    print("  expected:", clean(o["expected"])[:80])
    shown+=1

# 3) 非 choice 且非图表的"可规则化"候选：calc/eq/reaction/balance/ion 抽样
print("\n\n############ 非 choice 非图表 可规则化候选抽样 ############")
for tt in ["calculation","equilibrium","reaction_check","unknown","concept","vsepr","structure_verify"]:
    lst=by_tt.get(tt,[])
    print(f"\n----- {tt} ({len(lst)}) 各取 5 -----")
    for o in lst[:5]:
        print(f"  [{o['id']}] llm={o['llm_ok']} | Q: {clean(o['question'])[:120]}")
        print(f"        exp: {clean(o['expected'])[:60]}")
