# -*- coding: utf-8 -*-
"""运行 analyze_baseline.py 的逻辑，但指向 after.json。"""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import analyze_baseline as a

a.PATH = os.path.join(HERE, "after.json")
data = json.load(open(a.PATH, encoding="utf-8-sig"))
print("JSON valid. top keys:", list(data.keys()))
print()
a.main()
