import json, urllib.request

cases = [
("pdf-022","(2025·湖南长沙联考·★★★)(1)实验室常用的浓硫酸，溶质质量分数为 98%，密度为 1.84 g/cm³，该硫酸的物质的量浓度为 ________。(2)要配制 250 mL 物质的量浓度为 1.84 mol/L 的稀硫酸，需量取上述浓硫酸的体积为 ______________。"),
("pdf-023","(2024·浙江省舟山模拟预测·★★★★)将3.27 g Na2CO3 和 NaHCO3 的混合物加热到质量不再减轻时，测得残留固体的质量为2.65 g。另取相同质量的此混合物固体，与100 mL某浓度的盐酸刚好完全反应生成NaCl，求：(1)此混合物中 NaHCO3 与 Na2CO3 的物质的量之比为__________。(2)所用盐酸的物质的量浓度为__________。(3)生成 CO2 在标准状况下的体积为__________。"),
("pdf-243","(2023·全国乙卷·★★)已知下列热化学方程式：\n方程式①:FeSO4·7H2O(s)═FeSO4(s)+7H2O(g)　ΔH1=a kJ/mol\n方程式②:FeSO4·xH2O(s)═FeSO4(s)+xH2O(g)　ΔH2=b kJ/mol\n方程式③:FeSO4·yH2O(s)═FeSO4(s)+yH2O(g)　ΔH3=c kJ/mol\n则 FeSO4·7H2O(s)+FeSO4·yH2O(s)═2FeSO4·xH2O(s)的 ΔH=______ kJ/mol。\n已知:x=4,y=1。"),
("pdf-259","(2025·新课标卷·★★★)乙酸乙酯是一种应用广泛的有机化学品，可由乙酸和乙醇通过酯化反应制备。回答下列问题：(1)乙酸、乙醇和乙酸乙酯的燃烧热分别为−874 kJ/mol、−1 367 kJ/mol 和−2 238 kJ/mol，则酯化反应 CH3COOH(l)+C2H5OH(l)═CH3COOC2H5(l)+H2O(l) 的 ΔH = ______ kJ/mol。"),
("pdf-257","（2024·海南卷·★★★★）氨是一种理想的储氢载体，具有储氢密度高、储运技术成熟等优点。已知298 K，100 kPa时，反应①：N2(g) + 3H2(g) ⇌ 2NH3(g)  ΔH1 = -92 kJ/mol；p(NH3) = 总压 × NH3的物质的量分数。回答问题：(1)题述条件下，反应②：2NH3(g) ⇌ N2(g) + 3H2(g)  ΔH2 = ______ kJ/mol。"),
("pdf-273","1.（★★）常温下，等浓度的 NaNO2 溶液和 CH3COONa 溶液，哪种溶液的离子总浓度更大？已知：Ka(HNO2)=1×10^-4，Ka(CH3COOH)=1×10^-5。"),
]

for cid, q in cases:
    body = json.dumps({"text": q}, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request("http://localhost:5080/api/gateway/process", data=body,
        headers={"Content-Type":"application/json; charset=utf-8"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            data = json.loads(resp.read().decode("utf-8"))
        tt = data.get("task_type")
        er = data.get("engine_response") or {}
        ok = er.get("ok")
        fr = er.get("final_result") or ""
        print("="*70)
        print(f"[{cid}] taskType={tt} engineOk={ok}")
        print("FinalResult:", fr)
    except Exception as e:
        print(f"[{cid}] ERROR: {e}")
