# -*- coding: utf-8 -*-
"""将提取到的配图按 id 注入题库 JSON。
- pdf_questions.json（精简版，运行时加载）与 questions_parsed.json（完整版备份）都更新
- 仅对 manifest 中命中的题目追加 "images" 字段（相对路径列表）
- 幂等：重复运行会覆盖更新
"""
import json, os

ROOT = r"d:\github\CogniThink-Chem"
MANIFEST = os.path.join(ROOT, "pdfs", "_extract", "image_manifest.json")
SLIM = os.path.join(ROOT, "src", "CogniThink.TutorBrain.Api", "data", "pdf_questions.json")
FULL = os.path.join(ROOT, "pdfs", "questions_parsed.json")

def inject(path, manifest):
    data = json.load(open(path, encoding="utf-8"))
    n = 0
    for it in data:
        qid = it.get("id")
        if qid in manifest and manifest[qid]:
            it["images"] = manifest[qid]
            n += 1
        else:
            # 确保无残留（若此前注入过但本次无图，则移除）
            it.pop("images", None)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print(f"  {path}: injected {n}/{len(data)} questions with images")

def main():
    m = json.load(open(MANIFEST, encoding="utf-8"))
    manifest = m["manifest"]
    print(f"manifest: {len(manifest)} questions have images")
    missing = m.get("missing", [])
    print(f"missing (no figure extracted): {len(missing)}")
    if missing:
        print("  first 30 missing:", [x["id"] for x in missing[:30]])
    inject(SLIM, manifest)
    inject(FULL, manifest)
    print("done")

if __name__ == "__main__":
    main()
