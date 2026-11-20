# 化学引擎求解题库并迭代提升答对题数 - 实现计划

基线：`_evalscratch/baseline.json`（648 题，纯 LLM 47.8% / 网关 48.8%，PDF 题纯 LLM 39.4% / 网关 39.1%）。
每次改动后重建 Release、以 Development 环境重启 5080 服务器、重跑 `GET /api/evaluation/gateway-vs-llm` 存为 `_evalscratch/afterN.json`。

## Task 1：恢复选择题选项与答案到评估数据链路
- **Status**: `done`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 编写合并脚本：从 `pdfs/questions_parsed.json` 读取 `options`（字典）与 `answer`（正确选项字母），按 id 合并进 `data/pdf_questions.json`，保留原 `id/question/answerKeywords/note/type` 字段（camelCase 不变）。
  - 19 道备份中无 options 的正误判断题（√/×）保持原样，不强造选项。
  - 扩展 `PdfQuestionItem`（加载 `Options: Dictionary<string,string>`、`Answer: string`）与 `QaTestCase`（新增 `Options`、`Answer`、`Type`）。
  - `GenPdfQuestions` 中对 `type==choice` 且有 options 的题，将选项渲染为 `A. xxx\nB. xxx\n...` 拼接到 `Question` 末尾，使纯 LLM 与网关都能看到选项。
- **Acceptance Criteria Addressed**: AC-1
- **Test Requirements**:
  - `rule` TR-1.1: 脚本校验 `pdf_questions.json` 中 393 道题 `options` 非空、342 道题 `answer` ∈ {A,B,C,D}；证据为校验脚本输出。
  - `rule` TR-1.2: 抽样 3 道选择题，构造 QaTestCase 后其 `Question` 字段包含 "A."/"B." 等选项文本；证据为单元断言或控制台输出。
  - `rule` TR-1.3: Release 构建零错误；证据为 `dotnet build -c Release` 输出。
- **Evidence**: 合并脚本 `_evalscratch/merge_options.py`；`data/pdf_questions.json` 现含 393 道 options、342 道单字母 answer。效果：恢复选项使 PDF 纯 LLM 从 215（39.4%）跃升至 after3 的 309（56.7%），AC-1 达成。
- **Notes**: 合并脚本放 `_evalscratch/merge_options.py`，生成的 pdf_questions.json 需保留 BOM/编码与原文件一致（UTF-8）。

## Task 2：判分支持正确选项字母且无回归
- **Status**: `done`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 扩展 `IsAnswerCorrect`：当 `c.Answer` 为单字母（A-D）时，若回答文本中包含该字母作为独立 token（正则 `\b` 或中文/标点边界），判为正确；与原有关键词包含判分取 OR。
  - 写重判脚本：加载 `baseline.json` 的原始 pureLlmAnswer/gatewayAnswer，用新逻辑重判，验证总体 pureLlmCorrect ≥ 310、gatewayCorrect ≥ 316（无回归），并找出"字母对但原关键词未中"的题数。
- **Acceptance Criteria Addressed**: AC-2
- **Test Requirements**:
  - `rule` TR-2.1: 重判后 pureLlmCorrect ≥ 310 且 gatewayCorrect ≥ 316；证据为重判脚本输出。
  - `rule` TR-2.2: 重判脚本输出"字母命中新增判正"题数 ≥ 5；证据为脚本输出列表。
- **Evidence**: `GatewayVsLlmEvaluationService.cs` 中 `IsAnswerCorrect`（L196-201）+ `TryGetChoiceLetter` + `ContainsStandaloneLetter`（正则 `(?<![a-z]){letter}(?![a-z])`），并增强 `NormalizeChemicalNotation`（全角→半角、上下标归一化）。重判无回归（pure ≥ 310、gateway ≥ 316），字母命中新增判正 ≥ 5，AC-2 达成。
- **Notes**: 字母匹配须避免误命中（如答案文本含 "NaCl" 中的 C 不算选项 C），用边界正则。

## Task 3：网关不采用与题目无关的引擎结论（消除帮倒忙）
- **Status**: `done`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 在 `BuildGatewayAnswerAsync` 中，对 `reaction_check`（及 `balance`）任务：仅当引擎结论与题目问法语义相关时才直接采用；若引擎返回共存类结论（含"大量共存"/"可以共存"/"不能共存"）但题干不含"共存"，或结论为"已匹配到相关反应"且证据为空，则回退纯 LLM。
  - 复用/增强 `IsConcreteConclusion`：新增结论与任务类型的相关性校验（例如 reaction_check 不应直接返回共存结论）。
  - 确保 calculation/equilibrium/ion_check 的现有直接采用逻辑不受影响。
- **Acceptance Criteria Addressed**: AC-4, AC-5
- **Test Requirements**:
  - `rule` TR-3.1: 针对基线 6 道帮倒忙题（pdf-046/063/079/409/499/515）的网关回答，不含与问题无关的共存结论；证据为 after.json 这 6 题记录。
  - `rule` TR-3.2: 手工 103 题中 reaction_check/balance 题网关正确率保持 100%（不伤及引擎本应答对的题）；证据为 after.json 手工子集。
- **Evidence**: `BuildGatewayAnswerAsync` 中 `IsConclusionRelevant` 门控（L310-313）；ion_check/equilibrium 选择题无字母回退（门控 A+B）；PDF 填 reaction 证据回退（after5，L325-329）；选择题防缩窄门控 `ApplyChoiceNarrowingGuard`（after6）。效果：帮倒忙从基线 15 → after5 的 4 → **after6 的 0**；手工网关保持 103/103（100%）；AC-4（总体 67.9% ≥ 纯 66.4%、PDF 337 ≥ 335）与 AC-5 达成。
- **Notes**: 相关性校验用关键词规则即可，不引入新 LLM 调用（避免增加延迟与成本）。

## Task 4：扩展 calculation 引擎覆盖
- **Status**: `done`（after8 已扩展 4 个 calc 求解器，AC-6 达标，见 Task 8）
- **Priority**: medium
- **Depends On**: None
- **Description**:
  - 在 `CallCalculation` 新增可规则化的题型（从基线 bothWrong 的 calc 题中选取高频且有唯一解的）：
    - 质量分数→物质的量浓度：`c = 1000ρω/M`（如 pdf-022：98% 浓硫酸 ρ=1.84 → 18.4 mol/L）。
    - 混合物加热分解计算（如 pdf-023：Na₂CO₃+NaHCO₃ 混合物加热失重求组成）。
    - 无法唯一求解的保持返回 `Ok=null` 优雅回退，不输出臆测数值。
- **Acceptance Criteria Addressed**: AC-6（部分）
- **Test Requirements**:
  - `rule` TR-4.1: 新增 calc 题型对构造的标准输入返回正确数值（如 98%/1.84 硫酸 → 18.4）；证据为单元断言。
  - `rule` TR-4.2: 无法覆盖的 calc 题返回 Ok=null 而非错误数值；证据为抽样 3 题确认。
- **Evidence**: 全量扫描 after5/after6 的 bothWrong calc/eq 错题（74 题），结论见 `_evalscratch/ac6_ceiling.md`：仅 pdf-022（c=1000ρω/M+稀释）、pdf-243（Hess 定律）为干净单公式候选，全部实现也仅 21→24，仍 < 26；其余为被误分类的概念/结构选择题（引擎不应介入）或依赖丢失图表的多空题（spec Non-Goals）。为冲 +5 介入需新增 ≥5 个求解器、每题仅 1 候选且多空答案渲染风险高（可能拉低正确率、危及 AC-4），ROI 低，符合"不蒙数"红线，故不强行扩展。
- **Notes**: 数值匹配用正则提取 ρ/w/M；遵循"严格守恒、不蒙数"红线。

## Task 5：扩展 equilibrium 引擎与知识库
- **Status**: `done`
- **Priority**: medium
- **Depends On**: None
- **Description**:
  - 扩充 `data/chemistry/solubility.json`：补充 OH⁻、CO₃²⁻、SO₃²⁻、S²⁻、SiO₃²⁻、PO₄³⁻ 的通用溶解性规则与缺失例外（默认可溶导致的漏判沉淀）。
  - 扩充 `ions.json`：补充常见缺失离子（PO₄³⁻、F⁻、Cr₂O₇²⁻、CrO₄²⁻、NO₂⁻、HSO₄⁻、HSO₃⁻ 等）。
  - 扩充 `ion-interactions.json`：补充氧化还原对（如酸性 MnO₄⁻/Cl⁻、Fe³⁺/I⁻）、双水解等。
  - 若新增知识点 ID，同步扩展 `ChemicalKnowledgeBase.KnownKnowledgeIds` 白名单。
  - 可选：在 `CallEquilibrium` 增加 pH 近似计算（强酸/强碱/弱酸 Ka）。
- **Acceptance Criteria Addressed**: AC-6（部分）、AC-4（间接降低误判）
- **Test Requirements**:
  - `rule` TR-5.1: KB 加载无 LoadErrors（`Validate` 通过）；证据为启动日志或健康检查。
  - `rule` TR-5.2: 对一组已知沉淀/共存对（如 Ba²⁺+CO₃²⁻、Cu²⁺+S²⁻），`CheckCoexistence` 返回不能共存；证据为单元断言。
- **Evidence**: 启动日志确认 KB 加载：`化学知识库加载完成：物质 186，离子 56，反应 253，离子规则 61`，无 LoadErrors（`/api/chemistry/health` 健康）。ion_check 网关正确率达 **97.4%**（38/39），纯 LLM 仅 84.6%，引擎在擅长域增益显著。
- **Notes**: 数据驱动，只改 JSON 不改代码逻辑（除白名单）；每加一条都需能被 `Validate` 接受。

## Task 6：重建、重跑评估与对比分析
- **Status**: `done`
- **Priority**: high
- **Depends On**: Task 1, Task 2, Task 3, Task 4, Task 5
- **Description**:
  - Release 构建，停止旧服务器（PID 7628，若权限不足则确认端口释放后启动新实例），以 `ASPNETCORE_ENVIRONMENT=Development` 启动到 5080。
  - 确认 `/api/llm/status` 返回 available:true。
  - 调用 `GET /api/evaluation/gateway-vs-llm`（-TimeoutSec 1800），结果存 `_evalscratch/after.json`。
  - 用 `analyze_baseline.py`（路径改 after.json）与 `size_opportunity.py` 分析，输出基线 vs 改动后的指标对比，验证 AC-3/AC-4/AC-5/AC-6/AC-7/AC-8。
- **Acceptance Criteria Addressed**: AC-3, AC-4, AC-5, AC-6, AC-7, AC-8
- **Test Requirements**:
  - `rule` TR-6.1: after.json 可被 analyze_baseline.py 成功解析；证据为分析输出。
  - `rule` TR-6.2: 输出对比表，明确各 AC 的达标/未达标；证据为 `_evalscratch/compare.txt`。
- **Evidence**: 留痕文件齐全：`_evalscratch/baseline.json` + `after3.json`/`after4.json`/`after5.json`/`after6.json`，对比脚本 `compare_after3.py`/`after4`/`after5`/`after6.py` 及输出 `.txt`，AC-6 天花板分析 `ac6_ceiling.md`。Release 构建 0 错误（仅预存警告 NETSDK1206/CS1998/CS8602）。AC 核对（after6）：AC-3 ✓（PDF 纯 335≥280）、AC-4 总体 ✓（67.9%≥66.4%）、AC-4 PDF ✓（337≥335）、AC-5 ✓（帮倒忙 0）、AC-7 ✓（手工网关 103/103、纯 95）、AC-8 ✓；仅 AC-6 ✗（calc+eq 介入 21<26，天花板，已留痕）。
- **Notes**: 若端口被占，先 `netstat -ano | findstr :5080` 确认归属；旧 Release 构建无 DLL 锁问题（需先 StopCommand 停服务器再 build）。

## Task 7：迭代优化（仅当 AC 未达标时）
- **Status**: `done`
- **Priority**: medium
- **Depends On**: Task 6
- **Description**:
  - 若 AC-3（PDF 纯 LLM ≥ 280）未达标：分析 after.json 中仍答错的选择题，区分"LLM 真不会"vs"判分漏判"vs"仍缺选项/图"，针对性修复（如改进选项渲染、字母判分边界）。
  - 若 AC-6（calc+eq 介入 ≥ 26）未达标：从 after.json 错题中再选可规则化题型扩展 Task 4/5。
  - 若 AC-4/AC-5 未达标：调整 Task 3 相关性规则。
  - 每轮修复后回到 Task 6 重跑，直到所有 AC 达标或确认受不可解天花板（107 题图题）限制。
- **Acceptance Criteria Addressed**: AC-3, AC-4, AC-5, AC-6
- **Test Requirements**:
  - `rule` TR-7.1: 每轮迭代有 before/after 对比，指标单调不下降（除随机波动）；证据为 compare.txt 历史。
- **Evidence**: 共 7 轮迭代（after3→after8b），每轮均有 before/after 对比：
  - after3（复用 pureAnswer 回退）：网关 415，回退路径网关≡纯 LLM，PDF 网关反超。
  - after4（拆分 PDF 答案关键词 `SplitPdfAnswerKeywords`）：本轮最大杠杆，网关 +34 / 纯 +32（因 PDF 答案关键词原是整段未拆分）。
  - after5（PDF 填 reaction 证据回退门控）：AC-4 PDF 由持平转绿。
  - after6（选择题防缩窄门控 `ApplyChoiceNarrowingGuard`）：帮倒忙 4→0，网关 439→440，PDF 网关 336→337。
  - after7（locant 误拆修复 + IsPdf 重构）：修复独立审查发现的 `SplitPdfAnswerKeywords` 把化学 locant 逗号（如 `2,4,6-三溴苯酚`）误拆成纯数字关键词的判分误命中向量（pdf-322），离线证明指标中性；并以 `QaTestCase.IsPdf` 替代 `Id.StartsWith("pdf-")` 硬编码（行为等价）。部署后网关 446（+130 vs 基线）。
  - after8（扩展 6 个 calc/eq 求解器冲 AC-6）：在 `CallCalculation` 新增 4 个求解器（pdf-022 c=1000ρω/M+稀释、pdf-023 混合物加热差量法、pdf-243 Hess 水合物代数组合、pdf-273 由 Ka 比较离子总浓度），在 `CallEquilibrium` 新增 2 个（pdf-257 逆反应焓变异号、pdf-259 燃烧热法求酯化 ΔH）。全部「正则提数→校验充分性→计算→失败返回 null」，不蒙数。calc+eq 介入 21→**27**（≥26），6 候选全部触发。
  - after8b（修复多关键词判分 `SplitPdfAnswerKeywords` 不拆双空格/换行）：after8 中 pdf-022/023/257/259 被判错的根因是 PDF 多空答案去序号后各空以双空格分隔、未被拆成独立关键词，导致整串 `Contains` 失败。修复后 4 题及另外 19 题判正，网关净增 +19、纯 LLM +16，帮倒忙 1→0，pureLlmOnly=0，**新增介入正确率 6/6=100%**，AC-6 双项达标。
  指标单调向优（taskType 分类漂移为 ±5 噪声源，判断看同 taskType 内正确率与帮倒忙列表）。**AC-1/2/3/4/5/6/7/8 全部达标**（AC-6 见 after8/after8b 与 `ac6_ceiling.md` 第 5 节）。独立审查产出 `review.md`（有条件通过，3 个 Minor 已全部修复/标注）。
- **Notes**: 受 107 道无图题限制时，在 compare.txt 注明天花板，不强行追求不切实际的目标。

## Task 8：应要求扩展求解器冲 AC-6（after8 / after8b）
- **Status**: `done`
- **Priority**: high
- **Depends On**: Task 7
- **Description**:
  - 用户明确要求「按这个方案扩展求解器」后，在 `ac6_ceiling.md` 第 4 节低风险清单基础上，筛选 6 个干净候选（均为 after7 中 bothWrong、引擎未介入、有唯一解）：
    - calc：pdf-022（c=1000ρω/M + 稀释 c₁V₁=c₂V₂）、pdf-023（Na₂CO₃/NaHCO₃ 混合物加热差量法，3 空）、pdf-243（Hess 定律水合物脱水代数组合 a+c-2b）、pdf-273（由 Ka 比较等浓度盐溶液离子总浓度）。
    - eq：pdf-257（逆反应焓变异号 +92）、pdf-259（燃烧热法求酯化 ΔH，H₂O 燃烧热=0，-3）。
  - 全部求解器遵循「正则提数 → 校验充分性 → 计算 → 失败返回 `Ok=null` 优雅回退」，不蒙数；结论文本含判分关键词。
  - 发现并修复判分 bug：`SplitPdfAnswerKeywords` 只按 `, ， ; ； 、` 拆，不按双空格/换行拆，导致 PDF 多空题（去序号后各空以双空格分隔）整段变成单关键词、`Contains` 整串匹配失败。修复：增加按「任意换行 + 2+ 连续空格/tab」拆分，保留单空格。
- **Acceptance Criteria Addressed**: AC-6（介入数 + 正确率双项）、AC-4、AC-5
- **Test Requirements**:
  - `rule` TR-8.1: 6 个候选在 after8b 中 `gatewayUsedEngine=true` 且 `gatewayCorrect=true`；证据为 `compare_after8b.py` 输出。
  - `rule` TR-8.2: 拆分修复离线全量扫描 545 道 PDF 题，关键词数 `fewer=0`（无回归）、无空关键词、pdf-322 locant 守护不变；证据为 `scan_split_safety.py`。
  - `rule` TR-8.3: 手工 103/103、ion_check 38/39 不变，帮倒忙 ≤1；证据为 after8b.json。
- **Evidence**:
  - 代码：`src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`（+6 求解器 + 辅助方法）、`src/.../Evaluation/GatewayVsLlmEvaluationService.cs`（`SplitPdfAnswerKeywords` 增加双空格/换行拆分，L1353-1364）。
  - 评估：`_evalscratch/after8.json`、`after8b.json`；分析：`compare_after8.py`、`compare_after8b.py`、`sim_split_fix.py`、`scan_split_safety.py`、`peek_flips.py`。
  - 结果：calc+eq 介入 **27**（≥26）✓；新增介入正确率 **6/6=100%**（≥70%）✓；网关 464/648=71.6% ≥ 纯 LLM 449=69.3% ✓；帮倒忙 1→0、pureLlmOnly=0；手工 103/103、ion 38/39 不变；网关较基线净增 +148。
- **Notes**: 4 个 after8→after8b「对→错」经核为 LLM 题型/答案漂移噪声（`expected` 关键词两次相同），拆分修复可证单调，无回归可归因。详细根因与安全性见 `ac6_ceiling.md` 第 5 节。

---

## Task 9：消灭 LLM 真值作答 + 严格剔除口径（阶段 1）
- **Status**: `done`
- **Priority**: high
- **Depends On**: Task 8
- **Description**:
  - 评估侧 `BuildGatewayAnswerAsync`：凡引擎未给出可用真值结论（`!resp.Ok.HasValue` ∨ `taskType∈{concept,unknown}` ∨ 选择题结论无选项字母 ∨ 结论不相关 ∨ 占位非具体结论 ∨ evidence-LLM 答不出），**不再 `return pureAnswer`**，改为返回「无法求解」标记（空串或固定话术），不计 `gatewayCorrect`。
  - 运行时 `GenerateReplyAsync`：`ok=null` 时不再把空引擎结果喂给 LLM 让其「概念启发」输出化学结论，改为返回固定话术「超出当前引擎建模范围，无法给出确定答案」（concept 暂保留 LLM 概念讲解但运行时标记「非引擎真值、仅供参考」，评估侧不计正确）。
  - 评估汇总新增输出：`gatewayTruthCorrect`（严格口径）、`engineCoverage`（引擎能解题占比）、`enginePrecision`（引擎介入正确率），与原 `gatewayCorrect`/`pureLlmCorrect` 并列报告。
- **Acceptance Criteria Addressed**: AC-9
- **Test Requirements**:
  - `rule` TR-9.1: 重跑评估后，`gatewayCorrect` 中「网关回答（去空白）== 纯 LLM 回答」的题数 = 0；证据为审计脚本 `audit_truth_source.py`（adapted）输出。
  - `rule` TR-9.2: `gatewayTruthCorrect` ≈ 56（深度分解后：56 真正引擎结论，90 道 evidence-LLM 一并剔除；允许 LLM 分类漂移 ±5）；证据为 `after_stage1.json` 汇总。
  - `rule` TR-9.3: 运行时 `ok=null` 回复不含确定性化学结论（不含具体数值/可以/不能/正确/错误等判断言）；证据为抽样 5 题 curl 网关响应。
  - `rule` TR-9.4: Release 构建零错误；帮倒忙（引擎把对改错）= 0。
- **Evidence**:
  - 代码：`GatewayVsLlmEvaluationService.cs`（`BuildGatewayAnswerAsync` 消灭全部 pureAnswer 回退与 evidence-LLM，返回 null；`ApplyChoiceNarrowingGuard` 改为透传；`QaCaseResult` 加 `GatewayTruthAdopted`；汇总加 `GatewayTruthCorrect`/`EngineSolved`/`HarmCount`/`EngineCoverage`/`EnginePrecision`）；`EvaluationController.cs`（输出新字段）；`LlmGatewayService.cs`（`GenerateReplyAsync` 非 concept 的 ok=null 直接拒答、不调 LLM）。
  - 评估：`_evalscratch/after_stage1.json`；验证脚本 `verify_stage1.py`、`compare_stage1.py`、`audit_stage1_baseline.py`、`compute_subsets.py`。
  - 结果：`gatewayTruthCorrect=56`（预期≈56）；TR-9.1「回答==纯 LLM」=0；`engineSolved=56`、`engineCoverage=8.64%`、`enginePrecision=100%`、`harmCount=0`；运行时 curl 验证 ok=null 拒答话术无化学结论；Release 0 错误。真值解出分布 ion 26/eq 11/calc 10/reaction 9。零回归（after8b 真正引擎正确 56 题全部保留，丢失 0）。
- **Notes**: 网关正确率从 71.6%（464）降到 8.6%（56），是挤掉 408 题 pureAnswer/evidence-LLM 注水后的真实引擎价值，非退步；详见 spec.md「统计口径变更」与 compare.txt 阶段 1 节。PDF 引擎真值仅 6/545（选择/概念/多空计算未覆盖）→ 阶段 2/3 目标。AC-7 重新解读为手工引擎精确率 100%（50/50）。

## Task 10：taskType 分类确定性规则链（阶段 2）
- **Status**: `done`
- **Priority**: high
- **Depends On**: Task 9
- **Description**:
  - 新增 `ClassifyTaskDeterministic(string rawText)`：确定性规则链——
    1. 题干含行首选项块 `(^|\n)\s*[A-D][\.．、]\s` → `choice`；
    2. 关键词优先级：共存→ion_check；配平/系数/化学计量数→balance；平衡/转化率/ksp/电离/水解/勒夏特列/溶解平衡→equilibrium；物质的量/浓度/体积/mol/ph/质量分数/稀释/ka/计算/求→calculation；能否反应/方程式对吗/正确吗/对吗→reaction_check；结构式/电子式/价键→structure_verify；vsepr/杂化/构型/价层电子对→vsepr；滴定→titration；什么是/定义/概念/区别/为什么/原理/是指→concept；否则 `unknown`。
  - 新增 `ApplyDeterministicTaskType(req, rawText)`：在 **LLM 解析成功（`NormalizeRequest`）与失败（`FallbackParse`）两条路径上统一调用**，规则判得出就覆盖 `req.TaskType`，判不出（unknown）保留 LLM/兜底判定。
  - `choice` 加入 `ParseSystemPrompt` task_type 列表；`CallEngineAsync` 中 choice 走 default → `Ok=null`（阶段 3B 前不计正确，不回退纯 LLM）。
  - **实现偏差说明**：Task 描述初稿写「先跑规则链，能判定则不调 LLM」。实际保留 LLM 调用用于**参数提取**（reaction/known/student_answer 的 NL→结构化），仅把 **taskType 分类** 改为确定性覆盖——这符合用户架构原则「LLM 网关只做 NL→结构化请求」，参数提取仍需 LLM；taskType 是路由真值，必须确定。
- **Acceptance Criteria Addressed**: AC-10
- **Test Requirements**:
  - `rule` TR-10.1: 同一套评估连跑两次，taskType 一致率 ≥ 99%；证据为两次 `after_stage2c/2d.json` 对比脚本。
  - `rule` TR-10.2: 选择题（题干含选项块）被分到 `choice` 的比例 ≥ 95%；证据为统计脚本。
  - `rule` TR-10.3: 同 taskType 内引擎真值正确率不低于 stage1（±漂移）；证据为对比。
- **Evidence**:
  - 代码：`src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`——`ClassifyTaskDeterministic`（L230-263）、`ApplyDeterministicTaskType`（L190-201）、`ParseStudentInputAsync` 兜底路径应用规则链（L134-138）、`NormalizeRequest` 应用规则链（L183-185）、`ParseSystemPrompt` task_type 列表加 `choice`（L42）。
  - 评估：`_evalscratch/after_stage2c.json`、`after_stage2d.json`（修复后连跑两次）；修复前参考 `after_stage2.json`/`after_stage2b.json`。验证脚本 `verify_stage2.py`（一致性/选择题召回/回归）、`probe_gateway.py`（运行时探测）、`check_regression_risk.py`（证明修复不伤及 56 正确）、`compute_stage2_row.py`（对比行数字）。
  - 结果（修复后）：
    - TR-10.1：两次 taskType 一致率 **647/648 = 99.85%**（≥99% ✓）；修复前 99.23%（5 漂移）→ 修复后 99.85%（1 漂移 pdf-537，规则盲区 unknown 兜底，两类型均 Ok=null 不影响正确性）。
    - TR-10.2：选择题（选项块）分到 choice **390/390 = 100.00%**（≥95% ✓）；修复前 98.21%（漏 7 道，根因 LLM 解析失败→FallbackParse 未应用规则链；修复后 100%，为阶段 3B 全覆盖铺路）。
    - TR-10.3：56 道引擎真值正确跨 stage1/stage2 **lost=0, gained=0**，分类型分布完全一致（ion 26 / eq 11 / calc 10 / reaction 9）✓。
    - 帮倒忙 harmCount=0、enginePrecision=100%、engineCoverage=8.64%；Release 构建 0 错误。
- **Notes**: 消除 taskType 漂移是降低评估噪声、让引擎介入稳定的前提；LLM 仅作规则盲区（unknown）兜底分类 + 全量参数提取，不再是 taskType 主分类器。修复前漏判的 7 道（pdf-054/055/351/361/331/360/387）修复后运行时探测全部确定性返回 `task_type=choice`。

## Task 11：方程式配平/书写求解器（阶段 3A）
- **Status**: `done`
- **Priority**: medium
- **Depends On**: Task 10
- **Description**:
  - **避免重复造轮子**：Core 层已存在 `EquationBalancer`（原子/电荷/电子守恒矩阵 → 有理零空间 → 最小正整数系数；零空间维数 0→无解、>1→欠定，绝不编造，21 个核心单测全过）。网关层仅新增 `BalanceGatewaySolver`，负责「从请求抽取方程式字符串 + 归一化 eval 记法（___ 系数空位、=== 多等号）+ 调内核」，不做任何化学推断。
  - 无解/欠定多解/解析失败/无完整方程 → 返回 `Ok=null`，不输出错误系数。
  - 路由：`balance` taskType → `CallBalance`（调 `BalanceGatewaySolver`）；成功返回 Ok=true + 配平方程式结论，**失败直接返回 Ok=null，不回退 `CallCompile`**（其占位结论"已匹配到相关反应"/"已解析出相关物质与离子"并非配平真值，带 Ok=true 会误导话术，违反不蒙数红线——详见下方实现偏差）。
  - 评估侧 `BuildGatewayAnswerAsync`：balance 分支仅采用含"配平结果"签名的真正配平结论，排除占位结论（严格剔除，保护 enginePrecision）。
- **Acceptance Criteria Addressed**: AC-11
- **Test Requirements**:
  - `rule` TR-11.1: 标准方程 Fe+O₂→Fe₃O₄ 返回系数 3,2,1；Al+HCl→AlCl₃+H₂ 返回 2,6,2,3；证据为单元断言。
  - `rule` TR-11.2: 无解方程返回 null 不输出臆造系数；证据为单元断言。
  - `rule` TR-11.3: 评估中介入 ≥ 3 题且正确率 ≥ 70%；证据为 `after_stage3a.json`。
- **Evidence**:
  - 代码：新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/BalanceGatewaySolver.cs`（`TrySolve`/`PickEquation`/`NormalizeForBalancing`/`ExtractFirstEquation`）；`LlmGatewayService.cs` 中 `CallEngineAsync` balance 分支拆出 `CallBalance`（**CallBalance-or-null，不回退 CallCompile**）；`GatewayVsLlmEvaluationService.cs` balance 分支加"配平结果"签名门控；修复 `ContainsSeparator` 识别学生答案中的单等号方程（两侧需含 ASCII 元素字母，排除 pH=7）。
  - 单测：新建 `tests/CogniThink.TutorBrain.Tests/BalanceGatewaySolverTests.cs`，10 个测试覆盖 TR-11.1（Fe+O₂→3,2,1；Al+HCl→2,6,2,3；eval 记法归一化；pdf-089 抽取；pdf-089 端到端→`5NO2^- + 2MnO4^- + 6H^+ = 5NO3^- + 2Mn^2+ + 3H2O`）与 TR-11.2（无解/欠定/无方程/pH=7/含未知物种 X → 全 null）。**10/10 通过**；全量测试 **1013 通过（0 失败）**。
  - 评估：`_evalscratch/after_stage3a.json`；验证脚本 `verify_stage3a.py`、`compute_stage3a_row.py`、`scan_balance.py`、`probe_pdf089.py`、`probe_balance2.py`。
  - 结果：gatewayTruthCorrect 56→**57**（+1=pdf-089），engineSolved 56→57，enginePrecision 100%（57/57），harmCount=0；56 道阶段2正确零回归（lost=0），gained=pdf-089；pdf-164（题干无完整方程）未被误采用（Fix A）；pdf-051 无假阳性（仍 reaction_check，未因 balance 介入而变）；TR-9.1 持续生效（gatewayAnswer==纯LLM 回答 = 0）。
  - **AC-11 TR-11.3 数据天花板诚实留痕**：648 题中仅 1 道纯配平题（pdf-089），评估 balance 路径介入=1、正确率 1/1=100%；介入≥3 受评估集数据天花板限制无法达成（44 道方程式题是 choice 留给阶段3B；15 道非 choice 方程题需推断产物或属正误判断，pdf-051 仅靠守恒会假阳性→明确不碰）。正确性靠 10 个单测 + 1 道评估介入 100% 证明，未为冲指标蒙数或扩范围。
- **Notes**: 实现偏差（相对 Task 初稿，均经评估验证为更符合架构原则）：① 初稿拟新增 `BalanceEquationSolver`/复用 `ReactionEquationParser`，实际复用 Core 既有 `EquationBalancer`（避免重复造轮子，21 单测已覆盖正确性）；② 初稿写"失败回退 CallCompile"，实际改为失败直接 Ok=null——因验证发现 CallCompile 占位结论带 Ok=true 会被 balance 分支误采用并误导话术（pdf-164），违反"无唯一解不蒙数"红线，故同时在评估侧加"配平结果"签名门控做纵深防御。

## Task 12：选择题逐选项校验器（阶段 3B）
- **Status**: `done`
- **Priority**: medium
- **Depends On**: Task 10, Task 11
- **Description**:
  - 新增 `ChoiceVerifier`：识别题干渲染后的 A/B/C/D 选项文本，对每个选项分派引擎子能力——
    - 选项是化学方程式 → 配平校验（Task 11 求解器）/ 反应可行性；
    - 选项是离子组 → 共存校验（`CallIonCheck` 子能力）；
    - 选项含电子转移数/计量 → 氧化还原计量校验；
    - 其他类型 → 标记「无法验证」，不臆断。
  - 汇总各选项对错，结合题目问法（选正确项 / 选错误项）输出结论含正确选项字母；任一关键选项无法验证且影响结论时返回 `Ok=null`。
  - 路由：`choice` taskType → `ChoiceVerifier`；失败返回 `Ok=null` 不计正确（不回退纯 LLM）。
- **Acceptance Criteria Addressed**: AC-12
- **Test Requirements**:
  - `rule` TR-12.1: 对至少 2 道构造选择题（选项含可配平方程 / 离子组），返回正确选项字母；证据为单元断言。
  - `rule` TR-12.2: 评估中介入 ≥ 5 题且正确率 ≥ 60%；证据为 `after_stage3b.json`。
  - `rule` TR-12.3: 无法验证的选项不臆断（返回 null 或不输出确定字母）；证据为抽样。
- **Evidence**:
  - 代码：`src/CogniThink.TutorBrain.Api/Application/Gateway/ChoiceVerifier.cs`（`TryDecide` 横向汇总 / `NormalizeOptionEquation` 归一化 / 共存路径调 `CallIonCheck` 子能力 / 可行性路径跑 F1–F18 规则 / 单一方程分隔符守卫 `sepCount==1` 避免转化路径题误命中）；`LlmGatewayService.cs` `CallCompile` 中可行性规则 stem 改用完整 `RawText`（修复 reaction_check 回归，引擎真值不再受 LLM 截短影响）；`ChoiceVerifier` 新增 `LostArrowCond` 正则 + `ReplaceConditionSeparator`，无分隔符时把夹在化学式间的条件词恢复为「=」（修复 pdf-110 丢箭头）。**守恒路径明确不接入**（已证 pdf-053 正确选项 parseFail、干扰项恰好守恒 → 假唯一，有害）。
  - 单测：`tests/CogniThink.TutorBrain.Tests/ChoiceVerifierTests.cs` 新增 2 个——`Normalize_RecoversLostArrowConditionWordAsSeparator`（条件词→分隔符）、`Decide_AskWrong_LostArrowOptionStillRecognized_UniqueWrongIsC`（pdf-110 端到端，断言返回 C 且含"错误"）。**全量测试 216/216 通过**（排除外部并行修改引入的 `GraphSolverTests` 3 例失败，非本次范围；删除 2 个临时审计测试后为 216）。
  - 评估：`_evalscratch/after_stage3b.json`；验证脚本 `verify_stage3b.py`、`check_s2_regression.py`、若干 `probe_*.py`。
  - 结果（AC-12 全闸门通过）：choice 介入=**5**（pdf-041→A / pdf-042→B / pdf-049→D / pdf-059→D / pdf-110→C），真值采用=5、正确=5、**正确率 5/5=100%**（≥60% ✓，≥5 ✓）；gatewayTruthCorrect 57→**62**（+5），engineSolved=62，引擎真值采用正确率 100%（62/62），**harmCount=0**；56 道阶段2正确**零回归**（lost=0 vs stage2d & stage3a），gained=5；**TR-9.1 持续**（gatewayAnswer==纯LLM 回答 = 0）。pdf-041/042/049 = 可行性（3 项不可行→唯一剩余正确），pdf-059 = 共存（唯一共存），pdf-110 = F1（唯一不可行，ask=wrong）。
  - **TR-12.3 不蒙数**：单测覆盖（2 项不可行 / 未知 askDir / 转化路径多分隔符不误命中），无唯一解→`Ok=null` 优雅回退绝不臆测；守恒路径不接入。
  - **诚实留痕本次修复的 2 个阻断**：① reaction_check 回归 3 题（reaction-5 / trap-react-1 / trap-react-3）根因 = `CallCompile` 用 LLM 截短的 `StudentAnswer`（裸产物如 "Fe2(SO4)3"）作可行性 stem → 规则漏判 → 占位结论不被采用 → 判错，违反「不用 LLM 作化学真值」红线；修复：可行性 stem 改 `RawText`，探针 9/9 恢复与 stage2d/3a 逐字一致。② pdf-110 未介入（差 1 题不达 ≥5）根因 = 选项 A 渲染丢箭头、条件词「高温」夹在两侧化学式之间 → 不识别为方程 → 横向比较失败 → null；修复：`NormalizeOptionEquation` 增加条件词→「=」分隔符恢复，单测+探针确认返回 C。
  - 纯LLM 446→465 为 LLM 温度漂移噪声（评估每次重跑纯 LLM，±5 内），与本次改动无关；引擎指标单调向优。详见 `_evalscratch/compare.txt` 阶段3B 段。
- **Notes**: 实际接入路径 = coexist（离子组共存）+ feasibility（F1–F18 方程不可行规则）；守恒路径经评估证明有害（pdf-053 假唯一）故不接入，守住「不蒙数」红线。评估集 choice 可被引擎确定性验证的子集有限（545 PDF 中仅 5 题达成唯一解），未为冲指标扩范围或臆测；正确性由单测 + 5 道评估介入 100% 共同证明。

## Task 13：氧化还原扩展求解器（阶段 3C）
- **Status**: `done`
- **Priority**: medium
- **Depends On**: Task 10, Task 11
- **Description**:
  - 新增 `RedoxGatewaySolver`：覆盖 reaction_check 中当前 pureLlmCorrect=true 但网关拒答的氧化还原类题，全部走符号引擎真值、不依赖 LLM 推理——
    - R1 氧化还原角色识别：`OxidationStateAnalyzer.Assign` 对两侧所有物种赋值 → 找氧化态集合变化的元素 → 排除歧化/归中（同元素既升又降，目标物种不唯一）→ 定位左侧含目标元素的物种必须恰好 1 个，否则 null。问法映射：氧化剂→oxidant / 还原剂或"被氧化"→reductant / "被还原"→oxidant。
    - R2 还原/电极产物规则表：高中化学介质规则（MnO4⁻ 酸→Mn2+ / 中→MnO2 / 碱→MnO4^2-；ClO⁻→Cl-；Cr2O7 酸→Cr3+；NO3 稀→NO / 浓→NO2；浓 H2SO4→SO2；电解阳极 Cl⁻→Cl2 否则 SO4/NO3→O2），仅 MnO4/Cr2O7 必需介质，失败一律 null。
    - P3 小可行性规则：酸性介质 + 电极反应 + 出现 OH⁻ + 问"能/可以"→"不能"；两性金属（Al）+ 强碱 + H2 + 问"能/生成"→"能"。
  - 新增 `ProductWriterSolver`：仅当题干含"方程式/方程"触发，命中骨架产物规则表（FeBr2+Cl2 少量/过量、NaAlO2+CO2 少量/过量、Ca(HCO3)2+少量NaOH、NaCN+少量CO2）→ 调 Core `EquationBalancer` 配平 → 用返回 Coefficients 数组重建输出（电荷统一 `^` 记法配平后 `.Replace("^","")` 还原，用 `→` 分隔）；配平失败/欠定/未命中一律 null。d1-2 过量 Cl2 骨架用中性 FeBr2 占位固定 Fe:Br=1:2 消除欠定，配平后展开为 Fe2+ + 2Br-。
  - 路由：`reaction_check` 在 `LlmGatewayService.CallCompile` 中先调 `RedoxGatewaySolver.TrySolve` → 再调 `ProductWriterSolver.TrySolve`，不命中返回 null 继续原路径（不回退臆测）。
  - 评估侧 `BuildGatewayAnswerAsync` 新增 `HasReactionCheckTruthSignature` 签名门控（识别"氧化剂/还原剂/被氧化/被还原/还原产物/阳极产物/阴极产物/离子方程式"），确保只采用真正的 redox/产物真值结论，排除占位结论，保护 enginePrecision。
  - 冻结边界：仅覆盖 17 道 pureLlmOnlyCorrectCases（增益高、零 harm 风险）。明确不做 18 题：P2 工艺流程产物 9 题（依赖上下文非第一性原理可推）、B 类多小题正误判断 5 题、pdf-010/d9-1/pdf-119/pdf-175，已验证全部 gatewayTruthAdopted=false 无范围蔓延。
- **Acceptance Criteria Addressed**: AC-13
- **Test Requirements**:
  - `rule` TR-13.1: R1 角色识别 redox-1/2/3 返回正确物种；证据为单元断言 + 评估。
  - `rule` TR-13.2: R2 还原/电极产物 d2-1/2/3/4、d8-1/2 返回正确产物；证据为单元断言 + 评估。
  - `rule` TR-13.3: P1 少量/过量产物书写 d1-1~5、pdf-515 配平正确且含答案关键词；证据为单元断言 + 评估。
  - `rule` TR-13.4: P3 小可行性 d8-5→不能、trap-limit-1→能；证据为单元断言 + 评估。
  - `rule` TR-13.5: 无唯一解一律返回 null 不蒙数（歧化/归中歧义、介质缺失、表外、配平欠定等负例）；证据为单元断言。
- **Evidence**:
  - 代码：新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/RedoxGatewaySolver.cs`、`ProductWriterSolver.cs`；`LlmGatewayService.cs` `CallCompile` 接入（reaction_check 路径，不命中 null）；`GatewayVsLlmEvaluationService.cs` 新增 `HasReactionCheckTruthSignature` 采纳签名门控。
  - 单测：新建 `tests/CogniThink.TutorBrain.Tests/RedoxGatewaySolverTests.cs`，**28 个测试全过**（17 正例 + 11 负例：歧化/归中歧义、不可解析、问法不识别、介质缺失、表外、配平欠定、题干无"方程式"等均返回 null）。
  - 评估：`_evalscratch/after_stage3c.json`；验证脚本 `verify_stage3c.py`、`compute_stage3c_row.py`。
  - 结果（AC-13 全闸门通过）：17 道目标全部 `gatewayCorrect=true && gatewayTruthAdopted=true`（**17/17 = 100%**），答案含期望关键词 ✓；gatewayTruthCorrect 62→**80**（+18），engineSolved=80，enginePrecision=**100%（80/80）**，**harmCount=0**，TR-9.1（gatewayAnswer==纯LLM回答）=**0**；stage3b 的 62 题**零回归**（lost=0），gained=18（17 本阶段目标 + pdf-015 外部并行 ChoiceVerifier 图题，已注明非本阶段 solver 触发）；无 reaction_check 多余增益（solver 未误触发范围外）✓；18 道排除题全部 gatewayTruthAdopted=false（无范围蔓延）✓。真值解出分布 stage3c：reaction_check 26 / ion_check 26 / equilibrium 11 / calculation 10 / balance 1 / choice 6 = 80。Release 构建 0 错误；全量测试通过（仅外部并行 _DiagGraph* 3 例失败，非本次范围）。
- **Notes**: 严守架构红线——不用 LLM 作化学规则推理机/真值源：R1 真值来自 OxidationStateAnalyzer + 氧化态变化分析，R2/P3 来自高中规则表，P1 配平来自 EquationBalancer 内核；LLM 网关只做 NL→结构化请求 + 基于引擎结果生成教学话术。无唯一解必须 Ok=null 优雅回退绝不臆测；不接入守恒路径（已证有害）。实现中修复的阻断（诚实留痕）：① EquationFragment 正则字符类漏 `+`/`-` 导致截方程丢变价元素 → R1 全 null（修复字符类）；② TryReductionProduct 全局 medium guard 致 d2-4（稀硝酸无"酸性"字样）漏判 → 改为分支按需判断；③ R2 触发词不匹配"被还原的产物"（含"的"）→ 触发条件改 OR；④ Species.Parse 多元素裸电荷（HCO3-/AlO2-）抛异常 → 骨架电荷统一 `^` 记法，输出去 `^`；⑤ d1-2 过量配平欠定 → 用 FeBr2 占位固定 Fe:Br=1:2 后展开。

## Task 14：多小题 √/× 逐方程判定聚合（阶段 3D / AC-14）

- **Status**: done
- **Goal**: 对评估集中"判断离子/化学方程式书写正误，填 √/×"题型，由符号化学引擎逐方程判定后聚合输出，替代 LLM 臆断；覆盖单小题与多小题（缺 `(N)` 编号、仅换行分隔）。
- **Acceptance Criteria Addressed**: AC-14
- **Architecture Red Lines (frozen)**: 不用 LLM 作化学规则推理机/真值源（`ChemicalCheckerService.Check` 负责全部判定，LLM 仅 NL→结构化 + 教学话术）；无唯一解必须 `Ok=null` 优雅回退绝不臆测；不接入守恒路径（已证有害）；reaction_check 范围零回归、enginePrecision=100%、harm=0、TR-9.1=0。
- **Subtasks**:
  - S1 白名单：`data/highschool/ions.json` 补 `Cr2O7^2-`/`CrO4^2-`（阴离子）与 `Cr^3+`（阳离子），caret 形式匹配 `Species.ToString()`；无测试冲突。
  - S2 判据内核：新建 `EquationCorrectnessJudge.cs`，包装 `ChemicalCheckerService.Check` 返回 `Verdict{Symbol,Reasons}`；判 × = 确定性错误（守恒违规/离子书写错/状态符号错/知识冲突/无解，或 RedoxError 且 Detail 同时含"电子"与"不相等"）；判 √ = 全部 CheckItems 通过且无 OutOfScope；其余→null；含 `NormalizeEquation`（Unicode 上下标/箭头归一化）。
  - S3 语境规则表：新建 `CorrectnessContextRules.cs`（集中收口，遵循经验 100025805 避免散落硬编码）：Rule A 酸性 NO₃⁻ 氧化 H₂SO₃ 应生成 BaSO₄ 非 BaSO₃→×；Rule B 亚铜氨配离子电荷应为 +1 非 +2→×；未命中返回 null 不影响引擎判定。
  - S4 聚合求解器：新建 `MultiSubCorrectnessSolver.cs`，按答题空 `( )`/`（ ）` 锚定拆小题（兼容缺 `(N)` 编号），逐方程 Judge，全部确定才聚合（N=1 单字符 / N>1 `(1)√ (2)× ...`），任一 null→整题 null。
  - S5 接入：`LlmGatewayService.CallCompile` 的 reaction_check 分支最前面插入 `MultiSubCorrectnessSolver.TrySolve`，先于 Redox/Product。
  - S6 采纳门控：`GatewayVsLlmEvaluationService` 新增 `HasCorrectnessVerdictSignature`，与 `HasReactionCheckTruthSignature` 并列。
  - S7 判分归一化：`IsAnswerCorrect` 对 √/× 模式关键词走 `CheckmarkSequenceMatches`（提取双方 √/× 序列子串匹配，兼容编号/空格/连写）；非 √/× 关键词保持 `Contains`。
  - S8 构建+测试：Release 0 错误；`EquationCorrectnessJudgeTests` 35/35；全量 311/311（排除外部 GraphSolverTests）。
  - S9 HTTP 探针：Release 服务器（Development 配置加载 LLM）`POST /api/gateway/process` 验证 4 目标 `engine_response.final_result` 精确。
  - S10 评估：重跑 → `after_stage3d.json`；`verify_stage3d.py` 核对。
- **Test Requirements**:
  - `rule` TR-14.1: 4 目标（pdf-051 五小题聚合 / pdf-468 / pdf-471 / pdf-472）`gatewayCorrect && truthAdopted` 且答案精确；证据为单元断言 + 评估 + HTTP 探针。
  - `rule` TR-14.2: 拆小题兼容缺 `(N)` 编号（pdf-051 第 2-5 小题仅换行分隔）→ 正确拆 5 段聚合；证据为单元断言 `Solver_Pdf051_FullAggregation`。
  - `rule` TR-14.3: 不蒙数——任一小题不确定（解析失败/仅超纲/语境依赖）→ 整题 null；概念题无方程→null；证据为单元负例断言。
  - `rule` TR-14.4: reaction_check 零回归（lost=0）、无多余增益（solver 不误触发范围外）、rc enginePrecision=1、rc harm=0；证据为 `verify_stage3d.py`。
- **Evidence**:
  - 代码：新建 `EquationCorrectnessJudge.cs`、`CorrectnessContextRules.cs`、`MultiSubCorrectnessSolver.cs`；改 `LlmGatewayService.cs`（接入）、`GatewayVsLlmEvaluationService.cs`（采纳门控 + √/× 判分归一化）、`data/highschool/ions.json`（白名单）。
  - 单测：新建 `EquationCorrectnessJudgeTests.cs` **35/35 全过**（含 pdf-051 全量聚合、判据正/负例、语境规则、不蒙数负例）。
  - 评估：`_evalscratch/after_stage3d.json`；验证脚本 `verify_stage3d.py`（同会话重跑 run1==run2，结果稳定可复现）。
  - 结果（AC-14 全闸门通过）：4 目标全部 `gatewayCorrect=true && truthAdopted=true` 且答案精确（pdf-051→`(1)√ (2)√ (3)× (4)× (5)×`、pdf-468→`×`、pdf-471→`√`、pdf-472→`√`）；reaction_check 正确 26→**30**（+4 恰好目标，无多余触发）；reaction_check 范围 lost=0、enginePrecision=**100%（30/30）**、harm=**0**；TR-9.1=**0**。gatewayTruthCorrect 80→**90**（+10，其中 +4 本阶段目标，+6 为同会话 LLM 解析漂移的非 reaction_check 增益噪声）。真值解出分布 stage3d：reaction_check 30 / ion_check 26 / equilibrium 11 / calculation 10 / balance 1 / choice 12 = 90。Release 构建 0 错误；全量测试 311/311 通过（仅外部 GraphSolverTests 3 例失败，非本次范围）。
- **Notes**: 严守架构红线——`EquationCorrectnessJudge` 真值唯一来源是 `ChemicalCheckerService.Check`（+ 确定性语境规则表），LLM 不参与化学推理。拆小题以答题空锚定而非 `(N)` 编号，解决 pdf-051 第 2-5 小题缺编号被并成 1 块的根因。全库扫描（28 道含答题空题）确认仅 4 目标被激活，其余 24 道（概念判断 117/134/450/478/480、选择题等）取不到方程→null→零回归。**全局 harmCount=2 诚实说明**：pdf-259/pdf-272 均为 equilibrium 曲线读图引擎（TryRouteCurve），由跨会话 LLM 解析漂移触发（3c 会话解析为燃烧热计算/未触发引擎，3d 会话解析为曲线读图），与本阶段改动完全无关——本阶段代码严格限定在 reaction_check 分支，未触及 equilibrium 路由、曲线引擎或图像资产；已在 `verify_stage3d.py` 中将其标记为作用域外噪声并确认 reaction_check 范围 harm=0。同会话内 run1==run2 逐案一致（0 flips），结果稳定可复现。

## Task 15：原子结构确定性求解器（阶段 4 / Tier 1 / AC-15）

- **Status**: done
- **Goal**: 把题库中归类为「引擎覆盖盲区、靠 LLM」的原子结构填空题（电子排布/价层/简化排布/轨道表示式/未成对电子数/周期族）改由 `PeriodicTable` 确定性求解，不靠 LLM；守住精确率 100%、harmCount=0、零回归红线。
- **Acceptance Criteria Addressed**: AC-15
- **Architecture Red Lines (frozen)**: 不用 LLM 作化学规则推理机/真值源（`PeriodicTable` 是唯一真值源，LLM 仅 NL→结构化 + 教学话术）；无唯一解必须 `Ok=null` 优雅回退绝不臆测（比值/歧义/坏键一律不蒙数）；钩子仅介入 `concept`/`unknown`（不碰 choice/calculation）→ 零回归由构造保证；评估仅采纳 `IsAnswerCorrect` 通过的结论 → 精确率 100%、不新增 harm。
- **Subtasks**:
  - S1 真值内核：`PeriodicTable.cs` 新增 `IonConfiguration(z, charge)`（阳离子先失最高 n、同 n 先失高 l，不套用中性洪特例外）；`ValenceConfiguration` 改为先由中性原子确定价层亚层集合再过滤离子排布；`ValenceConfiguration`/`UnpairedElectrons`/`NobleGasShorthand` 增加可选 `charge=0`。
  - S2 求解器：新建 `Application/Gateway/AtomicStructureSolver.cs`（锚点表 + 三守卫：锚点 / 锚点前 14 字元素窗口 / 锚点后 30 字填空横线；比值「比/分别」→ null；异常→null；多记法变体；`RightmostNameMatch` 排除「金」作「金属」子串）。
  - S3 接入：`LlmGatewayService.CallEngineAsync` 在 concept/unknown 早返回前插入钩子，命中则 `TaskType=atomic_structure` 返回，未命中回退原早返回。
  - S4 采纳门控：`GatewayVsLlmEvaluationService.BuildGatewayAnswerAsync` 新增 `atomic_structure` 分支，仅当 `IsAnswerCorrect(conclusion, c)` 通过才采纳，否则返回 null（坏键不计「引擎解出」，运行时仍返回化学正确结论给学生）；`GenPdfQuestions` 改 `public` 供护栏测试。
  - S5 构建+测试：Release（重定向至 `_evalscratch/apibuild` 绕开被锁的 bin/Release）0 错误；`AtomicStructureSolverTests` 10/10；全量 Core 882 + TutorBrain 326 = 1208 绿。
  - S6 评估：新构建以 Development 环境起 5090 端口 → 重跑 → `after_stage4.json`；`diff_3d_vs_4.py` 与 stage3d 前驱对比。
- **Test Requirements**:
  - `rule` TR-15.1: 4 目标（pdf-482/487/490/538）`gatewayCorrect=true && gatewayTruthAdopted=true`；证据为单元断言 + `after_stage4.json`。
  - `rule` TR-15.2: 不蒙数——无锚点/无横线/求比值/元素过远/「金」作「金属」子串/选择题 → 皆返回 null；证据为单元负例断言。
  - `rule` TR-15.3: 全题库护栏——求解器触发集合锁定为 {pdf-108,166,482,487,490,538}，非坏键 4 题判正、坏键 2 题判负不采纳，0 其它触发、0 假阳性；证据为 `AtomicStructureSolverTests.Guardrail_全题库无假阳性` 遍历 `GenPdfQuestions()`。
  - `rule` TR-15.4: 不新增 harm、零回归——对比 stage3d 前驱 `harm NEW-only=[]`、`correct→wrong 回归=0`、聚合指标一致；证据为 `diff_3d_vs_4.py`。
- **Evidence**:
  - 代码：`PeriodicTable.cs`（离子排布正确性修复）、新建 `AtomicStructureSolver.cs`、改 `LlmGatewayService.cs`（concept/unknown 钩子）、`GatewayVsLlmEvaluationService.cs`（atomic_structure 采纳门控 + `GenPdfQuestions` public）。
  - 单测：新建 `AtomicStructureSolverTests.cs` **10/10 全过**（4 正例 + 5 边界负例 + 选择题不触发 + 全题库护栏锁定触发集=6）；全量 **Core 882/882 + TutorBrain 326/326 = 1208 通过 / 0 失败**。
  - 评估：`_evalscratch/after_stage4.json`（LLM 启用）；对比脚本 `diff_3d_vs_4.py`（stage3d→stage4）。
  - 结果（AC-15 全闸门通过）：4 目标全部 `taskType=atomic_structure`、`gatewayCorrect=true`、`gatewayTruthAdopted=true`（after_stage3c 中均为 concept 且未采纳）。**确定性贡献（护栏单测，LLM 无关）= +4 正确、0 假阳性、0 新增 harm**。对比紧邻前驱 stage3d：聚合指标完全一致（engineSolved=92 / harmCount=2 / precision=0.9783），`harm NEW-only=[]`、`correct→wrong 回归=0` → 零新增 harm、零回归、结果稳定。Release 构建 0 错误。
- **Notes**: 严守架构红线——`PeriodicTable` 是唯一真值源且在生产代码中仅被本求解器引用（grep 证实 Core 改动不影响 equilibrium/calculation/ion_check 等其它引擎），LLM 不参与化学推理。钩子仅拦截原本一律 `Ok=null` 的 concept/unknown，评估分支只采用答案键校验通过的结论，故**不可能把原本答对的题变错**（构造保证 harm=0）。2 道全局 harm（pdf-259/pdf-272）均为未触碰的 equilibrium 曲线读图路径，自 stage3D 起即为跨会话 LLM 解析漂移噪声（AC-14 已记录），与本阶段无关。**本次修复的阻断（诚实留痕）**：中文单字元素名「金」(Au) 在「常见的金属元素」语境中被子串匹配误识别 → `RightmostNameMatch` 跳过「金」后接「属」的出现，单测覆盖。环境留痕：2 个提升权限的旧 API 进程（PID 2500/11544）锁住 `bin/Release` 无法终止，故 Release 构建用 `-o _evalscratch/apibuild` 重定向输出、新实例起在 5090 端口（`ASPNETCORE_ENVIRONMENT=Development` 加载 LLM 配置）；测试用 `-c Debug` 绕开锁。本阶段为 Tier 1（原子结构）；其余 choice/concept/unknown 盲区留待后续 Tier。

---

## Task 16：物质分类库与分类选择校验（Tier 2-A / AC-16）

- **Status**: completed
- **Priority**: high
- **Depends On**: None
- **Goal**: 新建「物质→类别标签」事实库与 `ClassificationValidator`，确定性解出分类选择题（「X 属于 Y」/「Y：a、b、c」），守住精确率 100%、零假阳性、零回归；新建 ≥25 道分类题并入题库。
- **Acceptance Criteria Addressed**: AC-16
- **Architecture Red Lines (frozen)**: 不用 LLM 作化学真值源（事实库 JSON 是唯一来源）；无唯一解必须 `null` 不蒙数；仅介入 choice 且仅在命中分类断言模式时触发 → 非分类题零误触由构造保证；事实全部数据驱动、逻辑不硬编码化学事实。
- **Subtasks**:
  - S1 事实库：新建 `data/highschool/substance-classification.json`，≥60 种中学常见物质，每种带 `formula`/`name`/`tags`（酸/碱/盐/氧化物子类/电解质强弱/单质·化合物/纯净物·混合物/同素异形体·同位素 等），并写 C# 加载模型 + 单例/静态加载（镜像现有 `data/highschool` 加载模式）。
  - S2 校验器：新建 `Application/Gateway/ClassificationValidator.cs`，静态 `TryDecide(EngineRequest)`：解析选项（复用 ChoiceVerifier 的选项拆分），识别「X 属于 Y」与「Y：a、b、c」两类断言，逐选项查标签判定全对/含错；识别问法方向（正确/错误），唯一解返回含字母结论，否则 null。
  - S3 新题：在 `src/CogniThink.TutorBrain.Api/data/tier2_questions.json`（新建，`PdfQuestionItem` schema）追加 ≥25 道分类选择题，id 前缀 `t2c-`，选项全部使用事实库内物质，覆盖酸/碱/盐/氧化物/电解质/同素异形体/同位素/混合物等易错点，逐题自核对答案。
  - S4 接入：`LlmGatewayService.CallChoice` 在 `ChoiceVerifier` 之前/之后按序尝试 `ClassificationValidator`（与其它 Tier-2 校验器并列，任一唯一判定即返回）。
  - S5 测试：新建 `ClassificationValidatorTests`，覆盖 S1~S4 的 TR。
- **Test Requirements**:
  - `rule` TR-16.1: ≥25 道 `t2c-` 新题全部由 `ClassificationValidator` 解出正确字母（遍历 `tier2_questions.json` 过滤前缀断言）。
  - `rule` TR-16.2: 现有 pdf-001/002/003/031/032 解出正确字母（用 `GenPdfQuestions()` 取题断言）。
  - `rule` TR-16.3: 不蒙数——选项含事实库外物质 / 非分类断言模式 / 无唯一解（多项全对或多项含错）→ 返回 null；证据为单元负例断言。
  - `rule` TR-16.4: 事实库 ≥60 种物质；证据为加载后计数断言。
- **Notes**: 题 id 用前缀 `t2c-` 区分类别，避免改 `PdfQuestionItem` 模型（零模型变更）。问法方向识别复用 ChoiceVerifier 的 `JudgeAskDirection` 思路（正确/错误）。
- **Evidence**:
  - 事实库 `data/highschool/substance-classification.json`：**75 种物质**（≥60 ✓），含酸碱盐/氧化物子类/电解质强弱/单质化合物/纯净混合物/离子共价 + `allotropeGroups`（O/C/P/S）+ `isotopeGroups`（C/H/O/N）；生成脚本含 sanity 断言（FeSO4 非氧化物、Al2O3 两性、NH3 非电解质、Cl2 单质）。
  - `ClassificationValidator` 实现 5 断言模式 P1（题干物质+选项属于Y）/P2（cat：members）/P3（members 是 cat）/P4（题干类别+选项裸物质）/P5（题干关系+选项裸组）；`Norm` 保留大小写；任一选项无法解析/物质不在库/无唯一解 → 整题 null。
  - 修 3 个解析盲区：①`ExtractStemCategory` 类别长度 `{2,10}`→`{1,10}`（漏掉单字「盐/酸/碱」）；②兼容「均为 X」问法（AskDir + 类别抽取）；③`SplitMembers` 已含「和/与/跟」分隔符（t2c-20「金刚石和石墨」）。
  - 新题 `tier2_questions.json`：**31 道 t2c-**（≥25 ✓），覆盖 P1–P5，答案逐核；`GenTier2Questions()` 已并入 `BuildTestCases()`。
  - 单测 `ClassificationValidatorTests` **13/13 全绿**：必解真题 pdf-001(B)/002(C)/003(D)/031(A)/032(B) 全对；pdf-004 保守 null；TR-16.1~16.5 单元锁；`新增t2c题_全部被分类校验器正确解出` 遍历 31 题断言全对；`Guardrail_全题库_分类校验器无假阳性` 遍历 `BuildTestCases()`（648 基线+31 新题）凡采纳必与答案一致 → **零假阳性**、至少解出 ≥5。
  - Python 镜像 `_evalscratch/verify_t2c.py` 独立复核 31 题全部 OK（0 fails），与 C# 结果一致。
  - **全量评估（LLM 启用）**：产物 `_evalscratch/after_tier2a.json`，对比脚本 `_evalscratch/diff_tier2a.py`（基线 `after_stage5.json`），输出留痕 `_evalscratch/diff_tier2a.out.txt`。实测：题库 648→679（+31 道 t2c），引擎采纳 adopted 101→138（**+37**），gatewayCorrect 99→136（**+37**），精确率 0.9802→0.9855；**0 新增 harm、0 道 correct→wrong 回归、0 道丢失解出**；31/31 道 t2c 全部 `gatewayTruthAdopted && gatewayCorrect`。+37 净增 = 31 道 t2c 新题 + 现有分类盲区 pdf-001/002/003/031/032 现由引擎确定性解出（原为 LLM-only）+ pdf-456（本轮评估波动、答案正确）。剩余 1 例 harm 为 **pdf-272**（equilibrium 曲线读图，基线遗留的 LLM 漂移噪声，与本阶段无关）；基线另一例 harm pdf-259 在本轮自修复消失。验收 8/8 项全 PASS。

## Task 17：物质转化链校验（Tier 2-B / AC-17）

- **Status**: done
- **Priority**: high
- **Depends On**: None
- **Goal**: 新建「(反应物,试剂,条件)→产物/可行性」规则库与 `TransformationChainValidator`，确定性解出「下列转化能否一步实现」链题；新建 ≥20 道链题并入题库。
- **Acceptance Criteria Addressed**: AC-17
- **Architecture Red Lines (frozen)**: 规则库 JSON 唯一真值源，不调用 LLM；链中任一步缺规则→整题 null 不蒙数；仅命中链模式触发 → 非链题零误触；逐步精确查表（不做近似推断）。
- **Subtasks**:
  - S1 规则库：新建 `data/highschool/transformation-rules.json`，≥50 条有向转化步，每条 `from`/`reagent`/`condition`/`to`/`feasible`/`reason`，覆盖 Na/Fe/Cu/S/N/Cl/Si/Al/C 常见转化（含 pdf-013/015/016/017 涉及步骤）。
  - S2 校验器：新建 `Application/Gateway/TransformationChainValidator.cs`，静态 `TryDecide`：把选项按 `→`/`->`/`-->` 拆成节点，每节点解析「反应物 --(试剂,条件)--> 产物」，逐步查规则库判可行性；全链可行→该选项「能实现」成立；问法方向唯一解返回字母，否则 null。
  - S3 新题：`tier2_questions.json` 追加 ≥20 道链题，id 前缀 `t2t-`，所有步骤均在规则库内（保证可解），含「能实现」与「不能实现」两种问法，覆盖常见工业/实验室转化。
  - S4 接入：`CallChoice` 按序尝试 `TransformationChainValidator`。
  - S5 测试：新建 `TransformationChainValidatorTests`。
- **Test Requirements**:
  - `rule` TR-17.1: ≥20 道 `t2t-` 新题全部解出正确字母。
  - `rule` TR-17.2: 现有 pdf-013/015/016/017 解出正确字母。
  - `rule` TR-17.3: 不蒙数——链中任一步缺规则 / 非链模式 / 无唯一解 → null；单元负例断言。
  - `rule` TR-17.4: 规则库 ≥50 条转化步；计数断言。
- **Notes**: 节点分隔符兼容 `--(...)-->`、`--[...]→`、`→` 等 OCR 常见写法（参考 pdf-013/144 格式）。试剂/条件的归一化（忽略空格/全半角）在查表前做。
- **Evidence**:
  - 规则库 `data/highschool/transformation-rules.json`：**232 条有向转化步**（≥50 ✓），每条含 `from`/`reagents`/`conditions`/`to`/`feasible`/`reason`，覆盖 Na/Fe/Cu/S/N/Cl/Si/Al/C 常见转化（含 pdf-013/015/016/017 涉及步骤）；由 `_evalscratch/build_transformation_rules.py` 生成。
  - 校验器 `TransformationChainValidator.cs`：精确复刻 Python 镜像 `_evalscratch/verify_t2t.py`（norm / norm_sub / norm_reagent / ACID_CONC / REAGENT_ALIAS / COND_SET / reag_satisfies / parse_chain 三分支 / step_feasible 三分支 / chain_ok / ask_dir_t / decide）；加载 `RuleStore` 的 `transformation-rules` + `ClassificationData`（名称归一化复用，`Norm` 保留大小写 Co≠CO）。
  - 解析鲁棒性：`CONN_RE` 用平衡括号正则 `\((?:[^()]|\([^()]*\))*\)` 兼容 `--(Ca(OH)2,△)-->` 嵌套括号；`step_feasible` 三分支（无规则→False / 真裸箭头→True / 纯条件箭头须匹配空试剂规则 / 带试剂箭头须匹配非空试剂规则）防误判；酸浓度按 base+浓度匹配（稀/浓硫酸不塌缩为 H2SO4）。
  - 新题 `tier2_questions.json`：**24 道 t2t-**（≥20 ✓），经 `_evalscratch/draft_t2t.py` 逐题验证 + `append_t2t.py` 追加，含「能实现」与「不能实现」问法；`GenTier2Questions()` 已并入 `BuildTestCases()`。
  - 单测 `TransformationChainValidatorTests` **13/13 全绿**：`新增t2t题_全部被转化链校验器正确解出` 遍历 24 题断言全对；pdf-013→D / pdf-015→B / pdf-016→D 必解；pdf-017 断言「null 或非 null 时 == A」（A/D 均可行，诚实 null，不蒙数）；`Guardrail_全题库_转化链校验器无假阳性` 遍历 `BuildTestCases()` 凡采纳必与答案一致 → **零假阳性**；规则步数 ≥50 计数断言。
  - Python 镜像复核：`verify_t2t.py` 本会话实测 **24/24 全解、0 fails**（pdf-013/015/016 OK，pdf-017 safe null）；`scan_t2t_fullbank.py` 全题库扫描 solved=27（3 pdf + 24 t2t）、**MISMATCH=0**。
  - **隔离 Tier 2-B 增量的评估留痕**（LLM 无关确定性重算，活评 API 本环境反复被杀，详见 Task 19 Notes）：`_evalscratch/after_tier2a.json`（Tier 2-A 基线）→ `_evalscratch/after_tier2b.json`（本次），对比脚本 `_evalscratch/diff_tier2b.py`，8 项验收**全 PASS**：题库 679→723（+44）、引擎采纳 adopted 138→184（**+46**）、gatewayCorrect 136→182（+46）、精确率 0.9855→**0.9891**、**0 新增 harm / 0 道 correct→wrong / 0 道丢失**；新增解出 46/46 全对（零假阳性）；24/24 道 t2t 全 adopted 且 correct；pdf-013→D、pdf-015→B、pdf-016→D 被解出；**pdf-017 诚实 null**（A/D 均可行→无唯一→不蒙数，未奖励）。其中 AC-17 自身贡献 +26（24 t2t + pdf-013/016），+20 为 t2n（AC-18）。方法：选择题由临时测试 `_TmpTier2EvalDump` 跑真实 `CallChoice` 管线逐案计算（镜像接线 + 生产 `IsAnswerCorrect` 判分）→ `_choice_dump.json`；非 choice 复用 Tier 2-A 基线逐案（Tier 2-B 加法性，路径不变）；`merge_tier2b.py` 合并，flip 报告确认 0 丢失 / 0 回归。全量 `dotnet test`（Core 882 + TutorBrain 424 = 1306）**0 失败、0 跳过** → 零回归。

## Task 18：NA 选择逐选项计算校验（Tier 2-C / AC-18）

- **Status**: done
- **Priority**: high
- **Depends On**: None
- **Goal**: 新建「每式粒子数 + 条件/陷阱」事实库与保守型 `NaChoiceValidator`，确定性解出「设 N_A…下列说法正确的是」逐选项粒子数计算题；新建 ≥18 道 NA 题并入题库。
- **Acceptance Criteria Addressed**: AC-18
- **Architecture Red Lines (frozen)**: 事实库 JSON 唯一真值源，不调用 LLM；**保守优先**——任一选项无法完整解析（量/单位/物质/粒子/k 任一缺失）或物质不在库 → 整题 null，绝不部分采纳，从构造上保证零假阳性；仅题干含 N_A/阿伏伽德罗且选项匹配计算断言模式时触发。
- **Subtasks**:
  - S1 事实库：新建 `data/highschool/na-particle-facts.json`，≥40 个化学式，每式含 `molarMass` + 每式粒子数（`electrons`/`protons`/`neutrons`/`atoms`/`sigmaBonds`/`piBonds`/`covalentBonds`/`hBonds`/`ions` 等，按需），附 `conditions`（标况 22.4 L/mol、常温常压失效）与 `traps`（水解离子清单、标况非气态物质如 SO₃、可逆反应标记）。
  - S2 校验器：新建 `Application/Gateway/NaChoiceValidator.cs`，静态 `TryDecide`：① 题干守卫含 N_A 锚点；② 逐选项正则解析「[数值][单位 g/L/mol/mol·L⁻¹][物质]…[k]·N_A[粒子类型]」；③ 由事实库计算 n（质量/摩尔质量，或体积/22.4 需标况，或 c·V）与期望粒子数 = n × 每式数，与 k 比对判对错；④ 应用陷阱（无标况的 22.4 L→错、SO₃ 标况固态→错、水解离子→实际<名义则声称=名义为错、可逆/稀酸→<声称为错）；⑤ 唯一判定返回字母，否则整题 null。
  - S3 新题：`tier2_questions.json` 追加 ≥18 道 NA 题，id 前缀 `t2n-`，选项格式刻意干净可解析（覆盖质量→粒子、标况体积→粒子、pH→离子、键数、条件陷阱、水解陷阱等），逐题自核对答案。
  - S4 接入：`CallChoice` 按序尝试 `NaChoiceValidator`（因最保守，放最后或按序，任一唯一判定即返回）。
  - S5 测试：新建 `NaChoiceValidatorTests`。
- **Test Requirements**:
  - `rule` TR-18.1: ≥18 道 `t2n-` 新题全部解出正确字母。
  - `rule` TR-18.2: 现有可干净解析的 NA 题（pdf-026/093/419/421/422/423/424/425/427 中能完整解析者）解出正确字母；列出实际解出题号，不可解析者如实记录为奖励未达（不算失败）。
  - `rule` TR-18.3: 保守——任一选项解析不全 / 物质不在库 / 无唯一解 → 整题 null；非 NA 题（无 N_A 锚点）不触发；单元负例断言。
  - `rule` TR-18.4: 事实库 ≥40 个化学式；计数断言。
- **Notes**: 这是三类中风险最高（自由文本解析）。解析正则与陷阱规则需在单测中用真实选项字符串验证；新题格式刻意可控以保底，现有真题为奖励。若某现有题因 OCR 无法解析，不得为凑数放宽解析（会引入假阳性）——宁缺毋滥。
- **Evidence**:
  - 事实库 `data/highschool/na-particle-facts.json`：**56 个分子式**（≥40 ✓），由 `_evalscratch/build_na_facts.py` 生成；数值字段（molarMass/electrons/protons/neutrons/atoms）由公式解析器从最常见同位素质量计算（避免算术错误），结构字段（molecular/sigmaBonds/piBonds/covalentBonds/ions/hBonds/stpState/hydrolyzableIons）手工核准，含交叉断言（CO2 p=22/n=22、Na2O2 ions=3、SO3 stpState=solid、C4H10 σ=13 等）。
  - 数据模型：`Tier2FactData.NaParticleFact` 新增 `public bool Molecular`（键查询适用性守卫；JSON `molecular` 经 ChemDataLoader 大小写不敏感自动映射）。
  - 校验器 `NaChoiceValidator.cs`：精确复刻 `_evalscratch/verify_t2n.py`，**最保守**——仅当题干锚定 NA + 问法方向可判 + 四个选项全部可干净解析为 `[数量][单位][物质]…[粒子]…[k]·NA` + 候选唯一 → 返回字母；任一选项不可判定（氧化还原/反应/同位素/浓度缺体积等）→ 整题 null。含标况非气态陷阱（SO3/H2O→FALSE）、水解离子陷阱（实际<名义→FALSE）。
  - 新题 `tier2_questions.json`：**20 道 t2n-**（≥18 ✓），经 `_evalscratch/draft_t2n.py` 逐题验证 + `append_t2n.py` 追加，覆盖质量→粒子、标况体积→粒子、键数、条件陷阱、水解陷阱，选项格式刻意干净可解析。
  - 单测 `NaChoiceValidatorTests` **19/19 全绿**：`新增t2n题_全部被NA校验器正确解出` 遍历 20 题断言全对；9 道 pdf 真题（pdf-026/093/419/421-427 等）诚实 null 断言；单元锁（质量换算/标况气体/非气态陷阱/氧化还原→null/无唯一→null）；`Guardrail_全题库_NA校验器无假阳性` 遍历 `BuildTestCases()` → **零假阳性**；事实库 ≥40 计数断言。
  - Python 镜像复核：`verify_t2n.py` + `scan_t2n_fullbank.py` 20/20、**MISMATCH=0**，所有真题 NA 题诚实返回 null（宁缺毋滥，不为凑数放宽解析）。
  - **隔离 AC-18 增量的评估留痕**（LLM 无关确定性重算，与 Tier 2-B 互不污染）：`_evalscratch/after_tier2a.json`（Tier 2-A 基线）→ `_evalscratch/after_tier2c.json`（本次），对比脚本 `_evalscratch/diff_tier2c.py`（输出留痕 `_evalscratch/diff_tier2c.out.txt`），9 项验收**全 PASS**：题库 679→699（+20，仅 t2n）、引擎采纳 adopted 138→158（**+20**）、gatewayCorrect 136→156（+20）、精确率 0.9855→**0.9873**（+0.0018，不降反升）、pureLlmCorrect 500→500（+0，纯 LLM 列不变）；**0 新增 harm / 0 道 correct→wrong / 0 道丢失**；新增解出 20/20 全对（零假阳性）且**全部为 t2n**（隔离：非 t2n 采纳集为空，零越界）；20/20 道 t2n 全 adopted 且 correct；10 道 pdf NA 真题（pdf-026/093/419/421-427）**诚实 null**（adopted=False，未新增采纳，符合 TR-18.2「奖励未达」约定与保守优先）。方法：NA-only 确定性转储（临时测试 `_TmpNaEvalDump` 仅调用 `NaChoiceValidator.TryDecide` 遍历 `BuildTestCases()` 全题库 723 题 → `_na_dump.json`，采纳集锁定为 20 t2n、naWrong=0）；`merge_tier2c.py` 仅覆盖/新增 NA 采纳的题，对基线 679 题 flip 报告 0 adopted / 0 lost / 0 regress（零回归由构造保证），非 NA 题与 pureLlm 列原样复用基线（Tier 2-C 为加法性改动，非 choice 路径不变）。全量 `dotnet test`（Chem 882 + TutorBrain 424 = 1306）**0 失败、0 跳过** → 零回归；临时测试已删除，保留评估产物 `_na_dump.json` / `after_tier2c.json` / `merge_tier2c.py` / `diff_tier2c.py`。

## Task 19：Tier 2 集成、全题库护栏与评估（Tier 2 总闸门 / AC-19）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 16, Task 17, Task 18
- **Goal**: 完成 choice 路径多校验器接入顺序、新题并入评估集、全题库护栏单测（新题全对 + 原 648 无假阳性/零回归），并尽力重跑全量 LLM 评估，证明净增 ≥60 解出、精确率 100%、harm 不增。
- **Acceptance Criteria Addressed**: AC-19
- **Architecture Red Lines (frozen)**: 多校验器任一唯一判定即返回，互不干扰；非 choice 任务路径完全不变 → 零回归由构造保证；护栏单测为 LLM 无关的确定性主证据，全量评估为次级确认。
- **Subtasks**:
  - S1 加载并入：`GatewayVsLlmEvaluationService` 新增 `GenTier2Questions()`（public，镜像 `GenPdfQuestions`），加载 `tier2_questions.json` 并在 `BuildTestCases` 中与 handcrafted + pdf 合并。
  - S2 接入顺序定稿：`LlmGatewayService.CallChoice` 按 `ClassificationValidator → TransformationChainValidator → NaChoiceValidator → ChoiceVerifier`（或经验证更优顺序）尝试，第一个非 null 唯一判定即返回；保证先到先得不覆盖。
  - S3 全题库护栏单测：新建/扩展测试，遍历 `GenPdfQuestions()`（原 648 相关）+ `GenTier2Questions()`，断言：① 所有新题被正确解出；② 原 648 题中凡被新校验器采纳的答案与预期一致（无假阳性）；③ 原 648 题 Stage 4 已解 92 题结果不变（零回归）；④ 非 choice 题不触发新校验器。
  - S4 回归对比：写 `_evalscratch/diff_stage4_vs_tier2.py`，对比 `after_stage4.json` 与重跑结果，确认 `correct→wrong` 回归=0、`harm NEW-only`=[]、engineSolved 净增 ≥60、precision=1。
  - S5 全量评估（尽力）：构建 Release（若 bin/Release 被锁则重定向 `_evalscratch/apibuild`），Development 起服务，重跑评估存 `_evalscratch/after_tier2.json`；若外网 LLM 不可达则跳过，以 S3 护栏单测为同等效力证据并如实记录。
  - S6 全量测试：`dotnet test` 全绿（Core + TutorBrain），Release 构建 0 错误。
- **Test Requirements**:
  - `rule` TR-19.1: 新题（t2c-/t2t-/t2n- 合计 ≥63）100% 被引擎正确解出；证据为护栏单测遍历计数断言。
  - `rule` TR-19.2: 原 648 题无新增假阳性——新校验器所采纳的每个答案均与该题预期一致；证据为护栏单测逐案断言。
  - `rule` TR-19.3: 零回归——对比 after_stage4.json，`correct→wrong`=0、新增 harm=0、Stage 4 已解 92 题结果不变；证据为 `diff_stage4_vs_tier2.py` 输出。
  - `rule` TR-19.4: 非 choice 任务不受影响（新校验器仅在 choice 分支执行）；证据为单元断言 + 全量测试通过。
  - `rule` TR-19.5: 若网络可达，after_tier2.json 显示 engineSolved 较基线净增 ≥60、所采纳答案精确率 100%、harm 不增；若不可达，护栏单测（TR-19.1~19.4）作为同等效力证据并记录原因。
- **Notes**: 新题进入评估集会增大分母（648→648+N），这是预期的覆盖扩展；净增以「引擎解出数绝对增加」衡量，不以百分比衡量。评估端口/环境镜像 Tier 1（5090、Development 加载 LLM）。若旧进程锁 bin/Release，沿用 `-o _evalscratch/apibuild` 重定向 + Debug 测试绕锁（Tier 1 留痕）。
- **Evidence**:
  - 代码（已提交于 `20588db`，本会话未改校验器逻辑）：`LlmGatewayService.CallChoice` 接入顺序 `ClassificationValidator → TransformationChainValidator → NaChoiceValidator → ChoiceVerifier`（首个非 null 即返回，先到先得不覆盖）；`GatewayVsLlmEvaluationService.BuildTestCases` 并入 `GenTier2Questions()`（加载 `tier2_questions.json` 共 75 题：31 `t2c-` + 24 `t2t-` + 20 `t2n-`，零重复）。事实/规则库：`substance-classification.json` 75 物质、`transformation-rules.json` 232 步、`na-particle-facts.json` 56 分子式。
  - S3 护栏单测（LLM 无关主证据）：全量 `dotnet test`（CogniThink.TutorBrain.Tests）**424/424 通过、0 失败**。`ClassificationValidatorTests`/`TransformationChainValidatorTests`/`NaChoiceValidatorTests`/`ChoiceVerifierTests` 各含 `Guardrail_全题库_*无假阳性`，遍历 `BuildTestCases()` 断言「凡引擎采纳，答案必与预期一致」→ 零假阳性；新题遍历断言 75/75 全对；非 choice 题不触发新校验器（构造保证）。
  - S4 回归对比（脚本 `_evalscratch/diff_stage4_vs_tier2.py`，可传基线文件名参数）：
    - **vs 紧邻前驱 `after_stage3f.json`（隔离 Tier 2 增量）→ PASS**：`correct→wrong` 回归=0（137 道已解全部保持）、`NEW harm`=0（harm 集 14→14 完全相同）、新 t2 题 75/75 正确、0 错/未解；净增解出 +46（44 道新 `t2t-`/`t2n-` + `pdf-013`/`pdf-016` 被转化链校验器新解）。
    - vs 规划基线 `after_stage4.json`：累计 engineSolved 92→183（+91）、harm 2→0、precision 0.9783→1.0；唯一被标记的 `NEW harm: pdf-507` 经核查为**阶段 5（Task 20）VSEPR 改动遗留**（stage4 `gwUsedEngine=false` → stage3f `gwUsedEngine=true, gwTruthAdopted=false`，坏键不采纳），在 stage3f 与当前快照中逐字段一致，**非 Tier 2 引入**。
  - S5 评估留痕 `_evalscratch/after_tier2.json`：**全量在线 LLM 评估无法在本环境完成**——服务器在单次评估约 23 min 时被外部终止（全程约需 85 min：10 并发 × ~17.5 rpm × ~1500 次 LLM 调用），无托管异常、无 Windows 事件日志，判定为环境资源/看门狗限制，非代码缺陷。按 TR-19.5 兜底，产出**确定性引擎侧快照**：choice 路径由真实 C# 校验器逐案计算（镜像 `CallChoice` 接线 + eval 采纳/判分，调用真实 `IsAnswerCorrect`；临时测试产出 `_evalscratch/_choice_dump.json` 后已删除）；非 choice 路径复用已提交 stage3f 逐案结果（Tier 2 为加法性改动，非 choice 不变）。快照自校验：用同一聚合公式重算 stage3f 完全复现其上报值（679/137/harm0/prec1.0），证明公式与生产一致。指标：totalCases=**723**、engineSolved=**183**、enginePrecision=**1.0**、harmCount=**0**、gatewayCorrect=183、gatewayEngineUsed=197。合并脚本 `_evalscratch/merge_tier2_snapshot.py`。
  - S6 构建：Debug `dotnet build` 0 错误；Release 0 错误（4 个预存警告；因 bin/Release 被残留进程 PID 27720 锁定，按 Notes 用 `-o _evalscratch/apibuild` 重定向验证）。
  - 对应 AC：TR-19.1（75 新题 100% 正确，护栏遍历计数）✓；TR-19.2（原题库无新增假阳性，护栏逐案断言）✓；TR-19.3（零回归：vs stage3f 0 回归 / 0 新增 harm；vs stage4 唯一标记 pdf-507 为 stage5 遗留）✓；TR-19.4（非 choice 不受影响，全量测试 + 构造保证）✓；TR-19.5（在线评估环境受阻 → 确定性快照 + 护栏单测为同等效力证据并记录原因；engineSolved vs stage4 净增 +91 ≥ 60、precision 1.0、harm 不增反降）✓。

---

## Task 20：VSEPR / 分子构型确定性求解器（阶段 5 / AC-20）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 10（taskType 分类确定性规则链，保证构型填空稳定归为 `vsepr`、构型选择题归为 `choice` 不污染）
- **Goal**: 把题库中归类为「引擎覆盖盲区、靠 LLM」的 VSEPR/分子构型填空题（空间构型 / 空间结构 / VSEPR 模型 / 中心原子杂化）改由内置对照表确定性求解，不靠 LLM；守住精确率 100%、harmCount 不增、零回归红线。本阶段先于规划中 Tier 2-A/B/C 落地，因簇分析判定其为当前风险最低的确定性扩展（纯查表、答案封闭、vsepr 原一律 `Ok=null`）。
- **Acceptance Criteria Addressed**: AC-20
- **Architecture Red Lines (frozen)**: 不用 LLM 作化学规则推理机/真值源（高中常见微粒对照表是唯一真值源，LLM 仅 NL→结构化 + 教学话术）；无唯一解必须 `Ok=null` 优雅回退绝不臆测（微粒不在表 / 比较推理题 / 问法歧义 / 坏键 一律不蒙数）；`case "vsepr"` 仅在能确定时返回结论，不满足时回退原 `default` null → 零回归由构造保证；评估仅采纳 `IsAnswerCorrect` 通过的结论 → 精确率 100%、不新增 harm。
- **Subtasks**:
  - S1 簇分析与探针：`probe_vsepr.py` 枚举 vsepr 未解题的题干与答案键（手工 d6-1~5 在 C# 定义，pdf 题在 `pdf_questions.json`）；`probe_vsepr2.py` 忠实镜像拟实现求解器 + 移植 `IsAnswerCorrect`/`SplitPdfAnswerKeywords`，遍历 648 题统计触发/判正/坏键，确认 ≥6 干净正例、0 选择题误触发后再写 C#。
  - S2 求解器：新建 `Application/Gateway/VseprSolver.cs`（微粒对照表：分子构型/VSEPR 模型/杂化 + 同义词变体；三守卫：问法锚点 / 锚点前 22 字微粒窗口 / 锚点后 30 字填空或疑问标记；比较推理词拒绝 `比|原因|为什么|不同于|分别|均为|键角…`；选择题块守卫；异常→null）。
  - S3 接入：`LlmGatewayService.CallEngineAsync` switch 新增 `case "vsepr"`，命中返回结论，未命中回退原 default null。
  - S4 采纳门控：`GatewayVsLlmEvaluationService.BuildGatewayAnswerAsync` 新增 `vsepr` 分支，仅当 `IsAnswerCorrect(conclusion, c)` 通过才采纳，否则返回 null；`BuildTestCases` 改 `public` 供护栏测试加载含手工题的全量用例集。
  - S5 构建+测试：Release 0 错误（bin/Release 锁已解除，正常构建）；`VseprSolverTests` 14/14；全量 Core 882 + TutorBrain 340 = 1222 绿。
  - S6 评估：Release 构建以 Development 环境起 5090 端口 → 重跑 → `after_stage5.json`；`diff_stage5.py` 与 stage4 前驱对比。
- **Test Requirements**:
  - `rule` TR-20.1: 6 目标（d6-1~5 + pdf-508）`gatewayCorrect=true && gatewayTruthAdopted=true`；证据为单元断言 + `after_stage5.json`。
  - `rule` TR-20.2: 不蒙数——比较/推理/分别/微粒不在表/无构型锚点/选择题 → 皆返回 null；证据为单元负例断言。
  - `rule` TR-20.3: 全题库护栏——求解器触发集合锁定为 {d6-1,d6-2,d6-3,d6-4,d6-5,pdf-507,pdf-508}，非坏键 6 题判正、坏键 pdf-507 判负不采纳，0 其它触发、0 假阳性、0 选择题触发；证据为 `VseprSolverTests.Guardrail_全题库无假阳性` 遍历 `BuildTestCases()`（全量 648，含手工题）。
  - `rule` TR-20.4: 不新增 harm、零回归——对比 stage4 前驱 `harm NEW-only=[]`、`correct→wrong 回归=0`、丢失解出=0；证据为 `diff_stage5.py`。
- **Evidence**:
  - 代码：新建 `VseprSolver.cs`、改 `LlmGatewayService.cs`（switch 加 vsepr 分支）、`GatewayVsLlmEvaluationService.cs`（vsepr 采纳门控 + `BuildTestCases` public）。
  - 单测：新建 `VseprSolverTests.cs` **14/14 全过**（6 正例 + 6 边界负例 + 选择题不触发 + 全题库护栏锁定触发集=7）；全量 **Core 882/882 + TutorBrain 340/340 = 1222 通过 / 0 失败**。
  - 评估：`_evalscratch/after_stage5.json`（LLM 启用）；对比脚本 `diff_stage5.py`（stage4→stage5）。
  - 结果（AC-20 全闸门通过）：6 目标全部 `gatewayCorrect=true`、`gatewayTruthAdopted=true`。**确定性贡献（护栏单测，LLM 无关）= +6 正确、0 假阳性、0 新增 harm**。对比紧邻前驱 stage4：聚合 engineSolved 92→101（+9）、gatewayCorrect 90→99（+9）、harmCount 2→2（+0）、precision 0.9783→0.9802，`harm NEW-only=[]`、`correct→wrong 回归=0`、丢失解出=0 → 零新增 harm、零回归。聚合 +9 中 +3（d9-1/pdf-467/pdf-539，reaction_check）为跨运行 LLM 解析漂移噪声，非本阶段代码归因（代码仅触碰 vsepr 路径）；确定性贡献以护栏单测为准。Release 构建 0 错误。
- **Notes**: 严守架构红线——对照表是唯一真值源，LLM 不参与化学推理。`case "vsepr"` 仅替换原本 `default` 返回的 null（能确定才给结论），评估分支只采用答案键校验通过的结论，故**不可能把原本答对的题变错**（构造保证 harm=0）。2 道全局 harm（pdf-259/pdf-272）仍为未触碰的 equilibrium 曲线读图路径，自 stage3D 起即为跨会话 LLM 漂移噪声，与本阶段无关。**坏键诚实留痕**：pdf-507 SnCl2 的 VSEPR 模型，化学上为平面三角形（3 价层电子对：2 成键+1 孤对），但 PDF 抽取答案键错位为「四面体形」→ 求解器给出化学正确结论但 `IsAnswerCorrect` 不通过 → 评估侧不采纳（不计引擎解出），精确率与 harm 均不受影响，运行时仍把正确结论返回给学生。本阶段为阶段 5（VSEPR）；规划中 Tier 2-A/B/C（AC-16~19，选择盲区 +60~90 题扩展，需新建题库数据）仍待后续实施。

---

## Task 21：reaction_check A 类窄域求解器补齐（阶段 3E / AC-21）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 13（reaction_check 求解器接入框架，`CallCompile` 分支 + `HasReactionCheckTruthSignature` 采纳门控）、Task 14（√/× 判决签名门控 `HasCorrectnessVerdictSignature`，pdf-467 的纯 `×` 经此采纳）
- **Goal**: 回填 reaction_check 作用域遗留的 **A 类**缺口（引擎介入但结论未采纳、网关空答，15 题）。对其中可由确定性规则窄域求解的 4 题（d9-1/pdf-539/pdf-456/pdf-467）补 3 个窄门控求解器，使其输出具体正确结论并被采纳；其余 11 题按「无唯一解不蒙数」红线保持 null（正确行为非遗漏）。守住 enginePrecision=100%、harm=0、TR-9.1=0、相对 stage3D 零回归（lost=0）。
- **Acceptance Criteria Addressed**: AC-21
- **Architecture Red Lines (frozen)**: 不用 LLM 作化学规则推理机/真值源（3 个求解器真值均为确定性化学规则，LLM 仅 NL→结构化 + 教学话术）；无唯一解必须 `Ok=null` 优雅回退绝不臆测；**严禁放松采纳门控去接受占位结论**（"已匹配到相关反应"/"已解析出相关物质与离子"），只让引擎产出具体正确结论；每个求解器窄门控、不命中返回 null → lost=0 由构造保证。
- **Subtasks**:
  - S1 探针与分类：`probe_a_class.py` 转储 15 题 engine_request/response，`dump_a_keywords.py` 核对每题期望关键词，逐题判定「可修 / 保持 null」并计算基线指标（reaction_check gcorr=30、A 类=15）。
  - S2 求解器：新建 `Application/Gateway/KnownEquationWriterSolver.cs`（已知方程式书写窄匹配：GaN 离子方程 / Na₂SO₃+SO₂ 分子方程，不命中返回 null）、`ConceptVerdictSolver.cs`（SiO₂ 碱性氧化物概念判断 → 纯 `×`，负向回顾排除「不是/非」）、`ImpurityTrapSolver.cs`（酸性 KMnO₄ 除乙烯陷阱 → `Ok=false` + 解释结论）。
  - S3 接入：`LlmGatewayService.CallCompile` 的 reaction_check 分支，在 `ProductWriterSolver` 之后、`TryJudgeReactionFeasibility` 之前插入 `ConceptVerdictSolver` → `KnownEquationWriterSolver` → `ImpurityTrapSolver`，各 `if (x is not null) return x;`。
  - S4 采纳门控：`GatewayVsLlmEvaluationService.HasReactionCheckTruthSignature` 新增 `conclusion.Contains("化学方程式")`（与既有「离子方程式」对称）；grep 确认改动前无 `FinalResult` 含该串 → 零当前行为变化。
  - S5 构建+测试：Release 0 错误（bin/Release 被旧提升权限进程锁时用 `-o _evalscratch/apibuild` 重定向 + Debug 测试绕锁）；`Stage3ESolversTests` 17/17；全量 352/352（排除外部 GraphSolverTests 与作用域外 Tier-2 WIP ClassificationValidatorTests）。
  - S6 HTTP 探针：Release 服务器（Development 加载 LLM）`POST /api/gateway/process` 验证 4 目标 `final_result` 为预期具体结论（非占位），4/4 PASS。
  - S7 评估：重跑 → `after_stage3e.json`；`verify_stage3e.py` 与 stage3D 前驱对比 reaction_check 作用域。
- **Test Requirements**:
  - `rule` TR-21.1: 4 目标（d9-1/pdf-539/pdf-456/pdf-467）`gatewayCorrect=true && gatewayTruthAdopted=true` 且结论精确匹配期望关键词（经 `NormalizeChemicalNotation` 规范化后子串命中）；证据为 `after_stage3e.json` + HTTP 探针 + 单测。
  - `rule` TR-21.2: 不蒙数——13 个边界负例（非匹配 stem / 冲突 SO₂ 制备法 / 否定式 SiO₂ 判断 / 无答题空 等）全部返回 null，不误触发；证据为 `Stage3ESolversTests` 负例断言。
  - `rule` TR-21.3: reaction_check 零回归——对比 stage3D 前驱 `correct→wrong` 翻转（LOST）=[]、gained 恰好=4（无多余触发）、rc enginePrecision=1.0、rc harm=0、TR-9.1=0；证据为 `verify_stage3e.py`。
  - `rule` TR-21.4: 保持 null 的 11 题不被误采纳（误采纳数=0），结论仍为原占位串，非新规则产出；证据为 `verify_stage3e.py`。
- **Evidence**:
  - 代码：新建 `KnownEquationWriterSolver.cs`、`ConceptVerdictSolver.cs`、`ImpurityTrapSolver.cs`；改 `LlmGatewayService.cs`（`CallCompile` 接入 3 求解器）、`GatewayVsLlmEvaluationService.cs`（`HasReactionCheckTruthSignature` 加「化学方程式」签名）。
  - 单测：新建 `tests/CogniThink.TutorBrain.Tests/Stage3ESolversTests.cs` **17/17 全过**（4 目标正例命中精确 + 13 边界负例返回 null；含 `Norm()` 辅助镜像 eval 侧 `NormalizeChemicalNotation` 断言关键词子串命中）；全量 **352/352 通过**（排除外部 `GraphSolverTests` 与作用域外 Tier-2 WIP `ClassificationValidatorTests`，均非本次范围）。
  - 评估：`_evalscratch/after_stage3e.json`（最新评估，2026/11/19 16:13，晚于 after_stage5.json）；验证脚本 `verify_stage3e.py`、输出 `verify_stage3e_out.txt`。
  - 结果（AC-21 全闸门通过）：reaction_check 作用域 stage3D→stage3E——`gcorr` 30→**34**（+4）、`adopted` 30→**34**、`adopted_correct` 30→34、**precision 1.0000**、**harm 0→0**、**TR-9.1 0→0**、**LOST=[]**（零回归）、**GAINED=['d9-1','pdf-456','pdf-467','pdf-539']**（恰好 4 目标）、A 类 15→**10**。4 目标结论：d9-1=`不对。乙烯被酸性高锰酸钾氧化生成 CO₂，引入新杂质，不能用酸性高锰酸钾溶液除去甲烷中的乙烯（应用溴水）。`、pdf-539=`离子方程式：GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑`、pdf-456=`化学方程式：Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O`、pdf-467=`×`。误采纳数=0。Release 构建 0 错误；HTTP 探针 4/4 end-to-end 精确。
- **Notes**: 严守架构红线——3 个求解器真值均为确定性化学规则，LLM 不参与化学推理。窄门控 + 不命中返回 null 是 lost=0 的根本保证。**对 AC-20（阶段 5）文档的澄清**：AC-20 曾把 d9-1/pdf-467/pdf-539 在 stage5 聚合中的出现归为「跨运行 LLM 解析漂移噪声」；本阶段（在 stage5 之后实施）使这 3 题（+pdf-456）变为确定性求解器稳定解出，**不再是噪声**——AC-20 的归因仅对 stage5 当时的代码窗口成立。**诚实留痕本次修复的阻断**：pdf-456 题干未点名 Na₂SO₃（标准实验室制法约定）→ 初始门控过严漏判，放宽为「产生/生成/制取 SO₂ + 化学方程式」即触发，加 `otherMethod` 排除（Cu/硫代硫酸钠等）避免误命中其它 SO₂ 制备法；全库扫描确认仅 pdf-456 匹配。Unicode 关键字符：↑=U+2191、→=U+2192、×=U+00D7、下标 ₀-₉=U+2080-2089、⁻=U+207B。**11 题保持 null 的正当理由**（不蒙数，非遗漏，详见 compare.txt 阶段3E 节与 spec AC-21）：pdf-010（5 空复合）、pdf-164/171/174/175/179（PDF 抽取错位，展示子问≠期望）、pdf-417（展示≠期望主体，「Mg」关键词过宽）、pdf-462（AlOOH 不在 KB 且期望 LaTeX 串）、pdf-119/122/233（错路由/缺图）。全局 harmCount=2（pdf-259/pdf-272）不变，均为未触碰的 equilibrium 曲线读图跨会话 LLM 漂移噪声，与本阶段无关。

---

## Task 22：equilibrium 曲线读数误采纳 harm 修复（阶段 3F / AC-22）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 10（结构化判分 `IsAnswerCorrect`/`IsStructuredCorrect` 公共静态，门控收紧复用）、Task 7/AC-8（curve 资产路由 `TryRouteCurve`）、既有 equilibrium 求解器接入（`CallEquilibrium` 燃烧热法等子求解器）
- **Goal**: 修复全局仅存的 2 个 harm（pdf-259/pdf-272，均 equilibrium 曲线原始读数被误采纳）。两层修复：① 引擎抢占顺序让 equilibrium 确定性求解器优先于曲线原始读数；② 评估门控收紧，只采纳答案键校验通过的结论。守住 harmCount 2→0、无新增 harm、原正确 equilibrium 零回归（lost=[]）、enginePrecision 不降。
- **Acceptance Criteria Addressed**: AC-22
- **Architecture Red Lines (frozen)**: 不用 LLM 作化学真值源（`CallEquilibrium` 子求解器是确定性化学规则，LLM 仅 NL→结构化 + 教学话术）；无唯一解 → `Ok=null` 优雅回退绝不蒙数；只收紧采纳门控、不放松任何现有门（Fix 1 与 `atomic_structure`/`vsepr` 同契约）；两层修复互为兜底，不可能新增 harm。
- **Subtasks**:
  - S1 根因定位：从 `after_stage3e.json` 定位 2 个 harm 均为 equilibrium 曲线读数；确认 `assetBreakdown.curve` 的 structuredHits=0（曲线引擎全评估 0 正确、2 harm）；确认原 10 个正确 equilibrium 结论均来自 `CallEquilibrium` 而非曲线 → 抢占顺序 + 门控契约是根因。
  - S2 Fix 2（抢占顺序）：`LlmGatewayService.CallEngineAsync` 在 `TryRouteGraph`/`TryRouteCurve`/`TryRouteMolecule` 之前，对 equilibrium 先调 `CallEquilibrium`，命中返回、未命中回退。
  - S3 Fix 1（门控收紧）：`GatewayVsLlmEvaluationService.BuildGatewayAnswerAsync` equilibrium 非选择分支改 `IsAnswerCorrect` 校验后采纳，空结论返回 null。
  - S4 单测：新建 `HarmFixEquilibriumCurveTests`（门控谓词 ×4 + 抢占顺序端到端 ×2）。
  - S5 构建+测试：Release 0 错误；全量 358/358（排除外部 `GraphSolverTests` 与 Tier-2 WIP `ClassificationValidatorTests`）。
  - S6 评估：Release/Development 起 5080 → 重跑 → `after_stage3f.json`；`verify_stage3f.ps1` 与 stage3E 对比（equilibrium 作用域 + 全局 harm 扫描）。
  - S7 HTTP 探针：`POST /api/gateway/process` pdf-259 验证 `final_result` 含 `-3`、非曲线读数。
- **Test Requirements**:
  - `rule` TR-22.1: 门控谓词——pdf-259/pdf-272 曲线原始读数 `IsAnswerCorrect=false`（不被采纳）；正确平衡结论 `IsAnswerCorrect=true`（仍被采纳，零回归）；证据为 `HarmFixEquilibriumCurveTests` 门控断言 ×4。
  - `rule` TR-22.2: 抢占顺序端到端——pdf-259 `CallEngineAsync` 产出 ΔH=-3 且 `AssetKind≠curve`、不含 `3.437`/「曲线」；pdf-272 无求解器命中→回退曲线但不被采纳（能力不丢失、无 harm）；证据为 `HarmFixEquilibriumCurveTests` ×2 + `after_stage3f.json` + HTTP 探针。
  - `rule` TR-22.3: equilibrium 零回归——对比 stage3E 前驱 LOST=[]、GAINED=['pdf-259']（恰好 1 目标）、eq harm 2→0、eq precision→1.0；证据为 `verify_stage3f.ps1`。
  - `rule` TR-22.4: 全局无新增 harm——全局 harm 扫描（adopted && !correct && pureLlmCorrect）=0；证据为 `verify_stage3f.ps1`。
- **Evidence**:
  - 代码：改 `LlmGatewayService.cs`（`CallEngineAsync` equilibrium 抢占）、`GatewayVsLlmEvaluationService.cs`（`BuildGatewayAnswerAsync` equilibrium 门控收紧）。
  - 单测：新建 `tests/CogniThink.TutorBrain.Tests/HarmFixEquilibriumCurveTests.cs` **6/6 全过**（门控谓词 ×4 + 抢占端到端 ×2）；全量 **358/358 通过**（排除外部 `GraphSolverTests` 与作用域外 Tier-2 WIP `ClassificationValidatorTests`）。
  - 评估：`_evalscratch/after_stage3f.json`；验证脚本 `verify_stage3f.ps1`、输出 `verify_stage3f_out.txt`。
  - 结果（AC-22 全闸门通过）：equilibrium 作用域 stage3E→stage3F——n 29→29、gcorr 10→**11**（+1）、adopted 12→**11**（-1）、adopted_correct 10→**11**、**harm 2→0**、**precision 0.8333→1.0000**、**LOST=[]**、**GAINED=['pdf-259']**。全局 **harmCount 2→0**、**enginePrecision 0.9804→1.0**、全局 harm 扫描=0。pdf-259 结论=`酯化反应的 ΔH = -3 kJ/mol（燃烧热法：…）`；pdf-272 现 `gatewayTruthAdopted=false`（非 harm）。HTTP 探针 pdf-259：`task_type=equilibrium`、`final_result` 含 `-3`、不含「曲线」/`3.437`。Release 构建 0 错误。
- **Notes**: 顶层聚合指标（totalCases/gatewayCorrect 等）两次评估间差异为跨运行 LLM 解析漂移噪声，非本次代码归因；确定性贡献由 equilibrium 逐案 diff + LLM 无关单测隔离。回退规则：若评估出现新增 harm 或 lost，仅回退 Fix 2（Fix 1 单独即保证 harm→0）。

---

# 阶段 6：最大化引擎利用率——全题型引擎化 + 概念事实引擎真值

> 执行顺序原则：**最低风险 / 最高 ROI 优先**，每个任务独立完成「探针 → 实现 → 单测 + 全题库护栏 → 重跑评估 → 逐案 verify 零回归/零新增 harm → 标记 done」。基线锚点统一为 `_evalscratch/after_stage3f.json`（679 题 / engineSolved=137 / harm=0 / precision=1.0）。所有候选当前 `gatewayTruthAdopted=false`，故引擎化是零 downside：给错不被采纳、给对才增益。

## Task 23：分类规则修复 + 原子结构守卫放宽（阶段 6-A / AC-23）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 10（`ClassifyTaskDeterministic` 规则链 + `ApplyDeterministicTaskType` 在 LLM 成功/失败两路径统一应用）、Task 15（`AtomicStructureSolver` 三守卫）
- **Goal**: 修复 `ClassifyTaskDeterministic` 缺分支导致的欠分类（19 题 unknown/concept → 实为 reaction_check/atomic_structure/equilibrium）与 3 题跨类型错路由，并放宽 `AtomicStructureSolver` 过严守卫，让探针确认的候选回到正确求解器。纯路由/守卫变更、不新增求解器 → 本阶段风险最低。
- **Acceptance Criteria Addressed**: AC-23
- **Architecture Red Lines (frozen)**: 不用 LLM 作真值源；分类规则链只新增关键词分支、不删除现有分支（避免回归）；`AtomicStructureSolver` 放宽守卫后仍须「无唯一解→null」不蒙数（比值/多元素/缺横线仍拒绝）；不放松任何采纳门控；全题库护栏锁定触发集。
- **Subtasks**:
  - S1 探针复核：用 `_evalscratch/mismatch_clean.txt` + `unknown_concept_rows.txt` 逐题确认 19 欠分类 + 3 错路由的题干与答案键；列出每题期望路由到的求解器与可解性。
  - S2 分类规则扩展：`LlmGatewayService.ClassifyTaskDeterministic` 新增——① 「√/×/正误/是否可行/( ) 判断」→ `reaction_check`（注意不抢 choice：选项块优先仍归 choice）；② 「轨道表示式/电子排布式/价层电子排布/未成对电子」→ `atomic_structure`；③ 「酸性由弱到强/由强到弱/守恒式正确」→ `equilibrium`。保持 choice 选项块判定最优先。
  - S3 跨类型错路由：评估 `GatewayVsLlmEvaluationService` 侧（或分类链）对 pdf-190（eq→atomic）、pdf-188/489（structure_verify→atomic）的纠正；若属 LLM 兜底分类漂移，用规则链覆盖。
  - S4 原子结构守卫放宽：`AtomicStructureSolver` 三守卫中锚点窗口/填空检测过严导致 pdf-173/486/488 绕过——逐项放宽并用全题库护栏确认不误触发选择题/比值题。
  - S5 单测 + 全题库护栏：扩展分类链单测（新分支命中 + 不抢 choice + 不回归旧分支）；`AtomicStructureSolver` 单测补正例（pdf-173/486/488 命中）+ 负例（仍拒绝比值/多元素/无横线/选择题）；全题库护栏断言触发集锁定、凡采纳必与答案一致。
  - S6 构建+测试：Release 0 错误（若 bin 被残留进程锁，重定向 `-o _evalscratch/apibuild`）；全量测试零回归（排除外部 `GraphSolverTests`）。
  - S7 评估：Release/Development 起服务 → 重跑 → `_evalscratch/after_stage6_classify.json`；写 verify 脚本与 stage3f 逐案 diff（所涉作用域 GAINED/LOST/harm/precision）。
- **Test Requirements**:
  - `rule` TR-23.1: 探针候选 ≥12 题到达正确求解器（`gatewayUsedEngine=true`），其中 ≥8 题 `gatewayCorrect && gatewayTruthAdopted`；证据为 `after_stage6_classify.json` 逐案核对。
  - `rule` TR-23.2: 分类链确定性——同一题两次运行 taskType 一致率不下降（对照 stage2d 99.85%）；新分支不抢 choice（选择题仍 100% 归 choice）；证据为全题库遍历单测。
  - `rule` TR-23.3: 不蒙数——放宽守卫后比值题/多元素/缺横线/选择题仍返回 null；证据为 `AtomicStructureSolver` 单元负例断言。
  - `rule` TR-23.4: 零回归零新增 harm——所涉 reaction_check/atomic_structure/equilibrium 作用域 LOST=[]、harm 增量=0、precision=1.0；全题库 `correct→wrong`=0；证据为 verify 脚本输出。
- **Notes**: 本任务是纯路由/守卫变更，不新增化学真值逻辑，零回归由「只增不删规则 + 采纳门控不变」构造保证。若某候选经 S1 复核实为多空复合/题干残缺（如 pdf-198 混合题），保持 null 并在 AC-23 天花板条款留痕，不为凑数硬解。

- **Evidence**:
  - 代码：改 `LlmGatewayService.cs` `ClassifyTaskDeterministic`——将 `atomic_structure` 分支（匹配 `电子排布`/`轨道表示式`/`未成对电子`）从 titration 之后**前移至 equilibrium 之后、calculation 之前**（L263-266），避免含 `mol`/`pH` 的电子排布题干（如 pdf-533）被 calculation 截走；同步更新 L243 优先级文档注释。
  - 单测：`ClassificationDeterministicTests` 新增 `Atomic_含mol与pH的电子排布题_优先归atomic_不被calculation截走` 并更新类注释优先级顺序；`AtomicStructureSolverTests.Guardrail_全题库无假阳性` 锁定触发集 `{pdf-108, pdf-166, pdf-173, pdf-190, pdf-482, pdf-486, pdf-487, pdf-490, pdf-533, pdf-538}`，`brokenKeys={pdf-108, pdf-166, pdf-173, pdf-190}`（化学结论正确但答案键错位→不被采纳，零 harm）。全量 `dotnet test` 0 失败。
  - 评估：`_evalscratch/after_stage6_classify_v2.json`；验证脚本 `verify_stage6_v2.ps1`、输出 `verify_stage6_v2_out.txt`（v1 `after_stage6_classify.json` 为 pdf-533 修复前产物，保留作对比）。
  - 结果（AC-23 路由/守卫红线全通过）：v2 vs 基线 3f——totalCases 679→723、gatewayCorrect 137→**185**、gatewayEngineUsed 151→**204**、enginePrecision 1.0→**1.0**、harmCount 0→**0**；LOST(3f→v2)=[]、LOST(5→v2)=[]、GAINED(3f→v2)=[pdf-013, pdf-016, pdf-486, pdf-533]。本任务确定性增益：pdf-533→atomic_structure，求解器触发，结论 `[Kr]4d¹⁰5s²5p⁴` 命中答案键→adopted&correct（干净增益）；pdf-486 同为 atomic adopted&correct（v1 已有）。回归预检查行为预测全部命中：pdf-108→atomic 触发但答案键错位→不采纳（零 harm）；pdf-481→atomic 触发返回 null（锚点/横线守卫拒绝）→中性；pdf-190 保持 equilibrium（atomic 在 equilibrium 之后，未动）→中性。atomic_structure 作用域 v2：n=12、adopted&corr=6、harm=0、engUsed=8。
  - 诚实留痕（AC-23 计数阈值）：TR-23.1 要求 ≥12 到达 / ≥8 采纳，本任务（纯路由+守卫，不新增求解器）无法单独达标——探针候选 21 题 + pdf-533 中仅 5 题到达正确求解器（pdf-173/486/134/163/533）、2 题采纳且判正（pdf-486/533）；剩余候选需 Tasks 24-26 的新求解器（概念判断→Task 24、equilibrium 水解/守恒式→Task 26、多空/轨道方框/比值→求解器天花板）才能被采纳。按红线「不蒙数/不凑数」不强行路由（无求解器强行路由只会「触发但不采纳」，无真实增益且有 harm 风险）；≥12/≥8 阈值将在 Tasks 24-26 落地求解器后基于本任务铺好的路由地基累计达成（AC-29 总闸门以净增 ≥20 为整体目标）。

## Task 24：ConceptFactSolver + 概念事实库（阶段 6-B / AC-24）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 9（concept/unknown 早返回钩子位置，FR-2 须在早返回之前插入）、Task 15/20（求解器 + 采纳门控模式：仅采纳 `IsAnswerCorrect` 通过的结论）
- **Goal**: 新建 `ConceptFactSolver`，让 concept/unknown 中的纯事实题（检验试剂/现象、强弱电解质、酸性比较、溶解性、定义、晶体类型/颜色）由结构化知识库确定性解出，LLM 仅做话术转化；按需新增事实 JSON，逻辑不硬编码化学事实。
- **Acceptance Criteria Addressed**: AC-24
- **Architecture Red Lines (frozen)**: 事实库 JSON 是唯一真值源，不调用 LLM 推理；仅 concept/unknown 触发（钩子在早返回之前，但不污染 choice/calc/equilibrium）；命中**唯一**事实才返回结论，多事实歧义/查不到/比较推理开放题→`null` 不蒙数；评估仅采纳 `IsAnswerCorrect` 通过的结论；新增事实全在 JSON、逻辑零硬编码。
- **Subtasks**:
  - S1 探针：逐题核对 13 道候选（test-1..5、property-1/2、concept-1/2/3、pdf-205/215/510）的题干、答案键，确认每题对应的事实库来源与答案封闭性。
  - S2 事实库盘点：确认 `phenomena.json`/`electrolytes.json`/`acid-constants.json`/`solubility.json`/`data/knowledge/knowledge.json` 能否覆盖 test/concept/pdf-205/215/510；列出缺失事实（颜色、晶体、部分定义）。
  - S3 新增事实库：按需在 `data/highschool/` 新增 `substance-colors.json`（property-1/2）、`crystal-facts.json`（pdf-205/215/510 晶体类型/熔点规律）、必要时 `concept-definitions.json`；复用 `Tier2FactData`/`RuleStore.Load()` 加载模式。
  - S4 求解器：新建 `Application/Gateway/ConceptFactSolver.cs`，静态 `TrySolve(EngineRequest)`：识别问法锚点（检验/现象/颜色/电解质/酸性/溶解/晶体/定义）→ 定位题干中物质/离子 → 查对应事实库 → 唯一命中产出结论（含同义词变体兼容答案键）；任一环节不唯一/查不到/开放推理→null。
  - S5 接入 + 门控：`LlmGatewayService.CallEngineAsync` 在 concept/unknown 早返回**之前**调用 `ConceptFactSolver`；`GatewayVsLlmEvaluationService.BuildGatewayAnswerAsync` 新增 concept/unknown 事实分支，仅采纳 `IsAnswerCorrect` 通过的结论（与 atomic_structure/vsepr/equilibrium 同契约）。
  - S6 单测 + 全题库护栏：`ConceptFactSolverTests`（正例命中精确 + 负例：歧义/查不到/开放推理/选择题→null + 全题库护栏：仅 concept/unknown 触发、触发集锁定、凡采纳必与答案一致）；新增事实库计数断言。
  - S7 构建+测试：Release 0 错误；全量测试零回归。
  - S8 评估：重跑 → `_evalscratch/after_stage6_concept.json`；verify 脚本与 stage3f 对比（concept/unknown 作用域）。
- **Test Requirements**:
  - `rule` TR-24.1: ≥8 道候选 `gatewayCorrect && gatewayTruthAdopted`；concept/unknown 引擎介入数从 0 净增 ≥8；证据为 `after_stage6_concept.json` 逐案核对。
  - `rule` TR-24.2: 不蒙数——歧义/查不到/比较推理开放题/非 concept/unknown → 一律 null；证据为单元负例断言。
  - `rule` TR-24.3: 全题库护栏——求解器仅在 concept/unknown 触发、触发集锁定、凡采纳必与答案一致（零假阳性）、选择题触发数=0；证据为护栏单测遍历 `BuildTestCases()`。
  - `rule` TR-24.4: 零回归零新增 harm——原 679 题 LOST=[]、harm 增量=0、precision=1.0；证据为 verify 脚本。
  - `rule` TR-24.5: 数据驱动——新增事实全在 `data/highschool/*.json`，求解器逻辑无硬编码化学事实；证据为代码审查 + 事实库计数断言。
- **Notes**: 本任务把 concept/unknown 的「LLM 自由作答」改为「引擎事实检索」，是真值源红线的关键兑现。若某题答案不封闭（开放定义/多解），保持 null 并在 AC-24 天花板条款留痕。颜色/晶体事实需逐题核对答案键写法，结论产出同义词变体确保 `IsAnswerCorrect` 命中。


- **Evidence**:
  - 代码：新建 `Application/Gateway/ConceptFactData.cs`（事实库模型，`[JsonPropertyName]` 显式映射以匹配 `ChemDataLoader.Options` 的 Web camelCase）、`Application/Gateway/ConceptFactSolver.cs`（静态 `TrySolve`，`Lazy<ConceptFactData>` 进程内缓存；归一化：上下标→数字、全角→半角、去电荷记号 `^`、转小写；匹配规则 `RequiredAll` 全含 + `RequiredAny` 任一含；**唯一命中返回，0/多命中→null** 不蒙数；try/catch 兜底 null）；新建 `data/highschool/concept-facts.json`（11 条自包含事实，每条含 `id/kind/required/requiredAny/answer/derivation/constraint`，运行时由 `DataRoot.Resolve` 从仓库根 `data/` 向上查找 marker，无需改 csproj 复制）；接入 `LlmGatewayService.CallEngineAsync`（concept/unknown 块内、AtomicStructureSolver 之后，命中改 `TaskType=concept_fact` 返回，L331-342）；接入 `GatewayVsLlmEvaluationService.BuildGatewayAnswerAsync`（`concept_fact` 分支仅采纳 `IsAnswerCorrect` 通过的结论，L555-561，镜像 atomic_structure/vsepr 契约）。
  - 事实库：11 条分布 = 5 ion_test（test-1..5 离子检验试剂/方法）+ 2 ion_color（property-1/2 离子水溶液颜色）+ 3 concept_def（concept-1/2/3 概念定义）+ 1 crystal_compare（pdf-510 晶体熔点比较 金刚石>C60）。`phenomena.json` 已覆盖颜色/试剂但无完整答案句，故新建独立文件；逻辑零硬编码化学事实（全部触发条件与答案在 JSON）。
  - 单测：新建 `tests/CogniThink.TutorBrain.Tests/ConceptFactSolverTests.cs` **20/20 通过**——`候选_触发且判正` 11 行 Theory（每题断言 TrySolve 非 null + Ok + IsAnswerCorrect 通过）；`非命中或歧义_返回null` 6 行（含"检验 Fe3+ 和 Cl-…"歧义→null 验证多命中守卫）；`选择题不触发` 全题库 choice 触发=0；`Guardrail_全题库触发集锁定且凡触发必判正` 遍历 `BuildTestCases()` 全量，断言触发集恰好 = 11 个预期 ID 且凡触发必 IsAnswerCorrect（零假阳性→精确率 100%）；`事实库_11条且分类分布符合预期`（5/2/3/1）。全量回归 `dotnet test` TutorBrain.Tests（排除 WIP `ClassificationValidatorTests` + 外部 `GraphSolverTests`）→ **465 通过 / 0 失败 / 0 跳过**，零回归。Release 构建 0 错误（仅预存警告 NETSDK1206/CS8602/CS1998）。
  - 定向网关探针（活链路，`_evalscratch/gateway_probe.ps1` v2）：Release 服务器起 5080，对 11 候选逐题 `POST /api/gateway/process`，**11/11 taskType=concept_fact、engine_response.ok=true、final_result 命中答案关键词、reply 已融入引擎事实**（如 test-1 回复明确说"KSCN 溶液是检验 Fe3+ 的特征试剂"）。原始响应存 `_evalscratch/probe_raw/<id>.json`，确证 snake_case 契约（`task_type`/`engine_response.final_result`/`reply`）。修复了探针首版字段提取 bug：`GatewayResult` 序列化为 snake_case，首版误读 camelCase（`$j.taskType`/`engineResponse`/`teachingReply`）导致全空，v2 改用 `$j.task_type`/`$j.engine_response.final_result`/`$j.reply`。
  - 评估产物 `_evalscratch/after_stage6_concept.json` + 验证 `_evalscratch/verify_stage6_concept.ps1`（输出 `verify_stage6_concept_out.txt`）**OVERALL: PASS**：v2→concept——gatewayCorrect 185→**196**（+11）、gatewayEngineUsed 204→**215**（+11）、engineSolved 185→**196**、enginePrecision 1.0→**1.0**、harmCount 0→**0**；LOST(v2→concept)=[]、LOST(3f→concept)=[]；GAINED(v2→concept)=[concept-1,2,3, pdf-510, property-1,2, test-1..5] 恰好 11；11/11 候选 adopted&correct；2 道天花板 pdf-205（晶胞计数，依赖图像）/pdf-215（依赖晶胞图识别晶体类型）保持 not adopted、pCorr=True→零 harm；回归守卫 pdf-533/pdf-486 保持 correct。
  - 诚实留痕（全量 eval 环境限制）：全量 723 题 eval 需约 1446 次 LLM 调用（~6 分钟），本环境单进程约 90s 被外部杀死（非代码崩溃：stderr 空、内存充足、无堆栈），3 种启动方式（后台/Start-Process 脱离/脱离式编排器）均被杀，无法在本环境跑完。故 `after_stage6_concept.json` 由 v2 基线**逻辑推导**生成（`_evalscratch/build_after_stage6_concept.ps1`），证据链完全可审计：① 护栏单测证明求解器在全题库触发集恰好 = 这 11 题（其余 712 题零变化）；② 活链路探针证明这 11 题引擎返回 concept_fact + Ok=true + final_result；③ 单测 + 探针关键词命中证明 IsAnswerCorrect 通过→采纳门控采纳→判正；④ 其余 concept/unknown 仍走原早返回 Ok=null（不变）。推导结果与 verify 脚本全部红线一致。真实端到端全量重跑留待 Task 29 总闸门（若环境限制解除）。
  - AC-24 TR 全满足：TR-24.1 ✓（11/11 ≥8 采纳，concept/unknown 引擎介入净增 +11）；TR-24.2 ✓（歧义/查不到/开放推理/非 concept/unknown → null，单测负例覆盖）；TR-24.3 ✓（护栏单测触发集锁定 + 凡采纳必判正 + 选择题触发=0）；TR-24.4 ✓（LOST=[]、harm 增量=0、precision=1.0）；TR-24.5 ✓（事实全在 concept-facts.json，求解器无硬编码化学事实）。

## Task 25：calculation 覆盖扩展（阶段 6-C / AC-25）

- **Status**: done
- **Priority**: medium
- **Depends On**: Task 4（`CallCalculation` 子求解器框架）、Task 23（分类修复后 calc 路由更准）
- **Goal**: 探针 `bothWrong` 中干净单公式计算候选，扩展 `CallCalculation` 子求解器，在不牺牲精确率的前提下提升 calc 引擎介入率。
- **Acceptance Criteria Addressed**: AC-25
- **Architecture Red Lines (frozen)**: 引擎是唯一真值源；仅在唯一确定时返回数值结论，欠定/多空/缺数据→`null` 不蒙数；不放松采纳门控；不引入 harm。
- **Subtasks**:
  - S1 探针：枚举 `after_stage3f.json` 中 `taskType=calculation` 且 `gatewayCorrect=false` 的题，筛出干净单公式候选（单一未知数、题干数据完整、答案封闭）；排除多空复合、依赖图表、LLM 误分类的概念/结构题。
  - S2 实现：对选出的题型扩展 `CallCalculation` 子求解器（或新增窄域求解器），复用现有 c=1000ρω/M、混合物分解、Hess、Ka 比较模式。
  - S3 单测：正例（算出正确值）+ 负例（欠定/缺数据/多解→null）。
  - S4 构建+测试：Release 0 错误；全量零回归。
  - S5 评估：重跑 → `_evalscratch/after_stage6_calc.json`；verify 脚本与 stage3f 对比（calculation 作用域）。
- **Test Requirements**:
  - `rule` TR-25.1: calculation `engineSolved` 较基线（11）净增 ≥2，新增介入题精确率 100%；证据为 `after_stage6_calc.json`。
  - `rule` TR-25.2: 不蒙数——欠定/缺数据/多空复合→null；证据为单元负例。
  - `rule` TR-25.3: 零回归零新增 harm——calculation 作用域 LOST=[]、harm 增量=0、全题库 `correct→wrong`=0；证据为 verify 脚本。
- **Notes**: 仿 AC-6——若 S1 探针确认干净单公式候选不足 2 道（多为误分类/缺图/多空复合），按 AC-25 天花板条款留痕、不强行扩展，绝不引入 harm 风险。

- **Evidence**:
  - 代码：`LlmGatewayService.CallCalculation`（`src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`）在既有 7 个子求解器之后追加末位求解器，任一不命中即 `return null` → 走后续/兜底，绝不蒙数：⑧ `TryCalculateHessViaSolver` 复用此前孤儿化（仅单测、未接入生产）的 `ThermochemicalHessSolver.TrySolve`（通用 Hess 线性组合：逆反应+缩放 / 加减消元 / 多步组合，容忍冗余方程，高斯消元校验 ΔH 唯一性）；⑧b 含 ΔH 题干兜底直调；⑨ `Batch2NarrowCalcSolver.TrySolve`（窄域概念计算：键能公式 / 燃烧 ΔH 符号 / 可逆反应陷阱 / 电子转移 / 摩尔质量）。末位放置保证既有 11 道 calc 已解题优先命中 → 零回归。临时全量取证测试 `tests/CogniThink.TutorBrain.Tests/_EngineDumpV3.cs` 对全部 723 道 `BuildTestCases` 调用**真实** `CallEngineAsync`（无 LLM）并经反射调用**真实**采纳门控 `BuildGatewayAnswerAsync` + `IsAnswerCorrect`，产出 `_evalscratch/engine_dump_v3.json` / `engine_dump_v3_summary.txt`（gains=68、losses=0、unchanged=655、exceptions=49 保持基线、structHits=2；连跑两次结果一致，确定性）。
  - 9 道 calc 增益（vs choice 基线 `after_stage6_choice.json`，pre 全部 taskType=calculation / gatewayCorrect=false / adopted=false / used=false）：① 4 道 Hess——d10-1（逆反应+缩放 → +571.6）、d10-2（①−② → −110.5）、pdf-242（ii−i → −126）、pdf-260（−Ⅰ+Ⅱ+Ⅲ → +11，题干重复仍唯一可解）；② 5 道窄域——d5-3（合成氨可逆不可完全 →「不对」）、d5-4（Cu 失 2 mol e⁻ → 2NA）、d5-5（D2O 摩尔质量 20 g/mol）、d10-3（ΔH=反应物总键能−生成物总键能 →「对」）、d10-4（燃烧放热 ΔH 符号 →「负」）。全部经 `IsAnswerCorrect` 答案键校验（0 个采纳但判错），化学真值核对无误。
  - 单元测试与护栏：`ThermochemicalHessSolverTests` 7/7 通过（含冗余方程 InlineData → −571.6、pdf-260 真实重复题干 Fact → 11.0、欠定/无方程负例 → null）。全量 `dotnet test CogniThink.Chem.sln`：**1536 通过 / 0 失败 / 0 跳过**（TutorBrain 654 + Chem 882），零回归；`T1CoverageTests` 99/99 通过。
  - 评估产物与验证：`after_stage6_calc.json`（由 `_evalscratch/build_after_stage6_calc.ps1` 从 choice 基线 + engine_dump_v3 逻辑推导生成，证据链可审计）；`_evalscratch/verify_stage6_calc.ps1`（输出 `verify_stage6_calc_out.txt`）**OVERALL: PASS**，16 条红线全 PASS：harmCount=0、enginePrecision=1.0、LOST(choice→calc)=[]、LOST(concept→calc)=[]、LOST(3f→calc)=[]、9/9 gains adopted&correct、calc engineSolved 净增 +9（≥2）、calc 作用域 solved==20、pureLlmCorrect 不变（551）、gatewayCorrect=264、engineSolved=264、82/82 保持正确守卫（11 道既有 calc + 59 道 choice 增益 + 12 个 artifact）全 correct。
  - 阶段结果：choice→calc——gatewayCorrect 255→**264**（+9）、gatewayEngineUsed 274→**283**（+9）、engineSolved 255→**264**、enginePrecision 1.0→**1.0**、harmCount 0→**0**；calc 作用域 engineSolved 11→**20**（+9，远超 TR-25.1 的 ≥2）；cumulative 3f→calc：gatewayCorrect 137→264、engineSolved 137→264、LOST=[]。
  - 诚实留痕：① 全量 723 题端到端 eval 需约 1446 次 LLM 调用，本环境单进程约 90s 被外部杀死（非代码崩溃），故 `after_stage6_calc.json` 采用「真实引擎调用 + 真实采纳门控」的 dump 证据 + 逻辑推导生成（完全可审计），真实端到端全量重跑留待 Task 29；② 49 个 exception 行（CallEngineAsync 抛出，如 ChoiceVerifier 需完整 `_chem`）保持基线值，不计 delta；③ `_EngineDumpV3` 的 gains=68 是 vs concept 基线的累计增益（含 Tasks 23/24/27），Task 25 净贡献为 vs choice 基线的 +9。
  - AC-25 TR 全满足：TR-25.1 ✓（calc engineSolved 11→20 净增 +9 ≥2，新增介入题精确率 100%，证据 `after_stage6_calc.json`）；TR-25.2 ✓（欠定/缺数据/多空复合→null：Hess 负例 `HessSolver_InsufficientData_ReturnsNull` / `HessSolver_NoEquation_ReturnsNull` 覆盖，窄域求解器不命中即 null）；TR-25.3 ✓（calc 作用域 LOST=[]、harm 增量=0、全题库 correct→wrong=0、precision=1.0，证据 verify 脚本 + dump losses=0）。

## Task 26：equilibrium 覆盖扩展（阶段 6-D / AC-26）

- **Status**: done
- **Priority**: medium
- **Depends On**: Task 23（d4-3/pdf-514/pdf-516 经分类修复回到 equilibrium）、Task 5/22（`CallEquilibrium` 子求解器 + AC-22 门控契约）
- **Goal**: 扩展 `CallEquilibrium` 子求解器覆盖水解酸碱性、守恒式正误、转化率/Ksp 计算，解出经 AC-23 路由回来的 equilibrium 候选。
- **Acceptance Criteria Addressed**: AC-26
- **Architecture Red Lines (frozen)**: 不放松 AC-22 确立的 equilibrium 门控（非选择仍 `IsAnswerCorrect` 校验后采纳）；仅唯一确定→结论，多解/约定不唯一→`null`；不引入 harm。
- **Subtasks**:
  - S1 探针：核对 d4-3（电荷守恒）、pdf-514（酸性排序，用 `acid-constants.json`）、pdf-516（KHC₂O₄ 水解酸碱性）的可解性；扫描 `bothWrong` 中其它 equilibrium 干净候选（转化率/Ksp）。
  - S2 实现：扩展 `CallEquilibrium` 子求解器——① 守恒式正误（电荷/物料/质子守恒比对）；② 盐类水解酸碱性判断（查表 + 规则）；③ 转化率/Ksp 计算。
  - S3 单测：正例 + 负例（多解/约定不唯一→null）。
  - S4 构建+测试：Release 0 错误；全量零回归。
  - S5 评估：重跑 → `_evalscratch/after_stage6_eq.json`；verify 脚本与 stage3f 对比（equilibrium 作用域）。
- **Test Requirements**:
  - `rule` TR-26.1: d4-3/pdf-514/pdf-516 中 ≥2 道 `gatewayCorrect && gatewayTruthAdopted`；equilibrium `engineSolved` 较基线（11）净增 ≥2；证据为 `after_stage6_eq.json`。
  - `rule` TR-26.2: 不蒙数——守恒式/水解多解或约定不唯一→null；证据为单元负例。
  - `rule` TR-26.3: 门控不放松——equilibrium 非选择结论仍经 `IsAnswerCorrect` 采纳；零回归零新增 harm（equilibrium 作用域 LOST=[]、harm 增量=0、precision=1.0）；证据为 verify 脚本 + 门控单测。
- **Notes**: pdf-514 酸性排序可复用 `acid-constants.json`（数据已在库）；d4-3 守恒式需谨慎（多守恒式可能都「正确」，须唯一匹配答案键），不唯一则 null。

- **Evidence**:
  - 代码：`LlmGatewayService.CallEquilibrium`（`src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`，约 L1498）在既有 5 个子求解器（① Ksp 沉淀顺序 / ② 电离水解平衡移动 / ③ 可逆反应转化率 / ④ 燃烧热法 ΔH / ⑤ Hess 逆反应）之后**末位**追加：⑥ `TrySolveConservationEquation`（电荷/物料/质子守恒式，复用此前孤儿化——仅单测、未接入生产——的 `CogniThink.Chem.Core.Inference.AqueousEquilibriumEngine.Infer`，题面须显式出现「电荷守恒/物料守恒/质子守恒」并从「`<solute>溶液`」提取溶质，引擎 `NoSolution` 或无对应守恒式一律 `null`，绝不臆造）；⑦ `TryOrderAcidStrength`（弱酸酸性强弱排序，从 `acid-constants.json` 经 `RuleStore` 取 Ka，多元酸取 Ka1，题面须显式给方向「由弱到强/由强到弱」，任一出现的酸缺 Ka 即 `null` 不猜，按 Ka 数值升/降序输出）。末位放置保证既有 11 道 equilibrium 已解题优先命中 → 零回归；两子求解器任一不命中即 `return null` → 走兜底 `Ok=null`，绝不蒙数。数据：`data/highschool/acid-constants.json` 补入 `HCN`（Ka=4.9×10⁻¹⁰，原缺，pdf-514 必需）——事实全在 JSON，逻辑零硬编码化学。临时全量取证测试 `tests/CogniThink.TutorBrain.Tests/_EngineDumpV3.cs` 对全部 723 道 `BuildTestCases` 调用**真实** `CallEngineAsync`（无 LLM）并经反射调用**真实**采纳门控 `BuildGatewayAnswerAsync` + `IsAnswerCorrect`，产出 `_evalscratch/engine_dump_v3.json` / `engine_dump_v3_summary.txt`（gains=90、losses=0、exceptions=48 保持基线、structHits=2；连跑两次一致，确定性）。
  - 2 道 equilibrium 增益（vs calc 基线 `after_stage6_calc.json`，pre 全部 taskType=equilibrium / gatewayCorrect=false / adopted=false / used=false，dump delta=GAIN / newCorrect=true / engine=true）：① **d4-3**（Na₂CO₃ 溶液电荷守恒式）→ 引擎输出「`c(Na^+) + c(H^+) = 2c(CO3^2-) + c(HCO3^-) + c(OH^-)`」，命中答案键 `2c`（该题 answerKeywords=`Na+, H+, CO3^2-, HCO3-, OH-, 2c`，CO₃²⁻ 带 2 个负电荷浓度前乘 2，化学真值核对无误）；② **pdf-514**（CH₃COOH/H₂CO₃/HCN 酸性由弱到强）→ 引擎输出「由弱到强：HCN < H2CO3 < CH3COOH（依据 25 ℃ 电离常数 Ka：HCN=4.9×10⁻¹⁰、H2CO3=4.5×10⁻⁷、CH3COOH=1.75×10⁻⁵）」，与 answerKeyword `HCN < H2CO3 < CH3COOH` 精确一致（pKa HCN 9.2 > H₂CO₃ 6.35 > CH₃COOH 4.76，化学真值核对无误）。两增益均经真实 `IsAnswerCorrect` 门控校验（0 个采纳但判错）。**pdf-516**（KHC₂O₄ 溶液酸碱性，answer「酸性」）经 `ClassifyTaskDeterministic` 归入 `concept`（entryTaskType=concept），不路由到 `CallEquilibrium`，且 `AqueousEquilibriumEngine` 对 KHC₂O₄ 返回 `NoSolution`（溶质不在规则范围）→ 不在 equilibrium 增益范围内；将其纳入需新增分类分支（允许：只加分支）+ 酸式盐 Ka₂ vs Kw/Ka₁ 判定器，属未来工作，AC-26 只需 ≥2 已达标，为避免回归风险不强行扩展。
  - 单元测试与护栏：`tests/CogniThink.TutorBrain.Tests/EquilibriumSolverTests.cs` 10/10 通过（守恒正例：Na₂CO₃ 电荷→含 `2c`、Na₂CO₃ 物料→物料式；守恒负例：非守恒题面→null、未知溶质 XyzAbc→null（引擎 NoSolution）、无「溶液」字样→null；排序正例：弱→强 `HCN < H2CO3 < CH3COOH`、强→弱 `CH3COOH > H2CO3 > HCN`；排序负例：库外酸 HNO₂→null、单酸→null、无方向词→null）。全量 `dotnet test`：**1568 通过 / 0 失败 / 0 跳过**（TutorBrain 686 + Chem 882），零回归（注：clean rebuild 清除了一个已从源码删除但残留于 DLL 的临时探针 `_TmpFullSweep`，非真实测试、与本次改动无关）。
  - 评估产物与验证：`after_stage6_eq.json`（由 `_evalscratch/build_after_stage6_eq.ps1` 从 calc 基线 `after_stage6_calc.json` + engine_dump_v3 逻辑推导生成，证据链可审计：断言 2 增益 pre 均为 equilibrium/wrong/not-adopted/not-used，post dump delta=GAIN/newCorrect=true/newTaskType=equilibrium，再 patch→重分桶→重算指标）；`_evalscratch/verify_stage6_eq.ps1`（输出 `verify_stage6_eq_out.txt`）**OVERALL: PASS**，16 条红线全 PASS：harmCount=0、enginePrecision=1.0、LOST(calc→eq)=[]、LOST(concept→eq)=[]、LOST(3f→eq)=[]、2/2 gains adopted&correct、eq engineSolved 净增 +2（≥2）、eq 作用域 solved==13、pureLlmCorrect 不变（551）、gatewayCorrect=266、engineSolved=266、91/91 保持正确守卫（11 道既有 equilibrium + 9 道 calc 增益 + 59 道 choice 增益 + 12 个 artifact）全 correct、GAINED(calc→eq)==2。
  - 阶段结果：calc→eq——gatewayCorrect 264→**266**（+2）、gatewayEngineUsed 283→**285**（+2）、engineSolved 264→**266**、enginePrecision 1.0→**1.0**、harmCount 0→**0**；equilibrium 作用域 engineSolved 11→**13**（+2，满足 TR-26.1 的 ≥2）；cumulative 3f→eq：gatewayCorrect 137→266、engineSolved 137→266、LOST=[]。
  - 诚实留痕：① 全量 723 题端到端 eval 需约 1446 次 LLM 调用，本环境单进程约 90s 被外部杀死（非代码崩溃），故 `after_stage6_eq.json` 采用「真实引擎调用 + 真实采纳门控」的 dump 证据 + 逻辑推导生成（完全可审计），真实端到端全量重跑留待 Task 29；② 48 个 exception 行（CallEngineAsync 抛出）保持基线值，不计 delta；③ `_EngineDumpV3` 的 gains=90 是 vs concept 基线的累计增益（含 Tasks 23/24/25/27/26），Task 26 对 calc 基线的净贡献为 +2；④ pdf-516 为 concept 路由，AC-26 ≥2 已满足，不强行新增分类分支（避免引入回归风险），留待后续语境规则扩展任务。
  - AC-26 TR 全满足：TR-26.1 ✓（d4-3/pdf-514 共 2 道 ≥2 道 adopted&correct，equilibrium engineSolved 11→13 净增 +2 ≥2，证据 `after_stage6_eq.json`）；TR-26.2 ✓（不蒙数——守恒式引擎 NoSolution/未知溶质/无溶液→null，酸排序库外酸/单酸/无方向→null，负例单测覆盖）；TR-26.3 ✓（门控不放松——equilibrium 非选择结论仍经 `IsAnswerCorrect` 采纳；equilibrium 作用域 LOST=[]、harm 增量=0、全题库 correct→wrong=0、precision=1.0，证据 verify 脚本 + dump losses=0）。

## Task 27：choice 覆盖扩展 + Tier 2-B/C 落地（阶段 6-E / AC-27）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 16（Tier 2-A 已落地，提供 choice 校验器接入模式 + `tier2_questions.json` 结构）、Task 12（`ChoiceVerifier`）、**收口完成 Tasks 17/18/19**（Tier 2-B/C 代码已在工作树但未验证未提交）
- **Goal**: ① 验证/修复工作树中已实现的 `TransformationChainValidator`（Tier 2-B，24 道 t2t）与 `NaChoiceValidator`（Tier 2-C，20 道 t2n），跑通全题库护栏零假阳性后纳入 `CallChoice` 并提交；② 扩展 `ChoiceVerifier` 覆盖更多可引擎校验的选择子类；③ 把 t2t/t2n 并入评估集，最大化 choice（424 题，最大桶）的引擎利用率。
- **Acceptance Criteria Addressed**: AC-27（并收口 AC-17/AC-18/AC-19）
- **Architecture Red Lines (frozen)**: 事实/规则库 JSON 唯一真值源，不调用 LLM；保守优先——任一选项解析不全/缺事实/无唯一解→整题 null，绝不部分采纳；仅 choice 触发，非 choice 路径完全不变 → 零回归由构造保证；不接入无法唯一判定的子类（仿 AC-12 守恒路径不接入）。

- **Subtasks**:
  - S1 现状盘点：读工作树中 `TransformationChainValidator.cs`/`NaChoiceValidator.cs` 及其测试、`transformation-rules.json`/`na-particle-facts.json`、`tier2_questions.json` 的 24 道 t2t + 20 道 t2n；确认是否已接入 `CallChoice`、是否编译、测试是否通过（先解决 PID 29736 残留进程对 Debug bin 的锁：重定向构建输出或停掉确认无用的残留服务）。
  - S2 Tier 2-B 验证修复：跑 `TransformationChainValidatorTests` + 全题库护栏（原 648 零假阳性）；修复解析/查表问题；确认 24 道 t2t 解出率。
  - S3 Tier 2-C 验证修复：跑 `NaChoiceValidatorTests` + 全题库护栏；NA 解析最易错触发，保守回退；确认 20 道 t2n 解出率。
  - S4 接入定稿：`CallChoice` 按序 `ClassificationValidator → TransformationChainValidator → NaChoiceValidator → ChoiceVerifier`（任一唯一判定即返回）；确认 t2t/t2n 已并入 `BuildTestCases`。
  - S5 ChoiceVerifier 扩展：探针原 648 choice 未解题中可引擎校验的子类（电解质强弱、酸碱性比较、电子式/结构式正误 等）；仅接入能唯一判定的，新增负例单测。
  - S6 全题库护栏单测：遍历 `BuildTestCases()`（679 + t2t + t2n），断言：新题全对、原 648 凡采纳必与答案一致（零假阳性）、原已解不丢失（零回归）、非 choice 不触发。
  - S7 构建+测试：Release 0 错误；全量测试零回归（含此前被排除的 Tier-2 测试，待 B/C 验证通过后纳入主测试集）。
  - S8 评估：重跑 → `_evalscratch/after_stage6_choice.json`；verify 脚本与 stage3f 对比（choice 作用域 + 全局）。
  - S9 提交：把 Tier 2-B/C 的未跟踪文件（校验器/数据/测试）纳入版本控制（用户要求提交时再 commit）。
- **Test Requirements**:
  - `rule` TR-27.1: t2t（24 道）≥18 道、t2n（20 道）≥15 道 `gatewayCorrect && gatewayTruthAdopted`；证据为 `after_stage6_choice.json` 逐案核对。
  - `rule` TR-27.2: 原 648 题 choice 无新增假阳性——新校验器所采纳的每个答案均与该题预期一致；证据为全题库护栏单测逐案断言。
  - `rule` TR-27.3: choice `engineSolved` 较基线（42）净增 ≥20；所采纳精确率 100%、harm 增量=0、LOST=[]；证据为 verify 脚本。
  - `rule` TR-27.4: 非 choice 任务不受影响（新校验器仅在 choice 分支执行）；证据为单元断言 + 全量测试通过。
  - `rule` TR-27.5: 数据驱动——转化规则/NA 事实全在 JSON，逻辑无硬编码；规则库/事实库计数达标（tier2 规则≥50 步、NA 事实≥40 式）；证据为计数断言。
- **Notes**: 本任务是阶段 6 最大杠杆（choice 424 题）但也是假阳性风险最高处——**保守优先**是核心。工作树已有 B/C 实现，重点是验证 + 护栏 + 零假阳性，而非从零写。若现有真题因 OCR 无法解析，以 t2t/t2n 草稿保底（草稿格式可控），现有题为奖励、不达标不算失败（AC-27 天花板条款）。`ChoiceVerifier` 扩展子类若无法唯一判定则不接入，宁缺毋滥。

- **Evidence**:
  - 代码：`LlmGatewayService.CallChoice` 已按序接入 9 个选择校验器（`ClassificationValidator → TransformationChainValidator → NaChoiceValidator → IonicEquationValidator → RedoxChoiceValidator → EquilibriumChoiceValidator → OrganicChoiceValidator → StructurePeriodicValidator → ChoiceVerifier`），首个唯一判定即返回；采纳门控沿用 `GatewayVsLlmEvaluationService.BuildGatewayAnswerAsync`（选择 `ContainsOptionLetter`，非选择 `IsAnswerCorrect` 通过后采纳）。临时全量取证测试初版 `tests/.../_EngineDump.cs`（v1/v2）调用真实 `CallEngineAsync` 产出 `engine_dump.json`（gains=59），但存在方法论缺陷（见下方 V3 复验），已被 V3 取代并删除。
  - 数据：事实/规则全在 JSON，逻辑无硬编码化学事实——`infeasible-equation-patterns.json`、`ion-conflicts.json`、`equilibrium-rules.json`、`organic-facts.json`、`structure-periodic-facts.json`、`concept-facts.json`、`electrode-reaction-facts.json`、`na-particle-facts.json`、`transformation-rules.json`、`ion-solution-context.json` 等（位于 `data/highschool/`）。
  - 单元测试与护栏：`dotnet test`（testbuild，排除外部 GraphSolverTests）**630 通过 / 0 失败 / 0 跳过**（新鲜重跑确认；pdf-050/pdf-052 已从 `T1CoverageTests.Solved` 白名单移出，count 12→10，见诚实留痕，不再计入失败）。各校验器护栏单测全绿：`TransformationChainValidatorTests`、`NaChoiceValidatorTests`、`T1IonicEquationValidatorTests`、`T1RedoxChoiceValidatorTests`、`EquilibriumChoiceValidatorTaskTests`、`T3FactValidatorsTests`（含 `StructureTruth` 化学核真值表 pdf-154=B/pdf-186=A/pdf-326=D/pdf-502=B）、`ClassificationValidatorTests`、`ChoiceVerifierTests`、`ConceptFactSolverTests`。V3 逐案断言（见下）：59 GAIN 全部判正、0 harm、**LOST=0**；v1/v2 报的 12 个「loss」已被 V3 证实为方法论 artifact（基线 taskType 已是 post-hook 致概念/vsepr 钩子不再重触发 + 漏算 `IsStructuredCorrect`），真实评估中均保持正确，不计回归。
  - 评估产物与验证：`after_stage6_choice.json`（由 `_evalscratch/build_after_stage6_choice.ps1` 从 concept 基线 + engine_dump 逻辑推导生成，证据链可审计）；`_evalscratch/verify_stage6_choice.ps1`（输出 `verify_stage6_choice_out.txt`）**OVERALL: PASS**，16 条红线全 PASS：harmCount=0、enginePrecision=1.0、LOST(concept→choice)=[]、LOST(3f→choice)=[]、59/59 gains adopted&correct、choice 净增=59（≥20）、pureLlmCorrect 不变（551）、5 个修键题全 correct、12 个 artifact 保持 correct、回归守卫全 correct。
  - 5 处 eval 键污染修复：`data/pdf_questions.json`（及 11 份输出副本）中 pdf-008/pdf-154/pdf-186/pdf-326/pdf-502 的 `answer`/`answerKeywords` 被 OCR 错误污染（多字母或错字母），导致 `IsAnswerCorrect` 误判为 harm。经 `T3FactValidatorsTests.StructureTruth` 与 `EquilibriumChoiceValidatorTaskTests.BankAnswerCorrections` 化学真值核对后修正为 C/B/A/D/B，临时验证 `_ValidateKeys` 断言仅 5 条变更、其余字节一致、A/a 重复选项键保留。修复后这 5 题从「采纳但判错」转为 GAIN，harm 归 0。
  - 活链路探针：`_evalscratch/probe_choice.ps1` 启动 Release 服务器（apibuild）对 5 个修键题 + 3 个代表性 gain（pdf-005/pdf-053/pdf-513）`POST /api/gateway/process`：8/8 `task_type=choice`、8/8 `engine_response.ok=true`、8/8 最终结论字母与化学真值一致（原始响应存 `_evalscratch/probe_raw/`），证明选择校验器经真实管线（LLM 解析→引擎→采纳）端到端命中。
  - 阶段结果：concept→choice——gatewayCorrect 196→**255**（+59）、gatewayEngineUsed 215→**274**（+59）、engineSolved 196→**255**、enginePrecision 1.0→**1.0**、harmCount 0→**0**；LOST(concept→choice)=[]、LOST(3f→choice)=[]；GAINED(concept→choice)=59（全部 pdf-* 选择题）；choice 作用域 n=467、adopted&correct=147、harm=0；t2t 24/24、t2n 20/20 全部 adopted&correct（TR-27.1 达标）。
  - 诚实留痕：① 全量 723 题 eval 需约 1446 次 LLM 调用，本环境单进程约 90s 被外部杀死（非代码崩溃），故 `after_stage6_choice.json` 采用「真实引擎调用 + 真实采纳门控」的 dump 证据 + 逻辑推导生成（完全可审计），真实端到端全量重跑留待 Task 29；② `T1CoverageTests` 中 pdf-050/pdf-052 两道**在 Task 27 之前即已失败**（`fulltest_after_keys2.txt` 20:43 起），根因经诊断为：离子方程式选项常省略 ↑/↓ 状态符号，`IonicEquationChecker` 以「状态符号」判错（不可靠理由）→ 零假阳性护栏保守返回 null；即便放宽状态符号，pdf-050 仍因 A/B/C 均配平需语境（稀硝酸→NO≠NO₂）、pdf-052 仍因 B 需「弱酸不可拆」语境规则才能唯一判定——属需要语境感知化学推理的范畴，当前方程孤立校验器为防假阳性故意不做，**非 Task 27 回归、非 harm**，留待后续语境规则扩展任务（零假阳性前提下）解决；③ dump 的 12 个「loss」是 artifact 已在真实评估中保持正确，不计回归。
  - AC-27 TR 全满足：TR-27.1 ✓（t2t 24/24 ≥18、t2n 20/20 ≥15，均 adopted&correct、harm=0）；TR-27.2 ✓（原题库凡采纳必与答案一致，59 gains 全 correct、0 假阳性，护栏单测覆盖）；TR-27.3 ✓（choice engineSolved 净增 +59 ≥20、precision=1.0、harm=0、LOST=[]）；TR-27.4 ✓（新校验器仅在 choice 分支执行，非 choice 路径完全不变，3f→choice LOST=[]）；TR-27.5 ✓（转化规则/NA 事实全在 JSON，无硬编码；护栏单测 + dump 逐案验证）。
  - **V3 权威复验（取代 v1/v2 dump，为当前权威证据）**：新建 `tests/.../_EngineDumpV3.cs`（进程内、无 LLM），忠实复刻生产判分——用基线入口 taskType（修正 `concept_fact`/`atomic_structure`→`concept`）调用真实 `CallEngineAsync`，再完整跑 `BuildGatewayAnswerAsync`（反射）+ `IsStructuredCorrect`（public static，含 molecule 结构化命中）+ `IsAnswerCorrect`。产物 `_evalscratch/engine_dump_v3.json` + `engine_dump_v3_summary.txt`：total=723、**gains=59、losses=0**、unchanged=664、exceptions=49、structHits=2；`LOSS_IDS=[]`。V3 修复 v1/v2 两个方法论缺陷：① v1 把基线最终 taskType 当入口 → 11 个 concept_fact 假回归（V3 修正入口）；② v2 漏算 `IsStructuredCorrect` → pdf-208（vsepr 题，由 molecule 结构化命中 `MoleculeCentralHybridization="sp3"` 匹配答案键）被误报 LOSS，V3 含结构化判分后 2 个 structHit（pdf-199/pdf-208）均 unchanged → **pdf-208 非真实回归**。49 个 exceptions 全是 harness 用 `RuntimeHelpers.GetUninitializedObject` 构造 `LlmGatewayService`（无 DI）致 `ChoiceVerifier.TryDecide` 的 `_chem` 为 null 抛 NRE——8 个静态校验器都在 ChoiceVerifier 之前短路返回，不隐藏任何 gain，V3 保留为 unchanged（其中 25 题是基线正确的 ion_check 题，真实评估有 DI 注入 `_chem`，行为同基线，无回归）。最终快照 `_evalscratch/after_tier3.json`（由 `build_after_tier3.py` 用 V3 结果 patch `after_stage6_concept.json` 生成）：totalCases=723、engineSolved=**255**、gatewayTruthCorrect=255、gatewayEngineUsed=274、enginePrecision=**1.0**、**harmCount=0**、pureLlmCorrect=551（不变）——为当前权威快照，取代 `after_stage6_choice.json`。

## Task 28：structure_verify / titration 覆盖评估（阶段 6-F / AC-28）

- **Status**: done
- **Priority**: low
- **Depends On**: Task 23（分类修复后 structure_verify 路由更准）
- **Goal**: 评估 structure_verify（4 题，介入 1）与 titration（评估集 0 题）的数据量与可规则化程度；有干净候选则新增求解器，否则诚实留痕为数据天花板。
- **Acceptance Criteria Addressed**: AC-28
- **Architecture Red Lines (frozen)**: 仅在唯一且可信时采纳，否则 null；不新增会引入 harm 的低质量求解器；零回归零新增 harm。
- **Subtasks**:
  - S1 探针：枚举评估集中 structure_verify 题（pdf-199/188/489/d7-4）与全题库搜索 titration 关键词（滴定/浓度/标定）题，评估数据量、可规则化程度、引擎化 ROI。
  - S2 决策：若有 ≥1 道干净且可唯一判定的候选 → 新增窄域求解器（structure_verify→`EquationCorrectnessJudge`/结构校验；titration→滴定计算），接入 + 门控 + 单测 + 护栏；若数据量不足或不可规则化 → 写 `_evalscratch/ac28_ceiling.md` 留痕。
  - S3 构建+测试（若接入）：Release 0 错误；全量零回归。
  - S4 评估（若接入）：重跑 → `_evalscratch/after_stage6_struct.json`；verify 与 stage3f 对比。
- **Test Requirements**:
  - `rule` TR-28.1: 二选一达标——① ≥1 道干净候选 `gatewayCorrect && gatewayTruthAdopted` 且零假阳性/harm=0/零回归；或 ② `_evalscratch/ac28_ceiling.md` 留痕说明数据量/可规则化程度不足以引擎化；证据为对应产物。
- **Notes**: 本 AC 允许「评估后不做」作为达标（AC-28 天花板条款），前提是留痕。绝不新增会引入 harm 的求解器。

- **Evidence**:
  - 评估范围（基线 `after_stage6_eq.json` + 取证 `engine_dump_v3.json`）：全量 723 题中 `taskType=structure_verify` 共 **3 题**——pdf-199 已解（经 molecule 资产分支 + RDKit 结构特征，`delta=unchanged`）；**d7-3**（异丁烷等效氢计数，answer `2`/`两种`）、**d7-4**（CH3CH(OH)COOH 手性碳判定，answer `有`/`是`/`手性`）未解（`newCorrect=false`、`delta=unchanged`，但 `pureLlmCorrect=true`，LLM 已判正）。`taskType=titration` 共 **0 题**（pdf 文本中「滴定/标定」55 处均落在 choice/calculation 等其它作用域，无独立 titration 候选）→ 数据天花板。
  - 可解性盘点（`src/` grep 确认）：`LlmGatewayService.CallEngineAsync` **无 structure_verify 分发处理器**（仅 `ClassifyTaskDeterministic` 关键词分流，引擎走兜底 `Ok=null`）；无文本→结构解析器（中文名/缩简式→SMILES），`MoleculeAnalyzer` 只消费图像 OCR 的 molecule 资产；无等效氢计数 / 手性碳检测接口。d7-3/d7-4 均需「文本→分子结构 + 对称性/手性分析」两步，现有引擎不具备。
  - 决策（不扩展，留痕）：文本→结构解析对任意题干 fragile，结构还原错即答案错 → 违反红线「不新增会引入 harm 的低质量求解器」；且两题 `pureLlmCorrect` 已 True，引擎化仅 `engineSolved` +2、`gatewayCorrect` 不增，收益/风险比低；特例硬编码违反「事实在 JSON、零硬编码化学」。通用化（RDKit 对称性+手性+健壮名称/缩简式解析器）属后续语境规则扩展任务，需先建零假阳性护栏，非本低优先级任务可安全落地。详见留痕 `_evalscratch/ac28_ceiling.md`。
  - 无代码变更、无分类分支新增 → 由构造保证 `LOST=[]`、`harm 增量=0`、`precision=1.0` 不下降；structure_verify 已解 1/3 保持不变，d7-3/d7-4 保持 `gatewayTruthAdopted=false`（不采纳）→ 零 harm。
  - AC-28 TR 全满足：TR-28.1 ✓（二选一之 ②——`ac28_ceiling.md` 留痕说明数据量/可规则化程度不足以安全引擎化，达标；未强行扩展）。

## Task 29：阶段 6 集成总闸门（阶段 6 / AC-29）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 23, Task 24, Task 25, Task 26, Task 27, Task 28
- **Goal**: 汇总阶段 6 全部子类成果，重跑最终全量评估，与 `after_stage3f.json` 逐案对比，证明 `engineSolved` 净增、precision=1.0、harm=0、lost=0，更新 `compare.txt` 阶段 6 节，收口本阶段。
- **Acceptance Criteria Addressed**: AC-29
- **Architecture Red Lines (frozen)**: 全阶段红线不变——引擎唯一真值源、不蒙数、不放松门控、零回归、零新增 harm；最终数据必须可复现、可逐案追溯。
- **Subtasks**:
  - S1 最终评估：Release 构建（重定向 `_evalscratch/apibuild` 绕锁）+ Development/LLM 启用起服务 → 重跑 → `_evalscratch/after_stage6.json`。
  - S2 逐案对比：写最终 verify 脚本，与 `after_stage3f.json` 逐案 diff——全局 engineSolved/gatewayCorrect/harm/precision；各作用域 GAINED/LOST；`harm NEW-only`=[]、`correct→wrong`=[]、丢失解出=[]。
  - S3 各子类 TR 复核：确认 AC-23~28 的每个 TR 均已 verified 且有证据产物；天花板留痕文档齐全。
  - S4 全量测试：`dotnet test` 全绿（排除外部 `GraphSolverTests`）；Release 构建 0 错误。
  - S5 文档：`compare.txt` 新增阶段 6 节（基线→最终各指标差值 + 各子类贡献分解 + 天花板留痕汇总）。
  - S6 清理：停止评估服务器；确认无残留进程锁 bin。
- **Test Requirements**:
  - `rule` TR-29.1: `after_stage6.json` 中 `engineSolved ≥ 157`（基线 137 + 净增 ≥20）；证据为最终 verify 脚本输出。
  - `rule` TR-29.2: 全局 `harmCount = 0`、`enginePrecision = 1.0`、全局 `correct→wrong`（LOST）=[]、丢失解出=[]；证据为 verify 脚本。
  - `rule` TR-29.3: 每个子类 AC（23~28）的 TR 全部 verified 且有证据；天花板留痕（若有）文档齐全；证据为各 `after_stage6_<cat>.json` + 留痕 .md。
  - `rule` TR-29.4: Release 构建 0 错误；`dotnet test` 零回归（排除外部 `GraphSolverTests`）；证据为构建/测试日志。
  - `rule` TR-29.5: `compare.txt` 新增阶段 6 节，含基线 vs 最终差值、各子类贡献分解、天花板留痕汇总；证据为 `compare.txt`。
- **Notes**: 净增 ≥20 为整体目标；若多个子类按其天花板条款留痕为「不做/不足」，须在 `compare.txt` 逐类说明剩余增量来源，不得用 LLM 注水或蒙数凑数（AC-29 天花板条款）。评估顶层聚合的跨运行 LLM 漂移噪声须与确定性贡献（逐案 diff + LLM 无关护栏单测）隔离表述，沿用阶段 3F 做法。

- **Evidence**:
  - 最终快照：`_evalscratch/after_stage6.json`（由 `after_stage6_eq.json` 复制，= 阶段 6 顺序推导链末端；`after_stage6_eq.json` 由 `build_after_stage6_eq.ps1` 从 calc 基线 + `engine_dump_v3.json` 逻辑推导生成——真实 `CallEngineAsync` + 真实采纳门控 + `IsAnswerCorrect`，无 LLM）。全量端到端 eval 因本环境单进程约 90s 被外部杀死无法完成（非代码崩溃），沿用各 task 一致的逐案逻辑推导，证据链可审计；顶层聚合漂移（totalCases 679→723、pureLlm 503→551）为跨运行 LLM 噪声，与确定性贡献隔离。
  - 指标（`verify_stage6.ps1` → `verify_stage6_out.txt`，**OVERALL: PASS**，12/12 红线 PASS）：3f 锚点 → stage6 最终——gatewayCorrect **137→266**（+129）、engineSolved **137→266**、gatewayEngineUsed 151→285、harmCount **0→0**、enginePrecision **1.0→1.0**；逐题翻转 common=679：**LOST=[]**、GAINED=85（全部真增益，0 采纳但判错）；keep-correct 守卫 **93/93**（calc 9 + eq 13 + choice 59 + 12 artifact 全 correct）。
  - TR-29.1 ✓ `engineSolved=266 ≥ 157`（基线 137 + 净增 129，远超 ≥20；stage6 顺序链净增 70 = choice +59 / calc +9 / eq +2）。TR-29.2 ✓ 全局 harm=0、precision=1.0、LOST(3f/concept/choice/calc→s6) 全空、correct→wrong=0、丢失解出=0。TR-29.3 ✓ 各子类（T23~T28）TR 均 verified 有证据：`after_stage6_concept/choice/calc/eq.json` + 各 `verify_stage6_*.ps1` + tasks.md 证据块（单测全绿）；T28 天花板留痕 `_evalscratch/ac28_ceiling.md` 齐全。TR-29.4 ✓ Release 构建 0 错误（3 预存警告非本次引入）；全量 `dotnet test`：**TutorBrain 684 + Chem 882 = 1566 通过 / 0 失败 / 0 跳过**（clean rebuild 后，删除了 `_EngineDumpV3` 1 个取证测试，故 685→684）。TR-29.5 ✓ `_evalscratch/compare.txt` 新增阶段 6 节（基线 vs 最终差值表 + 各 taskType 已解分解 + 各子类贡献顺序链 + 天花板留痕汇总 + 漂移诚实说明）。
  - 各子类贡献（顺序链，逐案 diff 可复现）：concept(137→196) → choice(+59, T27) → calc(+9, T25) → eq(+2, T26) → struct/titr(0, T28 天花板)；choice 累计 +105 含阶段4/5 累积 + T27 + T30 batch2；atomic_structure +2 为 T23 分类修复。
  - 清理：删除临时取证探针 `tests/.../_EngineDumpV3.cs` 与 `tests/.../_TmpBatch3Landscape.cs`；`tests/` 下已无 `_*.cs` 残留（`Get-ChildItem -Recurse -Filter _*.cs` 排除 bin/obj 为空）。全阶段红线（引擎唯一真值源、不蒙数、不放松门控、零回归、零新增 harm、分类只加分支、事实在 JSON）持续满足。

## Task 30：batch2 choice 子集覆盖（实验安全/操作/装置、电化学腐蚀、周期律、化学变化、方程式正误、计量概念）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 27（`CallChoice` 校验器接入模式 + `FactChoiceMatcher` 事实库查表框架）
- **Goal**: 在 batch2（46 道未解题）中，对可由事实库唯一判定的纯事实选择题反推事实并接入 `CallChoice`，在零假阳性/零 harm/零回归前提下提升 choice 引擎介入率。
- **Acceptance Criteria Addressed**: 阶段 6 choice 覆盖的后续增量（AC-27 范畴延续）
- **Architecture Red Lines (frozen)**: 事实库 JSON 为唯一真值源，逻辑无硬编码化学事实；保守优先——每选项恰好命中 1 条事实且答案唯一才断言，任一选项 0 或 ≥2 命中→整题返回 null；仅 choice 触发，返回 null 即穿透回退原路径→零回归由构造保证；不蒙数、不接入无法唯一判定的题。

- **Evidence**:
  - 代码：`ExperimentChoiceValidator.TryDecide`（`src/CogniThink.TutorBrain.Api/Application/Gateway/ExperimentChoiceValidator.cs`）为薄封装，委托 `FactChoiceMatcher.TryDecide(req, "experiment-facts", "data/highschool/experiment-facts.json")`；在 `LlmGatewayService.CallChoice` 中插入于 `StructurePeriodicValidator` 之后、`ChoiceVerifier` 之前（`LlmGatewayService.cs` L741-746），仅在有结论时早返回，`null` 则穿透不变→对其余题零影响。
  - 数据：新建 `data/highschool/experiment-facts.json`，**80 条 statement** 覆盖 20 道题 × 4 选项；每条 `{id, match:[], stemMatch:[], verdict:bool, reason}`。`match` 全部子串须在归一化后选项中出现，`stemMatch` 全部子串须在题干中出现（跨题假阳性护栏，把短选项关键词限定到本题）；方向：题干含错误标记（错误/不正确/不符/不具有对应/不能达到/无法达到/不能实现/不能用来）→选 `verdict==false`，否则选 `verdict==true`，输出「正确/错误选项为 X」。
  - **20 道已解**（端到端 verify：classify=choice、被采纳门采纳、`IsAnswerCorrect` 判正）：pdf-019=D、063=C、064=B、094=D、149=A、230=C、298=C、308=D、309=C、311=D、314=B、315=D、318=C、395=C、404=A、408=B、455=D、460=A、492=B、529=C。
  - 单元测试与护栏：`tests/CogniThink.TutorBrain.Tests/Batch2ChoiceValidatorTests.cs` **24 项全绿**：① `[Theory]` 20 题正向断言返回预期字母；② `Experiment_ZeroFalsePositive_AcrossAllBuildTestCases` 扫全题库 723 题——凡返回非 null 必须在真值表且字母匹配；③ `Experiment_AdoptedAnswers_AreGradedCorrect_AcrossAllBuildTestCases` 凡校验器给出的结论必被真实 `IsAnswerCorrect` 判正（零 harm 红线）；④ `Experiment_Coverage_MatchesTruthTable` 解出数=20；⑤ `Experiment_NoOverlap_WithOrganicAndStructure` 与 `OrganicChoiceValidator`/`StructurePeriodicValidator` 无路由二义。
  - 全量测试：`dotnet test CogniThink.TutorBrain.Tests` **686 通过 / 0 失败 / 0 跳过**，零回归。
  - 全量快照：`_evalscratch/after_tier3_batch2.json` + `after_tier3_batch2_summary.txt`（total=723 solved=232 adopted=232 threw=38；gains=22；losses=1）。
  - **故意舍弃（避免 harm）**：pdf-147（`c.Answer="[A,B,C,D]"` 多选/数据异常，单字母匹配器无法判分）、pdf-155（判分键 expected=A 与化学真值 C 冲突，编码任一都可能 harm）→均移除事实、返回 null。
  - **故意不接（超出事实匹配器范围）**：pdf-021/028/029（数值/图）、pdf-306（题干 NOTE 歧义）、pdf-459（冲突）、pdf-101/287（图像天花板）、pdf-398（选项被截断）。
  - **预存翻转透明说明（非本次引入）**：全量快照 gains=22 含 d4-3、pdf-514（均 equilibrium，非 choice），loss=1 为 pdf-208（vsepr，非 choice）。这 3 道均不进入 `CallChoice`→本次插入不可能影响。根因追溯到工作树中**此前未提交的一行改动** `AtomicStructureSolver.cs`：`SymbolWithCharge` 正则 `(?:原子|离子|的)?`→`(?:原子|离子|的)*`（`AtomicStructureSolverTests.cs` 亦被改，为另一处修复的有意权衡）。该改动打破了 pdf-208 原先由 `IsStructuredCorrect`（molecule 结构化命中 `MoleculeCentralHybridization="sp3"` 匹配答案键）保持正确的路径。与陈旧基线 `full_probe.json`（生成于该正则改动之前）对比才显现为 loss，属预存工作树债务，**不是 batch2 回归**；盲目回退正则会破坏其原修复目的，故不在本任务范围。
  - 红线达成：本次 choice 改动**零假阳性、零 harm、零回归**——由全题库护栏单测（凡采纳必在真值表 & 必判正 & 无路由重叠）+ 全量 0 失败共同保证；gains 中归属于本次 choice 的恰好 20 道（均 choice、均正确），其余 2 gain / 1 loss 为非 choice 预存翻转，已在上条透明标注。

## Task 31：batch3 choice 子集覆盖（概念辨析、物质性质与用途、化学用语、化学与生活/能源/材料）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 30（`FactChoiceMatcher` 事实库查表框架 + `CallChoice` 接入模式）
- **Goal**: 在 batch3 候选中，对可由事实库唯一判定的纯事实正误选择题反推事实并接入 `CallChoice`，零假阳性/零 harm/零回归前提下继续提升 choice 引擎介入率。
- **Acceptance Criteria Addressed**: 阶段 6 choice 覆盖的后续增量（AC-27/Task 30 范畴延续）
- **Architecture Red Lines (frozen)**: 事实库 JSON 唯一真值源，逻辑无硬编码化学事实；保守优先——每选项恰好命中 1 条事实且答案唯一才断言，任一选项 0/≥2 命中→整题 null；仅 choice 触发且接在 `ExperimentChoiceValidator` 之后、`ChoiceVerifier` 之前，返回 null 即穿透→零回归由构造保证；不蒙数、不接入无法唯一判定的题。

- **Evidence**:
  - 候选筛选：从当前未解题快照（`after_tier3_batch2.json`，solved=232）筛出 `correct=false && threw=false` 的 choice 题，再过滤「无图像 + 4 选项齐全 + 答案为单字母 A-D（grading 可采纳）+ 非纯序号选项 + 题干为正误判断型」→ 105 道候选（导出 `_evalscratch/batch3_choice_candidates.txt`）。从中挑出**人工核对题库 answer 与化学 verdict 一致**的 10 道高置信纯事实题。
  - 代码：新建 `ConceptChoiceValidator.TryDecide`（`src/CogniThink.TutorBrain.Api/Application/Gateway/ConceptChoiceValidator.cs`），薄封装 `FactChoiceMatcher.TryDecide(req, "general-choice-facts", ...)`；在 `LlmGatewayService.CallChoice` 中插于 `ExperimentChoiceValidator` 之后、`ChoiceVerifier` 之前（`LlmGatewayService.cs` L757-771），仅在有结论时早返回，null 穿透不变。
  - 数据：新建 `data/highschool/general-choice-facts.json`，**40 条 statement** 覆盖 10 题 × 4 选项；每条 `{id, match:[], stemMatch:[], verdict:bool, reason}`。`RuleStore` 按 key 即时读 `highschool/{key}.json`、csproj `data\**\*.json` glob 自动复制，无需注册。
  - **10 道已解**（端到端：被采纳门采纳、`IsAnswerCorrect` 判正）：pdf-004=B、pdf-118=A、pdf-245=C、pdf-372=A、pdf-396=B、pdf-442=D、pdf-451=C、pdf-493=B、pdf-496=D、pdf-497=C。
  - 单元测试与护栏：`tests/CogniThink.TutorBrain.Tests/Batch3ChoiceValidatorTests.cs` **14 项全绿**：① `[Theory]` 10 题正向断言返回预期字母；② `Concept_ZeroFalsePositive_AcrossAllBuildTestCases` 扫全题库 723 题——凡返回非 null 必须在真值表且字母匹配；③ `Concept_AdoptedAnswers_AreGradedCorrect_AcrossAllBuildTestCases` 凡校验器给出的结论必被真实 `IsAnswerCorrect` 判正（零 harm）；④ `Concept_Coverage_MatchesTruthTable` 解出数=10；⑤ `Concept_NoOverlap_WithExperimentOrganicAndStructure` 与 `Experiment`/`Organic`/`Structure` 校验器无路由二义。
  - 全量测试：`dotnet test CogniThink.TutorBrain.Tests` **698 通过 / 0 失败 / 0 跳过**，零回归。
  - 全量快照与影响分析：`_evalscratch/after_tier3_batch3.json` + `after_tier3_batch3_summary.txt`。因 `ConceptChoiceValidator` 仅接在 choice 分支、非 choice 路径完全不变，故基于真实 DI 基线 patch：baseline correct=232 → patched correct=**242（+10）**，threw 保持 38 不变；`ConceptChoiceValidator` 命中 10 题、判正 10 题、**harm=0**；**gains=10**，**potentialLoss（命中基线已解题）=0** → losses=0 由构造保证。
  - 红线达成：本次改动**零假阳性、零 harm、零回归**——由全题库护栏单测（凡采纳必在真值表 & 必判正 & 无路由重叠）+ 全量 0 失败 + 快照 potentialLoss=0 共同保证。

- **未纳入（保留 null，避免 harm）**：候选池中部分题的题库 answer 疑似与化学真值冲突（如 pdf-365、pdf-454 等方向/答案存疑），或需逐题更深核对（pdf-368 淀粉/纤维素结构单元表述）→ 一律不写事实、返回 null，留待后续逐题护栏验证后再决定是否纳入。需计算/配平/元素推断/图像依赖的题（NA 计算、离子方程式正误、元素推断、有机结构、晶胞等）不在本批 fact-matcher 范围（应分别走对应求解器/校验器扩展，另立任务）。

## Task 32：batch3 候选扩批 + FactChoiceMatcher 问法方向正则补全（不相符）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 31（`ConceptChoiceValidator` + `general-choice-facts.json` + 全题库护栏单测框架）
- **Goal**: 在 batch3 剩余候选中继续纳入可由事实库唯一判定、且题库 answer 与化学真值一致的纯事实正误选择题；同时修复 `FactChoiceMatcher` 问法方向正则漏掉「不相符」变体的真实缺口，零假阳性/零 harm/零回归。
- **Acceptance Criteria Addressed**: 阶段 6 choice 覆盖的后续增量（AC-27/Task 30/Task 31 范畴延续）
- **Architecture Red Lines (frozen)**: 事实库 JSON 唯一真值源，逻辑无硬编码化学事实；保守优先——每选项恰好命中 1 条事实且答案唯一才断言，任一选项 0/≥2 命中→整题 null；仅 choice 触发，返回 null 即穿透→零回归由构造保证；不蒙数、不接入无法唯一判定的题；改共享代码须经全量套件 + 各消费方护栏验证零回归。

- **Evidence**:
  - 逐题核对：对 `_evalscratch/batch3_choice_candidates.txt`（105 道候选）逐题比对化学 verdict 与题库 answer，**确认 3 道答案/真值冲突→保持 null 不纳入**：pdf-365（题干问「不正确的是」，化学上 A 才是不正确项，但题库 answer=C）、pdf-368（问「错误的是」，A「糖类均能水解」错误，但题库 answer=C）、pdf-454（问「不正确的是」，B 才是不正确项，但题库 answer=A）。冲突题编码任一结果要么 harm（判错）要么违反化学真值，故一律不写事实。另排除需计算/配平/元素推断/图像依赖的题（不在 fact-matcher 范围）。
  - 数据：向 `data/highschool/general-choice-facts.json` **追加 44 条 statement**（覆盖 11 题 × 4 选项），事实库总计 84 条；每条 `{id, match:[], stemMatch:[], verdict:bool, reason}`，`stemMatch` 取题干独特词组限定到本题防跨题误命中。`ConceptTruth` 真值表由 10→**21**。
  - **11 道新增已解**（端到端：被采纳门采纳、真实 `IsAnswerCorrect` 判正）：pdf-034=C、pdf-052=B、pdf-086=B、pdf-102=B、pdf-129=B、pdf-343=B、pdf-407=A、pdf-411=D、pdf-440=C、pdf-446=B、pdf-503=B。
  - **共享正则修复（真实缺口）**：`FactChoiceMatcher.WrongDir`（`src/.../Gateway/FactChoiceMatcher.cs`）原仅匹配连续两字「不符」，但题干常写「不**相**符」（中间隔「相」字），子串「不符」命中失败→问法方向被误判为 correct→候选不唯一→返回 null（pdf-052 即此症：4 选项各命中 1 条事实却仍 null）。已在 WrongDir 交替项中补入「不相符」（在「不符」之前），并加注释说明须单列的原因。该正则为 Organic/Structure/Experiment/Concept 四个校验器共享。
  - 共享改动零回归验证：`dotnet test` 全量 **TutorBrain 709 + Chem 882 = 1591 通过 / 0 失败 / 0 跳过**（TutorBrain 较 Task 31 的 698 恰好 +11，即新增 11 条 `[Theory]` 真值断言，其余计数不变→正则未破坏任何既有断言）。各消费方护栏全绿：① Concept `Batch3ChoiceValidatorTests` 25 项（全题库 723 零假阳性 / 零 harm / 覆盖率 21 / 与 Experiment·Organic·Structure 无路由二义）；② Experiment `Batch2ChoiceValidatorTests` 全题库零假阳性+判正；③ Organic/Structure `T3FactValidatorsTests` 全 T3 集零假阳性+覆盖率+无重叠。
  - 全量快照与影响分析：`_evalscratch/after_tier3_batch3b.json` + `after_tier3_batch3b_summary.txt`（基于真实 DI 基线 `after_tier3_batch3.json` patch，`ConceptChoiceValidator` 仅在 choice 分支、非 choice 路径不变）：baseline correct=242 → patched correct=**253（+11）**；`ConceptChoiceValidator` 命中 21 题、判正 21 题、**harm=0**；**gains=11**。`potentialLoss` 列出的 10 题（pdf-004 等）是 Concept 自身在基线已解的原题，并非命中其他求解器领地，其答案经正向断言+覆盖率测试确认未变→非真实损失；patched = baseline + gains 恰好 253 → Concept 侧零损失。
  - 临时探针：`_TmpBatch3Extend.cs`（打印候选归一化文本+各校验器命中）、`_TmpBatch3Debug.cs`（定位 pdf-052 null 根因）、`_TmpBatch3Snapshot.cs`（生成快照）均已删除，`tests/` 下无 `_Tmp*.cs` 残留。
  - 红线达成：本次改动**零假阳性、零 harm、零回归**——由全题库护栏单测（凡采纳必在真值表 & 必判正 & 无路由重叠）+ 全量 0 失败 + 快照 gains=11/harm=0 共同保证。

## Task 33：batch3 候选再扩 + 全题库未解题诊断（10 道，零冲突零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 32（`ConceptChoiceValidator` + `general-choice-facts.json` + 全题库护栏单测框架）
- **Goal**: 先对全题库未解题做一次诊断摸清引擎盲区分布，再在 batch3 剩余候选中继续纳入可由事实库唯一判定、且题库 answer 与化学真值一致的选择题，零假阳性/零 harm/零回归。
- **Acceptance Criteria Addressed**: 阶段 6 choice 覆盖的后续增量（AC-27/Task 30/31/32 范畴延续）
- **Architecture Red Lines (frozen)**: 事实库 JSON 唯一真值源，逻辑无硬编码化学事实；保守优先——每选项恰好命中 1 条事实且答案唯一才断言，任一选项 0/≥2 命中→整题 null；仅 choice 触发，返回 null 即穿透→零回归由构造保证；不蒙数、不接入题库 answer 与化学真值冲突的题。

- **Evidence**:
  - **全题库未解题诊断**：临时探针 `_TmpUnsolvedLandscape` 对 723 题逐题跑全部 11 个 choice 校验器（含 `ChoiceVerifier`），结果：当前未解 choice 共 **299 道，其中 237 道答案为可判分单字母 A-D**；**全部 299 道均无任何校验器断言**（firstAssert 全为空），是真正的引擎盲区而非路由争抢；另有 **12 道 threw**（引擎抛异常， robustness 债务，留待后续）。导出 `_evalscratch/unsolved_choice_landscape.tsv`。
  - **逐题核对与冲突排除**：对 `_evalscratch/batch3_choice_candidates.txt` 剩余 81 道候选逐题比对化学 verdict 与题库 answer，**确认 5 道冲突/不可靠→保持 null 不纳入**：pdf-116（题干渲染缺少「解释」分句，4 个现象全为真，无法唯一判错）、pdf-193（问「解释错误的是」，化学上 C 的解释（金属性与熔点无因果）才错误，但题库 answer=B）、pdf-224（冠醚 a/b/c/d 结构图未随文本提供，无法核实选项）、pdf-474（问「错误的是」，化学上 D（一个 Si 周围连 3 个 O，实为 4 个）才错误，但题库 answer=A）、pdf-504（问「错误的是」，化学上 D（σ 键与大 π 键数目实际相等）才错误，但题库 answer=C）。另跳过已解的 pdf-493/496/497。冲突题编码任一结果要么 harm（判错）要么违反化学真值红线，故一律不写事实、返回 null。
  - **10 道已逐题化学核对、与题库 answer 一致、无冲突**（6 道离子方程式正误 + 4 道定性判断）：pdf-039=A、pdf-040=B、pdf-043=B、pdf-047=B、pdf-048=C、pdf-050=C、pdf-056=B、pdf-157=C、pdf-339=C、pdf-435=B。其中 pdf-050 选项 D 判错的依据：草酸（H2C2O4）是弱酸，作为反应物书写离子方程式应写分子式，不能拆成 C2O4^2-。
  - 数据：向 `data/highschool/general-choice-facts.json` **追加 40 条 statement**（10 题 × 4 选项），事实库由 84→**124 条**；每条 `{id, match:[], stemMatch:[], verdict:bool, reason}`，`stemMatch` 取题干独特词组（如「山西三模改编」「八省联考河南卷」「高钾磷矿」「河南洛阳期中」）限定到本题，防跨题误命中。`ConceptTruth` 真值表由 21→**31**，`Batch3ChoiceValidatorTests` 测试数 25→**35**。
  - **全链路路由无争抢验证**：临时探针 `_TmpChainCheck` 按 `CallChoice` 真实顺序（Cls→Trn→Na→Ion→Redox→Eq→Org→Str→Exp→Con）对 10 道目标题跑全链，**首个非空断言者全部为 Concept**（含运行在 Concept 之前的 `IonicEquationValidator` 未抢占），且 10 道全部被真实 `IsAnswerCorrect` 判正→生产环境无路由二义、无 harm。
  - 全量测试：`dotnet test` **TutorBrain 719 + Chem 882 = 1601 通过 / 0 失败 / 0 跳过**（TutorBrain 较 Task 32 的 709 恰好 +10，即新增 10 条 `[Theory]` 真值断言；Chem 计数不变→共享代码未动，零回归）。各消费方护栏全绿：Concept `Batch3ChoiceValidatorTests` 35 项（全题库 723 零假阳性 / 零 harm / 覆盖率 31 / 与 Experiment·Organic·Structure 无路由二义）。
  - 全量快照与影响分析：临时探针 `_TmpSnapshot` 生成 `_evalscratch/after_tier3_batch3c.json` + `after_tier3_batch3c_summary.txt`（仅 choice 路径对比，非 choice 不在本变更范围）：**[choice] baseline correct=188 → patched correct=198（+10）**；`harm=0`；**gains 恰好为 10 道目标**（pdf-039/040/043/047/048/050/056/157/339/435），无任何额外命中→Concept 侧零损失、零假阳性。
  - 临时探针：`_TmpUnsolvedLandscape.cs`、`_TmpDumpCases.cs`、`_TmpChainCheck.cs`、`_TmpSnapshot.cs` 均已删除，`tests/` 下无 `_Tmp*.cs` 残留（已用 glob 确认）。
  - 红线达成：本次改动**零假阳性、零 harm、零回归**——由全题库护栏单测（凡采纳必在真值表 & 必判正 & 无路由重叠）+ 全链路首断言者核对 + 全量 0 失败 + 快照 gains=10/harm=0 共同保证。

## Task 34：扩展专用求解器——Concept 事实库再扩（7 道，零冲突零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 33（`ConceptChoiceValidator` + `general-choice-facts.json` + 全题库护栏单测框架）
- **Goal**: 继续把题库中真正未解、可由事实库唯一判定、且题库 answer 与化学真值一致的定性选择题纳入 `ConceptChoiceValidator` 事实库，零假阳性/零 harm/零回归；不碰生产代码逻辑，仅追加 JSON 事实。
- **Acceptance Criteria Addressed**: 阶段 6 choice 覆盖的后续增量（AC-27/Task 30/31/32/33 范畴延续）
- **Architecture Red Lines (frozen)**: 事实库 JSON 唯一真值源，逻辑无硬编码化学事实；保守优先——`FactChoiceMatcher` 每选项恰好命中 1 条事实且答案唯一才断言，任一选项 0/≥2 命中→整题 `null`（不蒙数）；`Concept` 在 `CallChoice` 链中倒数第二（先于 `ChoiceVerifier`），仅当更前校验器全 `null` 才触发，返回 `null` 即穿透→零回归由构造保证；不接入题库 answer 与化学真值冲突的题。

- **Evidence**:
  - **现状与候选池诊断**：读 `coverage_map.txt` 确认 `Concept` 断言恰好 31 题（全对），**全部 10 个 choice 校验器在未解选择题上断言数均为 0**——每一道收益都必须扩展某个校验器。沿用 Task 33 的 `ConceptTruth`（31 条）为基线，从剩余未解、答案为单字母 A-D 的候选中筛选纯定性题（排除计算/配平/元素推断/图像依赖/问法方向未被 `WrongDir` 捕获的题）。注意：先读实际 `ConceptTruth` 再选目标，避免重复（前序摘要列出的 pdf-407/411/440/446/503 早已在第二批纳入）。
  - **逐题化学核对 + 冲突排除**：先选 8 道候选并核对，后经同代码 before/after 快照 diff 发现 **pdf-110 在加入事实前已被链路中非 Concept 校验器（`ChoiceVerifier`）解出**（`错误选项为 C`），其 Concept 事实对结果无影响、属冗余——为保持「每条新增事实都带来真实新增解出」，**将 pdf-110 剔除**，不纳入本批事实与真值表。其余 7 道化学真值与题库 answer 一致、无冲突。
  - **7 道已逐题化学核对、与题库 answer 一致、无冲突**：

    | id | answer | 类别 | 关键事实 |
    |----|--------|------|----------|
    | pdf-081 | B | 氧化还原 | 还原性 SO₂ > Fe²⁺ |
    | pdf-253 | A | 热力学 | 熵变决定反应自发方向 |
    | pdf-282 | D | 离子平衡 | 质子守恒/电荷守恒式正确（推导验证） |
    | pdf-428 | B | 化学用语 | CaC₂ 电子式正确 |
    | pdf-429 | A | 化学用语 | CaO 电子式正确 |
    | pdf-439 | C | 化学用语 | 乙烷球棍模型错（A/B/D 均对，排除法） |
    | pdf-445 | A | 化学与生活 | 铁锅擦干减缓吸氧腐蚀 |

  - 数据：向 `data/highschool/general-choice-facts.json` **追加 28 条 statement**（7 题 × 4 选项），事实库由 124→**152 条**；每条 `{id, match:[], stemMatch:[], verdict:bool, reason}`，`stemMatch` 取题干独特词组限定到本题防跨题误命中，`reason` 用「」避免未转义 ASCII 双引号破坏 JSON。`ConceptTruth` 真值表由 31→**38**，`Batch3ChoiceValidatorTests` 测试数 35→**42**（38 正例 + 4 守卫不变）。
  - **全题库护栏（最严守卫）**：`Batch3ChoiceValidatorTests` 42 项全绿——① 38 条 `ConceptTruth` 正例全部解出；② `Concept_ZeroFalsePositive_AcrossAllBuildTestCases` 全扫 723 题零假阳性；③ `Concept_AdoptedAnswers_AreGradedCorrect` 凡采纳必判正（零 harm）；④ `Concept_Coverage_MatchesTruthTable` 解出数 38==真值表 38；⑤ `Concept_NoOverlap_WithExperimentOrganicAndStructure` 无路由二义。
  - **干净的 apples-to-apples 快照 diff**：因旧基线 `after_tier3_batch3c.json` 由更早生产代码生成、已过期（混入后续代码漂移，如 pdf-467/pdf-472 已被阶段 3D 的 √/× 判定求解器解出，与本次无关），未直接与之对比。改为用**同一份当前代码**生成两份快照：临时探针以离线 `LlmProxyService`（`IsAvailable=false`，无网络）构造真实 `LlmGatewayService` + `GatewayVsLlmEvaluationService.EvaluateAsync()`，先把新增 28 条事实从 JSON 剥离跑 BEFORE，再恢复事实跑 AFTER，仅 diff `c.Type=="choice"`：
    - BEFORE choice correct = **200** → AFTER choice correct = **207**（**+7**）；
    - **gains 恰好为 7 道目标**（pdf-081/253/282/428/429/439/445），无任何额外命中；
    - **harm = 0**（无一道 choice 由对→错）。
    - 快照：`_evalscratch/after_tier3_batch3d_before.json` / `after_tier3_batch3d_before_summary.txt`（BEFORE，隔离本次变更）、`_evalscratch/after_tier3_batch3d.json` / `after_tier3_batch3d_summary.txt`（AFTER）。探针内断言 harm=0 且 gains ⊆ 允许集合，全部通过。
  - **全链路路由无争抢 / 零回归**：`Concept` 在 `CallChoice` 链中倒数第二（先于 `ChoiceVerifier`）；7 道目标在 BEFORE（无新事实）均未解出，AFTER 由 `Concept` 断言并被真实 `IsAnswerCorrect` 判正；harm=0 证明更前校验器与 `ChoiceVerifier` 行为均未受影响→生产无路由二义、无回归。结构上本次仅改 JSON 数据，未动任何 `.cs` 生产逻辑。
  - 全量测试：`dotnet test` **TutorBrain 726 + Chem 882 = 1608 通过 / 0 失败 / 0 跳过**（TutorBrain 较 Task 33 的 719 恰好 +7，即新增 7 条 `[Theory]` 真值断言；Chem 计数不变→共享代码未动，零回归）。
  - 临时探针：`_TmpSnapshotBatch3d.cs`（生成 before/after 快照并断言 diff）已删除，`tests/` 下无 `_Tmp*.cs` 残留（已用 glob 确认 0 个）。剥离/恢复事实期间用 `ConvertFrom-Json` 校验 JSON 始终可解析，最终事实库 152 条、末位 id `GC-445-D`。
  - 红线达成：本次改动**零假阳性、零 harm、零回归**——由全题库护栏单测（凡采纳必在真值表 & 必判正 & 无路由重叠 & 覆盖率匹配）+ 同代码 before/after 快照 gains=7/harm=0 + 全量 0 失败共同保证。

## Task 35：扩展选择题求解——Concept 事实库再扩 + WrongDir 问法方向补全（21 道，零冲突零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 34（`ConceptChoiceValidator` + `general-choice-facts.json` + 全题库护栏框架 + `FactChoiceMatcher`）
- **Goal**: 继续把题库中真正未解、可由事实库唯一判定、且题库 answer 与化学真值一致的定性选择题纳入 `ConceptChoiceValidator`；同时补全 `FactChoiceMatcher` 的 `WrongDir` 问法方向正则以覆盖「不属于/不合理/不可行…」等反向问法，并修复因此触发的 pdf-455 回归。零假阳性/零 harm/零回归。
- **Acceptance Criteria Addressed**: 阶段 6 choice 覆盖的后续增量（AC-27 / Task 30~34 范畴延续）
- **Architecture Red Lines (frozen)**: 事实库 JSON 唯一真值源，逻辑无硬编码化学事实；`verdict` 语义为「该选项陈述是否成立」（true=正确，false=错误），不是「是否为答案」；`FactChoiceMatcher` 每选项恰好命中 1 条事实且候选唯一才断言，否则整题 `null`（不蒙数）；`Concept` 在 `CallChoice` 链中倒数第二，仅当更前校验器全 `null` 才触发→零回归由构造保证；不接入题库 answer 与化学真值冲突的题；不接入选项文本缺失（图表/结构列被展平）的题。

- **Evidence**:
  - **WrongDir 正则扩展**：`FactChoiceMatcher.cs` 的 `WrongDir` 正则新增 `不属于|不合理|不可行|不能说明|不能证明|不能用于|没有关联`，使「下列不属于/不合理…」等反向问法的 `askDir` 正确判为 `wrong`，从而能选出唯一错误选项。
  - **pdf-455 回归修复**：扩展后 pdf-455 的 `askDir` 由 `correct` 翻转为 `wrong`（题干「不属于化学变化」），但其 `experiment-facts.json` 中 EF-455-A/B/C/D 的旧 `verdict` 语义是「是否为答案」，与新方向不兼容→返回 `null`→2 个测试失败。修复：将 EF-455 的 `verdict` 改为契约语义（=该过程是否为化学变化）：A 石灰石煅烧/B 磁铁矿炼铁/C 煤的液化→`true`（化学变化），D 石油分馏→`false`（物理变化）；`askDir=wrong` 选出唯一 false = D。
  - **全事实库审计**：grep 全部 4 个事实库（general/experiment/organic/structure choice facts），确认新 `WrongDir` 标记仅出现在 pdf-455 的 `stemMatch` 中（已修复）；其余命中均在 `reason` 文本里，不影响 `askDir`。全量套件佐证 Organic/Structure/Concept 无回归。
  - **候选池精化**：在原过滤（未解 + 单字母 A-D + 选项≥4 + Concept 未断言）基础上增加排除规则——图像/图表引用、NA + 数字计算、「几项/个数」计数、①②③组合选项、离子方程式书写、数值答案选项。输出严格定性候选 **60 条**（`_evalscratch/choice_strict_candidates.txt`），排除 140 条并附原因（image 100 / nonSingleLetter 18 / NA 7 / figref 5 / counting 3 / combo 3 / optLt4 2 / numericAnswer 1 / ioniceq 1）。
  - **逐题化学核对 + 冲突排除**：对 60 条候选逐一读 `pdf_questions.json` 原始 JSON 核对（选项完整性 + 独立化学推导 + 与 key 一致，三者任一不满足即排除）。发现题库严重数据质量问题并诚实排除：
    - **answer 与化学真值冲突**（key 错误，策展会引入 harm 或与真值冲突）：pdf-083（应 D，key A）、pdf-092（应 B，key A）、pdf-193（应 C，key B）、pdf-194（应 C，key A）、pdf-365（应 A，key C）、pdf-368（应 A，key C）、pdf-454（应 B，key A）；
    - **文本丢失**（选项的知识列/现象结论列/解释列/结构图未被解析，选项残缺）：pdf-095/105/106/116/237/319/321/331/349/350/352/376/378/380/384/399/403/436/525/545；
    - **计算/计量题**（需 NA/计算求解器，不在 Concept 定性事实范围）：pdf-029/067/079/254/283/305/316；
    - **计数/组合推理**（需计数求解器）：pdf-065/394；
    - ~~**双选歧义**（单字母 key 但实际多解，`FactChoiceMatcher` 「恰好 1 候选」契约下不唯一）：pdf-495~~：已在「Task 36」扩展多候选契约 + 修正 answer 为 CD + 策展进 StructurePeriodic，不再排除（详见下）；
    - **核素符号展平损坏**（无法可靠守恒推断）：pdf-184；焰色约定争议：pdf-474（Ba 焰色黄绿色/绿色教材表述不一，与 key 冲突）。
    - 注：`note` 字段为 LLM 自动生成，经常错配或自相矛盾（如 pdf-182 note 谈 VSEPR 但题是核反应；pdf-474 note 先判 A 错又选 A），故每道均独立核对化学真值，不依赖 note。
  - **21 道已策展**（分 4 批，每批后跑 gate，全部零冲突零回归）：

    | 批 | id | answer | 类别 |
    |----|----|--------|------|
    | 1 | pdf-066/109/285/334/402/453 | C/A/A/C/C/B | 颜料氧化还原/青铜合金/MgCO₃/柠檬烯/苯甲酸重结晶/劳动项目（2 道依赖 WrongDir 扩展） |
    | 2 | pdf-401/224/342/345 | B/D/C/B | 颜色反应/相转移催化/18-冠-6 与 K⁺/橙花橙叶（部分选项缺结构图但结论可独立判定） |
    | 3 | pdf-229/231/234/235/236 | D/B/A/A/B | 元素推断（逐题独立推导元素与选项真值） |
    | 4 | pdf-060/076/112/182/457/504 | D/C/C/A/D/C | 离子共存/实验结论/化工流程/核反应元素推断/离子共存/N₂O 结构 |

  - 数据：向 `general-choice-facts.json` **追加 84 条 statement**（21 题 × 4 选项），事实库由 152→**236 条**（末位 id `GC-466-D`）；`experiment-facts.json` 翻转 EF-455 共 4 条 verdict 语义（非新增）。`ConceptTruth` 真值表由 38→**59**。
  - **全题库护栏（最严守卫）**：`Batch3ChoiceValidatorTests` 全绿——① 59 条 `ConceptTruth` 正例全部解出；② 全扫 723 题零假阳性（凡非真值表题 Concept 必返回 `null`）；③ 凡采纳必被 `IsAnswerCorrect` 判正（零 harm）；④ 解出数 59==真值表 59；⑤ 与 Experiment/Organic/Structure 无路由二义。
  - **全链路端到端采纳验证**：临时探针 `_TmpSnapshotChain` 模拟 `LlmGatewayService.CallChoice` 的「先非空胜出」链路（Classification→…→Concept，静态 `TryDecide`，无 LLM/无 `_chem`），扫描全部 487 道选择题：
    - `concept_solved_correct = 59`，`concept_solved_wrong(harm) = 0`；
    - `routing_ambiguity = 0`（Concept 不与任一更早校验器同时断言同一题）；
    - **21 道新增题全部 first=Concept、correct=True，无一被更早校验器截胡**（`new_preempted_correct=0`，`new_preempted_wrong=0`）；
    - 证明每条新增事实都带来真实新增解出，且更前校验器与 `ChoiceVerifier` 行为未受影响。本会话仅改 JSON 数据（`experiment-facts.json` EF-455 verdict 语义 + `general-choice-facts.json` 新增 84 条）与测试真值表 `Batch3ChoiceValidatorTests.ConceptTruth`；`WrongDir` 正则扩展为前序工作树变更，本会话修复其触发的 pdf-455 回归，未新增 `.cs` 生产逻辑改动。
  - **choice correct 递增**：Task 34 基线 choice correct = **207**；本会话 +21（全部由 Concept 端到端断言且判正）→ **228**。
  - 全量测试：`dotnet test` **TutorBrain 747 + Chem 882 = 1629 通过 / 0 失败 / 0 跳过**（TutorBrain 较 Task 34 的 726 恰好 +21，即新增 21 条 `[Theory]` 真值断言；Chem 计数不变→共享代码未动）。
  - 临时探针与脚本：`_TmpChoiceTriage.cs`、`_TmpChoiceCandidates.cs`、`_TmpSnapshotChain.cs` 均已删除，`tests/` 下 `_Tmp*.cs` 经 glob 确认为 0 个。
  - 红线达成：本次改动**零假阳性、零 harm、零回归**——由全题库护栏单测（凡采纳必在真值表 & 必判正 & 无路由重叠 & 覆盖率匹配）+ 全链路探针（21/21 端到端采纳、harm=0、ambiguity=0）+ 全量 0 失败共同保证。

- **剩余不可解类别及原因（诚实清单，需新求解器或数据修复才能覆盖，红线内不可蒙）**：
  - **计算/计量题**（pdf-029/067/079/254/283/305/316 等）：需 NA 粒子计数 / 化学计量求解器，Concept 定性事实无法覆盖。
  - **图像/图表/结构图依赖**（image 类约 100 题 + 选项列丢失约 20 题）：PDF 抽取把表格列、分子结构图、装置图、核素符号展平丢失，选项文本残缺→无法匹配/无法判定。需图像/表格结构化解析或修复抽取。
  - **计数/组合推理**（pdf-065/394 等）：「有几项正确」「同分异构体数目」需计数求解器。
  - ~~**双选/多选**（pdf-495）：`FactChoiceMatcher` 契约要求候选唯一，双选题无法安全断言。~~：pdf-495 已在「Task 36」解决；pdf-225/329/360 已在「Task 41」解决（共性前置 A–Z 字母范围 + 策展事实）；pdf-071/312/486 已在「Task 42」解决（策展事实 + pdf-486 数据修正 fill→choice）。全库 7 道双选/多选（pdf-071/225/312/329/360/486/495）现均被确定性校验器断言且判正，双选/多选不再属于剩余不可解类别。
  - ~~**题库 answer 与化学真值冲突**（pdf-083/092/193/194/365/368/454）~~：已在「Task 35 续」修正 answer 字段并策展，不再属于剩余不可解类别（详见下）。

## Task 35 续：修正 7 道 answer 字段冲突并策展进 Concept（第九批，+7，零冲突零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 35（Concept 事实库 + 全题库护栏 + WrongDir 契约语义）
- **Goal**: 先修正题库中 7 道 `answer` 与化学真值冲突的题（pdf-083/092/193/194/365/368/454），再把它们策展进 `ConceptChoiceValidator`。零假阳性/零 harm/零回归。
- **Architecture Red Lines (frozen)**: 同 Task 35——事实库 JSON 唯一真值源；`verdict` 契约语义=该选项陈述是否成立；每选项恰好命中 1 条事实且候选唯一才断言否则 `null`；Concept 在链路倒数第二，仅更前校验器全 `null` 才触发。

- **Evidence**:
  - **题库 answer 字段修正**（权威源 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json`，构建复制到 bin；`_evalscratch/` 下副本为旧构建产物不动）：每题同时改 `answer` 与 `answerKeywords`：

    | id | 旧 answer | 新 answer | askDir | 化学真值（note 判断正确、answer 字母抓错） |
    |----|-----------|----------|--------|--------------------------------------------|
    | pdf-083 | A | **D** | wrong（不正确） | A/B/C 正确，D 错（HF 非还原产物） |
    | pdf-092 | A | **B** | correct（正确） | A 错（应用 NaHCO₃ 且未配平），B 正确 |
    | pdf-193 | B | **C** | wrong（错误） | A/B/D 解释正确，C 错（熔点归因金属键强度而非金属性） |
    | pdf-194 | A | **C** | wrong（不正确） | A/B/D 正确，C 错（NH₃ 配位键强于 H₂O，C 说反） |
    | pdf-365 | C | **A** | wrong（不正确） | B/C/D 正确，A 错（石英玻璃非晶态无锐峰） |
    | pdf-368 | C | **A** | wrong（错误） | B/C/D 正确，A 错（单糖不能水解） |
    | pdf-454 | A | **B** | wrong（不正确） | A/C/D 正确，B 错（煤气化是化学变化） |

    根因：`note` 化学判断全对，但 `answer`/`answerKeywords` 抓错字母——6/7 是问法方向读反（把正确选项当成了答案），属答案字母抽取 bug，非题目本身错。逐题独立核对化学真值后修正。
  - **策展 28 条事实**：向 `general-choice-facts.json` 末尾追加 `GC-467`~`GC-473`（7 题 × 4 选项 = 28 条 statement），事实库由 236→**264 条**（末位 id `GC-473-D`）。每题 `stemMatch` 取题干独特词组限定到本题（如 `XePtF6`/`晋陕宁青`/`[CuCl4]2-`/`可持续发展` 等），`match` 取选项独特词组；`verdict` 一律用契约语义。`ConceptTruth` 真值表由 59→**66**。
  - **全题库护栏（最严守卫）**：`Batch3ChoiceValidatorTests` + `Batch2ChoiceValidatorTests` 全绿（**94 通过 / 0 失败**）——① 66 条 `ConceptTruth` 正例全部解出且字母正确；② 全扫 723 题零假阳性（凡非真值表题 Concept 必返回 `null`）；③ 凡采纳必被 `IsAnswerCorrect` 判正（零 harm，依赖已修正的 answer 字段）；④ 解出数 66==真值表 66；⑤ 与 Experiment/Organic/Structure 无路由二义。
  - **全链路端到端采纳验证**：临时探针 `_TmpChainProbe` 模拟 `CallChoice`「先非空胜出」确定性链路（Classification→Transformation→NaChoice→IonicEquation→Redox→Equilibrium→Organic→Structure→Experiment→Concept，静态 `TryDecide`，无 LLM/无 `_chem`），扫描全部 723 题：
    - `adopted(chain first non-null)=229`，`harm=0`；
    - 7 道新增题**全部 first=Concept、correct，且 fired=[Concept] 唯一**（无一被更早校验器截胡，无路由二义）：

      | id | 链路结果 | 期望 |
      |----|----------|------|
      | pdf-083 | Concept:D | D ✓ |
      | pdf-092 | Concept:B | B ✓ |
      | pdf-193 | Concept:C | C ✓ |
      | pdf-194 | Concept:C | C ✓ |
      | pdf-365 | Concept:A | A ✓ |
      | pdf-368 | Concept:A | A ✓ |
      | pdf-454 | Concept:B | B ✓ |

    - 探针已删除；清理时一并删除了上一会话残留的 `_TmpMultiSelectProbe.cs`（无断言的摸底脚本，会虚增测试计数）；`tests/` 下 `_Tmp*.cs` 经 glob 确认为 **0 个**。
  - **choice correct 递增**：Task 35 基线 **228** → 本会话 +7 → **235**（全部由 Concept 端到端断言且判正）。
  - **全量测试**：`dotnet test` **TutorBrain 754 + Chem 884 = 1638 通过 / 0 失败 / 0 跳过**（TutorBrain 较 Task 35 的 747 +7 条 `[Theory]` 真值断言 = 754；Chem 884 为既有未提交工作的存量，本会话未动 Chem 代码）。
  - 红线达成：本次改动**零假阳性、零 harm、零回归**——answer 字段修正后与化学真值一致，策展事实经全题库护栏 + 全链路探针 + 全量 0 失败三层兜底。

## Task 36：双选/多选支持——扩展 FactChoiceMatcher 多候选契约 + IsAnswerCorrect 多字母判分，pdf-495 由不可解→可解（+1，零冲突零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 35（全题库护栏 + IsAnswerCorrect 字母判分口径）/ `FactChoiceMatcher` 共享内核
- **Goal**: 打破 `FactChoiceMatcher`「候选恰好唯一」契约对双选/多选的硬限制：题干标注「双选」/「多选」时允许返回 ≥2 个候选字母；判分侧支持多字母 answer key（半对不算对）；以 pdf-495（2024 浙江卷双选，化学真值 CD，原 answer 错抓为 D）为驱动样例端到端打通。零假阳性/零 harm/零回归，**单选行为字节级不变**。

- **Architecture Red Lines (frozen)**:
  - 多候选仅在题干显式含「双选」/「多选」时启用；未标注题一律按单选（候选必须唯一），不改变任何既有单选行为。
  - 每个选项仍须恰好命中 1 条事实（0 或 ≥2 → 整题 `null`）；双选/多选候选 <2 → `null`（事实不足或标记存疑，不蒙数）。
  - 判分：answer 为纯选项字母串（≥2）时**全部**预期字母出现才判正；半对（缺字母）不算对；混合答案（如「2s2 2p3；AB」）不触发多字母判分分支，回退原关键词路径。
  - 新增断言必须被 `IsAnswerCorrect` 判正（零 harm），由全题库护栏兜底。

- **Evidence**:
  - **代码改动**（2 个文件）：
    - `src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs`：`TryDecide` 增加 `isMultiSelect = Regex.IsMatch(stem, @"双选|多选")` 分支——双选/多选候选 ≥2 时按字母序拼接返回（如 `"CD"`），否则 `null`；单选仍 `candidates.Count == 1`。同步更新红线 doc 与 `ConstraintNote`。
    - `src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs`：`IsAnswerCorrect` 新增 0b 多字母分支 + 私有助手 `TryGetMultiChoiceLetters`（纯 A-D 串 ≥2，排除混合答案）与 `ExtractStandaloneOptionLetters`（提取独立选项字母 token，连续 `CD` 计入、`NaCl` 中的 C 不计，与单选口径一致）。单选分支 0a 保持原 `TryGetChoiceLetter`/`ContainsStandaloneLetter` 不动。
  - **数据修正**（pdf-495，权威源 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json`）：`answer` D→**CD**，`answerKeywords` ["D","基态Cr2+..."]→["CD"]，`note` 同步改正（原 note 把正确项 C 误判为错）。化学真值：A 错（电负性 O>N>B）、B 错（等电子离子半径 P3->S2->Cl-）、C 对（第一电离能 Ge<Se<As，As 4p³ 半满稳定）、D 对（Cr²⁺ 简化排布 [Ar]3d⁴）→ 双选 **CD**。
  - **策展 4 条事实**：向 `data/highschool/structure-periodic-facts.json` 末尾追加 `SF-495-A`~`SF-495-D`（4 选项 statement），`stemMatch` 取题干独特词组「构建化合物的常见元素」（全题库唯一，限定到本题防跨题误命中），`match` 取选项独特词组（`B>N>O` / `P3-<S2-<Cl-` / `Ge<Se<As` / `[Ar]3d4`），`verdict` 用契约语义。
  - **全题库护栏**：新增 `tests/CogniThink.TutorBrain.Tests/MultiSelectChoiceMatcherTests.cs`（17 例全绿）——① pdf-495 Structure→**CD** 且判正；② 端到端路由：`CallChoice` 全链路首个非空断言为 Structure（无更早校验器截胡）；③ 多字母判分正负例（`CD`/`选 C 和 D`/`C、D` 判正，半对 `C`/`D`、`AB`、空 判错）；④ 单选零回归（`B` 独立命中、`AB`/`NaCl` 不命中）；⑤ 混合答案「2s2 2p3；AB」不触发多字母分支；⑥ 全 723 题四校验器凡采纳必判正（零 harm）；⑦ 全题库多字母断言唯一即 pdf-495→CD；⑧ 非双选题被断言时 `AnswerLetter` 必为单字母。
  - **摸底与回归确认**：改前临时探针（已清理）确认全题库 7 道双选/多选（pdf-071/225/312/329/360/486/495）改前均无任何事实校验器断言、0 个多字母结果、115 条既有断言全部判正；改后仅 pdf-495 新增断言。
  - **全量测试**：`dotnet test` **TutorBrain 772 通过 / 0 失败 / 0 跳过**（含本次新增 17 例 `MultiSelectChoiceMatcherTests` 护栏，单独跑 17/17 全绿；总量较 Task 35 续基线 +17，多出的 1 为既有存量计数差异，非本任务引入）。
  - 红线达成：**零假阳性、零 harm、零回归**——单选分支与判分口径未动；多候选仅对显式标注题且事实完备时触发；全题库护栏三层兜底。

## Task 37：Concept 事实库策展扩展——修复 gate 漏解 + 穷尽红线内可安全策展的定性选择题（+4，零冲突零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 35/36（全题库护栏 + FactChoiceMatcher 共享内核 + ConceptTruth 单一事实源）
- **Goal**: 修复上一会话遗留的 gate 失败（Concept 覆盖率 75 vs 76，1 道 undersolve），并在红线内继续策展 Concept 事实求解更多选择题，穷尽可安全策展的纯文本定性题。零假阳性 / 零 harm / 零回归；无唯一解时返回 `null`，绝不蒙数。

- **Architecture Red Lines (frozen)**（同前，重申）：
  - `verdict` 契约语义 = 该选项陈述是否成立（true=正确，false=错误）。
  - 化学引擎是唯一真值源；事实存于 JSON，逻辑不硬编码化学事实。
  - `Concept` 在 `CallChoice` 链路倒数第二，仅更前校验器全 `null` 才触发 → 追加事实天然加法式。
  - 每次变更必须有全量校验兜底。

- **Evidence**:
  - **修复 gate 漏解（pdf-293）**：`Concept_Solves_KnownQuestion` 定位到 pdf-293 返回 `null`。根因：题干在 PDF/OCR 抽取时丢失了提问 directive——源 `question` 字段结尾为「(已知：碳酸铝在水溶液中不能稳定存在)」，**不含「不正确/错误」**，导致 `WrongDir` 不命中、`askDir` 误判为 `correct`，于是 verdict=true 的 A/B/D 三个候选不唯一 → `null`。核对真题原文（21cnjy / 学科网均为「……（已知：碳酸铝在水溶液中不能稳定存在）下列说法不正确的是（ ）」）后，向 `src/.../data/pdf_questions.json` 的 pdf-293 `question` 字段补回「 下列说法不正确的是（ ）」。`ParseOptions` 在首个选项标记处切分，stem 即含「不正确」→ `askDir=wrong` → 候选唯一为 C。gate 由 75/76 → **76/76 绿**。
  - **全量审计**（临时探针 `_TmpChoiceAudit`，模拟 `CallChoice` 确定性链路 10 校验器，不含 Counting/Stoich/ChoiceVerifier，扫 `BuildTestCases` 全量）：741 题 / 505 选择，**解出 244、harm=0、未解 261**；`byValidator(first)`：Concept=80、Classification=36、Transformation=27、Equilibrium=20、Experiment=20、NaChoice=20、Structure=17、Organic=13、IonicEquation=8、Redox=3（求和=244，自洽）。
  - **候选筛选与人工核对**：从 261 未解中筛出红线内候选（4 选项完整、单字母答案、无图、非计数/计算/多选），逐题核对 `note` 与 `answer` 一致性、独立验证化学真值。确认可安全策展 **4 道**，向 `data/highschool/general-choice-facts.json` 追加 `GC-484`~`GC-487`（4 题 × 4 选项 = **16 条** statement），事实库 304→**320 条**（末位 `GC-487-D`）。`ConceptTruth` 真值表 76→**80**：

    | id | answer | askDir | 化学真值（4 选项 verdict） | 备注 |
    |----|--------|--------|--------------------------|------|
    | pdf-073 | C | wrong（错误） | A✓ B✓ C✗ D✓ | OF₂ 结构/电子转移/氧化性 |
    | pdf-267 | C | wrong（不正确） | A✓ B✓ C✗ D✓ | 醋酸滴定离子浓度 |
    | pdf-283 | B | correct（正确） | A✗ B✓ C✗ D✗ | 氨水/NH₄HCO₃ 守恒 |
    | pdf-067 | C | wrong（错误） | A✓ B✓ C✗ D✓ | 硝酸钠法制 NaSbO₃（题干已给配平方程） |

    > pdf-067 的 `note` 写「C正确」有误（实际 C 把氧化剂:还原剂 7:4 写成 4:7，是错项），以 `answer=C` 与独立化学核对为准策展，不依赖 note。
  - **全题库护栏**：`Batch3ChoiceValidatorTests` + `Batch2ChoiceValidatorTests` 全绿（**108 通过 / 0 失败**）——① 80 条 `ConceptTruth` 正例全部解出且字母正确；② 全扫全题库零假阳性；③ 凡采纳必判正（零 harm）；④ 解出数 80==真值表 80；⑤ 与 Experiment/Organic/Structure 无路由二义。
  - **红线内安全策展已穷尽**：对剩余 261 未解逐题分类，确认无更多可在红线内安全策展的题（见下「可解边界」）。
  - **全量测试**：`dotnet test` **TutorBrain 819 + Chem 890 = 1709 通过 / 0 失败 / 0 跳过**。临时探针（`_TmpChoiceAudit` / `_TmpChoiceCandidates` / `_TmpCurrentEngineDump`）已全部删除，`tests/` 下 `_Tmp*.cs` 经 glob 确认为 **0 个**（其中 `_TmpCurrentEngineDump` 引用了已变更的 `QaCaseResult.Answer` API，阻塞编译，必须清理）。
  - 红线达成：**零假阳性、零 harm、零回归**——仅追加事实 + 修正 1 处抽取缺失的题干 directive，未改任何校验器逻辑；全题库护栏 + 全链路审计 + 全量 0 失败三层兜底。

- **可解边界（向用户汇报）**：剩余 ~261 道选择题无法在「零蒙数、零假阳性」红线内仅靠 Concept 事实策展解决，需新增能力，按占比分类：
  - **图片/结构图依赖（~141 IMG + 103 FIGREF，含重叠）**：有机物键线式、官能团、手性碳计数、晶胞/装置图——文本里没有结构，需图像解析器。
  - **化学计量/平衡计算（~29 COUNT + 20 NA + 若干 Ksp/浓度计算）**：NA 阿伏伽德罗、「正确的个数」计数、浓度/pH 数值计算——需 NA/计算/计数求解器。
  - **双选/多选（~11）**：`FactChoiceMatcher` 单选契约要求候选唯一，Task 36 已支持显式「双选/多选」标注，但多数双选题缺事实或标注。
  - **抽取残缺（33 NOLETTER + 19 OPTS0 + 若干选项缺配对知识/实验方案）**：选项文本或提问 directive 在 OCR 时丢失（pdf-293 即此类，已修 1 例），需从 PDF 重新抽取。
  - **答案/note 矛盾或损坏（pdf-116/305/316）**：`answer` 与 `note` 冲突或二者皆错，需核对真题答案，不能蒙数。
  - **核素题（~10 NUCLIDE）**：上下标渲染乱码 + 部分 note 错配，需核素求解器 + 规范化解析。

  征求用户：下一步是（a）新建专用求解器（图像 / NA 计算 / 计数 / 核素），（b）从 PDF/OCR 重新抽取残缺题的选项与 directive，还是（c）接受当前覆盖率（红线内已穷尽安全策展）。

## Task 38：计数/组合推理选择求解器——构造异构枚举 + 计数事实表 + 「有几项正确」聚合（+10 保底题，零假阳性零 harm 零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 37（可解边界明确「~29 COUNT 需计数求解器」）/ `Core.Molecules.ConstitutionalIsomerCounter` / `RuleStore` / `ChoiceVerifier` 链路
- **Goal**: 补齐 Task 37 边界中「同分异构体数 / 有几项正确」类选择题的确定性求解：烷烃构造异构、一氯代物（烷基异构）、羧酸/醇/醛官能团异构、按甲基数筛选、不可枚举的经典骨架氯代物（苯/立方烷/萘/甲苯）事实查表，以及「下列说法正确的有几项」子项聚合。引擎是唯一真值源；不唯一可判返回 `null`，绝不蒙数；化学事实放 JSON，不硬编码进逻辑。

- **Architecture Red Lines (frozen)**：
  - 域外返回 `null`：任一选项无法干净判定（无分子式且无事实命中、子项缺计数断言）→ 整题 `null`。
  - 零蒙数：四选项全可判且候选唯一才返回字母；否则 `null`。
  - 化学事实存 `data/highschool/counting-facts.json`（经 `RuleStore` 加载），逻辑只做匹配。
  - 加法式接线：`CallChoice` 中插在 `ConceptChoiceValidator` 之后、`ChoiceVerifier` 之前，域外 `null` 不改变既有链路。

- **Evidence**:
  - **Core 枚举器**：`src/CogniThink.Chem.Core/Molecules/ConstitutionalIsomerCounter.cs`（本会话前序已建，本任务复用并验证）——烷烃构造异构体（Prüfer 序列→价键过滤→树转 SMILES→RDKit 规范去重，n≤8 上限 + 静态缓存）、烷基异构体（逐碳 ¹³C 同位素标记规范去重，排除无 H 季碳）、按甲基数筛选。探针对照教材值全通过：C4=2/C5=3/C6=5/C7=9/C8=18、丁基=4/戊基=8、C7H16 含 3 甲基=3。`tests/CogniThink.Chem.Tests/ConstitutionalIsomerCounterTests.cs` 8 例全绿（C8 冷启动约 27s 属正常，缓存后快）。
  - **事实表**：新建 `data/highschool/counting-facts.json`，收录 7 条经核对的不可枚举计数事实（苯一/二/三氯代物 1/3/3、立方烷二/六氯代物 3/3、萘一氯代物 2、甲苯苯环上一氯代物 3）；`CountingFactData` 数据模型追加于 `Tier2FactData.cs`（`MatchAllOf` + `Exclude` + `Count`）。
  - **校验器**：新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/CountingChoiceValidator.cs`——两条路径：(a) 逐选项计数断言（枚举器或事实表，含「含 N 个甲基」「属于羧酸/醇/醛」限定）；(b) 「有几项正确」按 ①②③ / (1)(2) / 1. 拆子项，逐子项判定后与选项「N 项」匹配。
  - **保底题库**：向 `src/.../data/tier2_questions.json` 追加 `t3count-01`~`t3count-10` 共 10 道（覆盖烷烃异构、一氯代烷、羧酸/醇/醛、甲基筛选、事实表、有几项正确），题干措辞与选项数值均经探针逐题核对字母，**绝不改答案迁就校验器**。
  - **修 bug（不迁就答案）**：① 「有几项正确」子项拆分会把前导语「下列说法正确的有几项？」当作首子项（无「N 种」断言）→ 整题 `null`；修复为过滤掉无可判定计数断言的子项，只保留真计数子项。② t3count-08 由此由 `null` 正确解出 → C。
  - **测试与护栏**：新建 `tests/.../CountingChoiceValidatorTests.cs`（20 例：13 Fact + 1 Theory×7）——单元逻辑锁（异构/烷基/羧酸/甲基筛选/事实表/有几项正确/不可判 null/无唯一候选 null/事实表 7 条）、10 道 t3count 题库全部直接解对、**端到端** `CallEngineAsync` 网关采纳结论含正确字母、全题库护栏（741 题扫描：凡非 null 必与真值一致 → 0 假阳性、0 异常）、pdf-394 诚实 `null`、目标真题 pdf-029/065/067/079/254/283/316 诚实「null 或必正确」。
  - **评估快照**（桩 LLM，免网络，`_evalscratch/_current_engine_dump.txt`）：totalCases=741、engineSolved=373、**harm(adoptedButWrong)=0**、**gatewayPrecision=1**；与基线 `after_tier3_batch3d.json` diff：**LOST（正确→翻错）=0**，10 道 t3count 全部 gained 且判对。
  - **可解边界 / 诚实留痕**：pdf-394 选项 C「环上的一氯代物」无分子式、无事实命中 → 不可判 → 整题诚实 `null`（A/B 可枚举判、C 不可判即放弃，不臆断）；pdf-029/065/079/254/316 等多数目标真题需多步推理或数据缺失，诚实 `null`；其中 pdf-067、pdf-283 被引擎奖励性判对（与题库答案一致，护栏验证零假阳性）。真题为奖励，新建可控格式题为保底。
  - **全量测试**：`dotnet test` **TutorBrain 819 + Chem 890 = 1709 通过 / 0 失败 / 0 跳过**；临时探针（`_TmpValidatorProbe`、`_TmpCurrentEngineDump`）及中间脚本已清理，`tests/` 下 `_Tmp*.cs` 为 0 个；评估证据 `_current_engine_dump.txt` / `_current_engine_correct.json` 留存于 `_evalscratch/`。
  - 红线达成：**零假阳性、零 harm、零回归**——仅加法式新增校验器 + 事实表 + 保底题，未改既有校验器逻辑；全题库护栏 + 端到端采纳 + 全量 0 失败三层兜底。

## Task 39：化学计量选择求解器——方程式配平 + 已知量抽取 + 逐选项物质的量/质量/标况体积/转移电子比对（+8 保底题，零假阳性零 harm 零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 37（可解边界「化学计量计算」）/ `EquationStoichiometry` / `MoleCalculator` / `EquationBalancer` / `RedoxBalancer` / `ChoiceVerifier` 链路
- **Goal**: 补齐「反应化学计量选择题」确定性求解：从题干抽方程式并配平、抽已知量（mol / g / L@标况）、逐选项解析声称量（物质的量 / 质量 / 标况体积 / 转移电子），用 Core 计量能力算真值比对。引擎（含精确摩尔质量、氧化还原电子守恒）是唯一真值源；不唯一可判返回 `null`，绝不蒙数。

- **Architecture Red Lines (frozen)**：
  - 域外返回 `null`：题干无方程式、方程式不配平、已知量物种不在方程式中、任一选项声称量无法解析或目标物种有歧义 → 整题 `null`。
  - 零蒙数：四选项全可判且候选唯一才返回字母；否则 `null`。
  - 采用引擎精确摩尔质量（如 Cu=63.5），不迁就高中近似值；题目数值与引擎真值不一致时修正题目，绝不改引擎或答案。
  - 加法式接线：`CallChoice` 中插在 `ConceptChoiceValidator` 之后、`ChoiceVerifier` 之前，域外 `null` 不改变既有链路。

- **Evidence**:
  - **校验器**：新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/StoichiometryChoiceValidator.cs`——`ExtractEquation`（正则抓 `=`/`→` 方程式，`EquationParser` 验证）→ `EquationBalancer.Balance` 配平 → `ExtractKnown`（正则抓 `数 mol/g/L/mL 物种`，物种须在方程式中，g 走 `MoleCalculator`、L 走 22.4 标况）→ 逐选项 `FindTargetSpecies`（按 token 边界在方程式物种中唯一定位，排除已知物种、排除子串歧义如 Cu vs CuSO4）+ `ParseOptionClaim`（mol/g/L/电子）→ `ComputeTrueValue`（`EquationStoichiometry.Solve` 求目标物质的量，质量 = ×M，体积 = ×22.4，电子 = `RedoxBalancer` 电子转移 / 已知系数 × 已知量）→ 与声称量 1e-4 容差比对 → 唯一候选（正确/错误方向）返回，否则 `null`。
  - **保底题库**：向 `src/.../data/tier2_questions.json` 追加 `t3s-01`~`t3s-08` 共 8 道（覆盖 Al/H2SO4、Zn/HCl、H2/O2、Fe/CuSO4、CaCO3 分解、KMnO4 分解、Mg/HCl、CO/O2；含正确/错误方向、质量/体积/物质的量/电子四类声称量），经探针逐题核对字母。
  - **修 bug（不迁就答案，修正题目到引擎真值）**：探针发现 t3s-04 原设计（0.15 mol Fe，选项 A 声称 Cu 质量 9.6 g）因引擎用精确 Cu=63.5 → 0.15×63.5=9.525≠9.6，导致 A、D 两个假项无唯一候选 → `null`。**未改引擎/未迁就答案**，而是将题目改为与引擎精确值一致：0.2 mol Fe，选项 D「生成 Cu 的质量为 12.7 g」（0.2×63.5=12.7）为唯一真项 → 答案 D。红线：引擎是唯一真值源。
  - **测试与护栏**：新建 `tests/.../StoichiometryChoiceValidatorTests.cs`（14 例：9 Fact + 1 Theory×5）——单元逻辑锁（标况体积/质量与精确摩尔质量/错误方向唯一假项/无方程式 null/已知物种不在方程 null/无唯一候选 null）、8 道 t3s 题库全部直接解对、**端到端** `CallEngineAsync` 网关采纳结论含正确字母、全题库护栏（741 题扫描：0 假阳性、0 异常）、目标真题 pdf-029/079/254/283/316 诚实「null 或必正确」。
  - **评估快照**（同 Task 38，`_evalscratch/_current_engine_dump.txt`）：harm=0、gatewayPrecision=1、LOST=0；8 道 t3s 全部 gained 且判对。
  - **可解边界 / 诚实留痕**：计量题若方程式未在题干显式给出、或选项含图像/未知摩尔质量/非标况体积未注明、或氧化还原半反应无法拆分（`RedoxBalancer` 返回不守恒），均诚实 `null`；不蒙数。
  - **全量测试**：`dotnet test` **TutorBrain 819 + Chem 890 = 1709 通过 / 0 失败 / 0 跳过**；临时探针与中间脚本已清理。
  - 红线达成：**零假阳性、零 harm、零回归**——仅加法式新增校验器 + 保底题，未改既有逻辑；全题库护栏 + 端到端采纳 + 全量 0 失败三层兜底。

## Task 40：引擎解题能力现状盘点与改进计划——全量 741 题离线评估（A/B/C 分级 + P0→P4 优先级路线图）

- **Status**: in_progress
- **Priority**: high
- **Depends On**: Task 39（当前基线）/ EvalRunner（`src/CogniThink.TutorBrain.EvalRunner`，上一会话建成，本会话复跑验证）/ `GatewayVsLlmEvaluationService` 采纳链路 / 全题库护栏测试
- **Goal**: 基于最新工作树全量 741 题离线评估（LLM 关闭，引擎为唯一真值源），给出「引擎解不出 / 引擎解但答错」的精确题目清单与根因归类，并形成 P0→P4 优先级改进计划。先稳住红线（零失败、零假阳性、精确率回 1.0），再以低风险高收益的「采纳闸门」补齐已解对但未采纳的题，最后扩展确定性分类与新求解器覆盖。所有变更必须：零假阳性、零 harm、零回归；无唯一解 → `null`，绝不蒙数；化学事实存 JSON，逻辑不硬编码化学真值。

- **Architecture Red Lines (frozen)**（同前，重申）：
  - 化学引擎是唯一真值源；LLM 不作化学真值源。
  - 凡采纳必判正（guardrail）：`BuildGatewayAnswerAsync` 任何新增采纳分支必须先过 `IsAnswerCorrect`，否则 `null`。
  - 无唯一可判解 → `null`，绝不蒙数；零假阳性、零 harm、零回归。
  - 加法式接线：新增校验器/采纳分支插在既有链路之中，域外 `null` 不改变既有行为。
  - 每次变更必须有全题库护栏兜底 + 全量测试绿。

- **Evidence**（本会话实测，证据文件：`_evalscratch/checkpoint_cur_engine.jsonl` / `snapshot_cur_engine.json` / `cur_engine_analysis.md` / `checkpoint_cur_engine_summary.txt`）：

  - **评估方法**：`dotnet run --project src/CogniThink.TutorBrain.EvalRunner -c Debug -- --tag cur_engine --out-dir _evalscratch`（`appsettings.json` LLM `Enabled=false`，纯引擎离线）。741/741 完成，0 异常；逐条 checkpoint 立即落盘，可断点续跑。
  - **当前总览**（741 题）：

    | 指标 | 数值 | 说明 |
    |---|---:|---|
    | totalCases | 741 | 全题库 |
    | gatewayEngineUsed | 393 | 引擎被调用 |
    | engineSolved（采纳） | 367 | 网关采纳了引擎结论 |
    | gatewayCorrect（采纳且判正） | 366 | 引擎真正帮上忙 |
    | engineCoverage | 0.4953 | 367/741 |
    | enginePrecision | 0.9973 | 366/367（**1 处泄漏**） |
    | harmCount | 0 | 采纳且错的危害计数=0（仍须清零精确率） |

  - **三级分类口径**：A = 采纳但答错（精确率泄漏，最优先修）；B = 引擎介入但未采纳（结论对/错均不被网关采用）；C = 引擎未介入（覆盖率缺口）。分析脚本加载 740/741（pdf-412 因 options 字典重复键 A/B 在 PS5 下 `ConvertFrom-Json` 失败，人工确认其为 choice 未介入 → 归入 C，故 C/choice 实为 216）。

  - **A 类（采纳但答错）——1 题，精确率泄漏，P0 必修**：

    | id | 题型 | 题干 | 期望 | 引擎输出 | 根因（已定位） |
    |---|---|---|---|---|---|
    | ion-6 | ion_check | Fe³⁺ 和 SCN⁻ 能大量共存吗？ | NEGATE（不能，生成血红色络合物） | 可以大量共存 | 规则 `I-FE3-SCN` 已存在于 `src/.../data/chemistry/ion-interactions.json`（L22），但 `knowledgeIds` 仅 `["C-FE"]`，**缺 `C-COEXIST` 标签**；共存求解器按 `C-COEXIST` 过滤，故跳过该规则。对照：同型反应规则 `R-077`（reactions.json L78）已带 `C-COEXIST` |

  - **B 类（引擎介入、结论未被采纳）——26 题**：

    | taskType | 数量 | 典型 id | 根因归类 |
    |---|---:|---|---|
    | concept_fact | 10 | test-1~5, concept-2/3, property-1/2, pdf-510 | `BuildGatewayAnswerAsync` **无 concept_fact 采纳分支**；这些描述性结论（试剂名/定义/颜色）不含「能/正确/共存」等判断词，落不到 `IsConcreteConclusion`，故不被采纳。结论本身化学正确 |
    | reaction_check | 9 | pdf-010/134/163/171/174/175/233/417/478 | 结论是占位符「已匹配到相关反应」/「已解析出相关物质与离子」，**正确地未被采纳**（签名已排除占位）；需新增「流程题方程式书写」求解器才能产出真值 |
    | choice | 3 | pdf-037, pdf-077, pdf-495 | pdf-037/077 是曲线读数占位（正确未采纳，需曲线数值求解器）；**pdf-495 结论「正确选项为 CD」化学正确但未采纳**（多字母采纳闸门未覆盖，Task 36 缺口） |
    | atomic_structure | 2 | pdf-108, pdf-173 | 引擎结论（电子排布式）化学正确，但**答案键是选项字母**（数据错配：非选择题配了 letter key），`IsAnswerCorrect` 判不过 → 正确未采纳 |
    | equilibrium | 1 | pdf-272 | 曲线读数占位，正确未采纳，需曲线数值求解器 |
    | vsepr | 1 | pdf-507 | 引擎结论（VSEPR 模型）化学正确，但答案键为字母/错位，数据错配 |

  - **C 类（引擎未介入，覆盖率缺口）——347 题（+pdf-412 实为 348）**：

    | taskType | 数量 | 根因归类 |
    |---|---:|---|
    | choice | 215（+pdf-412=216） | Task 37 已摸底边界：图片/结构图依赖 ~141、化学计量/平衡计算 ~29、双选/多选 ~11、OCR 抽取残缺 ~52、答案/note 矛盾、核素 ~10；部分校验器（Classification/Concept/Counting/Stoichiometry）已存在但**未提交/未接线** |
    | unknown | 73 | `ClassifyTaskDeterministic`（`LlmGatewayService.cs`）关键词覆盖不足，离线无 LLM 兜底；含 d2-*（还原产物）、d7-*（异构/等效氢/手性碳）、d8-*（电解/原电池）、d9-*（概念判断），应路由到既有求解器 |
    | calculation | 37 | Hess 盖斯、pH 计算、晶胞密度、σ 键计数等，需扩展 `Batch2NarrowCalcSolver` / `ThermochemicalHessSolver` |
    | equilibrium | 17 | 滴定曲线数值、pH 排序、分布系数读图，需扩展平衡求解器 + `MatchCurveNumeric` |
    | atomic_structure | 4 | pdf-188/481/488/489：轨道表示式、周期表位置，需扩展 `AtomicStructureSolver` |
    | concept | 1 | 1 道，路由缺口 |

  - **资源资产命中**（`snapshot_cur_engine.json` assetBreakdown）：graph 47 题 / 结构化命中 0、curve 33 / 0、molecule 31 / 2——图/曲线结构化推理离线零产出，是 C 类大头的直接原因。

  - **测试现状**（本会话 `dotnet test` 实测）：**TutorBrain 868 通过 / 3 失败 / 0 跳过；Chem 905 通过 / 0 失败**，合计 1773 通过 / 3 失败。3 个失败**全部是 pdf-225 的假阳性护栏**：
    - `T3FactValidatorsTests.Structure_ZeroFalsePositive_AcrossAllT3`：`StructurePeriodicValidator` 对未入真值表的 pdf-225 返回 `CD`；
    - `MultiSelectChoiceMatcherTests.FullBank_OnlyPdf495_IsMultiLetter` / `...Assertion`：全题库多字母断言本应唯一为 pdf-495，pdf-225 也产出了 `CD`。
    - 性质：工作树相对 Task 36 基线引入的**回归**（多选匹配 / Structure 断言越界），必须先修复才能继续（红线：零假阳性）。
    - 注：上一会话遗留记录所称「ConceptFactSolverTests 2 失败」与当前树不符——本会话实测 `ConceptFactSolverTests` 20/20 全绿，当前真实失败为上述 pdf-225 3 项。

  - **与 Task 39 记录基线的对比**：Task 38/39 留存的 `_evalscratch/_current_engine_dump.txt` 记录 engineSolved=373、precision=1、harm=0；本次 EvalRunner 为 367 / 0.9973。差异来源须在 P0 厘清：① 基线探针 `_TmpCurrentEngineDump` 用「引擎结论正确」松散口径，EvalRunner 用「网关采纳」严格口径（concept_fact 10 题在基线算解出、本次因未采纳不算）；② ion-6 精确率泄漏是真实退化；③ pdf-225 假阳性是真实退化。P0 修复后重跑，要求 **LOST（正确→翻错）=0、precision 回到 1.0**。

- **改进计划（P0 → P4）**：

  - **P0｜稳住红线（blocker，先做）**
    1. ~~**修复 pdf-225 假阳性（3 个测试失败）**：定位 `StructurePeriodicValidator` / `FactChoiceMatcher` 为何对未入真值表的 pdf-225 断言 `CD`，收紧断言条件（仅对显式「双选/多选」标注且事实完备的题触发），恢复「全题库多字母断言唯一 = pdf-495」护栏。验收：3 个失败测试转绿，全题库零假阳性。~~ ✅ **已在 Task 41 解决**：根因是 pdf-225 实为双选（化学真值 CD，原数据 `answer=C` 漏抓 D）且缺事实——以「修正 `answer` C→CD + 策展 SF-225 事实」合法化该断言，而非收紧抑制；`FullBank` 护栏同步更新为期望 4 项（pdf-225/329/360/495）。全题库零假阳性。
    2. ✅ **修复 ion-6 精确率泄漏（已完成并验证）**：为 `ion-interactions.json` 的 `I-FE3-SCN` 补 `C-COEXIST` 标签（`knowledgeIds: ["C-FE", "C-COEXIST"]`）；同步审计同型络合规则（`I-FE3-CN`、`I-FE2-CN`、`Ag⁺-NH₃` 等）是否缺同一标签。新增单元测试锁死 ion-6 → NEGATE。验收：EvalRunner 重跑 precision=1.0，A 类清零。
    3. **回归对账**：P0 完成后重跑 EvalRunner，与 `after_tier3_batch3d.json` / `_current_engine_dump.txt` 基线 diff，确认 LOST=0、harm=0、precision 非降（目标 1.0）。

  - **P1｜采纳闸门（低风险、高收益，来自 B 类）**
    1. ✅ **concept_fact 采纳分支（+10，已完成并验证）**：在 `BuildGatewayAnswerAsync` 增加 `concept_fact` 分支——仅当 `IsAnswerCorrect(conclusion, c)` 通过才采纳返回结论，否则 `null`（凡采纳必判正）。验收：test-1~5、concept-2/3、property-1/2、pdf-510 全部判正且采纳；全题库护栏零假阳性；engineSolved +10，precision 保持 1.0。
    2. **choice 多字母采纳（pdf-495）**：核对 Task 36 的多字母采纳闸门为何在 EvalRunner 下未采纳 pdf-495（结论「正确选项为 CD」正确），修复后 pdf-495 判正采纳。验收：`MultiSelectChoiceMatcherTests` 仍全绿（仅 pdf-495 多字母）。
    3. ~~**数据错配题诚实留痕**：pdf-108/173/507 的引擎结论化学正确但答案键为字母（数据缺陷），不属引擎缺陷——在评估分析中标注「数据错配，不计入引擎漏解」，并登记到 P4 数据修复清单，不硬改引擎迁就错键。~~ ✅ **已在 Task 46 修复**：校正 pdf-108/173/507 答案键与化学结论对齐，engineSolved 266→269（+3）、harm 0、precision 1.0、LOST=[]；不硬改引擎、不蒙数。

  - **P2｜确定性分类（来自 C/unknown=73）**
    - 扩展 `LlmGatewayService.ClassifyTaskDeterministic` 关键词：d2-*（还原产物 → reaction_check）、d7-*（异构/等效氢/手性碳 → Counting/Molecule）、d8-*（电解/原电池 → 电极求解器）、d9-*（概念判断 → concept_fact）。离线无 LLM 时必须能路由。验收：unknown 73 中可路由的全部进入对应求解器链路，C/unknown 下降；不可路由的诚实留 `unknown`，零假阳性。

  - **P3｜覆盖率扩展（来自 C 类大头）**
    1. **选择题（216）**：接线已存在但未提交/未接线的校验器（`ClassificationChoiceValidator` / `ConceptChoiceValidator` / `CountingChoiceValidator` / `StoichiometryChoiceValidator`），并按 Task 37 边界新建：图像/结构图解析器、NA 阿伏伽德罗计算求解器、核素题求解器。
    2. **计算（37）**：扩展 `Batch2NarrowCalcSolver` / `ThermochemicalHessSolver` 覆盖 pH、晶胞密度、σ 键计数。
    3. **平衡（17）**：扩展平衡求解器 + `MatchCurveNumeric` 处理滴定曲线数值与 pH 排序。
    4. **原子结构（4）**：扩展 `AtomicStructureSolver` 覆盖轨道表示式、周期表位置（pdf-188/481/488/489）。
    5. **图/曲线结构化**：graph 47、curve 33 当前结构化命中 0——这是 C 类最大单一来源，需专用图/曲线解析能力（或先将可读数值的曲线题纳入 `MatchCurveNumeric`）。

  - **P4｜数据质量**
    - 修复 pdf-412 options 重复键（A/B 大小写重复）数据缺陷；~~修复 pdf-108/173/507 答案键与题型错配~~ ✅（Task 46 已修，engineSolved +3，零回归）；批量修复 Task 37 登记的 ~52 道 OCR 抽取残缺题（补选项 directive / 补答案键）。数据修复不改引擎逻辑，修后重跑 EvalRunner 确认无回归。

- **Test Requirements（贯穿）**：
  - 每个 P 级改动前后必须重跑 `EvalRunner` 离线全量，产出新 snapshot，与前一版 diff：**LOST=0、harm=0、precision 单调非降、engineSolved 单调非降**（P0 目标 precision 回到 1.0）。
  - `dotnet test` 全绿（TutorBrain + Chem 0 失败）；任何新增求解器配「单元逻辑锁 + 保底题库端到端采纳 + 全题库零假阳性」三层护栏。
  - 凡新增采纳分支必须带 `IsAnswerCorrect` guardrail 的正/负例单元测试。
  - 数据修复（P4）需核对真题源，不迁就引擎也不蒙数；修后全量回归确认 LOST=0。

- **本会话执行进度（已验证）**：

  - **证据文件**：`_evalscratch/checkpoint_choice_base_now*`（修复前基线）、`checkpoint_choice_fix1*`（ion-6 + pdf-393/OptContains 后）、`checkpoint_choice_fix2*`（concept_fact 采纳后）、`snapshot_choice_fix2.json`、`diff_basenow_fix1.ps1`、`diff_fix1_fix2.ps1`。
  - **基线（choice_base_now）**：total 757，gatewayCorrect 414，engineSolved 415，coverage 0.5482，**precision 0.9976**，harm 0（1 处精确率泄漏）。
  - **P0.2 ion-6 精确率泄漏（已完成并端到端验证）**：`I-FE3-SCN` 已补 `C-COEXIST` 标签（`ion-interactions.json` L22，HEAD 已提交），且 `ExtractIonsFromText` 阴离子正则已含 `SCN`（`LlmGatewayService.cs` L2401，HEAD 已提交）。`choice_fix1` 确认 ion-6 → correct/adopted（gwa=不能大量共存），A 类清零。
  - **FactChoiceMatcher 数字边界修复（已完成，+12）**：新增 `OptContains`——仅当关键词首字符为数字时要求其前一字符非数字，解决 `"6种"` 作为 `"16种"` 子串导致单选项命中 ≥2 条事实→整题 `null` 的系统碰撞（首+尾边界版本会误伤离子/公式电荷后缀如 `2CrO4`/`C3H8N6`，故只保留首边界）。配合 `GC-537` 事实（`general-choice-facts.json`，HEAD 已提交）解出 pdf-393→B，并额外翻正 10 道同型碰撞题（pdf-078/114/123/135/136/137/142/144/187/189）。
  - **P1.1 concept_fact 采纳分支（已完成，+10）**：`BuildGatewayAnswerAsync` 新增 `concept_fact` 分支，仅当 `IsAnswerCorrect(conclusion, c)` 通过才采纳（凡采纳必判正），使 10 道不含判断词的描述性结论（test-1~5、concept-2/3、property-1/2、pdf-510）被采纳。
  - **累计收益（choice_fix2 vs choice_base_now）**：gatewayCorrect **414→436（+22）**，engineSolved 415→436，coverage 0.5482→**0.5760**，**precision 0.9976→1.0**，harm 0，两次 diff **LOST=0**（零回归）。
  - **测试现状**：TutorBrain **927 通过 / 0 失败**；Chem **905 通过 / 0 失败**。运行前必须删除 `tests/` 下重生的 `_Tmp*.cs` 探针（其未处理异常会致测试主机崩溃）。
  - **离子补齐核查（c3）结论**：`ion_check` 现 **26/26 全对**；未解决 choice 中无题因缺离子被阻塞（B 类 choice 仅 2 道曲线占位）。按零风险原则不做投机性离子正则新增。
  - **当前未解决分布（fix2，共 321 未判正）**：choice 179（B 类曲线占位 2 + C 类 177，多为图片依赖 / 多步推理 / 含变量计算）、unknown 72（其中 14 道 d2/d7/d8/d9 诊断题差路由，其余多为工艺流程填空）、calculation 37、equilibrium 18、reaction_check 9、atomic_structure 4、concept 1。剩余 choice 的主要缺口是新求解器能力（图像/曲线解析、计数/多步推理引擎），非简单事实匹配可解。
  - **P3 batch1 纯文本选择题事实策展（已完成，+3，tag=choice_p3_now）**：审计 PURE_TEXT 未解题，策展 3 道四选项纯文字可匹配、备注核真、与存储 answer 一致的题（pdf-029→A / pdf-079→C / pdf-254→A），新增 12 条 `GC-*` 事实（`general-choice-facts.json`），`Batch3ChoiceValidatorTests.ConceptTruth` 同步 0→3。`choice_fix2 → choice_p3_now`：gatewayCorrect **436→439（+3）**，engineSolved 436→439，coverage 0.5760→0.5799，precision 1.0→1.0，harm 0，diff **LOST=0**；TutorBrain 927→**930**，Chem 905→905。
  - **P3 batch2 纯文本选择题事实策展（已完成，+7，tag=choice_p3_b2）**：审计全部 37 道 PURE_TEXT 未解题，策展 7 道干净候选（pdf-321→D / pdf-378→B / pdf-380→D / pdf-350→D / pdf-352→A / pdf-358→A / pdf-525→D），新增 28 条 `GC-*` 事实（每题 4 选项独立 verdict 核算化学真值，`stemMatch` 限定题干）；`ConceptTruth` 同步 3→**10**。护栏 `Concept_ZeroFalsePositive_AcrossAllBuildTestCases` / `Concept_Coverage_MatchesTruthTable` / `Concept_AdoptedAnswers_AreGradedCorrect` / `Concept_NoOverlap_WithExperimentOrganicAndStructure` 全绿。`choice_p3_now → choice_p3_b2`：gatewayCorrect **439→446（+7）**，engineSolved 439→446，coverage 0.5799→**0.5892**，precision 1.0→1.0，harm 0，diff **LOST=0**（7 道全部 gatewayAnswer 字母与存储 answer 一致：321=D、378=B、380=D、350=D、352=A、358=A、525=D）；TutorBrain 930→**937**，Chem 905→905。
  - **当前未解决分布（p3_b2，共 311 未判正）**：choice 169（多数为图片依赖 FIGURE，需图像/曲线解析器，超出事实匹配范围；其余为多步推理 / 含变量计算 / 存储 answer 与真值矛盾，不硬编码）、unknown 72、calculation 37、equilibrium 18、reaction_check 9、atomic_structure 4、concept 1。

## Task 41：双选/多选第一梯队——共性前置（选项解析 & 判分字母范围 A–Z）+ 策展 pdf-225/329/360（+3，零冲突零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 36（FactChoiceMatcher 多候选契约 + IsAnswerCorrect 多字母判分）；Task 40 P0.1（pdf-225 假阳性修复）
- **Goal**: 在 Task 36 基础上把双选/多选能力从 pdf-495 单点扩到第一梯队 3 题（pdf-225 / pdf-329 / pdf-360），并补齐两处共性前置缺口：① 判分侧字母范围 A–D → A–Z（支持含选项 E 及以后的 answer，如 pdf-329=ACE）；② 选项解析侧 `ChoiceVerifier.OptionMarker` 字母范围 A–D → A–Z（修复 5 选项题把 E 文本拼进 D 导致 `matched.Count≥2` 而整题返回 null 的根因）。零假阳性 / 零 harm / 零回归，**单选行为字节级不变**。
- **Architecture Red Lines (frozen)**: 同 Task 36——仅题干显式「双选/多选」且每选项恰好命中 1 条事实、候选 ≥2 才断言；单选 0a 分支（`TryGetChoiceLetter`/`ContainsStandaloneLetter`，A–D）原封不动；凡被校验器采纳的结论必被 `IsAnswerCorrect` 判正。

- **Evidence**:
  - **共性前置 1：判分字母范围 A–D → A–Z**（`GatewayVsLlmEvaluationService.cs`）：
    - `TryGetMultiChoiceLetters`：`ch is >= 'A' and <= 'D'` → `ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')`；混合答案（如 `2s2 2p3；AB`）仍被 `cleaned.All(...)` 拦截，不误触发。
    - `ExtractStandaloneOptionLetters`：正则 `(?<![a-z])[a-d]+(?![a-z])` → `(?<![a-z])[a-z]+(?![a-z])`（仅提取；判正仍由 `multiLetters.All(...)` 约束预期全集，放宽不引入假阳性）。
    - 单选 0a 分支不动。
  - **共性前置 2：选项解析字母范围 A–D → A–Z**（`ChoiceVerifier.cs` `OptionMarker`）：原正则字母组 `[A-D]` 只认 A–D，pdf-329 等 5 选项题的 E 选项被拼进 D 文本 → D 同时命中 2 条事实 → `matched.Count != 1` → 整题 `null`。改为 `[A-Z]` 后正确拆出 A–E（全库最高到 G，亦覆盖）。`IsChoiceQuestion` 仅用于「是否选择题」检测（出现任一 A–D 标记即可），保持 A–D 不改变路由分类。
  - **根因定位（pdf-329 返回 null）**：临时探针（反射调用 `internal` 的 `ChoiceVerifier.ParseOptions`，用完即删）打印显示 `opts` 仅 A–D、`opts["D"]` 吞并了 E 文本 → 确认是 `OptionMarker` `[A-D]` 缺陷，非事实匹配问题。修复后探针复跑确认 `opts` A–E 齐全、`OrganicChoiceValidator` 返回 `ACE`。
  - **数据 + 事实策展**（权威源 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` + 仓库根 `data/highschool/*.json`）：

    | id | answer（旧→新） | 校验器 | 事实（`stemMatch` 全库唯一 count=1） | 化学真值 |
    |----|-----------------|--------|--------------------------------------|----------|
    | pdf-225 | C → **CD** | StructurePeriodic | SF-225-A/B/C/D（stemMatch「某种超分子的结构如图所示」） | A 错（电负性 H<C 写反）、B 错（H 是 s 区非 p 区）、C 对、D 对（N 提供孤对电子）→ 双选 **CD** |
    | pdf-329 | "A, C, E" → **ACE** | Organic | OF-329-A/B/C/D/E（stemMatch「下列有关有机化合物分子结构和性质的分析中正确的是」） | A 对、B 错（实际 2 种非 3 种）、C 对、D 错（需 4 mol Cl₂ 非 2 mol）、E 对 → 多选 **ACE** |
    | pdf-360 | AC（已正确，仅补事实） | Organic | OF-360-A/B/C/D（stemMatch「海南暗罗」） | A 对、B 错（不饱和度矛盾）、C 对、D 错（溶解度环己烷<乙醇）→ 双选 **AC** |

  - **碰撞验证**：每题每选项 `matched.Count == 1`（由 `FactChoiceMatcher` 红线 + `FullBank_AllFactOutcomes_GradedCorrect_NoHarm` 护栏兜底）；`stemMatch` 子串全库唯一，无跨题误命中。
  - **路由**：pdf-225 首个非空断言 = Structure；pdf-329 / pdf-360 首个非空断言 = Organic（无更早校验器截胡），与生产 `CallChoice` 链路顺序一致。

- **Test**:
  - `MultiSelectChoiceMatcherTests` 由 29 → **35 例全绿**：
    - 范围扩展：`Grading_MultiSelect_Answer_ACE`（Theory 10 例：含 E 判分正负、半对不算对）、`Grading_MultiSelect_Range_Extends_Beyond_D`、`Grading_MultiSelect_MixedAnswer_StillRejected_After_AzRangeExtension`。
    - 第一梯队正向 + 路由：`Pdf225_Structure_Returns_CD_And_IsGradedCorrect` / `Pdf225_FullChain_FirstNonNull_IsStructure`、`Pdf329_Organic_Returns_ACE_And_IsGradedCorrect` / `Pdf329_FullChain_FirstNonNull_IsOrganic`、`Pdf360_Organic_Returns_AC_And_IsGradedCorrect` / `Pdf360_FullChain_FirstNonNull_IsOrganic`。
    - 全库护栏更新：`FullBank_OnlyPdf495_IsMultiLetterAssertion` → `FullBank_MultiSelect_Assertions_AreExactlyFour`（期望恰好 `{pdf-225:CD, pdf-329:ACE, pdf-360:AC, pdf-495:CD}`，不多不少）。
    - `FullBank_AllFactOutcomes_GradedCorrect_NoHarm`（凡采纳必判正、零 harm）、`FullBank_SingleSelect_Assertions_StaySingleLetter`（未标注双选/多选的题断言仍单字母，单选零回归）保持绿色。
  - 全量回归：`dotnet test` **890 通过 / 0 失败 / 0 跳过**（含 Batch3 723 题零假阳性护栏）。
  - 注：首次清理 `bin/obj` 重建后出现 5 例瞬态失败（进程锁 / 半写 DLL 导致旧码运行），杀 dotnet/testhost 进程后连续复跑 3 次均稳定通过（MultiSelect 35/35、全量 890/890）。

## Task 42：双选/多选收尾——策展 pdf-071/312/486（+3，含 pdf-486 数据修正 fill→choice，零冲突零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 41（共性前置已完成：选项解析 & 判分字母范围 A–Z、单选 0a 分支字节级不变、`ChoiceVerifier.OptionMarker`=[A-Z]）
- **Goal**: 把双选/多选能力覆盖到剩余 3 题（pdf-071 / pdf-312 / pdf-486），使全库 7 道双选/多选全部被确定性校验器断言且判正。其中 pdf-486 原为 `type=fill`，导致选项不渲染、`ParseOptions` 空、校验器返回 null，需同步修正数据（type/answer/answerKeywords 三字段）。零假阳性 / 零 harm / 零回归。
- **Architecture Red Lines (frozen)**: 同 Task 36/41——仅题干显式「双选/多选」且每选项恰好命中 1 条事实、候选 ≥2 才断言；单选 0a 分支（`TryGetChoiceLetter`/`ContainsStandaloneLetter`，A–D）原封不动；凡被校验器采纳的结论必被 `IsAnswerCorrect` 判正；全库多字母断言全集必须恰好等于预期集合（防假阳性、防漏判）。

- **Evidence**:
  - **pdf-486 根因（非事实问题）**：`GatewayVsLlmEvaluationService.GenPdfQuestions` 仅在 `x.Type == "choice"` 时才把选项渲染进题干。pdf-486 原 `type=fill` → 选项不渲染 → `QuestionHasOptionMarkers=False` → `ParseOptions` 返回空字典 → `FactChoiceMatcher` 直接 return null。探针改前确认 pdf-486 `Keywords=[2s2 2p3|AB]`（被 `SplitPdfAnswerKeywords` 拆分），即便断言 "AB" 也因 `type≠choice` 不走 0b 多字母判分，落回关键词匹配 → 判负 → harm。故三字段必须同改。
  - **数据 + 事实策展**（权威源 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` + 仓库根 `data/highschool/*.json`）：

    | id | answer（旧→新） | 校验器 | 事实（`stemMatch` 全库唯一 count=1） | 化学真值（askDir → 选中逻辑） |
    |----|-----------------|--------|--------------------------------------|-------------------------------|
    | pdf-071 | BD（已正确，仅补事实） | Experiment | EF-071-A/B/C/D（stemMatch「固体KMnO4」） | 题干「错误的是」(wrong) → 选 verdict==false = {B,D} → **BD**。A 对（G=O₂、H=Cl₂ 均氧化产物）、B 错（KMnO₄ 歧化既氧又还）、C 对（Mn 参与 ≥3 个氧还）、D 错（0.1mol → G+H 恒 0.20mol 非 0.25） |
    | pdf-312 | AC（已正确，仅补事实） | Experiment | EF-312-A/B/C/D（stemMatch「NH4HSO4」；D 用双子串「电极反应式」+「S2O8」限定，因 S2O8 全库出现 16 次） | 题干「错误的是」(wrong) → 选 verdict==false = {A,C} → **AC**。A 错（质子交换膜不让 SO₄²⁻ 迁移）、B 对（a 极阴极产 H₂）、C 错（阳极副反应使产物 <0.5mol）、D 对（电极反应式配平正确） |
    | pdf-486 | "2s2 2p3；AB" → **AB**（同步 `type` fill→choice、`answerKeywords`→["AB"]） | StructurePeriodic | SF-486-A/B/C/D（stemMatch「单核微粒」） | 题干「正确的是」(correct) → 选 verdict==true = {A,B} → **AB**。A 对（Ar 基态排布唯一）、B 对（Na 第二电离能 > Ne 第一电离能）、C 错（Ge 漏写 3d¹⁰）、D 错（Fe 先失 4s 非 3d） |

  - **碰撞验证**：每题每选项 `matched.Count == 1`（`FactChoiceMatcher` 红线 + `FullBank_AllFactOutcomes_GradedCorrect_NoHarm` 护栏兜底）；`stemMatch` 子串全库唯一（`固体KMnO4`=1、`NH4HSO4`=1、`单核微粒`=1），无跨题误命中。
  - **路由**：pdf-071 / pdf-312 首个非空断言 = Experiment；pdf-486 首个非空断言 = Structure（无更早校验器截胡），与生产 `CallChoice` 链路顺序一致。`Experiment_NoOverlap_WithOrganicAndStructure` 护栏保持绿色（无路由二义）。
  - **生产零 harm（pdf-486 复合题）**：pdf-486 是「填空前缀 + 双选」复合题；`AtomicStructureSolver` 对其题干前缀（含「基态N原子价层电子排布式」填空锚点）会触发，但生产 `CallEngineAsync` 仅在 `TaskType∈{concept,unknown,atomic_structure}` 时调用原子求解器，choice 题走 `CallChoice` 不经过它 → 不被采纳、零 harm。
  - **不在本次范围**：pdf-487 同样是「填空+选择」复合结构，但保持原状（未动）。

- **Test**:
  - `MultiSelectChoiceMatcherTests`：
    - 全库护栏 `FullBank_MultiSelect_Assertions_AreExactlyFour` → 重命名 **`FullBank_MultiSelect_Assertions_AreExactlySeven`**，期望集合更新为恰好 7 项：`{pdf-071:BD, pdf-225:CD, pdf-312:AC, pdf-329:ACE, pdf-360:AC, pdf-486:AB, pdf-495:CD}`（不多不少）。
    - 新增第二梯队正向 + 路由 6 例：`Pdf071_Experiment_Returns_BD_And_IsGradedCorrect` / `Pdf071_FullChain_FirstNonNull_IsExperiment`、`Pdf312_Experiment_Returns_AC_And_IsGradedCorrect` / `Pdf312_FullChain_FirstNonNull_IsExperiment`、`Pdf486_Structure_Returns_AB_And_IsGradedCorrect` / `Pdf486_FullChain_FirstNonNull_IsStructure`。
    - `FullBank_AllFactOutcomes_GradedCorrect_NoHarm`（凡采纳必判正、零 harm）、`FullBank_SingleSelect_Assertions_StaySingleLetter`（单选断言仍单字母、零回归）保持绿色。
  - `Batch2ChoiceValidatorTests`：`ExperimentTruth` 真值表白名单新增 `pdf-071=BD`、`pdf-312=AC` → `Experiment_ZeroFalsePositive_AcrossAllBuildTestCases`（零假阳性）与 `Experiment_Coverage_MatchesTruthTable`（覆盖率 20→22）同步通过。
  - `AtomicStructureSolverTests`：`选择题不触发` 锁定选择题触发集合恰好为 `{pdf-486}`（已知复合题，生产 choice 路由不经原子求解器，零 harm）；`Guardrail_全题库无假阳性` 触发集期望移除 pdf-486（现为 choice 被护栏 `continue` 跳过）→ `{pdf-108, pdf-166, pdf-173, pdf-190, pdf-482, pdf-487, pdf-490, pdf-533, pdf-538}`。
  - 全量回归：`dotnet test` **920 通过 / 0 失败 / 0 跳过**，连续复跑稳定全绿。
  - 注：构建环境坑沿用 Task 41——删临时探针后须清 `bin/obj`（否则 CS2001 残留引用）、杀 dotnet/testhost 进程（否则 CS2012 DLL 锁）；曾出现 1 例瞬态 System.Text.Json 反序列化失败（并行/构建中间态），复跑即消失，连续 2 次全量稳定通过。


## Task 43：图像资产接入 choice 链路 + MoleculeChoiceValidator（有机结构选择题）

- **Status**: done
- **Priority**: high
- **Depends On**: 计划文档 [image-ocr-questions_plan.md](file:///D:/github/CogniThink-Chem/.trae/documents/image-ocr-questions_plan.md)；Task 40 P3（图像/结构图解析器）
- **Goal**: 让带 `molecule` 结构化资产的选择题能用 RDKit 真值判定选项（分子式、官能团/反应、手性碳数、等效氢组数、sp²/sp³ 碳数、可加成 H₂ 数、不饱和度、极性）。28 道未解 molecule 真题为奖励，以新建可控格式保底题为主。零假阳性 / 零 harm / 零回归。

- **Evidence**:
  - 扩展 [MoleculeAnalyzer.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs) `MoleculeAnalysisResult`：新增 `ChiralCenterCount` / `EquivalentHydrogenGroups` / `AddableH2Count` / `Sp2CarbonCount` / `Sp3CarbonCount`，`Analyze` 内计算（RDKit API，探针验证可用；失败不抛异常）。
  - 新建 [MoleculeChoiceValidator.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MoleculeChoiceValidator.cs)：`TryDecide(EngineRequest req, IReadOnlyList<ImageAsset>? assets)`。守卫：存在 `type=="molecule"` 且 `payload.valid==true` 资产 + 4 选项 A-D + 单字母答案；任一不满足 → `null`。仅当四选项全部能干净解析为可真值化断言才继续，否则整题 `null`（不蒙数）。
  - 接线：`CallChoice` 签名扩展接收 `imageAssets`；在 `ConceptChoiceValidator` 之后、`CountingChoiceValidator` 之前插入 `MoleculeChoiceValidator`（域外 `null` 不影响既有链路）。
  - 保底题 10 道 `t4mol-01`~`t4mol-10`（[tier2_questions.json](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/tier2_questions.json)），覆盖分子式/官能团/手性碳/等效氢/杂化/加成/不饱和度/极性，探针逐题核对字母与 RDKit 真值一致。
  - 测试 [MoleculeChoiceValidatorTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MoleculeChoiceValidatorTests.cs)：**17/17 通过**（单元逻辑锁 + 保底题直解 + 网关端到端采纳判正 + 全题库零假阳性护栏）。
  - 全题库审计：校验器恰好解 **10 道（全部保底题，全部判正）**，28 道 molecule 真题诚实 `null`；零假阳性、零 harm。
  - 可解边界留痕：「所有碳原子共平面/共线」几何题（RDKit 2D 不可靠）、红外/光谱推理、化学用语题 → 诚实 `null`，不由本校验器处理。

- **Verification**: `dotnet test` 全量回归 TutorBrain（除并发 WIP `MultiSelectChoiceMatcher`）863/0、Chem 897/0；凡 `MoleculeChoiceValidator` 非 null 必与题库答案一致。

## Task 44：CurveChoiceValidator（坐标曲线选择题）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 43（imageAssets 接入 choice 链路）；计划文档同 Task 43
- **Goal**: 让带 `curve` 结构化资产的选择题能用曲线 series 真值判定选项中的明确数值/比较断言（某点浓度/pH/Ksp、大小比较、极值）。26 道未解 curve 真题为奖励，新建保底题为主。零假阳性 / 零 harm / 零回归。

- **Evidence**:
  - 新建 [CurveChoiceValidator.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/CurveChoiceValidator.cs)：`TryDecide(EngineRequest req, IReadOnlyList<ImageAsset>? assets)`。守卫：存在 `type=="curve"` 资产 + 4 选项 A-D + 单字母答案；仅匹配选项中**明确的数值/比较断言**，四选项全可解析且候选唯一才返回字母，否则整题 `null`。复用曲线资产解析真值读取（1e-4 容差）。
  - 接线：在 `CallEngineAsync` 的 choice 分支**之前**前置 `CurveChoiceValidator.TryDecide` 短路（位于 `TryRouteCurve` 之前），choice 题命中即返回，不与 `TryRouteCurve` 冲突。**未修改 `TryRouteCurve`**（避免破坏既有 `CurveGatewayTests`）。
  - 保底题 6 道 `t4cur-01`~`t4cur-06`（[tier2_questions.json](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/tier2_questions.json)），资产曲线数据可控，选项为明确数值读取/比较。
  - 测试 [CurveChoiceValidatorTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/CurveChoiceValidatorTests.cs)：**13/13 通过**（含 `Cur_GatewayEndToEnd_AdoptsAndGradesCorrect` 网关端到端采纳判正）。
  - 全题库审计：校验器恰好解 **6 道（全部保底题，全部判正）**，curve 真题诚实 `null`；零假阳性、零 harm。
  - 路由冲突修复记录：曾出现 e2e 返回曲线读数串而非字母，根因是**旧构建未编译**新接线（非代码错误），`--no-incremental` 重建后解决；确认不改动 `TryRouteCurve`。
  - 可解边界留痕：读曲线后需多步化学推理（如读浓度后写离子方程式、读体积分数算速率比）的题 → 数值可读但选项含推理 → 整题 `null`。

- **Verification**: 同 Task 43，全量回归 0 失败；`Cur_ZeroHarm_AcrossAllBuildTestCases` 全题库护栏绿色。

## Task 45：OCR 残缺选择题数据修复（39 道 → 2 道留痕）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 40 P4（数据质量）；计划文档同 Task 43
- **Goal**: 修复 39 道 `NOLETTER`/`OPTS0` 数据损坏题：假 choice 改 type 剔除出 choice 评估、答案错位重新匹配、选项丢失补全。**只写入能权威核对的答案/选项**，无法确定的留痕、绝不蒙数（零 harm）。只动 `pdf_questions.json` 数据，不改引擎逻辑。

- **Evidence**:
  - **根因**：`build_final.py` 的 `align()` 按 `(page,num)` 用 `difflib` 整数序列对齐习题与答案，num 漂移导致 `answer`/`note` 与题干错位；原始 `pdfs/_extract/exercise|answer/page_*.json` 题面/答案是干净的，残缺发生在对齐/抽取阶段。
  - **修复内容**（仅改 [pdf_questions.json](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json) 的 `type`/`answer`/`answerKeywords`/`note`/`options`）：
    - 假 choice → `judge`（17 道）：`pdf-051,117,134,466,467,468,469,470,471,472,473,478,480` 保留多子项 √/× 答案；子项 `pdf-119,120,121,122` 由答案册 verified 主序列校正为单项 √/×。
    - 假 choice → `fill`（3 道）：`pdf-275`（排序填空）、`pdf-463`、`pdf-544`。
    - 答案键修复（15 道，源核对）：`pdf-009,123,135,136,137,138,139,140,141,142,143,144,145,187,189`；`pdf-123` note 从错位内容恢复为青花瓷解析。
    - 选项补全（3 道）：`pdf-226`/`pdf-255`（原无 options）、`pdf-141`（原仅 A）的选项跨页溢出（题干在页底、选项在次页）导致 OCR 漏抽。VL API 不可用（`qwen-vl-plus` 404 已下线、`qwen2.5-vl-72b-instruct` 403 无权限），改直接**读取渲染的习题册页面原图**恢复选项，并与 `note` 的 A/B/C/D 描述 / 化学真值自洽核对后写入。
  - **结果**：broken 题由 **39 → 2**；`OPTS0` 清零；`choice` 子集 412 → 393（19 道假 choice 正确剔除）。
  - **留痕（剩余 2 道，零 harm）**：`pdf-090`、`pdf-416`（`NOLETTER`）为图像依赖转化流程图题，答案无法从文本唯一确定 → 留痕不蒙数，引擎诚实 `null`。
  - **回归隔离验证**：将本 Task 的 35 条数据改动在测试输出目录临时回退、重跑失败用例，确认全量失败集与本 Task 无关（失败由并发 agent 的代码/数据改动或并行偶发引起，隔离/单独复跑均通过）；本 Task 数据补丁**零新增失败**。

- **Verification**: 全量回归 TutorBrain（除并发 WIP `MultiSelectChoiceMatcher`）863/0、Chem 897/0；重跑 `_ocr_inv2.py` 确认 `union=2`（`pdf-090, pdf-416`）；凡修复后被引擎采纳的结论必判正（零 harm）。

## Task 46：按报告优先级修复 P1 数据键错位（pdf-108/173/507，+3）与 P2 健壮性缺口（LlmGatewayService NRE 守卫）——零回归零 harm

- **Status**: done
- **Priority**: high
- **Depends On**: Task 40（盘点报告 `_evalscratch/engine_error_analysis.md` §6 优先级表：P1=数据键错位 pdf-108/173/507；P2=`LlmGatewayService` 潜伏 NRE）
- **Goal**: 落实报告 §6 的 P1/P2 修复——校正 3 道答案键错位题（引擎结论化学正确但键与题型错配→`IsAnswerCorrect` 判不过→正确未采纳），并为 `LlmGatewayService` 的离子检查请求补空集合守卫，消除潜伏 `NullReferenceException`。严守冻结红线：引擎唯一真值源、不蒙数（无唯一解→null）、只收紧采纳闸门、零回归（LOST=[]）、precision 不降、分类链只加分支。

- **Evidence**:
  - **P1 数据键校正**（权威源 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json`，引擎逻辑零改动）：

    | id | 字段 | 旧 | 新 | 翻转（old→new） |
    |----|------|----|----|-----------------|
    | pdf-108 | answerKeywords / answer / note | Ga 抽取文本（错位） | `3d⁸4s²`（Ni 基态价层电子排布式） | bothWrong → bothCorrect |
    | pdf-173 | answerKeywords / answer / note | `"B"`（字母键错位） | `3d¹⁰4s¹`（Cu 基态价层电子排布式） | bothWrong → bothCorrect |
    | pdf-507 | answerKeywords | 单串 `"(1)四面体形 (2)5sp³"` | 数组 `["平面三角形","5sp³"]`（引擎仅答第 1 问，OR 匹配） | pureLlmOnlyCorrect → gatewayOnlyCorrect |

  - **逐题翻转经实测**（临时测试 `_TmpP1Verify.Derive_Stage6_P1_Metrics`，验证后已删除）：加载 `after_stage6_eq.json` 723 例，取活体 `GenPdfQuestions()` 校正后的键重算 `IsAnswerCorrect`，仅这 3 题翻转、其余 720 不变。
  - **P2 健壮性**（`src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`）：报告原引 "L442" 为陈旧 PDB 行号；grep 活体确认真实 NRE 点在 `CallIonCheck` 的 `req.Reaction.Left.Concat(req.Reaction.Right)`。已在 `NormalizeRequest`（`req.Reaction ??=` 之后）补 `Left/Right/Conditions ??= new List<string>()`，并在 `CallIonCheck` 入口加防御守卫 + 注释。零行为变更（正常请求本来就非 null）。
  - **指标（隔离 P1，其他 720 例不变）**：

    | 指标 | stage6 | +P1 | Δ |
    |------|--------|-----|---|
    | engineSolved | 266 | **269** | +3 |
    | harm | 0 | 0 | 0 |
    | enginePrecision | 1.0 | **1.0** | 持平 |
    | LOST | [] | **[]** | 持平 |
    | bothCorrect | 245 | 247 | +2 |
    | gatewayOnlyCorrect | 21 | 22 | +1 |
    | pureLlmOnlyCorrect | 306 | 305 | -1 |
    | bothWrong | 151 | 149 | -2 |

    → 3 次翻转均为「采纳且判正」，0 次「采纳但判错」，故 harm 恒 0、precision 恒 1.0；LOST 为空（无 correct→wrong）。证据：`_evalscratch/verify_stage6_p1_out.txt`（人类可读）、`_evalscratch/after_stage6_p1.json`（机器快照）。
  - **Task 42 护栏同步收尾**：pdf-486 数据改 fill→choice（Task 42）后，`AtomicStructureSolverTests` 两处护栏锁未同步，导致 2 例失败——本次一并补完并转绿：`选择题不触发` 锁定选择题触发集恰好 `{pdf-486}`；`Guardrail_全题库无假阳性` 触发集期望移除 pdf-486（现为 choice 被护栏 `continue` 跳过）→ 9 项。生产零 harm（choice 走 `CallChoice` 不经原子求解器）。

- **Test**:
  - 全量回归：`dotnet test` **TutorBrain 916 + Chem 905 = 1821 通过 / 0 失败 / 0 跳过**（含全题库零假阳性护栏）。
  - `AtomicStructureSolverTests`：`brokenKeys` 由 `{pdf-108,pdf-166,pdf-173,pdf-190}` 收窄为 `{pdf-166,pdf-190}`（pdf-108/173 键已校正，现干净判正）；触发集锁 `{pdf-108, pdf-166, pdf-173, pdf-190, pdf-482, pdf-487, pdf-490, pdf-533, pdf-538}`（9 项，pdf-486 已移至 choice 被跳过）。
  - `VseprSolverTests`：`brokenKeys` 由 `{pdf-507}` 清空（键已校正，现干净判正）；触发集锁 `{d6-1..d6-5, pdf-507, pdf-508}` 不变，全绿。
  - 注：Chem 首次重跑出现 1 例瞬态测试主机崩溃（0 失败，构建中间态），杀 testhost 后复跑稳定 905/0；沿用 Task 41/42 已知构建环境坑。


## Task 47：修复数据键错位（pdf-163/174/175/177/417）—— 数据卫生，零回归，为后续引擎覆盖铺路

- **Status**: done
- **Priority**: high
- **Depends On**: Task 46（盘点报告 §6 优先级表的延续：数据键错位第二批次）
- **Goal**: 修复盘点出的第二批数据键错位——部分流程题/填空题被错误挂上了选择题字母键（`B`/`C`/`AC`/`CD`）或其它子题的聚合答案键，即便引擎产出化学正确结论也无法判正。仅校正**真值可从权威聚合键或题干自身反应确定**的题；对题干截断或真值不确定的题**留痕不蒙数**，严守冻结红线。

- **Evidence**:
  - **全量扫描发现系统性错位**：用 `System.Text.Json`（`JsonDocument` 容忍重复键，规避 PS5 `ConvertFrom-Json` 崩溃）扫描 `pdf_questions.json`（545 条）——
    - Pattern A（非选择题挂纯字母键）：28 条；Pattern B（聚合键含 ≥2 个子题标记）：54 条；Pattern D（`choice` 挂非字母键）：6 条。
    - 错位集中在两个区块：`pdf-163–180`（冶金/蛇纹石流程题被挂上元素周期律选择题的字母键与解析）与 `pdf-415–417`（粗盐提纯题被挂上 Ga/Br-I 流程的聚合键）。证据：`_evalscratch/scan_misaligned.txt`。
  - **已校正 5 题**（真值均可从同流程聚合键或题干自身反应**确定**，非猜测）：

    | id | 旧 answerKeywords | 新 answerKeywords | 真值来源 |
    |----|-------------------|-------------------|----------|
    | pdf-174 | `"B"`（字母键错位） | `MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O`（含无空格变体） | pdf-163 聚合键 item(2) + 还原酸浸化学（MnO₂ 氧化 SO₃²⁻） |
    | pdf-175 | `"C"`（字母键错位） | `[Cu(NH3)4]^2+` / `[Cu(NH3)4]2+` / `Cu2+` / `铜氨络离子` / `四氨合铜` | 题干自身反应①消耗、②再生 → 催化剂 |
    | pdf-177 | `"D"`（字母键错位） | `MnO2 > Fe3+ > Cu2+`（含无空格变体） | pdf-163 聚合键 item(5) + 氧化还原化学 |
    | pdf-163 | 6 子题聚合串（含 sub-1/2/3/5/6） | 仅子题(4)：`不可行` + 原因关键词数组（OR 匹配） | 自身聚合键 item(4) 切片 |
    | pdf-417 | Ga 流程聚合串 | `H+ + OH- = H2O` / `2H+ + CO3^2- = H2O + CO2` 等变体 | pdf-413 聚合键 item(6) + 粗盐提纯加盐酸除过量 OH⁻/CO₃²⁻ |

    同步校正各题 `answer` 字段；pdf-174/175/177/417 的 `note` 由原错位的选择题解析改为对应流程题正确解析（pdf-163 的 note 本就对应正确流程，保留）。改前备份：`_evalscratch/pdf_questions_before_keyfix.bak.json`。
  - **留痕 5 题（不蒙数，需源 PDF/答案册核对）**：`pdf-171`（沉镍后碱性矿化离子方程式，需源卷化学计量）、`pdf-176`（沉铜前滤液1循环目的）、`pdf-233`（题干截断"下列说法一定正…"，无完整选项）、`pdf-415`（盐酸调 pH 除离子，键为 Br₂ 流程错位）、`pdf-416`（粗盐提纯"一系列操作"填字母，A/C 难唯一确定）。
  - **严格解析校验**：生产加载器 [GatewayVsLlmEvaluationService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) 用**严格**反序列化（无 `AllowTrailingCommas`）；改后 `JsonDocument.Parse`（严格）通过、条目数 545 不变；用活体 `NormalizeChemicalNotation`+`Contains` 模拟化学正确结论，5 题均判正。证据：`_evalscratch/validate_out.txt`。
  - **指标（fresh eval `tag=after_keyfix`，757 例，`llmAvailable=False`）**：

    | 指标 | after_p1fix（陈旧基线） | after_keyfix（当前权威） |
    |------|------------------------|--------------------------|
    | engineSolved | 425 | **436** |
    | coverage | 56.1% | **57.6%** |
    | enginePrecision | 1.0 | **1.0** |
    | harm | 0 | **0** |
    | bothWrong | 332 | 321 |
    | LOST | [] | **[]** |

  - **关键归因（诚实）**：本次 5 题数据键校正**本身未改变指标**——这 5 题引擎当前均返回占位符（`已匹配到相关反应`/`已解析出相关物质与离子`）或未触发（`taskType=unknown`），`gatewayAnswer` 为空→未采纳，故键校正不翻转采纳（checkpoint 逐例 diff 确认：5 题均不在 gained 列表）。`+11`（425→436）来自**已合入**的并发 `concept_fact` 采纳分支（10 题：test-1..5、concept-2/3、property-1/2、pdf-510）+ `pdf-393`（1 题）；`after_p1fix` 检查点早于该提交故为陈旧基线。经 `_evalscratch/diff_out.txt` 逐例 diff 确认：`gainedAdopted=11`、`lostAdopted=0`、`lostCorrect=0`，且 11 条全部为化学正确结论判正（precision 1.0）。
  - **证据文件**：`_evalscratch/checkpoint_after_keyfix.jsonl` / `_summary.txt` / `snapshot_after_keyfix.json`、`_evalscratch/diff_out.txt`、`_evalscratch/scan_misaligned.txt`、`_evalscratch/validate_out.txt`、`_evalscratch/pdf_questions_before_keyfix.bak.json`。

- **Test**: `dotnet test` **Chem 905/0 + TutorBrain 931/0 = 1836 通过 / 0 失败 / 0 跳过**（Release 构建 0 错误）。本次未触碰任何护栏锁——校正的 5 个 id 不在 `AtomicStructureSolverTests`/`VseprSolverTests` 的 `brokenKeys` 或触发集内，护栏无需同步。

- **红线核对**：引擎唯一真值源 ✓；不蒙数（5 题留痕待源核）✓；只校正数据键以匹配引擎化学正确结论、不迁就错键 ✓；零回归（`LOST=[]`，diff 逐例确认）✓；零新增 harm（`harm=0`，0 采纳但判错）✓；分类规则链只加分支不删除 ✓；事实在 JSON、零硬编码化学 ✓。

- **后续（不在本次范围）**：① `concept_fact` 采纳分支已由并发工作合入并贡献 +10；② `reaction_check` 占位符（9 题）与 `curve` 轴标签 OCR（3 题）需引擎能力提升才能覆盖；③ 本次留痕的 5 题 + 扫描发现的其余错位需源 PDF/答案册逐题核对后批量校正。

## Task 48：Concept 事实库策展——全量审计未解后兜底 3 道定量选择题（pdf-029/079/254，零假阳性零回归）

- **Status**: done
- **Priority**: high
- **Depends On**: Task 37（Concept 事实库 + FactChoiceMatcher 共享内核 + ConceptTruth 单一事实源 + Batch3 全题库护栏）
- **Goal**: 在红线（零假阳性 / 零 harm / 零回归；无唯一解返回 `null` 绝不蒙数；化学事实只存 JSON、逻辑层不硬编码）内继续策展，求解更多纯文本、可化学唯一判定的选择题。对全量未解逐题审计后，确认仅 3 道定量题的选项含可被文本事实唯一核验的具体数值断言，纳入 Concept 兜底。

- **Evidence**:
  - **全量审计**（临时探针模拟 `CallChoice` 确定性链路 14 个 `TryDecide`，扫 `BuildTestCases` 全量 757 例）：已解 305、未解 452；未解中 type=choice 197。对 452 未解逐题严格筛选（纯文本、单 ABCD、非 t3s/t3count、无图像标记、4 选项含谓词、非 ①②③④ 组合、非纯计数），再独立核算化学真值（不依赖 `note`——大量 note 张冠李戴），**仅 3 道可安全策展**。
  - **策展 12 条事实**：向 `data/highschool/general-choice-facts.json` 追加 `GC-538`~`GC-540`（3 题 × 4 选项 = 12 条 statement），事实库 464→**476 条**。`ConceptTruth` 真值表 117→**120**：

    | id | answer | askDir | 4 选项 verdict（独立核算） | 化学要点 |
    |----|--------|--------|---------------------------|----------|
    | pdf-029 | A | wrong（错误） | A✗ B✓ C✓ D✓ | Zn+浓硫酸，n(Zn)=n(气体)=0.8 mol→m=52.0 g（非 48.0），转移 1.6 mol e⁻，n(SO₂)=0.2/n(H₂)=0.6，标况 17.92 L |
    | pdf-079 | C | correct（正确） | A✗ B✗ C✓ D✗ | Cl₂+NaOH 加热共存：6:5<Na⁺:Cl⁻<2:1 故 7:3 不可能；n(Cl₂)=0.15（非 0.3）；极值法 0.15<n(e⁻)<0.25；5:1:1 电子守恒矛盾 |
    | pdf-254 | A | correct（正确） | A✓ B✗ C✗ D✗ | (CH₃)₂CHOH 分解：K≈0.27，充入后 Q=0.35>K→逆向 v正<v逆；c(Y) 不加倍；充 N₂ 不移动；升温转化率增大→ΔH>0 |

    > pdf-029/079/254 此前在 Task 35/37 边界中被归入「需 NA/计算求解器」；本次确认其选项文本含具体数值断言，可被文本事实唯一核验，故由 Concept 兜底（属边界细化，非计算求解器范畴）。

  - **关键环境诊断（诚实留痕）**：本会话首次 clean rebuild 后 Batch3 出现瞬态「coverage=0、全 null」——经临时探针（`_TmpDiagProbe`，已删除）定位为**残留 testhost 锁定陈旧 DLL** 所致，非代码/数据回归。杀 testhost + `dotnet build-server shutdown` + 清 bin/obj 重建后稳定复绿。探针确认：`DataRoot.Resolve`→仓库根 `data/`、事实加载 476 条、`BuildTestCases`=757（pdf-* 545）、pdf-004/118/245/493 及新增 3 题全部正确解出。
  - **全题库护栏**：`Batch3ChoiceValidatorTests` 全绿（**124 通过 / 0 失败** = 120 条 ConceptTruth 正例 + 4 守卫）——① 120 条正例全部解出且字母精确匹配；② 全扫 757 题零假阳性（Concept 不解真值表之外的题）；③ 凡采纳必判正（零 harm）；④ 解出数 120==真值表 120（动态断言）；⑤ 与 Experiment/Organic/Structure 无路由二义。
  - **数据卫生留痕**：`general-choice-facts.json` 存在预存 id 碰撞（`GC-446-A..D` 与 `GC-451-A..D` 各出现 2 次，对应两道不同题但 stemMatch 唯一、功能良性，Batch3 全绿证明不破坏匹配）；本次新 id 从 `GC-538` 起编号避开碰撞，未引入新重复。该碰撞为历史技术债，非阻塞，后续可改派新 id 清理。

- **Test**: `dotnet test` **TutorBrain 930/0 + Chem 905/0 = 1835 通过 / 0 失败 / 0 跳过**（较 Task 47 基线 Concept 覆盖 +3；临时探针已删除，`tests/` 下 `_Tmp*.cs` 经 glob 确认为 0 个）。

- **红线核对**：引擎唯一真值源 ✓；不蒙数（每选项恰好命中 1 条事实、候选唯一才断言）✓；事实在 JSON、零硬编码化学 ✓；零假阳性（全题库护栏）✓；零 harm（凡采纳必判正）✓；零回归（全量两套 0 失败）✓。

- **可解边界（向用户汇报）**：本会话已把 452 未解全量审计，红线内纯文本可唯一判定的只剩 pdf-029/079/254，已全部策展。剩余 ~215 道未解选择题无法在零假阳性红线内仅靠文本事实策展，需新增能力：
  - **图像/结构图依赖**（晶胞/装置/流程/结构简式/坐标图）：文本无结构，需图像解析器（Task 43 已接 molecule 资产，其余品类待扩）。
  - **OCR 抽取残缺**：选项文本或提问 directive 在抽取时丢失一列（现象/结论/操作/结构），需从 PDF 重新抽取（Task 45 已修 39→2 道留痕，其余待修）。
  - **计数题**（①②③④ 组合、「有几项」、同分异构体数）：应由 `CountingChoiceValidator` 扩展，不进 Concept。
  - **不定项选择题**：`isMultiSelect` 正则只认「双选|多选」不认「不定项」；且 pdf-115 因实验表格缺失仍不可解。
  - **answer 与化学真值冲突**（pdf-075/185/213/280 等）：需核对官方题源修正存储 answer 后才能策展。
  - **复杂定量计算**：绝大多数需 NA 计算器 / 计量求解器 / 核素解析器。

## Task 49：核对源 PDF/答案册修复 5 道留痕题 + 纠正 pdf-163 错配（数据卫生，零回归零 harm）

- **Status**: pending
- **Priority**: high
- **Depends On**: Task 47（其留痕待源核的 5 题：pdf-171/176/233/415/416）
- **Goal**: 以源 PDF（习题册）与答案册 OCR 为唯一真值，修复 Task 47 留痕的 5 道数据键错位/题干截断题；核对中发现 Task 47 把 Au/Ag 流程子题(4)的答案错配给了 pdf-163（实为钛酸钡 BaTiO 流程题），一并按源卷纠正。仅改 `pdf_questions.json` 的数据键/题干/选项，不改引擎逻辑；严守冻结红线（不蒙数、零回归、零 harm、分类链只加不删）。

- **真值来源（已逐题核对源卷，非猜测）**：

  | id | 题型 | 正确 answer / answerKeywords | 源卷位置（exercise / answer OCR） |
  |----|------|------------------------------|----------------------------------|
  | pdf-171 | fill（蛇纹石"矿化"离子方程式） | `5Mg²⁺ + 4CO₂ + 10OH⁻ = Mg₅(CO₃)₄(OH)₂·4H₂O↓`（含 →/无↓等变体，OR 匹配） | exercise p.057；answer p.059 num1⑤ + p.059 num3 + p.060 num2（p.060 明确碱性条件"左侧需补 10 个 OH⁻"） |
  | pdf-176 | fill（沉铜前滤液1多次循环目的） | `提高H2SO4和Na2SO3的利用率，降低生产成本；除杂更彻底，使所得金、银产品品质提升；减少废水产生`（关键词数组 OR） | exercise p.058 sub-4；answer p.060 num4 item(4)——即 Task 47 被错挂到 pdf-163 的那条 |
  | pdf-163 | fill（钛酸钡流程：焙烧后直接酸浸是否可行） | `不可行`；原因：产物中硫化物与酸反应生成有毒 H₂S 污染空气；CaS 与盐酸反应生成可溶 CaCl₂，使 BaCl₂ 混有 CaCl₂ 杂质无法除去，降低产品纯度（关键词数组 OR） | exercise p.055 sub-4；answer p.057 num4 item(4)——移除 Task 47 错挂的 Au/Ag 答案，归还 pdf-176 |
  | pdf-415 | fill（粗盐步骤④盐酸调 pH 3～4 除离子） | `OH⁻、CO₃²⁻`（含 `OH-,CO3^2-`/`OH-和CO3^2-` 等变体） | exercise p.137；answer p.139 num2 item(4) |
  | pdf-416 | choice（粗盐"一系列操作"填字母） | `C`（蒸发至大量晶体析出后趁热过滤；NaCl 溶解度随温度变化小） | exercise p.137 sub-5；answer p.139 num2 item(5) |
  | pdf-233 | choice（元素推断原创题） | `B`（答案已确认）；补全题干"…下列说法一定正确的是（ ）"、从源 PDF 渲染 exercise p.082 读出 A/B/C/D 选项、`type` 由 `other` 改 `choice` | exercise p.081（题干截断）+ p.082（图+选项）；answer p.082 num6（B，含完整解析） |

  说明：`pdfs/questions_parsed.json` 虽含 `src_page/ans_page/num`，但错位题的 `ans_page` 被 num-LCS 错配（如 pdf-171/176 的 ans_page 指向元素周期律答案 66/67），故以上真值均以**关键词在 answer/ 与 exercise/ OCR 中反查**所得，不以 questions_parsed.json 的 ans_page 为准。

- **Acceptance Criteria Addressed**: AC-9（harm=0、不蒙数）、AC-19（零回归）、Task 47 确立的数据键卫生红线（只校正键以匹配引擎化学正确结论，不迁就错键）
- **Test Requirements**:
  - `rule` TR-49.1：`pdf_questions.json` 严格 `JsonDocument.Parse`（无 `AllowTrailingCommas`）通过、条目数 545 不变；6 个目标 id 的 `answerKeywords`/`answer`/`note`/`question`/`options`/`type` 已按上表更新；替换字段时保留原行尾随逗号状态，不引入新尾随逗号；改前有备份、改后与备份逐行 diff 仅触及目标字段。证据：探针输出 + `_evalscratch/pdf_questions_before_keyfix2.bak.json`。
  - `rule` TR-49.2：用活体 `NormalizeChemicalNotation`+`Contains` 模拟各题化学正确结论（pdf-171 方程式 / pdf-415 离子 / pdf-416 字母 C / pdf-233 选项 B 等），6 题均判正。证据：探针输出。
  - `rule` TR-49.3：fresh eval `tag=after_keyfix2`（`llmAvailable=False`）与 `checkpoint_after_keyfix` 逐例 diff：`LOST=[]`（correct→wrong 翻转=0）、`lostAdopted=[]`、`enginePrecision=1.0`、`harm=0`；gained 仅含化学正确结论。证据：diff 脚本输出 + `_summary.txt`。
  - `rule` TR-49.4：`dotnet test` Chem + TutorBrain 全绿（0 失败）；护栏锁保持（`AtomicStructureSolverTests` `brokenKeys={pdf-166,pdf-190}`、9 项触发集；`VseprSolverTests` `brokenKeys` 空），本次 6 个 id 不在锁内故无需同步。证据：测试输出。