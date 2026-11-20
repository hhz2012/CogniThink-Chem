# -*- coding: utf-8 -*-
import json

PATH = r"src/CogniThink.TutorBrain.Api/data/tier2_questions.json"
data = json.load(open(PATH, encoding="utf-8"))
existing = {x["id"] for x in data}

def mol(smi, name):
    return [{"type": "molecule", "source": "", "payload": {"valid": True, "smiles": smi, "name": name}}]

new_qs = [
    {
        "id": "t4mol-01",
        "question": "如图为苯的结构简式，下列说法正确的是（ ）",
        "options": {
            "A": "分子式为 C6H6",
            "B": "不饱和度 Ω=3",
            "C": "一氯代物有 2 种",
            "D": "1 mol 最多与 2 mol H2 加成",
        },
        "answer": "A", "answerKeywords": ["A"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("c1ccccc1", "苯"),
    },
    {
        "id": "t4mol-02",
        "question": "如图为乙醇的结构简式，下列说法错误的是（ ）",
        "options": {
            "A": "分子式为 C2H6O",
            "B": "含有羟基",
            "C": "等效氢有 3 组",
            "D": "是极性分子",
        },
        "answer": "C", "answerKeywords": ["C"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("CCO", "乙醇"),
    },
    {
        "id": "t4mol-03",
        "question": "如图为 2-丁醇的结构简式，下列说法正确的是（ ）",
        "options": {
            "A": "含有 1 个手性碳",
            "B": "含有 2 个手性碳",
            "C": "一氯代物有 3 种",
            "D": "不饱和度 Ω=1",
        },
        "answer": "A", "answerKeywords": ["A"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("CCC(O)C", "2-丁醇"),
    },
    {
        "id": "t4mol-04",
        "question": "如图为乙酸的结构简式，下列说法错误的是（ ）",
        "options": {
            "A": "含有羧基",
            "B": "不饱和度 Ω=1",
            "C": "sp2 杂化的碳有 2 个",
            "D": "是极性分子",
        },
        "answer": "C", "answerKeywords": ["C"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("CC(=O)O", "乙酸"),
    },
    {
        "id": "t4mol-05",
        "question": "如图为乙醛的结构简式，下列说法正确的是（ ）",
        "options": {
            "A": "含有醛基",
            "B": "1 mol 最多与 2 mol H2 加成",
            "C": "含有羧基",
            "D": "sp2 杂化的碳有 2 个",
        },
        "answer": "A", "answerKeywords": ["A"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("CC=O", "乙醛"),
    },
    {
        "id": "t4mol-06",
        "question": "如图为甲苯的结构简式，下列说法错误的是（ ）",
        "options": {
            "A": "一氯代物有 4 种",
            "B": "含有苯环",
            "C": "1 mol 最多与 3 mol H2 加成",
            "D": "等效氢有 3 组",
        },
        "answer": "D", "answerKeywords": ["D"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("Cc1ccccc1", "甲苯"),
    },
    {
        "id": "t4mol-07",
        "question": "如图为四氯化碳的结构模型，下列说法正确的是（ ）",
        "options": {
            "A": "是极性分子",
            "B": "是非极性分子",
            "C": "不含有卤原子",
            "D": "不饱和度 Ω=1",
        },
        "answer": "B", "answerKeywords": ["B"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("ClC(Cl)(Cl)Cl", "四氯化碳"),
    },
    {
        "id": "t4mol-08",
        "question": "如图为苯酚的结构简式，下列说法错误的是（ ）",
        "options": {
            "A": "含有苯环",
            "B": "含有羟基",
            "C": "一氯代物有 4 种",
            "D": "不饱和度 Ω=4",
        },
        "answer": "C", "answerKeywords": ["C"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("Oc1ccccc1", "苯酚"),
    },
    {
        "id": "t4mol-09",
        "question": "如图为丙酮的结构简式，下列说法正确的是（ ）",
        "options": {
            "A": "含有酮羰基",
            "B": "含有醛基",
            "C": "sp2 杂化的碳有 2 个",
            "D": "不饱和度 Ω=2",
        },
        "answer": "A", "answerKeywords": ["A"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("CC(=O)C", "丙酮"),
    },
    {
        "id": "t4mol-10",
        "question": "如图为乙烯的结构简式，下列说法错误的是（ ）",
        "options": {
            "A": "分子式为 C2H4",
            "B": "含有碳碳双键",
            "C": "1 mol 最多与 2 mol H2 加成",
            "D": "sp2 杂化的碳有 2 个",
        },
        "answer": "C", "answerKeywords": ["C"], "type": "choice",
        "note": "Task 40 有机结构图像选择题（MoleculeChoiceValidator，RDKit 真值）",
        "imageAssets": mol("C=C", "乙烯"),
    },
]

added = 0
for q in new_qs:
    if q["id"] in existing:
        print("skip existing", q["id"]); continue
    data.append(q); added += 1

json.dump(data, open(PATH, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("added", added, "total now", len(data))
