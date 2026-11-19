# -*- coding: utf-8 -*-
"""量化基线中可通过「恢复选项+改进判分」获益的题量。"""
import json, collections
PATH = r"d:\github\CogniThink-Chem\_evalscratch\baseline.json"
data = json.load(open(PATH, encoding="utf-8-sig"))
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
rows = []
for p in parts: rows += data.get(p,[]) or []

pdf_ids = set()
# 加载 pdf_questions 的类型映射
import json as J
qbank = J.load(open(r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8"))
qtype = {x["id"]:x.get("type") for x in qbank}
# 备份中有 options 的
bk = J.load(open(r"d:\github\CogniThink-Chem\pdfs\questions_parsed.json",encoding="utf-8"))
has_opt = set(x["id"] for x in bk if x.get("type")=="choice" and x.get("options"))
has_ans_letter = set(x["id"] for x in bk if x.get("type")=="choice" and x.get("answer") and len(x["answer"])==1 and x["answer"] in "ABCD")

print("题库：choice", sum(1 for v in qtype.values() if v=="choice"),
      " 其中备份有options:", len(has_opt),
      " 有单字母答案:", len(has_ans_letter))

def is_pdf(r): return (r.get("id") or "").startswith("pdf-")
def t(r): return qtype.get(r.get("id"), "?")

both_wrong = [r for r in rows if not r.get("pureLlmCorrect") and not r.get("gatewayCorrect")]
print(f"\nboth_wrong 总数 {len(both_wrong)}")
print("both_wrong 按题型:", collections.Counter(t(r) for r in both_wrong if is_pdf(r)))

# 纯LLM明确索要选项/拒答的
def refuses(a):
    a = a or ""
    return ("请提供" in a and "选项" in a) or "缺少选项" in a or ("选项" in a and "无法判断" in a) or "无法判断哪" in a

bw_choice = [r for r in both_wrong if is_pdf(r) and t(r)=="choice"]
print(f"\nboth_wrong 中 PDF 选择题: {len(bw_choice)}")
print(f"  其中纯LLM明确拒答(索要选项): {sum(1 for r in bw_choice if refuses(r.get('pureLlmAnswer')))}")
print(f"  其中备份有options可恢复: {sum(1 for r in bw_choice if r.get('id') in has_opt)}")
print(f"  其中有单字母答案可用于判分: {sum(1 for r in bw_choice if r.get('id') in has_ans_letter)}")

# 含"如图/图所示/图略"的不可解题（无图）
def has_fig(q): 
    q=q or ""
    return any(k in q for k in ["如图","图所示","图略","见图","下图","上图"])
print(f"\nboth_wrong 中题干提及图(可能无图不可解): {sum(1 for r in both_wrong if has_fig(r.get('question')))}")

# 引擎帮倒忙的精确分布
hurts = [r for r in rows if r.get("pureLlmCorrect") and not r.get("gatewayCorrect") and r.get("gatewayUsedEngine")]
print(f"\n引擎帮倒忙 {len(hurts)} 题，按taskType:", collections.Counter(r.get("taskType") for r in hurts))

# 网关答对但纯LLM答错（引擎贡献）
gw_only = [r for r in rows if r.get("gatewayCorrect") and not r.get("pureLlmCorrect")]
print(f"网关独有正确 {len(gw_only)} 题，按taskType:", collections.Counter(r.get("taskType") for r in gw_only))
