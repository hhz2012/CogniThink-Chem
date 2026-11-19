# -*- coding: utf-8 -*-
"""用 0-1000 归一化坐标重新裁剪验证。"""
import os
from PIL import Image

RENDER = r"d:\github\CogniThink-Chem\pdfs\_extract\page_renders"
page = 7
img = Image.open(os.path.join(RENDER, f"page_{page:03d}.png"))
W, H = img.size
print(f"page size: {W}x{H}")

def norm(b):
    x1,y1,x2,y2 = b
    return [int(x1/1000*W), int(y1/1000*H), int(x2/1000*W), int(y2/1000*H)]

crops = [
    ("n3", "物质转化流程图", [90,492,681,621]),
    ("n4", "价类二维图",     [320,679,675,779]),
    ("n5", "物质转化关系图", [408,880,595,903]),
]
out = os.path.join(RENDER, "crops_norm")
os.makedirs(out, exist_ok=True)
for tag, desc, b in crops:
    x1,y1,x2,y2 = norm(b)
    pad = 10
    x1=max(0,x1-pad); y1=max(0,y1-pad); x2=min(W,x2+pad); y2=min(H,y2+pad)
    c = img.crop((x1,y1,x2,y2))
    fn = f"p{page}_{tag}_norm.png"
    c.save(os.path.join(out, fn))
    print(f"{fn} actual_bbox=[{x1},{y1},{x2},{y2}] size={c.size} desc={desc}")
print("done")
