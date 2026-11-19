# -*- coding: utf-8 -*-
import json, urllib.request
def process(text):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request("http://localhost:5080/api/gateway/process", data=body,
                                  headers={"Content-Type":"application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))

for q in ["Fe 和稀硫酸反应生成 Fe2(SO4)3 对吗？",
          "Fe 和稀盐酸反应生成 FeCl3 对吗？",
          "Fe 和稀 H2SO4 反应生成 Fe2(SO4)3 对吗？"]:
    print("="*70); print("Q:", q)
    for i in range(3):
        try:
            d = process(q)
            er = d.get("engine_response") or {}
            eq = d.get("engine_request") or {}
            rxn = eq.get("reaction") or {}
            print(f"  [{i}] task_type={eq.get('task_type')} ask_dir={eq.get('ask_dir')} "
                  f"student_answer={eq.get('student_answer')!r} raw_text={str(eq.get('raw_text'))[:50]!r}")
            print(f"       reaction L={rxn.get('left')} R={rxn.get('right')}")
            print(f"       eng.ok={er.get('ok')} final={str(er.get('final_result'))[:90]}")
        except Exception as e:
            print(f"  [{i}] ERR {e}")
