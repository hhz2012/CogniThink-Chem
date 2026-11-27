# solve 投影富化与 UI 结构统一 — 实施计划

> 模式：Plan Mode。本文件是唯一计划文档，待用户审阅批准后再进入实施。
> 关联已完成 spec：`.trae/specs/frontend-gateway-solve-loop/`（P2-4，已交付 `/solve` 端点与 miniapp `pages/solve`，但其 Non-Goal 明确「不改引擎」）。本计划在其之上**加性**扩展，不重做 P2-4。

## 1. 目标

1. **改造 `/api/gateway/solve` 投影**：加性接入本会话新建的引擎能力（`EquilibriumStateSimulator`、`ChemicalSimulator`），把「数值 / 粒子排序 / 守恒式 / pH / 反应轨迹」等富结构字段随响应下发——当前这些字段对多数题为空。
2. **统一 UI 结构**：`web/app.js` 与 `miniapp/src/pages/solve` 都消费同一份实时 `/solve` 响应结构，而非各自渲染离线预烘焙数据或稀疏字段。

## 2. 已确认决策（来自澄清）

| 议题 | 决策 |
|---|---|
| UI 目标 | 两个前端都做：先 `web/app.js`（可直接浏览器演示），再扩展 `miniapp/pages/solve` |
| 投影边界 | **仅加性下发**：不改 `FinalResult / Ok / ConclusionState / TaskType` 与采纳门；不把 Insufficient 题升级为确定答案 |
| web 改造方式 | **新增独立实时求解视图**，现有离线题库 `detailHtml / solveHtml` 与可视化函数、事件绑定完全不动 |

## 3. 研究证据（本回合实采）

探针输出均落盘在 `_evalscratch/probe/`：

- `*.process.json`（运行时 `/process` 解析结果）：
  - **CALC-001**「500 mL 含 0.5 mol NaOH」：`engine_request.known = {n:0.5, V:0.5}` 已被 LLM 正确解析，但计算引擎返回 `Insufficient`（`constraint_note`="当前计算引擎仅支持部分定量计算场景"）。→ `c = n/V = 1.0 mol/L` 可由 enricher **确定性补算**，零蒙数。
  - **AQ-H01**「0.1 mol/L CH3COONa…排序+守恒」：`known` 全空、`reaction` 空——LLM 未抽到物种/浓度；但 rawText 含 `0.1 mol/L CH3COONa`。→ enricher 内做**确定性正则抽取**（非 LLM、非判分路径）后喂新引擎。
  - **EQ-H01**（气相 N₂+3H₂⇌2NH₃ 三段式 / Kp）：新引擎是**水相** pH/Ksp 建模，**不覆盖气相 Kp**。→ 明确列入「不覆盖」，不硬塞。
  - Confirmed 题（PROC-001 / COEX-001 / ELEC-H01）：`final_result` 正常，缺的是富结构（轨迹/电极图等）。
- `AQ-H01.equilibrium.json`（新 `/equilibrium` 端点，输入 CH₃COONa 0.1）：返回 `status=Determined`、`ph=8.88`、3 条守恒式（电荷/物料/质子，与题目所求完全一致）、`acidityVerdict`、7 步推导；**但 `concentrationOrdering` 为空**（已知缺口，见 §8 风险）。→ 证明新引擎能产出 AQ 类核心富信息，接线可行。
- `GatewaySolveProjection.cs`：`GatewaySolveProjector.Project` 是**纯函数**，按 `ConclusionState` 裁剪；eval 走 `GatewayVsLlmEvaluationService` **进程内**调用 `ProcessStudentMessageAsync`，**不经过 HTTP `/solve`** → 在 HTTP 控制器层做 enrichment，构造上对判分零影响。
- `web/app.js`：仅 `fetch('data/problems.json')` + `fetch('data/knowledge.json')`，全文件无 `/api` 调用；`miniapp/pages/solve/index.tsx` 已消费 `StudentSolveView` 稀疏字段。

## 4. 红线 / Non-Goals（不可破）

- **不碰判分路径**：不修改 `LlmGatewayService.ProcessStudentMessageAsync`、`CallEngineAsync`、`BuildGatewayAnswerAsync`、`IsStructuredCorrect`、`ConclusionStateDeriver`、`WorldModelEnricher` 中的任何判决逻辑；`git diff` 中 `LlmGatewayService.cs` 不应出现。
- **enrichment 仅在 HTTP `/solve`（含 `solve-photo`）路径执行**：eval 进程内调用不触发 → `precision / harm / LOST / WORSEN` 与基线 byte-identical。
- **enricher 全程 best-effort**：`try/catch` 包裹，任何异常返回 `null`，绝不抛入响应管线、绝不影响 `final_result/reply`。
- **不蒙数**：只从 (a) 题给数据的确定性算术，或 (b) 引擎 `status=Determined` 的结果取值；`Underdetermined` → 该字段留空。
- **不改 `data/**.json`**；不整文件重写 `web/app.js`；不破坏既有 DOM 选择器 / 事件绑定 / 分子可视化初始化时序（经验 100019783）。
- **明确不覆盖**（后续工作，不在本计划）：气相 Kp / 三段式组成、电化学电极图、分子 3D、浓度-时间曲线图、离子共存冲突图。这些类别保持现状（Confirmed 展示 `final_result`+`reply`，Insufficient 展示 `constraint_note` + 可算的辅助字段）。

## 5. 目标契约：新增 `structured` 段（加性、nullable）

`StudentSolveView` 新增可选字段 `structured`，JSON 形态：

```jsonc
"structured": {
  "numeric": { "value": 1.0, "unit": "mol/L", "species": "NaOH", "formula": "c = n/V" } | null,
  "aqueous": {
    "ordering": ["c(Na^+)", "c(CH3COO^-)", "c(OH^-)", "c(H^+)"],   // 可能为空（见风险）
    "conservation_equations": ["c(Na^+)+c(H^+)=c(CH3COO^-)+c(OH^-)", "..."],
    "ph": 8.88,
    "acidity_verdict": "溶液显碱性：c(OH^-) > c(H^+)",
    "ion_concentrations": { "OH^-": 7.56e-6 }
  } | null,
  "equilibrium": {
    "ksp_residual_concentration": null, "ksp_residual_ion": null,
    "conversion_constant_k": null, "conversion_proceeds": null
  } | null,
  "reaction_trace": {
    "events": [ { "equation": "Fe2O3+6HCl=2FeCl3+3H2O", "stage_index": 0, "extent": 1.0,
                  "basis_knowledge_ids": ["..."], "priority_rank": 1 } ],
    "final_species": [ { "substance": "FeCl3", "moles": 2.0 } ],
    "converged": true
  } | null,
  "warnings": ["..."]
}
```

**下发规则**：enricher 能确定性算出即附，与 `conclusion_state` 无关；但 UI 对**非 Confirmed** 的 `structured` 必须标注「辅助计算 / 参考，不作最终判定结论」，绝不呈现为引擎确定答案。`conclusion_state`/`final_result` 本身维持 P2-4 裁剪规则不变。

## 6. 改动文件清单

### 后端（全部加性）
1. **新建** `Application/Gateway/SolveStructuredContracts.cs`：`StudentSolveStructured` 及其子 DTO（`NumericSnapshot` / `AqueousSnapshot` / `EquilibriumSnapshot` / `ReactionTraceSnapshot` / `TraceEventDto` / `SpeciesAmountDto`），全部 `JsonPropertyName` 小写下划线，与现有契约风格一致。
2. **新建** `Application/Gateway/SolveStructuredEnricher.cs`：注入 `EquilibriumStateSimulator`、`ChemicalSimulator`（已 DI 注册，Program.cs ~L68）。方法 `Enrich(GatewayResult result, string rawText) → StudentSolveStructured?`：
   - 读 `result.TaskType` + `result.EngineRequest`（`Known/Reaction/Formula`）+ rawText。
   - 确定性物种/浓度抽取器（私有，正则匹配 `数字 mol/L 物质`、`数字 mol 物质`、`数字 mL` 等）——仅用于 enrichment，不回写 `EngineRequest`。
   - Calculation：已知 `n/V/c` 任两者 → 算第三者（`c=n/V`、`n=cV`、`V=n/c`），写入 `numeric`。
   - Equilibrium / Aqueous：抽到物种+浓度 → 调 `EquilibriumStateSimulator.Compute`（quantities = Ph/IonConcentrations/ConcentrationOrdering/Conservations/AcidityQualitative；传 `OrderSpecies` 尝试修 ordering）→ 仅当 `status=Determined` 写入 `aqueous`/`equilibrium`；`Underdetermined` 留空。
   - ReactionCheck / 已 Confirmed 反应类：`reaction.left/right` → 调 `ChemicalSimulator.Simulate` 取 `SimulationTrace` 写入 `reaction_trace`（失败/空则留空）。
   - 全程 try/catch → 异常返回 null。
3. **改** `Application/Gateway/GatewaySolveProjection.cs`：`StudentSolveView` 加 `Structured` 属性（nullable）；`Project` 增加可选参数 `StudentSolveStructured? enriched = null`，赋值给 `view.Structured`。现有无参调用（测试、其它端点）签名兼容。
4. **改** `Api/Controllers/GatewayController.cs`：`Solve` 与 `SolvePhoto` 在 `Project` 前调用 `_enricher.Enrich(result, request.Text)` 传入；`Process` 端点**不**调用（保持原始 GatewayResult 不变，供调试）。控制器构造函数加注入 `SolveStructuredEnricher`。
5. **改** `Program.cs`：DI 注册 `SolveStructuredEnricher`（singleton）。
6. **测试**：
   - 新建 `SolveStructuredEnricherTests.cs`：CALC 确定性 c=n/V；AQ 守恒+pH 从 CH₃COONa 文本产出；engine 抛异常 → null 不抛；Underdetermined → 空不蒙数。
   - 扩展 `GatewaySolveProjectionTests.cs`：`structured` 透传；Insufficient 仍不下发 `final_result/reasoning/explanation`（判决裁剪不变）。

### web
7. **改** `web/app.js`（最小加性）：新增「实时求解」入口（顶部按钮 / 新 hash 路由），渲染一个文本框 + 「求解」按钮；调用 `/api/gateway/solve`；新增 `renderLiveSolve(view)` 渲染状态卡 + `structured` 段（复用现有卡片 CSS 类，不碰 `detailHtml`/`solveHtml`/`initMoleculeViewers`）。
8. **改** `web/index.html`（如需）：加入口按钮，不删改既有结构。

### miniapp
9. **改** `miniapp/src/services/tutorApi.ts`：`StudentSolveView` 类型加 `structured`（与后端 JSON 对齐）。
10. **改** `miniapp/src/pages/solve/index.tsx`：`Result` 组件新增 `structured` 渲染块（numeric / aqueous / reaction_trace），非 Confirmed 时附免责声明；复用 `Collapse`。

## 7. 实施步骤（依赖序）

1. 后端 DTO + Enricher（含确定性抽取器），先不接线。
2. Projector 加 `Structured` + 重载；Program.cs DI。
3. Controller `Solve`/`SolvePhoto` 接线 → `dotnet build -c Release` 0 error；确认 `Application/Chemistry` 下无 `*.wip_disabled` 且 `.cs` 存在。
4. 后端单测（enricher + projector 扩展）→ 全绿。
5. web 新增实时求解视图 + `renderLiveSolve`。
6. miniapp 类型 + 渲染。
7. 全量验证（见 §8）。

## 8. 验证

- `dotnet build -c Release` 0 error（Debug+Release）。
- `dotnet test` 全绿（基线 1773 + 新增用例，0 failed）。
- 启服务 `:5099`，重探 12 地标 `/solve`：
  - CALC-001 → `structured.numeric.value=1.0`；AQ-H01 → `structured.aqueous` 含 3 守恒式 + pH；反应类 → `structured.reaction_trace` 有事件。
  - 所有题 `conclusion_state / final_result` 与基线 `*.solve.json` 一致；Insufficient 仍不泄露 `final_result`。
- 跑 eval 新 tag → 与 `after_simulator` 基线 **diff=0**（precision/harm/LOST/WORSEN/gatewayCorrect 不变）。
- 浏览器 smoke：web 实时求解页分别输入一道 Confirmed、一道 Insufficient 题，渲染正确无 JS 报错；既有离线题库浏览不受影响。
- `git --no-pager diff --name-only` 核验：无 `data/**.json`、无 `LlmGatewayService.cs`、无判分路径文件。

## 9. 风险与缓解

- **ordering 缺口**：新引擎对 CH₃COONa 返回空 `concentrationOrdering`。缓解：enricher 传 `query.OrderSpecies`（从题目「将 c(X)、c(Y)…排序」目标抽取）；若仍空，则由 `ionConcentrations` + 分析浓度（强电解质完全电离）**确定性派生**；派生不出则留空，绝不蒙数。
- **enricher 异常拖垮 /solve**：全程 try/catch → null；单测覆盖「引擎抛异常 → enrich 返回 null 且不抛」。
- **eval 漂移**：enricher 不进 `ProcessStudentMessageAsync`，仅 HTTP 层调用 → 构造上零影响；eval diff 兜底验证。
- **web 破坏既有交互**：新增独立视图与函数，不修改 `detailHtml`/`solveHtml`/选择器/事件；分子可视化初始化时序不动。
- **确定性抽取器误抽**：仅用于 enrichment（辅助展示），不参与判分；抽不到则字段空，UI 回落到 `final_result`/`reply`，不崩。
