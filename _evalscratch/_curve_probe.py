# -*- coding: utf-8 -*-
import json, glob, os

files = [r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json",
         r"src/CogniThink.TutorBrain.Api/data/tier2_questions.json"]
items = []
for f in files:
    if os.path.exists(f):
        items += json.load(open(f, encoding="utf-8"))

curves = [x for x in items if any(a.get("type")=="curve" for a in x.get("imageAssets") or [])]
print("total curve-asset questions:", len(curves))

for x in curves[:6]:
    print("\n=====", x.get("id"), "type=", x.get("type"), "answer=", x.get("answer"))
    q = x.get("question","")
    print("Q:", q[:200].replace("\n"," | "))
    opts = x.get("options") or {}
    for k in sorted(opts): print(f"   {k}. {opts[k]}")
    for a in x.get("imageAssets") or []:
        if a.get("type")!="curve": continue
        p = a.get("payload") or {}
        print("  payload keys:", list(p.keys()))
        print("  xLabel=", p.get("xLabel"), "yLabel=", p.get("yLabel"),
              "xLabeled=", p.get("xAxisLabeled"), "yLabeled=", p.get("yAxisLabeled"))
        for s in p.get("series") or []:
            pts = s.get("points") or []
            xs = [pt[0] for pt in pts if isinstance(pt,list) and len(pt)>=2]
            ys = [pt[1] for pt in pts if isinstance(pt,list) and len(pt)>=2]
            print(f"    series name={s.get('name')!r} npts={len(pts)} x=[{min(xs) if xs else '?'}..{max(xs) if xs else '?'}] y=[{min(ys) if ys else '?'}..{max(ys) if ys else '?'}]")
