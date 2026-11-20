# -*- coding: utf-8 -*-
"""Refined token search for remaining real-choice answers."""
import json, glob
AN = []
for f in sorted(glob.glob(r"pdfs/_extract/answer/page_*.json")):
    j = json.load(open(f, encoding="utf-8"))
    pi = j.get("page_idx")
    for a in j.get("answers", []) or []:
        if isinstance(a, dict): a["_page"] = pi; AN.append(a)

queries = {
 "pdf-090(NA)": ["2.3 g", "0.2NA", "11.2", "黑吉辽蒙", "22.4 L含原子"],
 "pdf-136(处理方法)": ["波尔多液", "活性炭", "漂白草帽", "苏打去油污"],
 "pdf-138(H2S装置)": ["FeS", "H2S 尾气", "制备H2S", "干燥H2S", "尾气处理H2S"],
 "pdf-142(H2S+X)": ["CuS↓", "2Fe3+", "FeCl3", "H2S至过量", "通入H2S"],
 "pdf-143(SO2装置)": ["红色花瓣", "Br2的CCl4", "验证SO2", "SO2的性质"],
 "pdf-145(Na2S2O3)": ["品红", "1:2", "氧化剂与还原剂", "Na2S2O3与稀H2SO4"],
}
for name, toks in queries.items():
    print(f"\n===== {name} =====")
    rows = []
    for a in AN:
        blob = (a.get("explanation") or "") + " " + str(a.get("answer") or "")
        h = [t for t in toks if t in blob]
        if h: rows.append((len(h), h, a))
    rows.sort(key=lambda r: -r[0])
    for s, h, a in rows[:4]:
        print(f"  [hits={s} {h}] page={a['_page']} num={a.get('num')} ans={str(a.get('answer'))!r}")
        print(f"      {str(a.get('explanation'))[:300]!r}")
    if not rows: print("  (none)")
