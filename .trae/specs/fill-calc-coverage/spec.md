# 填空/计算题型覆盖率提升（fill-calc-coverage）- Product Requirements Document

## Overview
- **Summary**：在不修改判分逻辑、不降低精度的前提下，通过增强引擎能力与外置化学知识，让当前未解的填空（fill）/计算（calc）/判断（judge）题型尽可能被正确解出，从而提升题库全量覆盖率。
- **Purpose**：A 批次（svg_batch34）已通过追加 Concept 单选事实把覆盖率从 83.88% 提升到 84.15%；剩余 120 道未解中 82 道属于 fill/calc/other/judge（非 Concept 单选可覆盖），是当前覆盖率的主要瓶颈。本 spec 聚焦这 82 道的能力缺口。
- **Target Users**：使用 chem-engine 做高中化学题库自动判分/解题的产品与评测流程。

## Goals
- 在 **harm=0 / LOST=0 / precision=1** 红线下，提升 fill/calc/other/judge 未解题型的解题数量。
- 每一批次改进都可独立验证：新增解出的题恰好是预期题，且不引入任何回归或错解采纳。
- 形成可追溯的台账：每道未解的处置（已解 / 不可碰 / 引擎能力缺口留空）。

## Non-Goals
- **不修改判分逻辑**（`IsAnswerCorrect` / `AllKeywordsHit` / 结构化判分的匹配口径与容差）——判分口径是红线基准，改动会破坏历史可比性。
- **不碰答案键冲突题**：化学推理与官方 answer 不一致的题（如 pdf-227、pdf-409）一律跳过，绝不写事实/改逻辑去迎合错误答案键。
- **不碰多选题**（答案 BC/BD 等）——多选判分有独立口径，不在本 spec 范围。
- **不为凑覆盖率蒙数**：无法唯一确定答案的题（缺图数据、歧义、无真实锚点）一律留空，标注原因。
- 不追求 100% 覆盖率；以「能安全解的尽量解、不能解的诚实留空」为准。

## Background & Context
- 题库全量 757 case（pdf_questions 545 + tier2 109，部分大题拆多小问），基线（after_svg_batch34）solved=637，覆盖率 84.15%，未解 120。
- 120 道未解按 `(taskType, case.type)` 分诊（来自 `_evalscratch/checkpoint_after_svg_batch34.jsonl`）：
  - **unknown × fill = 24**：化工流程题填空/简答，答案为自由文本或多小问混合，引擎 taskType=unknown 未路由到专用求解器。
  - **equation_writer × fill = 17**：写化学/离子方程式，配平与产物推断能力不足。
  - **equilibrium/calculation × fill/calc/other = 32**：平衡与定量计算填空（产率%、Ksp、浓度、ΔH 等），现有 11 个计算求解器未覆盖。
  - **judgment × judge = 3、reaction_check × fill = 2、atomic_structure × fill = 1、unknown × other/calc = 3**：杂项。
  - 合计 **82 道** fill/calc/other/judge 未解；其余 38 道为 choice（已由 A 批次及不可解清单处置）。
- 判分口径（`GatewayVsLlmEvaluationService.IsAnswerCorrect`）：fill/calc 走关键词 OR 命中（答案键任一关键词命中即判正），多部分大题走 AND（`AllKeywordsHit`）。改引擎的 harm 风险来自「引擎产出错答文本，但该文本碰巧命中某关键词」→ 假阳性。
- 计算引擎（`LlmGatewayService.CallCalculation`）现有 11 个求解器（FeBr2+Cl2、Fe+HNO3、简单化学计量、质量分数→摩尔浓度、混合物分解、Hess 定律×2、Ka 离子浓度比较、batch2 窄域、盐水解 pH、氧化还原比例），未覆盖返回 `Ok=null`。
- 红线沿用全库规则：precision=1（零假阳性）、harmCount=0（零错解）、LOST=0（零回归）；R4（无真实锚点不臆造）。

## Functional Requirements
- **FR-1（分诊与排除）**：对 82 道 fill/calc/other/judge 未解逐题分诊，标注「可改进 / 不可碰（答案键冲突/歧义/缺图/无锚）」，输出可追溯清单。
- **FR-2（方程式书写能力，equation_writer）**：增强 17 道方程式填空题的求解能力（配平、产物推断），优先采用外置 JSON 反应规则/知识，不硬编码具体题目。
- **FR-3（定量计算能力，equilibrium/calculation）**：扩展计算引擎覆盖 32 道计算填空（产率、Ksp、浓度、ΔH 等），新增求解器必须满足物料/电子/电荷守恒校验与解的唯一性，不蒙数。
- **FR-4（流程填空路由与推理，unknown）**：对 24 道 unknown 流程填空题，评估能否通过路由识别 + 外置流程知识安全解出；无法安全解的诚实留空并标注。
- **FR-5（杂项处置）**：对 judgment/reaction_check/atomic_structure 等杂项（6 道）逐题评估，能解则解，不能则留空标注。
- **FR-6（台账同步）**：每批次结束后更新 `data-defect-register.md` 与本 spec `tasks.md`，记录处置结果。

## Non-Functional Requirements
- **NFR-1（红线零容忍）**：每批次 eval 对比必须满足 LOST=[]、precision=1、harm=0、GAINED 恰为该批新解出的题 id 集合。任一违反即回滚该批。
- **NFR-2（可验证）**：每批次有独立的 eval 快照（`snapshot_<tag>.json`）与 `cmp` 输出作为证据。
- **NFR-3（不破坏现有测试）**：全量测试（Batch3 / Chem / TutorBrain）保持 0 新增失败。
- **NFR-4（外置优先）**：化学知识只存 JSON（`data/**/*.json`），不硬编码进逻辑；引擎代码改动限于求解器扩展与路由，不改判分。

## Constraints
- **Technical**：Windows + dotnet 8.0.424；Python 评估脚本位于 `C:\Users\Administrator\.local\bin\python3.14.exe`（不在 PATH）；`_evalscratch/` 被 `.gitignore` 忽略，不入库。
- **Business**：用户未明确要求 commit 时不主动提交；红线优先于覆盖率数字。
- **Dependencies**：依赖 A 批次（svg_batch34）已完成的基线（solved=637）；判分口径与计算引擎现有结构。

## Assumptions
- 82 道未解中存在相当比例「不可碰」（答案键冲突/缺图/歧义），实际可安全提升的数量可能显著小于 82；以逐题核实为准。
- fill/calc 的关键词 OR 判分口径下，外置事实/规则需确保命中关键词时结论确实正确（否则 harm）。

## Acceptance Criteria

### AC-1: 基线分诊清单可追溯
- **Type**: `rule`
- **Given**: 82 道 fill/calc/other/judge 未解
- **When**: 完成逐题分诊
- **Then**: 每道题有明确处置标注（可改进子类 / 不可碰原因），清单写入 spec  artifacts
- **Pass Condition**: 82 道全部有标注，无遗漏
- **Evidence**: `tasks.md` 分诊表 + `_evalscratch/` 分诊脚本输出

### AC-2: 每批次红线零违规
- **Type**: `rule`
- **Given**: 任一改进批次完成
- **When**: 运行 `cmp.py <base> <after>` 对比
- **Then**: LOST=[]、precision=1、harm=0、GAINED 恰为该批预期新解题
- **Pass Condition**: cmp 输出满足上述全部
- **Evidence**: `_evalscratch/cmp.py` 输出 + `snapshot_<after>.json`

### AC-3: 不破坏现有测试
- **Type**: `rule`
- **Given**: 任一改进批次完成
- **When**: 运行全量测试
- **Then**: Batch3 / Chem / TutorBrain 无新增失败
- **Pass Condition**: 三个测试套件 failed=0
- **Evidence**: `dotnet test` 输出

### AC-4: 台账与任务可追溯
- **Type**: `rule`
- **Given**: 所有批次完成
- **When**: 检查 `data-defect-register.md` 与 `tasks.md`
- **Then**: 每道未解的最终处置（已解 / 不可碰 / 留空原因）已记录
- **Pass Condition**: 台账与 tasks 状态一致，无残留 `_待回填_`
- **Evidence**: 文件内容

### AC-5: 覆盖率提升且无精度损失
- **Type**: `rubric`
- **Dimension**: 全量题库覆盖率净提升（solved 增量），且 precision 维持 1
- **Scale**: 1-5
- **Anchors**: 1 = 无提升或引入 harm/LOST；3 = 小幅提升（+1~+5）且红线全过；5 = 显著提升（>+5）且红线全过、台账完整
- **Pass Threshold**: >= 3（必须红线全过；提升幅度是加分项，不为达标而冒险）
- **Evidence**: 基线 `snapshot_after_svg_batch34.json`（solved=637）与最终 `snapshot` 的 cmp 结果

## Open Questions
- [ ] equation_writer 的 17 道中，有多少可通过外置 JSON 反应规则低风险解决，多少必须改配平引擎？（Task 2 分诊时确定）
- [ ] unknown 流程填空的自由文本答案，关键词 OR 判分下能否安全采纳？是否需要收紧为 AND？（不改判分的前提下，评估引擎产出的可控性）
- [ ] 是否需要为计算引擎新增「Ksp 沉淀计算」「产率/纯度计算」通用求解器，还是逐题外置？（Task 3 评估）
