import json
from collections import Counter
with open(r"D:\github\CogniThink-Chem\_evalscratch\after7.json", encoding="utf-8-sig") as f:
    data = json.load(f)
cnt = Counter()
correct_by_task = Counter()
for key in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    for c in data.get(key, []):
        tt = c.get("taskType")
        used = c.get("gatewayUsedEngine")
        if used:
            cnt[tt] += 1
            if c.get("gatewayCorrect"):
                correct_by_task[tt] += 1
print("介入 by taskType:", dict(cnt))
print("网关正确 by taskType(介入):", dict(correct_by_task))
print("calc+eq 介入合计:", cnt.get("calculation",0)+cnt.get("equilibrium",0))
print("总体 网关正确:", data.get("gatewayCorrect"), "纯LLM:", data.get("pureLlmCorrect"), "total:", data.get("totalCases"))
print("引擎介入总数:", data.get("gatewayEngineUsed"))
