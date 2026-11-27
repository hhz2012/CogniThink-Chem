# 化学模拟器统一架构 — 实现 Review

## Review 目标

独立核验「化学模拟器统一架构」编码实现是否满足 `.trae/specs/chemical-simulator-implementation/spec.md` 的全部验收标准（AC-1 ~ AC-9），并确认红线（不碰 `Application/Gateway/`、不写 `data/`）未被突破。

## 评审产物

- 规约：`.trae/specs/chemical-simulator-implementation/spec.md`
- 任务：`.trae/specs/chemical-simulator-implementation/tasks.md`
- 设计源：`.trae/specs/chemical-simulator-architecture/chemical-simulator-design.md`

## Review 方式

1. 委派独立只读子代理（fresh context）对照设计与规约核验全部 AC、红线、构建、测试；
2. 主代理补齐独立评审未覆盖的两项证据：全量测试套件、live 端点 smoke；
3. 主代理执行干净 Release 构建确认无新增警告。

## 检查点结果

| AC | 结论 | 证据 |
|---|---|---|
| AC-1 契约落地 | pass | `ChemicalSimulatorContracts.cs` 与 `EquilibriumStateSimulatorContracts.cs` 逐字段对应设计 §2/§3/§7.2；`EquilibriumQuantity` 恰好七项，无非法枚举值。独立评审确认。 |
| AC-2 ChemicalSimulator | pass | `ChemicalSimulator.cs`（648 行）构造注入 KB+Parser，`Simulate` 输出富化 `ReactionEvent`；`ChemicalSimulatorTests` 通过；`/trace` smoke 返回正确事件与终态。 |
| AC-3 适配器等价 | pass | `ChemicalSimulationEngine.cs` 委托 `_simulator.Simulate` + `MapToLegacyResult`，旧 DTO 保留；既有 `ChemicalSimulationEngineTests` 全通过；全量 1787 测试 0 回归。 |
| AC-4 多步推理 | pass | `ChemicalMultiStepReasoningSimulator.cs` 含 `Reason`/`QueryState`/`WhatIf`/`Search`，签名与设计 §6 一致；对应测试通过。 |
| AC-5 平衡数值 + 不蒙数 | pass | 缺常数/缺输入 → `Underdetermined` + warning；转化 K 来自 `ConstantSpec.Ratio`（Ksp 比值），无硬编码；常数唯一来源 `acid-constants.json`/`query.Given`。`/equilibrium` smoke：pH 8.88 正确、推导可溯源、定性空时附「不猜」warning。 |
| AC-6 红线 | pass | 本实现新增/修改文件限于 `Application/Chemistry/`、`Program.cs`、`ChemistryController.cs`、`tests/`；无 `Application/Gateway/` 新增、无 `data/**.json` 写入。工作区中 Gateway/data 的既有改动属其他工作线，已区分不计入。 |
| AC-7 单元测试 | pass | 三个新测试类存在并通过（含于 1787 全量）。 |
| AC-8 构建与零回归 | pass | Debug 构建 0 错误；干净 Release 构建 0 错误、仅 1 条既有 NETSDK1206 警告（RDKit，无关）、无 CS 警告；全量 `dotnet test`：**1787 通过 / 0 失败 / 0 跳过**（65s）。 |
| AC-9 可访问性 | pass | `Program.cs` 注册三个单例；`ChemistryController` 暴露 `POST /trace` 与 `POST /equilibrium`；live smoke 两端点 HTTP 200 返回正确新契约。 |

## 红线检查结论

本实现**未触碰 `Application/Gateway/`（无新增、无修改），未写入任何 `data/**.json`**。`acid-constants.json` 只读。工作区中 `data/`、`Application/Gateway/` 的未提交改动来自其他并发工作线（data-repair、solve-projection、statement-judge 等），与本实现无关，不计入本规约回归。

## 补充证据（独立评审未覆盖项）

- **全量测试**：主代理运行 `dotnet test tests/.../CogniThink.TutorBrain.Tests.csproj -c Debug --no-build` → 1787 通过 / 0 失败 / 0 跳过。
- **live 端点 smoke**：API 运行于 `http://localhost:5099`，`POST /api/chemistry/trace`（Fe:1 + CuSO4:1）→ 1 事件、终态浅绿 + 紫红铜沉淀；`POST /api/chemistry/equilibrium`（0.1 M CH3COONa）→ Determined、pH 8.88、[OH⁻]=7.56e-6、推导步骤与 evidenceKeys 正确。
- **Release 构建**：停服释放锁后 `dotnet build -c Release` → 0 错误、无新增警告。

## Actionable Findings

无。All required checkpoints pass; no actionable finding.

## Review 结论

**pass** — 全部验收标准通过，红线未突破，无待修复项。实现轮可收尾。

## Review History

| 轮次 | 日期 | 结论 | 说明 |
|---|---|---|---|
| 1 | 2026-11-27 | pass | 独立只读子代理核验 AC-1~AC-9 + 红线；主代理补齐全量测试、live smoke、Release 构建证据。无 finding。 |
