# 氧化还原陈述判定扩展（路径 B）— 求解 pdf-075 等氧还计数选择题 - Implementation Plan

## Task 1: 恢复 pdf-075 选项文本 + 登记册登记
- **Status**: `pending`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 以 VL 原书 `page_021.png`/`page_022.png` 为准，把 `pdf_questions.json` 中 pdf-075 的 `options` 替换为真实选项（A: OH⁻的还原性比H₂O强；B: 反应Ⅳ中每生成1 mol O₂转移4 mol电子；C: 反应ⅰ～ⅳ中有3个氧化还原反应；D: 较He、Ne、Ar、Kr原子，Xe原子半径大，电离能小，所以含Xe的化合物种类更多），文本规范化为 `NormalizeText`/解析器可识别形式。
  - `answer` 保持 `B` 不动；`answerKeywords` 同步为 `["B", "反应Ⅳ中每生成1 mol O₂转移4 mol电子"]`。
  - `question`/`note`/`type` 不变。
  - 在 `data-defect-register.md` 追加 D 类登记（pdf-075 选项错乱已从原页恢复，键 B 正确）。
- **Acceptance Criteria Addressed**: AC-1
- **Test Requirements**:
  - `rule` TR-1.1: 读取 pdf-075，4 选项与原书一致、answer=="B"、question/note/type 未变；`git diff` 仅 pdf-075 options(+answerKeywords) 变动。证据：探针输出 + diff。

## Task 2: 探针验证现有电子计数 judge 对反应Ⅳ 的判定 + Xe 方程可解析/配平
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 写 blast-radius 风格的 C#/Python 探针，调用真实引擎链路（或复制 `RedoxElectronStatementJudge`/`RedoxBalancer`/`OxidationStateAnalyzer` 逻辑）验证：对恢复后的 pdf-075，选项 B 被判为 False（反应Ⅳ 每 mol O₂ = 6 mol e⁻ ≠ 4）。
  - 验证 `EquationParser`/`EquationBalancer`/`RedoxBalancer` 能处理 4 个 Xe 方程（含 `Na4XeO6`、`Na⁺`、`↑↓`、`⁻`/`₂`）；`OxidationStateAnalyzer` 对 ⅰ-ⅳ 的氧化态变化判定正确（ⅲ 非氧还）。
  - 若解析/配平失败：修复 Core 解析器，保持对既有方程行为字节级不变（补单测锁定）。
- **Acceptance Criteria Addressed**: AC-2（前置）、AC-3（前置）
- **Test Requirements**:
  - `rule` TR-2.1: 探针输出 B→False；4 个方程均成功解析配平；ⅰ/ⅱ/ⅳ IsRedox=true、ⅲ IsRedox=false。证据：探针输出。

## Task 3: 新增 RedoxCountStatementJudge（氧还反应计数判定）
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 2
- **Description**:
  - 新建 `RedoxCountStatementJudge : IStatementJudge`，判定 "反应ⅰ～ⅳ中有 N 个氧化还原反应" 类陈述。
  - 解析标签范围（复用 `RedoxElectronStatementJudge` 的 `LabelValue`/`FindLabelMarkers` 思路，支持 ⅰ-ⅹ / Ⅰ-Ⅹ / ①-⑩ / (1)~(n) / 一~十），取范围内各方程，逐个用 `OxidationStateAnalyzer`（或 `RedoxBalancer.SplitHalfReactions` 的 `IsRedox`）判定是否氧还，统计真值计数，与声称 N 比对。
  - 保守：标签范围解析不出 / 任一方程不可解析 / 计数歧义 → `Undecided`。
  - 复用 `NormalizeText`。
- **Acceptance Criteria Addressed**: AC-3
- **Test Requirements**:
  - `rule` TR-3.1: pdf-075-C（3 个）→True；声称 2 →False；范围外/歧义→Undecided。证据：xUnit 通过。

## Task 4: 新增 RedoxRoleRatioStatementJudge（角色/比值判定，通用能力）
- **Status**: `pending`
- **Priority**: medium
- **Depends On**: Task 2
- **Description**:
  - 新建 `RedoxRoleRatioStatementJudge : IStatementJudge`。
  - 角色："X 是氧化剂/还原剂/既是氧化剂又是还原剂/只作氧化剂/只作还原剂/氧化产物/还原产物"。依据 `RedoxBalancer.SplitHalfReactions` 的氧化态变化定位 X 中变价元素的升降；同一物种元素既升又降→歧化（既是又是）；只降→氧化剂；只升→还原剂；产物侧→氧化/还原产物。
  - 比值："被氧化与被还原的物质的量之比为 a:b"（含歧化中同一元素被氧化/被还原的量比）。由氧化态变化与配平系数计算。
  - 解析标签方程（复用标签解析）；保守：物种歧义 / 非氧还方程 / 系数不可得 → `Undecided`。
  - 本任务是通用能力沉淀，服务 pdf-075 之外的自洽氧还题（FR-7 扫描）。
- **Acceptance Criteria Addressed**: AC-3
- **Test Requirements**:
  - `rule` TR-4.1: 歧化（如 3S+6NaOH→2Na2S+Na2SO3+3H2O）被氧化:被还原=1:2 →True；声称 2:1 →False；非氧还方程问角色/比值→Undecided。证据：xUnit 通过。

## Task 5: pdf-075 的 A（还原性比较）与 D（周期律）判定
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 3
- **Description**:
  - A "OH⁻的还原性比H₂O强"：优先尝试保守引擎推导（同一氧化剂 XeF6 在 ⅲ 与 H2O 不发生氧还、在 ⅳ 与 OH⁻ 发生氧还且 OH⁻ 被氧化 → OH⁻ 还原性更强）；若无法在零假阳性预算内安全泛化，则用带 `stemMatch` 锚定的窄域事实兜底（参照 `choice-curated-facts.json`）。
  - D "较He、Ne、Ar、Kr，Xe 半径大、电离能小→化合物更多"：优先检查现有周期律事实/`StructurePeriodicValidator` 能否覆盖；否则用窄域锚定事实。
  - 两种机制都必须通过全库 blast-radius 探针验证零 stray-hit（只命中 pdf-075 的 A/D，不误判其它题）。
- **Acceptance Criteria Addressed**: AC-2、AC-5
- **Test Requirements**:
  - `rule` TR-5.1: pdf-075-A→True、D→True；全库扫描该机制无其它误命中（stray-hit=0）。证据：探针输出。

## Task 6: 新增单选择陈述路径 + 注册三处 CompositeStatementJudge
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 3, Task 4, Task 5
- **Description**:
  - 新增单选择陈述校验路径（新 `internal static` 类或扩展 `CountingChoiceValidator`）：逐选项经 `CompositeStatementJudge.JudgeWithIds` 判定；全部非 Undecided 且满足问法方向候选唯一 → 复用 `ChooseUnique` 返回字母；候选≥2 → `null`；任一 Undecided → `null`。复用 `ChoiceVerifier.ParseOptions` 与问法方向判定（与 `CountingChoiceValidator.AskDir` / `MultiSelectChoiceValidator.WrongDir` 同口径）。
  - 在 `LlmGatewayService.CallChoice` 中接在 `StoichiometryChoiceValidator` 之后、`MultiSelectChoiceValidator` 之前。
  - 把 `RedoxCountStatementJudge`、`RedoxRoleRatioStatementJudge` 注册进三处：`MultiSelectChoiceValidator.Judge`、新单选路径、`CountingChoiceValidator.SubItemJudge`。
- **Acceptance Criteria Addressed**: AC-4、AC-2
- **Test Requirements**:
  - `rule` TR-6.1: 单测：候选唯一→返回字母；候选2→null；含Undecided→null；pdf-075 端到端→B。证据：xUnit 通过。

## Task 7: 单测 + 护栏真值登记
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 6
- **Description**:
  - 为 FR-2/FR-3/FR-4 写 xUnit 正例/反例/歧义（→Undecided）测试，放在 `tests/CogniThink.TutorBrain.Tests/`（参照 `RedoxElectronStatementJudgeTests.cs`、`MultiSelectChoiceValidatorTests.cs` 模式）。
  - 把 pdf-075 加入相应护栏真值表（如 `Batch2ChoiceValidatorTests` 的 `ExperimentTruth` 或同类），锁定 →B。
  - 遵守并发护栏：`SolveStructuredEnricherTests.cs` 既有编译错误非本任务引入，跑测时临时改名 `.wipbak`，跑完还原；确认无 `*.wipbak` 残留；必要时 `Stop-Process testhost`。
- **Acceptance Criteria Addressed**: AC-3、AC-4、AC-6
- **Test Requirements**:
  - `rule` TR-7.1: 新增测试全通过；pdf-075 真值→B 被护栏锁定；无 `*.wipbak` 残留。证据：测试输出。

## Task 8: 全库 blast-radius 探针回归 + 扫描额外收益
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 6
- **Description**:
  - 写 Python 探针（镜像 C# 的 Norm/OptContains/judge 口径，参照 `_evalscratch/probe_batch3_verify.py`），加载真实文件，对全 757 题复现选择链判定：确认 pdf-075→B；列出所有被新能力改变判定的题；验证零 stray-hit、零 LOST。
  - 扫描 bothWrong 及其它题，找其它可被新 judge 保守判对的自洽氧还选择题，纳入候选（仍须满足零伤害）。
- **Acceptance Criteria Addressed**: AC-5（前置）、FR-7
- **Test Requirements**:
  - `rule` TR-8.1: 探针输出 pdf-075→B；被改变题集合仅含目标收益；stray-hit=0、LOST=0。证据：探针输出。

## Task 9: build + 护栏测试
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 7
- **Description**:
  - `dotnet build` 通过（0 error）。
  - 跑 xUnit（临时排除 WIP 文件后还原），0 失败。
- **Acceptance Criteria Addressed**: AC-6
- **Test Requirements**:
  - `rule` TR-9.1: build 退出码 0；测试 0 失败；无 `*.wipbak` 残留；无 testhost 锁。证据：命令输出。

## Task 10: 生产 eval + diff 红线核对
- **Status**: `pending`
- **Priority**: high
- **Depends On**: Task 9
- **Description**:
  - 用 `& "D:\github\CogniThink-Chem\_evalscratch\run_eval_resume.ps1" -Tag redox_stmt -Env Production -OutDir "_evalscratch"` 跑全量 eval（757 题）。
  - 写 diff 脚本对比 `snapshot_after_datarepair_engine_batch3.json` 与新快照：核对 `LOST=[]`、`harmCount=0`、`enginePrecision=1`、`GAINED ⊇ {pdf-075}`、之前正确的 `gatewayAnswer` 变化数=0、batch1/2/3 已解题仍正确。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rule` TR-10.1: diff 输出全部红线满足；gwCorrect 708→至少 709。证据：eval 快照 + diff 输出。

## Task 11: 登记册更新 + 独立评审
- **Status**: `pending`
- **Priority**: medium
- **Depends On**: Task 10
- **Description**:
  - 完成 `data-defect-register.md` 登记（pdf-075 D 类已恢复；新能力解锁记录）。
  - 委托独立 agent 评审（写 `review.md` 覆盖全部 AC/TR），修复 actionable 发现，复评至 pass。
- **Acceptance Criteria Addressed**: 全部 AC
- **Test Requirements**:
  - `rule` TR-11.1: `review.md` 存在且结果 pass，无 actionable 未修复项。证据：review.md。
