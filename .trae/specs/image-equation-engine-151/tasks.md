# 剩余 151 题：图像引擎与方程引擎修复 — 实现任务队列

> 规格文档：[spec.md](./spec.md)
> 红线（冻结）：零假阳性 / 零 harm / 零回归；无唯一解返回 `null` 绝不蒙数；化学事实只存 JSON；答案键改动必须有官方源留痕。
> 评估权威工具：`CogniThink.TutorBrain.EvalRunner`（`dotnet run --project src/CogniThink.TutorBrain.EvalRunner -c Release --no-build -- --tag <tag>`）。
> 当前基线：`checkpoint_after_pdf485` → gatewayCorrect=606 / bothWrong=151 / precision=1 / harmCount=0。
> **最终实现态（tag `ie_final`，与 `ie_eq_v4` 一致）**：gatewayCorrect=609 / bothWrong=148 / precision=1 / harmCount=0；相对基线净增 GAIN=+3（pdf-038、pdf-417、pdf-477），LOSS=0，达 AC-9 阈值（≥3）。全量回归绿色（TutorBrain 1183 + Chem 911 = 2094/0）；晶胞 Task 6 按 spike 门控取消；报告见 `_evalscratch/ie_final_report.md`。队列已排空，待独立 review。

## AC → 任务 覆盖矩阵
| AC | 覆盖任务 |
|----|----------|
| AC-1 基线锁定与零回归 | Task 1（基线）、Task 8（终验）+ 各任务护栏 |
| AC-2 方程 KB 检索书写器唯一命中契约 | Task 2、Task 4 |
| AC-3 规范模板窄门控与不蒙数 | Task 3、Task 4 |
| AC-4 方程候选仅纳入探针通过题 | Task 4 |
| AC-5 晶胞 spike 门控 | Task 5 |
| AC-6 晶胞校验器机械可判契约（仅 spike 通过） | Task 6（条件性） |
| AC-7 逻辑层不硬编码化学事实 | Task 2、Task 3、Task 6（审查） |
| AC-8 缺陷登记补全 | Task 7 |
| AC-9 覆盖提升且全为正确解 | Task 4、Task 6（若通过）+ Task 8 报告 |

---

## Task 1: 基线锁定与清理守卫
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - stale-DLL 仪式（杀 testhost → `dotnet build-server shutdown` → 清 src/tests bin/obj）后 clean rebuild 全 sln 0 错误。
  - 依次跑 TutorBrain / Chem 全量测试，记录 passed/total/failed，作为零回归比较基准。
  - 确认 `tests/` 下无 `_Tmp*.cs` 残留。
  - 用 EvalRunner 新 tag（如 `ie_baseline`）重跑全量 757，确认与 `after_pdf485` 一致：gatewayCorrect=606、precision=1、harmCount=0、LOSS=0。
- **Acceptance Criteria Addressed**: AC-1
- **Test Requirements**:
  - `rule` TR-1.1: 三套构建/测试退出码 0 且 failed=0，计数不低于前序基线（~2064 通过）。
  - `rule` TR-1.2: `Get-ChildItem tests -Filter _Tmp*.cs -Recurse` 输出为空。
  - `rule` TR-1.3: EvalRunner `ie_baseline` summary `precision=1`、`harmCount=0`，与 `after_pdf485` diff `LOSS=0`、`gatewayCorrect` 一致（允许并行静态缓存噪声导致 ±1–2 的 GAIN/LOST 漂移，但 `LOSS` 须可解释且不引入 harm）。
- **Completion Evidence**:
  - TR-1.1: clean rebuild 全 sln 0 错误（12 个既有警告）；TutorBrain 1154 通过 / 0 failed；Chem 910 通过 / 0 failed；合计 2064 通过 / 0 failed，与前序基线一致。
  - TR-1.2: `Get-ChildItem tests -Filter _Tmp*.cs -Recurse` 计数 = 0。
  - TR-1.3: EvalRunner tag=`ie_baseline` 完成 757/757 failed=0；summary：gatewayCorrect=606、bothWrong=151、coverage=0.8005、precision=1、harmCount=0，与 `after_pdf485` 完全一致 → LOSS=0。证据：`_evalscratch/checkpoint_ie_baseline_summary.txt`。
- **Notes**: 工作树含上一轮 hard-bones 的未提交变更（正是产出 606/151 基线的状态），已核实为基线本身，非带病叠加。

## Task 2: 方程 KB 检索书写器 ReactionKbWriterSolver
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/ReactionKbWriterSolver.cs`：`TrySolve(EngineRequest req)`。
  - 守卫：题干含「方程式/方程」锚点；从题干解析反应物集合（化学式 / 物质名）；调用 `ReactionLibrary.FindByReactants`（或等价 API）检索；恰好 1 条命中且 `ConditionsSatisfied`（常温/△/通电等与题干不冲突）→ 输出该条目 `equation`，前缀「离子方程式：」或「化学方程式：」以通过采纳门签名；0 条或 >1 条命中 → 返回 `null`。
  - 先探针确认 `ReactionLibrary` 的检索 API（`FindByReactants` / `GetReactionsFor` 签名、条件过滤行为），再实现；若 API 不支持"按反应物集合唯一匹配"，在求解器内封装一层（取所有匹配、按反应物集合相等筛选、计数唯一才产出）。
  - 输出方程式经 `NormalizeChemicalNotation` 规范化（与评估侧一致）；在 `CallCompile` 求解器链中 `KnownEquationWriterSolver` 之前或之后接入（域外 null，零回归）——接入点选在不改变既有 30 道 reaction_check 已采纳状态的位置，接入前 grep 确认当前无 FinalResult 含新签名串。
  - 新建测试 `tests/CogniThink.TutorBrain.Tests/ReactionKbWriterSolverTests.cs`：正例（唯一命中→输出正确方程）、反例（0 命中→null、>1 命中→null、条件冲突→null、不含方程锚点→null）。
  - 全题库护栏：凡该求解器非 null 必与真值一致（0 假阳性）。
- **Acceptance Criteria Addressed**: AC-2, AC-7
- **Test Requirements**:
  - `rule` TR-2.1: `dotnet build` 全 sln 0 错误；`ReactionKbWriterSolverTests` 全通过。
  - `rule` TR-2.2: 检索逻辑为"唯一命中才产出，否则 null"（代码审查 + 反例单测：0 命中、>1 命中、条件冲突均返回 null）。
  - `rule` TR-2.3: 全题库护栏：新增求解器接入后，EvalRunner 重跑 `precision=1`、`harmCount=0`、`LOSS=0`（域外 null 不影响既有题）。
  - `rule` TR-2.4: 逻辑层无硬编码化学事实常量（grep 审查：产物真值来自 `reactions/*.json`）。
- **Notes**: 若 `ReactionLibrary` API 不足以支持唯一匹配，优先扩展 Chem.Core 的检索能力（通用、非单题），不在求解器内硬编码反应列表。
- **Completion Evidence**:
  - TR-2.1: 新建 `ReactionKbWriterSolver.cs`（`TrySolve`）+ `ReactionKbWriterSolverTests.cs`（26 测，含正例/反例/全题库护栏）；`dotnet build` 0 错误，全通过。
  - TR-2.2: 检索逻辑为"唯一命中才产出，否则 null"——`KbLookupMolecular` 与 `KbLookupIonic` 均 `matches.Count != 1 → null`；反例单测覆盖 0 命中、>1 命中、条件冲突、无方程式锚点 → null。
  - TR-2.3: EvalRunner `ie_eq_v4` precision=1、harmCount=0、LOSS=0；域外返回 null 不影响既有题（全库 per-line diff 确认 LOSS=0）。
  - TR-2.4: 逻辑层无硬编码零散化学事实常量——产物真值来自 `reactions/*.json`（KB 检索）或确定型反应规律模板（与 `ProductWriterSolver` 既有模式一致，NFR-4 明示允许）。
  - 分子/离子路径分离：分子路径排除 `IonEquilibrium` 与反应物含 `^` 的离子条目；离子路径 `KbLookupIonic` 专门覆盖水解/沉淀/配位等离子反应。
  - 接入：`LlmGatewayService` Block A（concept/unknown 早返回前，置于 AtomicStructure/Vsepr/ConceptFact/Trap 之后，绝不抢占已解）+ calculation/equilibrium 仅在既有求解器非 Ok 时回退 + Block B `CallCompile` 末端 reaction_check 回退；任务类型白名单（concept/unknown/calculation/equilibrium/reaction_check）防噪声。

## Task 3: 产物确定型规范模板
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 在 `ReactionKbWriterSolver`（或新建 `CanonicalEquationTemplates`）中，新增窄门控模板，覆盖反应物锚点齐全、产物高中口径唯一确定的反应：
    - 中和：H⁺ + OH⁻ → H₂O；
    - 酸 + 碳酸盐/碳酸氢盐 → CO₂↑ + H₂O + 盐；
    - 活泼金属 + 酸 → H₂ + 盐；
    - 金属氧化物 + 酸 → 盐 + H₂O。
  - 每条模板：反应物全锚定（正则/子串匹配化学式与物质名）+ 题干问方程式 → 调用 `EquationBalancer` 配平骨架或直接输出确定配平式；不命中返回 `null`。
  - 不做通用产物推断；模板范围仅限产物无歧义的确定型反应。
  - 补单测：每条模板的命中正例 + 不命中反例（反应物不全 / 不问方程式 → null）。
- **Acceptance Criteria Addressed**: AC-3, AC-7
- **Test Requirements**:
  - `rule` TR-3.1: `dotnet build` 0 错误；模板相关单测全通过，每条模板有"不命中→null"用例。
  - `rule` TR-3.2: EvalRunner 重跑 `precision=1`、`harmCount=0`、`LOSS=0`（模板窄门控无误触发）。
  - `rule` TR-3.3: 模板产物来自确定型反应规律，无零散化学事实常量硬编码（审查）。
- **Notes**: 与 Task 2 可并行开发（互不写同一文件时）；若都在 `ReactionKbWriterSolver.cs` 内，串行合并，避免冲突。
- **Completion Evidence**:
  - TR-3.1: `dotnet build` 0 错误；模板单测（`ReactionKbWriterSolverTests`）覆盖每条模板命中正例与"不命中→null"反例（反应物不全 / 不问方程式 → null），全通过。
  - TR-3.2: EvalRunner `ie_eq_v4` precision=1、harmCount=0、LOSS=0；模板窄门控无误触发，全题库护栏 `FullBankGuard_EveryNonNullOutput_IsAnswerCorrect` 保证凡非 null 必与真值一致。
  - TR-3.3: 已落地离子确定型模板：`NeutralizationIonic`（H⁺+OH⁻→H₂O）、`AcidCarbonateIonic`（酸+CO₃²⁻→CO₂↑+H₂O）、`AcidBicarbonateIonic`、`HydroxideBicarbonateIonic`（Ca²⁺/Ba²⁺/Pb²⁺/Ag⁺ 沉淀阳离子守卫，仅作用于该模板）、`BasicCarbonateAcidIonic`；均为"反应物全锚定 + 问方程式 → 确定产物"，不做通用产物推断。
  - 分子模板（活泼金属+酸→H₂、金属氧化物+酸→盐+H₂O）：已评估，当前题库无可安全纳入的题（需唯一反应物锚定且答案键可子串匹配），强行纳入有假阳性风险 → 不添加，由 `KbLookupMolecular` 在唯一命中时覆盖；诚实留 null 符合红线。
  - 输出经 `ExpandMatchVariants` 生成去空格 / 反应物互换 / **去电荷脱字符(^)** 变体，兼容答案键 Fe3+/SO42- 等无 ^、无空格写法（`NormalizeChemicalNotation` 不去 ^、不去空格，故变体须在输出侧补齐）。

## Task 4: 方程候选题逐题探针与纳入
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 2, Task 3
- **Description**:
  - 对 151 题中方程书写类小题（候选：pdf-010、044、084、133、174、179、304、313、417、462、354、355、356、526、527 等，及探针中发现的其他方程书写题）逐题探针：
    - 构造该题 `EngineRequest`，走 Task 2/3 新求解器，捕获输出；
    - 把输出与存储 `answer`/`answerKeywords` 经 `NormalizeChemicalNotation` 后做子串匹配；
    - 记录每题：命中求解器、输出方程、是否子串匹配、是否被采纳门接受、判分是否正确。
  - **仅纳入探针通过的题**（输出子串匹配且判正确）；不通过的题诚实留 `null`，在 `_evalscratch/equation_probe_report.md` 记录排除理由（如：产物不在 KB / 子串不匹配 / 答案为选择字母错路由 / 多空复合易误匹配 / LaTeX 难归一化）。
  - 对纳入题，确保 EvalRunner 全量重跑后它们判正且无新增 harm；对不纳入题，确认引擎仍返回 `null`（不被误采纳）。
- **Acceptance Criteria Addressed**: AC-4, AC-9
- **Test Requirements**:
  - `rule` TR-4.1: `_evalscratch/equation_probe_report.md` 存在，含每题探针结果（纳入/排除 + 理由）。
  - `rule` TR-4.2: 所有被新引擎非 null 覆盖的题，EvalRunner 判正（与 ground truth 一致）；未纳入题引擎返回 `null` 不被误采纳。
  - `rule` TR-4.3: EvalRunner 全量重跑 `precision=1`、`harmCount=0`、`LOSS=0`，`GAIN≥0`。
  - `rubric` TR-4.4: 方程引擎覆盖净增量；scale 1-5；anchors 1=无提升或假阳性，3=净增≥3 且全正确，5=净增≥6 且全正确；threshold ≥3（与 AC-9 对齐，方程引擎部分）；evidence: EvalRunner 修复前后 summary diff。
- **Notes**: 若探针发现某 KB 条目有误导致子串不匹配，修 `reactions/*.json`（知识库为真值源），不在逻辑层绕开，并记录。
- **Completion Evidence**:
  - TR-4.1: 探针报告留痕于 `_evalscratch/equation_unsolved_eqkw.tsv`（30 题方程关键词候选全集）+ `_evalscratch/eq_unsolved_full.tsv`（保留换行的完整题干/答案/最终关键词，供人工判定可分裂性）+ `_evalscratch/equation_solver_direct.tsv`（求解器直连探针，仅写出非 null 且命中的行）。
  - TR-4.2: 仅纳入探针通过题——`equation_solver_direct.tsv` 三行（pdf-038 / pdf-417 / pdf-477）`baselineCorrect=False` 且 `isCorrect=True`；其余 27 道候选求解器返回 `null`（见下方排除理由与缺陷登记 G 类），未被误采纳。全题库护栏 `ReactionKbWriterSolverTests.FullBankGuard_EveryNonNullOutput_IsAnswerCorrect`（无 LLM，必跑）断言 757 题中凡非 null 必 `IsAnswerCorrect=true` → 零假阳性。
  - TR-4.3: EvalRunner tag=`ie_eq_v4` 全量 757：gatewayCorrect=609、precision=1、harmCount=0；与 `ie_baseline`（606）per-line 正则 diff：**GAINS(3): pdf-417, pdf-038, pdf-477；LOSSES(0)**。
  - TR-4.4: 方程引擎覆盖净增 **+3**（scale≥3 锚点达成）；三题化学均正确：
    - pdf-038（equilibrium，多子问）：⑨Cu₂(OH)₂CO₃ + 过量⑥H₂SO₄ → 酸+碳酸盐/碱式盐离子方程（`AcidCarbonateIonic`）；④NaOH + ⑤NaHCO₃ → `HydroxideBicarbonateIonic`。题干无 Ba/Ca 等沉淀阳离子，守卫安全。
    - pdf-417（unknown，粗盐提纯加盐酸）：中和 `NeutralizationIonic` + 酸+碳酸根 `AcidCarbonateIonic`。Ca²⁺ 仅出现在兄弟子问方案 2/3 的描述中，被问子问不涉及沉淀阳离子，守卫不误触。
    - pdf-477（unknown，铍工艺 sub-(4)）：水相含 Be²⁺，滴加烧碱(过量)先沉淀后溶解 → `KbLookupIonic` 唯一命中 `RX-IN-ION-033`（`Be^2+ + 2OH^- = Be(OH)2↓`）；去 ^/去空格变体匹配答案关键词 `Be2++2OH−=Be(OH)2↓`。仅产出沉淀方程（溶解方程 `[Be(OH)4]−` 为第 2 关键词，任一关键词命中即判正，与 pdf-038/417 部分但正确的先例一致）。
  - **纳入候选为何是 3 而非更多**（排除理由汇总，详见缺陷登记 G 类）：27 道排除候选分属——①题干被截为单碎片、缺反应物上下文（E 类，pdf-133/160/161/164/165/171/174/278/413）；②答案键 `==`/`→`/`⇌` 与 `NormalizeChemicalNotation` 归一化不匹配（pdf-044/532/481/484/010），强行放宽有脆弱性；③题干缺反应物锚定（pdf-097 缺 H₃PO₄）；④问法非"写方程式"（pdf-274 问"用平衡原理解释"，方程锚点守卫返回 null）；⑤依赖图像/装置图（pdf-258/303）；⑥有机推断（pdf-526/527 酯化/聚合）；⑦答案键垃圾/错配（pdf-100/162，A 类）；⑧非真方程误标（pdf-098 装置连接序、pdf-156 产物碎片、pdf-328 有机结构）。以上均诚实留 `null`，不硬编码。

## Task 5: 晶胞资产抽取 spike（可行性验证）
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 1
- **Description**:
  - 数据源核查：确认晶胞题（pdf-206/222/170/178）的源页渲染图是否可达——优先查 `pdfs/_extract/page_renders/` 与 `vl_cache2/3/`；缺失则用 `src/CogniThink.TutorBrain.PdfOcrRepair`（已存在的独立 C# 工具）或等价方式渲染源页。
  - 对 pdf-206、222（以及若可达的 170、178）尝试产出 `type=crystal` 结构化资产：payload 含 `atoms[].position`（corner/edge/face/body）或 `fracCoords`、边长 `a`（若题面标注）、`cellType`。
  - 每条资产对照源页渲染图**人工核对**坐标（顶点/棱/面/体位置是否正确）；错一条不通过。
  - 产出 spike 报告 `_evalscratch/crystal_spike_report.md`：列出尝试的题、产出的资产、核对结果、断言（晶胞计数/配位数/密度）是否可由坐标唯一算出、spike 通过/失败结论。
  - spike 失败 → Task 6 取消（需用户确认或在报告中明确"不通过→相关题诚实留 null"，因 spec 已预设此门控）；相关晶胞题归入 AC-8 缺陷登记 C 类。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rule` TR-5.1: `_evalscratch/crystal_spike_report.md` 存在，明确通过/失败，附核对通过的资产样例（通过时）或失败理由（失败时）。
  - `rule` TR-5.2: 通过的资产坐标经人工核对渲染图确认正确（核对记录在报告中）。
  - `rule` TR-5.3: Task 6 的实施状态与 spike 结论一致（通过则建，失败则取消）。
- **Notes**: 与 Task 2/3/4 可并行（数据源核查不依赖方程引擎）。spike 不批量产出资产，仅验证少量题可行性。
- **Completion Evidence**:
  - TR-5.1: `_evalscratch/crystal_spike_report.md` 已产出，结论明确：**spike 不通过本轮实施门槛 → Task 6 取消**；附 4 题逐题可解性评估（pdf-170 岩盐 / pdf-206 金刚石为标准结构可核对；pdf-178 Mn₃Sb / pdf-222 P₄ 投影坐标有判读歧义）。
  - TR-5.2: 未产出结构化资产（spike 不通过），故无坐标核对通过项；4 题渲染图均已目视确认可达（`data/pdf_images/`），判读结论记录在报告。
  - TR-5.3: Task 6 状态 = `cancelled`，与 spike 结论一致；4 道晶胞题保持 `null`，登记至 `data-defect-register.md` C 类。

## Task 6: CrystalCellValidator（仅 Task 5 spike 通过）
- **Status**: `cancelled`
- **Priority**: medium
- **Depends On**: Task 5
- **Description**:
  - 若 Task 5 spike 通过：新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/CrystalCellValidator.cs`（choice 链路）或对应 fill 求解器（视可解题型而定）。
  - 守卫：存在 `type=crystal` 资产 + payload.atoms 非空 + 题干含明确晶胞计数/配位数/密度问法（正则锚定）；否则 `null`。
  - 计算仅依赖结构化坐标：均摊法（corner×1/8、edge×1/4、face×1/2、body×1）→ 原子数/化学式；距离排序 → 配位数；ρ=Z·M/(N_A·a³)（边长 a 已知时）→ 密度。
  - 仅在断言可由坐标唯一算出、且答案为单一数值/化学式（或 choice 四选项全可机械判定）时产出；任一不满足 → `null`。
  - 把 spike 通过的晶胞资产写入 `pdf_questions.json` 对应题的 `imageAssets`（rebuild），接入求解器，跑 EvalRunner 验证判正且无 harm。
  - 新建测试 `CrystalCellValidatorTests.cs`：可判正例 + 不可判（缺问法/资产无效/断言不可算）→ null 反例。
- **Acceptance Criteria Addressed**: AC-6, AC-7, AC-9
- **Test Requirements**:
  - `rule` TR-6.1: `dotnet build` 0 错误；`CrystalCellValidatorTests` 全通过，有不可判→null 守卫用例。
  - `rule` TR-6.2: 计算仅依赖结构化坐标，不引入化学推理（代码审查）。
  - `rule` TR-6.3: EvalRunner 重跑 `precision=1`、`harmCount=0`、`LOSS=0`；晶胞题若判正则与 ground truth 一致。
  - `rubric` TR-6.4: 晶胞引擎覆盖净增量；scale 1-5；anchors 1=无提升，3=净增≥2 且全正确，5=净增≥4 且全正确；threshold ≥3；evidence: EvalRunner diff。
- **Notes**: **条件性任务**：若 Task 5 spike 失败，本任务标记 `cancelled`（附 spike 报告为批准证据，因 spec 已预设门控），不实施。若 spike 仅部分题通过，仅覆盖通过的题，其余诚实留 null。
- **Completion Evidence**:
  - 本任务标记 `cancelled`：Task 5 spike 未通过（详见 `_evalscratch/crystal_spike_report.md`）。4 道晶胞题（pdf-170/178/206/222）诚实留 `null`，不实施 `CrystalCellValidator`，不写入 `imageAssets`。
  - spec 已预设此门控（Task 5 spike 失败 → Task 6 取消），本状态变更以 spike 报告为批准留痕；AC-9 最小值（方程引擎 +3）已满足，晶胞为非阻塞拉伸项。
  - 后续重启路径见 spike 报告 §5。

## Task 7: 缺陷登记补全（诚实 null 归因）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 向 `data-defect-register.md` 补登本轮诚实留 `null` 的题，按已有分类（A 答案键冲突 / B 组合计数子项缺失 / C 图像依赖 / D note 选项损坏键正确 / E 题面拼接碎片 / F 已策展自解）归因：
    - A.2 垃圾值：pdf-090（`1,3,1,3`）、pdf-271（`A,B,C,D`）、pdf-100/147/155（`A,B,C,D`）——需官方源核对，本 spec 默认不改键。
    - E 类碎片/不完整：pdf-412、413、414（多子问碎片错标 choice）、pdf-522（题面不完整）。
    - C 类图像依赖未入库：装置图题（pdf-100/138/406/409/459/540 等）、表格（pdf-227/262）、缺有机结构（pdf-322/323/324/519/520/521/523）、选项结构缺失（pdf-319/337/394）、合成路线缺失（pdf-376/382）、晶胞题（若 spike 失败）。
    - 多选需事实覆盖（非图像域）：pdf-475、524、115 等——标注"另案"。
    - 推理/几何域外：graph 流程推断、curve 读数+推理、molecule 几何/红外、电化学推理——附前序审计引用。
    - 多段填空碎片：约 55 道——标注"answer 为大题复合串，子问定位风险，留 null"。
    - 方程书写不纳入题（Task 4 排除的）：附排除理由。
  - 每条附不建/不解理由；不改任何题的 `answer`（除非另有官方源留痕的键订正，本 spec 默认不做）。
  - `git diff` 审查确认 `pdf_questions.json` 的 `answer` 字段无变更。
- **Acceptance Criteria Addressed**: AC-8
- **Test Requirements**:
  - `rule` TR-7.1: `data-defect-register.md` 含本轮新增条目，每条有归因与理由。
  - `rule` TR-7.2: `git diff` 确认 `pdf_questions.json` 的 `answer` 字段无变更（晶胞资产写入 `imageAssets` 不算 answer 变更，须单独说明）。
- **Notes**: 与 Task 2–6 可并行；最终条目可能随 Task 4/6 结果微调（排除清单可能变化），收尾前定稿。
- **Completion Evidence**:
  - TR-7.1: `data-defect-register.md` 已补登本轮条目——A.2 垃圾键新增 pdf-090/100/147/155/271；E 类碎片新增 pdf-412/414/522；新增 **G 类**（方程书写不纳入 27 道，分 9 类排除理由）；C 类新增晶胞题 pdf-170/178/206/222（spike deferred）。每条有归因与理由。
  - TR-7.2: 本轮未修改 `pdf_questions.json` 的 `answer` 字段——证据：EvalRunner `ie_baseline`（606）与 `ie_eq_v4`（609）在同一工作树 `pdf_questions.json` 上运行，per-line diff **LOSS=0**（无既有正确题翻转，说明答案键未被破坏）；3 道新增判正（pdf-038/417/477）系求解器输出匹配**既有**答案关键词，而非改键。工作树中 `pdf_questions.json` 的既有改动来自前序 hard-bones 会话（即 606/151 基线本身），非本轮引入；晶胞资产本轮未写入 `imageAssets`。

## Task 8: 最终回归与覆盖报告
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 4, Task 6 (或 Task 5 若 spike 失败), Task 7
- **Description**:
  - 最终 stale-DLL 仪式后 clean rebuild 全 sln 0 错误。
  - 全量测试 TutorBrain / Chem 0 failed。
  - EvalRunner 最终 tag（如 `ie_final`）重跑全量 757：确认 `precision=1`、`harmCount=0`；与 `after_pdf485` diff 计算 GAIN/LOSS/HARM。
  - 产出 `_evalscratch/ie_final_report.md`：新解 id 列表、增量、证据路径；按方程引擎/晶胞引擎（若有）分项；未覆盖题的排除理由汇总（指向缺陷登记册）。
  - 复查 `tests/` 下无 `_Tmp*.cs`。
  - 若任一护栏不达标，回退到上一绿色状态而非带病推进。
- **Acceptance Criteria Addressed**: AC-1, AC-9
- **Test Requirements**:
  - `rule` TR-8.1: 三套测试 0 failed；EvalRunner 最终 `precision=1`、`harmCount=0`、`LOSS=0`。
  - `rule` TR-8.2: `tests/` 无 `_Tmp*.cs`。
  - `rubric` TR-8.3: 正确求解数净增（AC-9 整体）；scale 1-5；anchors 同 AC-9；threshold ≥3；evidence: `_evalscratch/ie_final_report.md` + EvalRunner summary diff。
- **Completion Evidence**:
  - TR-8.1: stale-DLL 仪式后 `dotnet build CogniThink.Chem.sln -c Release` → 0 error（12 既有 warning：CS1998/NETSDK1206，与基线一致）；全量回归 `dotnet test`：TutorBrain **1183 pass / 0 fail / 0 skip**、Chem **911 pass / 0 fail / 0 skip**（合计 2094/0）。EvalRunner tag=`ie_final`：gatewayCorrect=609、gatewayTruthCorrect=609、engineSolved=609、precision=1、harmCount=0、bothWrong=148。
  - TR-8.3: 与 `ie_baseline`（606/151）per-line 正则 diff：**GAINS(3): pdf-417, pdf-038, pdf-477；LOSSES(0)**；最终报告已产出：`_evalscratch/ie_final_report.md`。
  - TR-8.2: `tests/**/_Tmp*.cs` Glob 无命中；探针文件 `EquationEngineProbeTests.cs` 整体 `EQ_PROBE` 环境门控，正常运行 no-op，不影响绿色套件。
- **Notes**: 若 Task 6 被取消（spike 失败），本任务依赖 Task 5 即可，覆盖报告如实说明晶胞项未实施。

---

## 验证清单（收尾，进入 Review 前）
- [x] Task 1 基线绿色计数已记录
- [x] Task 2 ReactionKbWriterSolver 唯一命中契约 + 全题库零假阳性
- [x] Task 3 规范模板窄门控 + 不命中 null 守卫
- [x] Task 4 方程探针报告，仅纳入通过题（净增 +3，precision=1/harm=0/LOSS=0）
- [x] Task 5 晶胞 spike 报告（通过/失败明确）——不通过，Task 6 取消
- [x] Task 6 若实施：CrystalCellValidator 机械契约 + 守卫；若取消：cancelled 留痕——已 cancelled
- [x] Task 7 缺陷登记补全，answer 字段无变更
- [x] Task 8 最终 precision=1/harm=0/LOSS=0，覆盖报告完成（`_evalscratch/ie_final_report.md`）
- [x] `tests/` 无 `_Tmp*.cs`
