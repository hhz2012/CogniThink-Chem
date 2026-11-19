# -*- coding: utf-8 -*-
import json, urllib.request

URL = "http://localhost:5080/api/gateway/process"

def probe(text, label):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(URL, data=body, headers={"Content-Type": "application/json; charset=utf-8"})
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            data = json.loads(r.read().decode("utf-8"))
    except Exception as e:
        print(f"[{label}] ERROR: {e}")
        return
    tt = data.get("task_type")
    er = data.get("engine_response") or {}
    ok = er.get("ok")
    fr = (er.get("final_result") or "")[:120]
    used = data.get("gateway_used_engine")
    adopted = data.get("gateway_truth_adopted")
    reply = (data.get("reply") or "")[:80]
    print(f"[{label}]")
    print(f"  task_type={tt}  ok={ok}  used={used}  adopted={adopted}")
    print(f"  final_result={fr!r}")
    print(f"  reply={reply!r}")
    print()

# 8 个可判定方程（pdf-051 的 5 个 + 468/471/472），用"判断正误"问法触发判定
eqs = [
    ("pdf051-1", "判断下列离子方程式书写的正误：2Br- + Cl2 = Br2 + 2Cl-"),
    ("pdf051-2", "判断下列离子方程式书写的正误：6Fe2+ + Cr2O7^2- + 14H+ = 6Fe3+ + 2Cr3+ + 7H2O"),
    ("pdf051-3", "判断下列离子方程式书写的正误：H2SO3 + Ba2+ = BaSO3↓ + 2H+"),
    ("pdf051-4", "判断下列离子方程式书写的正误：2HCO3- + Cl2 = 2CO2 + Cl- + ClO- + H2O"),
    ("pdf051-5", "判断下列离子方程式书写的正误：[Cu(NH3)2]^2+ + CO + NH3 ⇌ [Cu(NH3)3CO]^2+"),
    ("pdf468",   "判断下列离子方程式书写的正误：SiO2 + 4H+ + 4F- === SiF4↑ + 2H2O"),
    ("pdf471",   "判断下列化学方程式书写的正误：SiO2 + 2OH- === SiO3^2- + H2O"),
    ("pdf472",   "判断下列化学方程式书写的正误：SiO2 + Na2CO3 --(高温)--> Na2SiO3 + CO2↑"),
]
for label, q in eqs:
    probe(q, label)

# pdf-051 整题（当前聚合行为）
probe("(2024·北京卷)判断下列离子方程式书写的正误,正确的填“√”,错误的填“×”。(1)海水提溴过程中,用氯气氧化苦卤得到溴单质:2Br-+Cl2=Br2+2Cl-( )\n(2)用绿矾将酸性工业废水中的Cr2O7^2-转化为Cr3+:6Fe2++Cr2O7^2-+14H+=6Fe3++2Cr3++7H2O( )\n(3)H2SO3溶液中滴加Ba(NO3)2溶液:H2SO3+Ba2+=BaSO3↓+2H+( )\n(4)NaHCO3溶液中通入少量Cl2:2HCO3-+Cl2=2CO2+Cl-+ClO-+H2O( )\n(5)亚铜氨溶液除去合成氨原料气中的CO:[Cu(NH3)2]^2++CO+NH3⇌[Cu(NH3)3CO]^2+( )", "pdf051-full")
