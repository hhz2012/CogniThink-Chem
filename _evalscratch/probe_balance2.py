# -*- coding: utf-8 -*-
import json, urllib.request
BASE = "http://localhost:5080"
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
with open(SCRATCH + r"\after_stage2d.json", encoding="utf-8-sig") as f:
    data = json.load(f)
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
case_by_id = {}
for p in parts:
    for c in data.get(p, []):
        case_by_id[c["id"]] = c

def post(text):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(BASE + "/api/gateway/process", data=body,
                                 headers={"Content-Type": "application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read().decode("utf-8"))

for cid in ["pdf-089", "pdf-164"]:
    c = case_by_id[cid]
    print(f"\n========== {cid} ==========")
    print("question:", c["question"][:260])
    print("expected:", (c.get("expected") or "")[:160])
    res = post(c["question"])
    er = res.get("engine_response") or {}
    print("task_type:", res.get("task_type"), "| top ok:", res.get("ok"))
    print("engine.ok:", er.get("ok"))
    print("engine.final_result:", repr(er.get("final_result"))[:300])
    print("engine.constraint_note:", repr(er.get("constraint_note"))[:160])
    print("reply:", repr(res.get("reply"))[:200])
