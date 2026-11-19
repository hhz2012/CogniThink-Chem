# -*- coding: utf-8 -*-
"""删除 pdf_images 中未被 manifest 引用的图片（误报裁剪、多余项），保持目录干净。"""
import json, os

ROOT = r"d:\github\CogniThink-Chem"
MANIFEST = os.path.join(ROOT, "pdfs", "_extract", "image_manifest.json")
IMG_DIR = os.path.join(ROOT, "src", "CogniThink.TutorBrain.Api", "data", "pdf_images")

m = json.load(open(MANIFEST, encoding="utf-8"))
referenced = set()
for files in m["manifest"].values():
    for f in files:
        referenced.add(os.path.basename(f))

deleted = 0; kept = 0
for fn in os.listdir(IMG_DIR):
    if not fn.lower().endswith(".png"): continue
    if fn in referenced:
        kept += 1
    else:
        os.remove(os.path.join(IMG_DIR, fn))
        deleted += 1
print(f"kept={kept}, deleted={deleted}")
print(f"referenced files in manifest: {len(referenced)}")
