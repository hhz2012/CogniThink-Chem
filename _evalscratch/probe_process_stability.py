# -*- coding: utf-8 -*-
import json, urllib.request

def process(text):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request("http://localhost:5080/api/gateway/process", data=body,
                                  headers={"Content-Type":"application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))

q = "Fe 和稀硫酸反应生成 Fe2(SO4)3 对吗？"
print("=== reaction-5 /process x3 ===")
for i in range(3):
    try:
        d = process(q)
        er = d.get("engineRequest") or {}
        resp = d.get("engineResponse") or {}
        print(f"--- call {i} ---")
        print(f"  taskType={er.get('taskType')}  askDir={er.get('askDir')}")
        print(f"  rawText={str(er.get('rawText'))[:90]}")
        print(f"  studentAnswer={er.get('studentAnswer')!r}")
        rxn = er.get("reaction") or {}
        print(f"  reaction L={rxn.get('left')} R={rxn.get('right')}")
        print(f"  engine.ok={resp.get('ok')}  finalResult={str(resp.get('finalResult'))[:120]}")
        print(f"  reply={str(d.get('reply'))[:120]}")
    except Exception as e:
        print(f"--- call {i} ERR {e}")
