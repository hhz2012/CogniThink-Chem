import json, urllib.request
with open(r"D:\github\CogniThink-Chem\_evalscratch\after7.json", encoding="utf-8-sig") as f:
    data = json.load(f)
targets = {"pdf-259","pdf-257"}
qmap = {}
for key in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
    for c in data.get(key, []):
        if c.get("id") in targets:
            qmap[c["id"]] = c["question"]
for cid in ["pdf-259","pdf-257"]:
    q = qmap[cid]
    body = json.dumps({"text": q}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request("http://localhost:5080/api/gateway/process", data=body,
        headers={"Content-Type":"application/json; charset=utf-8"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            d = json.loads(resp.read().decode("utf-8"))
        er = d.get("engine_response") or {}
        print("="*70)
        print(f"[{cid}] taskType={d.get('task_type')} engineOk={er.get('ok')}")
        print("FinalResult:", er.get("final_result"))
    except Exception as e:
        print(f"[{cid}] ERROR: {e}")
