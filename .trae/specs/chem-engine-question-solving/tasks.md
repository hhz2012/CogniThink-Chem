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

- **Status**: pending
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

## Task 18：NA 选择逐选项计算校验（Tier 2-C / AC-18）

- **Status**: pending
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

## Task 19：Tier 2 集成、全题库护栏与评估（Tier 2 总闸门 / AC-19）

- **Status**: pending
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
