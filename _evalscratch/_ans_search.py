# -*- coding: utf-8 -*-
"""Search answer explanations by distinctive tokens to recover correct answers
for real-choice questions with misaligned answer keys."""
import json, glob, os

AN = []
for f in sorted(glob.glob(r"pdfs/_extract/answer/page_*.json")):
    j = json.load(open(f, encoding="utf-8"))
    pi = j.get("page_idx")
    for a in j.get("answers", []) or []:
        if isinstance(a, dict):
            a["_page"] = pi; AN.append(a)

queries = {
 "pdf-090(Na/NA)": ["Na2O2", "NaClO", "黑吉辽蒙"],
 "pdf-135(硫性质)": ["FeS2", "沸腾炉", "11 mol", "Cu2S"],
 "pdf-136(处理方法)": ["盐析", "漂白草帽", "饱和食盐水", "CuSO4 溶液"],
 "pdf-138(H2S装置)": ["H2S", "尾气处理", "制备 H2S", "干燥 H2S"],
 "pdf-139(硫价类图)": ["价-类", "二维图", "高锰酸钾", "品红"],
 "pdf-140(S+NaOH)": ["3S + 6NaOH", "S6", "S—S", "6NA"],
 "pdf-142(H2S+X)": ["2Fe³⁺ + 3H₂S", "FeCl₃溶液", "H₂S至过量", "CuS↓"],
 "pdf-143(SO2装置)": ["BaCl₂", "Br₂的CCl₄", "红色花瓣", "SO₂性质"],
 "pdf-145(Na2S2O3)": ["Na2S2O3", "氧化剂与还原剂", "1:2", "黄色沉淀"],
 "pdf-189(量子材料)": ["X射线衍射", "石墨烯", "第一电离能:C<Si", "锗"],
 "pdf-416(GaN工艺)": ["一系列操作", "蒸发至晶膜", "LED生产废料", "GaN"],
}

def hit(a, toks):
    blob = (a.get("explanation") or "") + " " + str(a.get("answer") or "")
    return sum(1 for t in toks if t in blob)

for name, toks in queries.items():
    print(f"\n===== {name}  (tokens: {toks}) =====")
    scored = [(hit(a, toks), a) for a in AN]
    scored.sort(key=lambda t: -t[0])
    shown = 0
    for s, a in scored:
        if s == 0: break
        print(f"  [hits={s}] page={a['_page']} num={a.get('num')} ans={str(a.get('answer'))!r}")
        print(f"      expl: {str(a.get('explanation'))[:260]!r}")
        shown += 1
        if shown >= 3: break
    if shown == 0:
        print("  (no matches)")
