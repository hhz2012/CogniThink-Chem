# -*- coding: utf-8 -*-
from collections import Counter
rows=[]
with open(r"D:\github\CogniThink-Chem\_evalscratch\choice_audit.tsv", encoding="utf-8") as f:
    hdr=f.readline().rstrip("\n").split("\t")
    for line in f:
        rows.append(dict(zip(hdr,line.rstrip("\n").split("\t"))))

print("sample rows (id, ans, askWrong, valid, invalid, unpars, decided):")
for r in rows[:8]:
    print("  ", {k:r[k] for k in ("id","answer","askWrong","valid","invalid","unpars","decided")})

# distribution of number of valid options
def nvalid(r): return len(r["valid"])
def ninvalid(r): return len(r["invalid"])
def nunpars(r): return len(r["unpars"])
print("\n#valid distribution:", Counter(nvalid(r) for r in rows))
print("#invalid distribution:", Counter(ninvalid(r) for r in rows))
print("#unpars distribution:", Counter(nunpars(r) for r in rows))

# how many have >=1 unparsable option
print("questions with >=1 unparsable:", sum(1 for r in rows if nunpars(r)>0))

# show a few rows where unpars>0
up = [r for r in rows if nunpars(r)>0][:5]
print("\nrows with unparsable:")
for r in up:
    print("  ", r["id"], "valid=",r["valid"],"invalid=",r["invalid"],"unpars=",r["unpars"])
