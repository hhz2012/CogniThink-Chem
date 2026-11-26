# 多子问母题上下文恢复（轨道 1）- 产品需求文档

## Overview
- **Summary**: 让多子问大题的子问求解器能看到被丢弃的大题引语（及受控的兄弟子问线索），解锁因「上下文丢失」而未解的题；全程守住 `precision=1 / harm=0 / LOST=0 / mp27=27/27` 红线。
- **Purpose**: batch6 审计确认窄 solver/fact 增益已枯竭（88 leads → 23 候选 → 0 安全单题），真实杠杆在架构缺口；batch5 X.3 实测 `EquationBalancer` 对 pdf-164/171/462 方程均能配出正确系数，**阻塞是反应物上下文缺失，非求解器能力**。本 spec 修复上下文丢失根因。
- **Target Users**: 引擎评测网关（gwCorr），最终受益为作答学生。

## Goals
- **G1**: 在零回归前提下，提升多子问大题的引擎解出题数（gwCorr）。
- **G2**: 建立可验证、可开关的「上下文回填」机制，为后续跨 case 大题重组铺路。
- **G3**: 用探针数据**客观度量 ROI**——若机制无收益则诚实收口，不硬凑、不放宽守卫。

## Non-Goals
- 不改判分逻辑（`multi_part` AND 门、`IsAnswerCorrect` 关键词 OR、采纳门）。
- 不动 `images`、不改题录 JSON 数据（跨 case 重组若需改数据则排除，只做运行时文本重组）。
- 不臆造真值；不迎合存疑答案键（如 pdf-519「硫醚」、pdf-198 子问 (2) 裸键「C」、pdf-030 固定「25.00%」）。
- 不做图像/结构 ingest（属轨道 2）。
- 不做自由文本推理求解器（pdf-163/537 等不在范围）。
- 不修复数据错位/答案污染（属 W 类数据修复批次，与本 spec 解耦）。

## Background & Context

### 根因（源码确认）
- [LlmGatewayService.MultiPartDispatch.cs:36](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs#L36)：逐子问请求 `RawText = subText`，而 `subText` 来自 [MultiPartUtility.SplitSubQuestions](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiPartUtility.cs#L31-L49)，**只含该子问 (N) 标记之后的碎片，不含标记前的大题引语**。
- 子问求解器因此看不到：工艺流程描述、已知条件数据表、反应物出处 → 方程/计算题缺锚点 → `Ok!=true` → AND 门整题 null。

### 题录结构（数据确认，证据 `_evalscratch/dump_id_ranges.txt`）
题录无 `parentId`/`partIndex`，一道大题被拆成多个相邻 id 的碎片 case，且存在引言重复/丢失、答案错位：
- pdf-162/163/164/165 = 同一 BaTiO₃ 大题的子问 (3)(4)(5)(6)，引言在 pdf-161（但 pdf-161 子问题干已丢）。
- pdf-462/463/464/465 = 同一 Ni 流程大题的子问 (4)(5)(6)(7)，(1)(2)(3) 与引言缺失。
- pdf-539/540/541/542 = 同一 GaN 大题，子问 (2) 被拆成三条，引言在 540/541 重复。

### 目标题集（证据 `_evalscratch/multipart_targets_b7.txt`，基线 `checkpoint_b6baseline` 757 题）
未解 88 题中，`IsMultiPart`（含 ≥2 个 `(N)` 标记）共 20 题，分两类：
- **场景 A（完整大题 case，17 题）**：`case.question` 本身含引语 + 多子问 → 引语在单 case 内，运行时可回填（pureLlmOnly 13 / bothWrong 4）。
- **场景 B（碎片 case，3 题）**：`case.question` 只有 `(N)...` 碎片，引语/兄弟子问在别的 case → 需跨 case 重组。

> **诚实预判**：场景 A 的 17 题中，多数因 AND 门（含自由文本/缺图/计算/选择子问）或答案键缺陷，即使回填引语也未必能全解。静态分析无法精确判定，**必须靠探针实测**。batch5 X.3 的 pdf-164/171/462 实际属场景 B 碎片且题干缺反应物（反应物在流程图/答案里），文本回填未必能救——同样需探针验证。

## Functional Requirements

- **FR-1（场景 A 引语回填）**: 当整题未解且 `IsMultiPart` 为真时，`TryDispatchMultiPartAsync` 逐子问构造的请求文本应包含「第一个子问标记前的大题引语」+ 该子问文本，使子问求解器获得工艺流程/已知条件/反应物上下文。
- **FR-2（ROI 探针）**: 实现后必须全库（757 题）dry-fire，产出 `GAIN / LOST / harm / newlyFiredWrong` 数据，作为机制去留的客观依据。
- **FR-3（场景 B 可行性评估）**: 基于题录碎片结构数据，评估「运行时跨 case 大题重组」的风险与潜在收益，给出明确 go/no-go 建议（本 spec 不实现场景 B）。
- **FR-4（无收益收口）**: 若场景 A 探针 `GAIN=0` 且无法在不引入 harm 的前提下扩展，则回滚机制、记录「轨道 1 场景 A 无收益」结论，不硬上场景 B。
- **FR-5（机制可开关）**: 上下文回填必须可由配置/编译开关关闭，关闭后行为与基线 `b6baseline` 完全一致（便于回滚与 A/B 对比）。

## Non-Functional Requirements

- **NFR-1（零回归）**: 改动后 `LOST=0`、`harm=0`、`precision=1`、`mp27=27/27`。
- **NFR-2（最小侵入）**: 仅修改 Gateway 分发层（`MultiPartUtility` / `LlmGatewayService.MultiPartDispatch`），不碰判分、题录、`images`。
- **NFR-3（可验证）**: 每条新增触发路径必须有全库 dry-fire 证据，证明 `newlyFiredWrong=0`。
- **NFR-4（可回滚）**: 单次提交内可通过开关恢复基线行为，不依赖数据迁移。

## Constraints
- **Technical**: 红线 `precision=1 / harm=0 / LOST=0 / mp27 不退化`；`ProcessStudentMessageAsync` 仅在 `engineResponse.Ok is not true` 时触发分发 → 已解题天然不受影响（构造性零 LOST）。
- **Business**: 不为凑数放宽守卫；数据修复（W 类）与本 spec 解耦；不主动 commit/push。
- **Dependencies**: 基线快照 `_evalscratch/checkpoint_b6baseline.jsonl`（757 题，gwCorr=669）；工具链 `run_eval_resume.ps1` / `diff_eval.ps1` / `verify_mp27.py`。

## Assumptions
- **A1**: 引语回填不会改变已判正题（触发条件 `Ok!=true` 保证）。
- **A2**: 子问求解器对「引语+子问」输入不会比「单子问」更易误判（需探针 `newlyFiredWrong=0` 验证）。
- **A3**: 场景 A 至少 0 题可被解锁（探针验证；若为 0 触发 FR-4）。

## Acceptance Criteria

### AC-1: 已判正题零回归（LOST=0）
- **Type**: `rule`
- **Given**: 基线 `checkpoint_b6baseline` 中 `gatewayCorrect=True` 的题集
- **When**: 机制开启后重跑全库 eval，与基线 diff
- **Then**: `LOST` 集合为空
- **Pass Condition**: `diff_eval b6baseline <new>` 输出 `LOST (0)`
- **Evidence**: `_evalscratch/diff_*.txt` 或 eval 输出

### AC-2: harm=0 且 precision=1
- **Type**: `rule`
- **Given**: 机制开启后的全库 eval
- **When**: 统计 `harmCount` 与 `precision`
- **Then**: `harmCount=0` 且 `precision=1`
- **Pass Condition**: 新快照 summary 显示 `harmCount=0 precision=1`
- **Evidence**: `_evalscratch/checkpoint_<tag>_summary.txt`

### AC-3: mp27 守卫不退化
- **Type**: `rule`
- **Given**: mp27 守卫题集
- **When**: 机制开启后运行 `verify_mp27`
- **Then**: 27/27 保持 `gatewayCorrect=True`，无 True→False 回归
- **Pass Condition**: `verify_mp27.py` 输出 `RESULT: PASS (27/27)`
- **Evidence**: `_evalscratch/mp27_*.txt` 或命令输出

### AC-4: 回填仅作用于未解多子问题
- **Type**: `rule`
- **Given**: 任意已判正题（`Ok=true`）
- **When**: 走网关主链路
- **Then**: `TryDispatchMultiPartAsync` 不被触发（`engineResponse.Ok is true` 短路）
- **Pass Condition**: 代码审查触发条件 + 已解题结论与基线逐字一致
- **Evidence**: 源码 [LlmGatewayService.cs:111](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L111) + diff 无已解题变化

### AC-5: 全库 dry-fire 无错误采纳（newlyFiredWrong=0）
- **Type**: `rule`
- **Given**: 机制开启，全库 757 题
- **When**: 逐题模拟子问回填后的求解器调用，比对基线
- **Then**: 没有任何「基线未解 + 新产出结论被采纳但答案键未命中」的题
- **Pass Condition**: 探针报告 `newlyFiredWrong=0`
- **Evidence**: `_evalscratch/probe_context_refill_*.txt`

### AC-6: 机制去留由探针数据决定（决策有效性）
- **Type**: `rubric`
- **Dimension**: ROI 决策的数据充分性与诚实性
- **Scale**: 1-5
- **Anchors**: 1 = 无探针数据，靠主观判断去留；3 = 有探针 GAIN/LOST 数据但未据此决策；5 = 探针数据完整（GAIN/LOST/harm/newlyFiredWrong），且机制保留或回滚的决策与数据一致（GAIN≥1 且零回归→保留；GAIN=0→回滚收口）
- **Pass Threshold**: >= 4
- **Evidence**: 探针报告 + 最终 spec 状态说明

### AC-7: 场景 B 可行性评估质量
- **Type**: `rubric`
- **Dimension**: 跨 case 碎片重组的 go/no-go 评估是否基于数据证据
- **Scale**: 1-5
- **Anchors**: 1 = 未评估或主观臆断；3 = 列举了碎片题但无量化风险/收益；5 = 基于题录结构数据量化了可重组题数、假阳性风险、潜在增益，并给出明确 go/no-go 与理由
- **Pass Threshold**: >= 3
- **Evidence**: `_evalscratch/scenario_b_feasibility.txt` 或 spec 内评估章节

## Open Questions
- [ ] 场景 A 探针 `GAIN` 是否 ≥ 1？（决定机制保留还是 FR-4 收口）
- [ ] 引语回填是否需要门控（仅对 equation/calculation 子问，避免概念/判断题引语噪声）？（取决于探针 `newlyFiredWrong`）
- [ ] 场景 B（跨 case 重组）是否启动？（取决于 A 的收益 + AC-7 评估 + 用户决策）
