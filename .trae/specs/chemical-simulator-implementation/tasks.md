# 化学模拟器统一架构 — 编码实现任务清单

> 派生自已批准设计文档 `chemical-simulator-design.md` §11。每条任务含本地测试要求（TR，类型 `rule`）。证据随实现核验填入。

## Task 1: 新建统一契约文件 `ChemicalSimulatorContracts.cs`

- 优先级：high
- 依赖：无
- Status: completed
- 关联 AC：AC-1
- TR（rule）：文件定义 `ChemicalSystemInput`/`SpeciesInput`/`ChemicalConditions`/`SystemStage`（输入）与 `SimulationTrace`/`ReactionEvent`/`ReactionBasis`/`ReactionPriority`/`ConsideredCandidate`/`SpeciesState`/`SystemState`（输出），字段与设计 §2、§3 一致；旧 DTO 不删除，留在适配器文件中兼容。
- Completion Evidence：`src/CogniThink.TutorBrain.Api/Application/Chemistry/ChemicalSimulatorContracts.cs` 存在（162 行），逐字段比对设计 §2/§3 通过；`dotnet build` 0 错误。

## Task 2: 新建 `ChemicalSimulator` 主体类

- 优先级：high
- 依赖：Task 1
- Status: completed
- 关联 AC：AC-2
- TR（rule）：`ChemicalSimulator` 依赖注入 `ChemicalKnowledgeBase` + `ReactionEquationParser`；实现 `Simulate(ChemicalSystemInput) → SimulationTrace`，流程含条件过滤→候选评分→限量试剂→状态更新→收敛/终止→现象/颜色/沉淀汇总；输出 `ReactionEvent` 含 `Basis`、`Priority`、`Consumed`/`Produced`、`Extent`、`Phenomena`、`SolutionColorAfter`、`Precipitates`、`Gases`。
- Completion Evidence：`ChemicalSimulator.cs` 存在（648 行）；`ChemicalSimulatorTests` 通过；端点 `/trace` smoke 返回 Fe+CuSO4 事件与浅绿/紫红铜终态。

## Task 3: 把 `ChemicalSimulationEngine` 退化为薄适配器

- 优先级：high
- 依赖：Task 2
- Status: completed
- 关联 AC：AC-3
- TR（rule）：旧 DTO（`SimulationRequest`/`SimulationResult` 等）与类签名保留；`Simulate` 内部构造 `ChemicalSystemInput` → 调 `ChemicalSimulator.Simulate` → 映射回旧 `SimulationResult`；旧端点 `/simulate`、`/reverse`、`/coexist`、`/compile` 行为不变。
- Completion Evidence：`ChemicalSimulationEngine.cs`（修改）构造内 `new ChemicalSimulator(kb, parser)` 并委托 `_simulator.Simulate(input)`（第 118–129 行）；既有 `ChemicalSimulationEngineTests` 全通过（适配器等价）；全量 1787 测试 0 回归。

## Task 4: 新建 `ChemicalMultiStepReasoningSimulator`

- 优先级：medium
- 依赖：Task 2
- Status: completed
- 关联 AC：AC-4
- TR（rule）：实现 `Reason`（返回状态轨迹，每步含事件/状态快照/被拒候选）、`QueryState`（按状态查询某物种量/是否存在）、`WhatIf`（纯函数，给定加料返回分支轨迹，不污染原对象）、`Search`（目标导向，复用既有 `ChemicalReverseInferenceEngine`，结果含 `Found`/`Path`/`ReasonTrace`）。
- Completion Evidence：`ChemicalMultiStepReasoningSimulator.cs` 存在（369 行）；`ChemicalMultiStepReasoningSimulatorTests` 通过。

## Task 5: 新建 `EquilibriumStateSimulatorContracts.cs`

- 优先级：high
- 依赖：Task 1
- Status: completed
- 关联 AC：AC-1、AC-5
- TR（rule）：定义 `EquilibriumQuantity` 枚举（`Ph`/`IonConcentrations`/`KspResidual`/`PrecipitationConversion`/`ConcentrationOrdering`/`Conservations`/`AcidityQualitative` 七项，命名精确匹配 `EquilibriumStateSimulator` 内部）+ `EquilibriumQuery`（`Quantities`/`Given`/`Conversion`/`OrderSpecies`）+ `EquilibriumRequest` + `EquilibriumNumericalResult` + `ConversionSpec`。
- Completion Evidence：`EquilibriumStateSimulatorContracts.cs` 存在（89 行），`EquilibriumQuantity` 七项与设计 §7.2 表一致（避免 `ParticleOrdering`/`Acidity` 等非法枚举值导致 400）。

## Task 6: 新建 `EquilibriumStateSimulator` 主体类

- 优先级：high
- 依赖：Task 5
- Status: completed
- 关联 AC：AC-5
- TR（rule）：依赖注入 `ChemicalKnowledgeBase` + `ReactionEquationParser`；实现 `Compute(ChemicalSystemInput, EquilibriumQuery) → EquilibriumNumericalResult`；pH 用 `acid-constants.json` 的 Ka 计算（不蒙数，缺常数→warning）；离子浓度、Ksp 残留、沉淀转化 K=Ksp1/Ksp2（有理文本）、排序/守恒/酸碱性委托 Core 并映射；缺数据返回 `Underdetermined` 绝不猜。
- Completion Evidence：`EquilibriumStateSimulator.cs` 存在（514 行）；`EquilibriumStateSimulatorTests` 通过；端点 `/equilibrium` smoke：0.1 M CH3COONa → Determined、pH 8.88、[OH⁻]=7.56e-6、推导步骤正确、evidenceKeys=[CH3COOH]；定性项空时附「不猜」warning，无伪造数值。

## Task 7: `Program.cs` DI 注册 + `ChemistryController` 新端点

- 优先级：high
- 依赖：Task 2、4、6
- Status: completed
- 关联 AC：AC-9
- TR（rule）：`Program.cs` 注册 `ChemicalSimulator`/`EquilibriumStateSimulator`/`ChemicalMultiStepReasoningSimulator` 为单例（在 `AddControllers()` 之后）；`ChemistryController` 新增 `POST /api/chemistry/trace`（入参 `ChemicalSystemInput`，出参 `SimulationTrace`）与 `POST /api/chemistry/equilibrium`（入参 `EquilibriumRequest`，出参 `EquilibriumNumericalResult`）；CORS 已开，跨域可调用。
- Completion Evidence：`Program.cs` 第 69–71 行注册三个单例；`ChemistryController` 第 202 行 `/trace`、第 231 行 `/equilibrium`；两端点 HTTP 200 且返回新契约（落盘 JSON 已读回校验）。

## Task 8: 三个新类的单元测试

- 优先级：high
- 依赖：Task 2、4、6
- Status: completed
- 关联 AC：AC-7
- TR（rule）：`ChemicalSimulatorTests` 覆盖典型反应（Fe+CuSO4、HCl+NaOH 等）与现象/沉淀；`ChemicalMultiStepReasoningSimulatorTests` 覆盖 Reason/QueryState/WhatIf/Search；`EquilibriumStateSimulatorTests` 覆盖 pH 数值、守恒、缺常数 Underdetermined、不蒙数边界。
- Completion Evidence：三个测试类存在并通过（合计在 1787 全量中）；模拟器相关筛选 104 项全绿。

## Task 9: 构建 + 全量测试 + 红线核验（编码后执行）

- 优先级：high
- 依赖：Task 1–8
- Status: completed
- 关联 AC：AC-3、AC-6、AC-8、AC-9
- TR（rule）：
  1. `dotnet build` Release 0 错误、0 新增警告；
  2. 全量 `dotnet test` 0 失败、0 跳过；
  3. `git status`/`git diff --stat` 显示本实现新增/修改文件限于 `Application/Chemistry/`、`Program.cs`、`ChemistryController.cs`、`tests/`，无 `Application/Gateway/` 新增、无 `data/**.json` 修改由本实现引入；
  4. 端点 smoke `/trace`、`/equilibrium` 返回正确契约且无伪造。
- Completion Evidence：
  - 构建：Debug build 0 错误（仅 4 条既有警告，与本实现无关）；停服释放锁后干净 **Release build 0 错误、无 CS 新增警告**（仅 1 条既有 NETSDK1206/RDKit 警告）。
  - 全量测试：`dotnet test tests/.../CogniThink.TutorBrain.Tests.csproj` → **1787 通过 / 0 失败 / 0 跳过**，耗时 65s。
  - 红线：独立评审核对 `git status`/`git diff --stat`，本实现文件全部落在允许路径；无 `Application/Gateway/` 新增、无 `data/**.json` 写入。
  - 端点：`/trace` smoke（Fe+CuSO4 → 浅绿/紫红铜）、`/equilibrium` smoke（CH3COONa → pH 8.88/不蒙数）均通过。

## Task 10: 独立 Review 门禁

- 优先级：high
- 依赖：Task 9
- Status: completed
- 关联 AC：全部
- TR（rule）：由独立上下文（fresh agent，只读）对照 `spec.md` + 设计文档核验实现与红线，出具 `review.md`；结论为 pass 方可收尾。
- Completion Evidence：委派独立只读子代理核验 AC-1~AC-9 + 红线 → 结论 **pass，无 actionable finding**；主代理补齐独立评审未覆盖的全量测试（1787 全绿）与 live 端点 smoke 证据；详见 `review.md`（第 1 轮，2026-11-27）。
