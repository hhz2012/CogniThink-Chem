import json
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
def load(p): return json.load(open(HERE+"\\"+p,encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
a={r["id"]:r for r in cases(load("after8.json"))}
b={r["id"]:r for r in cases(load("after8b.json"))}
flipped=["pdf-283","pdf-337","pdf-355","pdf-015","pdf-045"]
for q in flipped:
    ra=a.get(q,{}); rb=b.get(q,{})
    print("="*60)
    print(q)
    print(f"  after8 : gw={ra.get('gatewayCorrect')} task={ra.get('taskType')} 介入={ra.get('gatewayUsedEngine')} ok={ra.get('gatewayOk')}")
    print(f"          gwAns={(ra.get('gatewayAnswer') or '')[:120]!r}")
    print(f"  after8b: gw={rb.get('gatewayCorrect')} task={rb.get('taskType')} 介入={rb.get('gatewayUsedEngine')} ok={rb.get('gatewayOk')}")
    print(f"          gwAns={(rb.get('gatewayAnswer') or '')[:120]!r}")
    print(f"  纯LLM after8={(ra.get('pureLlmAnswer') or '')[:80]!r} 对={ra.get('pureLlmCorrect')}")
    print(f"  纯LLM after8b={(rb.get('pureLlmAnswer') or '')[:80]!r} 对={rb.get('pureLlmCorrect')}")
    print(f"  expected after8={(ra.get('expected') or '')!r}")
    print(f"  expected after8b={(rb.get('expected') or '')!r}")
