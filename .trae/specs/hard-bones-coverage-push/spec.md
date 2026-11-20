# 硬骨头覆盖推进（Hard-Bones Coverage Push）

## 背景与基线

题库评估集共 **757** 题，引擎模式（无 LLM）当前基线为 `checkpoint_baseline_head`：

- `gatewayCorrect = 560`，`engineSolved = 560`，`coverage = 0.7398`
- `precision = 1.0`，`harmCount = 0`，`LOSS = 0`（已解出题零回归）
- 未解 **197** 题，构成为：选择 62、填空/计算/平衡等大题碎片与文本题约 120、判断 √/× 约 15，以及已登记数据缺陷 13 道（见 `data-defect-register.md`）。

本迭代针对用户指出的"其余硬骨头"：多选、曲线题、填空、判断、计算题，以及畸形/缺图/答案键冲突/正则未覆盖等——它们不能靠加单条事实唯一判定，需要**引擎逻辑或题库数据层面**的改动。

## 红线（不可突破，继承自 `data-defect-register.md` 与仓库既有原则）

1. **不注入 verdict 字眼**：题干/选项/解析里不塞"正确/错误/√/×"等判题结论。
2. **不改动存储 answer**，除非有官方源证明存储 answer 本身错误（如 pdf-237 B→A 先例），且留下来源证据。
3. **不基于低质量 OCR 猜测回填**：文本回填只写题干/选项，不写答案；仅采用权威来源（原页 OCR/答案册/教材）。
4. **不蒙数**：引擎只在文本完整且可唯一判定时给出结论，否则诚实 `null`。
5. **零假阳性 / 零回归**：`precision` 恒为 `1.0`，`harmCount = 0`，已判正题不得回退（`LOSS = 0`）。
6. 不向 `general-choice-facts` 等通用选择题事实库注入只对单题成立的策展事实（原则 #1）。

用户已确认：目标 = 红线内解出所有**可唯一判定**的题；确属缺图/畸形/无上下文的题保持诚实 `null` 并在结项报告列明。允许的改动：新增/扩展引擎逻辑、从官方源回填题干/选项文本、订正已确认错误的答案键。

## 目标（Goals）

- 在守红线的前提下，尽可能提升引擎对 197 道未解的求解覆盖。
- 每一批改动都用全量 757 题评估验证，做到 `GAIN ≥ 0 / LOSS = 0 / HARM = 0 / precision = 1.0`。
- 结项时输出：最终 checkpoint + 覆盖率，以及**所有仍未解的题的清单与红线原因分类**。

## 非目标（Non-Goals）

- 不追求字面 100% 解出：缺图、文本损坏、大题碎片缺上下文、超出高中引擎建模范围的题，按红线保持 `null`。
- 不接入/依赖在线 LLM（评估固定 `llmAvailable = False`）。
- 不重写既有已稳定的求解器链路（选择校验链、平衡求解器等），只做增量扩展。
- 不改动 `data-defect-register.md` 已登记且无官方源补救的缺陷题的 answer。

## 功能需求

1. **判断题路由修复**：√/× 正误判断题当前被 `ClassifyTaskDeterministic` 错分到 `unknown`，导致现有 `MultiSubCorrectnessSolver` 与后续判断聚合器无法触达。需让判断题进入专门的判断分支。
2. **概念正误判断聚合器**：复用现有 `CompositeStatementJudge`（`FactBaseStatementJudge` / `CountingStatementJudge` / `RedoxElectronStatementJudge`），对 √/× 题的每个小题逐句判定，仅当全部小题有确定结论时输出 √/× 序列；任一不确定 → 整题 `null`。不新增单题策展事实。
3. **选择/曲线/图形题缺口**：对文本完整可唯一判定但未解的选择题（含多选）、有结构化资产（svg 曲线/图形/分子）却未被消费的题，定位是路由/提取器/校验器缺口，做最小增量修复。
4. **上下文回填**：对题干/选项被截断（大题碎片）但可从官方源（`pdfs/` 原页 OCR / 答案册）补全的题，回填题干/选项文本（不改 answer），使引擎可判。
5. **答案键订正**：审计引擎能给出高置信正确结论、却与存储 answer 冲突的题，用官方源复核后订正 answer 键（留痕）。

## 非功能需求

- 全量回归：每批改动后跑 `EvalRunner` 全量 757 题，生成 `checkpoint_{tag}.jsonl` 与 `summary`。
- 可复现：所有结论可由 `dotnet test` 的单元测试覆盖至少新增解出的代表题。
- 零回归护栏：任何 `LOSS > 0` 或 `harmCount > 0` 或 `precision < 1.0` 的批次不得合入，必须回滚或修正。

## 约束与依赖

- 构建：`dotnet build CogniThink.Chem.sln -c Release`（已验证 0 错误）。
- 评估：`dotnet run --project src/CogniThink.TutorBrain.EvalRunner -c Release --no-build -- --tag {tag} --concurrency 24`。
- 评估数据：`GatewayVsLlmEvaluationService.BuildTestCases()` 加载题库（含 `pdf_questions.json` 等）；基线 checkpoint 为 `checkpoint_baseline_head`。
- 判分：`IsAnswerCorrect` 已支持选择字母、多选字母全中、√/× 序列子串匹配、关键词命中——新增输出格式需与之对齐。
- 不引入新的第三方依赖。

## 假设与开放问题

- 假设 `checkpoint_baseline_head`（560/197）是当前 HEAD 的真实基线（已由本迭代重跑确认，与 `after_svg_batch28` 一致）。
- 假设现有 4 个事实库（general-choice / experiment / organic / structure-periodic）已覆盖相当一部分概念陈述；概念聚合器先复用它们，不预先扩充。若复用后覆盖率不足，是否允许新增**独立的通用判断事实文件**（不污染选择事实库），留待实施中按批向用户确认。
- 大题碎片回填的官方源可得性：以 `pdfs/` 目录中的原页 OCR / 答案册为准；不可得的不硬填。

## 验收标准（Acceptance Criteria）

- **AC-1 [rule] 零回归**：合入的每个批次评估结果满足 `LOSS = 0`（无已判正题变错）、`harmCount = 0`、`enginePrecision = 1.0`。证据：批次 `checkpoint_{tag}_summary.txt` 与相对前一基线的 diff。
- **AC-2 [rule] 净增益**：最终 `gatewayCorrect` 严格大于基线 560；每个合入批次 `GAIN ≥ 0`。证据：最终 `checkpoint_final_summary.txt`。
- **AC-3 [rule] 红线合规**：仓库内无 verdict 字眼注入（题干/选项/解析不含新增"正确/错误/√/×"判题结论）；存储 answer 的任何变更都附官方源证据；无低质 OCR 猜测回填。证据：`git diff` 审查 + 结项报告。
- **AC-4 [rule] 逐批证据**：每个批次都有 `checkpoint_{tag}.jsonl`、`checkpoint_{tag}_summary.txt`，并在 `tasks.md` 记录 `GAIN +N / LOSS 0 / HARM 0`。
- **AC-5 [rule] 诚实 null 报告**：结项报告列出全部仍未解的题 id，并标注红线原因类别（缺图 / 文本畸形 / 大题碎片无上下文 / 超出现有引擎建模）。
- **AC-6 [rule] 测试通过**：`dotnet test` 全绿；新增引擎逻辑有单元测试覆盖至少每个新解出题型的代表题。
- **AC-7 [rubric] 覆盖提升幅度**：相对基线净新增解出题数。锚点：0 = 无提升；20 = 解出约 20 道；40+ = 解出约 40 道及以上（高成效）。通过阈值 ≥ 20；目标 ≥ 40。证据：最终 checkpoint 与基线的 id 差集。
