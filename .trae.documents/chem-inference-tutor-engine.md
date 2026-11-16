# 知识驱动推断引擎：让化学伴思「能推断、能计算、能解、能讲、能引导」

## Context（为什么做）

当前 `CogniThink.Chem.Core` 只是一个**校验器**：给定一个方程式/SMILES，检查白名单、守恒、配平、离子规则、氧化还原、官能团。它**不会推断**——产物推断、成分推断、过程推断、离子共存全部依赖外部（LLM 层）给出候选答案，引擎只判对错。基准测试集（42 题）里引擎也只是"校验 expected 答案"，而非"推导出 expected 答案"。

用户要求把引擎升级为**完整的伴思辅导引擎**：
1. **补充全部知识库**——建立覆盖课内全部主干反应的知识库（推断的地基）
2. **能推断**——产物推断、成分推断、过程推断、离子共存
3. **能计算**——在现有摩尔计算之上，补上方程式系数换算与过量判断
4. **能解**——端到端解出五类题，产出与标准答案一致的结构化解
5. **能讲**——逐步讲解，引用教材知识点
6. **能指导学生思考**——分级启发提问（伴思，不直接给答案）

**已确认的范围决策**：① 知识库覆盖课内全部主干反应（约 250+ 条）；② 五类题横向铺开；③ 讲解/引导输出为「结构化 + 中文模板」双形态。

### 设计原则的演进（重要）
旧铁律是"引擎绝不预测产物、绝不推断"。本次将其**演进**为：

> **引擎做"知识驱动的确定性推断"：每一个推断结论必须落到一条可溯源的知识库条目（反应条目/规则，含教材出处）。知识库能支撑就给出确定结论并引用依据；支撑不了就明确返回"无法推断/条件不足"（`NoSolution`/`Underdetermined`），绝不凭"讲得通"去猜。**

这样既获得推断能力，又保留原铁律的精神——**无幻觉、全溯源、并列不猜**（与 `RuleEngine` 的 `Unresolved` 语义、`EquationBalancer` 的"欠定不编造"一脉相承）。

---

## 可复用的既有资产（不重写）

| 资产 | 路径 | 在推断中的用途 |
|---|---|---|
| `EquationBalancer.Balance` / `ConservationViolations` | `src/.../Balancing/EquationBalancer.cs` | 对"反应物+候选产物"自动配平并做守恒可行性过滤（零空间维数 0=淘汰，>1=欠定） |
| `ChemicalCheckerService.Check` | `src/.../ChemicalCheckerService.cs` | 校验推断结果与知识库方程式（白名单/守恒/配平/离子/氧化还原） |
| `Species` / `ParsedEquation` / `EquationParser` | `src/.../Chemistry/`、`src/.../Parsing/` | 物种与方程式表示、解析 |
| `SolubilityTable` / `ActivitySeries` / `ElectrolyteRules` / `WhitelistGuard` | `src/.../Rules/`、`src/.../Ionic/` | 复分解/置换/共存判定、课内范围守卫 |
| `RuleEngine.Decide` + `FactContext` + `Verdict` | `src/.../Rules/RuleEngine.cs` 等 | 浓稀差异、钝化、置换方向等条件性裁决 |
| `MoleCalculator` | `src/.../Stoichiometry/MoleCalculator.cs` | 摩尔/浓度/产率等单物种计算 |
| `BenchmarkVerifier.ComputeCalc` | `src/.../Benchmark/BenchmarkVerifier.cs` | CALC 题复算逻辑，重构为求解器复用 |
| `KnowledgeBase` | `src/.../Knowledge/KnowledgeBase.cs` | 讲解时引用知识点名称/教材出处 |
| `ChemDataLoader` / `RuleStore` / `DataRoot` | `src/.../Data/`、`src/.../Rules/RuleStore.cs` | 数据加载，扩展以载入反应库 |
| `ProblemSpec` 等题目模型 | `src/.../Benchmark/ProblemModels.cs` | 求解器输入 |

---

## 架构总览（新增模块）

```
data/
  knowledge/reactions/reactions-inorganic.json   ← 无机主干反应库（新增，最大工作量）
  knowledge/reactions/reactions-organic.json     ← 有机主干反应库（新增）
  knowledge/reactions/reactions-principles.json  ← 原理/电化学/离子平衡反应库（新增）
  highschool/phenomena.json                      ← 特征现象→物质 索引（新增）
  highschool/{ions,solubility,electrolytes}.json ← 扩充物种，保证白名单全覆盖（编辑）

src/CogniThink.Chem.Core/
  Reactions/ReactionModels.cs      ← ReactionEntry、ReactionType、ReactionMatch
  Reactions/ReactionLibrary.cs     ← 载入+索引+查询（按反应物集、按参与物种）
  Inference/InferenceModels.cs     ← InferenceResult、Evidence（溯源引用）、风险标签复用
  Inference/ProductInferenceEngine.cs
  Inference/CoexistenceEngine.cs
  Inference/IdentificationEngine.cs
  Inference/ProcessInferenceEngine.cs
  Stoichiometry/EquationStoichiometry.cs  ← 方程式系数换算 + 过量判断
  Solving/SolutionModels.cs        ← Solution、SolutionStep
  Solving/ProblemSolver.cs         ← 按类别分派，端到端求解
  Explanation/Explainer.cs         ← 逐步中文讲解（模板 + 知识点引用）
  Explanation/GuideGenerator.cs    ← 分级启发提问（伴思，不给答案）
  Data/ChemDataLoader.cs           ← 扩展 LoadReactions / LoadPhenomena（编辑）

src/CogniThink.Chem.Cli/Program.cs ← 增加 --solve 演示（编辑）
tests/CogniThink.Chem.Tests/       ← 各引擎单测 + 端到端求解基准（新增）
```

---

## 实施步骤

### Phase 1 — 地基：反应知识库
1. **模型** `Reactions/ReactionModels.cs`：`ReactionEntry`（`Id`、`Name`、`Reactants`、`Conditions`、`Products`、`Equation`(L2 预配平串)、`ReactionType`、`Phenomenon`、`KnowledgeId`、`Source`、`GradeScope`、`Tags`）；`ReactionType` 枚举（化合/分解/置换/复分解/氧化还原/电极反应/水解等）。
2. **库** `Reactions/ReactionLibrary.cs`：载入全部反应文件，建两级索引——①按"排序后反应物集+条件"精确查产物；②按"任一参与物种"倒排（供过程推断图搜索）。提供 `FindByReactants`、`Involving`、`All`。
3. **数据编写**（最大工作量，约 250+ 条，全部可溯源到人教教材）：
   - 无机：钠/镁铝/铁铜/氯卤/硫/氮/硅 各元素族单质与化合物转化（含条件差异：浓稀、温度、催化剂）。
   - 有机：烃、卤代烃、醇酚、醛羧酸、酯、糖蛋白、高分子的官能团反应。
   - 原理：电极反应、关键离子平衡、热化学方程式。
   - 每条含现象与知识点 id，`Equation` 用现有 L2 语法（`2H2 + O2 =点燃= 2H2O`、`↓↑`、`⇌`、`Fe^3+`）。
4. **现象索引** `data/highschool/phenomena.json`：特征现象→物质/离子（如"血红色→Fe^3+(KSCN)"、"黄色火焰→Na"），供成分推断。
5. **白名单扩充**：补齐 `ions/solubility/electrolytes` 中缺失物种，使反应库全部物种通过 `WhitelistGuard`。
6. **自洽测试** `ReactionLibraryTests`：反应库每条 `Equation` 过 `ChemicalCheckerService.Check` 必须 `IsValid`（白名单+守恒+配平全绿）；反应物/产物化学式可被 `FormulaParser` 解析。

### Phase 2 — 五类推断引擎 + 计算扩展
7. **`InferenceModels.cs`**：`InferenceResult`（成功/答案/`Evidence` 溯源列表/候选/`RiskTag`），`Evidence` 引用反应 id 或规则 id + 教材出处。
8. **`ProductInferenceEngine`**：反应物+条件 → 产物。①反应库精确匹配；②规则生成系统族（金属+酸→H₂、金属+盐置换、复分解换离子），用 `SolubilityTable`/`ActivitySeries`/`ElectrolyteRules` 判方向；③候选产物集过 `EquationBalancer.Balance` 做守恒可行性过滤；多个并列可行解返回 `Underdetermined` + 候选，不猜。条件差异（浓稀/钝化）走 `RuleEngine`（COND-001/002、RED-005）。
9. **`CoexistenceEngine`**：离子组+介质 → 共存结论。介质约束（酸排弱酸根/OH⁻，碱排弱碱阳离子/H⁺）+ 两两反应（沉淀=溶解性表、气体/弱电解质、氧化还原不相容对），产出配平的"不共存理由方程式"。
10. **`IdentificationEngine`**：候选+实验步骤 → 成分（Must/MustNot/Maybe）。逐步约束传播：跟踪溶液离子组成，对每个候选假设模拟加试剂后的现象，剔除与观测现象矛盾的候选；证据方程式配平。
11. **`ProcessInferenceEngine`**：转化链 A→B→C + 候选 → `substanceMap`。在反应库上做图搜索：每步找"from+试剂→含 to 的产物"的反应，候选集消歧，链式闭合。
12. **`EquationStoichiometry`**：给定配平方程式+某物种已知量，按系数换算其它物种；多反应物时判过量。全程 `Rational`。
13. **单测**：每引擎一组（正例+干扰例+欠定例），断言答案与溯源引用。

### Phase 3 — 求解 + 讲解 + 引导
14. **`SolutionModels.cs`**：`Solution`（答案/`SolutionStep` 列表/是否解出/风险标签），答案按类别承载方程式/成分/物质映射/数值/共存结论。
15. **`ProblemSolver`**：按 `ProblemCategory` 分派到对应引擎；CALC 复用 `ComputeCalc` 逻辑；产出统一 `Solution`。
16. **`Explainer`**：把 `Solution` 渲染为逐步中文讲解，引用 `KnowledgeBase` 知识点名与教材出处；结构化步骤 + 模板文本双输出。
17. **`GuideGenerator`**：分级启发（定位题型→回忆知识点→关键步骤提示→更具体提示），只引导不给答案，模板中文。
18. **测试**：讲解/引导含知识点引用、不泄露答案的断言。

### Phase 4 — 集成与端到端
19. **CLI**：`--solve` 演示，五类各选一题，打印"推断/计算→解→讲解→引导"全链路。
20. **端到端基准** `ProblemSolverTests`：对全部 42 道基准题，断言求解器推导出的答案与 `expected` 一致（此前引擎只会校验、现在能推导）。
21. **边界样例修订**：有了产物推断，部分原标注"引擎职责外、由 LLM 判断"的 `expectValid=true` 干扰例（如 PROD-003-D1 `Fe2O3` vs `Fe3O4`）现在可被反应库判错——逐条复核并更新相应 `expectValid`/说明。
22. **文档注释更新**：把 `ChemicalCheckerService`、`ProblemModels` 中"引擎绝不推断"的旧表述改为"知识驱动推断"新原则。
23. 全量 `dotnet test` 绿。

---

## 关键风险与对策
- **反应库数据量大**：按模块分文件、逐族编写，先无机后有机再原理；每族编完即跑自洽测试，避免积压错误。
- **成分/过程推断复杂**：用"候选集约束传播 + 反应库图搜索"限定搜索空间；无法唯一确定时返回 `Maybe`/`Underdetermined`，不猜。
- **白名单缺口**：反应库自洽测试会暴露超纲物种，统一在 Phase 1 步 5 补齐 `highschool` 数据。
- **边界样例语义变化**：Phase 4 步 21 专门复核，避免旧预期与新能力冲突导致测试误报。

---

## 验证（端到端）
1. `dotnet build` 通过；`dotnet test` 全绿（含既有 192 例 + 新增）。
2. 反应库自洽：每条反应方程式过 `ChemicalCheckerService` 全绿。
3. 端到端：42 道基准题求解器推导答案与 `expected` 一致。
4. CLI：`dotnet run --project src/CogniThink.Chem.Cli -- --solve` 打印五类题的"解+讲+引导"。
5. 溯源抽查：任一推断结论都能给出反应 id/规则 id + 教材出处。
