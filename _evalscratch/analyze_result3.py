# -*- coding: utf-8 -*-
"""分析 gateway_vs_llm_result3.json：重现「未介入」盲区分布，为引擎扩展找方向。"""
import json, re, os
from collections import Counter, defaultdict

HERE = r"D:\github\CogniThink-Chem"
data = json.load(open(os.path.join(HERE, "gateway_vs_llm_result3.json"), encoding="utf-8-sig"))

print("top keys:", list(data.keys()))
print("totalCases:", data.get("totalCases"))
for k in ["pureLlmCorrect","gatewayCorrect","gatewayEngineUsed","bothCorrect","gatewayOnlyCorrect","pureLlmOnlyCorrect","bothWrong"]:
    print(f"  {k}: {data.get(k)}")

# 收集所有 case 记录
all_cases = []
for arr_key in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases",
                "bothCorrect","gatewayOnlyCorrect","pureLlmOnlyCorrect","bothWrong"]:
    v = data.get(arr_key)
    if isinstance(v, list):
        for o in v:
            if isinstance(o, dict) and "id" in o:
                all_cases.append(o)
print("\ncollected case records:", len(all_cases))
if all_cases:
    print("sample keys:", list(all_cases[0].keys()))

# 去重（按 id）
seen = {}
for o in all_cases:
    seen[o["id"]] = o
all_cases = list(seen.values())
print("unique by id:", len(all_cases))

def is_pdf(o):
    return str(o.get("id","")).startswith("pdf-") or bool(o.get("isPdf")) or bool(o.get("is_pdf"))

CHART_RE = re.compile(r"如图|下图|上图|图所示|图像|曲线|下表|表格|表中|流程图|流程如下|晶胞|装置图|实验装置|如图所示|见图|据图|坐标|滴定曲线|变化如图|关系图|示意图")
def is_chart(o):
    return bool(CHART_RE.search(o.get("question") or ""))

PLACEHOLDER = ["已匹配到相关反应","已查到相关颜色","已解析出相关物质与离子","已解析出相关物质","已查到溶解性","没有明显的反应证据","未找到直接匹配的反应"]
def is_placeholder(o):
    gw = o.get("gateway_conclusion") or o.get("gatewayConclusion") or ""
    return any(p in gw for p in PLACEHOLDER)

# 按 taskType 聚合
by_tt = defaultdict(lambda: {"n":0,"pdf":0,"used":0,"truth":0,"gw_correct":0,"llm_correct":0,"chart":0})
for o in all_cases:
    tt = o.get("taskType") or o.get("task_type") or "(none)"
    b = by_tt[tt]
    b["n"] += 1
    if is_pdf(o): b["pdf"] += 1
    if o.get("gatewayUsedEngine") or o.get("gateway_used_engine"): b["used"] += 1
    if o.get("gatewayTruthAdopted") or o.get("gateway_truth_adopted"): b["truth"] += 1
    if o.get("gatewayCorrect"): b["gw_correct"] += 1
    if o.get("pureLlmCorrect"): b["llm_correct"] += 1
    if is_chart(o): b["chart"] += 1

print("\n=== 按 taskType 分布（全部收集到的 case）===")
print(f"{'taskType':<20}{'n':>6}{'pdf':>6}{'used':>6}{'truth':>6}{'gw_ok':>7}{'llm_ok':>7}{'chart':>6}")
for tt, b in sorted(by_tt.items(), key=lambda x:-x[1]["n"]):
    print(f"{tt:<20}{b['n']:>6}{b['pdf']:>6}{b['used']:>6}{b['truth']:>6}{b['gw_correct']:>7}{b['llm_correct']:>7}{b['chart']:>6}")

# 未介入 = gatewayUsedEngine=False 或 taskType in concept/unknown 或 引擎给了占位/没真值
def not_intervened(o):
    used = bool(o.get("gatewayUsedEngine") or o.get("gateway_used_engine"))
    tt = o.get("taskType") or o.get("task_type") or ""
    truth = bool(o.get("gatewayTruthAdopted") or o.get("gateway_truth_adopted"))
    if not used: return True
    if tt in ("concept","unknown"): return True
    if not truth and is_placeholder(o): return True
    if not truth: return True   # 引擎介入了但真值未被采用（含占位/不相关）
    return False

notinv = [o for o in all_cases if not_intervened(o)]
print(f"\n=== 未介入（not intervened）总数: {len(notinv)} ===")
ni_tt = Counter((o.get("taskType") or o.get("task_type") or "(none)") for o in notinv)
for tt,c in ni_tt.most_common():
    print(f"  {tt:<20} {c}")
print(f"  其中 PDF 题: {sum(1 for o in notinv if is_pdf(o))}")
print(f"  其中 含图表信号: {sum(1 for o in notinv if is_chart(o))}")
print(f"  其中 纯LLM答对(引擎没解): {sum(1 for o in notinv if o.get('pureLlmCorrect'))}")

# 把未介入的 PDF 题（非图表）按问法关键词再细分，找引擎扩展机会
ASK = {
    "离子共存": r"共存",
    "配平/写方程": r"配平|书写.*方程式|离子方程式|化学方程式|写出.*方程式|配平下列",
    "氧化还原": r"氧化剂|还原剂|氧化产物|还原产物|被氧化|被还原|电子转移|转移电子",
    "物质的量/浓度计算": r"物质的量|浓度|mol/L|mol·l|稀释|质量分数|体积",
    "平衡/电离/水解": r"平衡|电离|水解|Ksp|勒夏特列|转化率|平衡常数",
    "pH/滴定": r"pH|滴定|中和|酸碱性",
    "电化学": r"原电池|电解池|阳极|阴极|正极|负极|放电|充电|电极反应",
    "热化学": r"反应热|焓变|ΔH|燃烧热|中和热|盖斯|Hess",
    "有机结构/同分异构": r"同分异构|结构简式|分子式|官能团|命名|核磁共振|氢谱",
    "实验/检验/鉴别": r"检验|鉴别|分离|提纯|实验|现象|试剂",
    "元素推断/周期表": r"元素|周期表|原子序数|质子数|电子排布",
    "阿伏伽德罗常数": r"阿伏伽德罗|NA|N_A|6\.02",
    "结构/杂化/VSEPR": r"杂化|构型|VSEPR|键角|σ键|π键|轨道|电子式",
    "工艺/流程": r"流程|工艺|转化关系",
    "晶胞/晶体": r"晶胞|晶体|密度|配位数",
}
pdf_notinv_nochart = [o for o in notinv if is_pdf(o) and not is_chart(o)]
print(f"\n=== 未介入 PDF 题（剔除图表信号）: {len(pdf_notinv_nochart)} 按问法关键词（可多选）===")
hit = Counter()
for o in pdf_notinv_nochart:
    q = o.get("question") or ""
    for name, pat in ASK.items():
        if re.search(pat, q):
            hit[name]+=1
for name,c in hit.most_common():
    print(f"  {name:<22} {c}")

# 保存未介入清单供后续抽样
out = []
for o in pdf_notinv_nochart:
    out.append({
        "id": o.get("id"),
        "taskType": o.get("taskType") or o.get("task_type"),
        "llm_ok": bool(o.get("pureLlmCorrect")),
        "gw_ok": bool(o.get("gatewayCorrect")),
        "used": bool(o.get("gatewayUsedEngine") or o.get("gateway_used_engine")),
        "truth": bool(o.get("gatewayTruthAdopted") or o.get("gateway_truth_adopted")),
        "question": (o.get("question") or "")[:120],
        "expected": (o.get("expected") or "")[:80],
    })
with open(os.path.join(HERE,"_evalscratch","result3_notinv.json"),"w",encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=1)
print(f"\n已保存未介入 PDF 清单到 _evalscratch/result3_notinv.json（{len(out)} 条）")
