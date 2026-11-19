# -*- coding: utf-8 -*-
"""真值源边界审计：量化网关判正中'纯 LLM 回退'的占比。"""
import json, io, sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

p = r"D:\github\CogniThink-Chem\_evalscratch\after8b.json"
with open(p, encoding="utf-8-sig") as f:
    data = json.load(f)

def norm(s):
    if s is None:
        return ""
    return "".join(str(s).split())

# 收集所有 case 记录（四个分区）
cases = []
for key in ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]:
    for c in data.get(key, []):
        c["_bucket"] = key
        cases.append(c)

print(f"总记录数: {len(cases)}")

# 先看一条记录的字段
print("\n=== 第一条记录字段 ===")
print(sorted(cases[0].keys()))

gw_correct = [c for c in cases if c.get("gatewayCorrect")]
print(f"\ngatewayCorrect 总数: {len(gw_correct)}")

# 统计 gatewayCorrect 的来源分解
no_engine = 0          # gatewayUsedEngine == False（concept/unknown/Ok=null）→ 靠 pureAnswer
used_but_answered_eq_pure = 0  # 引擎介入但 gatewayAnswer 文本 == pureAnswer → 回退 pureAnswer
real_engine = 0        # 引擎介入且回答 != pureAnswer → 真正用了引擎结论
no_pure_field = 0

for c in gw_correct:
    used = c.get("gatewayUsedEngine")
    gwa = c.get("gatewayAnswer") or ""
    pla = c.get("pureLlmAnswer") or ""
    if not used:
        no_engine += 1
    elif norm(gwa) == norm(pla) and pla:
        used_but_answered_eq_pure += 1
    else:
        real_engine += 1

print(f"\n=== gatewayCorrect 来源分解（去空白比较）===")
print(f"  ① 引擎未介入(gatewayUsedEngine=False)，靠纯LLM回退: {no_engine}")
print(f"  ② 引擎介入但回答==纯LLM回答(结论被弃用/回退): {used_but_answered_eq_pure}")
print(f"  ③ 引擎介入且回答≠纯LLM(真正用引擎真值): {real_engine}")
print(f"  ①+② = LLM真值作答(违反原则): {no_engine + used_but_answered_eq_pure}")

# 按 taskType 分解 ①+②
from collections import Counter
cnt = Counter()
for c in gw_correct:
    used = c.get("gatewayUsedEngine")
    gwa = c.get("gatewayAnswer") or ""
    pla = c.get("pureLlmAnswer") or ""
    if (not used) or (norm(gwa) == norm(pla) and pla):
        cnt[c.get("taskType") or "?"] += 1
print(f"\n=== 违反原则的判正 按 taskType ===")
for k, v in cnt.most_common():
    print(f"  {k}: {v}")

# 反过来：网关判错里，引擎真正介入且答错的（引擎能力问题，非 LLM 回退）
gw_wrong = [c for c in cases if not c.get("gatewayCorrect")]
engine_used_wrong = [c for c in gw_wrong if c.get("gatewayUsedEngine")]
print(f"\n=== 网关判错 {len(gw_wrong)} 中，引擎介入的: {len(engine_used_wrong)} ===")
# 其中回答==pureAnswer 的（引擎介入但被回退，实际是纯 LLM 答错）
eq_pure = sum(1 for c in engine_used_wrong if norm(c.get("gatewayAnswer") or "") == norm(c.get("pureLlmAnswer") or "") and (c.get("pureLlmAnswer") or ""))
print(f"  其中网关回答==纯LLM回答(引擎结论被弃用): {eq_pure}")
print(f"  真正用引擎结论但答错: {len(engine_used_wrong) - eq_pure}")
