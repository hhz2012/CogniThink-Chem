# -*- coding: utf-8 -*-
import json, urllib.request, time
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
with open(SCRATCH + r"\after_stage3b.json", encoding="utf-8-sig") as f:
    data = json.load(f)
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
m={}
for p in parts:
    for c in data.get(p,[]): m[c["id"]]=c

def process(text):
    body = json.dumps({"text": text}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request("http://localhost:5080/api/gateway/process", data=body,
                                  headers={"Content-Type":"application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))

PLACEHOLDERS = ["已解析出相关物质与离子","已匹配到相关反应","已识别"]

targets = {
 "rc": ["reaction-1","reaction-2","reaction-3","reaction-4","reaction-5","reaction-6",
        "trap-react-1","trap-react-2","trap-react-3"],
 "choice": ["pdf-041","pdf-042","pdf-049","pdf-059","pdf-110"],
}
print("=== reaction_check（期望全部给出真实判定，无占位）===")
for qid in targets["rc"]:
    c=m[qid]
    try:
        d=process(c["question"])
        er=d.get("engine_response") or {}
        fin=str(er.get("final_result") or "")
        is_placeholder = any(p in fin for p in PLACEHOLDERS) or fin.strip()==""
        flag = "PLACEHOLDER!" if is_placeholder else "OK"
        print(f"  {qid:14} exp={str(c.get('expected'))[:14]:14} [{flag}] ok={er.get('ok')} final={fin[:70]}")
    except Exception as e:
        print(f"  {qid:14} ERR {e}")

print("\n=== choice（pdf-110 必须 ok!=None 且命中 C；其余 4 道不能回归）===")
for qid in targets["choice"]:
    c=m[qid]
    try:
        d=process(c["question"])
        er=d.get("engine_response") or {}
        eq=d.get("engine_request") or {}
        fin=str(er.get("final_result") or "")
        print(f"  {qid:10} exp={str(c.get('expected'))[:22]:22} task={eq.get('task_type')} ok={er.get('ok')} final={fin[:60]}")
    except Exception as e:
        print(f"  {qid:10} ERR {e}")
