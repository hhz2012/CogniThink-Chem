# B-LlmMain 批：LlmGatewayService 主体 70 处 `new EngineResponse` 的 ReasoningNodes 线程化（统一推理链最后一批）

**父规格**：[unified-reasoning-dag/spec.md](../unified-reasoning-dag/spec.md)（P2-1.x 可溯源推理链，R4 红线：有真实锚点必须线程化、无锚点留空）。
**基线 checkpoint**：`after_b_mpdispatch`（B-外置 / B-judge-API / B-impurity / B-MpDispatch 已迁移，757 例）。红线锚点 `base_p0`。

## 1. 问题

统一推理链（父 spec R4）要求：网关生产者命中真实规则/知识时，必须在 `EngineResponse.ReasoningNodes` 中携带 `KnowledgeId`/`RuleId`；无真实锚点则留空。B 类审计（[data-defect-register.md §N.2](../../../data-defect-register.md) L616）确认 `LlmGatewayService.cs` 主体仍有 **66 处 `new EngineResponse`**（加 3 个 partial 文件 `.Graph`/`.Curve`/`.Molecule` 共 4 处，合计 **70 处 site**）**未设置 `ReasoningNodes`**——这是统一推理链迁移的最后一块、表面积最大的一块。

本批用运行时探针（非盲信登记）一手核实了基线：757 例中 **526 例「已解出（gatewayOk=true）但零节点」**，集中在 choice（467）、calculation（19）、equilibrium（14）、reaction_check（7）、ion_check（8）等 taskType。这些案例的结论正确但推理链为空，无法溯源到命中的知识/规则——与父 spec「引擎结论必须可溯源到真实锚点」的目标不符。

根因分三类（已逐 site 读码核实，非盲信登记）：
1. **传播缺口**：`CallChoice`（L931 起）有 15 个结构相同的包装点，从 `ChoiceVerifier.Outcome` 拷贝 `FinalResult`/`Reasons`/`ConstraintNote` 构造新 `EngineResponse`，但 `Outcome` 不含 `ReasoningNodes`——即便校验器能产节点也被丢弃；`CallEngineAsync` 的 curve_choice（L404）/concept_choice（L428）/trap（L540）/Hess（L1222）/narrow（L1235）包装点同理。
2. **有真实锚点但未挂 id**：`FactChoiceMatcher.TryDecide`（服务 Concept/Experiment/Organic/StructurePeriodic 4 个 choice 校验器，concept_choice 基线 250 命中）读取带 `Id` 字段的 JSON 事实库、且已逐选项追踪 `matched[0].Id`，但未把命中 id 写进 `Outcome`；`TryOrderAcidStrength`（equilibrium）读取 `acid-constants.json` 但未挂 `KnowledgeId`（HydrolysisPh 已示范此模式）。
3. **硬编码化学事实**：`StoichiometryStateTrap` 标况非气态、`TryCalculateKspPrecipitation` 的 `PrecipitateTypes` dict、`TryJudgeReactionFeasibility` 金属活动性规则等以 C# 字面量内联（无 `RuleId`/`KnowledgeId`），违反「化学事实只存 JSON」红线；其余纯计量计算（Hess/三段式/计量比）与引擎结果（离子共存检查/编译器/符号引擎/RDKit）无真实知识锚点，按 R4 应留空。

## 2. 目标

1. **全覆盖决策**：70 处 site 每一处都获得明确的 R4 处置——要么线程化（有真实 `KnowledgeId`/`RuleId` 锚点），要么留空（纯计量/引擎结果/守卫，文档化理由）。不留「未审计」site。
2. **线程化真实锚点**：
   - 扩展 `ChoiceVerifier.Outcome` 加性携带 `ReasoningNodes`；`FactChoiceMatcher.TryDecide` 收集命中事实 id 并构建节点（镜像 `JudgeApiReasoningNodes`：每命中一节点 + 末尾汇总，全空返回空列表 R4）；`CallChoice` 及各包装点把 `outcome.ReasoningNodes` 拷贝到父 `EngineResponse`。
   - 对其余读自有 JSON 的 choice 校验器逐评估：JSON 条目带 id 且逻辑可追踪命中 → 线程化；不带 id 但有自然查找键（如 formula）→ 用查找键作 `KnowledgeId`（R4：真实锚点，类比 HydrolysisPh 的 `kaKey`）；纯引擎/硬编码无干净锚点 → 留空。
   - `TryOrderAcidStrength` 读 `acid-constants.json` → 线程化每酸 `KnowledgeId`（HydrolysisPh 模式）。
3. **硬编码事实外置**：把 eval 有命中的硬编码化学事实（标况非气态 / Ksp 沉淀类型 / 金属活动性置换规则）外置为 JSON + 唯一 id，并在命中处线程化 `KnowledgeId`/`RuleId`（同 B-externalize / B-impurity 模式）。无命中的硬编码（如 coloredIons）留空（C，低 ROI，文档化）。
4. **R4 安全**：无真实锚点的 site 一律留空（空列表，非 null），不臆造节点；纯传播时子全空 → 父空。
5. **零行为漂移**：`Ok` / `FinalResult` / `DerivationSteps` / `ConstraintNote` / `Confidence` / 采纳门 / AND 判分 / `TaskType` / 所有 return-null 路径与基线字节级一致；**唯一**新增是 `ReasoningNodes` 赋值与外置事实 JSON。
6. **可测**：节点构建/合并逻辑抽为纯函数，单测直接验证正确性与 R4 安全；外置事实用 JSON 存在性 + 命中单测验证。

## 3. 非目标

- 不改任何判决字段（`Ok`/`FinalResult`/`DerivationSteps`/`ConstraintNote`/`Confidence`）、不改采纳门、不改 `IsAnswerCorrect` / AND 判分、不改 `TaskType` 赋值位置。
- 不臆造节点：纯计量计算、引擎结果、守卫/兜底、无干净锚点的硬编码规则一律留空，不为凑覆盖率挂假 id。
- 不改前几批已迁移的子求解器（CrystalCell/HalfReaction/AtomicStructure/Vsepr/ConceptFact/HydrolysisPh/JudgeApi/Impurity/ReactionKb 等）；本批只在 `LlmGatewayService` 消费/传播它们已产的节点（直接 `return Task.FromResult(solverResp)` 的路径本就传播，无需改）。
- 不改 `BuildGatewayAnswerAsync` / `ProcessStudentMessageAsync` 的判决/采纳逻辑（仅加性元数据透传）。
- 不提交 commit（除非用户明确要求）。

## 4. 约束与红线（必须全部守住）

- `harm=0`、`LOST=0`、`WORSEN=0`（离线 eval `diff_ckpt`：判决字段字节级不变 → GAINED/LOST/WORSEN/harm 全零）。
- **R4**：无真实锚点不臆造节点；纯传播子全空 → 父空；choice 校验器命中全空 → `Outcome.ReasoningNodes` 为空（不臆造汇总节点）。
- **仅加性**：不删改任何判决字段、return-null 路径、`TaskType`；外置 JSON 为新增文件，不改既有 JSON 语义（若需给既有 JSON 条目补 `id`，必须加性且不改变匹配行为）。
- **化学事实只存 JSON**：不把化学事实硬编码进 .cs；外置事实须经化学核对并落 JSON（同 B-externalize 模式）。
- **CS8601 教训**（b-impurity 批）：`EngineResponse.ReasoningNodes` 为非可空 `List<ReasoningNode>`（默认 `new()`），直接赋列表（空列表即可），**不得赋 null**。
- **不蒙数**：choice 校验器「任一选项无法干净判定 → 返回 null」铁律不变；外置事实不改变既有判定口径。
- Windows PowerShell 5（无 `&&`/`||`）；Python 路径 `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`；`_evalscratch/` gitignore；临时探针 `_Tmp*.cs` 用后必删。

## 5. 基线事实（已一手核实，非盲信登记）

- **70 处 site 精确定位**（grep `new EngineResponse` 实证）：`LlmGatewayService.cs` 66 处（L404–L2677）+ partial 4 处（`.Graph.cs` L36/L54、`.Curve.cs` L26、`.Molecule.cs` L47）。`LlmGatewayService.MultiPartDispatch.cs` 的 1 处已在 B-MpDispatch 批迁移，不计入本批。
- **526 个 solved-but-nodeless 案例分布**（python 探针解析 `checkpoint_after_b_mpdispatch.jsonl`，757 例，`gatewayOk==true && gatewayReasoningNodesCount==0`）：

  | taskType | 已解出零节点 | 主要来源（provenance / 结论特征） | R4 处置方向 |
  |---|---|---|---|
  | choice | 467 | concept_choice 250（FactChoiceMatcher 带 id JSON）+ 内部校验器 210 + curve_choice 6 + graph 1 | 线程化（T2a/T2b） |
  | calculation | 19 | 纯计量（Hess/计量比/FeBr2/三段式）| 留空 C |
  | equilibrium | 14 | Ksp 沉淀（硬编码 PrecipitateTypes）/ 酸顺序（读 acid-constants JSON）/ 守恒式（引擎）/ 三段式 | 酸顺序线程化（T3）；Ksp 外置（T4）；其余留空 C |
  | ion_check | 8 | `_chem.CheckCoexistence` 引擎结果 | 留空 C/R4 |
  | reaction_check | 7 | 金属活动性置换规则（硬编码）/ 编译器兜底 | 活动性外置（T4）；编译器留空 C |
  | concept_fact | 3 | 标况非气态（StoichiometryStateTrap 硬编码）2 + 半反应 1 | 标况态外置（T4） |
  | judgment | 3 | 未命中已迁移判断求解器的分支 | 留空 R4（逐点确认无锚点） |
  | unknown/structure_verify/vsepr | 4 | RDKit 分子分析（provenance=molecule）| 留空 C |
  | balance | 1 | 符号引擎配平 | 留空 C |

  **直接含义**：本批对 eval **不是**平凡零影响——choice（~250+）、equilibrium 酸顺序、Ksp、reaction_check 活动性等案例的 `gatewayReasoningNodesCount` 会**增长**（这是目标）。但判决字段（`gatewayCorrect`/`gatewayTruthAdopted`）字节级不变 → `diff_ckpt` 的 GAINED/LOST/WORSEN/harm **全零**（红线条），与 B-MpDispatch 的「平凡全零」不同。正确性靠单测 + 双 diff + 覆盖率脚本共同保证。
- **`FactChoiceMatcher` 已具备线程化基础**（读码核实）：`general-choice-facts.json` 等条目有 `Id` 字段（`FactStatement.Id`）；`JudgeStatementWithIds`（L140，B-judge-API 批）已实现「遍历命中 → 收集 `f.Id`」；`TryDecide`（L178）逐选项 `matched[0]` 已可得 `matched[0].Id`，仅未写入 `Outcome`。故 T2a 为低风险加性改动。
- **主文件唯一直接读 JSON**（grep `RuleStore`/`Query<` 实证）：`TryOrderAcidStrength`（L1998）读 `acid-constants.json`。其余 site 或纯计量、或硬编码 dict、或引擎结果。
- **partial 4 处无锚点**（读码核实）：`.Graph`（GraphSolver InferenceResult 引擎输出）、`.Curve`（曲线读数中间量）、`.Molecule`（RDKit 分析）——均无 `KnowledgeId`/`RuleId` 来源 → 留空（R4）。
- **投影/落盘链路确认**（同 B-MpDispatch）：`ProcessStudentMessageAsync` → `engineResponse` → `GatewayResult.EngineResponse` → [GatewayVsLlmEvaluationService.cs:212](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) 落盘 `gatewayReasoningNodesCount`；[GatewaySolveProjection.cs:75](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewaySolveProjection.cs) 透传前端。给 `EngineResponse.ReasoningNodes` 赋值即贯通。
- **契约**：`EngineResponse.ReasoningNodes` 非可空 `List<ReasoningNode>` 默认 `new()`（[GatewayContracts.cs:159](file:///d:/github/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs)）；`ReasoningNode` 字段见 [ReasoningContracts.cs:51-80](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs)（`Step` 从 1 连续无跳步 / `Description` / `Premises` / `RuleId?` / `KnowledgeId?` / `Conclusion?` / `EvidenceLevel`）。
- **聚合范式模板**（镜像）：[JudgeApiReasoningNodes.Build](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/JudgeApiReasoningNodes.cs)——每真实 id 一节点、`Step` 自增连续、末尾汇总节点；命中全空 → 返回空列表（调用方留空，R4）。
- **外置事实范式模板**（镜像）：B-impurity 批 `ImpurityTrapSolver` + `data/knowledge/...json`（化学事实落 JSON + 唯一 id + 命中处挂 `KnowledgeId`，单测 JSON 存在性 + 锚点引用）。
- **可测性**：`Api.csproj` L31 已设 `<InternalsVisibleTo Include="CogniThink.TutorBrain.Tests" />` → 节点构建辅助可声明 `internal static`，测试程序集直接调用。
- **全量测试基线**（B-MpDispatch 后）：Chem 913 + TutorBrain 1402 = **2315 passed / 0 failed**。build warning 基线：Debug/Release 13（7 预存代码 + 6 NETSDK1206 环境），0 新增为通过线。

## 6. 范围与逐点分类（70 site 处置）

> 实施时以代码语义为准（行号随编辑偏移）；下表为分类锚点，tasks.md 记录逐 site 审计结果。

| 处置类 | 含义 | site（代表行号/方法）| 数量 |
|---|---|---|---|
| **T1 传播复制** | 包装子响应的 site 加性拷贝 `<sub>.ReasoningNodes`（克隆，避免共享引用） | `CallChoice` 15 个包装点（L939–L1156）；`CallEngineAsync` curve_choice L404 / concept_choice L428 / trap L540 / Hess L1222 / narrow L1235 | ~20 |
| **T2a FactChoiceMatcher 线程化** | 扩展 `Outcome` 携带节点；`TryDecide` 收集命中 id → 构建节点；4 校验器（Concept/Experiment/Organic/StructurePeriodic）零改动即受益 | 经 T1 包装点消费 | 4 校验器（concept_choice 250 命中） |
| **T2b 自有 JSON 校验器** | 逐评估：带 id 且可追踪命中 → 线程化；用查找键作 KnowledgeId；无干净锚点 → 留空（R4） | Classification / Transformation / Na / IonicEquation / Redox / Equilibrium / Molecule / Counting / Stoichiometry / MultiSelect / CurveChoice | 11 校验器（逐个决策） |
| **T3 主文件读 JSON** | `acid-constants.json` → 每酸挂 `KnowledgeId`（HydrolysisPh 模式） | `TryOrderAcidStrength` L2033 | 1 |
| **T4 硬编码事实外置** | eval 有命中的硬编码化学事实 → 外置 JSON + id + 线程化 | 标况非气态（L540 trap，concept_fact 2 命中）/ `PrecipitateTypes`（L2102 Ksp，equilibrium ~4 命中）/ 金属活动性置换规则（L2455/2476/2493/2519/2538/2579/2649 feasibility，reaction_check ~3 命中） | 3 组事实 |
| **T5 留空（C/R4）** | 纯计量 / 引擎结果 / 守卫兜底 / 无命中硬编码 / ChoiceVerifier F1-F18 兜底 → 空列表（非 null），不臆造 | 所有 `TryCalculate*` 算术（L1278–L1791、L2233–L2330）；ion_check 主结果 L798；CallCompile L859；CallBalance L914；守恒式 L1978；`.Graph`/`.Curve`/`.Molecule` 4 处；所有 `Ok=null` 兜底（L568/589/639/689/699/1166/1245/1934）；可行性无规则分支 L2562/2603/2677；coloredIons（无命中，低 ROI）| ~40 |

**直接返回已迁移子求解器的路径**（`return Task.FromResult(crystal/half/atomic/bar/fact/eq/product/known/kb/v/hydrolysis)` 等）本就原样传播子响应节点，**无需改**——这些是前几批的成果，本批不重复劳动。

## 7. 验收标准（Acceptance Criteria）

> 类型仅 `rule`（客观可验二元）或 `rubric`（评分维度）。

- **AC1 (rule)**：T2a 线程化正确性。构造命中带 id 事实的选择题输入，`FactChoiceMatcher.TryDecide` 返回的 `Outcome.ReasoningNodes`：① 每个命中选项对应 ≥1 节点，`KnowledgeId` == 命中事实 `Id`、`EvidenceLevel==KnowledgeFact`；② `Step` 从 1 连续无跳步无重复；③ 末尾恰 1 个汇总节点，`Conclusion` == `Outcome.FinalResult`、`Premises` == 去重非空 `KnowledgeId`；④ 命中全空（返回 null 或无 id 命中）时 `ReasoningNodes` 为空（不臆造汇总）。证据：单元测试逐项断言。
- **AC2 (rule)**：T1 传播正确性。`CallChoice` 及 curve_choice/concept_choice/trap/Hess/narrow 包装点：子 `Outcome`/响应带节点时，父 `EngineResponse.ReasoningNodes` 包含全部子节点（克隆，`Step` 连续重排）；子空时父空（R4）；不修改子响应原节点对象。证据：单元测试 + 代码审查。
- **AC3 (rule)**：T2b 逐校验器 R4 合规。11 个自有 JSON 校验器每一个都有明确处置记录：线程化的须有单测证明 `KnowledgeId`/`RuleId` 来自真实 JSON 命中；留空的须有文档化理由（无干净锚点）。不得有未决策的校验器。证据：tasks.md 审计表 + 单测/文档。
- **AC4 (rule)**：T3 酸顺序线程化。`TryOrderAcidStrength` 命中时节点 `KnowledgeId` 为对应酸的 acid-constants key（同 HydrolysisPh `kaKey` 模式），`Step` 连续，命中为空时留空。证据：单元测试。
- **AC5 (rule)**：T4 外置事实合规。3 组硬编码事实外置为 JSON 文件（化学事实不硬编码进 .cs），有唯一 id；命中处线程化 `KnowledgeId`/`RuleId`；既有判定口径字节级不变（外置前后对同输入的 `FinalResult`/`Reasons` 一致）。证据：JSON 文件存在 + 单测命中 + 行为等价断言。
- **AC6 (rule)**：R4 全空安全。所有 T5 留空 site 的 `ReasoningNodes` 为空列表（非 null）；T1/T2a 子全空 → 父空；choice 校验器命中全空 → 不臆造汇总。证据：grep 无 `null` 赋给 `ReasoningNodes` + 单元测试 + 代码审查。
- **AC7 (rule)**：零行为漂移。`Ok` / `FinalResult` / `DerivationSteps` / `ConstraintNote` / `Confidence` / 采纳门 / AND 判分 / `TaskType` / 所有 return-null 路径与基线字节级一致；唯一新增是 `ReasoningNodes` 赋值与外置 JSON。证据：`git diff` 审查 + 既有测试全过。
- **AC8 (rule)**：`dotnet build` Debug + Release 0 error；本批改动/新增文件在 warning 中命中 0 次（0 新增 warning；基线 13 = 7 预存代码 + 6 NETSDK1206 环境，不计）。证据：build 输出 + grep。
- **AC9 (rule)**：全量 `dotnet test CogniThink.Chem.sln` 0 failed、0 新增跳过（基线 2315 passed 维持 + 本批新增测试）。证据：`dotnet test` 汇总行。
- **AC10 (rule)**：离线 eval 新 checkpoint（tag `after_b_llmmain`）对 `after_b_mpdispatch` 的 `diff_ckpt`：`GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS`（判决字段字节级不变）；对 `base_p0` 的 `diff_ckpt`：`LOST=0 / WORSEN=0 / harm=0 → PASS`。证据：`diff_ckpt.py` 输出。
- **AC11 (rule)**：覆盖率自检（`_evalscratch/verify/b_llmmain_coverage.py`，gitignored）：① `after_b_mpdispatch → after_b_llmmain` 的 `gatewayCorrect`/`gatewayTruthAdopted` 零翻转（与 diff 互证）；② `gatewayReasoningNodesCount` 只增不减（无案例减少）；③ 获增节点的案例集合 ⊆ 基线 `gatewayOk==true && gatewayReasoningNodesCount==0` 的 526 例（即不给未解出/已有节点的案例乱加节点）；④ 至少 choice concept_choice 类、equilibrium 酸顺序类有节点增长（证明线程化生效）；输出 `ALL ASSERTIONS PASSED`。证据：脚本输出。
- **AC12 (rule)**：台账更新：[data-defect-register.md §N.2](../../../data-defect-register.md) L616 行标 ✅ 已迁移（B-LlmMain 批，最后一批）→ 分级 `A·done`；分级统计 A·done 11→**12**、混合 1→**0**、C 维持 1（RedoxRatioCalculator）；建议执行顺序 (5) 标完成（统一推理链 P2-1.x 全部收口）；父 [tasks.md](../unified-reasoning-dag/tasks.md) 同步。证据：文件 diff + grep 无残留未迁移标记。
- **AC13 (rubric)**：代码质量。节点构建/合并抽为纯 `internal static` 函数（镜像 `JudgeApiReasoningNodes`），可独立单测、无副作用；子节点原样克隆（仅重排 `Step`，不改 `KnowledgeId`/`RuleId`/`Conclusion`/`EvidenceLevel`）；外置事实只存 JSON、无 .cs 硬编码；无密钥、无 `_Tmp*.cs` 残留。评分 1-5（1=有臆造或硬编码事实进 .cs；3=可单测但子节点字段被改写或锚点不真；5=纯函数、真锚点、R4 安全、事实全 JSON），通过阈值 ≥ 4。证据：代码 + 单测 + grep。

## 8. 开放问题

- [x] **eval 节点计数会增长是否违反红线**：否。红线是 `harm=0/LOST=0/WORSEN=0`（判决字段不变），节点计数增长是本批目标（给已解出案例补可溯源性）。`diff_ckpt` 只衡量判决翻转，会全零；覆盖率脚本（AC11）单独约束节点增长的合法性（只增不减、仅 526 例子集）。已在 §5 实证并定 AC。
- [ ] **T2b 自有 JSON 校验器的最终线程化/留空清单**：实施时逐个读码核实（JSON 是否带 id、逻辑是否可追踪命中），在 tasks.md 审计表落定；无干净锚点的留空并文档化（R4），不为覆盖率强行挂 id。不阻塞 spec 批准。
- [ ] **T4 外置事实的精确 JSON 结构与 id 命名**：实施时按 B-externalize/B-impurity 范式确定，须经化学核对；若某组事实外置风险过高，则降级为留空（C）并文档化，不阻塞本批其余工作。不阻塞 spec 批准。
