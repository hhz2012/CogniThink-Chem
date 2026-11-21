# Crystal Cell Validator — 任务清单

> 依赖顺序自上而下；每个任务标注所映射的 AC 与本地 Test Requirement（`rule` 必须通过 / `rubric` 需证据）。完成证据记录在各任务的 Completion Evidence 块。

## T1 — 基线锁定复核（实施前快照）
- **映射 AC**：AC-3、AC-9（前置）
- **内容**：重读 `_evalscratch/checkpoint_ie_final.jsonl` + `_evalscratch/_summary.txt`，确认基线 gatewayCorrect=609、bothWrong=148、precision=1、harm=0、LOSS=0；确认 pdf-170/206/178/222 当前均在 bothWrong 且 gatewayUsedEngine=false。
- **Test Requirement（rule）**：四题当前 `gatewayCorrect=false`、`gatewayTruthAdopted=false` 的证据贴入本任务；基线数字记录在案。
- **依赖**：无。

## T2 — 修正 pdf-170 答案键（官方溯源）
- **映射 AC**：AC-5
- **内容**：编辑 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` 中 pdf-170：
  - `answer` → `"4"`
  - `answerKeywords` → `["4"]`
  - `note` 增补：`【官方溯源】pdfs/_extract/answer/page_059.json num 2：O²⁻半径大，顶点和面心为Ni²⁺，个数=8×1/8+6×1/2=4。原 answer="B" 属抽取错位，已修正。`
- **Test Requirement（rule）**：`Select-String` 校验 pdf-170 条目 `answer` 为 `"4"`、`answerKeywords` 含 `"4"`、`note` 含 `page_059`。
- **依赖**：T1。

## T3 — 实现 CrystalCellValidator 核心
- **映射 AC**：AC-1、AC-6、AC-8、FR-1~FR-6
- **内容**：新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/CrystalCellValidator.cs`（静态类，镜像 `ReactionKbWriterSolver`）：
  - `TrySolve(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets) → EngineResponse?`
  - 解析 `type=crystal` 资产的 `Payload`（元素、分数坐标/位点 `corner|edge|face|body|internal`、`cellType`、晶格常数 `a`/`c`）。
  - 原子均摊（FR-2）、配位数最近邻（FR-3）、密度数值（FR-4）、双胞密度比符号式（FR-5）。
  - 输出变体格式化（FR-6）：脆弱答案用 `；` 连接「规范式 + 去空格」变体。
  - 域外/歧义 → `null`（FR-1 / NFR-4）。
- **Test Requirement（rule）**：`tests/CogniThink.TutorBrain.Tests/CrystalCellValidatorTests.cs` 单元测试覆盖：NiO 均摊=4、灰锡 CN=4、白锡 Z=2、密度比符号式=V2/4V1、P₄ 密度表达式命中关键词归一化子串、非晶胞资产返回 null、缺坐标返回 null。
- **依赖**：T2（数据先行，便于用真题验证）。

## T4 — 录入结构化晶胞资产（坐标人工核对）
- **映射 AC**：AC-6、AC-7、FR-9
- **内容**：在 `pdf_questions.json` 为以下题目 `imageAssets` 追加 `type=crystal` 载荷（`Source` 指向渲染图），坐标逐一与渲染图核对：
  - pdf-170：NiO 岩盐（Ni²⁺ 顶点+面心）—— 核对 `pdf_images/p057_n04_r0.png`。
  - pdf-206：灰锡金刚石立方（CN=4, Z=8）+ 白锡体心四方（Z=2）—— 核对 `pdf_images/p073_n07_2/3.png`。
  - pdf-222：白磷 P₄ 分子晶体（P₄ 分子位于顶点+面心，Z=4 分子）—— 核对 `pdf_images/p078_n07_4.png`。
  - pdf-178：Mn₃Sb（Sb 顶点、Mn 面心）资产**仅作运行时展示**，不计入评估 GAIN（见 T6 守卫）。
- **Test Requirement（rule）**：`pdf_questions.json` 可被题库加载无异常；每条资产坐标与渲染图一致性的人工核对记录（截图/坐标对照表）贴入 Completion Evidence。
- **依赖**：T3（先有解析器，再录入资产并联调）。

## T5 — 网关路由接入
- **映射 AC**：AC-1、AC-9、FR-7
- **内容**：编辑 `src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs` `CallEngineAsync`（L328）：在 `TryRouteMolecule`（L390）之后、concept/unknown 块（L396）之前，新增：
  ```
  var crystal = CrystalCellValidator.TrySolve(req, imageAssets);
  if (crystal is not null) { req.TaskType = "concept_fact"; return Task.FromResult(crystal); }
  ```
  - 守卫：仅 `imageAssets` 含 `crystal` **且** 题干命中 晶胞/个数/配位数/密度 意图才触发（由求解器内部判定，域外 null）。
  - 不抢占既有 Ok=true 路径（位于 graph/curve/molecule 之后）。
- **Test Requirement（rule）**：既有 `LlmGatewayService` 相关单元测试 0 失败；探针验证 pdf-170/206/222 经 `CallEngineAsync` 产出 `Ok=true` 且 `TaskType=concept_fact`；非晶胞题行为不变。
- **依赖**：T4。

## T6 — FullBankGuard 与聚焦测试
- **映射 AC**：AC-2、AC-8、NFR-2
- **内容**：在 `tests/CogniThink.TutorBrain.Tests/` 新增 `FullBankGuard_Crystal_EveryNonNullOutput_IsAnswerCorrect`（镜像 `ReactionKbWriterSolverTests` 的 `BuildTestCases()` 遍历 757）：对每题调用 `CrystalCellValidator.TrySolve`，若返回非 null，则断言 `GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, case) == true`。pdf-178 必须命中且 `IsAnswerCorrect=true`（官方溯源 page_060 已在 T10 找到）—— 若不满足则阻塞。
- **Test Requirement（rule）**：该测试无 LLM、始终运行、全绿；任何非 null 输出不满足判正即失败。
- **依赖**：T5。

## T7 — 构建与全量回归 + EvalRunner 验证
- **映射 AC**：AC-1、AC-3、AC-4、AC-9、NFR-1
- **内容**：
  1. `dotnet build-server shutdown` → 清理 bin/obj → `dotnet build CogniThink.Chem.sln -c Release`（防 stale DLL）。
  2. 跑 TutorBrain + Chem 测试：0 失败。
  3. EvalRunner 新 tag：`dotnet run --project src/CogniThink.TutorBrain.EvalRunner -c Release --no-build -- --tag ie_crystal --concurrency 24 --batch-size 100`。
  4. 逐行正则比对 `checkpoint_ie_final.jsonl` 与新 checkpoint 的 `gatewayCorrect` 布尔：不得有 true→false（LOSS=0）；统计新增正确（GAIN）。
- **Test Requirement（rule）**：precision=1、harm=0、LOSS=0；GAIN 证据（pdf-170/206/222 由 bothWrong 转为 GatewayOnlyCorrect）贴入；评估摘要写入 `_evalscratch/`。
- **依赖**：T6。

## T8 — 缺陷登记更新
- **映射 AC**：AC-5、AC-6（文档侧）
- **内容**：在既有缺陷登记（定位仓库内缺陷登记文件）记录：pdf-170 答案键已修正（溯源 page_059）；pdf-178 官方溯源已找到（page_060 num 4 第(6)小问），已在 T10 解除阻塞并解出；新增晶胞资产清单。
- **Test Requirement（rule）**：登记条目存在且与本 spec §6 决策一致。
- **依赖**：T7。

## T9 — 独立 Review 关卡
- **映射 AC**：全部（最终把关）
- **内容**：fresh-context 只读独立审查（写入 `review.md`）：核对零回归、precision=1、无逻辑硬编码、坐标核对证据、pdf-170 溯源、pdf-178 解除阻塞（溯源 page_060）与扩宽 CN 识别/formulaUnits 的零回归证据。不通过则回到对应任务修复，直到 pass。
- **Test Requirement（rubric + rule）**：`review.md` 结论为 pass；无未关闭的重大问题。
- **依赖**：T8。

## T10 — 解除 pdf-178 阻塞（改键 + 资产 + 求解器增强）
- **映射 AC**：AC-4、AC-5b、AC-6、AC-7、FR-10、FR-11
- **内容**：
  1. `CrystalCellValidator.cs`：① 扩宽配位意图 `asksCN = text.Contains("配位数") || (text.Contains("周围") && text.Contains("最近"))`（兼容"每个 Sb 周围与它最近且相等距离的 Mn"，仅对有 crystal 资产的题生效，零回归）；② `Cell` 增加可选 `FormulaUnits`，`DensityAnswer` 优先 `cell.FormulaUnits ?? Z(cell)`（多物种 Mn₃Sb：Z=4 总原子 → formulaUnits=1 公式单元；pdf-222/206 无此字段回退 Z(cell)，行为不变）。
  2. `pdf_questions.json` pdf-178：`answer`→`"(1)12 (2)122×1×55×3 / (NA×10^-7)^3"`，`answerKeywords`→`["12","2.87×10^23 / (N_A · a^3)","122×1×55×3 / (NA×10^-7)^3"]`，`note` 标注 page_060 溯源；追加 crystal 资产（Sb corner×8, Mn face×6, formulaUnits=1, molarMass=287, a=1.0, coordinationNumber=12）。
- **Test Requirement（rule）**：`CrystalCellValidatorTests` 新增 Mn₃Sb 配位=12（"周围+最近"意图）、密度产出 `2.87×10^23 / (N_A · a^3)`（formulaUnits=1）、扩宽意图不改变 pdf-170/206/222 产出；FullBankGuard pdf-178 命中且 IsAnswerCorrect=true。
- **依赖**：T9。

## T11 — 构建回归 + EvalRunner 再验证（GAIN +1）
- **映射 AC**：AC-1、AC-3、AC-4、AC-9、NFR-1
- **内容**：`dotnet build-server shutdown` → rebuild Release → 全量测试 0 失败 → EvalRunner `--tag ie_crystal178` → 逐 id 正则 diff vs `ie_final`：LOSS=0、GAIN=4（pdf-170/206/222/178）、harm=0、precision=1。
- **Test Requirement（rule）**：gatewayCorrect 609→613；pdf-178 由 false→true（GE=true/TA=true）；其余 756 行 gatewayCorrect 不变。
- **依赖**：T10。

## T12 — 独立复审（pdf-178 增量）
- **映射 AC**：全部（增量最终把关）
- **内容**：fresh-context 只读独立审查，追加到 `review.md`：核对 pdf-178 官方溯源 page_060、扩宽 CN 识别零回归、formulaUnits 零回归、密度等价性、LOSS=0/GAIN=4/harm=0。
- **Test Requirement（rubric + rule）**：`review.md` 结论 PASS；无未关闭重大问题。
- **依赖**：T11。

## 完成证据汇总（实施后逐任务填写）
| 任务 | 状态 | 证据 |
|------|------|------|
| T1 | ✅ | `_evalscratch/checkpoint_ie_final_summary.txt`：total=757、gatewayCorrect=609、bothWrong=148、precision=1、harmCount=0、llmAvailable=False、pureLlmCorrect=0。逐行确认 pdf-170/206/178/222 均 `gatewayCorrect=false, gatewayUsedEngine=false`。 |
| T2 | ✅ | `pdf_questions.json` pdf-170（L5030-5055）：`answer="4"`、`answerKeywords=["4"]`、`note` 含「官方溯源 page_059.json num 2」。Grep 校验通过。 |
| T3 | ✅ | 新建 `src/.../Gateway/CrystalCellValidator.cs`（静态类，镜像 `ReactionKbWriterSolver`，null 契约）；`tests/.../CrystalCellValidatorTests.cs` 9 测试全绿：NiO=4、灰锡 CN=4 + V2/4V1、P4 密度经真实 `IsAnswerCorrect` 判正、非晶胞/无意图/choice/缺 CN 事实/缺摩尔质量/异质量比 6 条 null 守卫。 |
| T4 | ✅ | `pdf_questions.json` 录入 4 个 `type=crystal` 资产：pdf-170 NiO（Ni corner×8+face×6）、pdf-206 白锡（corner×8+body×1, Z=2）+ 灰锡（corner×8+face×6+internal×4, Z=8, CN=4）、pdf-222 P4（corner×8+face×6, atomsPerFU=4, a=1.0）。坐标逐一与渲染图核对（`pdf_images/p057_n04_r0.png`、`p073_n07_2/3.png`、`p078_n07_4.png`，记录于 `data-defect-register.md` C 类）。pdf-178 **未录入**（阻塞）。FullBankGuard case-count>700 守卫通过 → JSON 合法。 |
| T5 | ✅ | `LlmGatewayService.CallEngineAsync` L394-402：`TryRouteMolecule` 之后接入 `CrystalCellValidator.TrySolve`，命中置 `req.TaskType="concept_fact"` 并返回；不抢占既有 Ok=true 路径；域外返回 null 不影响下游 switch。 |
| T6 | ✅ | `CrystalCellFullBankGuardTests`：`BuildTestCases()` 遍历 757，断言每条非 null 晶胞输出 `IsAnswerCorrect=true`；断言 pdf-170/206/222 命中、pdf-178 返回 null；Theory 校验 4 个关键词片段。全量 `dotnet test CogniThink.Chem.sln -c Release`：**2109 通过 / 0 失败**（TutorBrain 1198 + Chem 911）。 |
| T7 | ✅ | EvalRunner `--tag ie_crystal`（LlmAvailable=False）：`checkpoint_ie_crystal_summary.txt` gatewayCorrect 609→**612**、bothWrong 148→**145**、precision=1、harm=0。逐 id 正则 diff `ie_final` vs `ie_crystal`：**LOSS (true→false)=0**、**GAIN (false→true)=3**（pdf-170/206/222，均 GE=true/TA=true）、harm=0；pdf-178 不变（GC=false）。checkpoint 结论直证：170→"4"、206→含"V2/4V1"、222→"16；4.96×10^23 / (N_A · a^3)；…"。 |
| T8 | ✅ | `data-defect-register.md`：A.1 表新增 pdf-170 键订正行（`B`→`4`，page_059 溯源）并更新「改键同时解出」注（pdf-485 + pdf-170）；C 类 pdf-170/206/222 标「✅ 已解」、pdf-178 标「⏸ 仍阻塞」（无官方溯源、不录入资产、0 GAIN/0 harm）。 |
| T9 | ✅ | fresh-context 只读独立审查 `.trae/specs/crystal-cell-validator/review.md` 结论 **PASS**：独立实跑 build 0/0、2109 测试全绿；正则 diff LOSS=0/GAIN=3；抽样复核 page_059.json 溯源；grep 证逻辑层 0 硬编码；4 张渲染图目检与 payload 一致；AC-1..AC-9 与全部红线通过，无未关闭阻断问题。 |
| T10 | ✅ | **代码** `CrystalCellValidator.cs`：①L39 扩宽 `asksCN = text.Contains("配位数") \|\| (text.Contains("周围") && text.Contains("最近"))`（上游 `HasCrystalAsset` 守卫，零跨轨道回归）；②L22 `Cell` 增加可选 `FormulaUnits`，L94 `ParseCell` 读 `formulaUnits`，L214 `DensityAnswer` 优先 `cell.FormulaUnits ?? Z(cell)`（多物种 Mn₃Sb 用 1；pdf-222/206 无字段回退 `Z(cell)`，行为不变）。**数据** `pdf_questions.json` pdf-178（L5234-5266）：`answer`→`"(1)12 (2)122×1×55×3 / (NA×10^-7)^3"`、`answerKeywords`→`["12","2.87×10^23 / (N_A · a^3)","122×1×55×3 / (NA×10^-7)^3"]`、`note` 标注 page_060 num 4 第(6)溯源；追加 crystal 资产（Sb corner×8 M122、Mn face×6 M55、formulaUnits=1、molarMass=287、a=1.0、coordinationNumber=12）。**测试** `CrystalCellValidatorTests` 新增 3 例（Mn₃Sb 配位=12 经"周围+最近"意图、密度 `2.87×10^23 / (N_A · a^3)` 用 formulaUnits、NiO 扩宽意图零回归输出仍为"4"）；`CrystalCellFullBankGuardTests` 增加 `Assert.Contains("pdf-178", fired)`、FirmTargets theory 加 pdf-178 两条、`Pdf178_Unlocked_OfficialTrace_FiresAndIsCorrect` 替换原阻塞用例。 |
| T11 | ✅ | `dotnet build-server shutdown` → rebuild Release **0 警告 / 0 错误**。全量 `dotnet test -c Release --no-build`：**2122 通过 / 0 失败 / 0 跳过**（TutorBrain 1211 + Chem 911），主机无崩溃；`--filter FullyQualifiedName~CrystalCell` **20/20** 全绿。EvalRunner `--tag ie_crystal178`（LlmAvailable=False）757/757 完成、0 失败；`checkpoint_ie_crystal178_summary.txt`：gatewayCorrect 609→**613**、bothWrong 148→**144**、harmCount=0、precision=1、engineSolved=613、coverage=0.8098。逐 id 正则 diff `ie_final` vs `ie_crystal178`：**GAIN (false→true)=4**={pdf-170, pdf-178, pdf-206, pdf-222}、**LOSS (true→false)=0**、unchanged=753。pdf-178 直证：`gatewayConclusion`=`12；2.87×10^23 / (N_A · a^3)；2.87×10^23/(N_A·a^3)`，`gatewayCorrect=true`、`gatewayOk=true`、含"12"与"2.87"。 |
| T12 | ✅ | fresh-context 只读独立复审已在 `.trae/specs/crystal-cell-validator/review.md` 末尾追加「## Review #2 — pdf-178 增量复审」（L60-111，Review #1 原内容完整保留），结论 **PASS**。独立复核 12 项全通过：page_060 num4 第(6) 溯源 + exercise/page_058 (1)~(5) 交叉铁证、pdf-178 数据/渲染图目检、asksCN 扩宽零回归（L31 先返回 null 守卫）、formulaUnits 回退零回归（pdf-206/222 无字段回退 Z(cell)）、逻辑层 grep 0 硬编码、密度等价推导、两份 summary（609→613 / 148→144 / harm0 / precision1）、自行正则逐行 diff（GAIN=4={pdf-170,178,206,222}、LOSS=0、unchanged=753）、pdf-178 行直证含"12"且 GC=true。无 blocker/major；披露 2 项非缺陷（page_060 上游 OCR artifact、L38 注释表述略不精确但功能正确）。增量全部红线与 AC-4/AC-5b/AC-6/AC-8/AC-9 闭环。 |
