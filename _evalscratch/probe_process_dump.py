# -*- coding: utf-8 -*-
import json, urllib.request
def process(text):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request("http://localhost:5080/api/gateway/process", data=body,
                                  headers={"Content-Type":"application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))

q = "Fe 和稀硫酸反应生成 Fe2(SO4)3 对吗？"
d = process(q)
print("TOP KEYS:", list(d.keys()))
print("taskType:", d.get("taskType"))
er = d.get("engineResponse") or {}
print("engineResponse keys:", list(er.keys()))
print("engineResponse ok:", er.get("ok"))
print("engineResponse finalResult:", er.get("finalResult"))
print("engineResponse constraintNote:", str(er.get("constraintNote"))[:80])
eq = d.get("engineRequest") or {}
print("engineRequest keys:", list(eq.keys()))
print("engineRequest taskType:", eq.get("taskType"), "askDir:", eq.get("askDir"))
print("engineRequest rawText:", str(eq.get("rawText"))[:80])
print("engineRequest studentAnswer:", repr(eq.get("studentAnswer")))
print("reply:", str(d.get("reply"))[:200])
