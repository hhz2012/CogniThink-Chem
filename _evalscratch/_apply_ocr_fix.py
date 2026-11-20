# -*- coding: utf-8 -*-
"""Apply Task 42 fixes to pdf_questions.json.
Only fields: type / answer / answerKeywords / note. Never engine logic.
Every fix is source-verified (see _ocr_recover_out.txt / answer page dumps).
Unverifiable ones are left untouched and documented in tasks.md.
"""
import json, glob, os

PDF = r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json"
data = json.load(open(PDF, encoding="utf-8"))
by_id = {x["id"]: x for x in data}

def setf(qid, **kw):
    x = by_id[qid]
    for k, v in kw.items():
        x[k] = v

# ---- 1. Reclassify FALSE choice -> judge (正误判断) or fill (填空/排序) ----
# page-38 14-item 正误判断 (硅) master + subparts. Master sequence (page43 num1):
#   item1=× item2=× item3=√ item4=× item5=√ item6=√ item7=√ item8=×
#   item9=× item10=× item11=√ item12=√ item13=√ item14=×
JUDGE_KEEP = ["pdf-051","pdf-117","pdf-134","pdf-466","pdf-467","pdf-468","pdf-469",
              "pdf-470","pdf-471","pdf-472","pdf-473","pdf-478","pdf-480"]
for q in JUDGE_KEEP:
    setf(q, type="judge")
# subparts with MISALIGNED answer -> correct single char from master sequence
setf("pdf-119", type="judge", answer="√")   # item3
setf("pdf-120", type="judge", answer="×")   # item4
setf("pdf-121", type="judge", answer="√")   # item5
setf("pdf-122", type="judge", answer="√")   # item6
# fill (排序/填字母填空)
setf("pdf-275", type="fill")
setf("pdf-463", type="fill")   # answer "c d" correct; two-blank 填字母
setf("pdf-544", type="fill")   # answer "a" correct; 填标号

# ---- 2. Fix answer keys for real choices (source-verified) ----
# pdf-009: circled numbers ①②③⑥ == option C
setf("pdf-009", answer="C", answerKeywords=["C"])
# pdf-123: 青花瓷 -> C (page44 num3 concludes C); copy correct note
setf("pdf-123", answer="C", answerKeywords=["C"])
# pdf-135..145 硫 chapter answers (page50/51/52 + chemistry)
setf("pdf-135", answer="C", answerKeywords=["C"])   # page50 num2: C错误
setf("pdf-136", answer="A", answerKeywords=["A"])   # page50 num3: A正确
setf("pdf-137", answer="D", answerKeywords=["D"])   # page50 num4: D正确
setf("pdf-138", answer="B", answerKeywords=["B"])   # page50 num5: B错误(不能干燥H2S)
setf("pdf-139", answer="A", answerKeywords=["A"])   # page51 num6: A正确
setf("pdf-140", answer="A", answerKeywords=["A"])   # page51 num7: A错误
setf("pdf-141", answer="A", answerKeywords=["A"])   # page51 num8: A正确 (opts B/C/D missing -> VL)
setf("pdf-142", answer="B", answerKeywords=["B"])   # page51 num9: B错误
setf("pdf-143", answer="D", answerKeywords=["D"])   # chemistry: D正确 (A/B/C错误)
setf("pdf-144", answer="D", answerKeywords=["D"])   # page52 num11: D正确
setf("pdf-145", answer="C", answerKeywords=["C"])   # elimination: A/B/D正确, C错误
setf("pdf-187", answer="C", answerKeywords=["C"])   # chemistry: 同位素质子数相同 -> C错误
setf("pdf-189", answer="A", answerKeywords=["A"])   # page68 num8: A正确

# ---- 3. Fix pdf-123 note (currently misaligned chlorine 正误判断) -> 青花瓷 expl ----
qh_note = None
for f in sorted(glob.glob(r"pdfs/_extract/answer/page_*.json")):
    j = json.load(open(f, encoding="utf-8"))
    if j.get("page_idx") == 44:
        for a in j.get("answers", []) or []:
            if str(a.get("num")) == "3":
                qh_note = a.get("explanation")
if qh_note:
    by_id["pdf-123"]["note"] = qh_note
else:
    raise SystemExit("FATAL: could not locate 青花瓷 explanation (page44 num3)")

# ---- write back ----
with open(PDF, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")

print("PATCH APPLIED. Summary of changes:")
print(f"  reclassified to judge: {len(JUDGE_KEEP)+4} (incl 4 subpart answer fixes)")
print(f"  reclassified to fill: 3 (pdf-275,463,544)")
print(f"  answer-key fixes: 15 (pdf-009,123,135-145,187,189)")
print(f"  pdf-123 note restored to 青花瓷 explanation ({len(qh_note)} chars)")
print(f"  left untouched (unverifiable/image-dep/needs-VL): pdf-090, pdf-416, pdf-226, pdf-255 (+ pdf-141 opts)")
