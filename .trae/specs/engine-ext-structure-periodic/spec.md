# 引擎能力扩展·结构/周期律/化学键比较陈述判定（pdf-458）- Product Requirements Document

## Overview
- **Summary**：向 `MultiSelectChoiceValidator` 所用的 `CompositeStatementJudge` 追加 4 个通用 `IStatementJudge`（电子排布 / VSEPR 键角 / 晶体类型 / 配位键），并补一份领域通用真值数据，使结构·周期律·化学键比较类选择题（以 `pdf-458` 为锚点）在不依赖题干「双选/多选」标记、不做单题策展的前提下被引擎正确判定。
- **Purpose**：把 56 道未解中唯一一道「题面与选项文本完整、属真引擎缺口」的结构域选择题解出，同时把可泛化的结构判定能力沉淀为复用原语，为后续同域选择题解锁打基础。
- **Target Users**：网关评估（`GatewayVsLlmEvaluationService`）与下游导学求解链路——拿到正确选项字母 + 可溯源推理节点。

## Goals
- `pdf-458` 由未解（`gatewayCorrect=false`）翻转为判正，引擎最终结论含 `BD`。
- 零回归：相对 `after_dr1` 基线 `LOST=[]`、`harm=0`、`precision=1`、`gatewayTruthCorrect ≥ 701`。
- 4 个新判定原语均为**通用**能力（可复用于其它同域题），不向任何事实库追加只为匹配 `pdf-458` 单题选项的条目。

## Non-Goals
- 不解 `pdf-196`（`options=null`，数据缺选项）、`pdf-190`（answer/note 与题面张冠李戴）、`pdf-207`（开放论述填空、无唯一可输出结论）——三者均为已登记数据缺陷，不在引擎范围。
- 不恢复任何图像（不做「选项 B 含结构式图像」的图像轨道）。
- 不改 `pdf_questions.json` 的 `answer` / `answerKeywords` / `verdict` / `type` / `note`（本批纯引擎扩展，零数据键改动）。
- 不改 `LlmGatewayService.CallChoice` 单选瀑布链的顺序与既有校验器行为；新原语仅追加到 `CompositeStatementJudge`（多选 fallback 聚合器）。
- 不处理选择题里的**比较型**键角陈述（如「键角 NH₃>NO₃⁻」）——这类保持 `Undecided`，由既有路径/后续 spec 处理；本批只判**显式数值**键角断言。
- 不处理真值表之外的物种——查表未命中一律 `Undecided`（不蒙数）。

## Background & Context
- 基线快照 `_evalscratch/snapshot_after_dr1.json`：`totalCases=757`、`gatewayCorrect=701`、`bothWrong=56`、`harmCount=0`、`enginePrecision=1`、`LOST=[]`（LLM-off 确定性评估）。
- 56 道未解逐题核实结论（证据 `_evalscratch/`）：仅 `pdf-458` 是「题面+选项文本完整、无图像依赖、答案键自洽」的结构域真引擎缺口；`pdf-196/190/207` 均为已登记数据缺陷（见 `data-defect-register.md` C/H/K 类）。
- `pdf-458` 现状：choice，`answer=BD`，题干「下列说法正确的是」**不含**「双选/多选」标记 → 不走 `FactChoiceMatcher` 的多选路径，而是穿透到 `CallChoice` 瀑布链末端的 `MultiSelectChoiceValidator`（`MultiSelectChoiceValidator.cs`）。该 fallback 要求**每个选项都被 `CompositeStatementJudge` 确定判定**（任一 `Undecided` → 整题 `null`）。当前 4 个选项在 `FactBase / Counting / RedoxElectron` 三个原语下全部 `Undecided` → 整题 `null` → 未解。
- 四选项的化学真值（note 自洽 + 教材核对）：
  - A 基态 Ga 电子排布 `[Ar]4s²4p¹` → **错**（应为 `[Ar]3d¹⁰4s²4p¹`，漏写 `3d¹⁰`）。
  - B `NH₃BH₃` 结构式含 `N←B` 配位键 → **对**（氨硼烷中 N 提供孤对电子与 B 的空轨道成配位键）。
  - C 氮化镓属于分子晶体 → **错**（GaN 为原子/共价晶体）。
  - D `BCl₃` 键角 `120°` → **对**（B 为 `sp²` 杂化，平面三角形）。
- 现有可复用真值源：`PeriodicTable`（`CogniThink.Chem.Core/Structure/PeriodicTable.cs`）已能算任意元素/离子的电子排布（`Configuration` / `NobleGasShorthand` / `IonConfiguration` / `ValenceConfiguration`），`AtomicStructureSolver` 已在用，但仅服务填空、不判选择项真伪；`VseprSolver` 已有物种→构型/孤对电子对照表（`Table` / `LonePairs`），但无键角量级映射。
- 红线（用户确认，不变）：`precision=1`、`harm=0`、`LOST=[]`、**不蒙数**、不做单题策展；引擎是唯一真值源；数据修复只信官方源。

## Functional Requirements
- **FR-1（电子排布判定）**：新增 `ElectronConfigStatementJudge`，对形如「基态/离子 <元素> 电子排布式 <X>」/「简化电子排布 <X>」的陈述，从 `PeriodicTable` 计算真值排布，与陈述声称的排布归一化后比较；元素可识别、声称排布可解析、真值可算 → 返回 `True`/`False`，否则 `Undecided`。
- **FR-2（VSEPR 键角数值判定）**：新增 `VseprBondAngleStatementJudge`，对声称某微粒**显式键角数值**的陈述（如「BCl₃ 键角为 120°」），复用 `VseprSolver` 的构型/孤对电子真值映射到标准键角，与声称值在容差内比较；仅命中显式数值断言、且微粒在已知构型表内 → `True`/`False`，否则（含比较型键角、未知微粒）`Undecided`。
- **FR-3（晶体类型判定）**：新增领域通用真值数据文件（`data/highschool/` 下，含常见化合物→晶体类型映射，如 `GaN→原子晶体`、`SiC→原子晶体`、`SiO₂→原子晶体`、`CO₂→分子晶体`、`NaCl→离子晶体` 等教材通用条目）+ `CrystalTypeStatementJudge`，对「X 属于/是/为 Y 晶体」类陈述查表比对；X 不在表内 → `Undecided`。
- **FR-4（配位键判定）**：同一真值数据文件含「含配位键物种」表（如 `NH₃BH₃`、`BF₃·NH₃`、`NH₄⁺`、`[Cu(NH₃)₄]²⁺` 等教材通用条目）+ `CoordinateBondStatementJudge`，对声称某物种含/形成配位键（或明确给体→受体）的陈述查表比对；物种不在表内 → `Undecided`。
- **FR-5（聚合接入）**：上述 4 个原语追加进 `CompositeStatementJudge` 的 `_judges` 列表（位于 `FactBase / Counting / RedoxElectron` 之后，顺序不影响「冲突→Undecided」语义）；`MultiSelectChoiceValidator` 无需改调用口径。
- **FR-6（锚点题判正）**：`pdf-458` 经扩展后的 `MultiSelectChoiceValidator` 产出 `BD` 并被判正。

## Non-Functional Requirements
- **NFR-1（零假阳性构造保证）**：每个新原语在「陈述不在支持形态 / 物种未知 / 声称值不可解析 / 真值不可算」时必须返回 `Undecided`，不得返回 `True`/`False`。
- **NFR-2（零回归）**：`after_dr1` 中已判正的 701 题字节级不受影响（新原语只在多选 fallback 触发，而已解题由上游校验器命中、不到达 fallback）；全量 eval diff 必须 `LOST=[]`、`harm=0`、`precision=1`。
- **NFR-3（无单题策展）**：新增真值数据每条均为可跨题复用的化学真值；评审时核对无条目仅为匹配 `pdf-458` 单题选项文本而存在。
- **NFR-4（不拖垮链路）**：任一原语内部异常 → 捕获并返回 `Undecided`（与既有 `VseprSolver` / `AtomicStructureSolver` 的 `try/catch → null` 同口径）。

## Constraints
- **Technical**：复用 `RuleStore.Load().Query<T>(key)` 加载新 JSON（camelCase、允许注释/尾逗号）；csproj 已有 `data\**\*.json` 拷贝规则，新数据文件无需改 csproj，但改完必须 `dotnet build` 才同步到 bin。新原语实现 `IStatementJudge.Judge`（仅 `Judge`，不带 id——引擎推导子判决不发 `ReasoningNodes`，与 `CountingStatementJudge` / `RedoxElectronStatementJudge` 一致，R4）。
- **Business**：不蒙数、不迎合未核答案键；`pdf-458` 答案 `BD` 虽 `ans_page=-1` 未映射官方答案册，但 note 化学推导自洽，且本批引擎将**独立**由化学真值推出 BD（收敛证据），不修改存储键。
- **Dependencies**：依赖 `PeriodicTable`、`VseprSolver` 现有真值表（不改动其行为）；依赖 `_evalscratch/run_eval_resume.ps1 -Tag <tag> -Env Production` 产出新快照与 `after_dr1` 做 diff（复用 `_evalscratch/dr1_diff.py` 的 diff 方法）。

## Assumptions
- `pdf-458` 选项 B 的文本「`H—N←B—H`（图示为N与B间配位键…）」是完整可判文本，不依赖缺失图像（已逐字核对当前 `pdf_questions.json`）。
- 归一化口径（上下标→ASCII、破折号统一、全角→半角）沿用 `FactChoiceMatcher.NormOpt` / `ClassificationValidator.Norm`，保证新原语与既有判定一致。
- 新原语在 56 道未解中除 `pdf-458` 外若额外解出其它题，只要 `precision=1`、`harm=0` 即为合法收益；若引入 harm 立即回滚对应原语。

## Acceptance Criteria

### AC-1: pdf-458 判正
- **Type**: `rule`
- **Given**: `after_dr1` 基线（gatewayCorrect=701，pdf-458 ∈ bothWrong）。
- **When**: 实现 4 个新原语 + 真值数据并 `dotnet build`，跑 `_evalscratch/run_eval_resume.ps1 -Tag after_structperiodic -Env Production`。
- **Then**: 新快照中 `pdf-458` 的 `gatewayCorrect=true`，`gatewayAnswer`/`finalResult` 含独立字母 `BD`（经 `IsAnswerCorrect` 多选字母门控判正）。
- **Pass Condition**: `pdf-458` 判正且引擎结论为 `BD`。
- **Evidence**: 新快照 `_evalscratch/snapshot_after_structperiodic.json` 中 pdf-458 条目；`checkpoint_after_structperiodic.jsonl` 中 `gatewayConclusion`/`gatewayAnswer`。

### AC-2: 零回归红线
- **Type**: `rule`
- **Given**: `after_dr1` 与 `after_structperiodic` 两份快照。
- **When**: 对比两份快照的 `bothCorrectCases ∪ gatewayOnlyCorrectCases` 集合。
- **Then**: `LOST=[]`（无任何 `after_dr1` 判正题在新快照中翻错）、`harm=0`、`precision=1`、`gatewayTruthCorrect ≥ 701`；`pdf-458 ∈ GAINED`。
- **Pass Condition**: LOST、harm 均为空；precision 保持 1；pdf-458 为收益。
- **Evidence**: diff 脚本输出（复用 `_evalscratch/dr1_diff.py` 同方法）；新快照 `harmCount`/`enginePrecision` 字段。

### AC-3: 新原语不蒙数（负例守卫）
- **Type**: `rule`
- **Given**: 一组负例 fixture（未知物种、比较型键角、不可解析排布、表外晶体等）。
- **When**: 对每个新原语调用 `Judge`。
- **Then**: 全部负例返回 `Verdict.Undecided`，无一返回 `True`/`False`。
- **Pass Condition**: 负例集合 100% 返回 `Undecided`。
- **Evidence**: 新增单元测试（`tests/CogniThink.TutorBrain.Tests/` 下）通过，输出绿。

### AC-4: 既有链路行为不变
- **Type**: `rule`
- **Given**: `after_dr1` 的 701 道判正题。
- **When**: 新快照生成后，比对这 701 题的 `gatewayAnswer`。
- **Then**: 701 题的 `gatewayAnswer` 与 `after_dr1` 完全一致（字节级不变）；`CallChoice` 单选瀑布链源码无行为性改动（仅 `CompositeStatementJudge` 追加原语）。
- **Pass Condition**: 701 题答案逐题相同；瀑布链校验器文件无 diff。
- **Evidence**: diff 脚本输出；`git diff --stat` 显示未触碰既有校验器。

### AC-5: 真值数据无单题策展
- **Type**: `rubric`
- **Dimension**: 新增真值数据的通用性与化学正确性
- **Scale**: 1-5
- **Anchors**: 1 = 存在只为匹配 pdf-458 单题选项文本而写、无跨题复用价值的条目，或化学真值错误；3 = 条目通用但覆盖偏窄、化学正确性需逐条核；5 = 每条均为教材通用化学真值（可跨多题复用）、化学核对无误、命名/别名覆盖常见写法。
- **Pass Threshold**: >= 4
- **Evidence**: 评审对 `data/highschool/<new>.json` 的逐条核对记录。

### AC-6: 数据缺陷登记册留痕
- **Type**: `rule`
- **Given**: `data-defect-register.md` 现状（pdf-458 现登记于 C 类「选项 B 含结构式图像、文本不全」；pdf-196/190/207 已登记）。
- **When**: 本批实现完成。
- **Then**: 登记册更新：`pdf-458` 状态更新为「已由引擎解出（GAIN +1），选项 B 实为完整文本无需图像」；新增本批 section 说明 `pdf-196/190/207` 不在引擎范围的理由（引用既有登记）。`pdf-196/190/207` 保持 `null`。
- **Pass Condition**: 登记册反映最新状态，三题仍未解。
- **Evidence**: `git diff data-defect-register.md`。

## Open Questions
- [ ] 无（范围与机制已与用户确认：仅 pdf-458、混合机制）。
