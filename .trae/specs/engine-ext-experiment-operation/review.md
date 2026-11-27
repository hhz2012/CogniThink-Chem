# 引擎能力扩展·实验操作规范判定事实（pdf-416 / pdf-531）- Independent Review

## 元数据

- **Spec**: [spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/engine-ext-experiment-operation/spec.md)
- **Plan**: [tasks.md](file:///d:/github/CogniThink-Chem/.trae/specs/engine-ext-experiment-operation/tasks.md)
- **Review Cycle**: Cycle 1（fresh context，只读独立评审）
- **评审日期**: 2026-11-27
- **评审方式**: 仅运行只读探针 / dotnet build / dotnet test / git diff / Read & Grep；除本文件外未修改任何源码、数据、测试或文档。
- **变更目标**: 向 `data/highschool/experiment-facts.json` 追加 8 条通用实验操作事实（EF-416-A/B/C/D + EF-531-A/B/C/D），使 pdf-416（单选→C）与 pdf-531（多选→BC）由未解翻判正，零回归；同步两个测试真值表与 `data-defect-register.md`；零 C# 行为改动。

## 环境 / 注意事项（评审前核实，非本变更缺陷）

1. **WIP 文件状态与任务描述不符（任务前提已过时）**：任务说明称 `src\CogniThink.TutorBrain.Api\Application\Chemistry\ChemicalMultiStepReasoningSimulator.cs` 应被重命名为 `.wip_disabled` 以从编译排除，且引用了未定义的 `EquilibriumStateSimulator`（孤儿死代码）。**实际当前状态**：
   - 该文件为 `ChemicalMultiStepReasoningSimulator.cs`（**非** `.wip_disabled` 后缀），在编译中。
   - `EquilibriumStateSimulator` **已定义**于同目录未跟踪文件 [EquilibriumStateSimulator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Chemistry/EquilibriumStateSimulator.cs)（`public sealed class EquilibriumStateSimulator`）。
   - `ChemicalMultiStepReasoningSimulator` 被 [Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Program.cs) 与 [ChemistryController.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/ChemistryController.cs) 引用（Grep 命中）。
   - **结论**：该 WIP 已不再是「引用未定义类型的孤儿死代码」，当前可正常编译。本次 `dotnet build` + `dotnet test` 全绿未受其影响。此为既有工作区演进，与本变更无关，不计入缺陷。
2. **工作区为多层未提交变更叠加**：`git status`/`git diff --stat` 显示除本变更声明的文件外，还有 `MultiSelectChoiceValidator.cs`、`VseprSolver.cs`、`ChemicalSimulationEngine.cs`、`general-choice-facts.json`、`substances.json`、`electrode-half-reactions.json`、`pdf_questions.json` 等修改，以及一批未跟踪的 simulator/judge 文件。经核对，这些属上一批 **structure-periodic（基线）** 及其它在途工作（例如 `MultiSelectChoiceValidator.cs` 的 diff 仅新增 ElectronConfig/VseprBondAngle/CrystalType/CoordinateBond 四个结构周期律 judge，与实验操作无关）。**本变更的影响通过「基线快照 `snapshot_after_structperiodic` → 新快照 `snapshot_after_expop`」的 diff 隔离**，并以 `git diff data/highschool/experiment-facts.json` 单文件核对锁定范围；未因工作区脏误判。

## Checkpoints（逐条独立取证）

### CP-1: pdf-416 判正且引擎结论为 C（AC-1）
- **Type**: `rule`
- **Covers**: AC-1 / TR-4.1
- **证据**（重跑 `python _evalscratch\expop_diff.py`，输出全文留存）：
  ```
  pdf-416: gwCorrect=True usedEngine=True state=Confirmed
     gwAns='正确选项为 C'  concl='正确选项为 C'  expected='C'
  ```
  - 快照对比：BASE gwCorrect=702 → AFTER gwCorrect=704；pdf-416 ∈ GAINED。
  - `_evalscratch/checkpoint_after_expop.jsonl` 中 pdf-416 行：`gatewayCorrect=true`、`gatewayAnswer="正确选项为 C"`、`gatewayConclusion="正确选项为 C"`、`gatewayConclusionState="Confirmed"`、case.answer=`"C"`。
  - 路由：`_evalscratch/probe_exp3.py` 字节级复刻 C# `NormOpt`/`OptContains`/`WRONG`，落库后 `pdf-416 single(experiment) -> C`（ExperimentChoiceValidator 单库逐选项唯一求解）。
- **结论**: ✅ pass。pdf-416 判正，引擎结论为 `C`。

### CP-2: pdf-531 判正且引擎结论为 BC（AC-2）
- **Type**: `rule`
- **Covers**: AC-2 / TR-4.1
- **证据**（重跑 `expop_diff.py` + `probe_exp3.py`）：
  ```
  pdf-531: gwCorrect=True usedEngine=True state=Confirmed
     gwAns='正确选项为 BC'  concl='正确选项为 BC'  expected='BC'
  ```
  - checkpoint 中 pdf-531 行：`gatewayCorrect=true`、`gatewayAnswer="正确选项为 BC"`、`gatewayConclusionState="Confirmed"`、case.answer=`"BC"`。
  - 路由双路径（`probe_exp3.py`）：
    - `pdf-531 single(experiment) -> None`（ExperimentChoiceValidator 单库候选不唯一→安全返回 null，不抢道）；
    - `pdf-531 multi(allkeys) -> BC`（MultiSelectChoiceValidator 全库遍历采纳 BC）。
  - 与 spec 路由语义一致：瀑布链 Experiment 先于 MultiSelect，pdf-531 的 Experiment 路径返回 null，由 MultiSelect 胜出。
- **说明**: checkpoint 字段 `gatewayResultProvenance` 为 `null`（该快照格式不记录 provenance），故采纳方证据取自探针复刻的双路径决策而非快照 provenance 字段；行为结论一致。
- **结论**: ✅ pass。pdf-531 判正，引擎结论为 `BC`，由 MultiSelectChoiceValidator 采纳。

### CP-3: 零回归红线（AC-3）
- **Type**: `rule`
- **Covers**: AC-3 / TR-4.2
- **证据**（重跑 `expop_diff.py`，对比 `snapshot_after_structperiodic` → `snapshot_after_expop`）：
  ```
  BASE  gwCorrect=702 bothWrong=55 harmCount=0 enginePrecision=1
  AFTER gwCorrect=704 bothWrong=53 harmCount=0 enginePrecision=1
  LOST  = []
  GAINED= ['pdf-416', 'pdf-531']
  harm= 0
  previously-correct changed gatewayAnswer = 0
  extra GAINED (beyond 416/531) = []
  ALL RED LINES PASS
  ```
  - LOST=[]、harm=0、precision=1 ✓
  - GAINED 恰为 {pdf-416, pdf-531}，无多余收益 ✓
  - 已判正题 gatewayAnswer 字节级不变（changed=0）✓
- **结论**: ✅ pass。零回归。

### CP-4: 全库零假阳性 / 无路由二义 + 计数护栏（AC-4）
- **Type**: `rule`
- **Covers**: AC-4 / TR-2.2 / TR-3.1 / TR-3.2
- **证据**：
  - 先 `dotnet build`（隐式于 test）成功：`CogniThink.TutorBrain.Api.dll`、`CogniThink.TutorBrain.Tests.dll` 均生成，仅若干既有 CS8602/CS8603/CS1998 警告与 RDKit RID 警告（非错误，与本变更无关）。
  - 执行指定过滤测试：
    ```
    dotnet test tests\CogniThink.TutorBrain.Tests\CogniThink.TutorBrain.Tests.csproj -c Debug --nologo \
      --filter "FullyQualifiedName~Batch2ChoiceValidatorTests|FullyQualifiedName~Batch3ChoiceValidatorTests|FullyQualifiedName~MultiSelectChoiceMatcherTests|FullyQualifiedName~MultiSelectChoiceValidatorTests"
    ```
    结果：**已通过! — 失败: 0，通过: 364，已跳过: 0，总计: 364，持续时间: 20 s**（输出存 `_evalscratch/review_test_out.log`）。与预期 364/0 完全一致。
  - 两个计数护栏断言显式通过（`--no-build` 单独过滤 + console normal 日志）：
    - `MultiSelectChoiceMatcherTests.FullBank_SingleSelect_Assertions_StaySingleLetter` 已通过 [3 s]
    - `MultiSelectChoiceMatcherTests.FullBank_MultiSelect_Assertions_AreExactlySeven` 已通过 [2 s]
    → 多选断言仍为 7、单选保持单字母，未被本批破坏。
- **结论**: ✅ pass。全库零假阳性、无路由二义，护栏计数不变。

### CP-5: 新增事实通用性（非单题策展）（AC-5）
- **Type**: `rubric`
- **Covers**: AC-5 / TR-1.1 / TR-1.2 / NFR-3
- **Scale**: 1-5；Pass Threshold ≥ 4
- **审计证据**：
  - `git diff data/highschool/experiment-facts.json`：仅在原末条 EF-546-D 补尾逗号后追加 8 行（EF-416-A/B/C/D + EF-531-A/B/C/D），**无既有事实的删改**。
  - 8 条 `match` 均为实验操作通用词汇：`晶膜`/`趁热过滤`/`冷却结晶`/`大量晶体`（结晶域）、`KMnO4`/`碱式滴定管`/`酸式滴定管`/`待测液`/`锥形瓶`/`接近终点`/`冲洗锥形瓶内壁`/`变色后`/`立即记录`（滴定域）——可跨同域题复用，非硬编码 416/531 题面唯一标识。
  - `reason` 均为通用化学原理：NaCl 溶解度随温度变化小→蒸发至大量晶体趁热过滤、防钾盐冷却结晶混入；KMnO₄ 强氧化性腐蚀碱式滴定管橡胶；接近终点冲洗内壁使反应完全；指示剂变色后半分钟不褪色再读数。非单题凑数。
  - `stemMatch` 仅作题域限定：EF-416 锁「粗盐/精制/一系列操作」，EF-531 锁「滴定分析操作」（并辅以 `Fe3O4` 上下文锚点防跨题误命中）。
  - `probe_exp3.py`（读**当前落库文件**）输出：
    ```
    experiment-facts total statements: 108 (expect 108)
    new facts: ['EF-416-A'..'EF-531-D'] (expect 8)
    === blast-radius ===  8 条均 hits=[目标]  OK
    === 双路径决策 ===  pdf-416 single->C, pdf-531 single->None, pdf-531 multi->BC
    TOTAL PROBLEMS=0
    ```
    → 8 条在全库各仅命中目标 (qid, option)，零 stray-hit，双路径决策正确。
- **评分**: **5 / 5**
- **理由**: 8 条均为「操作词汇 + 判定方向」的教材通用规则表，reason 化学自洽且明显可跨同域题复用，stemMatch 仅承担题域/防误伤限定（探针 TOTAL PROBLEMS=0 佐证未牺牲通用性换正确性）；唯一轻微 caveat 是 EF-531 的 stemMatch 含 `Fe3O4` 上下文锚点，但不影响 match/reason 的通用真值属性，且属合理零误伤构造。达 anchor 5。

### CP-6: 登记册维护（AC-6）
- **Type**: `rule`
- **Covers**: AC-6 / TR-5.1
- **证据**（Grep `data-defect-register.md` 中 7 个 id 的当前登记行）：
  - pdf-416 过时登记已更正：L369（原「单条事实无法驱动/子问缺上下文 NO-GO」→「✅ 已解（engine-ext-experiment-operation 批次）…经 ExperimentChoiceValidator 单库单选判正 C」）、L534（跳过表→「✅ 已解…单选 C」「已移出未解」）。
  - pdf-531 过时登记已更正：L131（缺陷表→✅ 已解，EF-531 + MultiSelectChoiceValidator 判正 BC）、L192（跳过表→✅ 已解「已移出未解」）、L536（跳过表→✅ 已解「已移出未解」）。
  - 本批新发现未登记缺陷已补登（H.2 表 L215–L219）：
    - pdf-180：张冠李戴（answer=A 与电解填空题面/note 不匹配）
    - pdf-198：非引擎域填空（options 空、answer 多子问复合串）
    - pdf-333：题面截断（options 空、answer=C 无法对齐）
    - pdf-377：图缺失/题面截断（合成路线图缺失）
    - pdf-528：非引擎域填空（开放文字描述 + 溶解度曲线图）
- **结论**: ✅ pass。登记册与当前真实可解状态一致，无遗漏。

## 额外核对（红线）

- **仅 8 条新增、无对既有事实删改**：`git diff data/highschool/experiment-facts.json` 显示 `+8` 行新事实 + 原末条补 1 个尾逗号（格式），无任何既有事实内容变动；文件由 100→108 条 statements（probe 确认）。
- **`pdf_questions.json` 未被本变更改动**：`git diff -- src/.../pdf_questions.json | Select-String "pdf-416|pdf-531|416|531"` **无任何匹配**（0 命中）；其 40+/34- 行改动不触及 416/531，属其它批次。红线「不改数据字段」对本变更成立。

## Findings

### Actionable findings
（无）

### Advisory（不阻塞 pass）
1. **快照 provenance 可观测性**：`checkpoint_after_expop.jsonl` 的 `gatewayResultProvenance` 字段为 `null`，导致采纳方无法从快照直接读取，只能靠探针复刻逻辑旁证。建议后续评估流水线在 checkpoint 中记录实际胜出的校验器 provenance，降低评审取证成本。（属评估基础设施，非本变更缺陷。）
2. **工作区多层未提交叠加**：`git status` 混合了 structure-periodic、simulator WIP 及本变更，单看 `git diff` 无法干净界定本变更范围。建议在评审前先提交/暂存基线批次，使本变更 diff 可独立审阅（本次已用「基线快照 diff + 单文件 git diff」绕过，结论不受影响）。

## Review History

### Review R1（Cycle 1）
- **Result**: `pass`
- **Evidence**:
  - `python _evalscratch\expop_diff.py` → `ALL RED LINES PASS`；GAINED=[pdf-416,pdf-531]、LOST=[]、harm=0、precision=1、已判正 gatewayAnswer 变动=0、extra=[]。
  - `python _evalscratch\probe_exp3.py` → total 108 / new 8 / 8 条 blast-radius 全 OK / 双路径 416→C、531 single→None、531 multi→BC / `TOTAL PROBLEMS=0`。
  - `dotnet test`（4 目标类过滤）→ **364 passed / 0 failed / 0 skipped**；两个计数护栏断言显式通过。
  - `git diff data/highschool/experiment-facts.json` → 仅 +8 新事实，无既有事实删改；`pdf_questions.json` diff 不触及 416/531。
  - Grep `data-defect-register.md` → 7 个 id（416/531/180/198/333/377/528）登记行均已更正/补登。
- **Rubric**: AC-5 = 5/5（≥4 阈值）。
- **Blocked By**: 无。
- **最终 Verdict**: **pass**。全部 6 个 checkpoint 通过，无 actionable finding，无 blocked checkpoint。
