import json, re

PH1='\u0001'; PH2='\u0002'

def split_old(answer, type_, existing):
    if existing and len(existing)>1: return existing
    if type_ and type_.lower()=="choice": return existing or []
    raw=(answer or "").strip()
    if not raw: return existing or []
    stripped=re.sub(r"[（(]\s*\d+\s*[）)]\s*"," ",raw)
    protected=re.sub(r"(?<![.\d])(\d)\s*[,，]\s*(\d)", lambda m:m.group(1)+PH1+m.group(2), stripped)
    parts=re.split(r"[,，;；、]", protected)
    kws=[]
    for p in parts:
        t=p.strip().strip('.。').strip().replace(PH1,',')
        if t: kws.append(t)
    return kws or (existing or [])

def split_new(answer, type_, existing):
    if existing and len(existing)>1: return existing
    if type_ and type_.lower()=="choice": return existing or []
    raw=(answer or "").strip()
    if not raw: return existing or []
    stripped=re.sub(r"[（(]\s*\d+\s*[）)]\s*"," ",raw)
    protected=re.sub(r"(?<![.\d])(\d)\s*[,，]\s*(\d)", lambda m:m.group(1)+PH1+m.group(2), stripped)
    ws=re.sub(r"\s*[\r\n]+\s*", PH2, protected)
    ws=re.sub(r"[ \t]{2,}", PH2, ws)
    parts=re.split(r"[,，;；、"+PH2+"]", ws)
    kws=[]
    for p in parts:
        t=p.strip().strip('.。').strip().replace(PH1,',')
        if t: kws.append(t)
    return kws or (existing or [])

with open(r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8") as f:
    data=json.load(f)

more=0; same=0; fewer=0; weird=[]
for it in data:
    o=split_old(it.get("answer"),it.get("type"),it.get("answerKeywords"))
    n=split_new(it.get("answer"),it.get("type"),it.get("answerKeywords"))
    if len(n)>len(o): more+=1
    elif len(n)==len(o): same+=1
    else: fewer+=1; print("FEWER!",it.get("id"),o,n)
    for k in n:
        if not k.strip() or len(k.strip())==0:
            weird.append((it.get("id"),"empty",n))
# pdf-322 locant guard
for it in data:
    if it.get("id")=="pdf-322":
        print("pdf-322 old:",split_old(it.get("answer"),it.get("type"),it.get("answerKeywords")))
        print("pdf-322 new:",split_new(it.get("answer"),it.get("type"),it.get("answerKeywords")))
print(f"more={more} same={same} fewer={fewer} weird_empty={len(weird)}")
