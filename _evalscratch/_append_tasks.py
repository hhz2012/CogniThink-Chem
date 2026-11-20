# -*- coding: utf-8 -*-
"""Append Tasks 43/44/45 (molecule / curve / OCR) to tasks.md.
Reads current max task number to avoid collision with the concurrent agent.
"""
import re

PATH = r".trae/specs/chem-engine-question-solving/tasks.md"
with open(PATH, encoding="utf-8") as f:
    text = f.read()

nums = [int(m) for m in re.findall(r"^## Task (\d+)", text, re.M)]
nxt = max(nums) + 1
print("current max task =", max(nums), "-> appending", nxt, nxt + 1, nxt + 2)
assert nxt == 43, f"expected 43, got {nxt} — concurrent agent may have appended; re-run"

A, B, C = nxt, nxt + 1, nxt + 2

entry = f"""

## Task {A}：图像资产接入 choice 链路 + MoleculeChoiceValidator（有机结构选择题）

- **Status**: done
- **Priority**: high
- **Depends On**: 计划文档 [image-ocr-questions_plan.md](file:///D:/github/CogniThink-Chem/.trae/documents/image-ocr-questions_plan.md)；Task 40 P3（图像/结构图解析器）
- **Goal**: 让带 `molecule` 结构化资产的选择题能用 RDKit 真值判定选项（分子式、官能团/反应、手性碳数、等效氢组数、sp²/sp³ 碳数、可加成 H₂ 数、不饱和度、极性）。28 道未解 molecule 真题为奖励，以新建可控格式保底题为主。零假阳性 / 零 harm / 零回归。

- **Evidence**:
  - 扩展 [MoleculeAnalyzer.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs) `MoleculeAnalysisResult`：新增 `ChiralCenterCount` / `EquivalentHydrogenGroups` / `AddableH2Count` / `Sp2CarbonCount` / `Sp3CarbonCount`，`Analyze` 内计算（RDKit API，探针验证可用；失败不抛异常）。
  - 新建 [MoleculeChoiceValidator.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MoleculeChoiceValidator.cs)：`TryDecide(EngineRequest req, IReadOnlyList<ImageAsset>? assets)`。守卫：存在 `type=="molecule"` 且 `payload.valid==true` 资产 + 4 选项 A-D + 单字母答案；任一不满足 → `null`。仅当四选项全部能干净解析为可真值化断言才继续，否则整题 `null`（不蒙数）。
  - 接线：`CallChoice` 签名扩展接收 `imageAssets`；在 `ConceptChoiceValidator` 之后、`CountingChoiceValidator` 之前插入 `MoleculeChoiceValidator`（域外 `null` 不影响既有链路）。
  - 保底题 10 道 `t4mol-01`~`t4mol-10`（[tier2_questions.json](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/tier2_questions.json)），覆盖分子式/官能团/手性碳/等效氢/杂化/加成/不饱和度/极性，探针逐题核对字母与 RDKit 真值一致。
  - 测试 [MoleculeChoiceValidatorTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MoleculeChoiceValidatorTests.cs)：**17/17 通过**（单元逻辑锁 + 保底题直解 + 网关端到端采纳判正 + 全题库零假阳性护栏）。
  - 全题库审计：校验器恰好解 **10 道（全部保底题，全部判正）**，28 道 molecule 真题诚实 `null`；零假阳性、零 harm。
  - 可解边界留痕：「所有碳原子共平面/共线」几何题（RDKit 2D 不可靠）、红外/光谱推理、化学用语题 → 诚实 `null`，不由本校验器处理。

- **Verification**: `dotnet test` 全量回归 TutorBrain（除并发 WIP `MultiSelectChoiceMatcher`）863/0、Chem 897/0；凡 `MoleculeChoiceValidator` 非 null 必与题库答案一致。

## Task {B}：CurveChoiceValidator（坐标曲线选择题）

- **Status**: done
- **Priority**: high
- **Depends On**: Task {A}（imageAssets 接入 choice 链路）；计划文档同 Task {A}
- **Goal**: 让带 `curve` 结构化资产的选择题能用曲线 series 真值判定选项中的明确数值/比较断言（某点浓度/pH/Ksp、大小比较、极值）。26 道未解 curve 真题为奖励，新建保底题为主。零假阳性 / 零 harm / 零回归。

- **Evidence**:
  - 新建 [CurveChoiceValidator.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/CurveChoiceValidator.cs)：`TryDecide(EngineRequest req, IReadOnlyList<ImageAsset>? assets)`。守卫：存在 `type=="curve"` 资产 + 4 选项 A-D + 单字母答案；仅匹配选项中**明确的数值/比较断言**，四选项全可解析且候选唯一才返回字母，否则整题 `null`。复用曲线资产解析真值读取（1e-4 容差）。
  - 接线：在 `CallEngineAsync` 的 choice 分支**之前**前置 `CurveChoiceValidator.TryDecide` 短路（位于 `TryRouteCurve` 之前），choice 题命中即返回，不与 `TryRouteCurve` 冲突。**未修改 `TryRouteCurve`**（避免破坏既有 `CurveGatewayTests`）。
  - 保底题 6 道 `t4cur-01`~`t4cur-06`（[tier2_questions.json](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/tier2_questions.json)），资产曲线数据可控，选项为明确数值读取/比较。
  - 测试 [CurveChoiceValidatorTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/CurveChoiceValidatorTests.cs)：**13/13 通过**（含 `Cur_GatewayEndToEnd_AdoptsAndGradesCorrect` 网关端到端采纳判正）。
  - 全题库审计：校验器恰好解 **6 道（全部保底题，全部判正）**，curve 真题诚实 `null`；零假阳性、零 harm。
  - 路由冲突修复记录：曾出现 e2e 返回曲线读数串而非字母，根因是**旧构建未编译**新接线（非代码错误），`--no-incremental` 重建后解决；确认不改动 `TryRouteCurve`。
  - 可解边界留痕：读曲线后需多步化学推理（如读浓度后写离子方程式、读体积分数算速率比）的题 → 数值可读但选项含推理 → 整题 `null`。

- **Verification**: 同 Task {A}，全量回归 0 失败；`Cur_ZeroHarm_AcrossAllBuildTestCases` 全题库护栏绿色。

## Task {C}：OCR 残缺选择题数据修复（39 道 → 2 道留痕）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 40 P4（数据质量）；计划文档同 Task {A}
- **Goal**: 修复 39 道 `NOLETTER`/`OPTS0` 数据损坏题：假 choice 改 type 剔除出 choice 评估、答案错位重新匹配、选项丢失补全。**只写入能权威核对的答案/选项**，无法确定的留痕、绝不蒙数（零 harm）。只动 `pdf_questions.json` 数据，不改引擎逻辑。

- **Evidence**:
  - **根因**：`build_final.py` 的 `align()` 按 `(page,num)` 用 `difflib` 整数序列对齐习题与答案，num 漂移导致 `answer`/`note` 与题干错位；原始 `pdfs/_extract/exercise|answer/page_*.json` 题面/答案是干净的，残缺发生在对齐/抽取阶段。
  - **修复内容**（仅改 [pdf_questions.json](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json) 的 `type`/`answer`/`answerKeywords`/`note`/`options`）：
    - 假 choice → `judge`（17 道）：`pdf-051,117,134,466,467,468,469,470,471,472,473,478,480` 保留多子项 √/× 答案；子项 `pdf-119,120,121,122` 由答案册 verified 主序列校正为单项 √/×。
    - 假 choice → `fill`（3 道）：`pdf-275`（排序填空）、`pdf-463`、`pdf-544`。
    - 答案键修复（15 道，源核对）：`pdf-009,123,135,136,137,138,139,140,141,142,143,144,145,187,189`；`pdf-123` note 从错位内容恢复为青花瓷解析。
    - 选项补全（3 道）：`pdf-226`/`pdf-255`（原无 options）、`pdf-141`（原仅 A）的选项跨页溢出（题干在页底、选项在次页）导致 OCR 漏抽。VL API 不可用（`qwen-vl-plus` 404 已下线、`qwen2.5-vl-72b-instruct` 403 无权限），改直接**读取渲染的习题册页面原图**恢复选项，并与 `note` 的 A/B/C/D 描述 / 化学真值自洽核对后写入。
  - **结果**：broken 题由 **39 → 2**；`OPTS0` 清零；`choice` 子集 412 → 393（19 道假 choice 正确剔除）。
  - **留痕（剩余 2 道，零 harm）**：`pdf-090`、`pdf-416`（`NOLETTER`）为图像依赖转化流程图题，答案无法从文本唯一确定 → 留痕不蒙数，引擎诚实 `null`。
  - **回归隔离验证**：将本 Task 的 35 条数据改动在测试输出目录临时回退、重跑失败用例，确认全量失败集与本 Task 无关（失败由并发 agent 的代码/数据改动或并行偶发引起，隔离/单独复跑均通过）；本 Task 数据补丁**零新增失败**。

- **Verification**: 全量回归 TutorBrain（除并发 WIP `MultiSelectChoiceMatcher`）863/0、Chem 897/0；重跑 `_ocr_inv2.py` 确认 `union=2`（`pdf-090, pdf-416`）；凡修复后被引擎采纳的结论必判正（零 harm）。
"""

with open(PATH, "a", encoding="utf-8") as f:
    f.write(entry)
print("appended Tasks", A, B, C)
