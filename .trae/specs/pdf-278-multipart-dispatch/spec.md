# pdf-278 多部分大题分发与判正 - Product Requirements Document

## Overview
- **Summary**: 让 pdf-278（一道 5 子问的电离平衡综合大题）经由引擎多部分分发链路被正确逐子问求解、聚合，并被真值口径判正；同时修复"多部分题被单部分占位结论阻塞分发"的通用缺陷，全库零回归。
- **Purpose**: pdf-278 当前 `gatewayCorrect=False`、`gatewayUsedEngine=True`——引擎开了火却答错（吐出只覆盖子问(5)的占位结论），而 LLM 答对。根因是多部分分发链路被占位结论短路，且子问(5)缺求解能力、子问(2)方程判分不归一。本 spec 让引擎真正覆盖此题，并把同类占位阻塞问题一并修掉。
- **Target Users**: 化学引擎评测（EvalRunner 全库 757 题）、线上化学答疑网关的最终学生用户。

## Goals
- **G1**: pdf-278 由引擎判正：`gatewayCorrect=False → True`、`gatewayUsedEngine=True`、`gatewayTruthAdopted=True`，且结论由引擎确定性求解得出（非 LLM 猜答）。
- **G2**: 多部分大题不再被单部分占位结论阻塞——当整题被某求解器吐出只覆盖单个子问的 `Ok=true` 占位结论时，不得阻止多部分分发链路接管。
- **G3**: 全库红线守住：`LOST=0`、`harm=0`、`precision=1`；27 道已解对的多部分题 `gatewayCorrect` 保持 True。

## Non-Goals
- **不**新建曲线读数能力：pdf-272（curve 占位）判正留待既有 K.4 曲线专项；本 spec 仅让它从"吐占位错误结论"变为"诚实 null"，不追求判正。
- **不**全局重写化学方程判分：方程两侧物质顺序无关的判分扩展**仅作用于 multi_part 的 AND 聚合判分路径**，不改动 `IsAnswerCorrect` 的 OR 路径与其他题型，避免全局 precision 风险。
- **不**触碰 27 道已解对多部分题的求解路径（它们是零回归硬边界）。
- **不**硬编码 pdf-278 单题解法：子问(5)的求解能力必须是可泛化规则，对反例不蒙数。
- **不**回退纯 LLM 猜答案；化学引擎仍是唯一真值源。

## Background & Context

### pdf-278 题面与答案
- 题干：25℃ 电离平衡常量表（CH3COOH Ka=1.8e-5；H2CO3 Ka1=4.5e-7 Ka2=4.7e-11；HClO Ka=4.0e-8；HCN Ka=6.2e-10；H2C2O4 Ka1=5.6e-2 Ka2=1.5e-4），含 5 个子问。
- 答案：`(1)HCN< H2CO3 < CH3COOH (2)CN- + CO2 + H2O = HCO3- + HCN (3)酸性 (4)9.7 (5)10^-5 - 10^-9 BD`
- 当前状态（checkpoint `routing_fallbacks` / `routing_focused`，三次一致）：`taskType=equilibrium`、`gatewayOk=True`、`gatewayCorrect=False`、`gatewayUsedEngine=True`、`gatewayTruthAdopted=False`、`pureLlmCorrect=True`；引擎结论 = "平衡向正向（右）移动，电离程度增大。"

### 根因链（探针实证）
1. **占位阻塞分发**：整题被 `TryJudgeIonizationShift`（[LlmGatewayService.cs:2263](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L2263)）匹配到子问(5)的"加热/稀释/电离程度增大"，吐出只覆盖子问(5)的占位结论且 `Ok=true`。而分发器 `TryDispatchMultiPartAsync` 的激活条件是 `engineResponse.Ok is not true`（[LlmGatewayService.cs:111](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L111)）→ 分发器永不触发 → pdf-278 始终答错。
2. **子问(5)缺求解器**：探针（`_evalscratch/probe_pdf278_subs.txt`）显示子问(5)无论纯文本还是带背景，都只吐占位结论，无法产出 `c(CH3COO⁻)=10⁻⁵-10⁻⁹` 与选项 `BD`。
3. **子问(2)方程判分不归一**：探针（`_evalscratch/probe_pdf278_judge.txt`）显示 `SplitPdfAnswerKeywords` 正确拆出 5 个与子问顺序对齐的关键词；子问(2)结论 `CN- + CO2 + H2O → HCN + HCO3-` 与键 `CN- + CO2 + H2O = HCO3- + HCN` 经 `NormalizeConnectorsAndLatex` 后 `→` 已坍缩为 `=`、空格已去，但**产物顺序相反**（`hcn+hco3-` vs `hco3-+hcn`）→ 子串不命中。`AllKeywordsHit`(AND) 即使 5 子问全解对也 = False（卡在子问(2)），而 `IsAnswerCorrect`(OR) = True——证实 AND 门守住了 precision，但 pdf-278 因此无法判正。
4. 子问(1)(3)(4)已能正确命中：(4)的 `9.76` 经子串包含命中键 `9.7`（探针已验证 IsAnswerCorrect=True），无需改动。

### 全库多部分题分布（`_evalscratch/scan_mp_fired.py`，基于 checkpoint `routing_fallbacks`）
- 全库 757 题中 `IsMultiPart`（含 ≥2 个 `(N)` 子问标记）= **58 道**。
- 其中 **27 道已解对**（`gatewayCorrect=True`）——零回归硬边界，不可动。
- **2 道引擎开火但答错**（整题 `Ok=true` 占位阻塞）：**pdf-278**（equilibrium 占位）、pdf-272（curve 读数占位，K.4 已登记降级）。
- **29 道引擎未开火**（整题 `Ok!=true`，分发器已兜底过，因子问未解返回 null）。
- 结论："占位阻塞分发"的收益面 = 2 道；放宽后仅 pdf-278 有望判正（pdf-272 缺曲线能力）。

### 既有相关 spec / 台账
- `.trae/specs/multi-part-subquestion-dispatch`（P1-2）：分发骨架已交付，结论 0/27 全覆盖（纯基础设施）。本 spec 在此骨架之上解决"占位阻塞"与 pdf-278 子问能力缺口。
- `data-defect-register.md` K.1（27 道多部分未解台账）、K.4（占位噪声）、K.8（61 闭包冻结）。pdf-278 不在原 K.1 的 27 道清单内，是路由扫描新发现，亦不在 61 闭包内（已核对 `unsolved_registered_ids.txt` 不含 pdf-278）。

## Functional Requirements
- **FR-1（占位静默）**：当题干被 `MultiPartUtility.IsMultiPart` 判定为多部分时，已知的单部分占位求解器（至少 `TryJudgeIonizationShift`；curve 读数占位同理）不得返回 `Ok=true` 的部分占位结论，应返回 null，使整题 `Ok!=true` 从而让分发链路接管。非多部分题行为完全不变。
- **FR-2（分发接管）**：pdf-278 整题求解返回 null 后，`TryDispatchMultiPartAsync` 被激活，正确拆出 5 个子问并逐子问调用 `CallEngineAsync`。
- **FR-3（子问 1/2/3/4 复用现有求解器）**：子问(1)酸性排序、(2)离子方程、(3)酸碱性、(4)水解 pH 由现有求解器产出正确结论（探针已验证纯文本可解）；无需新增求解器，但需确保逐子问路由 taskType 正确。
- **FR-4（子问 5 新增求解能力）**：子问(5)需同时产出 ① 弱酸根浓度表示式 `c(A⁻) = c(H⁺) - c(OH⁻)`（对 pH=5 的稀醋酸 → `10⁻⁵ - 10⁻⁹`）；② 使弱电解质电离程度增大的操作多选（加热、加水稀释 → BD；加 HCl、加冰醋酸 → 不选）。该能力必须泛化，不得硬编码 pdf-278；对无法确定的情形返回 null（不蒙数）。
- **FR-5（聚合全对铁律）**：任一子问求解返回 null 或结论未命中，则整体返回 null（不产出部分答案）；仅当 5 子问全部解出且聚合结论通过 AND 判分才采纳。
- **FR-6（multi_part 方程顺序无关判分）**：在 multi_part 的 `AllKeywordsHit` AND 判分路径上，对形如化学方程式的关键词，两侧同侧重的物质顺序视为等价（`A+B=C+D` 与 `A+B=D+C`、`B+A=C+D` 均命中），使子问(2)产物顺序不影响命中。该扩展不改变 `IsAnswerCorrect` OR 路径与非 multi_part 题。
- **FR-7（采纳门）**：`BuildGatewayAnswerAsync` 的 multi_part 分支维持 `AllKeywordsHit` AND 全对才采纳（[GatewayVsLlmEvaluationService.cs:706](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L706)），凡采纳必全对。

## Non-Functional Requirements
- **NFR-1（零回归）**：全库 `LOST=0`、`harm=0`、`precision=1`；27 道已解对多部分题 `gatewayCorrect` 保持 True。
- **NFR-2（不蒙数）**：子问(5)求解器与所有改动在无法唯一确定时返回 null，绝不猜测。
- **NFR-3（无单题硬编码）**：新增规则不针对 pdf-278 特判。
- **NFR-4（先聚焦后全量）**：先用聚焦 eval（仅重跑 pdf-278 + 受影响占位题）验证，再跑全量 757 回归；禁止在所有任务完成前先跑全量。

## Constraints
- **Technical**: 改动限定在 `LlmGatewayService`（分发/占位守卫）、`MultiPartUtility`（如需）、`GatewayVsLlmEvaluationService`（multi_part AND 判分扩展）、新增子问(5)求解器及其数据；不得改动运行中的 API 进程锁定的 Debug DLL（用 Release 构建/测试）。
- **Business**: `precision=1` / `harm=0` / `LOST=0` 是不可突破的红线；不回退纯 LLM。
- **Dependencies**: 依赖 P1-2 已交付的 `TryDispatchMultiPartAsync` / `MultiPartUtility` / multi_part 采纳门（已存在，无需重建）。

## Assumptions
- 子问(4)的 `9.76` 子串命中键 `9.7` 在全量 eval 中稳定成立（探针已验证；若全量出现波动再回头处理）。
- `SplitPdfAnswerKeywords` 对 pdf-278 的 5 关键词拆分（顺序对齐）在全量数据加载时与探针一致（探针已用反射实测）。
- 方程顺序无关判分的扩展在 multi_part 路径上不会引入假阳性：因为仅在关键词呈现"方程两侧用 = 连接"时启用集合等价比较，且两侧物质集合必须严格相等（系数也要一致），化学上等价才判命中。

## Acceptance Criteria

### AC-1: pdf-278 引擎判正
- **Type**: `rule`
- **Given**: pdf-278 经改动后的网关求解
- **When**: 运行聚焦 eval（基线 `after_vsepr509` 剥离 pdf-278 后重跑，tag 如 `pdf278_mp`）
- **Then**: pdf-278 的 `gatewayCorrect=True`、`gatewayUsedEngine=True`、`gatewayTruthAdopted=True`、`gatewayResultProvenance=multi_part`
- **Pass Condition**: `cmp after_vsepr509 pdf278_mp` 显示 pdf-278 `GAINED`（solved 由 pureLlm-only 变为引擎判正），且 `LOST=[]`
- **Evidence**: `_evalscratch/` 下 cmp 输出 + checkpoint 记录

### AC-2: 全库零回归
- **Type**: `rule`
- **Given**: 所有任务完成后
- **When**: 跑全量 757 题 eval（tag 如 `pdf278_full`）并 `cmp after_vsepr509 pdf278_full`
- **Then**: `LOST=[]`、`harm=0`、`precision=1`
- **Pass Condition**: cmp 输出 `LOST (0): []` 且 metrics harm=0 prec=1
- **Evidence**: 全量 snapshot 与 cmp 输出

### AC-3: 27 道已解对多部分题不回归
- **Type**: `rule`
- **Given**: 全库 27 道 `gatewayCorrect=True` 的多部分题（`scan_mp_fired.py` 输出清单）
- **When**: 全量 eval 后比对
- **Then**: 这 27 道的 `gatewayCorrect` 全部保持 True，`gatewayConclusion` 可由原结论或等价聚合结论替换但判分不变
- **Pass Condition**: 27/27 保持 True；有单元回归守卫测试锁定该集合
- **Evidence**: 回归测试 + 全量 checkpoint 比对脚本输出

### AC-4: 多部分占位不再阻塞分发
- **Type**: `rule`
- **Given**: 题干为多部分且含"电离平衡移动/加热/稀释"等触发 `TryJudgeIonizationShift` 的文本（如 pdf-278 整题，以及合成的多部分反例）
- **When**: 调用 `CallEngineAsync` 整题求解
- **Then**: `TryJudgeIonizationShift` 对多部分题干返回 null，整题 `Ok != true`，进而触发分发链路
- **Pass Condition**: 单元测试断言 pdf-278 整题经改动后 `TryJudgeIonizationShift`（或整题 `CallEngineAsync` 主结论 provenance）不再返回 equilibrium 占位，且分发器被激活
- **Evidence**: 新增单元测试

### AC-5: multi_part 方程顺序无关判分
- **Type**: `rule`
- **Given**: multi_part 聚合结论含方程 `CN- + CO2 + H2O → HCN + HCO3-`，答案关键词含 `CN- + CO2 + H2O = HCO3- + HCN`
- **When**: 走 multi_part 的 `AllKeywordsHit` AND 判分
- **Then**: 该关键词被判定命中（两侧物质集合等价）
- **Pass Condition**: 单元测试断言 `AllKeywordsHit`/multi_part 采纳对产物顺序互换的方程返回命中；且对**不等价**方程（如 `CN- + CO2 = HCO3- + HCN` 缺 H2O）返回不命中
- **Evidence**: 新增单元测试（正例 + 反例，precision 守卫）

### AC-6: 子问(5)求解器正确性与不蒙数
- **Type**: `rule`
- **Given**: 子问(5)文本（pH=5 稀醋酸求 c(CH3COO⁻) + 电离程度增大的操作多选）
- **When**: 求解器处理
- **Then**: 产出包含 `10^-5 - 10^-9`（或归一化等价）与选项 `BD` 的结论；对缺少 pH / 缺少酸根信息 / 非弱电解质的输入返回 null
- **Pass Condition**: 单元测试断言 pdf-278 子问(5)结论命中关键词 `10^-5 - 10^-9 BD`；反例（无 pH、无酸根）返回 null
- **Evidence**: 新增单元测试

### AC-7: 子问(5)求解器泛化性
- **Type**: `rubric`
- **Dimension**: 新求解器对同类弱酸/弱碱题的泛化能力与无幻觉性
- **Scale**: 1-5
- **Anchors**: 1 = 硬编码 pdf-278 特判，换题即错或乱蒙；3 = 仅对 pdf-278 正确，对相近变体部分正确但偶有蒙数；5 = 规则清晰（c(A⁻)=c(H⁺)-c(OH⁻) + 电离程度影响因素表），对同类变体正确、对反例一律返回 null
- **Pass Threshold**: >= 4
- **Evidence**: 单元测试覆盖 ≥2 个同类变体（如 pH=4 的稀 HF、pH=6 的稀 NH4+ 水解场景）正确，≥2 个反例（无 pH、缺酸根、非电解质）返回 null

## Open Questions
- [ ] **OQ-1（范围确认）**：本 spec 目标含 G1（pdf-278 判正，需 FR-4 子问5新求解器 + FR-6 判分扩展）。若用户希望先只交付 G2（占位静默基础设施，pdf-278 仍判错但不再吐占位结论）作为第一步，可将 FR-4/FR-6 拆为后续 spec。请在 Approve 时确认是否一并交付 G1。
- [ ] **OQ-2（子问5选择判定方式）**：子问(5)的 BD 多选判定，是新建独立的"电离程度影响因素"选择规则，还是复用/扩展现有 concept-facts / choice 域？实现阶段选定，以"泛化 + 不蒙数"为准。
- [ ] **OQ-3（FR-6 扩展边界）**：方程顺序无关判分是否应在本 spec 结束后评估推广到非 multi_part 的方程题（潜在解锁更多方程题）？本 spec 内不做，仅在结论中留观察项。
