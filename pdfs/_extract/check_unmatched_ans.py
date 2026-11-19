import json, glob, os
AN_DIR = r"d:\github\CogniThink-Chem\pdfs\_extract\answer"
report = json.load(open(r"d:\github\CogniThink-Chem\pdfs\_extract\align_report.json", encoding="utf-8"))
ua = report["unmatched_answer"]

files = sorted(glob.glob(os.path.join(AN_DIR, "page_*.json")))
ans = []
for f in files:
    try:
        j = json.load(open(f, encoding="utf-8"))
    except Exception:
        continue
    for a in j.get("answers", []):
        if isinstance(a, dict) and a.get("num") is not None:
            ans.append({"page": j["page_idx"], "num": a["num"],
                        "answer": str(a.get("answer","")).strip(),
                        "explanation": (a.get("explanation") or "")[:120]})
print(f"unmatched answer count: {len(ua)}")
for idx in ua[:54]:
    a = ans[idx]
    print(f"  an[{idx}] p{a['page']} n{a['num']} ans={a['answer'][:30]!r} | {a['explanation'][:60]}")
