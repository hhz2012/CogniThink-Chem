# 引擎能力缺口修复（LLM 关闭评估下的确定性扩展）- 产品需求文档

## Overview
- **Summary**：在 `verify-fix-all-suspects` 完成数据修复后，全库 757 题 eval（LLM 关闭）仍有 136 道未解；经根因分类，其中 **63 道属于「引擎能力缺口」**（数据正确、题型认对了，但求解器返回 null 或产出占位结论未被采纳）。本 spec 为这 63 道题制定分阶段修复计划：**Phase 1 低风险高收益**（选择/判断事实策展，零代码）→ **Phase 2 中风险**（方程书写题重路由到方程式书写求解器 + 占位噪声守卫收紧，需回归验证）→ **Phase 3 / Non-Goal**（多部分大题子问分发、纯计算域计算器等高风险项，标记为后续立项，不在本 spec 实施）。
- **Purpose**：在不破坏 `precision=1 / harm=0 / LOST=0 / 不蒙数` 红线的前提下，用确定性事实策展与受控路由修正，让引擎解出更多原本诚实答 null 的题；对无法零风险解出的题（多部分大题、纯计算、依赖图）诚实留痕、登记台账、不强行蒙数。
- **Target Users**：化学题库与求解引擎的维护者；本 spec 是 `verify-fix-all-suspects` 的后继。

## Background & Context

### 基线证据（已验证，不可动摇）
- 绿色基线：`_evalscratch/checkpoint_verify_fix_all.jsonl`（757 题，`ASPNETCORE_ENVIRONMENT=Production`，LLM 关闭）→ 解对 **621** / 未解对 **136**；**precision=1 / harmCount=0**。证据：[checkpoint_verify_fix_all_summary.txt](file:///d:/github/CogniThink-Chem/_evalscratch/checkpoint_verify_fix_all_summary.txt)。
- 单元测试基线：`dotnet test CogniThink.Chem.sln -c Release --no-restore` → **0 failed / 2144 passed**（TutorBrain.Tests 1233 + Chem.Tests 911）。任何修改不得破此基线。
- 136 道未解按根因分类（脚本 `_classify_unsolved2.ps1` 二级划分，区分 `taskType=unknown` 真路由缺口 vs `taskType` 具体但求解器返回 null 的引擎缺口）：

| 根因 | 数量 | 处置归属 |
|------|------|----------|
| 数据缺陷（blocked / 答案键冲突 / 题面碎片 / 缺图 / 选项损坏） | 45 | 已由 `verify-fix-all-suspects` 处理或登记台账，**不在本 spec** |
| 路由缺口（`taskType=unknown`，分类器没认出题型） | 28 | 后续「路由补齐」spec，**不在本 spec** |
| **引擎能力缺口**（数据正确、题型认对，但求解器返回 null 或占位未采纳） | **63** | **本 spec 范围** |

### 63 道引擎能力缺口的逐题诊断
逐题题面/选项/答案/note/gateway 响应已提取到 [_evalscratch/verify/gap63_diagnose.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/gap63_diagnose.txt)（62/63 成功，1 行因 PDF json 重复键 A/a 解析失败；found=63 / parseFail=1）。

**排除 2 道合成探针**：`trap-limit-2`（歧化比例计算）、`trap-limit-3`（与量有关离子方程式）—— note 标注「引擎局限」，非真题，**不计入本 spec 范围**。真实引擎能力缺口 = **61 道**。

按根因与可修复性分类（全部 61 道列全，LOST=0 精神：不静默遗漏任何一道）：

| 修复路径 | 数量 | 题号 | 风险 | 本 spec 阶段 |
|----------|------|------|------|-------------|
| 选择事实策展（单选，不依赖图、干净可唯一判定） | ~9 | pdf-090(NA)、pdf-147(实验安全)、pdf-155(沉淀定量)、pdf-188(化学用语)、pdf-196(氮循环·待核实 choice/calc)、pdf-319(有机分类)、pdf-405(实验操作结论)、pdf-416(操作顺序)、pdf-531(滴定操作·多选 BC) | 低 | **Phase 1** |
| 选择事实策展（依赖装置/结构图，图仅作示意、化学可独立推出） | ~7 | pdf-138(H₂S 装置)、pdf-255(平衡曲线)、pdf-271(滴定曲线)、pdf-303(电池)、pdf-340(有机手性)、pdf-357(有机)、pdf-406(气体装置) | 中（需读图辅助但结论靠化学真值） | Phase 1（探针通过才落库，否则留痕） |
| 判断陈述策展（多陈述 √/×） | 3 | pdf-134(15 陈述)、pdf-478(10)、pdf-480(10) | 低 | **Phase 1** |
| 方程书写误路由（被分到 calculation 返回 null，应进方程式书写求解器） | 5 | pdf-044(3 离子方程式)、pdf-084(2 化学方程式)、pdf-354(酯化)、pdf-526(交酯)、pdf-527(聚酯) | 中（路由变更需全库回归） | **Phase 2** |
| 占位/无关结论噪声（`usedEngine=True` 但 `adopted=False`，当前未造成 harm，但产出占位结论） | 4 | pdf-010(reaction_check·占位「已匹配到相关反应」)、pdf-163(reaction_check·占位「已解析出相关物质与离子」)、pdf-175(reaction_check·占位「已匹配到相关反应」)、pdf-272(equilibrium·曲线中间量 `y=0.8` 非最终答案) | 低-中（守卫收紧需回归） | Phase 2（可选，仅压噪声，不增解对数） |
| **多部分大题**（一题含 (1)(2)(3)… 异构子问：填空+选择+方程+计算+简答混合，单一确定性求解器无法处理） | ~27 | pdf-010、024、025、070、098、146、156、163、166、167、168、169、175、190、207、228、258、260、261、262、272、418、461、484、506、532、107 | 高（需新建子问分发架构，precision 风险高） | **Non-Goal**（后续立项） |
| **纯计算题**（引擎 KB 不覆盖的多步/公式计算，LLM 关闭下无计算器可解） | 5 | pdf-030(滴定质量分数)、pdf-244(键焓)、pdf-275(pH 排序)、pdf-328(李比希法分子式)、pdf-517(pH 计算) | 高（需新建域计算器） | **Non-Goal**（后续立项） |
| **自由填空概念题**（无填空概念求解器，choice 事实链不覆盖 fill） | 5 | pdf-274(除杂填空)、pdf-415(除离子填空)、pdf-464(溶质成分填空)、pdf-535(配制浓度填空)、pdf-543(调 pH 目的填空) | 高（需新建填空概念求解器） | **Non-Goal**（后续立项） |

> 注：4 道占位噪声题（010/163/175/272）同时也是多部分大题子问，Phase 2 仅做「占位→null」的噪声压制（不增解对），其真正求解仍属多部分大题 Non-Goal。

### 可复用基础设施（已调研）
- **事实策展工作流**：[chem-fact-curation SKILL.md](file:///d:/github/CogniThink-Chem/.trae/skills/chem-fact-curation/SKILL.md) —— 成熟 8 步批次策展，向事实库增量追加 JSON 事实解单选题，红线 precision=1/harm=0/LOST=0/不蒙数/事实只存 JSON/不改 WRONG 正则/NoOverlap 硬门。自带 `probe_template.py`、`unsolved_pool.py`。
- **事实库与校验器**（`data/highschool/` + `Application/Gateway/`）：
  - `ConceptChoiceValidator` → 加载 `general-choice-facts` 并**合并** `choice-curated-facts`（红线 #6 隔离，单题策展库不污染通用库）。
  - `ExperimentChoiceValidator` → `experiment-facts`；`OrganicChoiceValidator` → `organic-facts`；`StructurePeriodicValidator` → `structure-periodic-facts`。
  - 共享内核 [FactChoiceMatcher.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs)：`TryDecide`（单选：要求每个选项恰好命中 1 条事实，否则返回 null 不蒙数；多选/双选需题目标注且候选 ≥2）；`JudgeStatement`（陈述真伪，**合并全部 6 个 fact key**，命中冲突→Undecided 不判）。
  - 硬门：`Batch3ChoiceValidatorTests.Concept_NoOverlap_WithExperimentOrganicAndStructure`——Concept 非 null ⟹ Experiment/Organic/Structure 全 null。
- **方程式书写求解器已存在**：`ReactionKbWriterSolver`、`KnownEquationWriterSolver`、`ProductWriterSolver`（[Application/Gateway/](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/)）。
- **确定性题型分类器**：`LlmGatewayService.ClassifyTaskDeterministic`（规则链：choice → judgment → ion_check → balance → equilibrium → atomic_structure → calculation → reaction_check → …）。方程书写题（「书写/写出 + 离子方程式/化学方程式/酯化反应」）当前不命中任何规则 → 落 `unknown` → 由 LLM 兜底分到 `calculation` → 返回 null。
- **eval 与回归工具**：`_evalscratch/diff_eval.ps1`（regex 法权威 diff）、`cmp.py`、`screen_overlap.py`、`check_json_count.py`、`inspect_one.py`。

### 冻结红线（从父 spec 继承，不可破）
- `precision=1`：不得把原本正确的题判错/改错；新增事实不得让任何已解对的题变成错。
- `harm=0`：不得删除/覆盖正确数据；严禁清空正确对应本子问的 options；不得让引擎采纳一个错误结论。
- `LOST=0`：不遗漏真缺陷；61 道每题在本 spec 有明确处置结论（解出 / 留痕 Non-Goal / 探针不通过则跳过并登记）。
- `不蒙数`：某题选项无法唯一判定真假（两项同真/同假、或化学推理与答案键冲突）→ 跳过，绝不为凑数写事实；求解器无唯一解一律返回 null。
- `answer` 字段永不当真值；真值 = `answerKeywords[0]` + 官方答案册 + 化学真值三者收敛。
- 化学事实**只存 JSON**（`data/highschool/*.json`），**不硬编码进 `.cs`**。
- 千问 API key 不进提交；用户未要求不主动 commit。

## Goals
1. **Phase 1（低风险，零代码）**：用 chem-fact-curation 8 步流程，为可唯一判定的单选/判断题策展事实，新增解对 ≥6 题（选择 ≥5 + 判断 ≥1），全程 precision=1 / harm=0 / LOST=0 / NoOverlap 门绿。
2. **Phase 2（中风险，代码+回归）**：让「书写/写出 + 方程式」题确定性路由到方程式书写求解器（不再落 calculation 返回 null），新增解对 ≥2 题；收紧 4 道占位噪声守卫使引擎返回 null 而非无关结论；全库回归零回归。
3. **全量验证**：`dotnet test` 0 failed；eval diff 对绿色基线 `verify_fix_all` 满足 LOST=0 / precision=1 / harm=0；新增解对全部经答案键校验通过。
4. **台账闭环**：61 道中无法在本 spec 零风险解出的题（多部分大题 ~27 / 纯计算 ~5 / 自由填空 ~5），逐题或逐簇登记 `data-defect-register.md`，标注「引擎能力缺口·后续立项」与可接续方向，不静默遗漏。

## Non-Goals
- **不做多部分大题子问分发架构**（~27 道）：需新建「拆子问 → 逐子问路由 → 聚合判分」架构，precision 风险高，本 spec 不实施，登记为后续 spec。
- **不做纯计算域计算器**（~5 道）：LLM 关闭前提下自由形式多步计算无可复用确定性求解器，本 spec 不新建计算器，登记为后续 spec。
- **不做自由填空概念求解器**（~5 道）：`TryDecide` 仅服务 choice；fill 概念题需新链路，本 spec 不做。
- **不重开 LLM**：eval 始终 `ASPNETCORE_ENVIRONMENT=Production`（LLM 关闭），引擎是唯一真值源。
- **不做数据修复**：题库数据已在 `verify-fix-all-suspects` 修对；本 spec 不改 `pdf_questions.json` 题面/答案/options（台账登记的已修数据不动）。
- **不处理路由缺口 28 道**（`taskType=unknown`）：属分类器识别能力，另立项。
- **不把守恒近似路径接入 choice**（前序 AC-12 已证明有害）；不改 `MultiSelectChoiceValidator` / `FactChoiceMatcher` 的 WRONG 正则。

## Functional Requirements
- **FR-1（选择事实策展）**：按 chem-fact-curation 8 步流程，对 ~9 道不依赖图的单选题（090/147/155/188/196?/319/405/416/531）逐题：核状态 → 候选池 → 分诊（安全域进 `choice-curated-facts` 或 `general-choice-facts`；实验域进 `experiment-facts`；有机域进 `organic-facts`；结构域进 `structure-periodic-facts`，严守 NoOverlap）→ 独立核实 4-verdict（从题干+结构图独立推化学，不信 note「千问求解」）→ 预检防撞车 + screen_overlap → 探针（TOTAL PROBLEMS=0 才落库）→ 落库（Edit 增量追加，给原末条补尾逗号）→ 验证门（Batch3 全绿 + Chem 全绿 + eval LOST=0）。探针不通过的题跳过并留痕，不蒙数。
- **FR-2（判断陈述策展）**：对 pdf-134/478/480，把每条可唯一判定真伪的陈述写入 `judgment-statement-facts.json`（仅 `JudgeStatement` 加载，不参与 `TryDecide` 单选），让 `JudgmentStatementAggregator` 聚合输出 √/× 串；任一陈述无法唯一判定 → 该题不采纳（整题 null），不蒙数。
- **FR-3（依赖图选择题）**：对 ~7 道依赖图的选择题（138/255/271/303/340/357/406），用千问视觉读图辅助，但结论必须经化学真值交叉核对；探针 TOTAL PROBLEMS=0 才落库，存疑即跳过留痕，不臆造。
- **FR-4（方程书写重路由）**：在 `ClassifyTaskDeterministic` 的 `calculation` 规则**之前**，新增「书写/写出 +（离子方程式|化学方程式|方程式|酯化反应）」检测 → 路由到新 `equation_writer` taskType（或复用现有 balance/reaction 链路），由 `ReactionKbWriterSolver` / `KnownEquationWriterSolver` 求解；无解返回 null。同步在 `CallEngineAsync` / `BuildGatewayAnswerAsync` 接入该 taskType 的采纳门控（仅采纳通过答案键校验的结论）。
- **FR-5（方程书写 KB 扩充）**：为 pdf-044/084/354/526/527 中可由 KB 唯一确定的方程式，向 `data/highschool/` 对应反应知识 JSON 增量追加（或复用现有 `reactions-inorganic.json` / `reactions-organic.json`）；化学事实只存 JSON。KB 无法覆盖的题保持 null，诚实留痕。
- **FR-6（占位噪声守卫收紧）**：对 reaction_check 占位结论（「已匹配到相关反应」/「已解析出相关物质与离子」）与 equilibrium 曲线中间量结论，当结论不满足「是题目最终答案」签名时，求解器返回 null 而非产出占位（当前已 `adopted=False` 未造成 harm，本项只为降低噪声）。**不得放松采纳门控去接受占位结论。**
- **FR-7（台账同步）**：61 道中未在本 spec 解出的题，逐簇登记 `data-defect-register.md` 新类别（如 K 类「引擎能力缺口·后续立项」），含根因 + 建议方向（子问分发 / 域计算器 / 填空求解器 / 图像资产）。
- **FR-8（证据与可复现）**：每批改动留 eval checkpoint（`checkpoint_<tag>.jsonl` + summary）+ diff；所有数字与 `gap63_diagnose.txt` / checkpoint summary / 台账自洽。

## Non-Functional Requirements
- **NFR-1（precision=1）**：没有任何一道原本正确的题被新增事实/路由改判为错。
- **NFR-2（harm=0）**：不删除/覆盖正确数据；引擎不采纳任何错误结论；不得清空正确 options。
- **NFR-3（LOST=0）**：61 道每题有明确处置结论（解出 / 探针不通过跳过留痕 / Non-Goal 登记台账），不静默遗漏。
- **NFR-4（不蒙数）**：事实探针 TOTAL PROBLEMS≠0 不落库；求解器无唯一解返回 null。
- **NFR-5（事实只存 JSON）**：不硬编码化学事实进 `.cs`；路由/守卫代码不含化学判定真值。
- **NFR-6（零回归）**：非目标题结果与绿色基线 `verify_fix_all` 逐案一致（diff LOST=0）。
- **NFR-7（密钥不入库）**：千问 API key 不进入任何提交文件。

## Constraints
- **Technical**：Windows PowerShell 5（无 `&&`，用 `;`）；dotnet 8；含中文脚本/TSV/TXT 必须 `Get-Content -Raw -Encoding UTF8 | Invoke-Expression` 解码避免 GBK 乱码；eval 读数从 `AppContext.BaseDirectory/data/pdf_questions.json`（bin 输出），跑前 `Get-FileHash` 确认 bin 副本与源文件一致。
- **Business**：`precision=1 / harm=0 / LOST=0 / 不蒙数` 红线不可破；破则回退对应批次，任务退回 `in_progress`。
- **Dependencies**：chem-fact-curation skill 及其脚本；`RuleStore` 事实加载链路；`ClassifyTaskDeterministic` 规则链；eval 管线（EvalRunner + diff_eval.ps1）；千问 API（读图辅助，运行时 `QWEN_API_KEY` 注入）。
- **Risks**：① 路由变更（FR-4）影响全库 757 题 taskType → 必须全库回归验证，确认已解对的题 taskType 不漂移；② 依赖图题化学真值存疑 → 存疑即跳过，不臆造；③ judgment 陈述若题面抽取噪声导致匹配不稳 → 单题不采纳（null），不影响其它题。

## Assumptions
- `gap63_diagnose.txt` 的 62/63 提取准确；1 道 parseFail 不影响分类总量（其 taskType 已从 checkpoint 二级分类获得）。
- eval 可在本环境以 `ASPNETCORE_ENVIRONMENT=Production`（LLM 关闭）运行。
- chem-fact-curation 的探针（C# 对齐 norm + 码点转义 + stray-hit 检查）能精确模拟生产匹配行为。
- 方程式书写求解器（ReactionKbWriterSolver / KnownEquationWriterSolver）对 pdf-044/084 等标准反应可由 KB 覆盖；不可覆盖的保持 null 可接受。

## Acceptance Criteria

### AC-1：选择事实策展新增解对 ≥5，零误伤
- **Type**: `rule`
- **Given**: 绿色基线 `verify_fix_all`（gatewayCorrect=621），~9 道不依赖图的单选候选（090/147/155/188/319/405/416/531 + 196 待核实）
- **When**: 按 chem-fact-curation 8 步完成策展，跑 eval 产新 tag，与 `verify_fix_all` diff
- **Then**: 至少 5 道候选由 `correct=false` 翻转为 `correct=true && truthAdopted=true`；所有翻转均通过答案键校验；无任何已解对题翻转为错（LOST=0）
- **Pass Condition**: diff GAINED ≥5（仅选择域）；LOST=0；summary precision=1 / harm=0；Batch3 `Concept_NoOverlap` 通过
- **Evidence**: `checkpoint_<tag>_summary.txt` + `diff_verify_fix_all_<tag>.txt` + 探针 `TOTAL PROBLEMS=0` 输出

### AC-2：判断陈述策展新增解对 ≥1
- **Type**: `rule`
- **Given**: pdf-134/478/480（多陈述 √/×，当前 `correct=false`）
- **When**: 向 `judgment-statement-facts.json` 追加陈述事实，重跑 eval
- **Then**: 至少 1 道判断题翻转为 `correct=true && truthAdopted=true`，输出 √/× 串与答案键一致；其余无法干净判定的题保持 null（不蒙数）
- **Pass Condition**: 至少 1 道 judgment 题 GAINED；judgment 域 LOST=0、harm=0
- **Evidence**: checkpoint 对应记录 + 陈述事实探针输出

### AC-3：方程书写重路由新增解对 ≥2，零回归
- **Type**: `rule`
- **Given**: pdf-044/084/354/526/527（当前 taskType=calculation 且 `correct=false`）
- **When**: `ClassifyTaskDeterministic` 新增方程书写检测规则，路由到方程式书写求解器 + KB 扩充，重跑 eval
- **Then**: 至少 2 道由 `correct=false` 翻转为 `correct=true && truthAdopted=true`；全库 757 题的 taskType 无有害漂移（已解对题不丢失）；KB 不覆盖的题保持 null
- **Pass Condition**: diff GAINED ≥2（方程书写域）；LOST=0；precision=1 / harm=0；路由变更前后已解对 621 题逐案一致
- **Evidence**: checkpoint + diff + `ClassifyTaskDeterministic` 全库路由对照脚本输出

### AC-4：占位噪声守卫收紧（可选，仅压噪声）
- **Type**: `rule`
- **Given**: pdf-010/163/175（reaction_check 占位结论）、pdf-272（equilibrium 曲线中间量），当前 `usedEngine=True / adopted=False`（未造成 harm）
- **When**: 守卫收紧使求解器在结论非最终答案时返回 null
- **Then**: 这 4 题 `usedEngine` 变为 False（不再产出无关结论）；**解对数不变（不增不减）**；全库无回归
- **Pass Condition**: 4 题 gatewayConclusion 为空/null；diff LOST=0；precision=1 / harm=0
- **Evidence**: checkpoint 对应记录（若回归验证有风险，本项可降级跳过，不阻塞 AC-1/2/3/5）

### AC-5：全量验证红线零回归
- **Type**: `rule`
- **Given**: 全部 Phase 1+2 改动完成
- **When**: `dotnet test CogniThink.Chem.sln -c Release --no-restore`；eval 产终态 tag；`diff_eval.ps1 -OldTag verify_fix_all -NewTag <final>`
- **Then**: 测试 0 failed；eval diff LOST=0、precision=1、harm=0；新增解对 ≥8（AC-1≥5 + AC-2≥1 + AC-3≥2）且全部经答案键校验
- **Pass Condition**: 测试 failed=0；diff LOST=0；summary precision=1 / harm=0；GAINED 总数 ≥8 且每道均可追溯到事实/KB 落库证据
- **Evidence**: 测试汇总 + `checkpoint_<final>_summary.txt` + `diff_<final>.txt`

### AC-6：台账闭环与 LOST=0 留痕
- **Type**: `rubric`
- **Dimension**: 61 道引擎能力缺口题的处置可追溯性与无遗漏
- **Scale**: 1-5
- **Anchors**: 1 = 仅说「解不了」无明细；3 = 有分类但缺逐题/逐簇登记；5 = 61 道逐题有结论（解出 / 探针跳过留痕 / Non-Goal 登记台账含建议方向），与 `gap63_diagnose.txt` id 集合一一对应，0 静默遗漏
- **Pass Threshold**: >= 4
- **Evidence**: `data-defect-register.md` 新增类别行 + 处置清单与 diagnose id 集合比对脚本输出

### AC-7：密钥与代码红线
- **Type**: `rule`
- **Given**: 本 spec 全部改动
- **When**: `git diff` 与新增文件审查
- **Then**: 无千问 API key 明文；无化学事实硬编码进 `.cs`；未改 WRONG 正则；未把守恒近似接入 choice
- **Pass Condition**: grep 密钥片段 0 命中；事实落库仅在 `data/highschool/*.json`
- **Evidence**: grep 输出 + git diff 审查

## Open Questions
- [ ] 多部分大题子问分发架构是否应在下一个 spec 立项？（本 spec 已登记 Non-Goal，建议后续单独立项，因 precision 风险高需独立评审）
- [ ] 纯计算域计算器（滴定/键焓/pH/李比希法）的投入产出比？LLM 关闭下需逐个建确定性计算器，建议评估覆盖题数后再立项。
- [ ] pdf-196 当前 taskType=calculation 但答案为单字母 D，是否实为选择题被误标？Implement 阶段第一步先核实其 options 与真实题型。
- [ ] AC-4 守卫收紧若回归验证有风险，是否接受降级跳过（不阻塞主目标）？默认：有风险则跳过，保持当前 `adopted=False` 的安全状态。
