import json, urllib.request
q="(2025·湖南长沙联考·★★★)(1)实验室常用的浓硫酸，溶质质量分数为 98%，密度为 1.84 g/cm³，该硫酸的物质的量浓度为 ________。(2)要配制 250 mL 物质的量浓度为 1.84 mol/L 的稀硫酸，需量取上述浓硫酸的体积为 ______________。"
body=json.dumps({"text":q},ensure_ascii=False).encode("utf-8")
req=urllib.request.Request("http://localhost:5080/api/gateway/process",data=body,headers={"Content-Type":"application/json; charset=utf-8"},method="POST")
with urllib.request.urlopen(req,timeout=120) as resp:
    data=json.loads(resp.read().decode("utf-8"))
print("TOP KEYS:", list(data.keys()))
print(json.dumps(data, ensure_ascii=False, indent=2)[:2500])
