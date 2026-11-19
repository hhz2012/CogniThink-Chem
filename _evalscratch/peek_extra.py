import json
ids = {"pdf-257","pdf-258","pdf-261","pdf-262"}
with open(r"D:\github\CogniThink-Chem\_evalscratch\after7.json", encoding="utf-8-sig") as f:
    data = json.load(f)
for key in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    for c in data.get(key, []):
        if c.get("id") in ids:
            print("="*70)
            print(f"[{c.get('id')}] task={c.get('taskType')} 介入={c.get('gatewayUsedEngine')}")
            print("Q:", c.get("question",""))
            print("expected:", c.get("expected",""))
