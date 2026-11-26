# 引擎补强计划（engine-gap-batch5）

> 状态：**执行完成**。红线全守住：gwCorr 663→**669**（+6），LOST=0，precision=1，harm=0，mp27=27/27。
> 基线快照：`_evalscratch/snapshot_gap_datafix1.json`（gwCorr=663/757，pureLlmCorrect=633，harm=0，precision=1，mp27=27/27）。
> 本批产物快照：`_evalscratch/snapshot_b5part2.json`（Production，LLM-off，纯引擎）。

## 1. 现状与待补强分布（已审计）

对最新基线 `snapshot_gap_datafix1` 的 leads（`pureLlmOnly` 50 ∪ `bothWrong` 44 = **94 题**）逐题判读，并用只读子问探针（`_evalscratch/subpart_probe.txt`，已用完删除探针 .cs）量化每道题的引擎可解性。分布如下：

| 类别 | 数量 | 性质 | 本批次处置 |
|---|---|---|---|
| **A. 选择题** | 38 | 引擎不输出字母，非引擎域 | 排除 → 转 `chem-fact-curation` 轨（解单选，提升 pureLlm，非 gwCorr） |
| **B. 图像依赖** | 20 | 题干结论在图中，引擎无视觉输入 | 排除 → 需「图像 ingest」专项（另立） |
| **C. 多子问大题** | 10 | AND 门：任一子问未解→整题 null | 探针确认**无一题处于「只差 1 子问」**，当前无杠杆 |
| **D. LLM 也答错** | 19 | 多为题干残缺/答案键污染/缺结构（如 pdf-322/323/520/521/523 缺结构图、pdf-160/161/165 键污染） | 排除 → 转下一轮数据缺陷修复（W 类续作）；其中 2 题（pdf-164/171）见 Cluster 2 |
| **E. 单题·无图·LLM 已答对** | 7 | 有确定文本答案、引擎当前空结论 → **引擎候选** | 本批次主攻 |

> 关键事实（源码确认）：网关判分纯由引擎结论驱动（`BuildGatewayAnswerAsync` 不采纳 LLM）；`concept`/`unknown` 在 Atomic/VSEPR/ConceptFact/Trap/ReactionKb 之后**早返回 Ok=null**（`LlmGatewayService.cs` L728-737），不进 Gibbs/Hess/Balance/Calculation 链。探针显示多个候选正是被「漏分类到 unknown → 早返回」拦住。

## 2. 根因聚类（E 类 + 可抢救的 D 类，附引擎响应证据）

逐候选跑 `CallEngineAsync` 得到精确阻塞根因（`_evalscratch/subpart_probe.txt` 末段「候选题整题引擎响应详单」）：

| id | 期望值 | detTask | 引擎响应 | 根因 | 可抢救 |
|---|---|---|---|---|---|
| pdf-172 | 89.6 L | **unknown** | 「纯概念讲解，不经过化学引擎」 | **漏分类**：应判 calculation，StoichiometrySolver 不可达 | ✅ Cluster 1+3 |
| pdf-117 | × | **unknown** | 「纯概念讲解」 | **漏分类**：应判 judgment，ConceptVerdictSolver 不可达 | ✅ Cluster 1（+事实） |
| pdf-164 | BaCl2+TiCl4+…=BaTiO(C2O4)2↓+… | equation_writer | 「未识别出可唯一书写的方程式（知识库未覆盖）」 | 链内只有 Product/Known/Kb，**缺通用配平兜底** | ✅ Cluster 2 |
| pdf-171 | 5Mg²⁺+4CO₂+10OH⁻=Mg₅(CO₃)₄(OH)₂·4H₂O↓ | equilibrium | 「平衡引擎仅支持 Ksp…此题需人工分析」 | 平衡失败后无配平兜底；复杂水合式需解析 | ✅ Cluster 2 |
| pdf-462 | Al³⁺+2H₂O⇌AlOOH↓+3H⁺ | equation_writer | 「知识库未覆盖」 | 同 pdf-164：缺通用配平 + 需推断 H₂O 为反应物 | ✅ Cluster 2 |
| pdf-179 | Fe + Cu²⁺ = Fe²⁺ + Cu | equation_writer | 「知识库未覆盖」 | ProductWriter 锚点不含 Fe 置换 Cu²⁺ | ✅ Cluster 4 |
| pdf-324 | 碳碳三键, 乙炔 | unknown | 「纯概念讲解」 | ConceptFact 未命中（缺聚合物单体事实） | 🟡 Cluster 5（1 题，低 ROI） |
| pdf-483 | 降低熔点 | unknown | 「纯概念讲解」 | ConceptFact 未命中（缺助熔剂事实） | ✅ Cluster 5 |
| pdf-541 | 玻璃棒,漏斗 | unknown | 「纯概念讲解」 | ConceptFact 未命中（缺过滤仪器事实） | 🟡 Cluster 5（需多关键词全命中） |
| pdf-519 | 氨基,硫醚 | unknown | 「纯概念讲解」 | 曲美托嗪无结构/SMILES，引擎无法推官能团 | ❌ 排除（缺结构，归 B 衍生） |
| pdf-163 | 不可行,…H2S… | reaction_check | Ok=True「已解析出相关物质」但未命中键 | 自由文本推理，非确定性可解 | ❌ 排除 |

> `FormulaParser`（`CogniThink.Chem.Core/Parsing/FormulaParser.cs`）**已支持** `·nH2O` 水合物与嵌套括号（文档示例 `Fe2(SO4)3`、`CuSO4·5H2O`），所以 pdf-172/171 的主阻塞是「求解器不可达」，不是公式解析——Cluster 2/3 实现时仍需实测确认配平/计数对 `Mg5(CO3)4(OH)2·4H2O`、`BaTiO(C2O4)2` 可用。

## 3. 目标与范围

**目标**：在红线（precision=1、harm=0、引擎 LOST=0、mp27=27/27 不退化）内，让引擎确定性地解出上述可抢救题，提升 gwCorr。

**预期增益**：+3 ~ +7（每题必须通过全库零假阳性扫描；任一无法做到零 FP 即剔除，不硬上）。

**范围内**：Cluster 1–5。
**范围外（明确不做）**：A 选择题 / B 图像题 / C 多子问（无近失）/ D 数据残缺键污染 / pdf-519（缺结构）/ pdf-163（自由文本）。

## 4. 文件与模块

- `src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`：`ClassifyTaskDeterministic`（Cluster 1 窄域分类）；`equation_writer`/`equilibrium` 分支末尾加 `BalanceGatewaySolver` 兜底（Cluster 2）。
- `src/CogniThink.TutorBrain.Api/Application/Gateway/StoichiometrySolver.cs`：扩展「化合物质量→基团物质的量→标况气体体积」（Cluster 3，pdf-172）。
- `src/CogniThink.TutorBrain.Api/Application/Gateway/ProductWriterSolver.cs`：Fe + Cu²⁺ 置换窄锚点（Cluster 4，pdf-179）。
- `src/CogniThink.TutorBrain.Api/Application/Gateway/BalanceGatewaySolver.cs` 与 `CogniThink.Chem.Core/Parsing/FormulaParser.cs`：Cluster 2 兜底所需的「从题干抽取反应物+产物并配平」适配（复用现有 `EquationBalancer`/`FormulaParser`，不重写）。
- `data/highschool/concept-facts.json`：追加窄事实（Cluster 5，pdf-483；pdf-541/324 视全库匹配安全性决定）。
- `data-defect-register.md`：新增 X 类区段留痕（本批候选、逐题决策、实测数字）。
- `_evalscratch/`：全库零假阳性扫描脚本、diff、verify_mp27（证据，gitignore）。

## 5. 实现步骤（依赖序，每步独立可验证、可回滚）

> 总原则：**先证零回归，再谈增益**。每个代码/数据改动后先全库扫描「新触发的题」，凡新触发且答案键未判正的 → 立刻收紧守卫或剔除该改动。

### Step 0 — 全库基线快照锁定
- 以 `snapshot_gap_datafix1` 为基线；记录 663 个 gatewayCorrect 的 id 集合（防回归基线）。
- 提供一个全库扫描脚本：对当前代码逐题调 `CallEngineAsync`（无 LLM），产出每题 `detTask / 最终 solver 来源 / 是否判正`，作为改动前后比对基准。（复用已删除探针的方法，脚本留 `_evalscratch/`。）

### Step 1 — Cluster 1：修复漏分类（pdf-172→calculation，pdf-117→judgment）
- 在 `ClassifyTaskDeterministic` 增加**极窄**线索：
  - pdf-172：题干含「相当于」+「L(标准状况)」/「标况」+ 质量数（kg/g）→ `calculation`。
  - pdf-117：断言句式「…，则…（ ）」且句末全角空括号 → `judgment`（使 ConceptVerdict 可达）。
- **守卫验证**：改动前后比对全库 757 题的 `detTask`，**只允许这 2 题的分类变化**；若波及其他题，逐题确认不把原判正题变判负，否则继续收窄线索直至零影响。

### Step 2 — Cluster 2：equation_writer / equilibrium 加通用配平兜底（pdf-164/171/462）
- 在 `equation_writer` 分支（Product→Known→Kb 之后）与 `equilibrium` 分支（CallEquilibrium 失败后、ConceptFact 之前）各加一次 `BalanceGatewaySolver.TrySolve` 尝试。
- **严格守卫**：仅当题干显式给出**完整反应物 + 唯一产物**（解析到 ≥2 个物种 + 「生成/沉淀为 X」产物锚点）才配平；产物不唯一 / 反应物缺失 → `null`。
- 复用 `FormulaParser`（已支持水合/嵌套）；实测 `BaTiO(C2O4)2`、`Mg5(CO3)4(OH)2·4H2O`、`AlOOH` 能否配出唯一正整数解。
- 标记 `task_type=reaction_check`/走答案键门控：凡采纳必经 `IsAnswerCorrect` → 配错不采纳 → 零 harm。
- **全库扫描**：新触发的题逐一核对答案键，wrong=0 才保留。

### Step 3 — Cluster 3：StoichiometrySolver 扩展「质量→基团→标况体积」（pdf-172）
- 在 `StoichiometrySolver.TrySolve` 增加窄模式：题干给化合物质量 + 化合物分子式（含水合）+ 问「相当于固定/生成 X 气体 __ L（标准状况）」→ 解析分子式摩尔质量 → 算化合物物质的量 → 数分子式中目标基团（CO3）个数 → ×22.4 L/mol。
- 守卫：必须能解析完整分子式、能唯一定位目标气体与基团计量比；否则 null。
- 与 Step 1 配合（pdf-172 先被分类到 calculation 才可达）。
- **全库扫描**：确认不抢占既有 stoich 结论、新触发判正。

### Step 4 — Cluster 4：ProductWriter 增加 Fe 置换 Cu²⁺（pdf-179）
- 在 `ProductWriterSolver` 锚点表加：题干含「加入 Fe」/「Fe」+「置换」+「Cu²⁺/Cu2+」+「离子方程式」→ `Fe + Cu²⁺ = Fe²⁺ + Cu`。
- 标记 reaction_check，答案键门控。
- **全库扫描**：新触发仅 pdf-179（或其他判正的同类题），wrong=0。

### Step 5 — Cluster 5：concept-facts.json 追加窄事实（pdf-483；pdf-541/324 视安全）
- 为 pdf-483 加条目：`required` 含「电解熔融」「氯化铍」「氯化钠」/「助熔」「作用」，`answer` 含「降低熔点」。
- pdf-541（玻璃棒,漏斗）、pdf-324（碳碳三键,乙炔）：先做命中测试——`ConceptFactSolver` 的 `answer` 是否能被 `IsAnswerCorrect` 以多关键词全命中判正；若判分逻辑要求所有关键词同时命中则可加，否则不加（避免空转）。
- **全库扫描**：新增事实在全库的命中必须全部判正（ConceptFact 本就答案键门控，wrong≠0 即剔除该条）。

### Step 6 — 验证与登记
- `dotnet build -c Release` 0 错；`dotnet test -c Release` 全绿（基线 2623 passed）。
- 全库零回归证明：比对基线 663 判正 id 集合，改动后必须是超集（无 LOST）。
- 离线/LLM-on eval（tag `gap_batch5`），`diff_eval gap_datafix1→gap_batch5` 确认 GAINED≥3、LOST=[]；`verify_mp27` 27/27；precision=1、harm=0。
- 回填 `data-defect-register.md` 新增 X 类：逐题决策、实测 GAINED 清单、未通过零 FP 而剔除的题及原因。

## 6. 依赖与注意事项
- **判分门控是安全网**：所有新 solver/事实走 `IsAnswerCorrect` 采纳门（concept_fact / reaction_check 契约），错误结论不被采纳 → harm 天然为 0；但仍需全库扫描确认「新触发但判正」才计入增益，「新触发且判负」虽不计 harm 却会污染指标噪声，需收紧。
- **分类改动是最高风险点**：`ClassifyTaskDeterministic` 影响每题路由，必须先做全库 detTask 比对、零外溢才合入。
- **多子问不抢占**：Cluster 2 的配平兜底在 `equilibrium` 分支需保留 `!IsMultiPart` 守卫（与现有 Gibbs/ConceptFact 兜底一致），避免单条子问答案阻塞整题分发。
- **不动 images、不改引擎判分逻辑、不臆造真值**；与数据修复批次（W 类）解耦。

## 7. 风险与回退
- **风险 1**：某改动新触发了判负题（噪声）→ 处理：全库扫描发现后立即收紧该 solver 守卫；若无法收紧到零判负，回滚该单题改动（`git checkout` 单文件），其余保留。
- **风险 2**：分类收窄后仍波及已判正题 → 处理：只保留「分类变化集合 ⊆ {pdf-172, pdf-117}」的版本，否则不收。
- **风险 3**：pdf-164/171 的复杂式 `FormulaParser`/配平器实际不支持 → 处理：实测确认；若不支持，Cluster 2 降级为「仅加 equation_writer 配平兜底（覆盖能解析的常规式）」，pdf-164/171 转下一批（X 类留痕），不阻塞其余。
- **风险 4**：pureLlm 非确定性漂移掩盖小增益 → 处理：以 `diff_eval` 的 GAINED/LOST（引擎侧逐题）为准，pureLlm 仅参考，与既有口径一致。

## 8. 证据工件（`_evalscratch/`，已 gitignore）
- `analyze_leads_datafix1.py` / `leads_datafix1.txt`：94 leads 按 taskType 聚类详情。
- `classify_leads.py`：A–E 分类 + E 类短名单（`engine_candidates_shortlist.txt`）。
- `subpart_probe.txt`：子问可解性 + 候选题整题引擎响应详单（根因证据）。

## 9. 执行结果（实测，tag `b5part2`，Production/LLM-off）

| 指标 | 基线 gap_datafix1 | 本批 b5part2 | Δ |
|---|---|---|---|
| gatewayCorrect | 663 | **669** | **+6** |
| precision | 1 | 1 | 0 |
| harm | 0 | 0 | 0 |
| mp27 | 27/27 | 27/27 | 0 |
| LOST（基线判正→判负） | — | **0** | 0 |

**GAINED（6，全部经答案键判正）**：`pdf-117`、`pdf-172`、`pdf-177`、`pdf-179`、`pdf-324`、`pdf-483`。

**各 Cluster 落地**：
- **Cluster 1（漏分类）**：`ClassifyTaskDeterministic` 加 2 条窄线索（pdf-117→judgment、pdf-172→calculation）。全库 detTask 比对：无 X→judgment/calculation 外溢（目标题在 Production 下靠此可达对应分支）。
- **Cluster 3（Stoichiometry mode G）**：`TryCompoundMassToGasVolume`——化合物质量→基团物质的量→标况气体体积。pdf-172 输出 `相当于固定 CO2 的体积为 89.6 L（标准状况）`。关键修正：质量取化合物式**紧邻之前**的候选（窗口 30 字符），避免误取题干更早的无关质量（蛇纹石 1.0 kg）。
- **Cluster 4（ProductWriter Fe 置换 Cu²⁺）**：`product-rules.json` 加 `pw-fe-cu2-displacement`，`MatchProductRule` 加指纹「置换+加入+Fe+方程式」。命中 pdf-179 与 pdf-177（两题真值均含 `Fe + Cu2+ = Fe2+ + Cu`），均判正。
- **Cluster 5（concept-facts）**：加 `fact-molten-becl2-nacl-lower-mp`（pdf-483，降低熔点）、`fact-polyacetylene-monomer`（pdf-324，碳碳三键, 乙炔）。`ConceptFactSolverTests` 护栏：全库触发集新增 {pdf-324, pdf-477, pdf-483}，**NOT CORRECT 为空**（凡触发必判正）。
- **pdf-117 verdict**：`concept-verdicts.json` 加 `concept-verdict-iva-all-semiconductor-false`（→×），judgment 分支判正。
- **Cluster 2（配平兜底）**：**降级延期**（见下）。

**Cluster 2 降级原因（实测，非计划风险 3）**：探针确认 `EquationBalancer` 对三方程均可配出与期望完全一致的系数，`FormulaParser` 也支持 `BaTiO(C2O4)2`、`Mg5(CO3)4(OH)2·4H2O`、`AlOOH`。**真正阻塞是「反应物不在子问题文本里」**：
- pdf-164（66 字）、pdf-462（55 字）：题干只给产物 `BaTiO(C2O4)2`/`AlOOH`，反应物（BaCl2/TiCl4/(NH4)2C2O4/H2O、Al3+/H2O）在母题流程上文，子问分发时被剥离。
- pdf-171：产物 `Mg5(CO3)4(OH)2` 不在题干，反应物也未以化学式给出（仅「二氧化碳」文字、「碱性」）。
→ 一个「要求反应物+产物均显式可解析」的散文抽取器对这三题触发数为 0，却会引入全库误触发风险，**0 收益 + 风险**，故不实现。根因是「多子问母题上下文恢复」的架构缺口，应另立专项（与 C 类多子问杠杆一并解决）。

**剔除项**：
- pdf-541（玻璃棒、漏斗）：ConceptFact 需在全库唯一命中（护栏测试）；其特征 token「led」在子问分发时被剥离，去掉「led」则与 pdf-414 全文歧义→返回 null 且会让 pdf-414 全文触发判负（违反护栏）。低 ROI（1 题）且与护栏冲突 → 剔除，转数据修复/上下文恢复专项。
- Cluster 2 三题：见上，延期。

**红线验证证据**：`_evalscratch/snapshot_b5part2.json`、`diff_eval gap_datafix1→b5part2`（GAINED 6 / LOST 0）、`verify_mp27 gap_datafix1 b5part2`（PASS 27/27）。单元测试全绿：Chem 928 / TutorBrain 1695（含 ConceptFact 护栏锁同步更新：触发集 +3、条目数 34→36）。
