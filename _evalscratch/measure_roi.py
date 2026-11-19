# -*- coding: utf-8 -*-
"""测算 184 错题中 配平/方程式书写(path A) vs 逐选项判断(path B) 的可解池。"""
import json, os, re, collections
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
d=json.load(open(os.path.join(HERE,"after8b.json"),encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
cs=cases(d)
wrong=[r for r in cs if not r.get("gatewayCorrect")]
CHART_RE=re.compile(r"如图|下图|上图|图所示|图像|曲线|下表|表格|表中|流程图|流程如下|晶胞|装置图|实验装置|如图所示|见图|据图|坐标|滴定曲线|变化如图|关系图")
def needs_chart(r): return bool(CHART_RE.search(r.get("question") or ""))

EQ_RE=re.compile(r"配平|书写.*方程式|离子方程式|化学方程式|方程式.*书写|写出.*方程式|按要求书写")
CHOICE_RE=re.compile(r"下列.*(错误|正确|不正确|不涉及|涉及)|说法.*(错误|正确)|解释.*错误|能.*实现的是|的有\s*\(")

pathA=[]   # 配平/书写方程式，非缺图
pathB=[]   # 选择题逐选项判断，非缺图
ceiling=[] # 缺图
for r in wrong:
    q=r.get("question") or ""
    if needs_chart(r):
        ceiling.append(r); continue
    if EQ_RE.search(q): pathA.append(r)
    elif re.match(r"关键词:\s*[A-D]", r.get("expected") or "") or CHOICE_RE.search(q): pathB.append(r)

print(f"错题总数 {len(wrong)}")
print(f"  缺图天花板: {len(ceiling)}")
print(f"  路径A 配平/方程式书写(非缺图): {len(pathA)}  -> 引擎可确定性求解")
print(f"    题号: {sorted(r['id'] for r in pathA)}")
print(f"  路径B 选择/判断逐选项(非缺图): {len(pathB)} -> 需选项拆解+事实校验")
print(f"  其余(纯LLM知识/概念/无引擎): {len(wrong)-len(ceiling)-len(pathA)-len(pathB)}")
print()
print("=== 路径A 明细 ===")
for r in sorted(pathA,key=lambda x:x['id']):
    print(f"  [{r['id']}] task={r['taskType']} eng={r['gatewayUsedEngine']}")
    print(f"    EXP={(r.get('expected') or '')[:140]}")
    print(f"    Q={(r.get('question') or '')[:140].replace(chr(10),' ')}")
