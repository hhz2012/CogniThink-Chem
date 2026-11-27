# 独立评审 — 数据修复（OCR 题面恢复）批次 dr1

> 评审对象：`after_dr1` 快照与本批全部落库改动。
> 评审口径：逐条核对 `spec.md` AC-1 ~ AC-8 与红线（precision=1 / harm=0 / LOST=[] / 不蒙数 / 不动 choice 键 / 不改判分 .cs）。
> 基线：`snapshot_after_keyfix_b38.json`（gatewayCorrect=700, bothWrong=57, harm=0, precision=1, LOST=[]）。

## 结论：**PASS（全部 AC 通过，红线守住）**

本批用本地 OCR 恢复了 2 道题面碎片（pdf-413、pdf-161），零键改动；离线 eval 实测 **GAIN +1（pdf-413 判正）、LOST=[]、harm=0、precision=1**。无判分逻辑代码改动，无 choice 键改动。

---

## 逐条 AC 核对

| AC | 类型 | 结论 | 证据 |
|----|------|------|------|
| AC-1 JSON 完整性 | rule | ✅ PASS | `python -c` 解析成功，条目数 = **545**；字段结构未变 |
| AC-2 题面恢复证据与对齐 | rule | ✅ PASS（见下方说明 1） | pdf-161：`ocr_page_054.txt`/`ocr_page_055.txt`，恢复 3 子问与键 `(1)(2)(3)` 一一对应；pdf-413：`ocr_page_137.txt`，恢复 (1)~(7)，键覆盖 (2)(3)(4)(5)(6) 且各段对齐 |
| AC-3 键订正命中安全 | rule | ✅ N/A（0 候选） | 57 未解逐题键核验：**43 EXACT 一致 + 14 未映射**，无一可独立核实地改错，故零键订正（`dr1_keyverify.txt`） |
| AC-4 引擎零回归 | rule | ✅ PASS | `dr1_diff.py`：LOST=[]、harm=0、precision=1、pureLlm 无变动 |
| AC-5 修复覆盖率（≥3） | rubric | ✅ PASS（5/5） | 可零歧义恢复的 Tier-1 碎片 = {pdf-413, pdf-161}，**2/2 = 100%**；其余暂缓项均有登记理由（`dr1_candidates.md` §2） |
| AC-6 登记册更新 | rule | ✅ PASS | 新增 §AA 批次记录；G 类碎片行、M 类两行用 `~~删除线~~` 标注 pdf-413/161 当前状态；暂缓项逐条登记 |
| AC-7 范围纪律 | rule | ✅ PASS（见下方说明 2） | dr1 净改动 ⊆ {`pdf_questions.json`（仅 2 条 question 行）、`data-defect-register.md`、`_evalscratch/*`}；无 `.cs` 判分逻辑；无 choice 键改动（`_dr1_scope.py`） |
| AC-8 不引入新 harm | rule | ✅ PASS | `checkpoint_after_dr1_summary.txt`：harmCount=**0**、gatewayTruthCorrect=**701** ≥ 700 |

---

## 关键数据

```
after_keyfix_b38 → after_dr1
  gatewayCorrect : 700 → 701   (+1, pdf-413)
  bothWrong      :  57 →  56
  LOST           :  []          (0 已解题退步)
  GAINED         :  [pdf-413]
  harm           :  0
  precision      :  1           (保持)
  pureLlm        :  无变动
```

- **pdf-413**：引擎产出 `H+ + OH- = H2O … 2H+ + CO3^2- = H2O + CO2↑`，与官方键 (6) 化学一致 → 真实判正，非假阳性。
- **pdf-161**：题面恢复完整、子问与键 3 段对齐；引擎仍 `null`（该 fill 需化学推理，Production 求解器暂不能解）→ 无回归，属预期。题面完整为未来引擎/LLM 清障。

---

## 需声明的注意事项（非缺陷，供复核者知情）

1. **pdf-413 恢复的子问 (1)、(7) 不在官方答案键中**：源页 137 OCR 显示该真题确有 (1)~(7)，但答案册官方键仅列出 (2)(3)(4)(5)(6)（[pdf_questions.json L15696](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json#L15696)）。本批**忠实恢复题面、不补键、不编造 (1)/(7) 答案**；判分器按现有键评估，(1)/(7) 仅为未被评分的题面子问。这是源数据键不完整，非本批引入的错误；若后续确认 (1)(7) 官方答案，可独立补键（需官方源）。

2. **工作区存在前序批次的未提交改动**：`git diff --stat` 显示 `general-choice-facts.json`、`substances.json`、`reactions-inorganic.json`、`OxidationStateAnalyzer.cs`、测试文件等也有改动——这些属批次 35-38（chem-fact-curation）的既存未提交工作，**非 dr1 引入**。dr1 的隔离净效应由快照 diff（`after_keyfix_b38`→`after_dr1`）权威证明，与工作区其余改动无关。

3. **pdf-528 是 pdf-413 同一道粗盐精制真题的兄弟碎片**（题面含「已知 KCl/NaCl 溶解度…(1)检验 BaCl2 过量」），但其键 `ans_page=-1` 未映射、无官方源可核 → 按"不蒙"保持不动（`dr1_candidates.md` §2c/§2e）。

4. **暂缓项均有明确理由**（`dr1_candidates.md` §2）：pdf-160 答案册分割歧义；B 类张冠李戴 7 题 `src_page` 映射不可靠；缺结构图题走图像轨道后续 spec；14 道未映射键无官方源。无"静默跳过"。

---

## 风险评估

- 假阳性风险：**无**。pdf-413 的判正由引擎产出与官方键化学一致的离子方程式触发；precision 由构造保证（gwCorr 仅当答案命中 `answerKeywords` 才判正），实测 precision=1。
- 回归风险：**无**。LOST=[] 证明 700 道已解题无一退步；pureLlm 无变动。
- 真值污染风险：**无**。未改任何 `answer`/`answerKeywords`/`type`/`note`；未改 choice 键；未引入编造答案。

## 建议后续方向（非本批范围）
- 选择题缺选项恢复（pdf-100/325/338/263 等，键与官方 EXACT 一致，抄回选项 A-D 有 GAIN 潜力，单字母命中门控 harm 风险低）→ 下批候选。
- 图像轨道（缺结构图/装置图）→ 独立 spec。
- B 类张冠李戴题需先建「题干片段→源大题」可靠映射 → 独立 spec。
