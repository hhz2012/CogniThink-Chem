# -*- coding: utf-8 -*-
import csv
from collections import Counter

TSV = r"D:\github\CogniThink-Chem\_evalscratch\probe_parse.tsv"
rows = []
with open(TSV, encoding="utf-8") as f:
    rdr = csv.DictReader(f, delimiter="\t")
    for r in rdr:
        rows.append(r)

print(f"total eq options probed: {len(rows)}")
ok = [r for r in rows if r["parseOk"] == "True"]
fail = [r for r in rows if r["parseOk"] != "True"]
print(f"parseOk={len(ok)}  parseFail={len(fail)}")
bal = [r for r in ok if r["balanced"] == "True"]
unbal = [r for r in ok if r["balanced"] != "True"]
print(f"  of ok: balanced={len(bal)}  unbalanced={len(unbal)}")

# error category
ctr = Counter()
for r in fail:
    e = r["error"]
    if "电荷写法有歧义" in e: ctr["电荷歧义(需^)"] += 1
    elif "必须包含一个等号" in e: ctr["无等号/分隔符"] += 1
    elif "一侧为空" in e: ctr["一侧为空"] += 1
    elif "数字位置非法" in e: ctr["数字位置非法"] += 1
    elif "无法解析的字符" in e: ctr["无法解析字符"] += 1
    elif "括号不匹配" in e: ctr["括号不匹配"] += 1
    elif "缺少化学式主体" in e: ctr["缺少主体"] += 1
    elif e.startswith("EX:"): ctr["异常"] += 1
    else: ctr[e[:40]] += 1
print("\n=== parseFail error categories ===")
for k, v in ctr.most_common():
    print(f"  {k}: {v}")

print("\n=== 关键题目归一化 + 解析结果 ===")
targets = ["pdf-040", "pdf-041", "pdf-042", "pdf-043", "pdf-046", "pdf-049", "pdf-059", "pdf-102", "pdf-110", "pdf-092"]
for r in rows:
    if r["id"] in targets:
        tag = "OK-bal" if (r["parseOk"]=="True" and r["balanced"]=="True") else ("OK-unbal" if r["parseOk"]=="True" else "FAIL")
        print(f"[{r['id']} {r['letter']}] {tag}")
        print(f"   norm: {r['normEq']}")
        if r["parseOk"] != "True":
            print(f"   err : {r['error']}")
        else:
            print(f"   pars: {r['parsedStr']}")
