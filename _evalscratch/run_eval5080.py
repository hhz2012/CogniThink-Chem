import json, sys, urllib.request, time

out = r"D:\github\CogniThink-Chem\_evalscratch\after_stage3c_rerun.json"
url = "http://localhost:5080/api/evaluation/gateway-vs-llm"
t0 = time.time()
req = urllib.request.Request(url)
with urllib.request.urlopen(req, timeout=1800) as resp:
    raw = resp.read().decode("utf-8")
obj = json.loads(raw)
with open(out, "w", encoding="utf-8") as f:
    json.dump(obj, f, ensure_ascii=False, indent=1)
print("saved", out, "bytes", len(raw), "elapsed", round(time.time() - t0, 1), "s")
for k in ["totalCases", "pureLlmCorrect", "gatewayCorrect", "gatewayTruthCorrect",
          "gatewayEngineUsed", "engineSolved", "enginePrecision", "harmCount",
          "bothCorrect", "gatewayOnlyCorrect", "pureLlmOnlyCorrect", "bothWrong"]:
    print(f"  {k} = {obj.get(k)}")
