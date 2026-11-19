# -*- coding: utf-8 -*-
"""验证 pdf-275/533 是否为空格导致的判分假阴性。"""
import json, os, re
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
d=json.load(open(os.path.join(HERE,"after8b.json"),encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
cs={r["id"]:r for r in cases(d)}
def norm(s):
    s=s or ""
    for a,b in [("₀","0"),("₁","1"),("₂","2"),("₃","3"),("₄","4"),("₅","5"),("₆","6"),("₇","7"),("₈","8"),("₉","9")]:
        s=s.replace(a,b)
    return s.lower()
def squeeze(s): return re.sub(r"\s+","",norm(s))
for q in ["pdf-275","pdf-533","pdf-274","pdf-531"]:
    r=cs.get(q)
    if not r: continue
    exp=r.get("expected","")
    body=exp[len("关键词:"):].strip()
    kws=[k.strip() for k in re.split(r"[,，;；、]", body) if k.strip()]
    ans=r.get("gatewayAnswer") or ""
    print(f"===== {q} task={r.get('taskType')} gwCorrect={r.get('gatewayCorrect')} =====")
    print("  expected kws:", kws)
    for k in kws:
        plain_hit = norm(k) in norm(ans)
        squeeze_hit = squeeze(k) in squeeze(ans)
        print(f"    kw={k!r:40s} 普通匹配={plain_hit}  去空格匹配={squeeze_hit}")
    print()
