# -*- coding: utf-8 -*-
import json, sys, time, urllib.request, urllib.error
OUT = r"d:\github\CogniThink-Chem\_evalscratch\after_tier2.json"
URL = "http://localhost:5090/api/evaluation/gateway-vs-llm"

# wait for server boot
for i in range(60):
    try:
        with urllib.request.urlopen(URL, timeout=5) as r:
            break
    except urllib.error.URLError as e:
        print(f"wait server... ({i}) {e}"); time.sleep(2)
    except Exception as e:
        print(f"wait... ({i}) {e}"); time.sleep(2)
else:
    print("server did not come up"); sys.exit(1)

print("fetching eval (may take a while if LLM online)...")
t0 = time.time()
try:
    with urllib.request.urlopen(URL, timeout=1500) as resp:
        raw = resp.read().decode("utf-8")
except Exception as e:
    print("fetch failed:", e); sys.exit(2)
obj = json.loads(raw)
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(obj, f, ensure_ascii=False, indent=1)
print("saved", OUT, "bytes", len(raw), "elapsed", round(time.time()-t0,1), "s")
for k in ["totalCases","gatewayCorrect","gatewayTruthCorrect","gatewayEngineUsed",
          "engineSolved","engineCoverage","enginePrecision","harmCount","llmAvailable"]:
    print(f"  {k} = {obj.get(k)}")
