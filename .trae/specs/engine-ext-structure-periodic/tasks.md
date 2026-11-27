# 引擎能力扩展·结构/周期律/化学键比较陈述判定（pdf-458）- Implementation Plan

> 基线：`_evalscratch/snapshot_after_dr1.json`（gatewayCorrect=701, bothWrong=56, harm=0, precision=1, LOST=[]）。
> 目标锚点：`pdf-458`（choice，answer=BD，无「双选/多选」标记 → `MultiSelectChoiceValidator` fallback）。

## Task 1: 新增领域通用真值数据 + 数据模型
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 新建 `data/highschool/structure-property-facts.json`，含两段：
    - `crystalTypes`：常见化合物 → 晶体类型（如 `GaN→原子晶体`、`SiC→原子晶体`、`SiO₂→原子晶体`、`CO₂→分子晶体`、`NaCl→离子晶体`、金刚石/石墨等），每条带 `formula` + `names`（中文别名）+ `type` + 可选 `aliases`（如「原子晶体」「共价晶体」等价）。
    - `coordinateBonds`：含配位键的常见物种（如 `NH₃BH₃`、`BF₃·NH₃`、`NH₄⁺`、`[Cu(NH₃)₄]²⁺`、`SiF₆²⁻` 等），每条带 `species` + `names` + `hasCoordinateBond` + 可选 `donor`/`acceptor`。
  - 在 `CogniThink.Chem.Core/Rules/Tier2FactData.cs` 追加 `StructurePropertyData` / `CrystalTypeEntry` / `CoordinateBondEntry` 数据模型类（与既有 `ClassificationData` 等同风格，camelCase 反序列化）。
  - 化学核对：每条均为教材通用真值，非为 pdf-458 单题而写（`GaN`/`NH₃BH₃` 是教材标准例子，可跨题复用）。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rule` TR-1.1: `RuleStore.Load().Query<StructurePropertyData>("structure-property-facts")` 不抛异常，`crystalTypes` 与 `coordinateBonds` 均非空，且各含 `GaN`/`NH₃BH₃` 条目。证据：单元测试 `StructurePropertyDataTests` 通过。
  - `rubric` TR-1.2: 数据通用性——维度「每条是否为可跨题复用的教材通用化学真值」；1=存在仅匹配 pdf-458 单题的条目；3=通用但覆盖窄；5=全部通用且化学核对无误；阈值≥4。证据：本任务完成后的自检记录（评审时复核 AC-5）。
- **Notes**: csproj 已拷贝 `data\**\*.json`，无需改 csproj；但后续 Task 8 必须 `dotnet build` 才同步到 bin。

## Task 2: ElectronConfigStatementJudge（电子排布真伪）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 新建 `ElectronConfigStatementJudge : IStatementJudge`，判定「<元素/离子> 电子排布式/简化电子排布/价层电子排布 <X>」类陈述。
  - 解析：从陈述中识别元素（符号或中文名，复用 `PeriodicTable.AtomicNumberOf`/`NameOf` 口径）+ 电荷 + 声称排布串；调用 `PeriodicTable` 对应方法（`Configuration`/`NobleGasShorthand`/`IonConfiguration`/`ValenceConfiguration`）算真值。
  - 归一化后比对（上下标→ASCII、去空白、`[Ar]` 简写对齐）：完全匹配 → `True`，真值可算但不匹配 → `False`；元素不可识别 / 声称排布不可解析 / 真值不可算 → `Undecided`。
  - 内部异常 → `Undecided`（NFR-4）。
- **Acceptance Criteria Addressed**: AC-3, AC-4（不触碰既有路径）
- **Test Requirements**:
  - `rule` TR-2.1: 正例「基态 Ga 核外电子排布式 [Ar]3d¹⁰4s²4p¹」→ `True`；错例 pdf-458-A「[Ar]4s²4p¹」→ `False`。证据：单元测试断言。
  - `rule` TR-2.2: 负例（未知元素符号、不含排布锚点、声称排布乱码、比较句「Ga 与 Ge 排布不同」）→ 全部 `Undecided`。证据：单元测试断言。
- **Notes**: 仅实现 `Judge`（不带 id，引擎推导不发节点，同 CountingStatementJudge）。

## Task 3: VseprBondAngleStatementJudge（显式键角数值真伪）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 新建 `VseprBondAngleStatementJudge : IStatementJudge`，仅判**显式数值**键角断言（如「BCl₃ 键角为 120°」）。
  - 复用 `VseprSolver` 的构型/孤对电子真值（可将其 `Table`/`LonePairs` 或新增内部映射暴露为 internal 只读），建立构型→标准键角映射（直线 180°、平面三角 120°、正四面体 109.5°/109°28′、三角锥 ~107°、V 形 ~104.5° 等），并对含孤对的微粒按孤对压缩修正（NH₃ 107°、H₂O 104.5°）。
  - 比对容差 ±2°（或含「约」字放宽）；命中 → `True`/`False`；比较型键角（含「>」「<」「比」）、未知微粒、无显式数值 → `Undecided`。
  - 异常 → `Undecided`。
- **Acceptance Criteria Addressed**: AC-3
- **Test Requirements**:
  - `rule` TR-3.1: 正例 pdf-458-D「BCl₃ 键角为 120°」→ `True`；错例「BCl₃ 键角为 109°」→ `False`；「NH₃ 键角 107°」→ `True`。证据：单元测试。
  - `rule` TR-3.2: 负例「键角 NH₃>NO₃⁻」（比较型）、「XyZ 键角 120°」（未知微粒）、「BCl₃ 构型」（无数值）→ 全部 `Undecided`。证据：单元测试。
- **Notes**: 不改 `VseprSolver` 现有 `TrySolve*` 行为；如需复用其表，仅以非破坏方式暴露只读数据。

## Task 4: CrystalTypeStatementJudge（晶体类型真伪）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `CrystalTypeStatementJudge : IStatementJudge`，判「X 属于/是/为 Y 晶体」类陈述。
  - 归一化陈述后，识别物质 X（化学式或中文名，匹配 Task 1 数据的 `formula`/`names`）与声称类型 Y（匹配 `type`/`aliases`，「原子晶体」≡「共价晶体」）。
  - 查表：X 命中且 Y 等于真值类型 → `True`；X 命中但 Y 不符 → `False`；X 未命中或陈述不含晶体类型断言 → `Undecided`。
- **Acceptance Criteria Addressed**: AC-3, AC-5
- **Test Requirements**:
  - `rule` TR-4.1: pdf-458-C「氮化镓属于分子晶体」→ `False`；「氮化镓属于原子晶体」→ `True`；「SiO₂ 是分子晶体」→ `False`。证据：单元测试。
  - `rule` TR-4.2: 负例「X₃Y 属于分子晶体」（表外）、「氮化镓熔点高」（无类型断言）→ `Undecided`。证据：单元测试。

## Task 5: CoordinateBondStatementJudge（配位键真伪）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `CoordinateBondStatementJudge : IStatementJudge`，判声称某物种含/形成配位键（含「配位键」锚点，或明确给体箭头 `A←B` / `A→B`）的陈述。
  - 归一化后识别物种（匹配 Task 1 `coordinateBonds` 的 `species`/`names`，上下标归一）。
  - 查表：物种命中 `hasCoordinateBond=true` 且陈述为肯定断言 → `True`；命中 `=false` 且肯定断言 → `False`（反之亦然）；物种未命中或无明确配位键断言 → `Undecided`。
- **Acceptance Criteria Addressed**: AC-3, AC-5
- **Test Requirements**:
  - `rule` TR-5.1: pdf-458-B「NH₃BH₃ 结构式含 N←B 配位键」→ `True`。证据：单元测试。
  - `rule` TR-5.2: 负例「CH₄ 含配位键」（表外物种）、「NH₃BH₃ 是极性分子」（无配位键断言）→ `Undecided`。证据：单元测试。

## Task 6: 接入 CompositeStatementJudge 聚合器
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 2, Task 3, Task 4, Task 5
- **Description**:
  - 在 `MultiSelectChoiceValidator` 的 `CompositeStatementJudge` 构造中，于 `FactBaseStatementJudge / CountingStatementJudge / RedoxElectronStatementJudge` 之后追加 `ElectronConfigStatementJudge / VseprBondAngleStatementJudge / CrystalTypeStatementJudge / CoordinateBondStatementJudge`。
  - 不修改 `CompositeStatementJudge` 聚合语义（任一原语定结论→记录；冲突→`Undecided`；全不命中→`Undecided`）；不改 `MultiSelectChoiceValidator.TryDecide` 调用口径。
  - 不改 `LlmGatewayService.CallChoice` 瀑布链顺序与既有校验器。
- **Acceptance Criteria Addressed**: AC-1, AC-4
- **Test Requirements**:
  - `rule` TR-6.1: `git diff` 显示仅 `CompositeStatementJudge`（或其构造处）新增 4 个原语实例，既有校验器文件无改动。证据：`git diff --stat`。
  - `rule` TR-6.2: 对 pdf-458 四选项逐选项 `Judge` 聚合结果为 A=False、B=True、C=False、D=True，且无冲突 → `MultiSelectChoiceValidator.TryDecide` 产出字母串 `BD`。证据：单元测试（构造含 pdf-458 题干+选项的 `EngineRequest`）。

## Task 7: 单元测试补齐
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 2, Task 3, Task 4, Task 5, Task 6
- **Description**:
  - 新建/补齐测试文件（镜像既有 `MultiSelectChoiceValidatorTests.cs` 风格）：
    - 各 judge 的正/负例断言（覆盖 TR-2.1/2.2、TR-3.1/3.2、TR-4.1/4.2、TR-5.1/5.2、TR-6.2）。
    - 数据加载测试（TR-1.1）。
  - 跑 `dotnet test` 全量，确保既有测试零失败（零回归）。
- **Acceptance Criteria Addressed**: AC-3
- **Test Requirements**:
  - `rule` TR-7.1: `dotnet test` 全绿，新增断言全部通过，既有测试无失败。证据：测试运行输出。

## Task 8: Build + 全量 eval + diff 红线验证
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 7
- **Description**:
  - `dotnet build`（触发 `data/**/*.json` 拷贝到 bin，0 errors）。
  - 跑 `_evalscratch/run_eval_resume.ps1 -Tag after_structperiodic -Env Production`，产出 `snapshot_after_structperiodic.json` / `checkpoint_after_structperiodic.jsonl`。
  - 与 `snapshot_after_dr1.json` 做 diff（复用 `_evalscratch/dr1_diff.py` 同方法）：核对 `LOST`、`GAINED`、`harm`、`precision`、`gatewayTruthCorrect`。
  - 确认 `pdf-458 ∈ GAINED`、结论 `BD`；若出现任何 `LOST` 或 `harm` 或 `precision<1` → 定位引入该回归的原语并回滚/收紧，重新跑，直到红线通过。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-4
- **Test Requirements**:
  - `rule` TR-8.1: 新快照 `gatewayCorrect = 702`（701+pdf-458；若有额外合法 GAIN 则 >702 但仍须 precision=1/harm=0），`pdf-458.gatewayCorrect=true` 且结论含 `BD`。证据：快照 + checkpoint。
  - `rule` TR-8.2: diff 输出 `LOST=[]`、`harm=0`、`precision=1`、`gatewayTruthCorrect≥701`；701 道已判正题 `gatewayAnswer` 逐题不变。证据：diff 脚本输出。
- **Notes**: 若额外 GAIN 出现非 pdf-458 的题，须逐题化学核对其正确性；任何存疑收益宁可收紧原语去掉，不保留。

## Task 9: 数据缺陷登记册更新
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 8
- **Description**:
  - 更新 `data-defect-register.md`：
    - 将 `pdf-458` 的 C 类登记（「选项 B 含结构式图像，文本不全」）更新为「已由引擎解出（GAIN +1）；选项 B 实为完整文本描述，无需图像轨道」，并指向本 spec。
    - 新增本批 section（如「AB 类」紧接 AA 类），说明 `pdf-196/190/207` 不在本批引擎范围的理由（引用既有 C/H/K 类登记），三者保持 `null`。
  - 不改 `pdf_questions.json` 任何字段。
- **Acceptance Criteria Addressed**: AC-6
- **Test Requirements**:
  - `rule` TR-9.1: `git diff data-defect-register.md` 含上述更新；`pdf_questions.json` 无 diff；`pdf-196/190/207` 在新快照中仍 `gatewayCorrect=false`。证据：`git diff` + 快照核对。
