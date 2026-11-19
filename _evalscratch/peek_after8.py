import json
ids={"pdf-022","pdf-023","pdf-243","pdf-259","pdf-257","pdf-273"}
with open(r"D:\github\CogniThink-Chem\_evalscratch\after8.json",encoding="utf-8-sig") as f:
    data=json.load(f)
for key in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    for c in data.get(key,[]):
        if c.get("id") in ids:
            print("="*70)
            print(f"[{c['id']}] task={c.get('taskType')} gwCorrect={c.get('gatewayCorrect')} pureCorrect={c.get('pureLlmCorrect')} 介入={c.get('gatewayUsedEngine')}")
            print("expected:", repr(c.get("expected","")))
            # dump all keys to see answerKeywords etc.
            for k in sorted(c.keys()):
                v=c[k]
                if isinstance(v,str) and len(v)>200: v=v[:200]+"..."
                print(f"  {k}: {repr(v)}")
