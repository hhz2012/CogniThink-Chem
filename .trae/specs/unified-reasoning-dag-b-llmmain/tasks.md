# B-LlmMain 批 实现任务队列

> 父规格：[spec.md](./spec.md)。基线 checkpoint：`after_b_mpdispatch`。红线锚点 `base_p0`。
> 状态字段仅取：pending / in_progress / blocked / completed / cancelled。
> 每个任务完成时回填「Completion Evidence」。

## 关键基线事实（决定 AC 写法，从 `after_b_mpdispatch` 一手捕获）

| 项 | 基线值（运行时实证） | 本批后期望 |
|---|---|---|
| 70 处未迁移 site | 主文件 66 + partial 4（grep 实证） | 全部获得 R4 处置（线程化 or 留空） |
| solved-but-nodeless 案例 | **526 例**（gatewayOk=true 且节点=0） | 子集获增节点（choice ~250+ / 酸顺序 / Ksp / 活动性），其余留空 |
| `diff_ckpt after_b_mpdispatch→after_b_llmmain` | — | GAINED/LOST/WORSEN/harm **全零**（判决字段不变），RED-LINE PASS |
| `gatewayReasoningNodesCount` 变化 | — | **只增不减**，且增长案例 ⊆ 526 例 solved-but-nodeless 子集（AC11） |
| 全量 test 基线 | Chem 913 + TutorBrain 1402 = **2315 passed / 0 failed** | 0 failed、0 新增跳过 + 本批新增测试 |
| build warning 基线 | 13（7 预存代码 + 6 NETSDK1206 环境） | 0 新增 warning |

> 与 B-MpDispatch 不同：本批 eval 节点计数**会增长**（目标），但判决字段字节级不变 → diff 红线条全零。正确性靠单测 + 双 diff + 覆盖率脚本共同保证。

---

## Task 1: 扩展 ChoiceVerifier.Outcome + FactChoiceMatcher 线程化（T2a 核心）

- **Status**: completed
- **Depends On**: —
- **Priority**: high
- **覆盖 AC**: AC1, AC6, AC13
- **Changes**:
  1. 改 `ChoiceVerifier.cs` 的 `Outcome` record：加性增加 `ReasoningNodes` 属性（`init`，默认 `new()`），**不破坏**现有 4 参数构造调用点（实施前 grep 确认无位置解构依赖）。
  2. 改 `FactChoiceMatcher.TryDecide`：在逐选项匹配时收集 `matched[0].Id`（非空），命中判定口径**字节级不变**；在返回 `Outcome` 前用纯函数构建 `ReasoningNodes`（镜像 `JudgeApiReasoningNodes`：每命中 id 一节点、`Step` 自增连续、末尾 1 个汇总节点 `Conclusion`=FinalResult、`Premises`=去重非空 KnowledgeId；命中 id 全空 → 空列表，不臆造汇总）。
  3. 节点构建抽为 `internal static` 纯函数（镜像 `JudgeApiReasoningNodes`），可独立单测。
  4. `Ok`/`FinalResult`/`Reasons`/`ConstraintNote`/return-null 路径一律不动（AC7）。
- **Test Requirements**:
  - (rule) 新建 `tests/CogniThink.TutorBrain.Tests/FactChoiceMatcherNodesTests.cs`：
    - AC1：构造命中带 id 事实的输入 → `Outcome.ReasoningNodes` 每命中选项 ≥1 节点、`KnowledgeId`==事实 Id、`EvidenceLevel==KnowledgeFact`、`Step` 连续无跳步、末尾恰 1 汇总节点且 `Conclusion`==FinalResult、`Premises`==去重非空 Id 集合。
    - AC6：命中全空（返回 null 或无 id 命中）→ `ReasoningNodes` 为空（`Assert.Empty`，不臆造汇总）。
    - AC1：`Assert.Equal(Enumerable.Range(1,K), nodes.Select(n=>n.Step))`。
  - (rule) 既有 choice 相关测试全过（加性不破坏判定口径）。
  - (rule) AC13：grep 本批 .cs 无密钥、无硬编码化学事实。
- **Completion Evidence**: ✅ 2026-11-22：扩展 `ChoiceVerifier.Outcome` 加性携带 `ReasoningNodes`（init 属性，默认 `new()`，不破坏现有 4 参数构造与属性访问）；新建 `ChoiceReasoningNodes` 纯构建器（镜像 `JudgeApiReasoningNodes`：每命中 id 一节点 + 末尾汇总，命中全空→空列表不臆造汇总，`IsNullOrWhiteSpace` 防空白 id 伪锚点）；`FactChoiceMatcher.TryDecide` 收集 `matched[0].Id` 构建节点并写入 `Outcome`（4 校验器 Concept/Experiment/Organic/StructurePeriodic 零改动受益）。Api build 0 error、0 新增 warning（仅剩预存 Adaptive×2/CrystalCell×1/NETSDK1206）。`FactChoiceMatcherNodesTests` 7/7 通过：AC1 结构（每 id 一节点/Step 1..N 连续/汇总 Conclusion==FinalResult/Premises 去重）、AC6 命中全空不臆造、AC7 判决字段不漂移；端到端验证 967 条真实 JSON id 流入节点且无臆造。TutorBrain 全量 1409 passed（基线 1402 + 本批 +7）、0 failed、0 新增跳过（AC9 子集）。

---

## Task 2: 传播点复制（T1）+ TryOrderAcidStrength 线程化（T3）

- **Status**: completed
- **Depends On**: Task 1
- **Priority**: high
- **覆盖 AC**: AC2, AC4, AC7
- **Changes**:
  1. **T1 传播复制**：给所有包装子响应的 `new EngineResponse` 加性赋值 `ReasoningNodes = <sub>.ReasoningNodes`（克隆，避免共享引用；空子/空列表 → 父空列表，不赋 null——CS8601 教训）。覆盖：
     - `CallChoice` 15 个包装点（L939–L1156）：拷贝 `outcome.ReasoningNodes`。
     - `CallEngineAsync`：curve_choice L404（`curChoice.ReasoningNodes`）、concept_choice L428（`con.ReasoningNodes`）、trap L540（`t` 若带节点）、Hess L1222（`h2` 若带节点）、narrow L1235（`n` 若带节点）。
     - 对 `ChoiceVerifier.Outcome` 来源：直接取 `outcome.ReasoningNodes`（Task 1 已扩展）。
  2. **T3 酸顺序**：`TryOrderAcidStrength`（读 `acid-constants.json`）命中时构建节点，每酸挂 `KnowledgeId`（同 HydrolysisPh `kaKey` 模式，如 `CH3COOH:Ka1`）、`Step` 连续、`EvidenceLevel=KnowledgeFact`；命中为空时留空（R4）。
  3. 节点构建/克隆抽纯函数（复用 Task 1 模式或 `MultiPartReasoningNodes` 风格）。
  4. **直接返回已迁移子求解器的路径**（crystal/half/atomic/bar/fact/eq/product/known/kb/v/hydrolysis）确认原样传播，无需改；如发现有剥离节点的中间构造，一并修复。
  5. `Ok`/`FinalResult`/`DerivationSteps`/`ConstraintNote`/`Confidence`/return-null/`TaskType` 一律不动（AC7）。
- **Test Requirements**:
  - (rule) AC2：单测断言包装点带子时父节点含全部子节点（克隆后原子响应未被修改）、子空时父空。
  - (rule) AC4：`TryOrderAcidStrength` 单测断言命中时节点 `KnowledgeId` 为 acid-constants key、`Step` 连续、空命中留空。
  - (rule) AC7：既有 `LlmGatewayService` 相关测试全过（行为字节级不变）。
- **Completion Evidence**: ✅ 2026-11-22：T1 传播复制——新建 `GatewayReasoningNodes.Clone` 内部深拷贝 helper（null/空→空列表不赋 null，CS8601 安全；Premises 深拷贝隔离父子引用；跳过 null 元素），给 17 个 `ChoiceVerifier.Outcome` 来源包装点加性赋值 `ReasoningNodes = GatewayReasoningNodes.Clone(<x>.ReasoningNodes)`：CallEngineAsync curve_choice(L404)/concept_choice(L428) + CallChoice 15 个（cls/trn/nac/ieq/redox/eqc/org/sp/exp/con/molc/cnt/stoi/multi/outcome）。trap(L540)/Hess(L1222)/narrow(L1235) 包装的是非 Outcome 类型（tuple / 纯计算），无 ReasoningNodes 字段 → 留待 Task 4/5，不臆造（R4）。直接返回子求解器（crystal/half/atomic/bar/fact/eq/product/known/kb/hydrolysis）的路径原样传播，无需改。T3 酸顺序——`TryOrderAcidStrength` 的 acidInfo 元组加性存储原始 JSON 键 kaKey；新建 `AcidOrderReasoningNodes.Build` 纯构建器（每酸一节点 KnowledgeId=kaKey / EvidenceLevel=KnowledgeFact / Conclusion=酸名 + 末尾汇总 Conclusion=FinalResult / Premises=去重非空 kaKey；kaKey 全空白 → 空列表不臆造汇总，R4），命中处写入 ReasoningNodes；FinalResult 提取为局部变量但字符串构造字节级不变（AC7）。新增 `GatewayReasoningNodesTests` 10/10 通过：AC2 克隆全字段保留 + 深拷贝隔离（源/克隆互不影响）+ null/空→空 + 跳过 null 元素 + Outcome→父传播语义（子空父空）；AC4 结构（每酸一节点 / Step 1..N 连续 / 汇总 Conclusion==FinalResult / Premises 去重）、R4 全空/空白锚点不臆造、端到端真实 `acid-constants.json` kaKey 经 `TryOrderAcidStrength` 流入（由弱到强 / 由强到弱两方向均验证）、缺方向→null；AC7 FinalResult 字节级相等断言。Api build 0 error、0 新增 warning（仅剩预存 Adaptive×2 / CrystalCell×1）。TutorBrain 全量 1419 passed（基线 1409 + 本批 +10）、0 failed、0 新增跳过（AC7 / AC9 子集）。

---

## Task 3: 自有 JSON choice 校验器逐个评估 + 线程化/留空（T2b，含审计表）

- **Status**: completed
- **Depends On**: Task 1
- **Priority**: high
- **覆盖 AC**: AC3, AC6
- **Changes**:
  1. 逐个读码核实 11 个自有 JSON 校验器（Classification / Transformation / Na / IonicEquation / Redox / Equilibrium / Molecule / Counting / Stoichiometry / MultiSelect / CurveChoice）：
     - 其 JSON 条目是否带 `Id`（或 `id`）字段？
     - 命中逻辑是否可识别命中条目（可拿到 id 或查找键）？
  2. 处置决策（每个校验器落 tasks 审计表）：
     - 带 id 且可追踪命中 → 收集命中 id，构建节点写入 `Outcome.ReasoningNodes`（复用 Task 1 纯函数），`CallChoice` 经 T1 自动消费。
     - 不带 id 但有自然查找键（如 `formula`）→ 用查找键作 `KnowledgeId`（R4：真实锚点，类比 `kaKey`）；如需给 JSON 条目加性补 `id`，不得改变匹配行为。
     - 纯引擎/硬编码/无干净锚点 → 留空（R4），文档化理由。
  3. `ChoiceVerifier` F1-F18 兜底（L1153 附近）：硬编码可行性规则，无 JSON id → 留空（R4/C），文档化。
  4. 判定口径字节级不变（AC7）；仅加性写 `ReasoningNodes`。
- **Test Requirements**:
  - (rule) AC3：每个线程化的校验器有单测证明 `KnowledgeId`/`RuleId` 来自真实 JSON 命中；每个留空的校验器在审计表有文档化理由。
  - (rule) 既有各校验器测试全过（判定口径不变）。
- **Completion Evidence**: ✅ 2026-11-22：委托只读子代理精读 11 个校验器源码 + 对应 JSON，逐个核实锚点与命中可追踪性，主代理复核后实现。**审计表**（THREAD 5 / LEAVE 6；另 Concept/Experiment/Organic/StructurePeriodic 4 个已在 Task 1 经 FactChoiceMatcher 线程化，不在本清单）：

| 校验器 | 加载 JSON | 锚点字段 | 命中可追踪 | 处置 | 理由 |
|---|---|---|---|---|---|
| EquilibriumChoiceValidator | equilibrium-rules.json | `id`（如 eq-005-c） | 是，`matched[0].Id` 已在用 | **THREAD** | 最干净：`rule.Id` 已写 reasons，收集后写 ReasoningNodes |
| MultiSelectChoiceValidator | 经 CompositeStatementJudge → 6 个 choice-facts | `id`（如 GC-004-A） | 是，`JudgeWithIds` 返回 HitIds | **THREAD** | 换用已存在的 `JudgeWithIds` 重载；引擎子判决（Counting/RedoxElectron）返回空 id（R4） |
| CountingChoiceValidator | counting-facts.json | `id`（如 BENZENE-1CL） | 仅事实表路径 | **THREAD（事实表）** | `JudgeCountOption` 返回三元组带 `factId`；枚举路径（ConstitutionalIsomerCounter）与子项/组合路径无锚点→留空（R4） |
| ClassificationValidator | substance-classification.json | `formula`（稳定主键） | 是，`FindSubstance` 返回对象 | **THREAD** | `formula` 类比 `kaKey` 先例（spec §3 已批准为自然键）；给 EvalOption/EvalTagChecks/EvalRelation 加 `out hitFormulas` |
| NaChoiceValidator | na-particle-facts.json | `formula`（稳定主键） | 是，`Lookup` 返回对象 | **THREAD** | `ParseOption` 返回三元组带 formula；未命中物质→formula=null→空（R4） |
| TransformationChainValidator | transformation-rules.json | 无 id，`(from,to,reagents,conditions)` 复合键 | 部分，但无单字段稳定锚点 | **LEAVE** | 复合键非指定稳定 id，R4 不臆造 |
| IonicEquationValidator | ion-conflicts / ion-solution-context / infeasible-patterns | 三者均无 id | 混合引擎+复合离子键 | **LEAVE** | 共存冲突复合键、方程正误主要来自引擎守恒，无干净锚点 |
| RedoxChoiceValidator | electrode-reaction-facts.json | 无 id（`correct`/`incorrect` 为字符串数组） | 命中仅为数组元素，部分判错来自引擎 | **LEAVE** | 无条目 id，部分判错为引擎计算 |
| MoleculeChoiceValidator | 无（RDKit MoleculeAnalyzer 引擎） | — | 不可追踪 | **LEAVE** | 纯引擎图像分子计算 |
| StoichiometryChoiceValidator | 无（引擎配平/摩尔/电子转移） | — | 不可追踪 | **LEAVE** | 纯引擎计算 |
| CurveChoiceValidator | 无（CurveQuery 图像点集计算） | — | 不可追踪 | **LEAVE** | 纯引擎/图像资产计算 |

实现均为加性：仅给返回 `Outcome` 时赋值 `ReasoningNodes = ChoiceReasoningNodes.Build(optionHits, finalResult)`（Task 1 已让 Outcome 携带该字段、Task 2 已让 CallChoice 克隆传播，故校验器侧赋值即贯通）；判决字段（AnswerLetter/FinalResult/Reasons/ConstraintNote）字节级不变（AC7）。`ChoiceVerifier` F1-F18 兜底（硬编码可行性规则，无 JSON id）→ 留空（R4/C，Task 5 文档化）。新增 `ChoiceValidatorNodesTests` 6/6 通过：Equilibrium(pdf-005→C) / Classification(裸物质→B) / Na(粒子断言→A) / MultiSelect(pdf-225→CD) 各断言节点 KnowledgeId 真实存在于对应 JSON（R4 不臆造）+ AnswerLetter 不变；Counting 事实表路径(→A) 断言真实 fact.Id；Counting 枚举路径(羧酸→A) 断言 ReasoningNodes 为空（R4 无锚点不发节点）。Api build 0 error、0 新增 warning。TutorBrain 全量 1425 passed（基线 1419 + 本批 +6）、0 failed（AC3/AC6/AC7/AC9 子集）。

---

## Task 4: 硬编码化学事实外置 + 线程化（T4）

- **Status**: completed
- **Depends On**: Task 2
- **Priority**: high
- **覆盖 AC**: AC5, AC6
- **Changes**:
  1. **标况非气态**（concept_fact 2 命中：SO3/己烷等标况非气态，`StoichiometryStateTrap` 硬编码）→ 外置 `data/knowledge/standard-state-phases.json`（或合适路径，镜像 B-impurity），每条唯一 id；`trap` 命中处线程化 `KnowledgeId`（经 T1 L540 包装点消费）。
  2. **Ksp 沉淀类型**（equilibrium ~4 命中：「X- 先沉淀」，`TryCalculateKspPrecipitation` 用 `PrecipitateTypes` 硬编码 dict）→ 外置 JSON + 唯一 id（Ksp 常数来源）；命中处线程化 `KnowledgeId`（L2102）。
  3. **金属活动性置换规则**（reaction_check ~3 命中：Zn+H2、Cu>Ag 等，`TryJudgeReactionFeasibility` 硬编码规则 + `activitySeries`）→ 引用既有 `data/highschool/activity-series.json`，为置换规则分配 `RuleId`（或外置规则 JSON）；命中处线程化 `RuleId`（L2455/2476/2493/2519/2538/2579/2649）。
  4. 每组外置事实：经化学核对；既有判定口径字节级不变（外置前后对同输入 `FinalResult`/`Reasons` 等价）；无命中的硬编码（如 coloredIons）留空（C），文档化低 ROI，不强行外置。
  5. 若某组外置风险过高 → 降级为留空（C）并文档化，不阻塞其余（spec OQ）。
- **Test Requirements**:
  - (rule) AC5：JSON 文件存在 + 含唯一 id；单测命中处 `KnowledgeId`/`RuleId` 指向真实条目；行为等价断言（外置前后同输入结论一致）。
  - (rule) AC13：grep 本批 .cs 无化学事实硬编码（事实全在 JSON）。
- **Completion Evidence**: ✅ 2026-11-22：三组硬编码事实外置 + 线程化，全部加性、判定口径字节级不变（AC7）：
  - **组1 标况非气态**：新建 `data/knowledge/standard-state-phases.json`（3 条，每条唯一 id）；`StoichiometryStateTrapSolver.TrySolve` 改返回 3 元组 `(FinalResult, Note, HitId?)`，命中时 `LlmGatewayService.cs` trap 包装点内联 `ReasoningNodes` 挂 `KnowledgeId = t.HitId`（`IsNullOrWhiteSpace` 过滤空白伪锚点；未命中 solver 返回 null、不到达包装点）。`Batch2NarrowSolversTests` 14/14 兼容通过（`.Value.FinalResult`）；新建 `StoichiometryStateTrapNodesTests` 5/5：HitId 真实锚点 + 下标 ₃ 字节级保留 + JSON 唯一 id + 未命中 null。
  - **组2 Ksp 沉淀类型**：新建 `data/highschool/precipitate-types.json`（23 条，键/值字节级精确复制原硬编码 dict，含括号键 `Mg(OH)2` 等，每条 `id:"ppt:{formula}"`）；新建 `PrecipitateReasoningNodes.Build(List<PrecipitateHit>, finalResult)` 纯函数（每 formula 一节点 KnowledgeId=条目 id + 末尾汇总；命中全空→空列表不臆造，R4）；`LlmGatewayService.cs` `TryCalculateKspPrecipitation` 替换硬编码 `PrecipitateTypes` dict 为 `Lazy<Dictionary<...>>` 从 JSON 加载（`RuleStore.Load().Query<PrecipitateTypesData>`，Ordinal 查找，try/catch 降级空表），元组加性携带 id，return 处加性写入 `ReasoningNodes`（修复 `Lazy<>.TryGetValue` → `.Value.TryGetValue` 的 CS1061）。`EquilibriumSolverTests` +4 → 14/14（Ksp 行为等价 + 节点 + AC5）。
  - **组3 金属活动性**：新建 `ActivityReasoningNodes`（5 方法：HydrogenPosition/MetalDisplacement/HighlyActiveWaterFirst/InertMetalOxidizingAcid/ActiveMetalOxidizingAcid，KnowledgeId="activity-series" + 派生稳定 RuleId；MetalDisplacement 第二节点 RuleId=RED-001、KnowledgeId=KH-IN-06，来自 rules.json）；`LlmGatewayService` 加 `ActivitySeries` 懒加载字段（从 `activity-series.json`，原硬编码数组字节级一致：15 位、H 在 index 9）；`TryJudgeReactionFeasibility` 内 `var activitySeries = ActivitySeries.Value;`；线程化 8 个活动性分支。`ActivityReasoningNodesTests` 11 项构建器/端到端通过（Task 5 补 3 端到端 + 3 构建器直接单测后共 16/16）。
  - **台账**：`tools/data-assets/asset_map.py` 补 6 条精确映射（impurity-traps / standard-state-phases / known-equations / product-rules / redox-rules / correctness-context-rules；其中 5 个为基线存量 UNMAPPED、1 个本批新增），精确项排在 `reactions/` 前缀之前保证精确优先；重跑 `gen_inventory.py` → exit 0、unmapped=0，`data-asset-manifest.json` + `data-asset-inventory.md` 已重生成。
  Api build 0 error / 0 新增 warning。AC13：grep 本批改动 .cs 无化学事实硬编码（事实全在 JSON / activity-series 外置）。

---

## Task 5: C/R4 留空审计（T5 文档化，确认无臆造）

- **Status**: completed
- **Depends On**: Task 2, Task 3, Task 4
- **Priority**: high
- **覆盖 AC**: AC6, AC7
- **Changes**:
  1. 逐 site 确认 T5 留空处置（~40 处）：纯计量计算（所有 `TryCalculate*` 算术 + Hess + 三段式 + 离子浓度比较）、引擎结果（ion_check 主结果 / CallCompile 编译器 / CallBalance 符号引擎 / 守恒式 / `.Graph`/`.Curve`/`.Molecule`）、所有 `Ok=null` 守卫兜底、可行性无规则分支、coloredIons（无命中）。
  2. 每处留空 site 确认 `ReasoningNodes` 为空列表（非 null，CS8601）、未臆造节点。
  3. 在 tasks.md 审计表汇总「70 site 全处置清单」（site / 处置 / 锚点或理由），作为父 spec 收口证据。
- **Test Requirements**:
  - (rule) AC6：grep `ReasoningNodes` 赋值无 `= null`（空列表安全）；留空 site 无节点构造。
  - (rule) AC7：`git diff` 审查无判决字段改动。
- **Completion Evidence**: ✅ 2026-11-22：委托 fresh-context 只读子代理精读 `LlmGatewayService.cs` + 4 个分部（`.Graph.cs`/`.Curve.cs`/`.Molecule.cs`/`.MultiPartDispatch.cs`）全部 `new EngineResponse` 站点，主代理复核代码锚点（含 L2539/L2770 实读确认）后落表。**权威审计：71 site（主文件 66 + 分部 5），有效 THREAD 24 / 有效 LEAVE 47 / 0 未归类、0 漏线程机会**（取代早期估算的 67：grep 实证主文件 66 + 分部 5 = 71）。

  **71 site 全处置清单**（`cs` = `LlmGatewayService.cs`；处置为**有效**口径——包装点运行 `GatewayReasoningNodes.Clone(<child>.ReasoningNodes)`，但子校验器留空时克隆空列表→有效 LEAVE，与 Task 3 决策一致，不臆造节点）：

  | # | File:Line | Method/Context | 处置 | 锚点 / R4 理由 |
  |---|---|---|---|---|
  | 1 | cs:404 | CallEngineAsync / curve_choice (CurveChoiceValidator) | LEAVE | 子无 JSON id（Task 3 LEAVE），克隆空 |
  | 2 | cs:429 | CallEngineAsync / concept_choice (ConceptChoiceValidator) | THREAD | 传播 ← ConceptChoiceValidator（FactChoiceMatcher 真实 id） |
  | 3 | cs:542 | CallEngineAsync / StoichiometryStateTrapSolver | THREAD | standard-state-phases，内联 KnowledgeId=t.HitId（未命中 solver 返回 null） |
  | 4 | cs:586 | CallEngineAsync / concept\|unknown 不走引擎 | LEAVE | Ok=null guard / early-return |
  | 5 | cs:607 | CallEngineAsync / balance 未唯一配平 | LEAVE | Ok=null guard |
  | 6 | cs:620 | CallEngineAsync / judgment ?? fallback | LEAVE | Ok=null guard |
  | 7 | cs:657 | CallEngineAsync / equation_writer 未命中 | LEAVE | Ok=null guard |
  | 8 | cs:707 | CallEngineAsync / vsepr 未命中 | LEAVE | Ok=null guard |
  | 9 | cs:717 | CallEngineAsync / default task type 未建模 | LEAVE | Ok=null guard |
  | 10 | cs:770 | CallIonCheck / 有色离子排除 | LEAVE | 硬编码 coloredIons 无命中（C，低 ROI 不强行外置） |
  | 11 | cs:789 | CallIonCheck / 加铝放H2 酸性共存 | LEAVE | 引擎结果（ion coexistence engine） |
  | 12 | cs:803 | CallIonCheck / 加铝放H2 碱性共存 | LEAVE | 引擎结果 |
  | 13 | cs:816 | CallIonCheck / 最终共存判定 | LEAVE | 引擎结果 |
  | 14 | cs:877 | CallCompile / 编译器 Evaluate | LEAVE | 符号引擎（CallCompile） |
  | 15 | cs:932 | CallBalance / 配平 | LEAVE | 符号引擎（CallBalance） |
  | 16 | cs:964 | CallChoice / ClassificationValidator | THREAD | 传播 ← ClassificationValidator（formula 主键） |
  | 17 | cs:981 | CallChoice / TransformationChainValidator | LEAVE | 子无单字段稳定 id（Task 3 LEAVE），克隆空 |
  | 18 | cs:997 | CallChoice / NaChoiceValidator | THREAD | 传播 ← NaChoiceValidator（formula 主键） |
  | 19 | cs:1013 | CallChoice / IonicEquationValidator | LEAVE | 子复合离子键/引擎守恒（Task 3 LEAVE），克隆空 |
  | 20 | cs:1029 | CallChoice / RedoxChoiceValidator | LEAVE | 子无条目 id/部分引擎（Task 3 LEAVE），克隆空 |
  | 21 | cs:1045 | CallChoice / EquilibriumChoiceValidator | THREAD | 传播 ← EquilibriumChoiceValidator（equilibrium-rules id） |
  | 22 | cs:1061 | CallChoice / OrganicChoiceValidator | THREAD | 传播 ← OrganicChoiceValidator（FactChoiceMatcher） |
  | 23 | cs:1077 | CallChoice / StructurePeriodicValidator | THREAD | 传播 ← StructurePeriodicValidator（FactChoiceMatcher） |
  | 24 | cs:1094 | CallChoice / ExperimentChoiceValidator | THREAD | 传播 ← ExperimentChoiceValidator（FactChoiceMatcher） |
  | 25 | cs:1111 | CallChoice / ConceptChoiceValidator | THREAD | 传播 ← ConceptChoiceValidator（FactChoiceMatcher） |
  | 26 | cs:1128 | CallChoice / MoleculeChoiceValidator | LEAVE | 子纯 RDKit 引擎（Task 3 LEAVE），克隆空 |
  | 27 | cs:1138 | CallChoice / CountingChoiceValidator | THREAD | 传播 ← CountingChoiceValidator（事实表路径 factId；子项/枚举路径空，R4） |
  | 28 | cs:1155 | CallChoice / StoichiometryChoiceValidator | LEAVE | 子纯引擎配平/摩尔（Task 3 LEAVE），克隆空 |
  | 29 | cs:1173 | CallChoice / MultiSelectChoiceValidator | THREAD | 传播 ← MultiSelectChoiceValidator（choice-facts id via JudgeWithIds） |
  | 30 | cs:1188 | CallChoice / ChoiceVerifier F1-F18 兜底 | LEAVE | 硬编码可行性规则无 JSON id（Task 3/C LEAVE） |
  | 31 | cs:1199 | CallChoice / 未唯一判定 fallback | LEAVE | Ok=null guard |
  | 32 | cs:1255 | CallCalculation / ThermochemicalHessSolver 后备 | LEAVE | 纯引擎计算（Hess） |
  | 33 | cs:1268 | CallCalculation / Batch2NarrowCalcSolver | LEAVE | 纯引擎计算（narrow calc） |
  | 34 | cs:1278 | CallCalculation / 未支持场景 fallback | LEAVE | Ok=null guard |
  | 35 | cs:1311 | TryCalculateFeBr2Cl2 / Cl2不足 | LEAVE | 纯引擎计算 |
  | 36 | cs:1330 | TryCalculateFeBr2Cl2 / 第二步氧化Br- | LEAVE | 纯引擎计算 |
  | 37 | cs:1376 | TryCalculateFeHNO3 / Fe过量 | LEAVE | 纯引擎计算 |
  | 38 | cs:1398 | TryCalculateFeHNO3 / 硝酸过量 | LEAVE | 纯引擎计算 |
  | 39 | cs:1420 | TryCalculateFeHNO3 / 双产物共存 | LEAVE | 纯引擎计算 |
  | 40 | cs:1460 | TryCalculateSimpleStoichiometry / H2+O2 O2过量 | LEAVE | 纯引擎计算 |
  | 41 | cs:1478 | TryCalculateSimpleStoichiometry / H2+O2 H2过量 | LEAVE | 纯引擎计算 |
  | 42 | cs:1507 | TryCalculateSimpleStoichiometry / C+O2 | LEAVE | 纯引擎计算 |
  | 43 | cs:1547 | TryCalculateSimpleStoichiometry / Fe+Cl2 | LEAVE | 纯引擎计算 |
  | 44 | cs:1624 | TryCalculateMassFractionMolarity | LEAVE | 纯引擎计算 |
  | 45 | cs:1675 | TryCalculateMixtureDecomposition | LEAVE | 纯引擎计算 |
  | 46 | cs:1768 | TryCalculateHessHydrate | LEAVE | 纯引擎计算（Hess） |
  | 47 | cs:1824 | TryCompareIonConcentrationByKa | LEAVE | 纯引擎计算（离子浓度比较） |
  | 48 | cs:2005 | CallEquilibrium / 未支持场景 fallback | LEAVE | Ok=null guard |
  | 49 | cs:2049 | TrySolveConservationEquation | LEAVE | 守恒式引擎（AqueousEquilibriumEngine） |
  | 50 | cs:2112 | TryOrderAcidStrength | THREAD | AcidOrderReasoningNodes.Build（acid-constants.json kaKey） |
  | 51 | cs:2189 | TryCalculateKspPrecipitation | THREAD | PrecipitateReasoningNodes.Build（precipitate-types.json id） |
  | 52 | cs:2259 | TryJudgeIonizationShift | LEAVE | 纯引擎计算（电离平衡移动） |
  | 53 | cs:2321 | TryCalculateEquilibriumConversion | LEAVE | 纯引擎计算（三段式） |
  | 54 | cs:2362 | TryCalculateCombustionEnthalpy | LEAVE | 纯引擎计算（Hess/燃烧热） |
  | 55 | cs:2392 | TryCalculateHessReverse | LEAVE | 纯引擎计算（Hess） |
  | 56 | cs:2418 | TryCalculateHessViaSolver | LEAVE | 纯引擎计算（Hess） |
  | 57 | cs:2539 | TryJudgeReactionFeasibility / 规则1 氧化性酸不生H2 | THREAD | FeasibilityRuleReasoningNodes.OxidizingAcidNoH2（redox-rules） |
  | 58 | cs:2561 | TryJudgeReactionFeasibility / 规则1 氢前生H2 | THREAD | ActivityReasoningNodes.HydrogenPosition(beforeH) |
  | 59 | cs:2579 | TryJudgeReactionFeasibility / 规则1 氢后不生H2 | THREAD | ActivityReasoningNodes.HydrogenPosition(!beforeH) |
  | 60 | cs:2606 | TryJudgeReactionFeasibility / 规则1c Pt/Au 惰性 | THREAD | ActivityReasoningNodes.InertMetalOxidizingAcid |
  | 61 | cs:2626 | TryJudgeReactionFeasibility / 规则1c 活泼金属+氧化性酸 | THREAD | ActivityReasoningNodes.ActiveMetalOxidizingAcid |
  | 62 | cs:2651 | TryJudgeReactionFeasibility / 规则1b 氢前+非氧化性酸 | THREAD | ActivityReasoningNodes.HydrogenPosition(beforeH) |
  | 63 | cs:2669 | TryJudgeReactionFeasibility / 规则1b 氢后+非氧化性酸 | THREAD | ActivityReasoningNodes.HydrogenPosition(!beforeH) |
  | 64 | cs:2694 | TryJudgeReactionFeasibility / 规则2 K/Ca/Na 先与水 | THREAD | ActivityReasoningNodes.HighlyActiveWaterFirst |
  | 65 | cs:2741 | TryJudgeReactionFeasibility / 规则2 金属置换盐溶液 | THREAD | ActivityReasoningNodes.MetalDisplacement（含 RED-001） |
  | 66 | cs:2770 | TryJudgeReactionFeasibility / 规则3 Fe 稀酸价态(+2) | THREAD | FeasibilityRuleReasoningNodes.FeDiluteAcidValence（KH-IN-06） |
  | 67 | Graph:36 | TryRouteGraph / 选择题 VerifyOptions | LEAVE | 引擎/graph 结果 |
  | 68 | Graph:54 | TryRouteGraph / 非选择题 Assign | LEAVE | 引擎/graph 结果 |
  | 69 | Curve:26 | TryRouteCurve / 曲线数值查询 | LEAVE | 引擎/curve 结果 |
  | 70 | Molecule:47 | TryRouteMolecule / 分子结构特征 | LEAVE | 引擎/molecule 结果 |
  | 71 | MultiPart:54 | TryDispatchMultiPartAsync / 子问聚合 | THREAD | MultiPartReasoningNodes.Merge（eval 未触发但代码已线程化） |

  **T5 补线程**（T4 因「无 activity 锚点」初判留空，T5 审计发现其他干净 JSON 锚点）：新建 `FeasibilityRuleReasoningNodes`（2 方法，纯函数）加性写入 `ReasoningNodes`：L2539 氧化性酸不生 H2 → `redox-rules.json` 的 `reductionProducts`（`redox-no3-dilute`→NO / `redox-h2so4-concentrated`→SO2）；L2770 Fe 稀酸只能 +2 价 → `knowledge.json` 条目 `KH-IN-06`（pitfall 明确写「Fe 与弱氧化剂（H+、Cu2+、S、I2）生成 Fe2+」，此处氧化剂正是 H+）。两锚点均由 `ActivityReasoningNodesTests` 断言真实存在于对应 JSON（R4 不臆造）；`FeasibilityRuleReasoningNodes` 构建器 3 项直接单测通过。

  **处置口径说明**：17 个 CallChoice/CallEngineAsync 包装点均运行传播代码，但其有效节点取决于子校验器——子留空时克隆空列表→有效 LEAVE（与 Task 3 一致）。THREAD 24 处锚点分布：传播自子校验器 10 处（#2/#16/#18/#21/#22/#23/#24/#25/#27/#29）+ 内联 JSON 锚点 3 处（#3 standard-state-phases / #50 acid-constants / #51 precipitate-types）+ 可行性 builder 10 处（#57–#66，ActivityReasoningNodes 8 + FeasibilityRuleReasoningNodes 2）+ 多部分聚合 1 处（#71）。LEAVE 47 处分布：Ok=null guard 9（#4–#9/#31/#34/#48）/ 纯引擎计算 20（#32/#33/#35–#47/#52–#56）/ 引擎结果 8（ion #11–#13、守恒 #49、graph #67–#68、curve #69、molecule #70）/ 符号引擎 2（#14 CallCompile、#15 CallBalance）/ 硬编码 coloredIons 1（#10）/ 子留空克隆空 6（#1/#17/#19/#20/#26/#28）/ 硬编码兜底 1（#30 ChoiceVerifier F1–F18）。

  AC6：grep 本批 `ReasoningNodes =` 无 `= null`（空列表安全，CS8601）；留空 site 无节点构造。AC7：离线 eval 验证 757 例 `gatewayConclusion` 字节级全同（见 Task 7），判决字段零漂移。

---

## Task 6: build 闸门 + 全量测试

- **Status**: completed
- **Depends On**: Task 1–5
- **Priority**: high
- **覆盖 AC**: AC8, AC9
- **Changes**:
  1. `dotnet build CogniThink.Chem.sln -c Debug` 与 `-c Release`：0 error；本批改动/新增文件 0 新增 warning（基线 13 = 7 预存 + 6 NETSDK1206，不计）。
  2. 全量 `dotnet test CogniThink.Chem.sln`：0 failed、0 新增跳过（基线 2315 passed 维持 + 本批新增）。
  3. 确认无 `_Tmp*.cs` 残留、无密钥。
- **Test Requirements**:
  - (rule) `dotnet build` 两配置 0 error；grep build 日志本批文件名 = 0 warning 命中。
  - (rule) `dotnet test` 全解决方案 0 failed。
- **Completion Evidence**: ✅ 2026-11-22：
  - `dotnet build CogniThink.Chem.sln -c Release`：**0 error、7 warning**——7 个全部为预存（AdaptiveTeachingOrchestrator×2 CS8602、CrystalCellValidator×1 CS8603、测试文件×4 CS1998/CS8604），**本批改动/新增文件 0 新增 warning**（基线 13 = 7 预存代码 + 6 NETSDK1206 环境，Release 此处未触发 NETSDK1206 还原提示）。Debug 配置由 `dotnet test` 隐式构建，0 error。
  - 全量 `dotnet test CogniThink.Chem.sln -c Debug`：**TutorBrain 1450 passed + Chem 913 passed = 2363 passed / 0 failed / 0 skipped**（基线 2315，+48 全来自本批 Task 1–5 新增单测），无回归。
  - 无 `_Tmp*.cs` 残留、本批 .cs 无密钥（AC13 grep 已覆盖）。

---

## Task 7: 离线 eval + 双 diff + 覆盖率自检

- **Status**: completed
- **Depends On**: Task 6
- **Priority**: high
- **覆盖 AC**: AC10, AC11
- **Changes**:
  1. 离线 eval：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "d:\github\CogniThink-Chem\_evalscratch\run_eval_resume.ps1" -Tag after_b_llmmain -Env Production`（`LlmAvailable=False`，757 例 0 failed）。
  2. `python _evalscratch\verify\diff_ckpt.py after_b_mpdispatch after_b_llmmain` → `GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS`（AC10）。
  3. `python _evalscratch\verify\diff_ckpt.py base_p0 after_b_llmmain` → `LOST=0 / WORSEN=0 / harm=0 → PASS`（AC10）。
  4. 写 `_evalscratch/verify/b_llmmain_coverage.py`（gitignored）：断言 ① 判决字段零翻转；② `gatewayReasoningNodesCount` 只增不减；③ 获增节点案例 ⊆ 基线 526 例 solved-but-nodeless；④ choice concept_choice 与 equilibrium 酸顺序类有节点增长（线程化生效）。输出 `ALL ASSERTIONS PASSED`（AC11）。
- **Test Requirements**:
  - (rule) 两次 `diff_ckpt` 均 PASS，`after_b_mpdispatch→after_b_llmmain` 全零。
  - (rule) 覆盖率脚本输出 `ALL ASSERTIONS PASSED`，节点只增不减、增长子集合法、有实际增长。
- **Completion Evidence**: ✅ 2026-11-22：
  - **离线 eval**：`run_eval_resume.ps1 -Tag after_b_llmmain -Env Production` → 757 例、`completed=757 failed=0`、`LlmAvailable=False`。summary：`gatewayCorrect=635 / harmCount=0 / precision=1`，与基线 `after_b_mpdispatch`（635/0）完全一致。
  - **双 diff**（`_evalscratch/verify/diff_ckpt.py`）：
    - `after_b_mpdispatch → after_b_llmmain`：`GAINED=0 / LOST=0 / WORSEN=0 / harm_old=0 / harm_new=0` → **RED-LINE PASS**（AC10：本批判决字段零翻转）。
    - `base_p0 → after_b_llmmain`：`LOST=0 / WORSEN=0 / harm=0` → **PASS**（AC10 红线锚点；`GAINED=6` 为相对 base_p0 的历史累计增益，已由 batch-local diff 证实本批贡献 0）。
  - **覆盖率自检**（`_evalscratch/verify/b_llmmain_coverage.py`）→ **`ALL B-LLMMAIN COVERAGE ASSERTIONS PASSED`**：
    - case 数 757==757、id 集全同；correct/adopted/harm 不变（635/635/0）。
    - **`gatewayConclusion` 757/757 字节级全同**（AC7 强证据，零答案漂移）。
    - `gatewayReasoningNodesCount`：**grew=411 / decreased=0 / unchanged=346**（只增不减，无回归）。
    - grew ⊆ 基线 solved-but-nodeless（`gatewayCorrect=True & count=0`，共 546 例）：**illegal=0**；grew 中 baseline correct=False 者 0、baseline count≠0 者 0（无臆造、无重复计数）。
    - 实际增长落在目标 taskType：choice 394 / equilibrium 6 / reaction_check 9 / concept_fact 2（choice 与 equilibrium 均 >0，AC11 线程化生效）。
  - **口径勘误**：基线 probe 用 `gatewayOk=true` 统计 solved-but-nodeless 得 526，漏掉 26 例「`Ok=False` 但 `Correct=True`」的否定式判决（可行性「不能」类）；本批 7 例 grew（reaction-1/2/5/6、trap-react-1/2/3）正是此类——Fe 价态 / Cu 在 H 后 / Na 先与水 / 氧化性酸不生 H2，均为已正确解出但原无节点的案例，补节点合法。覆盖率脚本改用 `gatewayCorrect`（true「已解出」语义）得 546，subset 断言 0 违规。

---

## Task 8: 台账更新

- **Status**: completed
- **Depends On**: Task 7
- **Priority**: high
- **覆盖 AC**: AC12
- **Changes**:
  1. 更新 [data-defect-register.md §N.2](../../../data-defect-register.md) L616：`LlmGatewayService` 主体 66 处（+ partial 4）行标 ✅ 已迁移（B-LlmMain 批，最后一批）→ 分级 `A·done`，记录 eval tag `after_b_llmmain`、70 site 全处置、线程化靠单测+eval 覆盖率证据；分级统计 A·done 11→**12**、混合 1→**0**、C 维持 1（RedoxRatioCalculator）；建议执行顺序 (5) 标完成（统一推理链 P2-1.x 全部收口）。
  2. 更新父 [tasks.md](../unified-reasoning-dag/tasks.md)：Status 追加 B-LlmMain 已完成、A·done=11→**12**、混合=1→**0**、执行顺序 item 5 标完成（父 spec 全批收口）。
  3. 回填本文件各 Task 的 Completion Evidence。**不提交 commit**（除非用户明确要求）。
- **Test Requirements**:
  - (rule) register / 父 tasks diff 显示 LlmGatewayService 行已迁移、A·done=12、混合=0、执行顺序 (5) 完成。
- **Completion Evidence**: ✅ 2026-11-22：台账与父 spec 同步完成：
  - `data-defect-register.md §N.2`：L616 `LlmGatewayService.cs` 行标 ✅ 已迁移（B-LlmMain 批）、分级 `混合（B 为主）`→`A·done`，真值源列记录 71 site 全处置（24 THREAD / 47 LEAVE / 0 漏线程）+ eval 证据（`after_b_mpdispatch`→`after_b_llmmain` diff 全零、411 例推理节点 0→>0、757 例 `gatewayConclusion` 字节不变）；§N.2 引言「✅ 标记的 11 个」→「12 个」并追加 B-LlmMain；分级统计 A·done 11→12、混合 1→0、C 维持 1（`RedoxRatioCalculator`）；建议执行顺序新增 (6) ✅ B-LlmMain 已完成，原「剩余：最后迁移 LlmGatewayService」改为「全部 ReasoningNodes 迁移批次完成（仅 RedoxRatioCalculator 诚实留空 C）」。
  - 父 `unified-reasoning-dag/tasks.md`：Tier 2+ Status 段追加 B-LlmMain 已完成；分级 A·done=12、混合=0；执行顺序 item 6 标完成。
  - 本文件 Task 1–3（上一会话）与 Task 4–8（本会话）Completion Evidence 全部回填。
  - **不提交 commit**（用户未明确要求）。
  - 口径注：登记原写「70 site / 主文件 66 + partial 4」，fresh-context 权威审计实证为 **71 site（主文件 66 + 分部 5：Graph 2 / Curve 1 / Molecule 1 / MultiPartDispatch 1）**，Task 5 审计表与台账均采用 71 口径。

---

## Task 9: fresh-context 独立 Review（spec mode 强制 gate）

- **Status**: completed
- **Depends On**: Task 8
- **Priority**: high
- **覆盖 AC**: 全部 AC 独立复核
- **Changes**: 委托 fresh-context **只读** 子代理独立复跑：build / 全量 test / 两次 eval diff / 覆盖率脚本 / 节点构建单测 / 代码核对（R4 全空不臆造 / 子节点字段未改 / 仅加性 / 70 site 全处置 / 事实全 JSON 无硬编码 / 无 `_Tmp*.cs` / 无密钥 / 未碰采纳门与判分）/ 台账核对。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数（R4 无臆造）/ 未改评估判分 / 契约加性不破坏消费者 / 节点只增不减且增长子集合法。子代理仅返回报告文本，由主代理落盘同目录 `review.md`。
- **Test Requirements**:
  - (rule) 独立复跑 build 0 error、test 0 failed、两次 diff 均 PASS、覆盖率脚本 PASS、节点单测 PASS。
  - (rule) 代码核对：R4 留空 site 不臆造、线程化锚点真实、`Ok`/`FinalResult`/`DerivationSteps`/`ConstraintNote`/`Confidence`/采纳门未动、无 `_Tmp*.cs`、无密钥、事实全 JSON。
- **Completion Evidence**: ✅ 2026-11-22：fresh-context 只读子代理独立复跑全部 6 个门并返回 **Verdict: PASS（整批通过，无阻塞问题）**，主代理已落盘 [review.md](./review.md)。全部红线达标：
  - BUILD Release：0 error；强制 `--no-incremental` 重建 14 条 warning 全部为预存允许项，**本批文件 0 新警告**。
  - TEST 全量：TutorBrain 1450 + Chem 913 = 2363 passed / **0 failed**；过滤测试 Activity 16 / Equilibrium 14 / Stoichiometry 5 均 0 failed。
  - EVAL DIFF：`after_b_mpdispatch`→`after_b_llmmain` 全零（GAINED/LOST/WORSEN/harm=0）RED-LINE PASS；`base_p0`→`after_b_llmmain` LOST/WORSEN/harm=0 PASS（GAINED 6 为历史累计）。
  - COVERAGE：`ALL B-LLMMAIN COVERAGE ASSERTIONS PASSED`，grew 411 / decreased 0 / `gatewayConclusion` drift 0 / 非法 grew 0。
  - CODE：5 个新增文件存在；锚点 KH-IN-06 / redox-no3-dilute / redox-h2so4-concentrated 真实、activity-series H 在 index 9；src 无 `ReasoningNodes = null`；三 builder 空输入→空列表不臆造；`git diff` 仅加 ReasoningNodes、未动判决字段；无 `_Tmp*.cs`、无密钥。
  - LEDGER：`data-defect-register.md` §N.2 LlmGatewayService 行 A·done、A=12、混合 0；父 tasks.md B-LlmMain 标 done。
  未修改/创建/删除任何源码或台账文件（仅生成本 review.md 与 `_evalscratch/` 下验证日志），未提交 commit。

---

## 执行顺序与风险控制

1. **Task 1 先扩展契约 + 核心线程化**（`Outcome` 加性字段 + FactChoiceMatcher 250 命中），build 一次尽早捕获编译/CS8601 错误。这是本批最大 eval 影响面，单测是正确性第一道闸。
2. Task 2 传播复制 + 酸顺序：确认所有包装点不丢节点、直接返回路径仍传播。
3. Task 3 自有 JSON 校验器逐个评估：无干净锚点的果断留空（R4），不为覆盖率挂假 id——这是本批最高 R4 风险点。
4. Task 4 外置事实：每组先行为等价验证再线程化；风险高的降级留空，不阻塞。
5. Task 5 留空审计 + 70 site 全处置清单：父 spec 收口证据。
6. Task 6/7/8/9 为标准闸门（build/test → eval 双 diff+覆盖率 → 台账 → Review）。
7. **R4 是最高红线**：留空 site 不得臆造、子全空父不得空造汇总——由 Task 1/3/5 单测拦截；失败回退修复，不进入下一闸。
8. **CS8601 防回归**：`ReasoningNodes` 直接赋列表（空列表即可），不得赋 null；每次编辑后 rebuild 确认 warning 回基线。
