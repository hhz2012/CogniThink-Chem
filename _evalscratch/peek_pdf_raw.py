import json
ids={"pdf-022","pdf-023","pdf-257","pdf-259","pdf-243","pdf-273"}
with open(r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8") as f:
    data=json.load(f)
print("type:",type(data),"len:",len(data))
for it in data:
    if it.get("id") in ids:
        print("="*60)
        print("id:",it.get("id"),"type:",it.get("type"))
        print("answer:",repr(it.get("answer","")))
        print("answerKeywords:",repr(it.get("answerKeywords")))
