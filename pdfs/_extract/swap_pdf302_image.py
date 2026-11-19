# -*- coding: utf-8 -*-
"""把 pdf-302 的配图从错图 p105_n08_4.png 换成正确的 p106_n08_battery.png。"""
import json, glob, os

ROOT = r"d:\github\CogniThink-Chem"
OLD = "pdf_images/p105_n08_4.png"
NEW = "pdf_images/p106_n08_battery.png"
FILES = [
    os.path.join(ROOT, r"src\CogniThink.TutorBrain.Api\data\pdf_questions.json"),
    os.path.join(ROOT, r"pdfs\questions_parsed.json"),
]
MANIFEST = os.path.join(ROOT, r"pdfs\_extract\image_manifest.json")

for path in FILES:
    d = json.load(open(path, encoding="utf-8"))
    items = d if isinstance(d, list) else list(d.values())
    changed = 0
    for it in items:
        imgs = it.get("images")
        if imgs and OLD in imgs:
            it["images"] = [NEW if x == OLD else x for x in imgs]
            changed += 1
    json.dump(d, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"{os.path.basename(path)}: updated {changed} item(s)")

# 同步 manifest（报告产物，保持一致）
if os.path.exists(MANIFEST):
    m = json.load(open(MANIFEST, encoding="utf-8"))
    man = m.get("manifest", {})
    if "pdf-302" in man and OLD in man["pdf-302"]:
        man["pdf-302"] = [NEW if x == OLD else x for x in man["pdf-302"]]
        json.dump(m, open(MANIFEST, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        print("manifest updated")

# 检查旧图是否还被任何 JSON 引用
referenced = False
for path in FILES + [MANIFEST]:
    txt = open(path, encoding="utf-8").read()
    if OLD in txt:
        referenced = True
        print("STILL referenced in:", os.path.basename(path))
print("old image still referenced anywhere:", referenced)
