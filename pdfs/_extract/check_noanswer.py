import json
data = json.load(open(r"d:\github\CogniThink-Chem\pdfs\questions_parsed.json", encoding="utf-8"))
no_ans = [r for r in data if not r.get("answer_keywords")]
print(f"total: {len(data)}, no-answer: {len(no_ans)}")
types = {}
for r in no_ans:
    types[r["type"]] = types.get(r["type"], 0) + 1
print("no-answer by type:", types)
print("\n--- first 40 no-answer questions ---")
for r in no_ans[:40]:
    print(f"  [{r['id']}] p{r['src_page']} n{r['num']} ({r['type']}): {r['question'][:90]}")
