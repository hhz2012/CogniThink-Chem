# -*- coding: utf-8 -*-
import json, os, re
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
data=json.load(open(HERE+"\\engine_wrong_full.json",encoding="utf-8"))
CHART_RE=re.compile(r"如图|下图|上图|图所示|图像|曲线|下表|表格|表中|流程图|流程如下|晶胞|装置图|实验装置|如图所示|见图|据图|坐标|滴定曲线|变化如图|关系图")
PLACEHOLDER=["已匹配到相关反应","已查到相关颜色","已解析出相关物质与离子","已解析出相关物质","已查到溶解性"]
def is_chart(o): return bool(CHART_RE.search(o.get("question") or ""))
def is_ph(o): return any(p in (o.get("gateway_conclusion") or "") for p in PLACEHOLDER)
r1=[o for o in data if is_ph(o) and not is_chart(o)]
print(f"R1 共 {len(r1)} 题，按题干长度：")
for o in sorted(r1,key=lambda x:len(x.get("question") or "")):
    q=o.get("question") or ""
    print(f"  {o['id']:10s} len={len(q):4d}  task={o['task']:16s} | {q[:55].replace(chr(10),' ')}")
