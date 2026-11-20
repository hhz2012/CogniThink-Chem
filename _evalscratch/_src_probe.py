# -*- coding: utf-8 -*-
"""Backtrack source: search exercise/answer page JSONs by keyword to recover
misaligned Q/options/answer for the 39 broken pdf_questions.
Usage: python _src_probe.py <keyword>
"""
import json, glob, sys, re

def load(pat):
    out = []
    for p in sorted(glob.glob(pat)):
        try:
            d = json.load(open(p, encoding="utf-8"))
        except Exception as e:
            continue
        pi = d.get("page_idx")
        for q in d.get("questions", []) or []:
            out.append((pi, "Q", q))
        for a in d.get("answers", []) or []:
            out.append((pi, "A", a))
    return out

EX = load(r"pdfs/_extract/exercise/page_*.json")
AN = load(r"pdfs/_extract/answer/page_*.json")
print(f"exercise entries: {len(EX)}  answer entries: {len(AN)}")

def show_entry(e):
    pi, kind, q = e
    print(f"  --- page {pi} [{kind}] ---")
    if isinstance(q, dict):
        for k, v in q.items():
            s = json.dumps(v, ensure_ascii=False) if not isinstance(v, str) else v
            print(f"    {k}: {s[:500]}")
    else:
        print(f"    {str(q)[:500]}")

kw = sys.argv[1] if len(sys.argv) > 1 else ""
if not kw:
    # print keys of first few entries to understand schema
    for e in EX[:2]: show_entry(e)
    for e in AN[:2]: show_entry(e)
    sys.exit(0)

print(f"\n===== exercise matches for '{kw}' =====")
n = 0
for pi, kind, q in EX:
    blob = json.dumps(q, ensure_ascii=False)
    if kw in blob:
        show_entry((pi, kind, q)); n += 1
        if n >= 4: break
print(f"===== answer matches for '{kw}' =====")
n = 0
for pi, kind, q in AN:
    blob = json.dumps(q, ensure_ascii=False)
    if kw in blob:
        show_entry((pi, kind, q)); n += 1
        if n >= 6: break
