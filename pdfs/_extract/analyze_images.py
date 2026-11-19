# -*- coding: utf-8 -*-
"""分析 pdf 题库中引用图片的题目，按源页码分组，输出报告。"""
import json, os, re

ROOT = r"d:\github\CogniThink-Chem"
PARSED = os.path.join(ROOT, "pdfs", "questions_parsed.json")   # 完整版（含 src_page）
SLIM   = os.path.join(ROOT, "src", "CogniThink.TutorBrain.Api", "data", "pdf_questions.json")
OUT_DIR = os.path.join(ROOT, "pdfs", "_extract")
REPORT  = os.path.join(OUT_DIR, "image_report.json")

IMG_KW = ["图", "image", "img", "picture", "figure", "图示", "下图", "上图", "如图", "见图",
          "曲线", "坐标图", "装置", "流程图", "示意图", "结构式", "球棍", "比例模型"]

def has_img(s):
    if not s:
        return False
    low = s.lower()
    for kw in IMG_KW:
        if kw.lower() in low:
            return True
    return False

def snippet(s, n=120):
    s = re.sub(r"\s+", " ", s or "")
    return s[:n] + ("..." if len(s) > n else "")

def main():
    parsed = json.load(open(PARSED, encoding="utf-8"))
    slim = json.load(open(SLIM, encoding="utf-8"))
    slim_by_id = {r["id"]: r for r in slim}

    # 建立 id -> src_page, num
    page_of = {}
    for r in parsed:
        page_of[r["id"]] = (r.get("src_page"), r.get("num"))

    need = []
    for r in parsed:
        blob_parts = [r.get("question",""), r.get("note",""), r.get("answer","")]
        for v in (r.get("options") or {}).values():
            blob_parts.append(v)
        matched_fields = []
        for fname, val in [("question", r.get("question","")),
                           ("note", r.get("note","")),
                           ("answer", r.get("answer",""))]:
            if has_img(val):
                matched_fields.append((fname, snippet(val)))
        for k, v in (r.get("options") or {}).items():
            if has_img(v):
                matched_fields.append((f"options.{k}", snippet(v)))
        if matched_fields:
            sp, num = page_of.get(r["id"], (None, None))
            need.append({
                "id": r["id"],
                "type": r.get("type",""),
                "src_page": sp,
                "num": num,
                "matched": matched_fields,
            })

    # 按 src_page 分组
    by_page = {}
    no_page = []
    for it in need:
        sp = it["src_page"]
        if sp is None:
            no_page.append(it["id"])
        else:
            by_page.setdefault(int(sp), []).append(it)

    report = {
        "total_questions": len(parsed),
        "questions_with_image_ref": len(need),
        "pages_with_images": sorted(by_page.keys()),
        "page_count": len(by_page),
        "no_src_page_ids": no_page,
        "by_page": {str(k): v for k, v in sorted(by_page.items())},
    }
    with open(REPORT, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)

    print(f"total questions: {len(parsed)}")
    print(f"questions referencing image: {len(need)}")
    print(f"distinct source pages: {len(by_page)}")
    print(f"pages: {sorted(by_page.keys())}")
    print(f"no src_page mapping: {no_page}")
    # 每页打印
    for p in sorted(by_page.keys()):
        ids = [f"{it['id']}(num={it['num']})" for it in by_page[p]]
        print(f"  page {p:>3}: {len(by_page[p])}q -> {', '.join(ids)}")
    print(f"report saved: {REPORT}")

if __name__ == "__main__":
    main()
