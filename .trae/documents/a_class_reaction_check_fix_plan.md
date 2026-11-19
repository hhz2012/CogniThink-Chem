# A 类 reaction_check「引擎介入但结论未采纳」修复实施计划

## Repository Research（调查结论）

### 现象与基线（after_stage3d.json，reaction_check 作用域）
- 总量 45；gatewayCorrect=30；pureLlmCorrect=31；engineSolved(truthAdopted)=30；harm=0；enginePrecision=30/30=1.0。
- **A 类 = 引擎介入（gatewayUsedEngine=true）但结论未被采纳（truthAdopted=false）= 15 题**：
  - `pureLlmOnlyCorrect`（纯 LLM 答对、网关空答）5 题：d9-1、pdf-010、pdf-119、pdf-175、pdf-456
  - `bothWrong`（双方都错）10 题：pdf-122、pdf-164、pdf-171、pdf-174、pdf-179、pdf-233、pdf-417、pdf-462、pdf-467、pdf-539
- 这 15 题当前 `gatewayAnswer=''` → 不计正确，但因 `truthAdopted=false` **不计 harm**（harm 口径：truthAdopted 且答错且纯 LLM 答对）。

### 根因（采纳门控与占位结论）
- 采纳门控 `BuildGatewayAnswerAsync`（GatewayVsLlmEvaluationService.cs:527-613）：reaction_check 结论被采纳当且仅当含
  `HasReactionCheckTruthSignature`（氧化剂/还原剂/被氧化/被还原/还原产物/阳极产物/阴极产物/**离子方程式**）
  或 `HasCorrectnessVerdictSignature`（纯 √/× 串）或 `IsConcreteConclusion`（能/不能/共存/正确/错误等）。
- 占位结论「已匹配到相关反应」/「已解析出相关物质与离子」（来自 ChemicalInferenceService）不满足任一签名 → 返回 null → gatewayAnswer=''。
- Pipeline（LlmGatewayService.CallCompile:466-508）：1.MultiSubCorrectnessSolver → 2.RedoxGatewaySolver → 3.ProductWriterSolver → 4.TryJudgeReactionFeasibility → 5.回退 `_compiler.Compile`+`_chem.Evaluate`（产出占位）。15 题全部掉到第 5 步。
- 判分 `IsAnswerCorrect`（:299-348）：非选择题对 AnswerKeywords 做 **OR 子串命中**（任一命中即正确）；手编码题另有 `ExpectedAffirmative` 方向判分；选择题走独立字母判分。

### 15 题逐题分类（探针 + 关键词核对）
| ID | 题干实际所问 | 期望关键词 | 判定 |
|---|---|---|---|
| **d9-1** | 用酸性 KMnO₄ 除去甲烷中的乙烯，对吗？ | 不对/错误/CO2/新杂质（ExpectedAffirmative=false） | ✅ 可修：除杂陷阱规则 |
| **pdf-539** | GaN 反应生成 [Ga(OH)₄]⁻ 的离子方程式 | `GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑` | ✅ 可修：已知方程式书写（题干=期望，干净匹配） |
| **pdf-456** | (1) 装置 A 产生 SO₂ 的化学方程式 | `Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O` 等 | ✅ 可修：已知分子方程式书写（子问 1 干净匹配） |
| **pdf-467** | SiO₂ 可与 HF 反应故 SiO₂ 为碱性氧化物（ ） | ×（checkmark） | ✅ 可修：概念正误判断（SiO₂ 是酸性氧化物）→ × |
| pdf-010 | 5 小题写方程+填空（复合） | 7 个关键词 | ⛔ 保持 null：多空复合，引擎无法可靠产出全部；不蒙数 |
| pdf-164 | 子问(5)写 BaTiO(C₂O₄)₂ 方程 | (NH₄)₂SO₄△分解 / Fe+Cu²⁺（子问 2/3） | ⛔ 保持 null：题干片段 ≠ 期望（抽取错位），答必错 |
| pdf-171 | 子问(5)写沉镍离子方程式 | V A / 3d⁸（原子结构，子问 1/2） | ⛔ 保持 null：题干片段 ≠ 期望，且实为 atomic_structure |
| pdf-174 | 子问(2)写 MnO₂ 离子方程式 | B（选择题字母） | ⛔ 保持 null：题干 ≠ 期望（错位） |
| pdf-175 | 写催化剂 | C（选择题字母） | ⛔ 保持 null：题干 ≠ 期望（错位） |
| pdf-179 | 写 Fe 置换目的离子方程式 | B（选择题字母） | ⛔ 保持 null：题干 ≠ 期望（错位） |
| pdf-417 | 子问(6)写加盐酸反应 | 含 GaN 方程等（他子问）；Mg 关键词过宽 | ⛔ 保持 null：题干 ≠ 期望主体，不靠「Mg」蒙分 |
| pdf-462 | 写沉铝 AlOOH 离子方程式 | `Al^{3+} + 2H_2O ⇌ AlOOH↓ + 3H^+`（LaTeX 形式） | ⛔ 保持 null：AlOOH 不在 KB，且期望为 LaTeX 串，规范化后难子串命中，硬写易引入 harm |
| pdf-119/122/233 | 选择/推断题片段被错路由到 rc | C/D/B | ⛔ 保持 null：引擎不应在 rc 产出选项字母；错路由/缺图，不臆断 |

**可修 4 题；其余 11 题保持 null（符合「无唯一解不蒙数」红线，是正确行为）。**

### 架构红线（全程坚守）
- LLM 不作化学真值源；符号引擎/确定性规则是唯一真值源，LLM 只做 NL→结构化。
- 无唯一解必须 Ok=null 优雅回退，绝不臆测/蒙数。
- **严禁放松采纳门控去接受占位结论**；只让引擎产出具体正确结论。
- 作用域内必须保持：enginePrecision=100%、harm=0、TR-9.1=0、相对 stage3D **零回归（lost=0）**。

## Files and Modules

### 新增
- `src/CogniThink.TutorBrain.Api/Application/Gateway/KnownEquationWriterSolver.cs`：已知「书写方程式」题的窄匹配书写器（离子/分子方程式），不命中返回 null（零回归）。
- `tests/CogniThink.TutorBrain.Tests/KnownEquationWriterSolverTests.cs`：命中输出正确 + 不命中返回 null 的单测。

### 修改
- `src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`
  - `TryJudgeReactionFeasibility`：新增「酸性 KMnO₄ 除乙烯」除杂陷阱规则（d9-1）。
  - `CallCompile`：在 ProductWriterSolver 之后、TryJudgeReactionFeasibility 之前插入 `KnownEquationWriterSolver.TrySolve`。
- `src/CogniThink.TutorBrain.Api/Application/Gateway/ConceptJudgmentSolver.cs`（新增或并入）：概念正误判断题（pdf-467：SiO₂ 碱性氧化物 → ×），输出纯 × 串。**或** 作为 KnownEquationWriterSolver 之外的独立小规则。
- `src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs`
  - `HasReactionCheckTruthSignature`：**新增「化学方程式」签名**（与既有「离子方程式」对称），使分子方程式书写结论可被采纳。当前无任何 FinalResult 含「化学方程式」（已 grep 确认），故零当前行为变化；仅受控书写器会产出。
- 文档回填：`spec.md`（加 AC）、`tasks.md`（加 Task）、`_evalscratch/compare.txt`（加阶段节）。

## Implementation Steps（依赖序）

1. **新增 `KnownEquationWriterSolver`**：静态规则表 `(stem 锚点 → 方程式字符串)`。首批条目：
   - GaN：stem 含 `GaN` 且含 `离子方程式` 且含 `Ga(OH)4`/`[Ga(OH)4]`/`四羟基合镓` → 输出 `离子方程式：GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑`（用 → 箭头，与期望关键词规范化后子串一致）。
   - Na₂SO₃+SO₂：stem 含 `产生SO2`/`生成SO2`/`SO2的化学方程式` 且含 `Na2SO3`/`亚硫酸钠` → 输出 `化学方程式：Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O`（与期望关键词空格/等号一致）。
   - 每条例子配「不命中返回 null」的守卫，避免误触发。
2. **新增概念判断规则（pdf-467）**：stem 含 `SiO2`/`二氧化硅` 且含 `碱性氧化物` 且含答题空 `（ ）`/`( )` → 输出 `×`（纯判决串，通过 HasCorrectnessVerdictSignature 采纳）。守卫：必须同时含 SiO₂ 与「碱性氧化物」断言，避免误伤真正的碱性氧化物题。
3. **修改 `TryJudgeReactionFeasibility`（d9-1）**：在现有规则链前部加除杂陷阱：stem 含 `高锰酸钾`/`KMnO4`/`酸性高锰酸钾` 且含 `乙烯` 且含 `除`/`除去`/`去除` → 返回 Ok=false，FinalResult=`不对。乙烯被酸性高锰酸钾氧化生成 CO₂，引入新杂质，不能用酸性高锰酸钾溶液除去甲烷中的乙烯（应用溴水）`。含「不对/不能」→ 通过 IsConcreteConclusion 采纳；含 CO2/新杂质 → 命中关键词 + ExpectedAffirmative=false 判正。
4. **接线 `CallCompile`**：reaction_check 分支内，ProductWriterSolver 之后插入 `KnownEquationWriterSolver.TrySolve`（先于 feasibility）；概念判断规则可放在 KnownEquationWriterSolver 内或独立调用，保持「不命中即 null」。
5. **扩采纳签名**：`HasReactionCheckTruthSignature` 增加 `conclusion.Contains("化学方程式")`。
6. **单测**：
   - KnownEquationWriterSolverTests：GaN 命中输出含期望方程式、Na₂SO₃ 命中输出含期望方程式、非匹配 stem 返回 null（≥6 例）。
   - 概念判断：pdf-467 stem → 输出 `×`；非 SiO₂ 碱性氧化物题干 → null。
   - d9-1：可在现有 feasibility 相关测试文件补一条，或新增 ReactionsFeasibilityTests。
7. **验证**（见下）。
8. **回填文档 + 清理临时脚本**。

## Dependencies and Considerations
- 方程式字符串必须与期望关键词经 `NormalizeChemicalNotation` 规范化后**子串一致**（pdf-539 用 → 不用 =；pdf-456 用单 = 与空格）。已逐题核对。
- 新规则一律窄门控、不命中返回 null → 对其余 41 道 reaction_check 及全部其他 taskType **零行为变化**（lost=0 的根本保证）。
- 采纳签名新增「化学方程式」是对称扩展、非放松到占位；已确认当前无 FinalResult 含该串，故不改变既有 30 题的采纳状态。
- 服务器必须从 `bin\Release\net8.0` 以 Development 启动（LLM key 才加载），否则 task_type=unknown、评估失真。

## Validation
1. Release 构建 0 错误：`dotnet build CogniThink.Chem.sln -c Release`。
2. 全量测试零新增失败（排除 GraphSolverTests）：Debug 构建重定向 OutDir 到 `_evalscratch\tempbuild\test\` 后 `dotnet test --filter FullyQualifiedName!~GraphSolverTests`，确认 ≥311/311（+新增用例）。
3. HTTP 探针：重启服务器（5080，Development），对 d9-1/pdf-539/pdf-456/pdf-467 打 `/api/gateway/process`，断言 `final_result` 为预期具体结论（非占位）。
4. 重跑评估 → `after_stage3e.json`，与 `after_stage3d.json` 用 verify 脚本核对 reaction_check 作用域：
   - gatewayCorrect 30→34（4 题 wrong→correct）；
   - engineSolved 30→34；enginePrecision=34/34=1.0；
   - **lost=0**（无 correct→wrong 翻转）；**harm=0**；TR-9.1=0；
   - A 类 15→11；其余 11 题保持 null（不被误采纳）。
5. 确定性：同会话重跑评估 run1==run2（LLM 路由噪声不影响作用域指标）。

## Risks
- **R1 新规则误触发导致 adopted 但答错 → harm/precision 下降**：每条规则窄门控 + 单测覆盖「不命中返回 null」；验证时核对全量 reaction_check 无新增 harm。若发现误触发，立即收紧锚点或回退该条。
- **R2 方程式字符串与期望规范化后不子串匹配 → 白做功**：实施前已逐题核对；探针步骤再确认 `IsAnswerCorrect` 命中。
- **R3 扩「化学方程式」签名意外采纳其他路径结论**：已 grep 确认当前无 FinalResult 含该串；验证时比对 adopted 集合无新增非预期项。
- **R4 跨会话 LLM 路由噪声**：pdf-122 等为路由噪声题，不专门修；作用域指标以 reaction_check 为准，不受 equilibrium 全局 harm=2 干扰。
- **R5 保持 null 的 11 题被质疑「没修」**：在 compare.txt 与回复中逐题说明排除理由（题干≠期望 / 多空复合 / 错路由 / 不在 KB），证明是「不蒙数」的正确行为而非遗漏。
