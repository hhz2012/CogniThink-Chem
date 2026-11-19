"""扫描 648 题评估集，量化 Tier 2 三类盲区（物质分类 / 物质转化链 / NA 阿伏伽德罗）。

从 after_stage4.json 读取全部 case（4 个分组合并），对 question 文本做关键词分类，
统计每类总数、当前未解数、LLM 对但网关错数（高价值零 harm 目标池），并输出清单供人工核对。
"""
import json, re, sys
from collections import defaultdict

EVAL = r"d:\github\CogniThink-Chem\_evalscratch\after_stage4.json"

with open(EVAL, encoding="utf-8") as f:
    summary = json.load(f)

cases = []
for key in ("bothCorrectCases", "bothWrongCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases"):
    cases.extend(summary.get(key, []))
print(f"total cases loaded: {len(cases)}", file=sys.stderr)

# ---- 分类检测器（基于 question 全文，含渲染后的选项） ----

NA_KW = ["阿伏伽德罗", "阿佛加德罗", "阿伏加德罗", "N_A", "Nₐ", "6.02×10", "6.02x10",
         "6.02×10", "6.02e23", "6.02×10²³", "6.02×1023"]
def is_na(q):
    if any(k in q for k in NA_KW):
        return True
    # “设NA为...” / “NA为阿伏伽德罗”
    if re.search(r"设\s*N\s*A", q) or re.search(r"N\s*A\s*为", q):
        return True
    return False

CAT_WORDS = ["酸性氧化物", "碱性氧化物", "两性氧化物", "过氧化物", "不成盐氧化物",
             "强电解质", "弱电解质", "非电解质", "电解质",
             "同素异形体", "同位素", "同分异构体", "同系物",
             "有机物", "无机物", "混合物", "纯净物", "单质", "化合物",
             "酸", "碱", "盐", "氧化物"]
def is_classification(q):
    if "分类" in q and ("正确" in q or "错误" in q or "下列" in q or "说法" in q):
        return True
    # 选项中出现“类别：物质1、物质2”模式
    if re.search(r"(酸性氧化物|碱性氧化物|两性氧化物|过氧化物|不成盐氧化物|"
                 r"强电解质|弱电解质|非电解质|电解质|同素异形体|同位素|"
                 r"同分异构体|同系物|有机物|无机物|混合物|纯净物|单质|化合物|"
                 r"酸|碱|盐|氧化物)\s*[:：]", q):
        return True
    # “下列属于 X 的是” / “X 属于”
    if re.search(r"属于.{0,4}(酸|碱|盐|氧化物|电解质|混合物|纯净物|单质|化合物)", q):
        return True
    return False

def is_transformation(q):
    has_arrow = ("→" in q) or ("→" in q) or ("->" in q) or ("⟶" in q) or ("⇒" in q)
    has_trans = ("转化" in q)
    has_step = ("一步" in q) or ("能实现" in q) or ("制备" in q) or ("制取" in q) or ("转化关系" in q)
    if has_trans and (has_arrow or has_step or "物质间" in q or "下列" in q):
        return True
    if has_arrow and ("反应" in q or "实现" in q or "下列" in q):
        # 选项形如 A→B→C 的转化链题
        if "转化" in q or "实现" in q or "制备" in q or "制取" in q:
            return True
    return False

buckets = defaultdict(list)
for c in cases:
    q = c.get("question") or ""
    cats = []
    if is_na(q): cats.append("NA")
    if is_classification(q): cats.append("classification")
    if is_transformation(q): cats.append("transformation")
    if not cats:
        cats.append("other")
    for cat in cats:
        buckets[cat].append(c)

def status(c):
    return dict(
        id=c.get("id"),
        taskType=c.get("taskType"),
        truthAdopted=c.get("gatewayTruthAdopted"),
        gwCorrect=c.get("gatewayCorrect"),
        pureCorrect=c.get("pureLlmCorrect"),
        gwUsed=c.get("gatewayUsedEngine"),
    )

out = []
for cat in ["classification", "transformation", "NA", "other"]:
    lst = buckets[cat]
    unsolved = [c for c in lst if not c.get("gatewayTruthAdopted")]
    pure_right_gw_wrong = [c for c in lst if c.get("pureLlmCorrect") and not c.get("gatewayCorrect")]
    both_wrong = [c for c in lst if not c.get("pureLlmCorrect") and not c.get("gatewayCorrect")]
    out.append(f"\n========== {cat} ==========")
    out.append(f"total={len(lst)}  unsolved(truthAdopted=false)={len(unsolved)}  "
               f"pureRight_gwWrong={len(pure_right_gw_wrong)}  bothWrong={len(both_wrong)}")
    out.append(f"-- unsolved list (id | taskType | pureCorrect | question[:60]) --")
    for c in unsolved:
        q = (c.get("question") or "").replace("\n", " ")
        out.append(f"  {c.get('id'):<10} {str(c.get('taskType')):<10} "
                   f"pure={c.get('pureLlmCorrect')}  {q[:70]}")

# 跨类重叠检查
overlap = [c for c in cases if sum(1 for cat in ("NA","classification","transformation")
                                   if c in buckets[cat]) >= 2]
out.append(f"\n========== overlap (in >=2 Tier2 cats) ==========")
out.append(f"count={len(overlap)}")
for c in overlap[:40]:
    cats = [cat for cat in ("NA","classification","transformation") if c in buckets[cat]]
    out.append(f"  {c.get('id'):<10} {cats}  {(c.get('question') or '')[:60].replace(chr(10),' ')}")

text = "\n".join(out)
with open(r"d:\github\CogniThink-Chem\_evalscratch\tier2_scan.txt", "w", encoding="utf-8") as f:
    f.write(text)
print(text)
