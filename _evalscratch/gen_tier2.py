import json

# 每道题核库保证：唯一正确；所有选项物质在 substance-classification.json 的 formula/names 中；
# 类别词在 aliases 或 tag 中；错误选项明确为假且可判定（不出现库外物质）。
Qs = [
 # ---- 电解质 / 非电解质（P4 题干类别 + 选项裸物质）----
 dict(id="t2c-01", q="下列物质属于电解质的是", opts={"A":"CO2","B":"HCl","C":"蔗糖","D":"乙醇"}, ans="B"),
 dict(id="t2c-02", q="下列物质属于非电解质的是", opts={"A":"NaCl","B":"H2SO4","C":"SO2","D":"NaOH"}, ans="C"),
 dict(id="t2c-03", q="下列物质不属于电解质的是", opts={"A":"HNO3","B":"KOH","C":"BaSO4","D":"乙醇"}, ans="D"),
 dict(id="t2c-04", q="下列物质中，属于弱电解质的是", opts={"A":"HCl","B":"CH3COOH","C":"NaOH","D":"NaCl"}, ans="B"),
 dict(id="t2c-05", q="下列物质中，属于强电解质的是", opts={"A":"CH3COOH","B":"H2CO3","C":"HClO","D":"HNO3"}, ans="D"),
 dict(id="t2c-06", q="下列物质属于非电解质的是", opts={"A":"氯气","B":"氯化氢","C":"氨气","D":"氢氧化钠"}, ans="C"),

 # ---- 酸 / 碱 / 盐 / 氧化物（P3 选项"X是Y"）----
 dict(id="t2c-07", q="下列物质分类正确的是", opts={"A":"H2SO4 是碱","B":"NaOH 是酸","C":"NaCl 是盐","D":"CO2 是碱"}, ans="C"),
 dict(id="t2c-08", q="下列关于物质分类的说法正确的是", opts={"A":"HCl 是酸","B":"Na2CO3 是碱","C":"CO2 是盐","D":"CaO 是酸"}, ans="A"),
 dict(id="t2c-09", q="下列物质属于氧化物的是", opts={"A":"H2SO4","B":"NaOH","C":"H2O","D":"NaCl"}, ans="C"),
 dict(id="t2c-10", q="下列物质属于盐的是", opts={"A":"HNO3","B":"KOH","C":"CaCO3","D":"CH4"}, ans="C"),

 # ---- 类别：成员列表（P2）----
 dict(id="t2c-11", q="下列各组物质的分类正确的是",
      opts={"A":"酸：HCl、NaOH、NaCl","B":"碱：KOH、Ba(OH)2、Ca(OH)2","C":"盐：CO2、SO2、H2O","D":"氧化物：H2SO4、HNO3、HCl"}, ans="B"),
 dict(id="t2c-12", q="下列分类正确的是",
      opts={"A":"电解质：NaCl、HCl、CO2","B":"非电解质：蔗糖、乙醇、SO2","C":"强酸：HCl、CH3COOH、H2CO3","D":"弱电解质：NaOH、KOH、Ba(OH)2"}, ans="B"),
 dict(id="t2c-13", q="下列物质分类正确的是",
      opts={"A":"酸性氧化物：CO2、SO2、CaO","B":"碱性氧化物：Na2O、CaO、Fe2O3","C":"两性氧化物：CO2、SO2、SiO2","D":"不成盐氧化物：CO、NO、SO3"}, ans="B"),
 dict(id="t2c-14", q="下列分类正确的是",
      opts={"A":"单质：O2、N2、CO2","B":"化合物：H2O、CO2、NaCl","C":"混合物：H2O、CO2、NaCl","D":"纯净物：空气、氯水、钢铁"}, ans="B"),
 dict(id="t2c-15", q="下列物质分类正确的是",
      opts={"A":"离子化合物：NaCl、KCl、HCl","B":"共价化合物：CO2、CH4、NaCl","C":"离子化合物：NaOH、CaO、NaCl","D":"共价化合物：H2O、HCl、KCl"}, ans="C"),

 # ---- 同素异形体 / 同位素（P5）----
 dict(id="t2c-16", q="下列各组物质互为同素异形体的是",
      opts={"A":"氧气、臭氧","B":"¹²C、¹³C","C":"水、过氧化氢","D":"氧化钠、过氧化钠"}, ans="A"),
 dict(id="t2c-17", q="下列各组物质互为同位素的是",
      opts={"A":"O2、O3","B":"氕、氘","C":"红磷、白磷","D":"金刚石、石墨"}, ans="B"),
 dict(id="t2c-18", q="下列各组互为同素异形体的是",
      opts={"A":"白磷、红磷","B":"¹⁴N、¹⁵N","C":"CO、CO2","D":"水、过氧化氢"}, ans="A"),
 dict(id="t2c-19", q="下列各组互为同位素的是",
      opts={"A":"氧气、臭氧","B":"¹²C、¹⁴C","C":"白磷、红磷","D":"金刚石、石墨"}, ans="B"),
 dict(id="t2c-20", q="下列各组物质中，互为同素异形体的是",
      opts={"A":"CO2 和 SO2","B":"金刚石和石墨","C":"H2O 和 H2O2","D":"Na2O 和 Na2O2"}, ans="B"),

 # ---- 纯净物 / 混合物 / 单质 / 化合物 ----
 dict(id="t2c-21", q="下列物质属于纯净物的是",
      opts={"A":"钢铁","B":"氯水","C":"干冰","D":"漂白粉"}, ans="C"),
 dict(id="t2c-22", q="下列物质属于混合物的是",
      opts={"A":"H2O","B":"Cl2","C":"钢铁","D":"CO2"}, ans="C"),
 dict(id="t2c-23", q="下列物质属于单质的是", opts={"A":"HCl","B":"O2","C":"H2O","D":"NaCl"}, ans="B"),
 dict(id="t2c-24", q="下列物质属于化合物的是", opts={"A":"Fe","B":"Cu","C":"H2SO4","D":"C"}, ans="C"),
 dict(id="t2c-25", q="下列属于共价化合物的是", opts={"A":"NaCl","B":"KCl","C":"CO2","D":"CaO"}, ans="C"),

 # ---- 离子 / 共价化合物、有机物、盐分类 ----
 dict(id="t2c-26", q="下列属于离子化合物的是", opts={"A":"HCl","B":"CO2","C":"CH4","D":"NaOH"}, ans="D"),
 dict(id="t2c-27", q="下列物质属于有机物的是", opts={"A":"NaCl","B":"H2SO4","C":"乙醇","D":"CaCO3"}, ans="C"),
 dict(id="t2c-28", q="下列不属于有机物的是", opts={"A":"CH4","B":"乙醇","C":"蔗糖","D":"碳酸"}, ans="D"),
 dict(id="t2c-29", q="下列属于钠盐的是", opts={"A":"NaCl","B":"KCl","C":"CaCO3","D":"NH4Cl"}, ans="A"),
 dict(id="t2c-30", q="下列属于硫酸盐的是", opts={"A":"NaCl","B":"CuSO4","C":"KNO3","D":"Na2CO3"}, ans="B"),
 dict(id="t2c-31", q="下列各组均为强酸的是",
      opts={"A":"HCl、H2SO4、HNO3","B":"HCl、CH3COOH、H2CO3","C":"NH3·H2O、Cu(OH)2、Fe(OH)3","D":"NaOH、KOH、Ba(OH)2"}, ans="A"),
]

out = []
for q in Qs:
    out.append({
        "id": q["id"],
        "question": q["q"] + "（ ）",
        "options": q["opts"],
        "answer": q["ans"],
        "answerKeywords": [q["ans"]],
        "type": "choice",
        "note": "Tier 2-A 物质分类选择题（事实库查表，ClassificationValidator 确定性求解）"
    })

p = r'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\tier2_questions.json'
with open(p, 'w', encoding='utf-8') as f:
    json.dump(out, f, ensure_ascii=False, indent=2)
print(f"written {len(out)} questions to {p}")
