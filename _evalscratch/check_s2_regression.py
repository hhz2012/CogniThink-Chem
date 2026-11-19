# -*- coding: utf-8 -*-
import json
SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
def load(p):
    with open(p, encoding="utf-8-sig") as f: return json.load(f)
parts = ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]
def idx(d):
    m={}
    for p in parts:
        for c in d.get(p,[]): m[c["id"]]=c
    return m
s2=idx(load(SCRATCH+r"\after_stage2d.json"))
s3b=idx(load(SCRATCH+r"\after_stage3b.json"))
s2_correct=set(i for i,c in s2.items() if c.get("gatewayCorrect"))
s3b_correct=set(i for i,c in s3b.items() if c.get("gatewayCorrect"))
lost=sorted(s2_correct - s3b_correct)
print(f"stage2d gatewayCorrect = {len(s2_correct)}")
print(f"stage3b gatewayCorrect = {len(s3b_correct)}")
print(f"stage2d->stage3b LOST (必须=0) = {len(lost)}  {lost}")
# 分类型
from collections import Counter
print("stage2d 正确分布:", dict(Counter(s2[i].get('taskType') for i in s2_correct)))
print("stage3b 正确分布:", dict(Counter(s3b[i].get('taskType') for i in s3b_correct)))
