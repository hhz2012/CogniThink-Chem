# 引擎能力缺口补强 batch3 实施计划

> 目标：在 `precision=1 / harm=0 / 引擎层 LOST=0` 红线下，从当前「LLM 答对、引擎未答对」的 leads 中筛出可由通用确定性求解器低风险翻正的题目，实现并验证。预期 **+3**（gwCorr 656→659），含 1 个 spike-gated 可选扩展（pdf-098，通过则 +4）。

## 一、调研结论（现状与分布）

### 1.1 基线快照
以**当前引擎 + LLM-on** 重跑全量得到 `snapshot_gap_batch3_base.json`（tag=`gap_batch3_base`，757 题，`llmAvailable=True`，模型 qwen3-vl-plus）：

| 指标 | 值 |
|---|---|
| gatewayCorrect | **656** |
| pureLlmCorrect | 625（82.56%） |
| pureLlmOnly leads | **51** |
| bothWrong | 50 |
| harmCount / enginePrecision | 0 / 1 |

> 对比上一轮基线 `snapshot_gap_batch1`（50 leads，gwCorr 654）：batch2 已解出 pdf-024、pdf-169（gwCorr 654→656，引擎层 LOST=0）。本轮 leads 由 50→51 的 +1 净漂移来自 **LLM 非确定性**（NEW={pdf-097,197,218,306,376}，DROPPED={pdf-024,169（引擎已解✓）, 355,461（LLM 本轮答错）}），**引擎侧无变化、无回归**。证据：`_evalscratch/analyze_batch3_leads51.py`、`_evalscratch/batch3_leads51.txt`。

### 1.2 当前 51 leads 分布

| taskType | 数量 | 有图 | 无图 | 失败模式 |
|---|---|---|---|---|
| choice | 23 | 18 | 5 | 全部 B 类（引擎空结论 state=Insufficient） |
| equation_writer | 8 | 3 | 5 | 全部 B 类 |
| concept | 6 | 1 | 5 | A 类（设计不走引擎，Insufficient） |
| calculation | 5 | 3 | 2 | 全部 B 类 |
| unknown | 5 | 4 | 1 | A/B 混合 |
| equilibrium | 3 | 2 | 1 | 全部 B 类 |
| reaction_check | 1 | 0 | 1 | 引擎有结论但未采纳（pdf-163，非门控误拦） |

**关键判断**：失败模式仍以 **B 类（引擎空结论）** 为绝对主体；**无 C 类**（不存在「引擎算对但被采纳门误拦」→ 没有纯门控修复点）。仅 pdf-163 / pdf-391 是 `Confirmed` 未采纳，但它们是「引擎产出了不相关占位结论」而非「正确结论被误拦」，不是门控 bug。

### 1.3 逐题判读（可补 vs 不补）

**不补（共 ~44 题，按原因）**：
- **图像依赖，纯文本无法定解**：choice 中 18 题（pdf-138/218/255/284/303/306/325/336/337/338/357/406/416/458/498/511/524/531）、unknown 中 4 题（pdf-205/215/391/413）、equilibrium pdf-262/418、calculation pdf-534（仪器 C 需读图）、equation_writer pdf-156/177/532。→ 引擎无视觉，不补。
- **数据缺陷 / 答案键与解析矛盾**：pdf-197（问「错误的是」答案键=ABCD 全对，与解析矛盾）、pdf-376（note 称 B 正确但答案=B 且问「不正确的是」矛盾）、pdf-263（「正确的有几项」答案键=字母 B）、pdf-084（答案键=C 但题干要求写方程）、pdf-306（不可解清单已登记键冲突）。→ 先修数据，不补引擎。
- **开放论述 / 比较解释**：calculation pdf-207（4 问熔沸点原因）、concept 6 题（pdf-176/198/324/483/519/528，A 类设计回退）、pdf-070（子问含双线桥图 + 选项字母驱动）、pdf-163（上下文缺失的子问片段）。→ 无唯一确定数值/方程可输出，不蒙数。
- **多选/不定项字母驱动**（chem-fact-curation 另轨，非本次引擎求解器范围）：pdf-405 等。

**可补（spike 已验证零 FP，共 3 题，+3）**：

| 题 | taskType | 求解器 | 机制 | spike 结果 |
|---|---|---|---|---|
| **pdf-274** | equilibrium | HydrolysisEquationSolver | 题干「用平衡原理解释」+ 除杂语境 + 可水解阳离子盐（Fe³⁺/Al³⁺/Cu²⁺…）→ 输出该阳离子水解方程 `Mⁿ⁺ + nH₂O ⇌ M(OH)ₙ + nH⁺` | fired={pdf-274}，命中关键词 `Fe3+ + 3H2O ⇌ Fe(OH)3 + 3H+`，adopted=True，0 FP |
| **pdf-146** | calculation | IodometryMassFractionSolver | 题干「碘标准溶液滴定 SO₂ 含量」→ 提取 c、V(滴定剂)、定容体积、取样体积、样品质量 → n(I₂)=cV，SO₂:I₂=1:1，M(SO₂)=64 → 含量(g/kg) | fired={pdf-146}，命中 `0.032`，adopted=True，0 FP |
| **pdf-355** | equation_writer（hook 路由 calculation 门） | EsterificationEquationSolver | 题干「酯化」+ 乙二酸与乙醇 1:2 → 输出 `HOOC-COOH + 2CH₃CH₂OH ⇌ CH₃CH₂OOC-COOCH₂CH₃ + 2H₂O` | fired={pdf-355}，命中 `CH3CH2OOC-COOCH2CH3 + 2H2O`，adopted=True，0 FP |

> 注：pdf-355 在 `gap_batch3_base` 中属 bothWrong（LLM 本轮答错），非 leads；但引擎仍未答对，求解器翻正后 gwCorr 仍 +1（bothWrong→gatewayOnly），无 harm。

**spike-gated 可选扩展（+1 潜力，视实现阶段 spike 结果定）**：
- **pdf-098**（calculation，σ 键数）：子问(1)「1 mol NH₄Cl 含 σ 键 ____」答案键 `4 mol`。可行性已验证：`MoleculeAnalyzer.Analyze("[NH4+].[Cl-]")` 成功，RDKit 键数=4 = σ 键数（每条共价键含 1 个 σ）。但需：(a) 给 `MoleculeAnalysisResult` 暴露 `SigmaBondCount`（Chem.Core 一般化增强）；(b) 写「分子式→SMILES」归一化器（离子盐拆分为阴/阳离子，如 NH₄Cl→`[NH4+].[Cl-]`）。batch2 曾因全库 21 道 σ 键题多为选择、离子化合物口径 FP 风险高而延后。**本次 gate**：实现阶段先做专用 spike（加 SigmaBondCount + 归一化器 + 全库 dry-fire），仅当 fired 集中每个 baseFalse 触发均 IsAnswerCorrect=True 时才纳入；否则继续延后登记，不影响 batch3 主目标 +3。

### 1.4 采纳门与钩子架构（复用，零漂移）
- 新增求解器接在 [LlmGatewayService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) 的 **last-resort 钩子链末尾**，每个钩子 `if (engineResponse.Ok is not true)` 守卫 → 已判正题（Ok=true）永不触达 → **构造性零 LOST**。
- 命中后设 `engineRequest.TaskType` 选择采纳门：pdf-274→`equilibrium`、pdf-146/pdf-355/pdf-098→`calculation`，均走 `IsAnswerCorrect`（OR 门）→ 凡采纳必关键词命中 → precision=1、零 harm。
- **不改 `ClassifyTaskDeterministic`**（零漂移）。

## 二、涉及文件与模块

**新增（生产）**：
- `src/CogniThink.TutorBrain.Api/Application/Gateway/HydrolysisEquationSolver.cs`
- `src/CogniThink.TutorBrain.Api/Application/Gateway/IodometryMassFractionSolver.cs`
- `src/CogniThink.TutorBrain.Api/Application/Gateway/EsterificationEquationSolver.cs`
- （仅当 pdf-098 spike 通过）`src/CogniThink.TutorBrain.Api/Application/Gateway/SigmaBondCountSolver.cs`

**修改（生产）**：
- `src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`：钩子链末尾追加 3~4 个 last-resort 钩子（每段含 `if (engineResponse.Ok is not true)` 守卫 + 设 TaskType）。
- （仅当 pdf-098）`src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs` + `MoleculeAnalysisResult`：暴露 `SigmaBondCount`（= RDKit `getNumBonds`，每条共价键=1 σ）。

**新增（测试）**：
- `tests/CogniThink.TutorBrain.Tests/HydrolysisEquationSolverTests.cs`
- `tests/CogniThink.TutorBrain.Tests/IodometryMassFractionSolverTests.cs`
- `tests/CogniThink.TutorBrain.Tests/EsterificationEquationSolverTests.cs`
- （仅当 pdf-098）`tests/CogniThink.TutorBrain.Tests/SigmaBondCountSolverTests.cs`
- （仅当 pdf-098）`tests/CogniThink.Chem.Tests/` 下补 `MoleculeAnalyzer` 的 SigmaBondCount 单测。

**修改（登记）**：
- `data-defect-register.md`：K.0 增补 batch3 小节（pdf-274/146/355 翻正；pdf-098 通过/延后留痕；不补项原因留痕）。

**证据（gitignored `_evalscratch/`，不入库）**：`snapshot_gap_batch3_base.json`、`batch3_leads51.txt`、`analyze_batch3_leads51.py`、spike 输出。

## 三、实施步骤（依赖序）

1. **（可选 gate）T0：pdf-098 专用 spike** — 在 tests/ 建临时探针验证 SigmaBondCount + 归一化器的全库 fired 集是否零 FP。通过则纳入 T1.x；不通过则删除探针、pdf-098 登记延后，不阻塞主流程。
2. **T1：实现 3 个求解器**（Hydrolysis / Iodometry / Esterification），域外/缺数据/歧义一律返回 null（不蒙数）。
3. **（若 T0 通过）T1.x：实现 SigmaBondCountSolver + Chem.Core SigmaBondCount 暴露**。
4. **T2：接入钩子链** — 在 `LlmGatewayService` 末尾追加，每个 `if (engineResponse.Ok is not true)` 守卫 + 设对应 TaskType。
5. **T3：单测 + 护栏** — 每个求解器：通用规则正向单测 + 域外 null 单测 + 全库 dry-fire 护栏（断言 fired 集 = 预期集合，且 baseFalse 触发必 IsAnswerCorrect=True）。基线快照用 `snapshot_gap_batch3_base.json`。
6. **T4：全量回归（所有实现完毕后才跑）** — `ASPNETCORE_ENVIRONMENT=Development`（LLM-on）跑 eval tag=`gap_batch3`；`diff_engine gap_batch3_base gap_batch3` 确认 LOST=[]、GAINED⊇{pdf-274,pdf-146,pdf-355}、harm=[]；`verify_mp27` 27/27；`dotnet test -c Release` 全绿。
7. **T5：登记** — 更新 `data-defect-register.md`。

## 四、依赖与注意事项

- **红线严守**：precision=1、harm=0、引擎 LOST=0、不蒙数（无唯一解→null）、不硬编码单题（规则通用）、禁止在所有候选实现完毕前跑全量回归。
- **关键词归一化**：`IsAnswerCorrect` 经 `NormalizeChemicalNotation`（⇌/→/==→=、去 ↑↓△、去空格、小写），两侧对称 → 子串包含关系保持。方程类输出只要含归一化后的关键词子串即命中，格式稳健（spike 已实证）。
- **LLM 非确定性**：leads 集合会随 LLM 重跑漂移（±2~3 题）。引擎层回归判定以 **gatewayCorrect 集合**为准（与 LLM 无关），不以 solved/pureLlm 口径为准。
- **构建/测试用 Release**（`-c Release`）；临时探针 `.cs` 放 tests/ 用完必删；证据留 `_evalscratch/`（已 gitignore）。
- **不主动 git commit/push**，除非用户明确要求。

## 五、验证

1. 求解器单测全绿（正向 + 域外 null）。
2. 护栏单测：每个求解器全库 fired 集 = 预期集合，baseFalse 触发必判正，0 错采纳。
3. `dotnet build -c Release` 0 error（新文件无新增 warning）。
4. 全量 eval（LLM-on）：`gatewayCorrect` 656→**659**（或 660 若含 pdf-098）；LOST=[]；harm=0；precision=1。
5. `verify_mp27 gap_batch3_base gap_batch3` → PASS 27/27，REGRESSIONS=[]。
6. `dotnet test CogniThink.Chem.sln -c Release` → 0 failed。

## 六、风险与处置

- **pdf-098 离子化合物 σ 键口径 FP**（高风险）：处置=T0 spike gate，不通过即延后登记，不纳入本次实现，保证主目标 +3 不受影响。
- **pdf-146 数字提取错位**（中风险）：c/V/定容/取样/样品质量 5 个数需语义锚定。spike 已锁 fired={pdf-146} 且判正；生产代码保持与 spike 一致的正则锚，护栏锁 fired 集。若未来新增碘量题误触发且未判正，无 harm（不采纳）；若误判正则护栏立即捕获。
- **pdf-274/355 触发面漂移**（低风险）：收窄触发（「平衡原理+除杂」「酯化+乙二酸/乙醇/1:2」）使 fired 集唯一；护栏锁死。
- **钩子顺序与既有求解器冲突**（低风险）：新钩子置于链末尾且 `Ok!=true` 守卫，不影响已解出题；batch2 已验证该模式零 LOST。
