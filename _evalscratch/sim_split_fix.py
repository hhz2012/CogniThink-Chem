import json, re

PH1='\u0001'
PH2='\u0002'

def split_kw(answer, type_, existing):
    if existing and len(existing)>1: return existing
    if type_ and type_.lower()=="choice": return existing or []
    raw=(answer or "").strip()
    if not raw: return existing or []
    stripped=re.sub(r"[（(]\s*\d+\s*[）)]\s*"," ",raw)
    protected=re.sub(r"(?<![.\d])(\d)\s*[,，]\s*(\d)", lambda m: m.group(1)+PH1+m.group(2), stripped)
    ws=re.sub(r"\s*[\r\n]+\s*", PH2, protected)
    ws=re.sub(r"[ \t]{2,}", PH2, ws)
    parts=re.split(r"[,，;；、"+PH2+"]", ws)
    kws=[]
    for p in parts:
        t=p.strip().strip('.。').strip().replace(PH1,',')
        if t: kws.append(t)
    return kws or (existing or [])

ids=["pdf-022","pdf-023","pdf-257","pdf-259","pdf-243","pdf-273"]
with open(r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8") as f:
    data=json.load(f)
for it in data:
    if it.get("id") in ids:
        kws=split_kw(it.get("answer"), it.get("type"), it.get("answerKeywords"))
        print(it["id"], "->", kws)
