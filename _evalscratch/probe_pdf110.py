# -*- coding: utf-8 -*-
import json, urllib.request
def process(text):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request("http://localhost:5080/api/gateway/process", data=body,
                                  headers={"Content-Type":"application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))

q = """(2025·云南卷·★★)下列化学方程式错误的是( )
A. 煤制水煤气:C+H2O(g)高温CO+H2
B. Na2O2供氧:2Na2O2+2CO2=2Na2CO3+O2
C. 覆铜板制作印刷电路板:2FeCl3+3Cu=2Fe+3CuCl2
D. 铅酸蓄电池放电:Pb+PbO2+2H2SO4=2PbSO4+2H2O"""
for i in range(2):
    d = process(q)
    er = d.get("engine_response") or {}
    eq = d.get("engine_request") or {}
    print(f"--- call {i} ---")
    print(f"  task_type={eq.get('task_type')} ask_dir={eq.get('ask_dir')}")
    print(f"  eng.ok={er.get('ok')} final={str(er.get('final_result'))[:120]}")
    print(f"  constraintNote={str(er.get('constraintNote'))[:120]}")
    print(f"  derivation={er.get('derivation_steps')}")
