# -*- coding: utf-8 -*-
import json, sys, urllib.request

BASE = "http://localhost:5080"
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"

def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)

def find_case(data, cid):
    for key in ("bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"):
        for c in data.get(key, []):
            if c.get("id") == cid:
                return c, key
    return None, None

def post(text):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(BASE + "/api/gateway/process", data=body,
                                 headers={"Content-Type": "application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read().decode("utf-8"))

data = load(SCRATCH + r"\after_stage2d.json")
c, key = find_case(data, "pdf-089")
print("=== pdf-089 baseline (stage2) ===")
print("partition:", key)
print("question:", repr(c["question"])[:300])
print("expected:", repr(c.get("expected"))[:200])
print("stage2 gatewayCorrect:", c.get("gatewayCorrect"), "taskType:", c.get("taskType"),
      "usedEngine:", c.get("gatewayUsedEngine"), "truthAdopted:", c.get("gatewayTruthAdopted"))
print("stage2 gatewayAnswer:", repr(c.get("gatewayAnswer"))[:200])

print("\n=== POST pdf-089 to gateway (stage3A runtime) ===")
res = post(c["question"])
print("task_type:", res.get("task_type"))
print("ok:", res.get("ok"))
print("final_result:", repr(res.get("final_result"))[:400])
print("reply:", repr(res.get("reply"))[:300])
er = res.get("engine_response") or {}
print("engine final:", repr(er.get("final_result"))[:400])

print("\n=== POST unsolvable balance (H2O = H2 + N2) ===")
res2 = post("配平下列化学方程式：H2O = H2 + N2")
print("task_type:", res2.get("task_type"))
print("ok:", res2.get("ok"))
print("final_result:", repr(res2.get("final_result"))[:300])
print("reply:", repr(res2.get("reply"))[:300])
