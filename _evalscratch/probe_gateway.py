import json, urllib.request, re
BASE=r"D:\github\CogniThink-Chem\_evalscratch\\"
with open(BASE+"after_stage2.json",encoding="utf-8-sig") as f:
    data=json.load(f)
flat={}
for k,v in data.items():
    if isinstance(v,list):
        for c in v:
            if c.get("id") is not None: flat[c["id"]]=c

CHOICE_RE = re.compile(r"(^|\n)\s*[A-D][\.．、]\s")

def probe(cid):
    q=flat[cid]["question"]
    body=json.dumps({"text":q},ensure_ascii=False).encode("utf-8")
    req=urllib.request.Request("http://localhost:5080/api/gateway/process",
        data=body, headers={"Content-Type":"application/json; charset=utf-8"})
    try:
        with urllib.request.urlopen(req,timeout=120) as r:
            resp=json.loads(r.read().decode("utf-8"))
        return resp.get("task_type"), resp.get("ok"), (resp.get("reply") or "")[:40]
    except Exception as e:
        return "ERR", None, str(e)[:60]

ids=["pdf-054","pdf-055","pdf-351","pdf-361","pdf-331","pdf-360","pdf-387"]
print(f"{'id':10} {'py_match':9} {'eval_tt':14} {'live_tt':14} {'ok':6} reply_preview")
for cid in ids:
    pm = "BLOCK" if CHOICE_RE.search(flat[cid]["question"]) else "noblock"
    ett = flat[cid].get("taskType")
    ltt,ok,rp = probe(cid)
    print(f"{cid:10} {pm:9} {str(ett):14} {str(ltt):14} {str(ok):6} {rp}")
