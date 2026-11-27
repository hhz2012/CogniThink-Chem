# 化学模拟器统一架构 — 详细设计

> 本文件是 `spec.md` 验收 AC-1 ~ AC-4、AC-6、AC-8 的设计交付物，粒度到 C# 签名级，下一轮编码可直接照做。
> 关联：[object-responsibility-map.md](file:///d:/github/CogniThink-Chem/.trae/specs/chemical-simulator-architecture/object-responsibility-map.md)（对象职责分类）。
> 本轮**不写代码**；本文件中的 C# 代码块是设计契约（目标形态），非已实现代码。

---

## 1. 设计目标与原则

1. **一个正向模拟主体类** `ChemicalSimulator`，吸收现有 `ChemicalSimulationEngine`；旧类退化为薄兼容适配器，签名与行为不变（现有测试/控制器零改动）。
2. **一个多步推理主体类** `ChemicalMultiStepReasoningSimulator`，在 `ChemicalSimulator` 之上提供状态轨迹、what-if 分支、目标导向搜索。
3. **统一输入** `ChemicalSystemInput`（含 `ChemicalConditions`），三个主体类共用；旧 `SimulationRequest` 由适配器映射到它。
4. **输出富化**：每步反应带结构化化学依据（`Basis`）与优先级（`Priority`），定量（`Extent` + 剩余量）。
5. **不蒙数**：所有反应/产物来自知识库；无命中返回空并说明。
6. **零回归**：不修改 `Application/Gateway/` 与任何 JSON；live 判分路径不受影响。
7. **平衡态数值有数据支撑、不蒙数**：`EquilibriumStateSimulator` 只读 `acid-constants.json`（Ka/Kb/Ksp/Kw，人教版教材真值）与题给数值；常数/输入缺一即返回欠定（`Underdetermined`）并留 Warnings，绝不猜数。

---

## 2. 统一输入契约

### 2.1 `ChemicalConditions` — 集中所有环境开关

替换原先散落在 `SimulationRequest` 上的平级布尔字段。所有开关默认值与现有 `SimulationRequest` 一致（`Aqueous=true`、`Air=true`、其余 false、`MaxSteps=24`），保证映射后行为等价。

```csharp
namespace CogniThink.TutorBrain.Application.Chemistry;

/// <summary>模拟环境条件。未显式给出的条件按"不满足"处理，避免引擎凭空加热或点燃。</summary>
public sealed class ChemicalConditions
{
    /// <summary>水溶液体系；为 true 时自动补充足量水作溶剂</summary>
    public bool Aqueous { get; set; } = true;

    public bool Heating { get; set; }
    public bool Ignition { get; set; }
    public bool Catalyst { get; set; }
    public bool Electricity { get; set; }
    public bool Light { get; set; }
    public bool Concentrated { get; set; }
    public bool Dilute { get; set; }

    /// <summary>空气环境；为 true 时自动补充足量氧气</summary>
    public bool Air { get; set; } = true;

    public bool Acidic { get; set; }
    public bool Alkaline { get; set; }

    /// <summary>最大反应步数，防止病态数据导致死循环</summary>
    public int MaxSteps { get; set; } = 24;

    /// <summary>是否返回每步被考虑但未发生的候选反应（默认关闭，避免噪声）</summary>
    public bool ReturnConsidered { get; set; }
}
```

### 2.2 `SpeciesInput` / `ChemicalSystemInput`

```csharp
/// <summary>参与模拟的初始物种</summary>
public sealed class SpeciesInput
{
    /// <summary>物质 Id、名称、化学式或别名（经知识库解析）</summary>
    public string Substance { get; set; } = string.Empty;

    /// <summary>物质的量（mol），默认 1；水作溶剂时可不填。反应模拟（ChemicalSimulator）以 Moles 为准</summary>
    public double Moles { get; set; } = 1;

    /// <summary>可选：物质的量浓度（mol/L）。平衡数值模拟（EquilibriumStateSimulator）以 Concentration/Volume 为准</summary>
    public double? Concentration { get; set; }

    /// <summary>可选：溶液体积（L）。与 Concentration 配合；仅给 Concentration 时视为单物种浓度</summary>
    public double? Volume { get; set; }

    /// <summary>可选角色标签（如 "待测物"/"滴定剂"），不影响推演，仅用于讲解归因</summary>
    public string? Role { get; set; }
}

/// <summary>分步加料：同一容器内先加一批、反应完后再加下一批</summary>
public sealed class SystemStage
{
    public string Name { get; set; } = string.Empty;
    public List<SpeciesInput> Species { get; set; } = new();
}

/// <summary>
/// 化学体系统一输入：三个模拟器共用。描述"初始有什么物种、各多少量、在什么环境条件下"，
/// 可选分步加料（Stages）。`Moles` 供反应模拟；`Concentration`/`Volume` 供平衡数值模拟。
/// </summary>
public sealed class ChemicalSystemInput
{
    public List<SpeciesInput> Species { get; set; } = new();
    public List<SystemStage> Stages { get; set; } = new();
    public ChemicalConditions Conditions { get; set; } = new();
}
```

> 旧 `SimulationRequest` 的 `Species: List<SimulationSpeciesInput>` 与 `Stages: List<SimulationStage>` 与上述一一对应，适配器做字段映射（见 §5）。

---

## 3. 输出契约

### 3.1 `ReactionBasis` — 结构化化学依据

替代现有 `SimulationEvent.Evidence` 的纯字符串。`KnowledgeIds` 直接取自 `Reaction.KnowledgeIds`（保证溯源真实）；`SelectionReason` 说明为何此反应在本步胜出。

```csharp
/// <summary>一步反应的化学依据（可溯源）</summary>
public sealed record ReactionBasis(
    string ReactionId,
    /// <summary>关联知识点 Id，取自 Reaction.KnowledgeIds；为空表示该反应未挂知识点</summary>
    IReadOnlyList<string> KnowledgeIds,
    /// <summary>本步为何选中此反应：类型优先级、限量试剂判定、条件满足情况</summary>
    string SelectionReason);
```

### 3.2 `ReactionPriority` — 优先级

```csharp
/// <summary>反应优先级（数值越大越优先）</summary>
public sealed record ReactionPriority(
    /// <summary>综合评分（类型优先级 + 反应物数 + 金属活动性 - 可逆/兜底惩罚）</summary>
    double Score,
    /// <summary>在本步候选中的排名（1 = 最优先，实际发生的反应 Rank=1）</summary>
    int Rank);
```

### 3.3 `ReactionEvent` — 一步反应（富化）

```csharp
/// <summary>一次反应事件（可追溯到反应条目与计量依据）</summary>
public sealed record ReactionEvent(
    int Step,
    int StageIndex,
    string StageName,
    string ReactionId,
    string Equation,
    string Type,
    List<string> Conditions,
    List<string> Phenomena,
    /// <summary>反应物消耗量（物质 Id → mol）</summary>
    Dictionary<string, double> Consumed,
    /// <summary>产物生成量（物质 Id → mol）</summary>
    Dictionary<string, double> Produced,
    /// <summary>反应进度（mol）：限量试剂允许的最大反应量</summary>
    double Extent,
    ReactionBasis Basis,
    ReactionPriority Priority,
    string SolutionColorAfter,
    List<string> Precipitates,
    List<string> Gases);
```

### 3.4 `ConsideredCandidate` — 被考虑但未发生的候选

仅当 `Conditions.ReturnConsidered = true` 时填充。

```csharp
/// <summary>本步被考虑但未发生的候选反应及其原因</summary>
public sealed record ConsideredCandidate(
    string ReactionId,
    string Equation,
    double Score,
    /// <summary>未发生原因：条件不满足 / 反应进度≈0 / 被更高优先级胜出</summary>
    string Reason);
```

### 3.5 `SpeciesState` / `SystemState` / `SimulationTrace`

```csharp
/// <summary>某物质的状态快照</summary>
public sealed record SpeciesState(
    string Id,
    string Name,
    string Formula,
    double InitialMoles,
    double FinalMoles,
    string AmountText,
    string State,
    string? Color,
    string? SolutionColor,
    string Solubility,
    string Role);

/// <summary>体系状态汇总</summary>
public sealed class SystemState
{
    public List<SpeciesState> Species { get; set; } = new();
    public string SolutionColor { get; set; } = "无色";
    public List<string> Precipitates { get; set; } = new();
    public List<string> Gases { get; set; } = new();
    public List<string> ResidualGases { get; set; } = new();
    public List<string> Solids { get; set; } = new();
    public List<string> Phenomena { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>正向模拟结果</summary>
public sealed class SimulationTrace
{
    public bool Converged { get; set; }
    public int Steps { get; set; }
    public List<ReactionEvent> Events { get; set; } = new();
    /// <summary>每步被考虑但未发生的候选（按步分组）；ReturnConsidered=false 时为空</summary>
    public List<List<ConsideredCandidate>> Considered { get; set; } = new();
    public List<SpeciesState> InitialSpecies { get; set; } = new();
    public SystemState FinalState { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
    public List<string> SkippedReactions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

> `SimulationTrace` 与旧 `SimulationResult` 信息等价但结构对齐新契约；适配器负责双向映射（见 §5）。

---

## 4. `ChemicalSimulator` 主体类设计

### 4.1 类签名与依赖

```csharp
namespace CogniThink.TutorBrain.Application.Chemistry;

/// <summary>
/// 化学模拟器（正向，唯一主体类）。
/// 给定初始物种与环境条件，反复在知识库中检索"所有反应物都已存在、条件满足"的反应，
/// 按反应类型优先级与金属活动性择一发生，再按方程式系数计算限量试剂与剩余量，
/// 直到没有可发生的反应为止。输出每一步的反应事件（含参与物质、化学依据、优先级、定量）与最终体系状态。
/// 铁律：只使用知识库中已有反应；绝不凭空生成产物。
/// </summary>
public sealed class ChemicalSimulator
{
    private readonly ChemicalKnowledgeBase _kb;
    private readonly ReactionEquationParser _parser;

    public ChemicalSimulator(ChemicalKnowledgeBase kb, ReactionEquationParser parser)
    {
        _kb = kb;
        _parser = parser;
    }

    public SimulationTrace Simulate(ChemicalSystemInput input);
}
```

### 4.2 处理流程（来自现有引擎，迁入新类）

`Simulate` 的主循环与现有 `ChemicalSimulationEngine.RunStage` 完全一致（已验证正确），仅在三处富化输出：

1. **候选收集时**：把每个通过筛选的候选记录 `Score`（已有 `Score(...)`）；未通过筛选的候选在 `ReturnConsidered=true` 时记录原因（条件不满足 / extent≈0）。
2. **择一时**：`chosen` 的 `Rank=1`；其余候选按分数降序赋予 `Rank=2,3,...`（仅当 `ReturnConsidered` 时保留到输出）。
3. **应用反应时**：除现有 `Apply` 产出的 `Consumed`/`Produced` 外，补充 `Extent`、`Basis`、`Priority`。

主循环伪代码（与现有 `RunStage` 等价）：

```text
state = 解析 input.Species（未知物质 → Warnings，跳过）
if input.Conditions.Aqueous: 确保环境 H2O（足量）
if input.Conditions.Air: 确保环境 O2（足量）
RunStage(0, "初始")
for each stage in input.Stages: 加入物种 → RunStage(stageIndex, stage.Name)
BuildOutput → SimulationTrace
```

每步内部（`RunStage`）：

```text
for step in 1..MaxSteps:
    candidates = []
    for reaction in kb.Reactions (未在本阶段 fired):
        stoich = parser.Get(reaction)
        if 反应物不全可用: 若 ReturnConsidered 记录"反应物不足"，continue
        if 可逆且气体被溶液吸收: skip（记入 SkippedReactions）
        failure = CheckConditions(reaction, stoich)
        if failure != null: 若 ReturnConsidered 记录 failure，continue
        extent = ComputeExtent(reaction, stoich, state)
        if extent <= ε: 若 ReturnConsidered 记录"反应进度≈0"，continue
        candidates.Add({reaction, stoich, extent, Score=Score(reaction,stoich)})
    if candidates 为空: return converged=true
    chosen = candidates 按 Score 降序、再按 Id 升序的第一个
    event = Apply(chosen)  // 富化：补充 Extent / Basis / Priority(Rank=1)
    if ReturnConsidered: 其余候选按分数降序赋 Rank，记入 Considered[step-1]
    fired.Add(chosen.ReactionId)
return converged=false  // 达步数上限
```

### 4.3 `Basis` 生成规则

```text
Basis.ReactionId    = reaction.Id
Basis.KnowledgeIds  = reaction.KnowledgeIds（直接取，不臆造）
Basis.SelectionReason =
    $"类型 {reaction.Type}（优先级 {TypePriority}）；" +
    $"反应物齐备：{每个反应物的余量/系数}；" +
    $"按系数比可进行 {extent} mol 反应" +
    (limiting != null ? $"，{limiting} 为限量试剂" : "")
```

与现有 `BuildRationale` 文本一致，但拆成结构化字段；`KnowledgeIds` 独立暴露便于前端/讲解溯源。

### 4.4 `Priority` 生成规则

```text
Priority.Score = Score(reaction, stoich)   // 现有算法：TypePriority + 反应物数*2 + MetalBonus - 可逆/兜底惩罚
Priority.Rank  = 本步候选中的排名（chosen=1）
```

现有 `Score` / `TypePriority` / `MetalBonus` / `MetalActivity` 逻辑**原样迁入**，不改变数值（保证行为等价）。

### 4.5 定量字段

- `Extent`：现有 `ComputeExtent` 结果（限量试剂允许的最大反应进度，mol）。
- `Consumed[Id]`：`extent * 系数`；`state[id].Remaining` 扣减。
- `Produced[Id]`：`extent * 系数`；`state[id].Remaining` 增加（新产物则创建）。
- 终态 `SpeciesState.FinalMoles`：现有 `BuildOutput` 计算（环境物种按净生成量展示）。

---

## 5. 吸收合并方案（现有 `ChemicalSimulationEngine` → 薄适配器）

### 5.1 迁移映射表

现有 `ChemicalSimulationEngine` 的成员逐项处理：

| 现有成员 | 处理 | 去向 |
|---|---|---|
| 字段 `_kb`, `_parser` | 迁移 | `ChemicalSimulator` 同名字段 |
| 常量 `Epsilon`, `EnvironmentMoles` | 迁移 | `ChemicalSimulator` |
| `Simulate(SimulationRequest)` | **保留为适配器入口**，内部映射后委托新类 | 旧类（适配器） |
| `RunStage(...)` | 迁移（主循环） | `ChemicalSimulator.RunStage`（私有） |
| `ResolveInputs` / `ResolveStageInputs` / `AddInput` / `EnsureEnvironment` | 迁移（输入解析） | `ChemicalSimulator` |
| `AllReactantsAvailable` | 迁移 | `ChemicalSimulator` |
| `CheckConditions` / `EvaluateCondition` / `IsCatalystSubstance` / `TryParseRelativeAmount` / `RelativeAmountSatisfied` | 迁移（条件判定） | `ChemicalSimulator` |
| `ComputeExtent` | 迁移 | `ChemicalSimulator` |
| `Score` / `TypePriority` / `MetalActivity` / `MetalBonus` | 迁移（优先级，数值不变） | `ChemicalSimulator` |
| `BuildRationale` | 迁移并改造为 `BuildBasis`（产出 `ReactionBasis`） | `ChemicalSimulator` |
| `Apply` | 迁移并富化（补充 `Extent`/`Basis`/`Priority`） | `ChemicalSimulator` |
| `BuildOutput` / `ComposeFinalState` / `ComputeSolutionColor` / `IsPrecipitate` / `Has` / `Round` / `FormatMoles` | 迁移（结果汇总） | `ChemicalSimulator` |
| 私有类 `SpeciesAmount`, `Candidate` | 迁移 | `ChemicalSimulator`（私有） |

### 5.2 旧 `ChemicalSimulationEngine` 退化为薄适配器

旧类**保留类名、构造函数签名、`Simulate` 方法签名与返回类型**，确保 `ChemistryFixture`、`ChemistryController`、现有测试零改动：

```csharp
namespace CogniThink.TutorBrain.Application.Chemistry;

/// <summary>
/// 兼容适配器（保留旧签名）。新代码请使用 <see cref="ChemicalSimulator"/>。
/// 内部把 SimulationRequest 映射为 ChemicalSystemInput，委托 ChemicalSimulator，再把结果映射回 SimulationResult。
/// </summary>
public sealed class ChemicalSimulationEngine
{
    private readonly ChemicalSimulator _simulator;

    public ChemicalSimulationEngine(ChemicalKnowledgeBase kb, ReactionEquationParser parser)
    {
        _simulator = new ChemicalSimulator(kb, parser);
    }

    public SimulationResult Simulate(SimulationRequest request)
    {
        var input = MapToInput(request);            // SimulationRequest → ChemicalSystemInput
        var trace = _simulator.Simulate(input);
        return MapToLegacyResult(trace, request);   // SimulationTrace → SimulationResult
    }

    private static ChemicalSystemInput MapToInput(SimulationRequest request) { /* 字段一一映射 */ }
    private static SimulationResult MapToLegacyResult(SimulationTrace trace, SimulationRequest request) { /* 字段一一映射 */ }
}
```

`MapToInput` / `MapToLegacyResult` 是纯字段映射（`SimulationSpeciesInput`→`SpeciesInput`、`SimulationStage`→`SystemStage`、`SimulationRequest` 上的环境布尔→`ChemicalConditions`；反向同理），**不改变任何数值**。`SimulationEvent` 的旧字段（`Consumed`/`Produced`/`Evidence` 等）从新 `ReactionEvent` 映射填充，旧 `Evidence` 字符串由 `Basis` 拼接生成，与现有 `BuildRationale` 文本一致 → 现有断言（如 `result.Events` 中 `ReactionId`、`FinalSpecies`、`FinalSolutionColor`）全部保持通过。

### 5.3 反向引擎的兼容

`ChemicalReverseInferenceEngine` 构造函数注入 `ChemicalSimulationEngine`（`ChemistryFixture.Reverse = new(Knowledge, Simulator)`）。保持该构造函数不变；其内部调用 `_simulator.Simulate(simulationRequest)` 走适配器，行为不变。后续可新增一个接受 `ChemicalSimulator` 的重载，但本轮不动反向引擎（避免扩大改动面）。

---

## 6. `ChemicalMultiStepReasoningSimulator` 主体类设计

### 6.1 类签名与依赖

```csharp
/// <summary>
/// 化学多步推理模拟器。在 <see cref="ChemicalSimulator"/> 之上：
/// ① 按步骤计划进给，产出状态轨迹；
/// ② 对任一状态做 what-if 分支（纯函数，不污染原轨迹）；
/// ③ 目标导向搜索：从目标产物/现象反查候选路径并正向回放验证。
/// </summary>
public sealed class ChemicalMultiStepReasoningSimulator
{
    private readonly ChemicalSimulator _simulator;
    private readonly ChemicalKnowledgeBase _kb;
    private readonly EquilibriumStateSimulator? _equilibrium;

    public ChemicalMultiStepReasoningSimulator(ChemicalSimulator simulator, ChemicalKnowledgeBase kb, EquilibriumStateSimulator? equilibrium = null)
    {
        _simulator = simulator;
        _kb = kb;
        _equilibrium = equilibrium;
    }

    public ReasoningResult Reason(ReasoningPlan plan, ChemicalSystemInput initial);
    public StateSnapshot? QueryState(IReadOnlyList<StateSnapshot> trajectory, Func<StateSnapshot, bool> predicate);
    public SimulationTrace WhatIf(StateSnapshot from, ChemicalConditions alternative);
    public IReadOnlyList<ReasoningCandidate> Search(ChemicalSystemInput initial, ReasoningGoal goal);
}
```

### 6.2 状态与计划模型

```csharp
/// <summary>体系在某一步结束后的状态快照</summary>
public sealed class StateSnapshot
{
    public int StepIndex { get; set; }
    public string StageName { get; set; } = string.Empty;
    /// <summary>该步结束后的体系状态（物种量/颜色/沉淀/气体）</summary>
    public SystemState State { get; set; } = new();
    /// <summary>该步发生的反应事件（空表示该步无反应）</summary>
    public List<ReactionEvent> Events { get; set; } = new();
    /// <summary>可选：该步结束后体系的平衡态数值（pH/离子浓度/Ksp 残留等），由 ReasoningStep.Numerical 触发（见 §7）</summary>
    public EquilibriumNumericalResult? Numerical { get; set; }
}

public enum ReasoningStepKind { AddSpecies, SetConditions, Assert }

/// <summary>推理计划中的一步</summary>
public sealed class ReasoningStep
{
    public ReasoningStepKind Kind { get; set; }
    /// <summary>AddSpecies：追加的物种（在当前状态基础上加入，再模拟）</summary>
    public List<SpeciesInput>? Species { get; set; }
    /// <summary>SetConditions：替换或增量修改的环境条件</summary>
    public ChemicalConditions? Conditions { get; set; }
    /// <summary>Assert：对当前状态的断言描述（不推进反应，仅记录到证据）</summary>
    public string? Assertion { get; set; }
    /// <summary>可选：在该步模拟完成后，对所得状态求哪些平衡态数值（见 §7 EquilibriumQuery）；结果挂到本步 StateSnapshot.Numerical</summary>
    public EquilibriumQuery? Numerical { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ReasoningPlan
{
    public List<ReasoningStep> Steps { get; set; } = new();
}

/// <summary>搜索目标（可组合，多项为 AND）</summary>
public sealed class ReasoningGoal
{
    public List<string> TargetProducts { get; set; } = new();
    public string? SolutionColor { get; set; }
    public string? PrecipitateColor { get; set; }
    public string? Gas { get; set; }
    public List<string> Phenomena { get; set; } = new();
    public int MaxDepth { get; set; } = 3;
    public int MaxCandidates { get; set; } = 6;
}

public sealed record ReasoningCandidate(
    List<ReasoningStep> Steps,
    SimulationTrace Trace,
    ReverseVerification Verification);   // 复用现有反向引擎的验证记录类型

/// <summary>推理端点请求体（HTTP / 测试入口）：初始体系 + 推理计划</summary>
public sealed class ReasonRequest
{
    public ChemicalSystemInput Initial { get; set; } = new();
    public ReasoningPlan Plan { get; set; } = new();
}
```

### 6.3 `Reason` — 状态轨迹

算法：从 `initial` 出发，依次执行 `plan.Steps`：

- **AddSpecies**：把 `step.Species` 加到当前状态的物种量上，用当前 `Conditions` 调 `_simulator.Simulate`，取终态作为下一个快照。
- **SetConditions**：用 `step.Conditions` **全量替换**当前条件后，对当前物种状态调 `_simulator.Simulate`。调用方需传完整条件；未传的字段取 `ChemicalConditions` 默认值（`Aqueous`/`Air` 为 true，其余为 false，`MaxSteps=24`）。<br>**不做增量合并**：`ChemicalConditions` 是普通 `bool` 类，默认值无法区分"显式设为 false"与"未设置"，全量替换可消除歧义。若未来需增量修改，可引入 `bool?` 字段的 patch 类型，属后续优化。
- **Assert**：不调模拟器，仅在快照记录断言（用于讲解标注）。
- **Numerical**（若 `step.Numerical != null`）：该步终态确定后，用终态物种量/浓度构造一个 `ChemicalSystemInput`，调 `_equilibrium.Compute(stateInput, step.Numerical)`，把结果挂到本步 `StateSnapshot.Numerical`；若 `_equilibrium` 未注入或数据欠定，留 Warnings，不中断推理。

每步产出一个 `StateSnapshot`，形成 `StateSnapshot[]`。任一步模拟器返回 `Converged=false`（达步数上限）则在 `Warnings` 留痕但继续。

```csharp
public sealed class ReasoningResult
{
    public List<StateSnapshot> Trajectory { get; set; } = new();
    public SystemState FinalState => Trajectory.LastOrDefault()?.State ?? new SystemState();
    public List<string> Evidence { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

### 6.4 `QueryState` — 状态查询

```csharp
public StateSnapshot? QueryState(IReadOnlyList<StateSnapshot> trajectory, Func<StateSnapshot, bool> predicate)
    => trajectory.FirstOrDefault(predicate);
```

薄封装，供上层按条件（如"找到首次出现 Cu 沉淀的步骤"）定位快照。具体谓词由调用方提供（避免在模拟器里硬编码题型）。

### 6.5 `WhatIf` — 纯函数分支

**关键语义**：不修改 `from` 与原轨迹。实现：把 `from.State.Species` 的各物质 `FinalMoles` 深拷贝为新 `ChemicalSystemInput.Species`（`InitialMoles`/`Moles` 取 `FinalMoles`），应用 `alternative` 条件，调 `_simulator.Simulate` 返回独立 `SimulationTrace`。

```csharp
public SimulationTrace WhatIf(StateSnapshot from, ChemicalConditions alternative)
{
    // 深拷贝起点：不引用 from 的对象
    var branchInput = new ChemicalSystemInput
    {
        Species = from.State.Species
            .Where(s => s.FinalMoles > Epsilon)
            .Select(s => new SpeciesInput { Substance = s.Id, Moles = s.FinalMoles })
            .ToList(),
        Conditions = alternative   // 替代条件
    };
    return _simulator.Simulate(branchInput);  // 全新 trace，与原轨迹无关
}
```

环境物种（H2O/O2）由模拟器按 `alternative.Aqueous/Air` 自动补充，无需显式拷贝。单测断言：调用 `WhatIf` 后，原 `trajectory` 各快照的 `State.Species` 量值不变。

**已知限制**：`StateSnapshot.State.Species`（`SpeciesState`）只含 `FinalMoles`，不含 `Concentration`/`Volume`，因此 WhatIf 分支输入只带 `Moles`、不带浓度——分支仅用于**反应推演**（mol / 现象 / 状态），不挂载平衡数值。平衡数值请通过 `Reason` 轨迹的 `ReasoningStep.Numerical` 挂载（见 §6.3 与 §7.6）。若未来需 WhatIf 也支持 pH，需先扩展 `SpeciesState` 保留浓度并按体积重算，属后续优化。

### 6.6 `Search` — 目标导向搜索（复用现有逆向引擎）

**不重新实现**反查与验证逻辑，而是委托现有 `ChemicalReverseInferenceEngine`：构造一个 `ReverseInferenceRequest`（`Known` = `initial.Species` 解析后的 Id，`TargetProducts` = `goal.TargetProducts`，观察字段 = goal 的颜色/沉淀/气体/现象，`MaxDepth`/`MaxCandidates` = goal 的值，`Conditions` 由 initial 映射），调用其 `Infer`，把返回的 `ReverseCandidate` 映射为 `ReasoningCandidate`：

- `Steps`：从候选的 `UpstreamPaths` 反推出需要补加的物种步骤（缺失反应物 → AddSpecies 步骤）。
- `Trace`：候选的 `Verification` 已包含正向回放的模拟结果（现有引擎在 `Verify` 内调了 `_simulator.Simulate`）；复用该回放，或用推导出的 `Steps` 重新调 `_simulator.Simulate` 生成 trace（二选一，优先复用已有回放避免重复计算）。
- `Verification`：直接取反向引擎的 `Verification`（`Matched` 标志 + 匹配观察）。

**不蒙数保证**：候选全部来自知识库反应（反向引擎的铁律）；`Verification.Matched=false` 的候选如实标注，不强行给结论。无命中返回空列表。

> 本轮设计只定义委托关系；下一轮实现时若 `ReverseInferenceEngine` 内部回放的 trace 类型是旧 `SimulationResult`，需在映射层转成 `SimulationTrace`（复用 §5.2 的映射逻辑，或让反向引擎也接受 `ChemicalSimulator`——后者留作后续优化，本轮不扩大改动）。

---

## 7. `EquilibriumStateSimulator` 平衡态数值模拟器

> 设计依据：`spec.md` D-5 / FR-9A / FR-9B / AC-8。数据来源为已落地的 `acid-constants.json`（人教版，25 ℃，`ConstantSpec` 精确有理存储，经 [RuleStore](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Rules/RuleStore.cs#L99) 加载为 [AcidConstantsData](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Rules/RuleStore.cs#L192)）。定性/排序/守恒/沉淀转化委托 Core [AqueousEquilibriumEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/AqueousEquilibriumEngine.cs#L75)（该引擎已做精确有理比较，但不输出绝对 pH/浓度数值）；绝对 pH 与离子浓度由本模拟器新增计算。

### 7.1 类签名与依赖

```csharp
namespace CogniThink.TutorBrain.Application.Chemistry;

/// <summary>
/// 平衡态数值模拟器（第三个主体类）。
/// 给定体系物种（带 Concentration/Volume）与平衡查询项，基于 acid-constants.json 的 Ka/Kb/Ksp/Kw
/// 计算 pH、离子浓度、Ksp 残留浓度、沉淀转化平衡常数；定性/排序/守恒委托 Core AqueousEquilibriumEngine。
/// 铁律：常数只来自 acid-constants.json 或题给；缺常数/缺输入/歧义一律返回欠定（Underdetermined），绝不猜数。
/// </summary>
public sealed class EquilibriumStateSimulator
{
    private readonly AcidConstantsData _constants;   // RuleStore.Load().Query<AcidConstantsData>("acid-constants")

    public EquilibriumStateSimulator()
        : this(RuleStore.Load().Query<AcidConstantsData>("acid-constants")) { }

    /// <summary>测试用：注入常数数据，避免依赖文件系统</summary>
    public EquilibriumStateSimulator(AcidConstantsData constants) { _constants = constants; }

    public EquilibriumNumericalResult Compute(ChemicalSystemInput input, EquilibriumQuery query);
}
```

> 构造时一次性加载常数（与 `AqueousEquilibriumEngine.Infer` 内部加载口径一致）；注入构造函数仅服务单测，不改变行为。`AqueousEquilibriumEngine` 是 `public static`，直接静态调用，无需注入。

### 7.2 输入与输出模型

```csharp
/// <summary>平衡态数值查询项（可组合，多项 AND）</summary>
public enum EquilibriumQuantity
{
    Ph,                       // 体系 pH 数值
    IonConcentrations,        // 主要物种的平衡浓度数值（mol/L）
    KspResidual,              // Ksp 残留离子浓度
    PrecipitationConversion,  // 沉淀转化平衡常数 K = Ksp1/Ksp2
    ConcentrationOrdering,    // 粒子浓度排序（委托 Core）
    Conservations,            // 三大守恒式（委托 Core）
    AcidityQualitative        // 酸碱性定性（委托 Core）
}

/// <summary>平衡态数值查询：对给定体系求什么</summary>
public sealed class EquilibriumQuery
{
    public List<EquilibriumQuantity> Quantities { get; set; } = new();
    /// <summary>题给已知数（如 pH、指定离子浓度、题给 Ksp 覆盖值）；当 acid-constants.json 缺常数时优先用题给</summary>
    public Dictionary<string, double> Given { get; set; } = new();
    /// <summary>沉淀转化时指定原沉淀 / 加入离子 / 目标沉淀（映射到 AqueousRequest.Conversion）</summary>
    public ConversionSpec? Conversion { get; set; }
    /// <summary>要求排序的粒子清单（映射到 AqueousRequest.OrderSpecies）</summary>
    public List<string> OrderSpecies { get; set; } = new();
}

/// <summary>平衡数值端点请求体（HTTP / 测试入口）：体系 + 查询项</summary>
public sealed class EquilibriumRequest
{
    public ChemicalSystemInput Input { get; set; } = new();
    public EquilibriumQuery Query { get; set; } = new();
}

public sealed class ConversionSpec
{
    public string Original { get; set; } = "";   // 原沉淀化学式，如 "AgCl"
    public string AddedIon { get; set; } = "";   // 加入的离子，如 "I^-"
    public string Target { get; set; } = "";     // 目标沉淀，如 "AgI"
}

public enum EquilibriumDetermination { Determined, Underdetermined }

/// <summary>平衡态数值结果</summary>
public sealed class EquilibriumNumericalResult
{
    public EquilibriumDetermination Status { get; set; }
    public double? Ph { get; set; }

    /// <summary>物种 / 离子 → 平衡浓度（mol/L）；只填能确定的项</summary>
    public Dictionary<string, double> IonConcentrations { get; set; } = new();

    /// <summary>Ksp 残留浓度（mol/L）与对应离子</summary>
    public double? KspResidualConcentration { get; set; }
    public string? KspResidualIon { get; set; }

    /// <summary>沉淀转化平衡常数 K = Ksp1/Ksp2（精确有理文本，来自 ConstantSpec.Ratio）；无法确定时为空</summary>
    public string? ConversionConstantK { get; set; }
    public bool? ConversionProceeds { get; set; }   // K > 1

    /// <summary>定性结果（委托 Core AqueousEquilibriumEngine）</summary>
    public List<string> ConcentrationOrdering { get; set; } = new();
    public List<string> ConservationEquations { get; set; } = new();
    public string? AcidityVerdict { get; set; }

    /// <summary>推导步骤（人类可读），每步标注所用常数与来源</summary>
    public List<string> DerivationSteps { get; set; } = new();
    /// <summary>知识锚点：常数 Key（如 "CH3COOH"）/ 规则 Id（如 "SOL-006"），用于溯源</summary>
    public List<string> EvidenceKeys { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

### 7.3 计算目录（高中范围；每项注明数据需求与不蒙数边界）

设 `input.Species` 中每个物种已给 `Concentration`（mol/L）与可选 `Volume`（L）；多物种混合浓度按 Core `MoleCalculator.MolesFromConcentration` 口径折算。常数取 `_constants.Ka/Kb/Ksp/Kw`，中间量用 `ConstantSpec.ToRational()` 精确运算，最后转 `double` 取对数。

| # | 计算项 | 公式 / 算法 | 数据需求 | 不蒙数边界（缺则该字段留空 + Warnings） |
|---|---|---|---|---|
| 1 | **弱酸 pH** | `[H⁺] = √(Ka·c)`；`pH = -lg[H⁺]`（近似条件 `c/Ka ≥ 500`；多元弱酸取一级电离 `Ka1`，忽略二级及以后） | `Ka`（JSON：一元键 `"CH3COOH"`，多元分步键 `"H2CO3:Ka1"` 等）+ 物种 `Concentration` | 物种不在 `Ka` 表 → 欠定；缺 `Concentration` → 欠定；`c/Ka < 500` → 加 Warning（近似失效）并标注结果为近似 |
| 2 | **弱碱 pH** | `[OH⁻] = √(Kb·c)`；`pH = 14 - pOH`。Kb 缺但共轭酸 Ka 在表时：`Kb = Kw/Ka`（`ConstantSpec.Ratio(Kw, Ka)`） | `Kb`（JSON）或对应共轭酸 `Ka` + `Kw` + `Concentration` | 两者皆缺 → 欠定；近似边界同 #1 |
| 3 | **盐水解 pH** | 强碱弱酸盐：`pH = 14 + ½·lg(Kw·c/Ka)`；强酸弱碱盐：`pH = -½·lg(Kw·c/Kb)`。复用现有 `HydrolysisPhCalculator` 口径 | 弱酸 `Ka` 或弱碱 `Kb` + `Kw` + 盐 `Concentration` | 常数缺 → 欠定（与现有计算器返回 null 一致，改为结构化结果） |
| 4 | **缓冲溶液 pH** | Henderson–Hasselbalch：`pH = pKa + lg([A⁻]/[HA])`，`pKa = -lg(Ka)`；`[A⁻]/[HA]` 由两组分的量（同体积即浓度比）得到 | 弱酸 `Ka` + 共轭酸/共轭碱两组分的量（默认两组分同处一溶液即同体积，摩尔比 = 浓度比；若显式给出不同 `Volume`，则按 `Concentration` 比） | 缺 Ka 或缺任一组分量 → 欠定；不自行假设共轭碱来自完全电离 |
| 5 | **Ksp 残留浓度** | 题给 pH：`[OH⁻] = 10^(pH-14)`；对 `M(OH)ₙ`，`c(Mⁿ⁺) = Ksp / [OH⁻]ⁿ`（n 由 `KspExpressions`/电荷判定）。复用 `KspPhResidualSolver` 口径 | `Ksp`（JSON `Ksp` 表，如 `"Mg(OH)2"`）+ `query.Given["pH"]`，或题给 Ksp 覆盖 | Ksp 不在表且题给未给 → 欠定；缺 pH → 欠定；n 无法判定 → 欠定 |
| 6 | **沉淀转化 K** | `K = Ksp(原)/Ksp(新)`，`ConstantSpec.Ratio(kspOriginal, kspTarget)` 精确求商；`K > 1` 可转化 | 委托 `AqueousEquilibriumEngine.Infer`（`AqueousRequest.Conversion` 映射自 `query.Conversion`） | Core 已保证：反应库无转化记录或 Ksp 缺 → `NoSolution`/`Underdetermined`，不猜；原样透传其状态 |
| 7 | **粒子排序 + 三大守恒** | 委托 `AqueousEquilibriumEngine.Infer`（`Solutes`/`OrderSpecies` 映射），取 `Order` 与 `ConservationEquations` | 透传 Core；各溶质需 `Concentration`/`Volume` | Core 已实现：定性判据不可判的对（如 NaHCO₃ 中 c(CO₃²⁻) 与 c(OH⁻)）如实 `Underdetermined`，本模拟器不改写、不补数 |
| 8 | **酸碱性定性** | 委托 `AqueousEquilibriumEngine.Infer`，取 `QuantitativeConclusion` | 透传 Core | 同上 |

> 上表 #3、#5 复用现有 Gateway 计算器的**算法口径**，但本模拟器是**独立实现**（读 JSON 而非解析题干文本），不替换、不触碰 Gateway live 路径（见 §10 零回归）。

### 7.4 不蒙数规则（硬约束）

1. **常数唯一来源**：`_constants`（`acid-constants.json`）或 `query.Given`（题给覆盖值）。禁止硬编码 pKa / 凭记忆填常数。
2. **逐项可观测状态**：每个 `EquilibriumQuantity` 独立判定；某一项欠定不阻断其他项。能确定的字段填值，不能的留空 + Warnings；`Status` = 有任一确定项 → `Determined`，全欠定 → `Underdetermined`。
3. **近似边界透明**：`c/Ka < 500` 等近似失效时，`Warnings` 写明"近似条件 c/Ka=X < 500 不满足，结果为近似值"，不静默吞掉。
4. **不补全组成**：物种缺 `Concentration` 时不假设 0 或默认值（与 `SpeciesInput.Moles=1` 的反应模拟默认值不同——平衡数值不设浓度默认，缺即欠定）。
5. **Core 透传不改写**：委托得到的 `Underdetermined`/`NoSolution` 原样保留，不强行给结论。
6. **温度口径**：`acid-constants.json` 常数为 25 ℃；题给温度显式非 25 ℃ 时无对应温度常数 → 返回欠定，不做温度外推；未给温度时默认 25 ℃。

### 7.5 与现有分散能力的关系（收敛口径，不替换文件）

| 现有位置 | 现状 | 本期处理 |
|---|---|---|
| `HydrolysisPhCalculator`（Gateway） | 盐水解 pH = `14 + lg√(Kw/Ka·c)`，缺常数返回 null | 算法口径迁入本模拟器 §7.3 #3；**Gateway 原文件不动**（live 路径零回归）；后续 spec 再评估迁移 |
| `KspPhResidualSolver`（Gateway） | `c = Ksp/[OH⁻]ⁿ`，Ksp/pH 从题干解析 | 算法口径迁入 §7.3 #5；原文件不动 |
| [AqueousEquilibriumEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/AqueousEquilibriumEngine.cs)（Core） | 精确有理比较 Ka/Kh/Ksp、三大守恒、排序、沉淀转化 K；**不输出绝对 pH/浓度** | 本模拟器**委托**其 `Infer`（排序/守恒/定性/转化），并在其上**新增**绝对数值（#1~#5）。Core 不改动 |

### 7.6 与多步推理模拟器的组合（FR-9B）

- `ChemicalMultiStepReasoningSimulator` 构造函数可选注入 `EquilibriumStateSimulator`（见 §6.1）；`ReasoningStep.Numerical`（见 §6.2）指定该步后求哪些数值。
- `Reason` 每步终态确定后（见 §6.3），把终态物种量/浓度构造成 `ChemicalSystemInput`，调 `_equilibrium.Compute(stateInput, step.Numerical)`，结果挂到 `StateSnapshot.Numerical`。
- `_equilibrium` 未注入或数值欠定时，只在 `Warnings` 留痕，不中断推理轨迹。
- **解耦**：`ChemicalSimulator`（反应量，mol）与 `EquilibriumStateSimulator`（浓度/pH）互不依赖，组合发生在多步推理层，二者可独立使用。

---

## 8. 向后兼容策略汇总

| 现有调用方 | 影响 | 保证 |
|---|---|---|
| `ChemistryFixture.Simulator`（`ChemicalSimulationEngine` 实例） | 旧类变为适配器 | 构造函数与 `Simulate(SimulationRequest)→SimulationResult` 签名不变，测试零改动 |
| `ChemistryFixture.Reverse`（`ChemicalReverseInferenceEngine`） | 不动 | 构造函数不变，内部走适配器，行为不变 |
| `ChemistryController` 现有端点 | 不动 | 注入的还是 `ChemicalSimulationEngine`/`ChemicalReverseInferenceEngine`，响应不变 |
| `Program.cs` DI | 加性注册 | 新增 `ChemicalSimulator`/`ChemicalMultiStepReasoningSimulator` 单例；旧注册保留 |
| 现有测试断言（`result.Events`/`FinalSpecies`/`FinalSolutionColor` 等） | 不变 | 适配器映射保证旧 `SimulationResult` 字段与现有等价 |

---

## 9. DI 注册与可访问性（下一轮实现）

`Program.cs` 加性注册（不删旧注册）：

```csharp
// 新增（加性）
services.AddSingleton<ChemicalSimulator>();
services.AddSingleton<EquilibriumStateSimulator>();
services.AddSingleton<ChemicalMultiStepReasoningSimulator>();
// 旧注册保留（ChemicalSimulationEngine、ChemicalReverseInferenceEngine、ChemicalKnowledgeBase、ReactionEquationParser）
```

可访问性：在 `ChemistryController` 新增三个加性端点（不影响旧端点路由）：

```csharp
[HttpPost("simulate")]    public ActionResult<SimulationTrace> Simulate([FromBody] ChemicalSystemInput input);
[HttpPost("reason")]      public ActionResult<ReasoningResult> Reason([FromBody] ReasonRequest req);       // req = { initial, plan }
[HttpPost("equilibrium")] public ActionResult<EquilibriumNumericalResult> Equilibrium([FromBody] EquilibriumRequest req); // req = { input, query }
```

若评估 HTTP 端点风险偏高，最低限度必须提供可被测试项目调用的公共入口（DI 可解析 + 公共方法），对应 AC-4（可直接实现）的可访问性要求。

---

## 10. 零回归论证与红线核验清单

### 10.1 为何不修改 Gateway / 数据

- `ChemicalSimulationEngine` / `ChemicalReverseInferenceEngine` **未被 `LlmGatewayService` 引用**（grep 全 src 证实：仅 `Program.cs` + `ChemistryController` + 自身文件）。在该命名空间内合并/演进，不触 live 判分路径。
- `ChemicalSimulator` / `ChemicalMultiStepReasoningSimulator` 只读 `ChemicalKnowledgeBase`（已加载的 JSON），不新增、不修改任何 `data/*.json`。
- `EquilibriumStateSimulator` 只读 `acid-constants.json`（经 `RuleStore` 加载为 `AcidConstantsData`），并静态调用 Core `AqueousEquilibriumEngine`（该引擎内部同样只读 JSON）；不写入、不修改任何常数数据；Gateway 中 `HydrolysisPhCalculator` / `KspPhResidualSolver` 原文件不动（仅收敛算法口径，下一轮不替换）。

### 10.2 实现期红线核验清单（下一轮编码后执行）

| 红线 | 核验方式 | 预期 |
|---|---|---|
| precision=1 | 全量 eval diff（相对当前基线） | GAINED 可正、LOST=0 |
| harm=0 | 同上 + 诊断脚本 | 无新增假阳性 |
| LOST=0 / WORSEN=0 | 同上 | 已判正题不翻转 |
| 不蒙数（反应） | 新模拟器无命中返回空；`KnowledgeIds` 取自 `Reaction.KnowledgeIds` | 单测断言 |
| 不蒙数（平衡数值） | `EquilibriumStateSimulator` 缺常数/缺输入返回 `Underdetermined`；常数只来自 `acid-constants.json` 或题给 | 单测断言：缺 Ka → `Status=Underdetermined`、`Ph=null`、`Warnings` 非空 |
| 不改 Gateway | `git diff --name-only` 不含 `Application/Gateway/` | 0 改动 |
| 不改数据 | `git diff --name-only` 不含 `data/**.json` | 0 改动 |
| 构建/测试 | `dotnet build` + `dotnet test` | 0 error，全绿 |

---

## 11. 下一轮实现任务清单（编码阶段，按依赖序）

| # | 任务 | 依赖 | 验收 |
|---|---|---|---|
| 1 | 新建 `ChemicalSimulatorContracts.cs`：定义 §2/§3 全部 DTO（`ChemicalSystemInput`/`ChemicalConditions`/`SpeciesInput`/`SystemStage`/`ReactionBasis`/`ReactionPriority`/`ReactionEvent`/`ConsideredCandidate`/`SpeciesState`/`SystemState`/`SimulationTrace`） | 无 | 编译通过，签名如设计 |
| 2 | 新建 `ChemicalSimulator.cs`：把 `ChemicalSimulationEngine` 的逻辑迁入并富化（`Simulate` 主循环 + `RunStage` + 输入解析 + 条件判定 + `ComputeExtent` + `Score` 系列 + `BuildBasis` + 富化 `Apply` + `BuildOutput`） | 1 | 单测：HCl+NaOH 事件字段非空；Fe+CuSO4 限量/剩余量正确 |
| 3 | 改造 `ChemicalSimulationEngine.cs` 为薄适配器（`MapToInput`/`MapToLegacyResult`）；删除已迁移的重复逻辑 | 2 | 现有 `ChemicalSimulationEngineTests` 全绿 |
| 4 | 新建 `ChemicalMultiStepReasoningSimulator.cs`：`Reason`/`QueryState`/`WhatIf`/`Search`（Search 委托反向引擎） + `ReasoningPlan`/`ReasoningStep`/`StateSnapshot`/`ReasoningGoal`/`ReasoningCandidate`/`ReasoningResult` | 2 | 单测：两步轨迹；WhatIf 不污染原轨迹；Search 命中/未命中 |
| 5 | 新建 `EquilibriumStateSimulatorContracts.cs`：定义 §7.2 全部 DTO（`EquilibriumQuery`/`EquilibriumQuantity`/`ConversionSpec`/`EquilibriumDetermination`/`EquilibriumNumericalResult`）；确认 `SpeciesInput.Concentration`/`Volume` 已在任务 1 定义 | 1 | 编译通过，签名如设计 |
| 6 | 新建 `EquilibriumStateSimulator.cs`：`Compute` 实现 §7.3 #1~#5（弱酸/弱碱/盐水解/缓冲 pH、Ksp 残留），常数读 `AcidConstantsData`，中间量走 `ConstantSpec.ToRational()`；#6~#8 委托 `AqueousEquilibriumEngine.Infer` 并映射结果；实现 §7.4 不蒙数规则 | 5 | 单测：0.1 mol/L CH₃COOH pH≈2.88；NH₃·H₂O pH；AgCl→AgI 转化 K；缺 Ka → Underdetermined |
| 7 | `Program.cs` 加性 DI 注册（含 `EquilibriumStateSimulator`）；`ChemistryController` 加性端点（或测试入口） | 2,4,6 | 端点冒烟返回非空 |
| 8 | 新增 `EquilibriumStateSimulatorTests`（成功 + 边界：缺常数/缺浓度/近似越界）；补充 `ChemicalSimulatorTests` / `ChemicalMultiStepReasoningSimulatorTests`（含 `ReasoningStep.Numerical` 挂载） | 2,4,6 | 每主类 ≥1 成功 + ≥1 边界，断言到字段级 |
| 9 | `dotnet build` + `dotnet test` 全绿；`git diff` 确认未改 Gateway/数据；全量 eval 红线核验 | 7,8 | 红线全过 |

> 实现顺序保证：任务 3 完成后旧测试即绿（适配器等价），任务 4/5/6 增量新增（反应、推理、平衡三者解耦，可分别回退），任何时刻可回退不影响基线。
