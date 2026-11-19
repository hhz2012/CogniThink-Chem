# -*- coding: utf-8 -*-
"""逐题归因：45 道引擎介入但答错的题。导出完整内容 + 规则初判。"""
import json, os, re
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
d=json.load(open(os.path.join(HERE,"after8b.json"),encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
cs=cases(d)
# 引擎介入但答错
rows=[r for r in cs if r.get("gatewayUsedEngine") and not r.get("gatewayCorrect")]
rows=sorted(rows, key=lambda x:(x.get("taskType") or "", x.get("id") or ""))

CHART_RE=re.compile(r"如图|下图|上图|图所示|图像|曲线|下表|表格|表中|流程图|流程如下|晶胞|装置图|实验装置|如图所示|见图|据图|坐标|滴定曲线|变化如图|关系图")
PLACEHOLDER=["已匹配到相关反应","已查到相关颜色","已解析出相关物质与离子","已解析出相关物质","没有明显的反应证据","未找到直接匹配的反应"]

def classify_auto(r):
    """规则初判，返回标签列表 + 说明"""
    tags=[]
    q=r.get("question") or ""
    exp=r.get("expected") or ""
    gw=(r.get("gatewayConclusion") or r.get("gatewayAnswer") or "")
    # R5 缺图
    if CHART_RE.search(q): tags.append(("R5-缺图缺数据","题干引用图表"))
    # R1 占位结论
    if any(p in gw for p in PLACEHOLDER): tags.append(("R1-占位结论(答非所问)","引擎返回检索占位，未真正作答"))
    # R3 选择题/判断题
    if re.match(r"关键词:\s*[A-D]", exp): tags.append(("R3-选择题/判断题","引擎不支持选项级作答"))
    if "√" in exp or "×" in exp: tags.append(("R3-判断题","引擎不支持逐项正误判断"))
    # R6 配平/书写
    if re.search(r"配平|书写.*方程式|离子方程式|化学方程式|写出.*方程式", q): tags.append(("R6-需配平/书写方程式","引擎无配平器"))
    # R4 题干截断（题干很短或只有小问片段，没有反应物）
    if len(q) < 120: tags.append(("R4-题干可能截断/缺上下文",f"题干仅{len(q)}字"))
    return tags

# 统计
from collections import Counter
cnt=Counter()
for r in rows:
    for t,_ in classify_auto(r): cnt[t]+=1
print(f"引擎介入但答错 共 {len(rows)} 题（含缺图）")
print("=== 规则初判标签分布（可多选）===")
for k,v in cnt.most_common(): print(f"  {k:28s} {v}")
print()

# 导出完整明细
out=[]
for r in rows:
    tags=classify_auto(r)
    out.append({
        "id":r.get("id"),"task":r.get("taskType"),"ok":r.get("gatewayOk"),
        "tags":[t[0] for t in tags],
        "tag_notes":[t[1] for t in tags],
        "expected":r.get("expected",""),
        "question":r.get("question",""),
        "gateway_conclusion":r.get("gatewayConclusion") or r.get("gatewayAnswer") or "",
        "derivation":r.get("gatewayDerivation") or r.get("derivationSteps") or "",
        "pure_wrong":not r.get("pureLlmCorrect"),
    })
with open(os.path.join(HERE,"engine_wrong_full.json"),"w",encoding="utf-8") as f:
    json.dump(out,f,ensure_ascii=False,indent=1)
print(f"完整明细已存 engine_wrong_full.json ({len(out)}题)")

# 打印每题：id task 标签 | expected前80 | 引擎结论前100
print("\n=== 逐题速览 ===")
for o in out:
    print(f"[{o['id']}] {o['task']} tags={o['tags']}")
    print(f"   EXP: {(o['expected'] or '')[:90]}")
    print(f"   GW : {(o['gateway_conclusion'] or '')[:110]}")
