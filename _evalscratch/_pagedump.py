# -*- coding: utf-8 -*-
"""Dump full content of specific exercise/answer pages to verify mappings."""
import json, glob, os, sys

def load_page(d, pi):
    for f in sorted(glob.glob(os.path.join(d, "page_*.json"))):
        j = json.load(open(f, encoding="utf-8"))
        if j.get("page_idx") == pi:
            return j
    return None

pages = sys.argv[1:]  # e.g. ex38 an43 an44
for spec in pages:
    kind = spec[:2]; pi = int(spec[2:])
    d = r"pdfs/_extract/exercise" if kind == "ex" else r"pdfs/_extract/answer"
    key = "questions" if kind == "ex" else "answers"
    j = load_page(d, pi)
    print(f"\n########## {spec} (page {pi}) ##########")
    if not j:
        print("  NOT FOUND"); continue
    for it in j.get(key, []) or []:
        print(f"  --- num={it.get('num')} type={it.get('type')!r} ---")
        for k, v in it.items():
            if k == "num": continue
            s = json.dumps(v, ensure_ascii=False) if not isinstance(v, str) else v
            print(f"    {k}: {s[:900]}")
