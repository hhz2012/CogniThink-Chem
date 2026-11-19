# 修复 2 个 harm：equilibrium 曲线读数被误采纳 实施计划

## 背景与根因（两层，契约不一致）

评估集 `after_stage3e.json`（648 题）中 `harmCount = 2`，全部集中在 equilibrium 的两道题（pdf-259、pdf-272）。两题的 `gatewayTruthAdopted = true` 但 `gatewayCorrect = false`，且纯 LLM 答对了——即引擎把对的题改成了错。

逐题结论：
- pdf-259：期望 `-3 / 30 3.9 2.6×10⁻⁶ / 平衡移动原因`；引擎产出 `x=3 时，曲线「乙醇浓度」的 y = 3.437`
- pdf-272：期望 `HC2O4- + H+, 1.0×10^-1.2, HC2O4- + H2O`；引擎产出 `x=2.5 时，曲线「H₂C₂O₄」的 y = 0.8`

**根因 1（引擎层，抢占）**：`CallEngineAsync` 中 `TryRouteCurve`（[LlmGatewayService.cs:296](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L296)）在 `CallEquilibrium`（[L353](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L353)）之前执行，曲线非空即抢占返回。曲线引擎返回的是**原始坐标读数**，不是题目要的最终答案（pdf-259 要 ΔH / 转化率 / K / k⁻），却抢占了本可正确求解的平衡求解器——pdf-259 的燃烧焓子求解器 `TryCalculateCombustionEnthalpy` 会算出 `ΔH = -874 + (-1367) - (-2238) = -3 kJ/mol`，恰好命中期望关键词 `-3`。

**根因 2（评估门控层，契约不一致）**：`BuildGatewayAnswerAsync` 的 equilibrium 分支（[GatewayVsLlmEvaluationService.cs:573-578](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L573-L578)）对非选择题**无条件 `return conclusion`**，不做答案键校验。而同文件的 atomic_structure（[L549-553](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L549-L553)）、vsepr（[L558-562](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L558-L562)）都用 `IsAnswerCorrect(conclusion, c) ? conclusion : null` 只采纳经校验的结论。equilibrium 的无条件采纳使曲线原始读数被当成真值 → harm。

**数据佐证（零回归风险依据）**：
- `assetBreakdown.curve`：33 题，**structuredHits = 0**——曲线数值判分零命中。
- 全评估集仅 4 题的 `gatewayConclusion` 来自曲线引擎：pdf-037/077（choice，被 choice 门控正确拒绝，未采纳）、pdf-259/272（equilibrium，被错误采纳 = harm）。
- 即曲线引擎当前**零正确贡献**，却产生 2 个 harm。让平衡求解器优先于曲线，不会丢掉任何已答对的题。

## 修复

### Fix 1（评估门控收紧，必做，零回归可证）
文件：`src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs`，L573-578。

equilibrium 非选择分支由：
```csharp
if (isChoice && !ContainsOptionLetter(conclusion))
    return null;
return conclusion;
```
改为（与 atomic_structure / vsepr 对齐）：
```csharp
if (isChoice && !ContainsOptionLetter(conclusion))
    return null;
if (string.IsNullOrWhiteSpace(conclusion)) return null;
// 非选择填空：只采纳经答案键校验的结论，避免曲线原始读数等非最终答案被误采纳造成 harm；
// 曲线数值走结构化判分（MatchCurveNumeric），不重复经文本采纳。
return IsAnswerCorrect(conclusion, c) ? conclusion : null;
```

**零回归证明**：10 道当前正确的 equilibrium 均经 `IsAnswerCorrect` 判对（equilibrium structuredHits = 0），过滤后仍通过；2 道 harm 经 `IsAnswerCorrect` 判错 → 不被采纳 → harm 消除。采纳量只减不增，且只减错误采纳，不影响任何已答对的题。

### Fix 2（引擎抢占修正，根因，受 Fix 1 保护无新增 harm）
文件：`src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`，`CallEngineAsync` 资产路由之前。

在 `var graphResp = TryRouteGraph(...)` 之前插入：equilibrium 任务先跑 `CallEquilibrium`，命中（`Ok == true`）直接返回；未命中（`Ok == null`）才继续走资产路由（曲线/graph）与通用分支。

**效果**：
- pdf-259 → `CallEquilibrium` 燃烧焓子求解器产出 `ΔH = -3 kJ/mol` → 命中期望 `-3` → **变正确（gained +1）**；runtime 不再返回曲线原始读数。
- pdf-272 → `CallEquilibrium` 无匹配子求解器 → `Ok = null` → 回退曲线 → 曲线读数经 Fix 1 不被采纳 → 变为非 harm miss（纯 LLM 仍答对，bucket 不变，不再计 harm）。

**零回归证明**：仅改变"曲线当前抢占 equilibrium"的题（仅 pdf-259/272）；其余 27 道 equilibrium 曲线本就返回 null（无意图/无资产），行为不变；10 道正确题 `CallEquilibrium` 产出相同结论，Fix 1 校验通过，仍正确。**Fix 1 兜底**：即便 `CallEquilibrium` 对某题产出错误结论，也不会被采纳（`IsAnswerCorrect` 拒绝），不可能新增 harm。

## 明确不改
- 不碰曲线引擎本身（`CurveQuery` / `CurveIntentExtractor` / `TryRouteCurve`）：pdf-037/077 等 choice 题当前未 harm，改动曲线→choice 路径有引入新 harm 的风险，留作后续单独验证。
- 不放松任何采纳门控（本次只收紧）。
- 不碰 reaction_check 等其他作用域。

## 验证
1. **构建/测试**：Release 构建 0 错误；Debug 全量测试通过（排除已知外部 `GraphSolverTests` 与作用域外 Tier-2 WIP `ClassificationValidatorTests`）。新增单测覆盖：equilibrium 错误结论不被采纳、equilibrium 正确结论仍被采纳、equilibrium 求解器优先于曲线。
2. **评估**：重跑 → `after_stage3f.json`，与 `after_stage3e.json` 对比：
   - 硬指标：`harmCount` 2 → **0**
   - equilibrium：`lost = []`（10 道正确零回归），`gained` 期望 `+1`（pdf-259）
   - 全局：**无新增 harm**（扫描所有 `truthAdopted=true & correct=false & pureLlmCorrect=true`，应为空）
   - `enginePrecision` 不下降（应从 0.9804 升至 1.0，因 adopted 错误归零）
3. **HTTP 探针**：pdf-259 runtime `final_result` 含 `-3`（`CallEquilibrium` 产出），不再是曲线原始读数；pdf-272 runtime 不被采纳（网关空答，但不再 harm）。

## 风险与回退
- 若评估发现新增 harm 或 lost → 回退 Fix 2，仅保留 Fix 1（仍保证 harm → 0，零回归）。
- Fix 1 独立可验证：即使 Fix 2 回退，harm 也已消除。
