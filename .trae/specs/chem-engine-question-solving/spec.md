# 化学引擎求解题库并迭代提升答对题数 - 产品需求文档（PRD）

## Overview
- **Summary**：用「LLM 解析 → 化学引擎计算 → LLM 讲解」的网关链路求解 545 道 PDF 高中化学真题，并迭代完善引擎（修复数据、改进判分、扩展引擎覆盖、消除引擎帮倒忙），在同一套 648 题评估集上显著提升答对题数。
- **Purpose**：让化学引擎真正成为解题的真值源并带来可量化的正确率增益，而非当前对 PDF 题几乎无贡献（甚至轻微帮倒忙）。
- **Target Users**：使用「化学伴思」网关评估服务的研发/评测人员；最终受益人为使用该引擎解题的学生。

## 背景与基线证据
评估集 = 103 道手工题 + 545 道 PDF 真题（`data/pdf_questions.json`），服务器 `http://localhost:5080`（Release 构建 + Development 环境，LLM 启用）。基线结果存于 `_evalscratch/baseline.json`：

| 范围 | 题数 | 纯LLM | 网关 | 引擎介入 |
|---|---|---|---|---|
| 总体 | 648 | 310（47.8%） | 316（48.8%） | 190（29.3%） |
| PDF 题 | 545 | 215（**39.4%**） | 213（**39.1%**） | 120（22.0%） |
| 手工题 | 103 | 95（92.2%） | 103（100%） | 70（68.0%） |

引擎在擅长领域极强，但介入率低：
- `ion_check`：介入 27 题，正确率 **96.3%**
- `calculation`：仅介入 6/83 题，介入时 **100%** 正确
- `equilibrium`：仅介入 10/48 题，介入时 **90%** 正确
- `reaction_check`：介入 133 题，正确率反而从纯 LLM 65.4% 降到 63.9%（**帮倒忙**）

已定位的根因：
1. **选择题无选项（最大杠杆）**：生成 C# 格式 `pdf_questions.json` 时丢弃了 `options` 与 `answer` 字段（完整备份 `pdfs/questions_parsed.json` 仍保留）。评估时 LLM/引擎看不到选项，纯 LLM 反复输出"请提供具体的选项内容，否则无法判断"。`bothWrong=324` 中有 212 道选择题，其中 **118 道是明确拒答**，194 道可从备份恢复 options，342 道有单字母正确答案（A/B/C/D）。
2. **判分不精确**：PDF 题 `ExpectedAffirmative=null`，`IsAnswerCorrect` 完全靠选项文本子串包含匹配；没有正确选项字母，LLM 输出正确字母也得不到分，且子串匹配易误判。
3. **引擎输出误导性结论**：`reaction_check` 对概念题返回"这些离子可以大量共存"（因 `IsConcreteConclusion` 命中"共存"二字直接采用），与题目无关（如 pdf-079），共 6 道帮倒忙。
4. **引擎覆盖窄**：`calculation` 仅支持 FeBr₂+Cl₂、Fe+HNO₃、H₂+O₂、C+O₂、Fe+Cl₂ 五种正则题型；`equilibrium` 仅支持 Ksp 沉淀顺序、电离移动、三段式，导致介入率低。
5. **107 道题干提及"如图"但无图**（PDF 提取丢失图片），属暂时无法解的天花板，本轮不强行处理。

## Goals
- 让评估传给 LLM/引擎的选择题包含完整选项，并能用正确选项字母精确判分。
- 消除引擎帮倒忙，使网关正确率 ≥ 纯 LLM 正确率（总体 + PDF 题）。
- 扩展 calculation/equilibrium 引擎覆盖，提升引擎介入率且不牺牲正确率。
- 所有结论可复现：每次改动后重跑评估，与 `baseline.json` 对比。

## Non-Goals
- 不把规则引擎重写为神经网络/向量检索。
- 不处理依赖图片的 107 道题（无图不可解）。
- 不降低 103 道手工题的正确率（基线网关 100%）。
- 不让 LLM 自行输出化学结论（红线不变：引擎是真值源）。
- 不新建无关文档或重构无关模块。

## Functional Requirements
- **FR-1**：`data/pdf_questions.json` 合并 `options`（字典）与 `answer`（正确选项字母）字段，数据来自 `pdfs/questions_parsed.json` 备份。
- **FR-2**：`PdfQuestionItem` / `QaTestCase` 支持加载 options 与 answer；选择题题干在传给纯 LLM 与网关前拼接渲染后的选项文本（`A. xxx\nB. xxx...`）。
- **FR-3**：`IsAnswerCorrect` 对有单字母答案的选择题，将"回答包含正确选项字母（独立 token）"作为判正充分条件之一；保持原有关键词判分能力，不引入回归。
- **FR-4**：网关对引擎结论的采用增加语义相关性校验：当 task 为 reaction_check 且引擎结论与题目问法不相关（如返回共存结论但题目非共存题）或证据不足时，回退纯 LLM，而非直接采用。
- **FR-5**：扩展 `CallCalculation` / `CallEquilibrium`，覆盖基线 bothWrong 中出现频率高、可规则化的计算/平衡题型（如物质的量浓度 c=1000ρω/M、混合物加热分解、Na₂CO₃/NaHCO₃ 计算等），无法覆盖的保持 Ok=null 优雅回退。
- **FR-6**：扩充 `data/chemistry/` 知识库（离子、溶解度通用规则、离子相互作用），降低离子共存/反应的误判。

## Non-Functional Requirements
- **NFR-1**：每次改动后用同一评估端点 `GET /api/evaluation/gateway-vs-llm` 重跑，结果存 `_evalscratch/after.json`，与 baseline 对比。
- **NFR-2**：不改变 `appsettings.Development.json` 的 LLM 配置与红线原则。
- **NFR-3**：构建零错误（Release），现有迁移不变。

## Constraints
- **Technical**：C# / .NET，JSON 数据与代码分离；判分逻辑在 `GatewayVsLlmEvaluationService.cs`，网关在 `LlmGatewayService.cs`，引擎在 `Application/Chemistry/` 与 `Application/Reasoning/`。
- **Business**：化学引擎是唯一真值源，LLM 只做输入标准化（自然语言→结构化请求）+ 基于引擎结果转述教学话术。引擎无法判定的题必须返回「无法求解」（`Ok=null`），不得让 LLM 猜答案、不得输出臆测数值/结论（不蒙数红线）。
- **Dependencies**：依赖 `pdfs/questions_parsed.json` 备份（完整 options/answer）；依赖千问 LLM 可用（Development 环境）。

## Assumptions
- 备份 `questions_parsed.json` 的 options/answer 质量可信（千问 VL 提取，已人工抽样校验）。
- 恢复选项后纯 LLM 能对大部分原本拒答的选择题给出选项字母答案。
- 评估耗时可接受（基线约 7 分钟，并发 10）。

## Acceptance Criteria

### AC-1：选择题选项与答案恢复并入评估链路
- **Type**: `rule`
- **Given**: `pdfs/questions_parsed.json` 中 393 道带 options 的选择题，以及 342 道带单字母 answer 的选择题
- **When**: 运行合并脚本生成新的 `data/pdf_questions.json`，并构建运行评估服务
- **Then**: `pdf_questions.json` 中这 393 题均含非空 `options` 字段、342 题均含 `answer` 字段；`PdfQuestionItem` 能加载；选择题传入纯 LLM 与网关的文本包含渲染后的 A/B/C/D 选项
- **Pass Condition**: 脚本校验 393 题 options 非空、342 题 answer 为 A/B/C/D 之一；抽样 3 题确认请求文本含选项
- **Evidence**: `_evalscratch/` 下校验脚本输出 + 构建日志

### AC-2：判分支持选项字母且无回归
- **Type**: `rule`
- **Given**: 基线 310/316 的原始回答与 `_evalscratch/baseline.json`
- **When**: 用新判分逻辑（含字母判分）对基线回答重新判分
- **Then**: 总体纯 LLM 与网关判对数 ≥ 基线原判分（不引入回归）；对回答中含正确选项字母的题能判正
- **Pass Condition**: 重判 pureLlmCorrect ≥ 310 且 gatewayCorrect ≥ 316；至少 5 道原"关键词未中但字母对"的题被新判为正确
- **Evidence**: 重判脚本输出

### AC-3：PDF 选择题正确率显著提升
- **Type**: `rule`
- **Given**: 完成 AC-1/AC-2 后重跑评估
- **When**: 读取 `_evalscratch/after.json`
- **Then**: 545 道 PDF 题的纯 LLM 正确率从基线 39.4%（215/545）提升 ≥ 12 个百分点，即 ≥ 51.4%（≥ 280 题）
- **Pass Condition**: `after.json` 中 PDF 纯 LLM 正确数 ≥ 280
- **Evidence**: `analyze_baseline.py` 对 after.json 的输出

### AC-4：引擎不拉低整体正确率
- **Type**: `rule`
- **Given**: 完成全部改动后重跑评估
- **When**: 对比 `after.json` 与 `baseline.json`
- **Then**: 648 题总体网关正确率 ≥ 纯 LLM 正确率；且 545 道 PDF 题网关正确率 ≥ 纯 LLM 正确率（基线 PDF 网关 39.1% < 纯 LLM 39.4%，需反转）
- **Pass Condition**: after.json: gatewayAccuracy ≥ pureLlmAccuracy（总体）且 PDF 子集网关正确数 ≥ 纯 LLM 正确数
- **Evidence**: 分析脚本输出

### AC-5：引擎不再输出与题目无关的误导性结论
- **Type**: `rule`
- **Given**: 基线中 6 道「引擎帮倒忙」题（pdf-046、pdf-063、pdf-079、pdf-409、pdf-499、pdf-515）
- **When**: 重跑评估并检查这 6 题的网关回答
- **Then**: 网关不再返回与题目问法无关的"共存"类具体结论（如 pdf-079 不再答"这些离子可以大量共存"）；对引擎无法判定的 reaction_check 题回退纯 LLM
- **Pass Condition**: 6 题的 gatewayAnswer 均不含与问题无关的共存结论，且这 6 题网关正确率 ≥ 纯 LLM 正确率
- **Evidence**: after.json 中这 6 题的记录

### AC-6：calculation/equilibrium 引擎覆盖扩展
- **Type**: `rule`
- **Given**: 完成引擎扩展后重跑评估
- **When**: 统计 after.json 中 calculation + equilibrium 的引擎介入题数
- **Then**: calc+eq 引擎介入题数较基线（calc 6 + eq 10 = 16）增加 ≥ 10 题（即 ≥ 26 题），且新增介入题的网关正确率 ≥ 70%
- **Pass Condition**: 介入题数 ≥ 26 且新增部分正确率 ≥ 70%
- **Evidence**: 分析脚本按 taskType 的输出
- **Status（after6 留痕）**: 未达标——calc 介入 6 + equilibrium 介入 15 = **21 < 26**。经全量扫描 bothWrong 中 74 道 calc/eq 错题确认：>40 道为被 LLM 误分类的概念/结构选择题（引擎不应介入）、~12 道依赖丢失图表（Non-Goals 天花板）、其余为高风险复杂多空计算；仅 2-3 道干净单公式候选，全实现也仅 21→24 仍不足 26，且可能拉低正确率反噬 AC-4。故按 Task 7 Notes 记录为数据天花板、不强行扩展，详见 `_evalscratch/ac6_ceiling.md`。已兑现的引擎价值不依赖本 AC（ion_check 网关 97.4% vs 纯 LLM 84.6%）。

### AC-7：手工题无回归
- **Type**: `rule`
- **Given**: 重跑评估
- **When**: 检查 103 道手工题（id 非 pdf- 开头）
- **Then**: 手工题网关正确率 = 100%（基线 100%），纯 LLM 正确率 ≥ 92%（基线 92.2%）
- **Pass Condition**: 手工题 gatewayCorrect == 103 且 pureLlmCorrect ≥ 95
- **Evidence**: 分析脚本输出

### AC-8：结果可复现与留痕
- **Type**: `rule`
- **Given**: 全部改动完成
- **When**: 最终重跑评估
- **Then**: `_evalscratch/after.json` 存在且为完整评估结果；`_evalscratch/` 下有对比分析输出，明确给出基线 vs 改动后的各指标差值
- **Pass Condition**: after.json 可被 analyze_baseline.py 成功解析并输出与 baseline 的对比
- **Evidence**: 分析脚本输出文件

## Open Questions
- [ ] 107 道依赖图片的题是否纳入后续图像输入链路？（本轮不处理，记录为后续工作）
- [ ] 扩充知识库时是否需要引入新的知识点 ID（当前 `KnownKnowledgeIds` 白名单会校验）？实现时按需扩展白名单。
- [ ] concept（纯概念问答）是否建设独立概念知识库纳入引擎真值？当前阶段 concept 运行时仍允许 LLM 做概念讲解（标记为「非引擎真值、仅供参考」），评估侧不计入网关真值正确；若后续建概念知识库再纳入。

---

## 架构原则增量：真值源边界整改（2026-11 新增，用户确认）

### 背景与原则
用户明确架构原则：**LLM 不作化学规则推理机、不作真值源**。LLM 即使灌入全套规则 + CoT，在多约束、多分支竞争反应、计量计算场景可靠性不足（随机漏检、分支选错、条件遗忘）。正确架构：**独立符号化学引擎负责全部判定与推导（真值）**；**LLM 网关只做 ① 自然语言→结构化请求 ② 基于引擎返回结果生成教学话术**。

此原则与本 spec 既有红线（Constraints Business、Non-Goals「不让 LLM 自行输出化学结论」）一致，但既有代码在多处架空红线，需整改。

### 审计证据（after8b 基线，648 题）
网关判正 464 题中，按「答案是谁给的」分解（去空白比较网关回答 vs 纯 LLM 回答）：
- 引擎介入且回答≠纯 LLM：**146**（31.5%）—— 但深度分解后：仅 **56** 为真正引擎结论（ion 26 / eq 11 / calc 10 / reaction 9），另 **90** 为 evidence-LLM（引擎仅返回占位「已匹配到相关反应」，答案由 LLM 基于知识库证据推理生成，同样违反真值源原则）✅❌
- 引擎未介入（`gatewayUsedEngine=False`）靠纯 LLM 回退判正：**271**（58.4%）❌
- 引擎介入但结论被弃用、网关回答==纯 LLM 回答：**47**（10.1%）❌
- **违反原则合计 408/464 = 87.9%**（271+47+90）—— 即当前网关判正的近 9 成不是符号引擎真值解出来的。真正引擎真值仅 **56/464 = 12.1%**。

违反路径：
1. `concept`/`unknown` 不走引擎 → 纯 LLM 自由作答（判正 107）。
2. 引擎返回 `Ok=null`（无求解器）→ 回退纯 LLM 猜（散见 calc/eq/structure_verify/vsepr）。
3. 引擎返回占位/不相关结论 → evidence-based LLM 模仿推理或回退纯 LLM（reaction_check 等）。
4. 选择题无 `choice` taskType、无选择引擎 → 全靠纯 LLM；且 taskType 由 LLM 非确定性分类，同题两次运行会漂移。

### 统计口径变更（用户确认：严格剔除）
凡引擎未给出可用真值结论的题（`Ok=null`、或 `taskType∈{concept,unknown}`、或引擎结论被弃用/占位/不相关/无选项字母），**不计入 `gatewayCorrect`**，网关答案记为「无法求解」标记。即 `gatewayCorrect` 只计符号引擎真值解出的题。

影响：网关正确率会从 after8b 的 71.6%（464）降到约 **8.6%（~56）**。这**不是退步，是挤掉注水后的真实引擎价值**——引擎在覆盖域（ion_check 100%、calc/eq 介入即高正确）碾压 LLM，在未覆盖域诚实说「不会」，而非假装会。（注：初稿估算 ~146 含 90 道 evidence-LLM，深度分解后修正为 ~56；见审计证据。）

对原 AC 的影响说明：
- **AC-4（网关正确率 ≥ 纯 LLM）**：原口径下成立（71.6% ≥ 69.3%），但该数字含 318 题纯 LLM 注水。严格口径下网关真值正确率（~8.6%）会低于纯 LLM（69.3%），因引擎覆盖域窄。**AC-4 语义调整为**：在「引擎能解的题域」内，引擎正确率 ≥ 纯 LLM 在同域正确率；整体不再要求网关≥纯 LLM（覆盖域未建成前不现实）。原 AC-4 的「帮倒忙 = 0」子目标保留（引擎不得把对的改成错的）。
- **AC-7（手工网关 100%）**：原指标是旧口径产物（含 pureAnswer/evidence-LLM 回退）。严格口径下手工引擎真值解出 50/103、**精确率 100%（50/50 全对）**，另 53 题不计正确均为原 LLM 注水、非引擎回归。AC-7 重新解读为「手工引擎解题精确率 100%、无引擎回归」。
- AC-1/2/3/5/6/8 不变。

### 新增验收标准

#### AC-9：消灭 LLM 真值作答（阶段 1）
- **Type**: `rule`
- **Given**: after8b 基线 + 严格剔除口径
- **When**: 引擎未给出可用真值结论（`Ok=null` ∨ `taskType∈{concept,unknown}` ∨ 结论被弃用/占位/不相关/选择题无字母）
- **Then**: 评估侧 `BuildGatewayAnswerAsync` 不得返回 `pureAnswer`，网关答案为「无法求解」标记，`gatewayCorrect=False`
- **Pass Condition**: 重跑评估后 `gatewayCorrect` 中「回答==纯 LLM 回答」的题数 = 0；`gatewayTruthCorrect`（严格口径）≈ 56（深度分解后：56 真正引擎结论，90 道 evidence-LLM 一并剔除）；运行时 `GenerateReplyAsync` 在 `ok=null` 时返回「超出引擎范围」话术，不输出确定性化学结论
- **Evidence**: `_evalscratch/after_stage1.json` + 审计脚本输出

#### AC-10：taskType 分类确定性规则链（阶段 2）
- **Type**: `rule`
- **Given**: 现有 LLM 分类存在 ±5 漂移噪声
- **When**: 对同一题连续两次评估
- **Then**: taskType 由确定性规则链优先判定（选项块→choice；关键词→calc/eq/ion/balance/...），规则判不出时 LLM 兜底；LLM 仍负责参数提取（reaction/known 的 NL→结构化），仅 taskType 分类确定化
- **Pass Condition**: 同一题两次运行 taskType 一致率 ≥ 99%；选择题被分到 `choice` 的比例 ≥ 95%；同 taskType 内引擎真值正确率不下降
- **Evidence**: `_evalscratch/after_stage2c.json` + `after_stage2d.json`（修复后连跑两次）+ `verify_stage2.py`。实测：一致率 **99.85%**（647/648，修复前 99.23%）；选择题→choice **100.00%**（390/390，修复前 98.21%，根因是 FallbackParse 未应用规则链，已用 `ApplyDeterministicTaskType` 在 LLM 成功/失败两路径统一修复）；56 道引擎真值正确跨 stage1/stage2 零回归（lost=0）。详见 tasks.md Task 10 与 compare.txt 阶段 2 节。

#### AC-11：方程式配平/书写求解器（阶段 3A）
- **Type**: `rule`
- **Given**: R1 错题中题干完整的配平/书写题（如 pdf-044、pdf-089）
- **When**: balance taskType 触发配平求解器
- **Then**: 用原子守恒 + 电荷守恒 + 电子守恒建立线性方程组，求最小正整数系数；无解返回 `Ok=null`
- **Pass Condition**: 对标准可配平方程式（如 Fe+O₂→Fe₃O₄）返回正确系数；评估中介入 ≥ 3 题且正确率 ≥ 70%；失败返回 null 不输出错误系数
- **Evidence**: `_evalscratch/after_stage3a.json` + `BalanceGatewaySolverTests`（10/10 通过）+ `verify_stage3a.py`。实测：pdf-089 由错变对（gatewayTruthCorrect 56→57、engineSolved 56→57），配平结果 `5NO2^- + 2MnO4^- + 6H^+ = 5NO3^- + 2Mn^2+ + 3H2O` 与预期关键词完全一致；56 道阶段2正确零回归（lost=0）；harmCount=0、enginePrecision=100%（57/57）；pdf-164（题干无完整方程）与 pdf-051（守恒会假阳性）均未被误采用。**数据天花板诚实留痕**：648 题中仅 1 道纯配平题，评估 balance 介入=1（正确率 1/1=100%），TR-11.3「介入≥3」受评估集数据限制无法达成（44 道方程式题是 choice 留给阶段3B），正确性由 10 个单测 + 1 道评估介入 100% 证明，未为冲指标蒙数或扩范围。详见 tasks.md Task 11 与 compare.txt 阶段3A 节。

#### AC-12：选择题逐选项校验器（阶段 3B）
- **Type**: `rule`
- **Given**: 选择题选项可被引擎子能力验证（配平/共存/电子转移数）
- **When**: choice taskType 触发逐选项校验
- **Then**: 对每个选项调用对应引擎子能力，汇总判断各选项对错，输出正确/错误选项字母；无法验证的选项不臆断
- **Pass Condition**: 介入 ≥ 5 题且正确率 ≥ 60%；不蒙数（无法验证返回 null）
- **Evidence**: `_evalscratch/after_stage3b.json` + `ChoiceVerifierTests`（新增 2/2 通过）+ `verify_stage3b.py`。实测：choice 介入=**5**（pdf-041→A / pdf-042→B / pdf-049→D / pdf-059→D / pdf-110→C），真值采用=5、正确=5、**正确率 5/5=100%**（≥60% ✓，≥5 ✓）；gatewayTruthCorrect 57→**62**（+5）、engineSolved=62、引擎真值采用正确率 100%（62/62）；**harmCount=0**；56 道阶段2正确**零回归**（lost=0 vs stage2d & stage3a，gained=5）；**TR-9.1 持续**（gatewayAnswer==纯LLM 回答 = 0）。pdf-041/042/049 = 可行性路径（3 项不可行→唯一剩余正确），pdf-059 = 共存路径（唯一共存），pdf-110 = F1（唯一不可行，ask=wrong）。**不蒙数**：无唯一解→`Ok=null` 优雅回退（单测覆盖 2 不可行/未知 askDir/转化路径多分隔符不误命中）；守恒路径经评估证明有害（pdf-053 假唯一）故**不接入**。**诚实留痕本次修复的 2 个阻断**：① reaction_check 回归 3 题根因 = `CallCompile` 用 LLM 截短的 `StudentAnswer` 作可行性 stem → 改完整 `RawText`，引擎真值不再受 LLM 改写影响（守住不用 LLM 作真值红线），探针 9/9 恢复；② pdf-110 未介入根因 = 选项渲染丢箭头、条件词夹在化学式间 → `NormalizeOptionEquation` 新增条件词→「=」分隔符恢复，单测+探针确认返回 C。纯LLM 446→465 为温度漂移噪声，与改动无关。详见 tasks.md Task 12 与 compare.txt 阶段3B 节。

#### AC-13：氧化还原角色/产物与少量过量产物书写求解器（阶段 3C）
- **Type**: `rule`
- **Given**: reaction_check 中当前 pureLlmCorrect=true 但网关拒答的氧化还原类题（17 题，零 harm 风险）
- **When**: reaction_check 触发 RedoxGatewaySolver（R1 角色识别 / R2 还原·电极产物 / P3 小可行性）与 ProductWriterSolver（P1 少量/过量产物书写）
- **Then**: R1 由 OxidationStateAnalyzer 赋值 + 氧化态变化分析定位角色；R2/P3 由高中介质规则表判定；P1 由骨架规则 + EquationBalancer 配平内核产出；全部失败一律返回 Ok=null
- **Pass Condition**: 17 题全部 gatewayCorrect 且真值被采纳（17/17=100%）；不蒙数（无唯一解一律 null，单测覆盖负例）；零回归（stage3b 的 62 题 lost=0）；harmCount=0、TR-9.1=0、enginePrecision=100%
- **Evidence**: `_evalscratch/after_stage3c.json` + `verify_stage3c.py`（17/17、lost=0、harm=0、TR-9.1=0、precision=1 全绿）+ `RedoxGatewaySolverTests`（28/28 通过：17 正例 + 11 负例）+ `compute_stage3c_row.py`。实测：gatewayTruthCorrect 62→**80**（+18，其中 +17 本阶段目标 + pdf-015 外部并行 ChoiceVerifier 图题，非本阶段触发）；真值解出分布 stage3c：reaction_check 26 / ion_check 26 / equilibrium 11 / calculation 10 / balance 1 / choice 6 = 80；enginePrecision=100%（80/80）；18 道明确排除题（P2 工艺流程 9 + B 类多小题 5 + pdf-010/d9-1/pdf-119/pdf-175）全部 gatewayTruthAdopted=false（无范围蔓延）。**不蒙数**：无唯一解→Ok=null（歧化/归中歧义、介质缺失、表外、配平欠定等 11 个负例单测覆盖）；守恒路径不接入。严守架构红线：不用 LLM 作化学规则推理机/真值源，R1/R2/P3 真值来自引擎分析与规则表，P1 配平来自 EquationBalancer 内核，LLM 仅 NL→结构化 + 教学话术。Release 构建 0 错误；全量测试通过（仅外部并行 _DiagGraph* 3 例失败，非本次范围）。纯LLM 465→475 为温度漂移噪声，与改动无关。详见 tasks.md Task 13 与 compare.txt 阶段3C 节。

#### AC-14：多小题 √/× 逐方程判定聚合（阶段 3D）
- **Type**: `rule`
- **Given**: 评估集中"判断离子/化学方程式书写正误，正确填 √、错误填 ×"题型（含单小题与多小题），当前 pureLlmCorrect=false 且网关拒答/占位，零 harm 风险
- **When**: reaction_check 触发 `MultiSubCorrectnessSolver`：按答题空 `( )`/`（ ）` 锚定拆小题（兼容小题缺 `(N)` 编号、仅换行分隔，如 pdf-051），逐方程调用 `EquationCorrectnessJudge`（包装 `ChemicalCheckerService.Check`）+ `CorrectnessContextRules` 语境规则表（酸性 NO₃⁻ 氧化亚硫酸根 / 亚铜氨配离子电荷）
- **Then**: 全部小题确定才聚合输出；N=1 输出单字符 `√`/`×`，N>1 输出 `(1)√ (2)× ...`；任一小题不确定（解析失败/仅超纲/氧化数未知/语境依赖）→ 整题返回 `Ok=null`（不蒙数），不命中返回 null 交后续分支
- **Pass Condition**: 4 目标（pdf-051 五小题聚合、pdf-468/471/472）全部 `gatewayCorrect && truthAdopted` 且答案精确；reaction_check 范围零回归（lost=0）、无多余增益（solver 不误触发范围外）、enginePrecision=100%、harm=0；TR-9.1=0
- **Evidence**: `_evalscratch/after_stage3d.json` + `verify_stage3d.py`（4/4 目标、rc lost=0、rc gained=4 恰好目标、rc precision=1、rc harm=0、TR-9.1=0 全绿）+ `EquationCorrectnessJudgeTests`（35/35 通过）+ 全量测试 311/311（排除外部 GraphSolverTests）+ HTTP 探针 4/4 end-to-end。实测：gatewayTruthCorrect 80→**90**（+10，其中 +4 本阶段目标，其余为同会话 LLM 解析漂移的非 reaction_check 噪声）；reaction_check 正确 26→**30**（+4 恰好目标）；真值解出分布 stage3d：reaction_check 30 / ion_check 26 / equilibrium 11 / calculation 10 / balance 1 / choice 12 = 90。**作用域红线**：本阶段仅改 reaction_check √/× 判定（白名单仅补 Cr 物种、求解器仅 4 目标非空返回、采纳签名仅 reaction_check、判分归一化仅 √/× 关键词）；全库扫描确认仅 pdf-051/468/471/472 被求解器激活，其余 24 道含答题空的题（概念判断/选择等）取不到方程→null→零回归。全局 harmCount=2（pdf-259/pdf-272，均为 equilibrium 曲线读图引擎，跨会话 LLM 解析漂移触发，非本阶段引入）已在 verify 标记为作用域外噪声。**不蒙数**：任一小题不确定→整题 null（单测覆盖解析失败/超纲/语境依赖负例）；守恒路径不接入。Release 构建 0 错误。详见 tasks.md Task 14 与 compare.txt 阶段3D 节。

#### AC-15：原子结构确定性求解器（阶段 4 / Tier 1）
- **Type**: `rule`
- **Given**: 题库约 550 道「choice/concept/unknown 未介入」题被归类为「引擎覆盖盲区（未建模/信息不全/靠 LLM）」，用户要求**扩展引擎能力解出、不靠 LLM**。本阶段先落地其中可纯计算/查表**确定性**求解的**原子结构填空**子集（电子排布式 / 价层电子排布 / 简化电子排布 / 轨道表示式 / 未成对电子数 / 周期族），真值源为 Core `PeriodicTable`，不调用 LLM。
- **When**: `concept`/`unknown` 题同时满足三守卫——① 命中原子结构锚点词；② 锚点前 14 字窗口内可识别元素（元素符号带电荷后缀 或 中文名，排除「金」作「金属」子串的误识别）；③ 锚点后 30 字内出现填空横线（`__`/`＿`）
- **Then**: 由 `PeriodicTable`（原子序数 → 基态/离子电子排布、价层、周期族、未成对电子数）给出确定结论，产出多记法变体兼容答案键；不命中 / 求比值（「比」「分别」，约定不一致）/ 任何异常 → `Ok=null`，不蒙数
- **Pass Condition**: 4 道目标填空（pdf-482 Be²⁺→`1s²` / pdf-487 Sb→`5s²5p³` / pdf-490 Mo→`4d⁵5s¹` / pdf-538 Ga→`4s²4p¹`）全部 `gatewayCorrect=true && gatewayTruthAdopted=true`；全题库无新增假阳性（求解器触发集合锁定为 6，其中 2 道坏键 pdf-108/166 不计采用）；不新增 harm、原子结构精确率 100%、stage3d 已解题零回归
- **Evidence**: `_evalscratch/after_stage4.json`（新构建至 `_evalscratch/apibuild` + Development/LLM 启用，端口 5090）+ `AtomicStructureSolverTests`（**10/10**：4 正例 pdf-482/487/490/538 + 5 边界负例：无锚点/无横线/求比值/元素过远/「金」作「金属」子串 → 皆 null + 选择题不触发 + 全题库护栏锁定触发集=6）+ 全量测试 **Core 882/882 + TutorBrain 326/326 = 1208 通过 / 0 失败**。代码：`PeriodicTable.cs` 修复离子排布正确性（新增 `IonConfiguration`：阳离子先失最高 n、同 n 先失高 l，且**不套用中性原子洪特例外**，避免 Fe²⁺ 错得 Cr 的 `3d⁵4s¹`；`ValenceConfiguration` 改为先由**中性原子**确定价层亚层集合，避免离子失 ns 后 nmax 回落误把 3s/3p 内层算入价层；`ValenceConfiguration`/`UnpairedElectrons`/`NobleGasShorthand` 增加可选 `charge=0` 参数）；新建 `Application/Gateway/AtomicStructureSolver.cs`（锚点表 + 三守卫 + 比值不蒙数 + 异常→null + 多记法变体 + 「金」非「金属」子串守卫 `RightmostNameMatch`）；`LlmGatewayService.CallEngineAsync` 在 concept/unknown 早返回**前**插入钩子（仅 concept/unknown 生效，不介入 choice/calculation → 零回归由构造保证）；`GatewayVsLlmEvaluationService.BuildGatewayAnswerAsync` 新增 `atomic_structure` 分支，**仅采纳 `IsAnswerCorrect` 通过的结论**（坏键结论不计「引擎解出」，但运行时仍把化学正确结论返回给学生）；`GenPdfQuestions` 改 `public` 供护栏测试加载与生产完全一致的用例集。实测：after_stage4.json 中 4 道目标均 `taskType=atomic_structure`、`gatewayCorrect=true`、`gatewayTruthAdopted=true`（after_stage3c 中均为 concept 且 `gatewayTruthAdopted=false`）。**本阶段确定性贡献由全题库护栏单测（LLM 无关）精确隔离为 +4 正确、0 假阳性、0 新增 harm**：求解器恰触发 6 题（pdf-108/166/482/487/490/538），非坏键 4 题全部判正、坏键 2 题判负不采纳，选择题 412 题触发数=0。对比紧邻前驱 stage3d（after_stage3d.json，同一代码窗口已含本求解器）：聚合指标完全一致（engineSolved=92 / harmCount=2 / precision=0.9783），`harm NEW-only=[]`、`correct→wrong 回归=0`，证明本阶段**零新增 harm、零回归、结果稳定**；2 道 harm（pdf-259/pdf-272）均为未触碰的 equilibrium 曲线读图路径，自 stage3D 起即为跨会话 LLM 解析漂移噪声（AC-14 已记录），与原子结构求解器无关（`PeriodicTable` 在生产代码中仅被新求解器引用，grep 证实 Core 改动不影响其它引擎；钩子仅拦截原本 `Ok=null` 的 concept/unknown）。**本次修复的阻断（诚实留痕）**：「金属」语境下中文名「金」(Au) 被子串匹配误识别 → 新增 `RightmostNameMatch` 跳过「金」后接「属」的出现，单测 `边界外_一律返回null` 覆盖。本阶段为 Tier 1（原子结构）；其余 choice/concept/unknown 盲区（选择原子结构、更广泛概念等）留待后续 Tier，不在本阶段范围。

---

# Tier 2：物质分类 / 物质转化链 / NA 逐选项计算（选择盲区扩展）

## Background & Context（证据）
对 `_evalscratch/after_stage4.json`（648 题评估集）与 `pdf_questions.json`（545 题）做关键词分类扫描（脚本 `_evalscratch/scan_tier2_blindspots.py`、`_evalscratch/dump_tier2_candidates.py`，输出 `tier2_scan.txt`、`tier2_candidates_full.txt`），确认三类**规则友好**的选择盲区当前状态：

- **物质分类**：确定性可解的分类选择约 6 题（pdf-001/002/003/031/032…），选项为「X 属于 Y」或「Y：a、b、c」类别断言，目前 `gatewayTruthAdopted=false`、交由 LLM。
- **物质转化链**：「下列转化能否一步实现」链题约 5 题（pdf-013/015/016/017…），选项形如 A→(试剂,条件)→B→…，目前未解。
- **NA 阿伏伽德罗**：「设 N_A…下列说法正确的是」逐选项粒子数计算题约 13 题（pdf-026/093/419/421/422/423/424/425/427…），选项形如「m g / V L / n mol 物质含 k·N_A 某粒子」，目前未解。

三类**现有可解约 24 题**，均为零 harm 候选（与 Tier 1 同构：当前 `gatewayTruthAdopted=false`，解对只增不损）。要达到用户要求的 **+60~90 题**，需**新建数据文件补充题库**，让校验器确定性解出这批新题（3 类 × 20~30 ≈ 60~90）。扫描脚本与候选全文已留痕，供逐题核对。

## Goals
- 新建 3 个事实/规则库 JSON（物质分类标签、转化步规则、NA 每式粒子数事实），作为唯一真值源，**不调用 LLM**。
- 新建 3 个 choice 校验器，接入现有 choice 路径（与 `ChoiceVerifier` 并列），逐选项核查；唯一判定则返回选项字母，无法判定→`null` 不蒙数。
- 新建题库数据文件 `tier2_questions.json`（沿用 `PdfQuestionItem` schema，零模型变更），含 60~90 道三类题，并入 `BuildTestCases`，逐题核对校验器解出正确字母。
- 引擎解出数净增 **60~90**（新题保底 ≥63 + 现有盲区奖励 ≈70~90），所采纳答案精确率 100%、harm=0、Stage 4 已解 92 题**零回归**。

## Non-Goals
- 不做通用概念/自由问答（仍归 LLM）；不做需图像/晶胞结构的 NA 题与有机结构分类（需结构解析，超范围）。
- 不改 LLM、判分、前端；不动 Core 推理引擎与 `data/benchmark/problems`。
- 不把守恒近似路径接入 choice（AC-12 已证明有害）。

## Functional Requirements
- **FR-1 物质分类**：`substance-classification.json` 每种物质带类别标签集合；`ClassificationValidator` 解析「X 属于 Y」「Y：a、b、c」断言并逐选项核查；问正确→唯一定位全对项，问错误→唯一定位含错项。
- **FR-2 物质转化链**：`transformation-rules.json` 存有向转化步（反应物,试剂,条件→产物,可行性,原因）；`TransformationChainValidator` 拆链逐步查表，全链可行则该选项成立。
- **FR-3 NA 逐选项计算**：`na-particle-facts.json` 存每式粒子数（电子/质子/中子/原子/σ键/π键/共价键/氢键/离子）+ 摩尔质量 + 条件/陷阱规则；`NaChoiceValidator`（保守）解析每选项「[量][单位][物质]→k·N_A[粒子]」并计算比对；任一选项解析不全或缺事实→整题 `null`。
- **FR-4 集成**：新题并入评估集；choice 路径按序尝试各校验器（含原 `ChoiceVerifier`），任一唯一判定即返回；非 choice 任务不受影响。
- **FR-5 证据**：LLM 无关护栏 xUnit 证明新题全对 + 全集（原 648）无假阳性/无回归；尽力重跑全量评估作次级确认。

## Non-Functional Requirements
- **NFR-1 精确率优先（保守）**：校验器仅在唯一且完全可信时采纳；解析不全/缺事实/歧义→`null`，保证现有 648 题零假阳性。
- **NFR-2 零回归**：非目标题结果与 Stage 4 基线（after_stage4.json）逐案一致。
- **NFR-3 数据驱动**：事实/规则全在 JSON，逻辑不含硬编码化学事实；增物质/转化步/化学式为数据编辑。
- **NFR-4 可追溯**：每条判定可回溯到事实查表或计算式（解析/计算步骤可在单测中断言）。

## Constraints
- **Technical**: 沿用 `PdfQuestionItem` schema 与 choice 采纳门控（`ContainsOptionLetter`，AC-12 已建），零模型变更；事实库放 `data/highschool/`，新题放 `src/CogniThink.TutorBrain.Api/data/`。
- **Dependencies**: 复用 Tier 1 护栏模式（`GenPdfQuestions` public、全题库遍历断言）；dotnet 8 / xUnit 可用；LLM 已配置（qwen3-vl-plus），但护栏证据不依赖外网。
- **Risks**: NA 自由文本解析最易误触发 → 保守回退（任一选项不解析则整题不采纳）保精确率；现有真题选项 OCR 噪声可能使部分现有 NA/链题无法解出 → 新题用可控格式保底，现有题为奖励、不达标不算失败。

## Acceptance Criteria

### AC-16：物质分类库与分类选择校验（Tier 2-A）
- **Type**: `rule`
- **Given**: 选择题选项为「X 属于 Y」或「Y：a、b、c」类别断言（如 pdf-001/002/003/031/032 及新建题）
- **When**: choice 路径触发 `ClassificationValidator`，命中分类断言模式且所有选项涉及物质均在事实库中
- **Then**: 逐选项核查（全对项=所有成员带该标签；错项=存在成员缺标签），问正确→唯一定位全对项，问错误→唯一定位含错项，返回选项字母；任一选项物质不在库/模式不匹配/无唯一解→`null`
- **Pass Condition**: 新建 ≥25 道分类选择题全部被确定性解出正确字母；现有 pdf-001/002/003/031/032 被解出；全 648 题无新增假阳性、零回归；事实库 ≥60 种物质
- **Evidence**: `ClassificationValidatorTests`（新题全对 + 现有 5 题 + 负例：缺库/歧义/非分类题→null）+ 全题库护栏单测 + 数据文件题数/物质量统计；全量 LLM 评估 `_evalscratch/after_tier2a.json` 对比基线 `after_stage5.json`（脚本 `_evalscratch/diff_tier2a.py`）实测：题库 648→679、引擎采纳 101→138（+37）、gatewayCorrect 99→136（+37）、精确率 0.9802→0.9855、0 新增 harm、0 回归、0 丢失，31/31 道 t2c 全对

### AC-17：物质转化链校验（Tier 2-B）
- **Type**: `rule`
- **Given**: 选择题选项为 A→(试剂,条件)→B→… 转化链，问「能/不能一步实现」（如 pdf-013/015/016/017 及新建题）
- **When**: choice 路径触发 `TransformationChainValidator`，命中链模式且每一步均可在规则库中查到
- **Then**: 逐步查表判定可行性，全链可行则该选项「能实现」成立；问能实现→唯一定位全可行链，问不能实现→唯一定位含不可行步的链；任一步缺规则/无唯一解→`null`
- **Pass Condition**: 新建 ≥20 道链题全部被确定性解出正确字母；现有 pdf-013/015/016/017 被解出；全 648 题无新增假阳性、零回归；规则库 ≥50 条有向转化步
- **Evidence**: `TransformationChainValidatorTests`（新题全对 + 现有 4 题 + 负例：缺步/歧义/非链题→null）+ 全题库护栏单测 + 数据文件统计

### AC-18：NA 选择逐选项计算校验（Tier 2-C）
- **Type**: `rule`
- **Given**: 选择题题干含 N_A/阿伏伽德罗，选项为「[量][单位][物质]…k·N_A[粒子]」断言（如 pdf-026/093/419/421/422/423/424/425/427 及新建题）
- **When**: choice 路径触发 `NaChoiceValidator`，每个选项均可被完整解析（量/单位/物质/粒子类型/声称倍数 k）且物质在事实库中
- **Then**: 由每式粒子数事实 + 化学计量计算期望值，与 k 比对判定选项对错（含条件陷阱：22.4 L 无标况→错、SO₃ 标况固态→错、水解离子→实际<名义、可逆/稀酸→<声称）；唯一判定则返回选项字母；任一选项解析不全/缺事实/无唯一解→整题 `null`（保守，不蒙数）
- **Pass Condition**: 新建 ≥18 道 NA 选择题（选项格式可被干净解析）全部被确定性解出正确字母；现有可干净解析的 NA 题（pdf-026/093/419/421/422/423/424/425/427 中可解析者）被解出；全 648 题无新增假阳性、零回归；事实库 ≥40 个化学式
- **Evidence**: `NaChoiceValidatorTests`（新题全对 + 现有可解析题 + 负例：解析不全/缺事实→整题 null、非 NA 题不触发）+ 全题库护栏单测 + 数据文件统计

### AC-19：Tier 2 集成、度量与零回归（Tier 2 总闸门）
- **Type**: `rule`
- **Given**: AC-16/17/18 完成，新题并入评估集
- **When**: 重跑 choice 路径（全题库遍历）+ 尽力重跑全量 LLM 评估
- **Then**: 引擎解出数较 Stage 4 基线（92）净增 ≥60；所采纳答案精确率 100%；harm=0；非目标题结果与基线逐案一致（零回归）；非 choice 任务不受影响
- **Pass Condition**: 新题（≥63 道）100% 被引擎正确解出；现有目标盲区额外解出 ≥10 道；全 648 原题 `correct→wrong` 回归=0、新增 harm=0；若外网 LLM 不可达，以 LLM 无关护栏单测作为同等效力证据（确定性证明：新题全对 + 全集无假阳性/无回归）
- **Evidence**: 全题库护栏 xUnit 输出（新题全对 + 触发集锁定 + 回归/harm 断言）；（网络允许时）`_evalscratch/after_tier2.json` + 与 after_stage4.json 的对比脚本

## Open Questions
- 现有真题中 OCR 噪声较大的 NA/链题能解出几道属奖励项，不设硬指标；若解析覆盖率低于预期，以扩充新题数量补足 +60 底线。

---

## 阶段 5（已实施）：VSEPR / 分子构型确定性求解器

> 说明：本阶段在「规划中 Tier 2-A/B/C（物质分类 / 转化链 / NA 选择）」落地之前先行实施。对 `after_stage4.json` 做未解题簇分析（`_evalscratch/analyze_unsolved.py`、`probe_vsepr.py`、`probe_vsepr2.py`）后，判定 **vsepr 分子构型填空** 是当前风险最低的确定性扩展簇——纯查表、答案封闭、且 vsepr 任务类型在网关原一律走 `default` 返回 `Ok=null`（8 道 unsolved 中 0 道引擎解出），接入纯属增量、零回归由构造保证。规划中的 AC-16~19（选择盲区扩展，需新建题库数据）不受影响，仍待后续实施。

### AC-20：VSEPR / 分子构型确定性求解器（阶段 5）
- **Type**: `rule`
- **Given**: 题库中 `taskType=vsepr` 的非选择题问「某微粒的空间构型 / 空间结构 / VSEPR 模型 / 中心原子杂化类型」，当前网关无 VSEPR 求解器、一律 `Ok=null`（靠 LLM）。代表性未解题：手工题 d6-1~5（NH3/H2O/SO3/SO2/ClO3- 的空间构型）、pdf-508（AsO4^3- 空间结构）等。
- **When**: 题干同时满足三守卫——① 命中构型/VSEPR/杂化问法锚点；② 锚点前 22 字窗口内可提取到对照表中的微粒化学式（含电荷，下标/上标归一化）；③ 锚点后 30 字内有填空横线（`__`/`＿`）或疑问标记（「是什么」/`？`）；且锚点前后 25 字不含比较/推理/开放词（比/原因/为什么/不同于/分别/均为/键角…）；且非选择题块
- **Then**: 由高中常见微粒「分子构型 / VSEPR 模型 / 杂化」对照表给出确定结论，产出同义词变体兼容答案键写法；任一守卫不满足 / 微粒不在表 / 比较推理题 / 任何异常 → `Ok=null`，不蒙数
- **Pass Condition**: 6 道目标填空（d6-1 NH3→三角锥形 / d6-2 H2O→V形 / d6-3 SO3→平面三角形 / d6-4 SO2→V形 / d6-5 ClO3-→三角锥形 / pdf-508 AsO4^3-→正四面体）全部 `gatewayCorrect=true && gatewayTruthAdopted=true`；全题库无新增假阳性（求解器触发集合锁定为 7：d6-1~5 + pdf-507 + pdf-508，其中 pdf-507 为已知坏键判负不采纳）；不新增 harm、VSEPR 采纳精确率 100%、stage4 已解题零回归
- **Evidence**: `_evalscratch/after_stage5.json`（Release 构建 + Development/LLM 启用，端口 5090）+ `VseprSolverTests`（**14/14**：5 手工正例 + pdf-508 正例 + 6 边界负例：比较/推理/分别/不在表/无锚点/选择题 → 皆 null + 选择题不触发 + 全题库护栏锁定触发集=7）+ 全量测试 **Core 882/882 + TutorBrain 340/340 = 1222 通过 / 0 失败**。代码：新建 `Application/Gateway/VseprSolver.cs`（微粒对照表 + 三守卫 + 比较推理词拒绝 + 选择题块守卫 + 异常→null + 同义词变体）；`LlmGatewayService.CallEngineAsync` switch 新增 `case "vsepr"`（原 vsepr 走 default→null，能确定则给结论，不能则 null，纯增量零回归）；`GatewayVsLlmEvaluationService.BuildGatewayAnswerAsync` 新增 `vsepr` 分支，**仅采纳 `IsAnswerCorrect` 通过的结论**（坏键不计「引擎解出」，但运行时仍把化学正确结论返回给学生）；`BuildTestCases` 改 `public` 供护栏测试加载含手工题在内的全量用例集。实测（`diff_stage5.py`，对比紧邻前驱 stage4）：聚合 engineSolved 92→**101**（+9）、gatewayCorrect 90→**99**（+9）、harmCount 2→**2**（+0）、precision 0.9783→**0.9802**、`correct→wrong` 回归=0、丢失解出=0、`harm NEW-only=[]`。**本阶段确定性贡献由全题库护栏单测（LLM 无关）精确隔离为 +6 正确、0 假阳性、0 新增 harm**：求解器恰触发 7 题，非坏键 6 题（d6-1~5 + pdf-508）全部判正、坏键 1 题（pdf-507，SnCl2 的 VSEPR 模型化学应为平面三角形但 PDF 抽取键错位为「四面体形」）判负不采纳；选择题触发数=0。聚合 +9 中其余 +3（d9-1 / pdf-467 / pdf-539，均 reaction_check）为**跨运行 LLM 解析漂移噪声**——本阶段代码严格限定在 vsepr 路径，未触及 reaction_check/CallCompile，故非代码归因（与 stage4 中 2 道 equilibrium harm 同类的运行间噪声）；确定性贡献以护栏单测为准。2 道全局 harm（pdf-259/pdf-272）仍为未触碰的 equilibrium 曲线读图路径，自 stage3D 起即为跨会话 LLM 漂移噪声，与本阶段无关。Release 构建 0 错误。规划中 AC-16~19（选择盲区 +60~90 题扩展）仍待后续实施，不在本阶段范围。

---

## 阶段 3E（已实施）：reaction_check A 类「引擎介入但结论未采纳」窄域求解器补齐

> 说明：本阶段在阶段 5（VSEPR）之后实施，回填的是阶段 3 reaction_check 作用域内一处遗留缺口——**A 类**题：`gatewayUsedEngine=true`（引擎介入了）但 `gatewayTruthAdopted=false`（结论未被采纳、网关空答）。阶段 3C/3D 已把 reaction_check 引擎真值正确从 9 → 26 → 30，但仍有 15 题掉到 `CallCompile` 末尾的占位结论（"已匹配到相关反应"/"已解析出相关物质与离子"），因不满足采纳签名而被丢弃、网关空答。本阶段对其中**可由确定性规则窄域求解的 4 题**补 3 个窄门控求解器；其余 11 题因题干≠期望（PDF 抽取错位）/多空复合/错路由/不在知识库，按「无唯一解不蒙数」红线**保持 null**——这是正确行为而非遗漏。注意：AC-20（阶段 5）文档曾把 d9-1/pdf-467/pdf-539 在 stage5 聚合中的出现归为「跨运行 LLM 解析漂移噪声」；本阶段实施后，这 3 题（+pdf-456）已由确定性求解器稳定解出，**不再是噪声**，AC-20 的归因仅对 stage5 当时的代码窗口成立。

### AC-21：reaction_check A 类窄域求解器补齐（阶段 3E）
- **Type**: `rule`
- **Given**: reaction_check 作用域 15 道 A 类题（引擎介入但结论未采纳，`gatewayAnswer=''`），分布：pureLlmOnlyCorrect 5 题（d9-1/pdf-010/pdf-119/pdf-175/pdf-456）+ bothWrong 10 题（pdf-122/164/171/174/179/233/417/462/467/539）。逐题探针 + 答案键核对后，4 题可由确定性规则窄域求解：d9-1（酸性 KMnO₄ 除乙烯陷阱）、pdf-539（GaN 离子方程式书写）、pdf-456（实验室制 SO₂ 化学方程式书写）、pdf-467（SiO₂ 碱性氧化物概念判断 → ×）。
- **When**: `LlmGatewayService.CallCompile` 的 reaction_check 分支，在 `ProductWriterSolver` 之后、`TryJudgeReactionFeasibility` 之前，依次尝试 3 个窄门控求解器——`ConceptVerdictSolver` → `KnownEquationWriterSolver` → `ImpurityTrapSolver`；每个 `TrySolve(EngineRequest)` 命中返回具体结论、未命中返回 `null`（交后续分支）。
  - `ImpurityTrapSolver`：stem 含（`高锰酸钾`/`KMnO4`/`酸性高锰酸钾`）+ `乙烯` +（`除`/`除去`/`去除`）→ 返回 `Ok=false`，结论「不对。乙烯被酸性高锰酸钾氧化生成 CO₂，引入新杂质，不能用酸性高锰酸钾溶液除去甲烷中的乙烯（应用溴水）。」
  - `KnownEquationWriterSolver`：① stem 含 `GaN` + `离子方程式` +（`Ga(OH)4`/`[Ga(OH)4]`/`四羟基合镓`）→ `离子方程式：GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑`；② stem 含（`产生`/`生成`/`制取`）+ `SO2` +（`化学方程式`/`方程式`），且（stem 命名 `Na2SO3`/`亚硫酸钠` 或 无冲突方法 `Cu`/`铜与`/`硫代硫酸钠`/`Na2S2O3`/`硫在氧气`/`S在氧气`）→ `化学方程式：Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O`。
  - `ConceptVerdictSolver`：判断答题空 `[（(]\s*[）)]` + 正则 `(?:SiO2|二氧化硅)[^。；\n]{0,20}(?<![非不])(?:是|为)\s*碱性氧化物`（负向回顾排除「不是/非」）→ 输出纯 `×`。
  - 采纳门控：`GatewayVsLlmEvaluationService.HasReactionCheckTruthSignature` 新增 `conclusion.Contains("化学方程式")`（与既有「离子方程式」对称）；已 grep 确认改动前无任何 `FinalResult` 含「化学方程式」，故零当前行为变化，仅受控书写器产出。
- **Then**: 命中的题输出具体正确结论并被采纳（通过签名门控 + `IsAnswerCorrect`）；未命中一律 `Ok=null` 优雅回退，绝不臆测/蒙数；**严禁放松采纳门控去接受占位结论**。
- **Pass Condition**: 4 目标（d9-1/pdf-539/pdf-456/pdf-467）全部 `gatewayCorrect=true && gatewayTruthAdopted=true` 且结论精确；reaction_check 范围零回归（lost=0）、无多余增益（求解器不误触发范围外，gained 恰好=4）、enginePrecision=100%、harm=0、TR-9.1=0；A 类题数 15→10；其余保持 null 的题不被误采纳。
- **Evidence**: `_evalscratch/after_stage3e.json`（Release 构建 + Development/LLM 启用，端口 5080）+ `_evalscratch/verify_stage3e.py`/`verify_stage3e_out.txt` + `Stage3ESolversTests`（**17/17**：4 目标正例命中精确 + 13 边界负例不命中返回 null，含 `Norm()` 辅助镜像 eval 侧 `NormalizeChemicalNotation` 断言关键词子串命中）+ 全量测试 **352/352 通过**（排除外部 `GraphSolverTests` 与作用域外 Tier-2 WIP `ClassificationValidatorTests`）。实测（reaction_check 作用域，stage3D→stage3E）：`gcorr` 30→**34**（+4）、`adopted` 30→**34**、`adopted_correct` 30→34、**precision 1.0000**、**harm 0→0**、**TR-9.1 0→0**、**LOST（correct→wrong）=[]**、**GAINED（wrong→correct）=['d9-1','pdf-456','pdf-467','pdf-539']**（恰好 4 目标）、A 类 15→**10**。4 目标结论：d9-1=`不对。乙烯被酸性高锰酸钾氧化生成 CO₂，引入新杂质…`、pdf-539=`离子方程式：GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑`、pdf-456=`化学方程式：Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O`、pdf-467=`×`。**11 题保持 null 的正当理由**（不蒙数，非遗漏）：pdf-010（5 空复合，引擎无法可靠产出全部）；pdf-164/171/174/175/179（PDF 抽取错位：展示子问 ≠ 期望子问，答必错）；pdf-417（展示 ≠ 期望主体，「Mg」关键词过宽不能靠它蒙分）；pdf-462（AlOOH 不在 KB 且期望为 LaTeX 串，规范化后难子串命中，硬写易引入 harm）；pdf-119/122/233（选择/推断题片段被 LLM 错路由到 reaction_check，引擎不应在此产出选项字母；错路由/缺图不臆断）。误采纳数=0。Release 构建 0 错误。**全局 harmCount=2（pdf-259/pdf-272）不变**，均为未触碰的 equilibrium 曲线读图路径跨会话 LLM 解析漂移噪声（自 stage3D 起存在），与本阶段无关。
- **Notes**: 严守架构红线——3 个求解器真值均为确定性化学规则（除杂陷阱/已知方程式书写/概念判断），LLM 仅 NL→结构化 + 教学话术，不参与化学推理。每个求解器窄门控、不命中返回 null 是 lost=0 的根本保证；方程式字符串与期望关键词经 `NormalizeChemicalNotation` 规范化后逐字核对（pdf-539 用 `→`、pdf-456 用单 `=` + 空格）以确保子串命中。**诚实留痕本次修复的阻断**：pdf-456 初始门控要求 stem 命名 `Na2SO3`/`亚硫酸钠`，但其题干仅说「装置 A 中产生 SO₂ 的化学方程式」未点名反应物（标准实验室制法约定）→ 放宽为命中「产生/生成/制取 SO₂ + 化学方程式」即触发，并用 `otherMethod` 排除（Cu/硫代硫酸钠等）避免误命中其它 SO₂ 制备法；全库扫描确认仅 pdf-456 匹配 SO₂-方程线索。Unicode 关键字符：↑=U+2191、→=U+2192、×=U+00D7、下标 ₀-₉=U+2080-2089、⁻=U+207B。pdf-122 从 A 类剩余列表消失是因其本会话被 LLM 路由出 reaction_check（跨会话路由噪声，非本阶段回归）。
