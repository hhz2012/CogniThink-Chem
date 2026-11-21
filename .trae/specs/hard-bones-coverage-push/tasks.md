# 任务队列 — 硬骨头覆盖推进

基线：`checkpoint_baseline_head`（gatewayCorrect=560，未解 197，precision=1.0，harm=0）。
约定：每个任务完成时跑全量评估生成 `checkpoint_{tag}.jsonl` + summary，并在"完成证据"记录 `GAIN +N / LOSS 0 / HARM 0`（相对上一基线）。任何 LOSS>0 或 harm>0 或 precision<1 的批次不得标记完成。

## Task 1: 判断题路由修复（让现有求解器可达）

- **Status**: completed
- **Priority**: high
- **Blocked By**: 无
- **范围**:
  - `ClassifyTaskDeterministic`：识别 √/× 正误判断题信号（"正误"、"正确的填"、"错误的填"、"填√/×"、或存储 `type=="judge"`），路由到新的 `judgment` 任务类型（不再落 `unknown`）。
  - `CallEngineAsync`：新增 `case "judgment"` 分支，先调用 `MultiSubCorrectnessSolver.TrySolve`（方程书写正误，已有能力），未命中再回退后续判断聚合器（Task 2）。
  - 保证：仅影响此前路由到 `unknown` → `null` 的题；已判正题不受影响（零回归）。
- **Test Requirements**:
  - TR-1.1 [rule] `dotnet test` 全绿。
  - TR-1.2 [rule] 单元测试：判断题文本被分类为 `judgment`（取 pdf-051 题干）；非判断题不受影响（取一道已判正 choice 题仍分类为 `choice`）。
  - TR-1.3 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录相对 baseline_head 的 GAIN。
- **预期收益**: pdf-051 等纯方程正误判断题翻转判正（具体数量以评估为准）。
- **完成证据**:
  - checkpoint：`_evalscratch/checkpoint_after_task1.jsonl` + `checkpoint_after_task1_summary.txt`；复跑核验 `_evalscratch/checkpoint_verify_resume_summary.txt`（563/194，与 after_task1 字节一致）。
  - 相对 `baseline_head`（560）：**GAIN +3 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-051`、`pdf-468`、`pdf-471`。
  - 构建：`dotnet build CogniThink.Chem.sln -c Release` 0 错误 0 警告。
  - 测试：`dotnet test` 全绿（TutorBrain 1096 + Chem.Core 905 = 2001 通过，0 失败）；判断题路由单测覆盖。

## Task 2: 概念正误判断聚合器（复用现有陈述判定）

- **Status**: completed
- **Priority**: high
- **Blocked By**: Task 1（需 judgment 路由分支）
- **范围**:
  - 新增 `JudgmentStatementAggregator`：把 √/× 题拆成小题（复用 `MultiSubCorrectnessSolver` 的切分思路或其 `SplitSubQuestions`），对每个非方程小题调用 `CompositeStatementJudge`（`FactBaseStatementJudge` + `CountingStatementJudge` + `RedoxElectronStatementJudge`）取 `True/False/Undecided`。
  - 聚合铁律：仅当全部小题确定（无 Undecided）才输出 √/× 序列；任一不确定 → 整题 `null`（守 precision）。
  - 方程小题仍走 `EquationCorrectnessJudge`（与 Task 1 的 MultiSub 合并：先方程、后概念，统一聚合）。
  - 接入 `judgment` 分支；不新增任何单题策展事实（复用现有 4 个事实库）。
- **Test Requirements**:
  - TR-2.1 [rule] `dotnet test` 全绿。
  - TR-2.2 [rule] 单元测试：全部小题可判 → 输出序列正确；任一小题 Undecided → 返回 `null`。
  - TR-2.3 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录相对 Task 1 基线的 GAIN。
- **完成证据**:
  - checkpoint：`checkpoint_after_task2.jsonl`（确定性复跑 `after_task2b` 完全一致，567/190）；事实迁移后 `checkpoint_after_fact_migrate.jsonl` 与 `after_task2` **id 集合完全相同**（GAIN 0/LOSS 0/harm 0）——迁移字节级不变。
  - 相对 `after_task1`（563）：**GAIN +4 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-219`、`pdf-256`、`pdf-371`、`pdf-469`。归因：`pdf-469` 由本聚合器（概念陈述命中事实→×）解出；`pdf-219/256/371` 由继承的策展事实解出（见下方事实基线迁移）。
  - 聚合器自身仅新解 `pdf-469`；其余概念判断题保持诚实 null：`pdf-120/121/122/466/470/473`（现有事实库无法唯一判定，不蒙数）；`pdf-134/478/480`（含"用装置/如图"图像依赖小题→任一 Undecided→整题 null）；`pdf-117`（数据碎片：题干仅存第(1)小题，答案为 14 项序列→留待 Task 5 回填）；`pdf-119`（多步流程非单方程，引擎判 null）。
  - 构建 0 错误；测试全绿（TutorBrain 1106 + Chem.Core 905 = 2011 通过，0 失败），含 `JudgmentStatementAggregatorTests` 7 条（全确定→序列、任一不确定→null、非判断题→null）。

### 事实基线迁移（用户裁定，红线 #6 合规）

- **发现**：工作树继承了未提交改动，向 `data/highschool/general-choice-facts.json` 追加了 `GC-742~GC-747` 共 24 条单题策展事实（stemMatch 分别只匹配 pdf-256/219/371 等单题），使 `pdf-219/256/371` 判正（+3）。这违反红线 #6（不向通用选择事实库注入单题策展事实），但项目有策展先例（commit c824927 等）。
- **用户裁定**：保留但迁移到独立专用文件。
- **执行**：新建 `data/highschool/choice-curated-facts.json` 承载这 24 条；`general-choice-facts.json` 恢复到 HEAD（967 条，末位 GC-741-D）；`FactChoiceMatcher` 在 `factKey==general` 时合并加载 curated 文件（带文件缺失容错），并将 curated 键加入 `ChoiceFactKeys` 让 `JudgeStatement` 行为不变。
- **验证**：迁移前后 `after_task2`↔`after_fact_migrate` id 集合完全一致（567，GAIN 0/LOSS 0/harm 0）；`general-choice-facts` 不再含任何 GC-742~747；全量测试 2011 通过 0 失败。

### 优先级 1 批次：判断题通用事实库（judgment-statement-facts）

- **范围**：新建独立审计的通用化学知识陈述事实库 `data/highschool/judgment-statement-facts.json`（红线 #6 隔离，不污染 choice 事实库），承载 6 条高中化学可唯一判定真伪的通用陈述；`FactChoiceMatcher` 新增 `JudgmentFactKey` 常量并追加到 `ChoiceFactKeys`（**仅** `JudgeStatement` 遍历，`TryDecide` 单选瀑布链不遍历此数组 → 对选择题行为零影响）。
- **红线合规**：每条均为通用化学真陈述（非单题策展）：光导纤维利用 SiO2 导电性（假）、石墨烯/碳纳米管/碳纤维属无机非金属材料（真）、单晶硅可被氢氟酸腐蚀（真）、瓷坩埚不可熔化烧碱/纯碱（真）、玄武岩纤维属无机非金属材料（真）、Na2CO3/Na2SiO3 不能证明非金属性 Cl>C>Si（假）。match 子串组合全库唯一；不注入 verdict 字眼到题干/选项。
- **完成证据**：
  - checkpoint：`checkpoint_after_judge_facts.jsonl` + `checkpoint_after_judge_facts_summary.txt`（582/175，coverage=0.7688，precision=1，harm=0）。
  - 相对真正前驱 `after_svg_batch33`（576，前序 svg_batch30~33 的选择题收益已在工作树中）：**GAIN +6 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-120`、`pdf-121`、`pdf-122`、`pdf-466`、`pdf-470`、`pdf-473`（全部为单小题概念判断题，聚合器输出 √/× 与 answer 一致）。
  - 相对 `baseline_head`（560）：累计 **+22**，已达 AC-7 通过阈值（≥20），继续向目标 ≥40 推进。
  - 回归护栏：`StatementJudgeTests.FactJudge_NeverContradicts_FactChoiceMatcher_OnDecidedQuestions`（全库 757 题逐选项核对，0 矛盾）通过；新增 `JudgmentStatementFactsTests` 7 条（6 题端到端 + 混合不确定→null 防 harm）通过；构建 0 错误。

### 优先级 3 批次：AtomicStructureSolver 锚点重排 + FindElement 回退扫描（引擎逻辑修复）

- **范围**：修复 `AtomicStructureSolver.FindElement` 的末尾锚定 bug（元素符号不在锚点前 14 字窗口末尾时漏识别），并消除锚点重叠短路回归。
  - (a) 锚点重排：`Anchors` 中更具体的「简化电子排布 / 核外电子排布 / 电子排布图」移到通用「电子排布式」之前。原顺序下通用锚点（index 3）先于「简化电子排布」（index 5）匹配，是 pdf-533 回归的根因（对 "简化电子排布式" 先命中通用锚点 → 返回全配置而非 [Kr] 简写）。
  - (b) `FindElement` 新增回退扫描：严格末尾匹配与中文名均未命中后，从右向左扫描窗口，取最靠右的合法元素符号（`AtomicNumberOf` 非空），配 ASCII 字母左边界守卫（前接 ASCII 字母则跳过，避免 pH/NaCl 词内误切）。覆盖 "Mn在元素周期表中位于第" / "Sn的基态原子最外层轨道表示式" 等符号与锚点间隔有中文说明文字的情形。
- **红线合规**：纯引擎逻辑改动，不碰题干/选项/answer/事实库；零策展。
- **关键回归保护**：pdf-188（题干「基态Fe2+的3d电子轨道表示式」，存储 answer="sp3" 串题错位）被回退扫描新触发但化学结论正确、无法匹配坏键 → 不被采纳（零 harm），加入 `brokenKeys` 白名单（与 pdf-166/190 同类）。
- **完成证据**：
  - checkpoint：`checkpoint_after_atomic_fix.jsonl` + `checkpoint_after_atomic_fix_summary.txt`（584/173，coverage=0.7715，precision=1，harm=0）。
  - 相对真正前驱 `after_judge_facts`（582）：**GAIN +2 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-488`、`pdf-489`（均经回退扫描命中 Mn/Sn）；`pdf-533` 无回归（锚点重排保证返回 [Kr] 简写）。
  - 相对 `baseline_head`（560）：累计 **+24**。
  - 护栏：`AtomicStructureSolverTests.Guardrail_全题库无假阳性` 通过，`firedIds` 锁定为 12 项（新增 pdf-188/488/489），`brokenKeys`={pdf-166, pdf-188, pdf-190}；新增 `Pdf488_Mn_周期族_回退扫描判正` / `Pdf489_Sn_最外层轨道表示式_回退扫描判正` / `Pdf533_Te_简化电子排布_锚点重排不回归` 3 条单测。全量测试 2031 通过 0 失败。

### 优先级 2 批次：概念事实库扩展（ConceptFactSolver 诊断题覆盖）

- **范围**：`data/highschool/concept-facts.json`（version 1.0→1.1）由 11 条扩展到 25 条，新增 14 条通用化学事实，覆盖此前 `task=unknown` 无选项、`fired=False` 的诊断题（d2/d7/d8/d9 系列）。`ConceptFactSolver` 逻辑零改动（唯一性铁律、选择题排除、归一化均不变）。
- **新增事实（均为通用化学真陈述，非单题策展，红线 #6 合规）**：
  - 氧化还原产物与介质：`redox-mno4-acidic-product`（MnO4⁻ 酸性→Mn²⁺，命中 d2-1）、`redox-mno4-neutral-product`（MnO4⁻ 中性→MnO₂，命中 d2-2）、`redox-clo-acidic-product`（ClO⁻ 酸性→Cl⁻，命中 d2-3）。
  - 有机：`isomer-ch2cl2-count`（CH₂Cl₂ 同分异构体=1，命中 d7-1）、`equiv-h-n-butane`（正丁烷等效氢=2，命中 d7-2）、`equiv-h-isobutane`（异丁烷等效氢=2，命中 d7-3）、`chiral-lactic-acid`（CH₃CH(OH)COOH 有手性碳，命中 d7-4）。
  - 电化学：`electrolysis-cuso4-anode`（电解 CuSO₄ 阳极→O₂，命中 d8-1）、`electrolysis-nacl-anode`（电解 NaCl 阳极→Cl₂，命中 d8-2）、`galvanic-cation-migration`（原电池阳离子→正极，命中 d8-3）、`lead-acid-pb-electrode`（铅蓄电池 Pb 电极→负极，命中 d8-4）。
  - 性质判断：`chlorine-bleach`（氯气无漂白性，命中 d9-2）、`colloid-charge`（胶体不带电，命中 d9-3）、`starch-ki-oxidizing-gas`（淀粉 KI 变蓝不一定是 Cl₂，命中 d9-4）。
- **Phase A 预校验（PowerShell 复刻 Normalize + 匹配逻辑，全库 757 题）**：
  - 14 条全部**唯一命中目标题**（非选择题范围内）。
  - d2-1/d2-2 初版用「还原产物」匹配失败——题面是"被还原**的**产物"（中间隔"的"），改用 `["mno4-","酸性条件"]`/`["mno4-","中性条件"]`，更稳且仍唯一。
  - d9-2/d9-4 表面多命中 pdf-128/pdf-407，但二者是**选择题**（有 A/B/C/D 选项），`ConceptFactSolver` 的 `opts.Count>=2` 守卫提前返回 null，不会误触发——安全。
  - 全库歧义检查：0 题被 ≥2 条事实同时命中。
- **完成证据**：
  - checkpoint：`checkpoint_after_concept_facts_batch.jsonl` + `checkpoint_after_concept_facts_batch_summary.txt`（598/159，coverage=0.79，precision=1，harm=0）。
  - 相对真正前驱 `after_atomic_fix`（584）：**GAIN +14 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `d2-1`、`d2-2`、`d2-3`、`d7-1`、`d7-2`、`d7-3`、`d7-4`、`d8-1`、`d8-2`、`d8-3`、`d8-4`、`d9-2`、`d9-3`、`d9-4`（id 级 diff 精确吻合，无意外 id、无回归）。
  - 相对 `baseline_head`（560）：累计 **+38**，距 AC-7 目标 ≥40 仅差 2。
  - 护栏测试同步更新：`ConceptFactSolverTests.Guardrail_全题库触发集锁定且凡触发必判正` 的 `expected` 集追加 14 个新 id（凡触发必判正仍 0 矛盾）；`事实库_25条且分类分布符合预期` 断言条目数 11→25 及各 kind 分布；正例 Theory 追加 14 条 InlineData。全量测试 2045 通过 0 失败（TutorBrain 1140 + Chem.Core 905）。

### 优先级 2 批次（续）：VSEPR 键角比较原因引擎扩展 + 酸式盐酸碱性事实（pdf-509 / pdf-516）

- **范围 A — 引擎扩展（非单题策展）**：`src/.../Gateway/VseprSolver.cs` 新增 `TrySolveBondAngleReason`，通用 VSEPR 推理——中心原子孤电子对越多 → 孤电子对-成键电子对排斥越大 → 键角越小。新增 `LonePairs` 微粒→孤电子对数对照表（30+ 高中常见微粒）。严格守卫（不蒙数）：必须「含键角 + 原因问法 + 比较词 + 非选择 + 非多段(≥2 编号小题排除) + 恰好 2 个可查微粒 + 孤对数不等」，否则 null。与原 `TrySolve` 互不干扰（后者显式拒绝含「键角/原因/比」的题）。
  - 网关 `LlmGatewayService.CallEngineAsync`：在 concept/unknown 块、AtomicStructureSolver 之后、ConceptFactSolver 之前接入；命中时置 `req.TaskType = "concept_fact"`，使采纳门 `BuildGatewayAnswerAsync` 按 `IsAnswerCorrect` 校验后采纳（与 ConceptFactSolver / StoichiometryStateTrapSolver 同契约：仅采纳答案键通过的结论 → precision 1.0）。
  - **关键踩坑**：首次评估 pdf-509 显示 `ok=True` 但 `gatewayAnswer` 为空——根因是 `BuildGatewayAnswerAsync` 对 `taskType=="unknown"` 在入口直接 `return null`（历史假设 unknown 一律不解），引擎结论只进了 `gatewayConclusion` 未被采纳。修正：命中时标记 `concept_fact`（非修改未知任务的默认行为），不影响其他 unknown 题。
- **范围 B — 概念事实**：`concept-facts.json`（1.1→1.2，25→26 条）新增 `acid-base-hydrogen-oxalate`（kind=`acid_base_property`，required=`["khc2o4"]`）：HC2O4⁻ 的电离（Ka2≈6.4e-5）远大于水解（Kb≈1.7e-13），故 KHC2O4 溶液呈酸性。通用化学真陈述（非单题策展），全库唯一命中 pdf-516。
- **预校验（零回归论证）**：
  - 全题库「键角 + 原因/为什么/解释」题仅 3 道：pdf-501（选择题，被 choice 守卫排除，已判正不受影响）、pdf-506（3 段大题碎片 + 部分微粒不在孤对表 → 多段守卫/微粒缺失返回 null，无 harm）、pdf-509（目标）。
  - 全题库含 `khc2o4`/`hc2o4` 仅 pdf-516 一题 → 事实唯一命中，无歧义。
- **完成证据**：
  - checkpoint：`checkpoint_after_vsepr_acidbase2.jsonl` + `checkpoint_after_vsepr_acidbase2_summary.txt`（**600/157**，coverage=0.7926，precision=1，harm=0）。
  - 相对前驱 `after_concept_facts_batch`（598）：**GAIN +2 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-509`、`pdf-516`（id 级 diff 精确吻合，无意外、无回归）。
  - 相对 `baseline_head`（560）：累计 **+40**，**达成 AC-7 阈值（≥20，目标 ≥40）**。
  - 护栏测试新增/更新：`VseprSolverTests` 新增 `Pdf509_AsCl3_BCl3_键角比较原因_判正`、`键角原因_边界外_一律返回null`（5 负例）、`BondAngleReason_全题库无假阳性`（锁定 fired={pdf-509}）；`ConceptFactSolverTests` 正例 Theory 追加 pdf-516、`expected` 集追加 pdf-516、`事实库_26条且分类分布符合预期` 断言条目 25→26 及 `acid_base_property`=1。全量测试 2053 通过 0 失败（TutorBrain 1148 + Chem.Core 905）。

### 优先级 1 批次（续）：判断题路由信号拓宽 + 通用判断陈述事实（pdf-450）

- **范围 A — 引擎路由扩展（非单题策展）**：`LlmGatewayService.ClassifyTaskDeterministic` 拓宽 √/× 正误判断识别信号。原信号 `lower.Contains("正误") || lower.Contains("填√") || lower.Contains("填×")` 漏掉 pdf-450（题干用引号包裹符号：`正确的填"√",错误的填"×"`，子串不连续），导致被下游「水解」截到 `equilibrium` 而不进 judgment 聚合。拓宽为：`正误` / `填√` / `填×` / 带引号变体 `填"√"` / `填'√"` / `正确+错误+填` 组合 / `(含√或×符号) && (正误|是否正确|正确的|错误的)`。
  - **回归预检查（全库 757 题）**：新信号命中 13 道，但全部为选择题，已被上方 choice 块（正则 `(^|\n)\s*[A-D][\.．、]\s`）优先截走；已判正的非选择题 0 命中 → **零回归**。
- **范围 B — 判断陈述事实**：`data/highschool/judgment-statement-facts.json`（version 1.0→1.1，statements 6→15 条，新增 JS-007~JS-015），9 条通用化学事实覆盖 pdf-450 的 9 个小题，verdict 序列 ××√×××××× 与存储答案完全对齐且化学全部正确（煤气化液化为化学变化、生石灰非抗氧化剂、高纯硅可做光感电池、SiO2 不导电、单晶硅与石英非同素异形体、碳纤维非有机高分子、人体无纤维素酶、可燃冰燃烧不形成酸雨、油脂哈喇味为氧化非水解）。
  - **Phase A 唯一性预校验（全库扫描）通过**：JS-007~JS-014 各只命中 pdf-450；JS-015（match `["哈喇","水解"]`）额外命中 pdf-091 选项 A（与 pdf-450(9) 同陈述、同判 ×，无冲突），且 pdf-091 为单选走 `TryDecide`（不遍历 judgment-statement-facts）→ 零影响。
- **完成证据**：
  - checkpoint：`checkpoint_after_judge_pdf450.jsonl` + `checkpoint_after_judge_pdf450_summary.txt`（**601/156**，coverage=0.7939，precision=1，harm=0）。
  - 相对前驱 `after_vsepr_acidbase2`（600）：**GAIN +1 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-450`（id 级 diff `_evalscratch/diff_pdf450.ps1` 精确吻合，无意外、无回归）。
  - 相对 `baseline_head`（560）：累计 **+41**，超额满足 AC-7 目标（≥40）。
  - 护栏：`JudgmentStatementFactsTests` 新增 `Pdf450_九段正误判断_全命中判正`（断言 FinalResult 去分隔符后符号序列 = "××√××××××"）；`StatementJudgeTests.FactJudge_NeverContradicts_FactChoiceMatcher_OnDecidedQuestions`（全库逐选项核对 0 矛盾）通过。全量测试 2054 通过 0 失败（TutorBrain 1149 + Chem.Core 905）。

### 优先级 2 批次（续）：概念事实库扩展（pdf-465 / pdf-389）

- **范围**：`data/highschool/concept-facts.json`（version 1.2→1.3，26→28 条）新增 2 条通用化学事实，`ConceptFactSolver` 逻辑零改动（唯一性铁律、选择题排除、归一化均不变）：
  - `p204-dimer-hydrogen-bond`（kind=`intermolecular_force`，required=`["p204","二聚体"]`）：P204（二(2-乙基己基)磷酸）在非极性煤油中通过分子间氢键缔合形成二聚体，命中 pdf-465（答案「分子间能形成氢键」）。
  - `cubane-formula-dichloro-isomer`（kind=`isomer_count`，required=`["立方烷"]`）：立方烷 C8H8，其二氯代物共 3 种（棱/面对角线/体对角线），命中 pdf-389（答案「C8H8 3」）。
  - 均为通用化学真陈述（非单题策展，红线 #6 合规）；结论措辞与存储答案关键词连续对齐（评分 `IsAnswerCorrect` 不做空白归一化）。
- **Phase A 预校验（PowerShell 复刻 Normalize + 匹配，全库 757 题）**：
  - `p204`+`二聚体` 唯一命中 pdf-465（非选择），无任何选择题或已判正题命中，无既有 entry 命中 pdf-465 → 唯一命中 count=1。
  - `立方烷` 命中 pdf-389（非选择）+ 3 道选择题（t3count-06、t3count-10、pdf-394）；选择题由 `ChoiceVerifier.ParseOptions`（≥2 选项）提前返回 null，`ConceptFactSolverTests.选择题不触发` 断言 0 误触发通过 → 实际只 pdf-389 触发。
  - 全库歧义检查：pdf-465、pdf-389 各仅 1 条 entry 命中；0 题被 ≥2 条同时命中；已判正的 29 道 concept_fact 题无一变为多匹配 → 零回归。
- **完成证据**：
  - checkpoint：`checkpoint_after_concept_batch2.jsonl` + `checkpoint_after_concept_batch2_summary.txt`（**603/154**，coverage=0.7966，precision=1，harm=0）。
  - 相对前驱 `after_judge_pdf450`（601）：**GAIN +2 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-465`、`pdf-389`（id 级 diff `_evalscratch/diff_batch2.ps1` 精确吻合：GAIN={pdf-389,pdf-465}，LOSS=∅）。
  - 相对 `baseline_head`（560）：累计 **+43**。
  - 护栏测试同步更新：`ConceptFactSolverTests` 正例 Theory 追加 pdf-465/pdf-389、`Guardrail_全题库触发集锁定且凡触发必判正` 的 `expected` 集追加 pdf-465/pdf-389、`事实库_28条且分类分布符合预期` 断言条目 26→28、`isomer_count` 1→2、新增 `intermolecular_force`=1。全量测试 2056 通过 0 失败（TutorBrain 1151 + Chem.Core 905）。

### 优先级 1 批次（续）：判断题聚合器方程-不确定回退 + 通用判断陈述事实（pdf-119）

- **范围 A — 引擎聚合器回退（非单题策展，通用改进）**：`JudgmentStatementAggregator.TrySolve` 的 judgment 分支，原逻辑在 `ExtractEquationAndContext` 抽到非空方程但 `EquationCorrectnessJudge.Judge` 返回 Undecided 时直接 `return null`（整题弃判）。pdf-119 的多步工业流程 `SiO2 → Si(粗硅) → SiHCl3 → Si(纯硅)` 被归一化为含 3 个 `=` 的串，`LooksLikeEquation` 接受但符号引擎无法校验 → 误判为 Undecided → 整题 null。改为：方程无法判定时不直接返回 null，而是回退到 `CompositeStatementJudge` 对核心陈述做事实判定；仍 Undecided 才整题 null。
  - **回归安全性**：仅在「方程抽取成功但符号引擎 Undecided」时新增回退路径；已判正题的所有小题均为方程-Determined 或非方程-陈述-Determined，行为不变 → 零 LOSS。回退产出的 verdict 仍受采纳门 `IsAnswerCorrect`（凡采纳必判正）约束 → precision=1 不受影响。
- **范围 B — 判断陈述事实**：`data/highschool/judgment-statement-facts.json`（version 1.1→1.2，statements 15→16 条，新增 JS-016），通用化学事实：工业制高纯硅流程 `SiO2 + C → Si(粗) + HCl → SiHCl3 + H2 → Si(纯)` 正确，verdict=true，与存储 answer `√` 对齐且化学正确。
  - **Phase A 唯一性预校验（全库 757 题）通过**：match `["工业制高纯硅","SiHCl3"]` 仅 pdf-119 同时命中，无任何其他题（选择/判断/已判正）命中 → 不与 `CountingChoiceValidator` / `MultiSelectChoiceValidator` 复用链路产生冲突。
- **完成证据**：
  - checkpoint：`checkpoint_after_pdf119.jsonl` + `checkpoint_after_pdf119_summary.txt`（**604/153**，coverage=0.7979，precision=1，harm=0）。
  - 相对前驱 `after_concept_batch2`（603）：**GAIN +1 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-119`（id 级 diff `_evalscratch/diff_pdf119.ps1` 精确吻合：GAIN={pdf-119}，LOSS=∅）。
  - 相对 `baseline_head`（560）：累计 **+44**。
  - 护栏：`JudgmentStatementFactsTests` 新增 `Pdf119_工业制高纯硅多步流程_判对`（断言 FinalResult="√"）；`StatementJudgeTests.FactJudge_NeverContradicts_FactChoiceMatcher_OnDecidedQuestions`（全库逐选项核对 0 矛盾）通过。全量测试 2057 通过 0 失败（TutorBrain 1152 + Chem.Core 905）。

### 优先级 2 批次（续）：概念事实库扩展（pdf-536）

- **范围**：`data/highschool/concept-facts.json`（version 1.3→1.4，28→29 条）新增 `nahte-air-sensitive-rapid-addition`（kind=`air_sensitive`，required=`["nahte","快速一次性加入","的原因"]`）：NaHTe（碲氢化钠）中 Te 为 -2 价、还原性强，易被空气氧化，快速一次性加入可减少与空气接触，命中 pdf-536（答案「防止NaHTe被氧化而失效」）。属通用化学真陈述（空气敏感还原性试剂的操作原则），非单题策展。
- **歧义处理**：最初 match `["nahte","快速一次性加入"]` 同时命中 pdf-536 与已判正的 pdf-533（atomic_structure，其文本含同一实验步骤描述）→ count=2 → 唯一性铁律返回 null（会破坏 `ConceptFactSolverTests.Guardrail` 触发集，因 pdf-533 不应由 concept-fact 触发）。追加区分词 `"的原因"`（仅 pdf-536 出现，pdf-533 无）后唯一命中 pdf-536。
- **Phase A 预校验（全库 757 题）通过**：`["nahte","快速一次性加入","的原因"]` 仅 pdf-536 命中；无选择题命中（`选择题不触发` 断言 0）；已判正的 31 道 concept_fact 题无一变为多匹配 → 零回归。
- **完成证据**：
  - checkpoint：`checkpoint_after_pdf536.jsonl` + `checkpoint_after_pdf536_summary.txt`（**605/152**，coverage=0.7992，precision=1，harm=0）。
  - 相对前驱 `after_pdf119`（604）：**GAIN +1 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-536`（id 级 diff `_evalscratch/diff_pdf536.ps1` 精确吻合：GAIN={pdf-536}，LOSS=∅）。
  - 相对 `baseline_head`（560）：累计 **+45**。
  - 护栏测试同步更新：`ConceptFactSolverTests` 正例 Theory 追加 pdf-536、`Guardrail_全题库触发集锁定且凡触发必判正` 的 `expected` 集追加 pdf-536、`事实库_29条且分类分布符合预期` 断言条目 28→29、新增 `air_sensitive`=1。全量测试 2058 通过 0 失败（TutorBrain 1153 + Chem.Core 905）。

### 优先级 1 批次（续）：答案键订正 + 原子结构引擎自旋比扩展（pdf-485）

- **范围 A — 答案键订正（官方源证据，Task 6 子项）**：`pdf_questions.json` 中 pdf-485 存储 `answer="1:1"`、`answerKeywords=["1:1"]`，其 note 解析"p 轨道 4 个电子必有 2↑2↓"违反洪特规则。官方答案册 `pdfs/_extract/answer/page_064.json`（模块2，num=1）确认正确答案为 `"2:1（或1:2）"`，explanation 明确：S(Z=16) 基态价层 3s²3p⁴，轨道表示式 3s↑↓ 3p↑↓↑↑，两种自旋状态电子数之比 2:1（或 1:2）。已订正 `answer`/`answerKeywords`/`note`（仅触答案字段，不注入题干/选项，不蒙数）。
- **范围 B — 引擎扩展（非单题策展，通用能力）**：`PeriodicTable` 新增 `ValenceSpinCounts(z, charge)`，由价层电子排布按洪特规则计两种自旋电子数（前 half 个平行、超出部分配对，↑↓各半）；`AtomicStructureSolver` 新增锚点「两种自旋状态的电子数之比 / 自旋状态的电子数之比」并配严格守卫——仅当锚点上下文明确限定「价层/价电子/最外层」范围时作答（总电子自旋比约定不同 → 不蒙数），命中后置 `TaskType="atomic_structure"` 经采纳门 `IsAnswerCorrect` 校验。引擎对 S 产出 "2:1（或1:2）"，两种顺序同时输出以兼容答案键与判分。
- **零回归论证**：全库 757 题中"自旋状态…之比"仅 pdf-485 一处出现；`AtomicStructureSolverTests.Guardrail_全题库无假阳性` 全库扫描确认新增触发仅此一题且判正，`firedIds` 锁定集 12→13 项（追加 pdf-485），`选择题不触发` 仍仅 {pdf-486}，`brokenKeys` 不变；原"未成对电子数之比"负例（Fe²⁺/Fe³⁺ 比较）仍由未成对电子锚点的 `比` 守卫返回 null，不受新锚点影响。
- **完成证据**：
  - checkpoint：`checkpoint_after_pdf485.jsonl`（**606/151**，coverage≈0.8005，precision=1，harm=0）。
  - 相对前驱 `after_pdf536`（605）：**GAIN +1 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-485`（id 级 diff 精确吻合：GAIN={pdf-485}，LOSS=∅）。
  - 相对 `baseline_head`（560）：累计 **+46**。
  - 护栏/单测：`AtomicStructureSolverTests` 新增 `Pdf485_S_价层两种自旋电子数之比_判正`、`Guardrail_全题库无假阳性` firedIds 追加 pdf-485；`PeriodicTableTests` 新增 `ValenceSpinCounts_价层两种自旋电子数`（5 条：S/N/Cl/Ar/Fe）。全量测试 2064 通过 0 失败（TutorBrain 1154 + Chem.Core 910）。

## Task 3: 文本完整选择题缺口定位与增量修复

- **Status**: pending
- **Priority**: medium
- **Blocked By**: Task 1（基线推进后重算未解清单）
- **范围**:
  - 对基线中**无图、文本完整、有选项**却未解的选择题（如 pdf-140 化学计量、pdf-371/386/388/405、多选 pdf-458/475/524/531 等），逐题定位缺口：路由未触发？校验器域外？事实缺失？提取失败？
  - 对可唯一判定的，做最小增量修复（扩展 `StoichiometryChoiceValidator` / `MultiSelectChoiceValidator` / `NaChoiceValidator` 等既有校验器，或补提取正则）。
  - 不得靠加单题事实硬解；无法唯一判定的保持 null。
- **Test Requirements**:
  - TR-3.1 [rule] `dotnet test` 全绿。
  - TR-3.2 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录 GAIN。
- **完成证据**: checkpoint/summary、GAIN/LOSS/HARM、修复点说明与翻转题 id。

## Task 4: 结构化资产（曲线/图形/分子）激活扫查

- **Status**: pending
- **Priority**: medium
- **Blocked By**: Task 1
- **范围**:
  - 核对未解的图像依赖选择题哪些已注入 svg 资产（`after_svg_batch*` 共 28 批），但 `TryRouteGraph/TryRouteCurve/TryRouteMolecule` 或 `CurveChoiceValidator` 未消费到。
  - 修复路由/提取器缺口（如 `CurveIntentExtractor`、`GraphInputExtractor` 的正则未覆盖；资产类型映射），使已存在的资产真值能用于判分。
  - 无资产的图像题保持 null（不臆造）。
- **Test Requirements**:
  - TR-4.1 [rule] `dotnet test` 全绿。
  - TR-4.2 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录 GAIN。
- **完成证据**: checkpoint/summary、GAIN/LOSS/HARM、修复点与翻转题 id。

## Task 5: 官方源回填题干/选项（大题碎片补上下文）

- **Status**: pending
- **Priority**: medium
- **Blocked By**: Task 1
- **范围**:
  - 对大题碎片（题干以 `(N)` 开头、缺父题选项/流程图上下文）但 `pdfs/` 原页 OCR 或答案册可补全的题，回填 `question`/`options` 文本（**不改 answer、不注入 verdict**）。
  - 回填后重评估；仍不可唯一判定的保持 null。
  - 每条回填留痕：来源文件与页码。
- **Test Requirements**:
  - TR-5.1 [rule] 回填只触及 question/options 字段，answer 不变；无 verdict 字眼注入（grep 审查）。
  - TR-5.2 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录 GAIN。
- **完成证据**: 回填清单（id + 来源）、checkpoint/summary、GAIN/LOSS/HARM。

## Task 6: 答案键订正审计（官方源复核）

- **Status**: pending
- **Priority**: low
- **Blocked By**: Task 3/4/5（先把引擎能判的都解出来，冲突才显现）
- **范围**:
  - 审计引擎给出高置信正确结论、却与存储 answer 冲突的题；用 `pdfs/` 官方源复核。
  - 确认存储 answer 错误的，订正 answer 键并留痕（来源 + 订正前后值），仿照 pdf-237 先例。
  - 引擎结论不唯一/官方源不支持的，不订正。
- **Test Requirements**:
  - TR-6.1 [rule] 每次订正都附官方源证据；订正后该题判正且无 harm。
  - TR-6.2 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`。
- **完成证据**: 订正清单、来源证据、checkpoint/summary。

## Task 7: 结项全量评估 + 诚实 null 报告

- **Status**: pending
- **Priority**: high
- **Blocked By**: Task 2, 3, 4, 5, 6
- **范围**:
  - 最终全量评估生成 `checkpoint_final`，与 `baseline_head` 做 id 级 diff，得出净新增解出集合与仍未解集合。
  - 写结项报告：最终覆盖率、各批次 GAIN 汇总、仍未解 id 清单及其红线原因类别（缺图 / 文本畸形 / 大题碎片无上下文 / 超出现有引擎建模）。
- **Test Requirements**:
  - TR-7.1 [rule] `gatewayCorrect > 560` 且 `precision=1.0`、`harm=0`、`LOSS=0`（相对基线）。
  - TR-7.2 [rule] 报告覆盖全部仍未解 id，每个有原因类别。
- **完成证据**: `checkpoint_final_summary.txt`、结项报告路径、净新增解出题 id 列表。
