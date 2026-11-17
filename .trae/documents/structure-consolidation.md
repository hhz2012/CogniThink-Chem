# 系统结构整合重构（行为不变 + 前端已知条件补齐）

## Context（为什么做）

两轮快速扩张（7 个新引擎 + 56 道 Hard 题）后，系统功能完整（12 类 98 基准题、27 真题、724 测试全绿，HEAD 19c2061），但结构上积累了四类问题。本次做** targeted 整合**：消除实证确认的重复、收拢散点、拆分巨型 CLI，并补齐审查中发现的前端导出缺口。**引擎结论、题库 expected、讲解/引导文本逐字不变**，724 测试全程全绿。

### 审查发现（已逐一读码核实）

1. **链式引擎复制粘贴**：[ProcessInferenceEngine.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ProcessInferenceEngine.cs)（264 行）与 [OrganicInferenceEngine.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/OrganicInferenceEngine.cs)（328 行）约 200 行几乎逐字相同：标签收集、回溯搜索（≤2 解）、解计数输出、VerifyStep 骨架、DescribeReagents、IsLabelToken/IsKnownSpecies。差异全部可参数化：RX-OR- 前缀过滤、Evidence 取方程式 vs 条目 id（防泄露）、步骤描述含/不含方程式、现象约束模式（有机多一段通用约束）、候选白名单预检、约 10 处文案字符串。ProcessFlowEngine 已是"委托 ProcessInferenceEngine"的正确组合形态，不动。
2. **CLI 巨型文件**：[Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Cli/Program.cs) 545 行顶层语句 + 5 个巨型局部函数；其中 `CompareWithStandardAnswer`（L327-601，275 行、12 类别 switch）是纯逻辑，应入 Core 成为可复用可测的比对器。
3. **类别元数据两处维护**：`Explainer.CategoryName`（[Explainer.cs L284-302](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Explanation/Explainer.cs#L284-L302)）与 `BenchmarkLoader.CategoryFiles`（[BenchmarkLoader.cs L16-31](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Benchmark/BenchmarkLoader.cs#L16-L31)）是同一枚举的两张平行表。
4. **数据访问小疵**：[ProcessFlowEngine.LoadRule](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ProcessFlowEngine.cs#L198-L207) 每遇到新规则 id 就全量重读 rules.json（每次 Infer 内多次读盘）。已核实 RuleStore（highschool/*.json，带缓存）与 ChemDataLoader.LoadRules（rules/rules.json）**不是重复实现**，保持双轨。
5. **前端导出缺口**（用户已批准一并修复）：`RunExportWeb` 的 given 导出（Program.cs L183-202）缺 `cell/equilibrium/solution` 三字段、steps 投影缺 `Purpose`，导致电化学/平衡/水溶液/流程四类题详情页"结构化已知条件"区块整体缺失（[app.js givenHtml](file:///d:/github/CogniThink-Chem/web/app.js#L247-L293) 无对应渲染）。

### 明确不做（避免过度工程）

- **不做全量"类别描述符注册表"**：SummarizeAnswer/BuildLevel3/ValidateSchema/answerHtml 的 switch 各类别逻辑实质不同，注册表只是把 switch 搬进字典初始化器，不减复杂度、徒增间接层。保留这些 switch。
- 不动 BenchmarkVerifier（已是字段存在性驱动的好设计）、不动题库数据与 expected、不动测试（它们是行为守卫）、不给 BenchmarkLoader 加缓存（收益小、引入状态风险）。
- 不碰 `.trae/`、`.trae-html-share-packages/web/index.html.zip`（既有脏改动）。

## 铁律（重构不得破坏）

- 结论溯源知识库/规则条目；推不出如实 Underdetermined/NoSolution；Rational 精确。
- 题库 expected 与引擎输出逐字一致；有机引擎 Evidence 只放条目 id（防泄露）；四级引导非空不泄露。
- 每阶段结束 `dotnet test` 724 全绿、`--verify-real` 27 PASS、代表题 `--solve` 输出与基线逐字节一致。
- 中文注释/测试名/提交信息；PowerShell 5 无 heredoc，`git commit -m` 内严禁双引号；push 需 dangerouslyDisableSandbox。

## 实施步骤

### 阶段 0：基线快照（守卫，存系统 Temp，不入仓库）

1. `dotnet build CogniThink.Chem.sln --nologo -v minimal` + `dotnet test`（记录总数，预期 724）。
2. `--verify-real` 输出重定向到 Temp\verify-real-before.txt。
3. 12 个代表题各跑 `--solve <id>` 存黄金输出：PROD-003 / IDENT-006 / PROC-001 / COEX-001 / CALC-003 / ELEC-H01 / EQ-H01 / AQ-H01 / PROCF-H01 / IONP-H01 / ORG-H01 / EXP-H01。
4. 复制 web/data/problems.json 到 Temp 供阶段 4 差异比对。

### 阶段 1：抽取链式赋值公共核心（最大收益，独立提交）

新文件 `src/CogniThink.Chem.Core/Inference/ChainAssignmentCore.cs`（internal static）：

- `ChainOptions`（sealed record）：`SourceName`、`EntryIdPrefix`（null=不过滤）、`EvidenceUsesEquation`、`StepDescriptionIncludesEquation`、`FullPhenomenonConstraint`、`SelfTransformText`（"自身分解"/"自身转化"），以及逐字文案委托/字符串：NoSteps、NoLabels、Intro（标签数/候选数模板）、ExhaustedStep、NoSolution、LostSupport（步号模板）、Underdetermined、Solved。
- 核心算法原样搬移：标签收集 → 回溯（solutions≤2）→ 解计数输出 → 逐步 VerifyStep（参数化过滤/现象约束/Evidence 形态）→ DescribeReagents/IsLabelToken/IsKnownSpecies。
- [ProcessInferenceEngine.Infer](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ProcessInferenceEngine.cs#L21-L193) 变薄包装：构造 process 选项调核心；[OrganicInferenceEngine.Infer](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/OrganicInferenceEngine.cs#L24-L217) 保留候选白名单预检（L38-57 原样）后调核心（organic 选项）。
- **公共 API 与 Request/Result 类型完全不变**（ProcessInferenceRequest/Result、OrganicInferenceRequest/Result、ProcessStepResult 原位不动）；ProcessFlowEngine 零改动。
- 逐字保留的字符串差异清单（两引擎各一套，搬移时不得"顺手统一"）：首步介绍、无步骤/无标签/无解/失去支撑/欠定/已解出 Message、步骤匹配 Description（process 含方程式、organic 不含）、Evidence（process=entry.Equation、organic=entry.Id）、DescribeReagents 的"自身分解/自身转化"。
- 判据：724 全绿；PROC-001/ORG-H01/PROCF-H01 黄金输出逐字节一致；`InferenceEngineTests.ProcessInferenceEngineTests`（L179 起）与 `OrganicInferenceEngineTests`（"至少 2 组"/"白名单"断言）通过。

### 阶段 2：CLI 拆分 + 比对器入 Core（独立提交）

- 新文件 `src/CogniThink.Chem.Core/Benchmark/RealExamComparer.cs`：`public static (bool Ok, List<string> Detail) Compare(ProblemSpec, Solution)` —— 把 Program.cs L327-601 的 `CompareWithStandardAnswer` **逐字搬移**（含全部 detail 文案与全角空格对齐）。
- CLI 新目录 `src/CogniThink.Chem.Cli/Commands/`：`DemoCommand.cs`（RunDemo）、`SolveCommand.cs`（RunSolveDemo）、`ExportWebCommand.cs`（RunExportWeb）、`VerifyRealCommand.cs`（RunVerifyReal，改调 RealExamComparer）。各为 internal static class，方法体逐字搬移。
- [Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Cli/Program.cs) 缩为 ~35 行：UTF8 输出编码 + 参数分派 + 帮助文案（逐字不变）。
- 判据：724 全绿；`--verify-real` 输出与基线逐字节一致；`--solve`/`--demo` 黄金输出一致。

### 阶段 3：类别元数据归一 + 规则读盘修复（独立提交）

- 新文件 `src/CogniThink.Chem.Core/Benchmark/CategoryCatalog.cs`：`CategoryInfo(string Name, string FileName)` + 12 类别静态表（唯一维护点）。`Explainer.CategoryName` 与 `BenchmarkLoader.CategoryFiles` 改为委托该表（公共签名不变，未知类别仍回退 `category.ToString()`）。
- [ProcessFlowEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ProcessFlowEngine.cs#L96-L207)：Infer 开头一次性 `ChemDataLoader.LoadRules(DataRoot.Resolve())` 建 id→Rule 字典传入 ClassifyPurpose；删除 LoadRule 的按 id 重复读盘。行为不变（同一份规则数据）。
- 判据：724 全绿；PROCF-H01 黄金输出一致；`--export-web` 产物除 generatedAt 外零差异。

### 阶段 4：前端已知条件补齐（用户已批准，独立提交）

- `ExportWebCommand`（原 RunExportWeb）given 匿名对象追加：`cell = problem.Given.Cell`、`equilibrium = problem.Given.Equilibrium`、`solution = problem.Given.Solution`（直接序列化，与现有 `solution.Equilibrium` 同法，camelCase 由 ChemDataLoader.Options 保证）；steps 投影追加 `s.Purpose`。
- [app.js givenHtml](file:///d:/github/CogniThink-Chem/web/app.js#L247-L293) 追加渲染（复用现有 chem/esc 工具，风格与现有行一致）：
  - 电化学装置：type/electrodes/electrolyte/medium/overall/gasFeeds；
  - 平衡体系：equation/container/initial（逐物种起始量）/known/disturbance/totalPressure/fractionSpecies；
  - 溶液组成：solutes/ph/note/orderSpecies/conversion；
  - 过程步骤行：有 purpose 时追加"操作：…"。
  - 字段为空/缺省时不产生行（旧 7 类渲染零变化）。
- 重跑 `--export-web`；与 Temp 基线 problems.json 比对：**solution/explanation/guide 块逐字节不变**，仅受影响类别的 given 块新增字段 + generatedAt。
- 浏览器抽查（browser_use 子代理，query≤600 字符；静态服务器用系统 Temp 的 PowerShell HttpListener 脚本，用完删除）：ELEC-H01 / EQ-H01 / AQ-H01 / PROCF-H01 详情页已知条件区块正确显示，IDENT-006 旧类别渲染无回归。

### 阶段 5：收尾

- 全量回归：build + test（724）+ `--verify-real`（27 PASS）+ 12 代表题黄金输出比对 + problems.json 差异审计。
- 按阶段分 4 个逻辑提交（中文信息，不含 .trae/ 与 zip），推送（dangerouslyDisableSandbox:true）。

## Verification（每阶段通用）

```powershell
dotnet build CogniThink.Chem.sln --nologo -v minimal
dotnet test CogniThink.Chem.sln --nologo -v minimal   # 724 全绿，禁止任何既有测试变红
dotnet run --project src/CogniThink.Chem.Cli --no-build -- --verify-real  # 与基线逐字节比对
```

阶段 1-3 后另跑 12 代表题 `--solve` 与黄金输出比对（FC/Compare-Object）；阶段 4 后 problems.json 差异审计 + 浏览器抽查。

## 风险与取舍

- **最大风险是字符串漂移**：引擎 Message/Description 被测试子串断言、黄金输出、引导防泄露逻辑多方依赖。对策：搬移逐字复制、选项参数化而非重写文案、每阶段黄金输出比对。
- 顶层语句局部函数搬移为静态类时注意 `static` 局部函数捕获语义（现有函数均无捕获，直接搬移安全）。
- RealExamComparer 入 Core 后 CLI 与测试仍各自独立断言（测试不改为依赖比对器，保持守卫独立性）。
- 阶段 4 是唯一改变输出产物的阶段（仅 given 块增量），已获用户批准；其余阶段产物零差异。
