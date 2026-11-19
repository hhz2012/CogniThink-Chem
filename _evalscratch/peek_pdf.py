# -*- coding: utf-8 -*-
import json
data=json.load(open(r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8"))
ids=["pdf-518","pdf-025","pdf-450","pdf-508","pdf-022","pdf-535","pdf-542","pdf-510","pdf-487"]
for x in data:
    if x.get("id") in ids:
        print("="*70)
        print("ID:",x.get("id"),"type:",x.get("type"),"answer:",repr(x.get("answer")))
        print("AnswerKeywords:",x.get("answerKeywords"))
        print("note:",(x.get("note") or "")[:80])
