# 化学模拟器统一架构 — 编码实现规约

## 参考

- 已批准的设计文档：`.trae/specs/chemical-simulator-architecture/chemical-simulator-design.md`（含 §2–§11 全部契约与实现任务分解）。
- 设计轮任务已全部完成（`chemical-simulator-architecture/tasks.md`），用户已审阅设计并批准进入编码。
- 本规约是设计批准后的**实现轮**：把设计文档 §2–§9 的契约与 §11 的任务清单落到代码，并通过零回归红线核验。

## 问题与目标

把「化学模拟器统一架构」从设计变为可运行、可测试、零回归的代码：三个主体模拟器类（`ChemicalSimulator`、`ChemicalMultiStepReasoningSimulator`、`EquilibriumStateSimulator`）共用统一输入/输出契约；现有 `ChemicalSimulationEngine` 退化为薄适配器；新增只读端点；全程不修改 `Application/Gateway/` 与 `data/`，不改变既有解题判分口径。

## 非目标

- 不改 `Application/Gateway/**`、不改 `data/**.json`（红线）。
- 不替换 `CogniThink.Chem.Core` 下既有文件（`AqueousEquilibriumEngine` 等保持不动，由新模拟器委托调用）。
- 不重做前端（前端模拟器控件为另一条已完成的工作线）。
- 不提交代码（仅实现与核验，提交由用户决定）。

## 验收标准（Acceptance Criteria）

### rule

- **AC-1（契约落地）**：存在 `ChemicalSimulatorContracts.cs`（输入 `ChemicalSystemInput`/`SpeciesInput`/`ChemicalConditions` + 输出 `SimulationTrace`/`ReactionEvent`/`ReactionBasis`/`ReactionPriority` 等）与 `EquilibriumStateSimulatorContracts.cs`（`EquilibriumQuantity` 七项枚举 + `EquilibriumQuery`/`EquilibriumNumericalResult`/`ConversionSpec`），字段与设计 §2、§3、§7.2 一一对应。
- **AC-2（ChemicalSimulator）**：`ChemicalSimulator.Simulate(ChemicalSystemInput)` 可运行，输出含富化 `ReactionEvent`（Basis、Priority、Consumed/Produced、Extent、现象/颜色/沉淀/气体）与 `SystemState`；覆盖设计 §4.2–§4.5。
- **AC-3（适配器等价）**：`ChemicalSimulationEngine` 退化为委托 `ChemicalSimulator` 的薄适配器；既有 `ChemicalSimulationEngineTests` 全部通过，证明对外行为不变。
- **AC-4（多步推理）**：`ChemicalMultiStepReasoningSimulator` 提供 `Reason`（状态轨迹）、`QueryState`（状态查询）、`WhatIf`（纯函数分支）、`Search`（目标导向，复用既有逆向引擎），与设计 §6 一致。
- **AC-5（平衡数值 + 不蒙数）**：`EquilibriumStateSimulator.Compute` 支持七项查询；缺常数时返回 `Underdetermined` + warning，绝不凭空给数（FR-7B）；pH/推导/evidenceKeys 可溯源。
- **AC-6（红线：不碰 Gateway/数据）**：本实现的全部新增/修改文件限于 `src/**/Application/Chemistry/`、`Program.cs`（DI）、`Api/Controllers/ChemistryController.cs`（端点）、`tests/**`；无任何对 `Application/Gateway/` 的新增或对 `data/**.json` 的写入。
- **AC-7（单元测试）**：存在并通过 `ChemicalSimulatorTests`、`ChemicalMultiStepReasoningSimulatorTests`、`EquilibriumStateSimulatorTests` 三个新测试类。
- **AC-8（构建与零回归）**：`dotnet build` Release/Debug 0 错误；全量测试套件通过、0 失败、0 跳过。
- **AC-9（可访问性）**：`Program.cs` 注册三个模拟器为单例；`ChemistryController` 暴露 `POST /api/chemistry/trace` 与 `POST /api/chemistry/equilibrium`，且 smoke 调用返回正确契约数据。

## 约束与假设

- 设计决策 D-1～D-5 已锁定，本实现严格遵循，不重新决策。
- 工作区含其他工作线的未提交改动（`data/`、`Gateway/`、前端等）；红线核验只针对**本实现引入的改动**，不把其他工作线的既有改动计入本规约的回归。
- 既有 Core 引擎（`AqueousEquilibriumEngine` 等）行为不在本规约修改范围；其对某些输入形态返回 NoSolution 属预期「不蒙数」表现。
