# 图形结构化数据接入解题管线 - Implementation Plan

> 实施顺序：共享接入层 → graph（先打通一条端到端）→ curve → molecule → 评估判分 → 整体验证。
> 每个引擎先单元测试（构造已知输入，不依赖 LLM），再临时 harness 跑真实题验证。
>
> **状态裁决（2026-11-22 审计同步）**：本 spec 的 12 个任务当前均为 `pending`、无 `review.md`。其中 **molecule 与 curve 图像资产 → 选择题判分的能力已由主设计 `chem-engine-question-solving` 的 Task 43（`MoleculeChoiceValidator`）与 Task 44（`CurveChoiceValidator`）旁路落地**——`CallChoice` 已接收 `imageAssets` 并接入这两个校验器，生产可解 molecule/curve 保底题（真题诚实 `null`，零假阳性）。**未覆盖**：① `graph` 结构化资产路由（`GraphSolver` 适配层，对应本 spec Task 3–5）——当前 graph 47 题结构化命中 0，仍为 C 类覆盖率缺口；② 本 spec 规划的「ImageAsset 迁移共享命名空间」（Task 1）等架构调整未执行。结论：molecule/curve 目标已旁路达成，graph 及架构迁移留作下一阶段 backlog；本 spec 任务标记保持 `pending`（如实反映未执行），待后续决定是补做 graph 还是正式废弃本 spec 方案。

## Task 1: 迁移 ImageAsset 到共享命名空间 + 网关增加 imageAssets 入参
- **Status**: `pending`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 把 `ImageAsset` 类型从 `CogniThink.TutorBrain.Application.Evaluation` 移到网关层可见的共享位置（如 `CogniThink.TutorBrain.Application.Gateway` 或新建 `Application/Common`），更新 `QaTestCase`、`PdfQuestionItem`、`GenPdfQuestions` 的引用与 `using`。
  - `LlmGatewayService.ProcessStudentMessageAsync` 增加可选参数 `IReadOnlyList<ImageAsset>? imageAssets = null`（默认 null = 旧行为，向后兼容）；`ParseStudentInputAsync` 同步传递（暂不消费，仅打通参数流）。
  - `GatewayController.ProcessStudentMessageAsync` 调用处保持不传（线上默认 null）。
- **Acceptance Criteria Addressed**: AC-1, AC-9
- **Test Requirements**:
  - `rule` TR-1.1: `dotnet build` 0 error，0 个因命名空间迁移导致的编译错误。证据：build 输出。
  - `rule` TR-1.2: 不传 imageAssets 时网关行为不变（无新分支被触发）。证据：grep 确认 CallEngineAsync 未新增分支前，默认路径与原逻辑一致。

## Task 2: 无资产回归 + 运行时反射冒烟
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - `dotnet build` Api 项目；建临时 console 项目反射调用 `GenPdfQuestions`，确认 `total=545/withImages=206/withImageAssets=106`、资产类型 graph47/curve39/molecule42 不变。
  - 确认无资产题走原路径（不读取 imageAssets）。
- **Acceptance Criteria Addressed**: AC-1, AC-9
- **Test Requirements**:
  - `rule` TR-2.1: 反射输出 `total=545 withImageAssets=106`，资产类型计数与基线一致。证据：临时项目运行输出。
  - `rule` TR-2.2: build 0 error。证据：build 输出。
- **Notes**: 复用上次反射验证方法（临时项目 + 拷 data 目录 + FrameworkReference）。

## Task 3: 新建 GraphSolver 适配层（payload → Core 引擎）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `GraphSolver`（Api 层，引用 Chem.Core）：输入 `ImageAsset`(graph payload) + 候选物种列表（来自题干/选项）+ 模式（assign / verify_options）。
  - 标签映射：识别占位标签（甲/乙/丙、A/B/C、X/Y/Z 等）→ 映射为单字母 A-Z 序列；已知物质节点（命中白名单/化学式）与操作节点（电解/沉淀/滤渣等）不参与赋值映射。
  - assign 模式：构造 `ProcessFlowRequest`/`OrganicInferenceRequest`（edges→Steps，cond→Conditions），调对应 Core 引擎，返回 `Assignment` + 溯源步骤。
  - verify_options 模式：对每个选项物质组，验证图中每条边的一步转化是否可行（调 Core 反应可行性规则 / `ChemicalInferenceService` / 知识库），返回逐选项判定。
  - 映射失败 / 引擎无解 → 如实返回空（Ok=null），不蒙数。
- **Acceptance Criteria Addressed**: AC-2, AC-3, AC-4
- **Test Requirements**:
  - `rule` TR-3.1: 单元测试——构造一个已知可解的小型转化图（3 节点 + 候选物种），assign 模式返回正确 Assignment。证据：dotnet test 通过。
  - `rule` TR-3.2: 单元测试——构造不可唯一解的图，返回 Underdetermined/NoSolution 且 FinalResult 非臆造。证据：dotnet test 通过。
  - `rule` TR-3.3: 中文标签"甲/乙/丙"被正确映射为字母后引擎接受（不被 IsLabelToken 拒绝）。证据：单元测试断言映射结果。
- **Notes**: 先写单元测试（不依赖 LLM），候选物种手动传入。真实题候选物种提取在 Task 4/5 处理。

## Task 4: 网关接入 graph 分支 + 评估传入 ImageAssets
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 3
- **Description**:
  - `CallEngineAsync` 增加 `case "graph"` 分支，调用 `GraphSolver`，把结果包装为 `EngineResponse`。
  - 候选物种与模式的获取：复用网关 LLM 解析（在 `EngineRequest` 或解析环节提取候选物种 / 识别是选择题选项验证），或先用规则从题干+选项抽取。确保 graph 题能被分类到 graph task_type（在 `ClassifyTaskDeterministic` 或 LLM 解析中识别有图转化题）。
  - `GatewayVsLlmEvaluationService.EvaluateAsync`：对有 graph 资产的题，把 `c.ImageAssets` 传入 `ProcessStudentMessageAsync`。
- **Acceptance Criteria Addressed**: AC-2, AC-3, AC-7
- **Test Requirements**:
  - `rule` TR-4.1: graph 题经网关能进入 graph 分支并返回非空 EngineResponse（Solved 或如实无解）。证据：临时 harness 跑 pdf-015/018/064 的日志。
  - `rule` TR-4.2: 评估对有 graph 资产题传入了 imageAssets（非 null）。证据：harness/日志确认参数传递。

## Task 5: graph 真实题端到端验证
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 4
- **Description**:
  - 写临时 harness（或扩展评估）跑 pdf-015（选项验证）、pdf-018（赋值）、pdf-064（成分）三道，记录引擎结论与判分结果。
  - 确认至少 1 道判分命中正确（AC-2/AC-3 evidence），无解题如实返回（AC-4 evidence）。
- **Acceptance Criteria Addressed**: AC-2, AC-3, AC-4
- **Test Requirements**:
  - `rule` TR-5.1: pdf-015 选项验证能选出正确选项（或如实返回无解不答错）。证据：harness 输出。
  - `rule` TR-5.2: 边界题（知识库无法赋值）返回 Ok=null，不产生错误结论被判正。证据：harness 输出。

## Task 6: 新建 CurveQuery 引擎（精确数值查询）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `CurveQuery`（Core 或 Api 层）：输入 curve payload + 查询意图（`value_at_x` / `extrema` / `intersection` / `point_at`）+ 参数。
  - 实现：线性插值查值（用 series 点集）、极值点检测、两 series 交点检测、最近点读取。
  - 精度约束：有标注轴方向（`xAxisLabeled`/`yAxisLabeled`=true）返回实际数值；无标注轴方向返回归一化相对位置，并在结论中标注"该轴无刻度，为相对值"。
  - 用 xTicks/yTicks 做点坐标↔实际值的线性标定（点坐标已是标定时的实际值，ticks 用于校验）。
- **Acceptance Criteria Addressed**: AC-5, AC-4
- **Test Requirements**:
  - `rule` TR-6.1: 单元测试——构造已知点集，value_at_x 插值结果与期望误差 < 1e-6；extrema 返回正确极值点。证据：dotnet test。
  - `rule` TR-6.2: 单元测试——无标注轴查询返回相对位置且结论含精度标注。证据：dotnet test。
  - `rule` TR-6.3: 用 pdf-077 payload 查拐点 x，结果与 payload 一致（≈0.1）。证据：单元测试用真实 payload。

## Task 7: 网关接入 curve 分支 + 查询意图传递
- **Status**: `pending`
- **Priority**: medium
- **Depends On**: Task 6
- **Description**:
  - `CallEngineAsync` 增加 `case "curve"` 分支，调 `CurveQuery`。
  - 查询意图与参数从题干+选项解析（LLM 解析环节产出，或规则识别问法："拐点""交点""n(Cl₂)=0.12 时"）。解析不出具体查询时返回 Ok=null（不猜）。
  - 评估对有 curve 资产题传入 imageAssets。
- **Acceptance Criteria Addressed**: AC-5, AC-7
- **Test Requirements**:
  - `rule` TR-7.1: pdf-077 经网关能查询到拐点数值并进入结论。证据：harness 输出。
  - `rule` TR-7.2: 无法解析查询意图时返回 Ok=null，不臆造数值。证据：单元/边界测试。

## Task 8: 新建 MoleculeAnalyzer 引擎（RDKit 结构特征）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `MoleculeAnalyzer`（Core 层，用 `GraphMolWrap` RDKit）：输入 molecule payload（smiles 或 molblock）。
  - 计算：不饱和度（Degree of Unsaturation）、分子式、官能团识别（RDKit 功能团模式匹配）、中心原子杂化（VSEPR：从成键数+孤对近似）、极性（基于对称性/偶极近似）。
  - 输出结构化特征字典 + 计算步骤；RDKit 解析失败返回空（不蒙数）。
- **Acceptance Criteria Addressed**: AC-6, AC-4
- **Test Requirements**:
  - `rule` TR-8.1: 单元测试——SOCl₂（smiles）返回杂化 sp³、不饱和度等正确特征。证据：dotnet test 断言。
  - `rule` TR-8.2: 单元测试——无效 smiles 返回空/失败标记，不抛异常、不臆造。证据：dotnet test。
  - `rule` TR-8.3: 至少 3 种常见官能团（羟基/羧基/苯环）能被识别。证据：dotnet test。
- **Notes**: Chem.Core 已引用 RDKit（MoleculeStructureService 在用），确认可在 Core 内调用。杂化/极性为高中近似，满足判题需求即可。

## Task 9: 网关接入 molecule 分支
- **Status**: `pending`
- **Priority**: medium
- **Depends On**: Task 8
- **Description**:
  - `CallEngineAsync` 增加 `case "molecule"` 分支，调 `MoleculeAnalyzer`，把结构特征包装进 `EngineResponse`。
  - 特征作为上下文辅助解题：若题目问杂化/极性/官能团，特征可直接命中判分；否则作为精确上下文。
  - 评估对有 molecule 资产题传入 imageAssets。
- **Acceptance Criteria Addressed**: AC-6, AC-7
- **Test Requirements**:
  - `rule` TR-9.1: pdf-199 SOCl₂ 经网关返回含杂化/极性特征的结论。证据：harness 输出。
  - `rule` TR-9.2: 与题干计算弱相关的 molecule 题（如 pdf-140），引擎返回特征但不臆造与题无关的结论。证据：harness 输出。

## Task 10: 补充结构化判分
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 4, Task 7, Task 9
- **Description**:
  - 在 `GatewayVsLlmEvaluationService` 判分逻辑中，为结构化结论增加比对：
    - graph：Assignment（标签→物质）或选项可行性结论与标准答案关键词/选项字母比对。
    - curve：引擎返回的数值与标准答案数值比对（带容差，如 ±1% 或末位）。
    - molecule：结构特征（杂化/极性/官能团）与标准答案关键词比对（规范化匹配）。
  - 保持现有 `IsAnswerCorrect` 作为兜底；结构化命中优先。
- **Acceptance Criteria Addressed**: AC-8
- **Test Requirements**:
  - `rule` TR-10.1: 至少 1 道题通过结构化判分（数值或特征）命中正确，区别于纯关键词路径。证据：判分日志。
  - `rule` TR-10.2: 数值比对带容差，标准答案数值在容差内判正。证据：单元测试。

## Task 11: 评估产出 106 子集量化对比
- **Status**: `pending`
- **Priority**: medium
- **Depends On**: Task 10
- **Description**:
  - 扩展 `EvaluateAsync`（或加可选参数）支持只跑有资产的 106 题子集；摘要区分每类资产（graph/curve/molecule）的覆盖率、结构化正确率、VL 正确率、harm 数。
  - 跑一次 106 子集评估，产出报告（接口输出 + 落盘 json）。
- **Acceptance Criteria Addressed**: AC-7
- **Test Requirements**:
  - `rule` TR-11.1: 评估摘要含每类资产的 coverage/structuredAccuracy/vlAccuracy/harm 字段。证据：评估输出。
  - `rule` TR-11.2: 106 子集跑完无未捕获异常。证据：评估运行完成。
- **Notes**: 需 VL 端点可用；106×2 ≈ 200+ 次 LLM 调用。若端点不可用，记录为 blocked 而非 fail。

## Task 12: 全量构建 + 反射 + 回归终验 + 文档
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 11
- **Description**:
  - 全量 `dotnet build` 0 error；反射验证数量不变。
  - 回归：无资产题路径未受影响（抽查）。
  - 更新 `POC_REPORT.md`：新增"引擎接入"章节，记录三类引擎能力、评估对比结果、上线状态。
  - 清理一次性勘察脚本（`_dump_payload_samples.py`、`_sample_asset_questions.py`、`_inspect_payload_quality.py`）。
- **Acceptance Criteria Addressed**: AC-1, AC-9
- **Test Requirements**:
  - `rule` TR-12.1: `dotnet build` 0 error。证据：build 输出。
  - `rule` TR-12.2: 反射 `total=545 withImageAssets=106` 不变。证据：反射输出。
  - `rubric` TR-12.3: 文档完整度；scale 1-5；anchors 1=缺失关键结果, 3=有结果缺分析, 5=能力/结果/上线状态齐全；threshold >= 4；evidence: POC_REPORT.md 新增章节。
