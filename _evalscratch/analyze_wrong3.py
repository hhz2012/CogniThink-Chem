# -*- coding: utf-8 -*-
import json, os, re
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
def is_choice(r): return bool(re.match(r"关键词:\s*[A-D]", (r.get("expected") or "")))

print("===== ⑤ calc/eq 缺求解器（非选择、非缺图、引擎未介入）=====")
n=0
for r in sorted(wrong, key=lambda x:x.get("id") or ""):
    t=r.get("taskType") or "?"
    if needs_chart(r) or r.get("gatewayUsedEngine") or is_choice(r): continue
    if t in ("calculation","equilibrium"):
        n+=1
        print(f"  [{r.get('id')}] {t}")
        print(f"    exp={(r.get('expected') or '')[:90]}")
        print(f"    q={(r.get('question') or '')[:110].replace(chr(10),' ')}")
        print(f"    gw={(r.get('gatewayAnswer') or '')[:90]}")
print(f"  共 {n} 题")
print()
print("===== 疑似判分漏判 pdf-531 =====")
for r in wrong:
    if r.get("id")=="pdf-531":
        print("  expected:", repr(r.get("expected","")))
        print("  gatewayAnswer:", repr((r.get("gatewayAnswer") or "")[:300]))
        print("  pureLlmAnswer:", repr((r.get("pureLlmAnswer") or "")[:200]))
        print("  gatewayCorrect:", r.get("gatewayCorrect"), " pureCorrect:", r.get("pureLlmCorrect"), " task:", r.get("taskType"))
