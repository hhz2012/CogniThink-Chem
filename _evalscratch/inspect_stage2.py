import json, sys

for name in ["after_stage2.json", "after_stage2b.json", "after_stage1.json"]:
    p = r"D:\github\CogniThink-Chem\_evalscratch\\" + name
    try:
        with open(p, encoding="utf-8-sig") as f:
            data = json.load(f)
    except Exception as e:
        print(name, "LOAD FAIL", e); continue
    print("="*60)
    print(name)
    print("top keys:", list(data.keys()))
    # print summary-ish scalar fields
    for k,v in data.items():
        if isinstance(v,(int,float,str,bool)) or v is None:
            print(f"  {k} = {v}")
    # find case partitions
    parts = [k for k in data if k.lower().endswith("cases")]
    print("  partitions:", parts)
    total=0
    sample=None
    for pk in parts:
        n=len(data[pk])
        total+=n
        if sample is None and n>0:
            sample=data[pk][0]
    print("  total cases:", total)
    if sample:
        print("  sample case keys:", list(sample.keys()))
        # print a few key fields
        for fld in ["id","taskType","gatewayCorrect","gatewayTruthAdopted","gatewayUsedEngine","gatewayOk","pureLlmCorrect"]:
            if fld in sample:
                print(f"     {fld} = {sample[fld]!r}")
