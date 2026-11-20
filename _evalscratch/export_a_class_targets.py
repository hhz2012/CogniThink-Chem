# -*- coding: utf-8 -*-
"""导出 A 类工程目标子集（引擎调用了但没解出，有 LLM 参照答案）。
排除 concept/unknown（设计上不走引擎，本轮搁置）。
复用 gen_unsolved_report.classify 打知识点标签。"""
import json, importlib.util, sys
from collections import Counter, defaultdict

EVAL = r"d:\github\CogniThink-Chem\_evalscratch"
SRC = EVAL + r"\after_tier2.json"

# 复用 classify
spec = importlib.util.spec_from_file_location("gur", EVAL + r"\gen_unsolved_report.py")
gur = importlib.util.module_from_spec(spec)
# 阻止它执行写文件：gen_unsolved_report 顶层就会跑生成逻辑。改为只抽取函数源码执行。
import re
src = open(EVAL + r"\gen_unsolved_report.py", encoding="utf-8").read()
# 截取 TOPIC_RULES 定义到 classify 函数结束
start = src.index("TOPIC_RULES = [")
end = src.index("unsolved = [")
ns = {"re": re, "Counter": Counter, "defaultdict": defaultdict}
exec(src[start:end], ns)
classify = ns["classify"]
TOPIC_RULES = ns["TOPIC_RULES"]

d = json.load(open(SRC, encoding="utf-8"))
def all_cases():
    for n in ["bothCorrectCases", "gatewayOnlyCorrectCases",
              "pureLlmOnlyCorrectCases", "bothWrongCases"]:
        for c in d[n]:
            yield c

EXCLUDE = {"concept", "unknown"}

def is_A(c):
    return (not c.get("gatewayCorrect")) and c.get("pureLlmCorrect")

def is_B(c):
    # 引擎跑了但未采纳：gatewayUsedEngine=true 且 gatewayTruthAdopted=false 且 gatewayCorrect=false
    return (not c.get("gatewayCorrect")) and c.get("gatewayUsedEngine") and (not c.get("gatewayTruthAdopted"))

def is_C(c):
    return (not c.get("gatewayCorrect")) and (not c.get("pureLlmCorrect"))

# 主目标集：A 类，排除 concept/unknown
targets = []
for c in all_cases():
    if is_A(c) and c.get("taskType") not in EXCLUDE:
        targets.append({
            "id": c["id"],
            "taskType": c.get("taskType"),
            "topic": classify(c),
            "question": c.get("question", ""),
            "expected": c.get("expected", ""),
            "llmReference": c.get("pureLlmAnswer", ""),   # 已判对的 LLM 答案，作验证参照
            "note": c.get("note", ""),
        })

out = EVAL + r"\a_class_engine_targets.json"
json.dump(targets, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"导出主目标集（A类，排除 concept/unknown）：{len(targets)} 题 -> {out}")

# 聚类：topic × taskType
print("\n=== 聚类：知识点 × taskType（主目标集）===")
cross = Counter((t["topic"], t["taskType"]) for t in targets)
topic_totals = Counter(t["topic"] for t in targets)
for topic, _ in topic_totals.most_common():
    print(f"\n【{topic}】合计 {topic_totals[topic]}")
    sub = sorted([(k[1], v) for k, v in cross.items() if k[0] == topic], key=lambda x: -x[1])
    for tt, n in sub:
        print(f"   {tt:18} {n}")

# 附带：B 类和 C 类的数量与分布（全景参考，不在本轮主目标）
B = [c for c in all_cases() if is_B(c)]
C = [c for c in all_cases() if is_C(c)]
print(f"\n=== 全景参考（本轮主目标=A类排除concept/unknown={len(targets)}）===")
print(f"B 类（引擎跑了未采纳，需修坏键）：{len(B)} 题，taskType:", Counter(c.get('taskType') for c in B))
print(f"C 类（LLM也答错，难题）：{len(C)} 题，taskType:", Counter(c.get('taskType') for c in C))
print(f"搁置（concept/unknown）：A类中 {sum(1 for c in all_cases() if is_A(c) and c.get('taskType') in EXCLUDE)} 题")
