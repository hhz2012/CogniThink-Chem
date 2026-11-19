# -*- coding: utf-8 -*-
"""after3 深挖：反应证据路径净价值、both-wrong 分布、可扩展候选。"""
import json, os, collections, re
HERE = os.path.dirname(os.path.abspath(__file__))
d = json.load(open(os.path.join(HERE, "after3.json"), encoding="utf-8-sig"))
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
cases=[]
for p in parts: cases.extend(d.get(p,[]) or [])
def is_pdf(r): return (r.get("id") or "").startswith("pdf-")

# 1) 顶层
print(f"总数 {len(cases)}  网关对 {d.get('gatewayCorrect')}  纯对 {d.get('pureLlmCorrect')}  引擎介入 {d.get('gatewayEngineUsed')}")
print(f"bothCorrect={d.get('bothCorrect')} gatewayOnly={d.get('gatewayOnlyCorrect')} pureOnly={d.get('pureLlmOnlyCorrect')} bothWrong={d.get('bothWrong')}")
print()

# 2) reaction_check: 结论是否具体(concrete) vs 证据路径(evidence)
def is_concrete(c):
    c=c or ""
    if any(k in c for k in ["已匹配","相关反应","相关颜色","已查到"]): return False
    return any(k in c for k in ["能","不能","共存","不共存","可以","不可以","正确","错误"])
rc=[r for r in cases if r.get("taskType")=="reaction_check"]
conc=[r for r in rc if is_concrete(r.get("gatewayConclusion"))]
evid=[r for r in rc if not is_concrete(r.get("gatewayConclusion"))]
def stat(rows,label):
    if not rows: return
    g=sum(1 for r in rows if r.get("gatewayCorrect")); p=sum(1 for r in rows if r.get("pureLlmCorrect"))
    print(f"  {label}: n={len(rows)} 网关对{g}({g/len(rows)*100:.0f}%) 纯对{p}({p/len(rows)*100:.0f}%) 网关-纯={g-p}")
print("=== reaction_check 按结论具体性 ===")
stat(conc,"concrete(直接用结论)")
stat(evid,"evidence(走LLM证据)")
# evidence 路径里：网关对 vs 纯对，若纯≥网关则门控回退有收益
e_g=sum(1 for r in evid if r.get("gatewayCorrect")); e_p=sum(1 for r in evid if r.get("pureLlmCorrect"))
print(f"  -> evidence 路径若全部回退 pureAnswer：网关将从 {e_g} 变为 {e_p} (净{e_p-e_g:+d})")
print()

# 3) 帮倒忙明细 + 是否 evidence 路径
hurts=[r for r in cases if r.get("pureLlmCorrect") and not r.get("gatewayCorrect") and r.get("gatewayUsedEngine")]
print(f"=== 帮倒忙 {len(hurts)} 题 ===")
for r in hurts:
    print(f"  [{r.get('id')}] task={r.get('taskType')} concrete={is_concrete(r.get('gatewayConclusion'))} 结论={(r.get('gatewayConclusion') or '')[:30]}")
    print(f"     Q: {(r.get('question') or '').replace(chr(10),' ')[:90]}")
print()

# 4) both-wrong 按 taskType / 有图 / 选择
def has_fig(q):
    q=q or ""; return any(k in q for k in ["如图","图所示","图略","见图","下图","上图"])
def is_choice(q):
    q=q or ""; return bool(re.search(r"(^|\n)\s*[A-D][\.．、]\s", q))
bw=[r for r in cases if not r.get("pureLlmCorrect") and not r.get("gatewayCorrect")]
print(f"=== both-wrong {len(bw)} 题 ===")
print("  by taskType:", dict(collections.Counter(r.get("taskType") for r in bw)))
print(f"  其中题干提及图(无图不可解): {sum(1 for r in bw if has_fig(r.get('question')))}")
print(f"  其中PDF选择题: {sum(1 for r in bw if is_pdf(r) and is_choice(r.get('question')))}")
print(f"  其中引擎介入: {sum(1 for r in bw if r.get('gatewayUsedEngine'))}")
# 引擎介入的 both-wrong (引擎给了结论但还错)
eng_bw=[r for r in bw if r.get("gatewayUsedEngine")]
print(f"  -- 引擎介入但仍错 {len(eng_bw)} (前15) --")
for r in eng_bw[:15]:
    print(f"   [{r.get('id')}] task={r.get('taskType')} 期望={(r.get('expected') or '')[:30]} 结论={(r.get('gatewayConclusion') or '')[:30]}")
print()

# 5) calc/eq 未介入 (Ok=null 或 taskType concept/unknown 但其实是计算?) — AC-6 候选
print("=== AC-6: calc+eq 引擎介入明细 ===")
for task in ["calculation","equilibrium"]:
    rows=[r for r in cases if r.get("taskType")==task]
    used=[r for r in rows if r.get("gatewayUsedEngine")]
    print(f"  {task}: 总{len(rows)} 介入{len(used)} 网关对{sum(1 for r in used if r.get('gatewayCorrect'))}/{len(used)}")
    # 未介入的，纯LLM答对数量(潜在可救)
    notused=[r for r in rows if not r.get("gatewayUsedEngine")]
    print(f"     未介入 {len(notused)}: 纯LLM对{sum(1 for r in notused if r.get('pureLlmCorrect'))} 网关对{sum(1 for r in notused if r.get('gatewayCorrect'))}")
print()

# 6) 网对->网错 4题核查 (是否 taskType 漂移)
print("=== 网对->网错 核查 (after3 vs after2) ===")
d2=json.load(open(os.path.join(HERE,"after2.json"),encoding="utf-8-sig"))
ca={r.get("id"):r for r in cases}
parts2=[]
for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    parts2.extend(d2.get(p,[]) or [])
cb2={r.get("id"):r for r in parts2}
flip_wrong=[]
for qid in ca:
    a1=cb2.get(qid,{}).get("gatewayCorrect"); a2=ca[qid].get("gatewayCorrect")
    if a1 and not a2: flip_wrong.append(qid)
print(f"  after2对->after3错(疑似回归): {flip_wrong}")
for qid in flip_wrong:
    a=cb2[qid]; b=ca[qid]
    print(f"   [{qid}] after2 task={a.get('taskType')} 结论={(a.get('gatewayConclusion') or '')[:25]} | after3 task={b.get('taskType')} 结论={(b.get('gatewayConclusion') or '')[:25]} 纯对after3={b.get('pureLlmCorrect')}")
