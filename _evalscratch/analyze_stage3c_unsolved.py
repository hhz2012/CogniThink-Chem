# -*- coding: utf-8 -*-
"""分析 after_stage3c.json：PDF 题库未解出分布，为引擎扩展找方向（严格口径）。"""
import json, re, os
from collections import Counter, defaultdict

HERE = r"D:\github\CogniThink-Chem\_evalscratch"
data = json.load(open(os.path.join(HERE,"after_stage3c.json"), encoding="utf-8-sig"))

def collect(d):
    out={}
    for k in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        for o in d.get(k,[]) or []:
            if isinstance(o,dict) and "id" in o:
                out[o["id"]]=o
    return list(out.values())

cases=collect(data)
print("collected:",len(cases))

def is_pdf(o): return str(o.get("id","")).startswith("pdf-")
pdf=[o for o in cases if is_pdf(o)]
print("PDF cases:",len(pdf))

CHART_RE=re.compile(r"如图|下图|上图|图所示|图像|曲线|下表|表格|表中|流程图|流程如下|晶胞|装置图|实验装置|如图所示|见图|据图|坐标|滴定曲线|变化如图|关系图|示意图")
def is_chart(o): return bool(CHART_RE.search(o.get("question") or ""))

def solved(o):
    # 严格口径：引擎真值被采用且网关判正
    return bool(o.get("gatewayTruthAdopted") or o.get("gatewayCorrect"))

print("\n=== PDF 题总体（stage3c 严格口径）===")
print(f"  PDF 总数: {len(pdf)}")
print(f"  引擎解出(truthAdopted/网关正): {sum(1 for o in pdf if solved(o))}")
print(f"  引擎介入(usedEngine): {sum(1 for o in pdf if o.get('gatewayUsedEngine'))}")
print(f"  未解出(未靠引擎真值): {sum(1 for o in pdf if not solved(o))}")
print(f"  未解出中含图表信号(天花板): {sum(1 for o in pdf if not solved(o) and is_chart(o))}")
print(f"  未解出中纯LLM答对(靠LLM): {sum(1 for o in pdf if not solved(o) and o.get('pureLlmCorrect'))}")
print(f"  未解出中两者都错: {sum(1 for o in pdf if not solved(o) and not o.get('pureLlmCorrect'))}")

# 未解出 PDF 按 taskType
unsolved=[o for o in pdf if not solved(o)]
print(f"\n=== 未解出 PDF 按 taskType: {len(unsolved)} ===")
for tt,c in Counter((o.get('taskType') or '(none)') for o in unsolved).most_common():
    print(f"  {tt:<20} {c}")

# 未解出 PDF 剔除图表
uns_nc=[o for o in unsolved if not is_chart(o)]
print(f"\n=== 未解出 PDF（剔除图表）: {len(uns_nc)} 按问法关键词（可多选）===")
ASK={
 "离子共存":r"共存",
 "配平/写方程":r"配平|书写.*方程式|离子方程式|化学方程式|写出.*方程式",
 "氧化还原":r"氧化剂|还原剂|氧化产物|还原产物|被氧化|被还原|转移电子|电子转移|化合价",
 "物质的量/浓度":r"物质的量|浓度|mol/L|mol·l|稀释|质量分数|体积分数",
 "平衡/电离/水解":r"平衡|电离|水解|Ksp|勒夏特列|转化率|平衡常数|移动",
 "pH/滴定":r"pH|滴定|中和|酸碱性|H\+浓度|c\(H",
 "电化学":r"原电池|电解池|阳极|阴极|正极|负极|放电|充电|电极反应",
 "热化学":r"反应热|焓变|燃烧热|中和热|盖斯|Hess|ΔH",
 "有机":r"同分异构|结构简式|分子式|官能团|命名|核磁共振|氢谱|消去|加成|取代|酯化",
 "实验/检验/鉴别":r"检验|鉴别|分离|提纯|实验|现象|试剂|除杂",
 "元素推断/周期":r"元素|周期表|原子序数|质子数|电子排布|主族|短周期",
 "阿伏伽德罗":r"阿伏伽德罗|NA|N_A|6\.02|物质的量为",
 "结构/杂化/VSEPR":r"杂化|构型|VSEPR|键角|σ键|π键|轨道|电子式|空间结构",
 "工艺/流程":r"流程|工艺|转化关系|框图",
 "晶胞/晶体":r"晶胞|晶体|密度|配位数|面心|体心",
}
hit=Counter()
for o in uns_nc:
    q=o.get("question") or ""
    for n,p in ASK.items():
        if re.search(p,q): hit[n]+=1
for n,c in hit.most_common(): print(f"  {n:<22} {c}")

# 按 taskType × 是否纯LLM答对 找高价值可扩展域
print(f"\n=== 未解出 PDF 按 taskType × 纯LLM是否答对（找高ROI扩展域）===")
mat=defaultdict(lambda:[0,0])
for o in unsolved:
    tt=o.get('taskType') or '(none)'
    mat[tt][0]+=1
    if o.get('pureLlmCorrect'): mat[tt][1]+=1
for tt,(tot,llm) in sorted(mat.items(),key=lambda x:-x[1][0]):
    print(f"  {tt:<20} 未解出{tot:>4}  其中纯LLM答对{llm:>4}  图表{sum(1 for o in unsolved if (o.get('taskType') or '(none)')==tt and is_chart(o)):>3}")

# 导出未解出 PDF 清单（含题干+期望+回答）供抽样研读
dump=[]
for o in unsolved:
    dump.append({
        "id":o.get("id"),"taskType":o.get("taskType"),
        "chart":is_chart(o),
        "llm_ok":bool(o.get("pureLlmCorrect")),"gw_ok":bool(o.get("gatewayCorrect")),
        "used":bool(o.get("gatewayUsedEngine")),"truth":bool(o.get("gatewayTruthAdopted")),
        "question":o.get("question") or "",
        "expected":o.get("expected") or "",
        "gw_conclusion":(o.get("gatewayConclusion") or "")[:80],
        "gw_answer":(o.get("gatewayAnswer") or "")[:120],
    })
with open(os.path.join(HERE,"stage3c_unsolved_pdf.json"),"w",encoding="utf-8") as f:
    json.dump(dump,f,ensure_ascii=False,indent=1)
print(f"\n已导出 {len(dump)} 条未解出 PDF 到 _evalscratch/stage3c_unsolved_pdf.json")
