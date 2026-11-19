"""导出 Tier-2 候选选择题的完整题干+选项+答案+解析，供设计事实库/校验器时核对格式。

从 pdf_questions.json 读取，按更收紧的 3 类检测器筛选，输出到 tier2_candidates_full.txt。
"""
import json, re

PDF = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
with open(PDF, encoding="utf-8") as f:
    items = json.load(f)
print(f"pdf questions: {len(items)}")

def is_na(q):
    kw = ["阿伏伽德罗","阿佛加德罗","阿伏加德罗","N_A","Nₐ","6.02×10","6.02x10","6.02e23","6.02×10²³"]
    if any(k in q for k in kw): return True
    if re.search(r"设\s*N\s*A", q) or re.search(r"N\s*A\s*为", q) or re.search(r"N_A", q): return True
    return False

def is_classification(q):
    if re.search(r"分类.{0,6}(正确|错误|说法|下列)", q): return True
    if re.search(r"(酸性氧化物|碱性氧化物|两性氧化物|过氧化物|不成盐氧化物|"
                 r"强电解质|弱电解质|非电解质|电解质|同素异形体|同位素|"
                 r"同分异构体|同系物|混合物|纯净物)\s*[:：]\s*[^\n]{0,40}[、，,]", q):
        return True
    # 选项里“属于硫酸盐”这类
    if re.search(r"属于\s*(硫酸盐|硝酸盐|碳酸盐|氯化物|钠盐|钾盐|铵盐|亚铁盐|铁盐|铝盐|"
                 r"酸|碱|盐|氧化物|电解质|非电解质|单质|化合物|有机物|无机物)", q):
        return True
    return False

def is_transformation(q):
    has_arrow = any(a in q for a in ["→","→","->","⟶","⇒","-->"])
    if re.search(r"(能|不能|可以|无法).{0,4}(一步实现|实现|转化)", q) and has_arrow:
        return True
    if re.search(r"(转化|制备|制取).{0,10}(能|不能|可以|无法|均能|均不能|一步)", q) and has_arrow:
        return True
    if re.search(r"物质间.{0,4}转化", q):
        return True
    return False

def render(it):
    lines = [f"### {it.get('id')}  type={it.get('type')}  answer={it.get('answer')}"]
    lines.append(f"Q: {it.get('question','')}")
    opts = it.get("options") or {}
    for k in sorted(opts):
        lines.append(f"  {k}. {opts[k]}")
    note = (it.get("note") or "").replace("\n"," ")
    lines.append(f"NOTE: {note[:300]}")
    return "\n".join(lines)

buckets = {"classification":[],"transformation":[],"NA":[]}
for it in items:
    q = it.get("question","") + "\n" + "\n".join(f"{k}. {v}" for k,v in (it.get("options") or {}).items())
    if is_na(q): buckets["NA"].append(it)
    if is_classification(q): buckets["classification"].append(it)
    if is_transformation(q): buckets["transformation"].append(it)

out = []
for cat, lst in buckets.items():
    out.append(f"\n{'='*70}\n# {cat}  count={len(lst)}\n{'='*70}")
    for it in lst:
        out.append(render(it))
        out.append("-"*60)

with open(r"d:\github\CogniThink-Chem\_evalscratch\tier2_candidates_full.txt","w",encoding="utf-8") as f:
    f.write("\n".join(out))
print("written tier2_candidates_full.txt")
for cat,lst in buckets.items():
    print(f"  {cat}: {len(lst)}")
