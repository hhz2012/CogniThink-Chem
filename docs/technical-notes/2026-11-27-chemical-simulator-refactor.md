# 化学模拟器重构变更摘要

- **日期**：2026-11-27
- **主题**：chemical-simulator-architecture（统一契约 / 多步推理 / 平衡数值 三主体类）
- **依据**：[.trae/specs/chemical-simulator-architecture/chemical-simulator-design.md](../../.trae/specs/chemical-simulator-architecture/chemical-simulator-design.md) §11 任务清单
- **性质**：纯加性重构，零回归；未修改 `Application/Gateway/` 与任何 `data/*.json`

---

## 1. 背景与目标

原 `ChemicalSimulationEngine` 同时承担「反应推演引擎」与「HTTP/旧 DTO 契约」两层职责，输出事件缺少化学依据（Basis）、优先级（Priority）、反应进度（Extent）等可溯源字段，且无法表达「分步加料推理」「平衡态数值（pH / Ksp 残留 / 沉淀转化 K）」。

本次重构按设计文档落地三个主体类，并用薄适配器保留旧路径，红线约束：

- 不蒙数：常数只来自 `acid-constants.json` 或题给 `query.given`；缺数据 → `Status=Underdetermined`
- 不修改 `Application/Gateway/`、不修改 `data/*.json`（线上判分路径不动）
- 旧端点 / 旧 DTO / 旧构造签名保持不变，现有测试全绿

## 2. 变更范围

### 2.1 新增源文件（5 个，均在 `src/CogniThink.TutorBrain.Api/Application/Chemistry/`）

| 文件 | 作用 |
|---|---|
| `ChemicalSimulatorContracts.cs` | 统一输入/输出契约：`ChemicalSystemInput`、`SpeciesInput`（Moles / Concentration? / Volume? / Role?）、`ChemicalConditions`（含 `ReturnConsidered`）、`SystemState`、`SpeciesState`、`SimulationTrace`、`ReactionEvent`（富化 +Extent / Basis / Priority）、`ConsideredCandidate` |
| `ChemicalSimulator.cs` | 从旧引擎逐字迁入全部推演逻辑（RunStage / CheckConditions / EvaluateCondition / ComputeExtent / Score / TypePriority / MetalActivity / Apply / BuildOutput），输入 `ChemicalSystemInput` → 输出 `SimulationTrace` |
| `ChemicalMultiStepReasoningSimulator.cs` | 第二主体类：`Reason`（按计划逐步进给，产出状态轨迹 + 可选平衡数值挂载）、`QueryState`、`WhatIf`（纯函数分支）、`Search`（委托 `ChemicalReverseInferenceEngine` 反查 + 正向回放验证） |
| `EquilibriumStateSimulatorContracts.cs` | 第三主体类契约：`EquilibriumQuery`、`EquilibriumQuantity` 枚举、`ConversionSpec`、`EquilibriumRequest`、`EquilibriumNumericalResult`、`EquilibriumDetermination` |
| `EquilibriumStateSimulator.cs` | 平衡数值计算：弱酸/弱碱 pH、盐类水解、缓冲溶液（Henderson–Hasselbalch）、Ksp 残留、沉淀转化 K（`ConstantSpec.Ratio` 精确有理）、粒子排序/守恒/酸碱性（委托 Core `AqueousEquilibriumEngine.Infer`） |

### 2.2 修改源文件（3 个，均加性 / 兼容）

| 文件 | 变更 |
|---|---|
| `Program.cs` | 新增 3 行 DI 注册（`ChemicalSimulator` / `EquilibriumStateSimulator` / `ChemicalMultiStepReasoningSimulator`），`ChemicalSimulationEngine` 注册保持不变 |
| `Api/Controllers/ChemistryController.cs` | 新增构造参数注入三个服务；新增 3 个端点：`POST trace`、`POST reason`、`POST equilibrium`。旧 `simulate` / `reverse` / `health` 端点零改动 |
| `Application/Chemistry/ChemicalSimulationEngine.cs` | 改写为薄适配器：旧 DTO（`SimulationRequest`/`Result`/`Event` 等）与构造签名 `(ChemicalKnowledgeBase, ReactionEquationParser)` 完整保留，内部 `new ChemicalSimulator(kb, parser)`，`Simulate()` 做字段双向映射；`SkippedReactions` 由 `SimulationTrace.SkippedReasons` 还原 |

### 2.3 新增测试文件（3 个，共 13 个用例）

- `tests/CogniThink.TutorBrain.Tests/ChemicalSimulatorTests.cs`：Fe+CuSO₄ 富化事件断言（Basis/Priority/Extent）、未知物质 Warning 边界、`ReturnConsidered` 结构
- `tests/CogniThink.TutorBrain.Tests/ChemicalMultiStepReasoningSimulatorTests.cs`：Reason 轨迹增长 + Numerical 挂载 pH、AddSpecies + WhatIf 不可变性 + QueryState、Search 返回带 Verification 的候选
- `tests/CogniThink.TutorBrain.Tests/EquilibriumStateSimulatorTests.cs`：弱酸 pH≈2.88、弱碱 pH≈11.12、缺常数 → Underdetermined（Ph=null、无蒙数）、温度非 25℃ → Underdetermined、AgCl→AgI 转化 K、题给 pH 直取；常数经构造函数注入 `AcidConstantsData`，不依赖文件系统

## 3. 架构与调用链变化

```
旧：HTTP simulate ──► ChemicalSimulationEngine（引擎+契约一体）
新：HTTP simulate ──► ChemicalSimulationEngine（薄适配器）──► ChemicalSimulator（纯引擎）
    HTTP trace    ──► ChemicalSimulator.Simulate            ──► SimulationTrace（新契约）
    HTTP reason   ──► ChemicalMultiStepReasoningSimulator   ──► Reason（内部调用 ChemicalSimulator）
                                                            └─► 步内 Numerical 挂 EquilibriumStateSimulator.Compute
    HTTP equilibrium ─► EquilibriumStateSimulator.Compute   ──► Core AqueousEquilibriumEngine.Infer（排序/守恒/酸碱性）
```

- `ChemicalMultiStepReasoningSimulator` 构造：`(ChemicalSimulator, ChemicalKnowledgeBase, EquilibriumStateSimulator?=null, ChemicalReverseInferenceEngine?=null)`，反向引擎缺省懒加载。
- `EquilibriumStateSimulator` 构造：`(AcidConstantsData?=null)`，为 null 时通过 `RuleStore.Load().Query<AcidConstantsData>("acid-constants")` 加载；测试注入构造数据以隔离文件系统。

## 4. 关键设计要点（红线保证）

1. **字节级兼容**：适配器把旧引擎逻辑逐字迁入 `ChemicalSimulator`，反应选择与终态判定逻辑不变；现有测试断言的 `Events[].ReactionId`/`StageIndex`、`FinalSolutionColor`、`SkippedReactions[].ReactionId` 全部保持。
2. **不蒙数**：`EquilibriumStateSimulator.Compute` 温度≠25℃、缺常数、缺溶质浓度 → 字段留空 + 加 `Warning` + `Status=Underdetermined`；`ToDouble` 仅在最终展示时调用，中间量走 `ConstantSpec.ToRational` / `Ratio` 精确有理。
3. **纯函数 WhatIf**：`WhatIf(from, alternative)` 从快照物种 `FinalMoles` 构造新输入，不携带浓度（设计 §6.5 限制），不修改原轨迹。
4. **数值挂载**：`Reason` 在某步 `Numerical` 非空时，按物种 Id 匹配源输入携带 `Concentration`/`Volume`，调用 `EquilibriumStateSimulator.Compute` 并把结果挂到 `StateSnapshot.Numerical`。
5. **路由不冲突**：新端点使用 `trace`/`reason`/`equilibrium`，避开既有 `simulate`/`reverse`，避免 `AmbiguousMatchException`。

## 5. 测试与验证

- **单元测试**：全量 `dotnet test` → **1773 通过 / 0 失败 / 0 跳过**（基线 1760 + 新增 13）。
- **构建**：Api 与 EvalRunner 的 Debug / Release 均 **0 错误**。
- **端到端冒烟**：Release 启动服务后实测三个新端点均返回正确结果——`/equilibrium` ph=2.88、`/trace` 命中 `R-FE-CUSO4`、`/reason` 轨迹 2 步且 Numerical pH=2.88；健康端点 200，DI 全部解析、无路由冲突。
- **基准评测**（`CogniThink.TutorBrain.EvalRunner`，tag `after_simulator`，757 题全量，`completed=757 / failed=0`）与今日基线 `after_expop` 逐字段一致：

  | 指标 | 基线 after_expop | 本次 after_simulator | 变化 |
  |---|---|---|---|
  | total | 757 | 757 | 0 |
  | gatewayCorrect | 704 | 704 | 0 |
  | engineSolved | 704 | 704 | 0 |
  | coverage | 0.93 | 0.93 | 0 |
  | **precision** | **1** | **1** | **0** |
  | **harmCount** | **0** | **0** | **0** |
  | bothWrong | 53 | 53 | 0 |

  → 红线 `precision=1 / harm=0 / LOST=0 / WORSEN=0` 全部保持，线上判分路径零回归。

  证据文件：`_evalscratch/checkpoint_after_simulator_summary.txt`、`_evalscratch/snapshot_after_simulator.json`、`_evalscratch/checkpoint_after_simulator.jsonl`（757 行）。

## 6. 兼容性与运行影响

- **纯加性**：无破坏性变更；旧 `SimulationRequest`/`SimulationResult` 等 DTO、旧构造签名、旧 HTTP 端点均不变。
- **启动开销**：启动时多构造 3 个单例服务（`ChemicalSimulator` 内部不再重复建引擎，`EquilibriumStateSimulator` 懒加载常数），对启动时间无显著影响。
- **运行时**：新端点按需调用；旧路径行为不变。

## 7. 踩坑记录

1. **`.wip_disabled` 自动禁用**：`ChemicalMultiStepReasoningSimulator.cs` 曾在某次编译失败后被环境进程重命名为 `.wip_disabled` 并排除编译，导致一次「构建 0 错误」为假阳性。恢复后必须验证该文件以 `.cs` 存在且确实被编译（构建输出 + 文件存在性双重确认）。
2. **路由冲突预判**：设计文档 §9 初稿写新端点 `[HttpPost("simulate")]`，与既有 `POST api/chemistry/simulate` 冲突会抛 `AmbiguousMatchException`；已改用 `trace`/`reason`/`equilibrium`。
3. **全局枚举序列化**：`Program.cs` 已全局注册 `JsonStringEnumConverter`，故端点可直接接收字符串枚举（如 `quantities: ["Ph"]`、`kind: "AddSpecies"`），无需额外配置。
