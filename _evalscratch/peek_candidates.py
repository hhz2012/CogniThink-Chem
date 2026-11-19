import json, sys
ids = {"pdf-022","pdf-023","pdf-243","pdf-259","pdf-273","pdf-278"}
with open(r"D:\github\CogniThink-Chem\_evalscratch\after7.json", encoding="utf-8-sig") as f:
    data = json.load(f)
for key in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    for c in data.get(key, []):
        if c.get("id") in ids:
            print("="*70)
            print(f"[{c.get('id')}] task={c.get('taskType')} 介入={c.get('gatewayUsedEngine')} 网关对={c.get('gatewayCorrect')} 纯LLM对={c.get('pureLlmCorrect')}")
            print("Q:", c.get("question",""))
            print("expected:", c.get("expected",""))
            print("note:", c.get("note",""))
            print("conclusion:", repr(c.get("gatewayConclusion","")))
