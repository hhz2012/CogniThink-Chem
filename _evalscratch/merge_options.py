# -*- coding: utf-8 -*-
"""把 pdfs/questions_parsed.json 中的 options / answer 合并回 data/pdf_questions.json。
保留原 pdf_questions.json 的所有字段（camelCase），仅新增 options / answer。
"""
import json

SRC = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
BK = r"d:\github\CogniThink-Chem\pdfs\questions_parsed.json"

src = json.load(open(SRC, encoding="utf-8-sig"))
bk = json.load(open(BK, encoding="utf-8-sig"))
bk_map = {x["id"]: x for x in bk}

added_opt = 0
added_ans = 0
missing_in_bk = []
for item in src:
    bid = item.get("id")
    b = bk_map.get(bid)
    if b is None:
        missing_in_bk.append(bid)
        continue
    opts = b.get("options")
    if opts:
        item["options"] = opts
        added_opt += 1
    ans = b.get("answer")
    if ans:
        item["answer"] = ans
        added_ans += 1

# 校验：单字母答案
single_letter = sum(1 for x in src if x.get("type")=="choice" and isinstance(x.get("answer"),str) and len(x["answer"])==1 and x["answer"] in "ABCD")
with_opt = sum(1 for x in src if x.get("options"))

with open(SRC, "w", encoding="utf-8") as f:
    json.dump(src, f, ensure_ascii=False, indent=2)

print(f"src total: {len(src)}")
print(f"backup matched: {len(src)-len(missing_in_bk)}  missing: {len(missing_in_bk)} {missing_in_bk[:5]}")
print(f"added options: {added_opt}  (with options now: {with_opt})")
print(f"added answer: {added_ans}  (single-letter choice answers: {single_letter})")
