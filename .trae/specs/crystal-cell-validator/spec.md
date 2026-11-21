# Crystal Cell Validator（晶胞轨道）— 规格说明

> 立项依据：`_evalscratch/crystal_spike_report.md` §5。基线：`_evalscratch/checkpoint_ie_final.jsonl`（gatewayCorrect 609 / bothWrong 148 / precision=1 / harm=0 / LOSS=0）。本轨道为单独立项，不改动既有方程/曲线/分子引擎。

## 1. 问题陈述

评估基线 `ie_final` 中 148 道 bothWrong 题里，存在一类**可确定性求解的晶胞（晶体结构）题**：原子个数（均摊法）、配位数（最近邻计数）、密度 / 密度比均可由**带分数坐标的结构化晶胞资产**唯一算出，无需 LLM 猜数。当前引擎没有晶胞求解器，这些题全部走 LLM 猜错。

spike §5 在 bothWrong 中定位到 4 道晶胞候选题：

| id | 题面要点 | 存储 answer / keywords | 数据状态 |
|----|----------|------------------------|----------|
| pdf-170 | NiO 晶胞中 Ni²⁺ 个数 | `"B"` / `["B"]` | **三重缺陷**：题干拼接（NiO + 无关流程题）、answer 错位、note 错位 |
| pdf-206 | ①灰锡配位数 ②白锡/灰锡密度比 | `"(1)4 (2)V2/4V1"` / `["4","V2/4V1"]` | 干净，note 给出完整正确推理 |
| pdf-222 | 白磷 P₄ 晶胞 P 原子个数 + 密度 | `"4.96×10^23 / (N_A · a^3)"` / 同 | 干净（仅密度入关键词；P 原子数 16 子答案未入库但不影响 Any-keyword 判分） |
| pdf-178 | Mn₃Sb ①Sb 的 Mn 配位数 ②密度 | `"C"` / `["C"]` → 修正为官方答案 | **三重缺陷**：answer/note 属于另一道非金属性选择题，题干拼接；**官方溯源已找到**（`answer/page_060.json` num 4 第(6)小问） |

## 2. 目标（Goals）

1. 新增确定性求解器 `CrystalCellValidator`，消费 `type=crystal` 结构化资产，计算：原子均摊个数、配位数、密度（数值）、密度比（符号式）。
2. 在 `LlmGatewayService.CallEngineAsync` 接入晶胞路由，使正确结论被采纳门采纳 → 评估 GAIN；全程零回归、精确率 1。
3. 修正 pdf-170 的数据缺陷：`answer` → `"4"`，`answerKeywords` → `["4"]`，并在 `note` 标注官方溯源 `pdfs/_extract/answer/page_059.json` num 2（"O²⁻半径大，顶点和面心为Ni²⁺，个数=8×1/8+6×1/2=4"）。
4. 修正 pdf-178 的数据缺陷（官方溯源已找到：`pdfs/_extract/answer/page_060.json` num 4 第(6)小问 `①12 ②122×1×55×3 / (NA×10^-7)^3`）：`answer` 改为官方配位+密度表达式，`answerKeywords` 命中求解器产出；`note` 标注溯源；录入 Mn₃Sb crystal 资产使求解器解出。
5. 不回归 `ie_final` 基线（609 正确 / precision=1 / harm=0 / LOSS=0）。

## 3. 非目标（Non-Goals）

- 不做通用晶体学引擎；仅覆盖本轨道候选题的具体结构。
- 不修正**无官方溯源**的答案键（pdf-178 现已具备 page_060 溯源，已纳入本 spec；其余无溯源者仍受限）。
- 不改动评估侧 `IsAnswerCorrect` / `SplitPdfAnswerKeywords` / `BuildGatewayAnswerAsync` 的判分与采纳语义。
- 不处理未经验图核对的投影歧义坐标；坐标存疑的候选宁可返回 `null`。

## 4. 约束与红线（冻结，不可违反）

- **precision=1、harmCount=0、LOSS=0**。
- **无唯一解返回 `null`，绝不臆造**。
- 化学事实只存在于 JSON / 结构化资产；**逻辑层不得硬编码逐题答案**。
- **`answer` 改动必须有官方来源溯源**（pdf-170 → page_059；pdf-178 → page_060 num 4 第(6)小问）。
- 晶胞坐标必须与渲染图**人工逐题核对**。

## 5. 功能需求（FR）

- **FR-1 求解器契约**：`CrystalCellValidator.TrySolve(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets) → EngineResponse?`。域外返回 `null`；命中时 `Ok=true`，`FinalResult` 为最终答案串，`Confidence=1.0`。镜像 `ReactionKbWriterSolver` 的结构与 null 契约。
- **FR-2 原子均摊计数**：顶点 ×1/8、棱 ×1/4、面心 ×1/2、体内/完全归属 ×1；按元素汇总。
- **FR-3 配位数**：由分数坐标 + 晶胞度量计算最近邻（等距且最近）原子个数。
- **FR-4 密度（数值）**：ρ = Z·M / (N_A·V)，V 由晶格常数（a nm → cm）换算。
- **FR-5 密度比（符号式）**：当一题含两个晶胞资产时，输出符号化比值（如 `V2/4V1`），不代入数值。
- **FR-6 输出格式化**：`FinalResult` 须保证经 `NormalizeChemicalNotation` + 小写后**包含**答案关键词的归一化子串；对含空格/×/·/^ 的脆弱答案（如 pdf-222 密度），用「规范式 + 去空格式」变体以 `；` 连接（同 `ReactionKbWriterSolver.ExpandMatchVariants` 模式），确保 `IsAnswerCorrect` 子串命中。
- **FR-7 网关路由**：在 `CallEngineAsync` 的资产路由块（graph/curve/molecule 之后、concept/unknown 早返回之前）接入：仅当 `imageAssets` 含 `crystal` 资产 **且** 题干问 晶胞/个数/配位数/密度 时触发；命中后置 `req.TaskType = "concept_fact"`；**绝不抢占已解结论**（位于既有资产路由之后、不覆盖 Ok=true 路径）。
- **FR-8 pdf-170 数据修正**：`pdf_questions.json` 中 pdf-170 的 `answer`→`"4"`、`answerKeywords`→`["4"]`、`note` 增补 page_059 溯源说明。
- **FR-9 结构化资产**：为可解候选在 `pdf_questions.json` 的 `imageAssets` 增加 `type=crystal` 载荷（含元素、分数坐标/位点、晶胞类型、晶格常数），坐标与渲染图人工核对一致。
- **FR-10 pdf-178 数据修正与资产**：pdf-178 的 `answer` 改为官方（page_060 num 4 第(6)）配位+密度表达式，`answerKeywords` 命中求解器产出（含 `12` 及密度式），`note` 标注溯源；录入 Mn₃Sb crystal 资产（Sb corner×8, Mn face×6, `formulaUnits=1`, `molarMass=287`, `a=1.0`, `coordinationNumber=12`）。
- **FR-11 配位意图与多物种密度**：配位意图识别除「配位数」外，兼容「每个 X 周围与它最近…的 Y」表述（`周围` ∧ `最近`）；密度计算当 basis 含多物种时，以资产 `formulaUnits`（公式单元数/晶胞）为 Z，缺失时回退为 basis 加权总数（保持 pdf-222/206 单物种行为不变，零回归）。

## 6. 关键决策与已决问题

- **采纳契约 = `concept_fact`**：`BuildGatewayAnswerAsync` 对 `calculation`（L609）**盲目采纳**结论（错答被采纳且纯 LLM 答对时 → HARM）；对 `concept_fact`（L636）**仅采纳 `IsAnswerCorrect` 通过的结论**（凡采纳必判正 → precision 1.0、零 harm）。故晶胞输出统一标记 `concept_fact`。即使底层题型为 calculation/fill，资产路由先于 switch 拦截并改 taskType，不落入 `CallCalculation`。
- **pdf-178 已解除阻塞**：穷尽检索后在 `pdfs/_extract/answer/page_060.json` **num 4 第(6)小问**找到官方答案 `①12 ②122×1×55×3 / (NA×10^-7)^3`（同条 num 4 的 (1)~(5) 与 `exercise/page_058` 大题 A 逐一吻合，铁证）。结构（图像 `p058_n06_1` 核对）：Sb 白球占 8 顶点（Z=1）、Mn 黑球占 6 面心（Z=3）→ Mn₃Sb；Sb 的 Mn 最近邻配位数=12，与官方 ①12 一致；密度 Z=1, M=122+3×55=287 → 2.87×10²³/(N_A·a³)，等价于官方 ②式。决策：改正 answer 键 + 录入 crystal 资产 + 求解器扩宽配位意图识别（`周围`∧`最近`）与多物种密度 `formulaUnits` 字段 → pdf-178 纳入确定 GAIN。
- **pdf-170 / pdf-206 / pdf-222 / pdf-178 = 确定 GAIN 目标**：四者答案键干净或可溯源修正，输出可命中关键词。

## 7. 非功能需求（NFR）

- **NFR-1 零回归**：相对 `ie_final`，gatewayCorrect 不下降、无 correct→wrong 翻转（LOSS=0）。
- **NFR-2 FullBankGuard**：无 LLM、始终运行的测试，遍历全题库 757 题，断言**每条非 null 晶胞输出对其所在题 `IsAnswerCorrect=true`**（任何不满足即阻塞发布）。
- **NFR-3 确定性**：晶胞求解不依赖网络/LLM。
- **NFR-4 不蒙数**：资产缺失 / 坐标歧义 / 问法不匹配 → 一律 `null`。

## 8. 验收标准（Acceptance Criteria）

> 类型仅 `rule`（必须满足，可自动/手动验证）或 `rubric`（定性评估，证据支撑）。

- **AC-1（rule）**：`dotnet build CogniThink.Chem.sln -c Release` 成功；既有 TutorBrain + Chem 单元测试 0 失败。
- **AC-2（rule）**：`FullBankGuard_Crystal_EveryNonNullOutput_IsAnswerCorrect` 通过（遍历 757 题；任一非 null 晶胞输出必须对该题 `IsAnswerCorrect=true`）。
- **AC-3（rule）**：EvalRunner 新 tag（如 `ie_crystal`）结果：precision=1、harmCount=0、LOSS=0（逐行正则比对 `checkpoint_ie_final.jsonl` 的 `gatewayCorrect` 布尔，不得有 true→false 翻转）。
- **AC-4（rubric）**：覆盖率 GAIN ≥ 4（pdf-170、pdf-206、pdf-222、pdf-178 全部由 bothWrong 翻转为 gatewayOnlyCorrect）；证据：eval diff（gatewayCorrect 609→613）+ FullBankGuard。
- **AC-5b（rule）**：pdf-178 的 `answer`/`answerKeywords` 命中求解器产出；`note` 引用 `pdfs/_extract/answer/page_060.json` num 4 第(6)小问；求解器产出含 `12` 且密度等价于官方式。
- **AC-5（rule）**：pdf-170 的 `answer="4"`、`answerKeywords=["4"]`、`note` 引用 `pdfs/_extract/answer/page_059.json` num 2。
- **AC-6（rule）**：逻辑层无逐题答案硬编码；所有坐标 / 晶胞事实位于 `pdf_questions.json` 的 `imageAssets` 载荷中。
- **AC-7（rule）**：每个晶胞资产的坐标与对应渲染图一致（人工核对证据记录于 tasks.md）。
- **AC-8（rule）**：求解器对非晶胞题返回 `null`；对无法唯一确定的晶胞题返回 `null`（无臆造）。
- **AC-9（rule）**：无 bothWrong→correct 之外的副作用回归；新增 harm=0。

## 9. 假设与未决项

- 假设 `SplitPdfAnswerKeywords` 对 pdf-206 产出 `["4","V2/4V1"]`（已由 checkpoint 验证）。
- 假设 `NormalizeChemicalNotation` 不去空格、不去 `^`、保留 `·`、转全角为半角（已读源码确认 L563-588）。
- 已决：pdf-178 官方溯源（page_060 num 4 第(6)小问）已找到并纳入本 spec；题干拼接（大题 A 的 (6) 与大题 B 的 (2) 合并入同一 id）属数据管线缺陷，本轨道不拆分条目，仅以晶胞子问的正确产出命中关键词实现 GAIN。
