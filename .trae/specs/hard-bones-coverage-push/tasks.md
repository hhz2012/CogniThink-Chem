# 任务队列 — 硬骨头覆盖推进

基线：`checkpoint_baseline_head`（gatewayCorrect=560，未解 197，precision=1.0，harm=0）。
约定：每个任务完成时跑全量评估生成 `checkpoint_{tag}.jsonl` + summary，并在"完成证据"记录 `GAIN +N / LOSS 0 / HARM 0`（相对上一基线）。任何 LOSS>0 或 harm>0 或 precision<1 的批次不得标记完成。

## Task 1: 判断题路由修复（让现有求解器可达）

- **Status**: completed
- **Priority**: high
- **Blocked By**: 无
- **范围**:
  - `ClassifyTaskDeterministic`：识别 √/× 正误判断题信号（"正误"、"正确的填"、"错误的填"、"填√/×"、或存储 `type=="judge"`），路由到新的 `judgment` 任务类型（不再落 `unknown`）。
  - `CallEngineAsync`：新增 `case "judgment"` 分支，先调用 `MultiSubCorrectnessSolver.TrySolve`（方程书写正误，已有能力），未命中再回退后续判断聚合器（Task 2）。
  - 保证：仅影响此前路由到 `unknown` → `null` 的题；已判正题不受影响（零回归）。
- **Test Requirements**:
  - TR-1.1 [rule] `dotnet test` 全绿。
  - TR-1.2 [rule] 单元测试：判断题文本被分类为 `judgment`（取 pdf-051 题干）；非判断题不受影响（取一道已判正 choice 题仍分类为 `choice`）。
  - TR-1.3 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录相对 baseline_head 的 GAIN。
- **预期收益**: pdf-051 等纯方程正误判断题翻转判正（具体数量以评估为准）。
- **完成证据**:
  - checkpoint：`_evalscratch/checkpoint_after_task1.jsonl` + `checkpoint_after_task1_summary.txt`；复跑核验 `_evalscratch/checkpoint_verify_resume_summary.txt`（563/194，与 after_task1 字节一致）。
  - 相对 `baseline_head`（560）：**GAIN +3 / LOSS 0 / HARM 0 / precision=1.0**，新解出 `pdf-051`、`pdf-468`、`pdf-471`。
  - 构建：`dotnet build CogniThink.Chem.sln -c Release` 0 错误 0 警告。
  - 测试：`dotnet test` 全绿（TutorBrain 1096 + Chem.Core 905 = 2001 通过，0 失败）；判断题路由单测覆盖。

## Task 2: 概念正误判断聚合器（复用现有陈述判定）

- **Status**: pending
- **Priority**: high
- **Blocked By**: Task 1（需 judgment 路由分支）
- **范围**:
  - 新增 `JudgmentStatementAggregator`：把 √/× 题拆成小题（复用 `MultiSubCorrectnessSolver` 的切分思路或其 `SplitSubQuestions`），对每个非方程小题调用 `CompositeStatementJudge`（`FactBaseStatementJudge` + `CountingStatementJudge` + `RedoxElectronStatementJudge`）取 `True/False/Undecided`。
  - 聚合铁律：仅当全部小题确定（无 Undecided）才输出 √/× 序列；任一不确定 → 整题 `null`（守 precision）。
  - 方程小题仍走 `EquationCorrectnessJudge`（与 Task 1 的 MultiSub 合并：先方程、后概念，统一聚合）。
  - 接入 `judgment` 分支；不新增任何单题策展事实（复用现有 4 个事实库）。
- **Test Requirements**:
  - TR-2.1 [rule] `dotnet test` 全绿。
  - TR-2.2 [rule] 单元测试：全部小题可判 → 输出序列正确；任一小题 Undecided → 返回 `null`。
  - TR-2.3 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录相对 Task 1 基线的 GAIN。
- **完成证据**: checkpoint/summary、GAIN/LOSS/HARM、测试输出；列出翻转的题 id。

## Task 3: 文本完整选择题缺口定位与增量修复

- **Status**: pending
- **Priority**: medium
- **Blocked By**: Task 1（基线推进后重算未解清单）
- **范围**:
  - 对基线中**无图、文本完整、有选项**却未解的选择题（如 pdf-140 化学计量、pdf-371/386/388/405、多选 pdf-458/475/524/531 等），逐题定位缺口：路由未触发？校验器域外？事实缺失？提取失败？
  - 对可唯一判定的，做最小增量修复（扩展 `StoichiometryChoiceValidator` / `MultiSelectChoiceValidator` / `NaChoiceValidator` 等既有校验器，或补提取正则）。
  - 不得靠加单题事实硬解；无法唯一判定的保持 null。
- **Test Requirements**:
  - TR-3.1 [rule] `dotnet test` 全绿。
  - TR-3.2 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录 GAIN。
- **完成证据**: checkpoint/summary、GAIN/LOSS/HARM、修复点说明与翻转题 id。

## Task 4: 结构化资产（曲线/图形/分子）激活扫查

- **Status**: pending
- **Priority**: medium
- **Blocked By**: Task 1
- **范围**:
  - 核对未解的图像依赖选择题哪些已注入 svg 资产（`after_svg_batch*` 共 28 批），但 `TryRouteGraph/TryRouteCurve/TryRouteMolecule` 或 `CurveChoiceValidator` 未消费到。
  - 修复路由/提取器缺口（如 `CurveIntentExtractor`、`GraphInputExtractor` 的正则未覆盖；资产类型映射），使已存在的资产真值能用于判分。
  - 无资产的图像题保持 null（不臆造）。
- **Test Requirements**:
  - TR-4.1 [rule] `dotnet test` 全绿。
  - TR-4.2 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录 GAIN。
- **完成证据**: checkpoint/summary、GAIN/LOSS/HARM、修复点与翻转题 id。

## Task 5: 官方源回填题干/选项（大题碎片补上下文）

- **Status**: pending
- **Priority**: medium
- **Blocked By**: Task 1
- **范围**:
  - 对大题碎片（题干以 `(N)` 开头、缺父题选项/流程图上下文）但 `pdfs/` 原页 OCR 或答案册可补全的题，回填 `question`/`options` 文本（**不改 answer、不注入 verdict**）。
  - 回填后重评估；仍不可唯一判定的保持 null。
  - 每条回填留痕：来源文件与页码。
- **Test Requirements**:
  - TR-5.1 [rule] 回填只触及 question/options 字段，answer 不变；无 verdict 字眼注入（grep 审查）。
  - TR-5.2 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`；记录 GAIN。
- **完成证据**: 回填清单（id + 来源）、checkpoint/summary、GAIN/LOSS/HARM。

## Task 6: 答案键订正审计（官方源复核）

- **Status**: pending
- **Priority**: low
- **Blocked By**: Task 3/4/5（先把引擎能判的都解出来，冲突才显现）
- **范围**:
  - 审计引擎给出高置信正确结论、却与存储 answer 冲突的题；用 `pdfs/` 官方源复核。
  - 确认存储 answer 错误的，订正 answer 键并留痕（来源 + 订正前后值），仿照 pdf-237 先例。
  - 引擎结论不唯一/官方源不支持的，不订正。
- **Test Requirements**:
  - TR-6.1 [rule] 每次订正都附官方源证据；订正后该题判正且无 harm。
  - TR-6.2 [rule] 全量评估 `LOSS=0 / harm=0 / precision=1.0`。
- **完成证据**: 订正清单、来源证据、checkpoint/summary。

## Task 7: 结项全量评估 + 诚实 null 报告

- **Status**: pending
- **Priority**: high
- **Blocked By**: Task 2, 3, 4, 5, 6
- **范围**:
  - 最终全量评估生成 `checkpoint_final`，与 `baseline_head` 做 id 级 diff，得出净新增解出集合与仍未解集合。
  - 写结项报告：最终覆盖率、各批次 GAIN 汇总、仍未解 id 清单及其红线原因类别（缺图 / 文本畸形 / 大题碎片无上下文 / 超出现有引擎建模）。
- **Test Requirements**:
  - TR-7.1 [rule] `gatewayCorrect > 560` 且 `precision=1.0`、`harm=0`、`LOSS=0`（相对基线）。
  - TR-7.2 [rule] 报告覆盖全部仍未解 id，每个有原因类别。
- **完成证据**: `checkpoint_final_summary.txt`、结项报告路径、净新增解出题 id 列表。
