# -*- coding: utf-8 -*-
import json, urllib.request

def parse(text):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request("http://localhost:5080/api/gateway/parse", data=body,
                                  headers={"Content-Type":"application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=40) as r:
        return json.loads(r.read().decode("utf-8"))

def show(tag, d):
    rxn = d.get("reaction") or {}
    left = rxn.get("left") or []
    right = rxn.get("right") or []
    print(f"  [{tag}] taskType={d.get('taskType')} askDir={d.get('askDir')}")
    print(f"         left={left}")
    print(f"         right={right}")
    print(f"         studentAnswer={d.get('studentAnswer')}")

q = "Fe 和稀硫酸反应生成 Fe2(SO4)3 对吗？"
print("=== reaction-5 parse x3 ===")
for i in range(3):
    try:
        d = parse(q)
        show(i, d)
    except Exception as e:
        print(f"  [{i}] ERR {e}")
