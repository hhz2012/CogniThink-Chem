# 独立评审 — redox-statement-judge-pdf075（路径 B 接入 redox 求解器）

- **评审对象**: spec `redox-statement-judge-pdf075` 的实现（T1–T11）
- **基线快照**: `_evalscratch/snapshot_after_datarepair_engine_batch3.json`（gwCorrect=708, bothWrong=49, harm=0, enginePrecision=1）
- **新快照**: `_evalscratch/snapshot_redox_stmt.json`（Production, LlmAvailable=False, 757 题, 0 failed）
- **结论**: **PASS** — 全部 AC 通过，红线零违反；无 actionable 未修复项。

---

## 1. 验收标准逐项核对

### AC-1 pdf-075 选项恢复正确 — ✅ PASS
- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` 中 pdf-075 的 `options` 已替换为原书 page_022 真选项（A: OH⁻还原性>H₂O；B: 反应Ⅳ每生成1 mol O₂转移4 mol电子；C: 反应ⅰ～ⅳ中有3个氧化还原反应；D: Xe 半径大/电离能小→化合物更多）。
- `answer` 保持 `"B"`；`answerKeywords` → `["B","反应Ⅳ中每生成1 mol O₂转移4 mol电子"]`；`question`/`note`/`type` 未动。
- JSON 合法（545 条）。`data-defect-register.md` D 类已登记并标记 ✅ 已闭环。
- 证据：T1 探针输出 + 已更新的登记册行（L106）。

### AC-2 引擎端到端解出 pdf-075 → B — ✅ PASS
- 判定链路：A=True（事实 GC-075-A，stemMatch 锚定 `Na4XeO6`）、B=False（`RedoxElectronStatementJudge`，反应Ⅳ 实际每 mol O₂ = 6e ≠ 4）、C=True（`RedoxCountStatementJudge`，ⅰ/ⅱ/ⅳ 氧还、ⅲ 非氧还 → 3）、D=True（事实 GC-075-D）。问法「错误的是」→ 唯一假项 **B**。
- 护栏单测 `StatementChoiceValidatorTests.KnownQuestion_SolvedToExpectedLetter(pdf-075→B)` 通过。
- 集成 eval：`checkpoint_redox_stmt.jsonl` 中 pdf-075 `gatewayCorrect=True`、`gatewayUsedEngine=True`、`gatewayAnswer='错误选项为 B'`、期望 `B`。

### AC-3 氧还计数 / 角色比值 judge 保守正确 — ✅ PASS
- `RedoxCountStatementJudgeTests`（11 项）+ `RedoxRoleRatioStatementJudgeTests`（15 项）全绿，含：
  - 计数正例 pdf-075-C 声称 3→True、声称 2/4→False、范围外/未解析方程→Undecided；
  - 歧化比值（3S+6NaOH）被氧化:被还原=1:2→True、2:1→False、倒序「被还原与被氧化为 2:1」→True；
  - 角色正/反例（Cl2 只作氧化剂→True、Na 是氧化剂→False、S 既是又是→True、S 只作氧化剂→False）；
  - 产物角色（NaCl 是氧化产物→True、H2O2 分解中 H2O 是氧化产物→False 化学正确、反应物被问作产物→Undecided）；
  - 域外/歧义：非氧还方程问比值→Undecided、跨元素比值→Undecided、无关键词→Undecided、不可解析方程→Undecided。
- 证据：`_evalscratch/roletests_run.log`（26/26 通过）。

### AC-4 单选择陈述路径行为正确 — ✅ PASS
- `StatementChoiceValidator.TryDecide`：逐选项经 `MultiSelectChoiceValidator.StatementJudge.JudgeWithIds` 判定；任一选项 Undecided→整题 `null`；候选（问法方向匹配项）唯一→返回字母；≥2→`null`（交 MultiSelect）；0→`null`。
- 插入点：`LlmGatewayService.CallChoice` 中 `StoichiometryChoiceValidator` 之后、`MultiSelectChoiceValidator` 之前 —— 加性末端，不抢跑更早的单选校验器。
- 阈值单测 4 项全绿：唯一真候选→字母、两真候选→null、含 Undecided→null、错向假项不唯一→null。
- 证据：`_evalscratch/stmttests_run.log`（7/7 通过）。

### AC-5 全库回归零伤害 / 零 LOST / 精度 1 — ✅ PASS
`_evalscratch/redox_stmt_diff.py` 输出：
```
BASE(batch3)  gwCorrect=708 bothWrong=49 harmCount=0 enginePrecision=1
AFTER(redox)  gwCorrect=709 bothWrong=48 harmCount=0 enginePrecision=1
LOST   = []
GAINED = ['pdf-075']
previously-correct changed gatewayAnswer = 0
pdf-075: gwCorrect=True  gwAns='错误选项为 B'  expected='B'
extra GAINED (beyond pdf-075) = 0
ALL RED LINES PASS
```
- `LOST=[]`、`harmCount=0`、`enginePrecision=1`、`GAINED⊇{pdf-075}`（恰好等于）；
- 此前正确的 `gatewayAnswer` 变化数 = 0（字节级不变，NFR-2 满足）；
- 全库扫描额外收益：0（孤立路径能判出的 ~280 题全部已由瀑布链其它校验器正确解出，故不计 GAINED，也不构成伤害）—— 由 `StatementChoiceValidatorTests.FullBank_AllAdoptions_GradedCorrect` 与集成 diff 双重确认 precision=1。

### AC-6 护栏测试通过且无残留 WIP — ✅ PASS
- 相关测试套件全绿：`Redox*|StatementChoice|MultiSelectChoice|Batch3Choice` 共 **442 项通过**（`_evalscratch/redox_regression_run.log`），其中含 28 s 全库 `FullBank_AfterRedoxWiring_MultiSelectAndCounting_NoWrongAdoption`；Core `BalancingTests` **24 项通过**（确认 T2 对 `EquationBalancer` 的修改无回归）。
- 既有编译错误 `SolveStructuredEnricherTests.cs`（非本任务引入）按 spec 协议跑测期间改名 `.wipbak`、测后已**还原为 `.cs`**；工作区 `*.wipbak` 与 `_Probe*.cs` 残留扫描 = 0。

### AC-U1 保守性与可维护性（rubric ≥4）— ✅ PASS（评分 5）
- 每个新 judge 的 `Undecided` 边界清晰且有注释：标签范围不明 / 方程不可解析配平 / 物种侧位歧义 / 跨元素比值语义不一致 / 非整数系数 / 非氧还方程 → 一律 `Undecided`，不蒙数。
- **未修改 Core `RedoxBalancer`**（经验 100025931：避免连锁回归）。歧化时 `OxidationChange.FromText/ToText` 为该侧全集、非单一半反应跃迁，`RedoxRoleRatioStatementJudge` 在 judge 侧自行从 `OxidationStateAnalyzer.Assign` + 配平系数重建氧化数多重集并排序配对，得到正确的歧化比值，不依赖 Core 不可靠文本。
- 唯一 Core 修改：`EquationBalancer.Balance` 的 `basis.Count > 1` 分支对用户给定系数做 `ConservationViolations` 校验，通过即接受（反应Ⅳ null-space dim 2 的 Xe 组合方程），不臆造系数；违反守恒的欠定方程（如 `H2+O2+C+O2=H2O+CO2` 全 1 系数）仍被拒绝 —— `BalancingTests.Balance_ImpossibleEquation_ReportsNoSolution` 锁定。
- 与既有 `IStatementJudge` 模式一致（`Judge`/`JudgeWithIds`，冲突→Undecided）；新增路径为加性末端 fallback，未改变既有瀑布顺序与已解题行为。

---

## 2. Open Questions 关闭情况
- **A/D 机制**：采用 `stemMatch` 锚定的窄域事实（GC-075-A：还原性 + `Na4XeO6`；GC-075-D：电离能/Xe 化合物 + `Na4XeO6`）。全库 `BuildTestCases()` 零 stray-hit 验证通过（仅命中 pdf-075 的 A/D）。
- **额外收益**：全库扫描未发现 pdf-075 之外可保守解锁的自洽氧还选择题（GAINED 恰好 = {pdf-075}），符合零伤害纪律下的保守结果。

---

## 3. 残留项（非阻塞，留待后续）
1. pdf-075 的 `note` 仍张冠李戴（谈及旧变体选项 A），不影响判分，留待数据管道重写（已在登记册标注）。
2. `SolveStructuredEnricherTests.cs` 的既有编译错误与本任务无关，按 spec 协议未修复；还原后该测试项目整体构建仍会因此报错（既有状态，非回归）。
3. `_evalscratch/` 下新增 `snapshot_redox_stmt.json`、`checkpoint_redox_stmt.jsonl`、`redox_stmt_diff.py` 及若干 `.log` 为 eval 产物，按仓库既有惯例留存于 `_evalscratch/`。

---

## 4. 评审结论
**PASS。** 全部 AC（AC-1 ~ AC-6、AC-U1）通过，红线 `LOST=[] / harm=0 / enginePrecision=1` 零违反，GAINED 恰好为 {pdf-075}，无 actionable 未修复项。实现严守「加新分支、不替旧逻辑、Undecided 优先于猜测」的纪律。
