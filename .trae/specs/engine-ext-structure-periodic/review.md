# Review — engine-ext-structure-periodic（结构/周期律/化学键比较引擎扩展，pdf-458）

- **Reviewer 立场**：实现后独立复核（同一上下文，证据驱动，逐条对照 spec.md 的 6 条 AC）。
- **基线**：`_evalscratch/snapshot_after_dr1.json`（gatewayCorrect=701, bothWrong=56, harm=0, precision=1, LOST=[]）。
- **新快照**：`_evalscratch/snapshot_after_structperiodic.json`。
- **结论**：**6/6 AC 全部 PASS**，红线零回归，pdf-458 判正，无单题策展，pdf_questions.json 本批零改动。

---

## AC-1: pdf-458 判正 — **PASS**

- 证据（`checkpoint_after_structperiodic.jsonl`，pdf-458 行）：
  - `gatewayCorrect=true`（基线 `after_dr1` 中为 false，∈ bothWrong）。
  - `gatewayAnswer="正确选项为 BD"`、`gatewayConclusion="正确选项为 BD"`，含独立字母 token `BD`，经 `GatewayVsLlmEvaluationService.IsAnswerCorrect` 多选字母门控判正。
  - `gatewayUsedEngine=true`、`gatewayTruthAdopted=true`、`gatewayConclusionState="Confirmed"`。
- 端到端单测 `StructurePropertyStatementJudgesTests.Pdf458_MultiSelect_Solves_BD_AndGradedCorrect` 断言 `AnswerLetter=="BD"` 且 `IsAnswerCorrect` 判正，通过。
- 逐选项引擎推导与化学真值一致：A=False（漏 3d¹⁰）、B=True（N→B 配位键）、C=False（GaN 原子晶体）、D=True（BCl₃ sp² 120°）。

## AC-2: 零回归红线 — **PASS**

`_evalscratch/structperiodic_diff.py`（复用 `dr1_diff.py` 同方法）输出：

```
BASE(dr1)  gatewayCorrect=701  harmCount=0  enginePrecision=1
AFTER      gatewayCorrect=702  harmCount=0  enginePrecision=1
LOST  (gw true->false) = []
GAINED(gw false->true) = ['pdf-458']
previously-correct cases with changed gatewayAnswer = 0
extra GAINED (non pdf-458) = []
ALL RED LINES PASS
```

- LOST=[] ✓、harm=0 ✓、precision=1 ✓、gatewayTruthCorrect 701→702（≥701）✓、pdf-458 ∈ GAINED ✓。
- 无额外 GAIN（不存在需额外化学核对的存疑收益）。

## AC-3: 新原语不蒙数（负例守卫） — **PASS**

`tests/CogniThink.TutorBrain.Tests/StructurePropertyStatementJudgesTests.cs`（26 条）覆盖各原语负例，全部返回 `Verdict.Undecided`：

- 电子排布：未知元素 / 无排布锚点 / 无声称排布值 / 比较型「Ga 与 Ge 排布不同」/ 轨道表示式 → Undecided。
- 键角：比较型 `NH₃>NO₃⁻` / 未知微粒 `XyZ` / 无键角数值 → Undecided。
- 晶体类型：表外 `X₃Y` / 无晶体类型断言 → Undecided。
- 配位键：表外 `XYZ` / 无配位键断言 → Undecided。
- **全库护栏**：既有 `MultiSelectChoiceValidatorTests.FullBank_AnyMultiSelectConclusion_GradedCorrect_NoHarm` 对全库 757 题跑 `MultiSelectChoiceValidator.TryDecide`，断言任何非 null 结论都判分正确——新原语可能让此前 null 的题突然产出结论，此测试即 harm 早期信号。全量 `dotnet test` **1758/0 通过**，护栏未被击穿 ⇒ 新原语在全库范围内零假阳性。

> 过程发现（证明守卫有效）：单测首轮暴露 3 个实现缺陷，均在 eval 前被捕获并修复，未流入红线评估：
> 1. 电子排布亚层正则 `(\d+[spdf])(\d+)` 对无分隔符拼接串（`1s22s22p6…`）贪婪错配 → 改为非贪婪计数 + 前瞻 `([1-7])([spdf])(\d+?)(?=[1-7][spdf]|[^0-9]|$)`。
> 2. 晶体/配位键物种边界用 `char.IsLetterOrDigit`，中文字符被当成字母致 `SiO2是…`/`NH3BH3的…` 匹配失败 → 新增 `StatementNorm.IsAsciiAlnum`，仅 ASCII 字母数字作边界。
> 3. `Fe2+` 中元素符号 `Fe` 紧跟电荷数字被 Sym 边界排除 → Sym 允许吞掉尾随 `\d+[+\-]`。

## AC-4: 既有链路行为不变 — **PASS**

- 701 道已判正题 `gatewayAnswer` 逐题零变更（diff 脚本：`previously-correct cases with changed gatewayAnswer = 0`）。
- 源码范围核对（本批 `.cs` 净改动）：
  - 新增 4 文件：`ElectronConfigStatementJudge.cs` / `VseprBondAngleStatementJudge.cs` / `CrystalTypeStatementJudge.cs` / `CoordinateBondStatementJudge.cs`（均实现 `IStatementJudge.Judge`，不带 id，不发 ReasoningNodes）。
  - 新增 1 文件：`StatementNorm.cs`（共享归一化，纯函数）。
  - 追加：`Tier2FactData.cs`（数据模型类）、`VseprSolver.cs`（两个 internal 只读访问器 `TryGetShapeInfo` / `FindKnownSpeciesInText`，不改既有 `TrySolve*` 行为）、`MultiSelectChoiceValidator.cs`（`CompositeStatementJudge` 构造处追加 4 个原语实例，在 FactBase/Counting/RedoxElectron 之后）。
  - **未触碰** `LlmGatewayService.CallChoice` 瀑布链顺序、任何既有上游校验器文件、判分逻辑。
- 新原语仅在 `MultiSelectChoiceValidator`（瀑布末端 fallback）触发；已解题由上游校验器命中、不到达 fallback，故字节级不受影响（diff 已实证）。

## AC-5: 真值数据无单题策展（rubric） — **PASS（评分 5/5）**

逐条核对 `data/highschool/structure-property-facts.json`：

- `crystalTypes`（25 条）：GaN/SiC/SiO₂/BN/金刚石/Si → 原子晶体；CO₂/H₂O/NH₃/PH₃/CS₂/SiCl₄/GeCl₄/SnCl₄/C₆₀ → 分子晶体；NaCl/KCl/MgO/ZnO/ZnS/NaOH/KNO₃ → 离子晶体；Cu/Fe/Al → 金属晶体。**全部为教材通用真值，化学正确**。
- `coordinateBonds`（7 条）：NH₃BH₃ / BF₃NH₃ / NH₄⁺ / [Cu(NH₃)₄]²⁺ / [Ag(NH₃)₂]⁺ / SiF₆²⁻ → 含配位键（含给体/受体）；CH₄ → 不含。**全部教材通用、化学正确**，并带中文别名覆盖常见写法。
- 无任何条目只为匹配 pdf-458 单题选项文本而存在；GaN、NH₃BH₃ 虽出现在 pdf-458，但均为跨题复用的标准教材例子。
- 评分：**5**（每条均为教材通用化学真值、化学核对无误、命名/别名覆盖常见写法）≥ 阈值 4。

## AC-6: 数据缺陷登记册留痕 — **PASS**

`git diff data-defect-register.md`：

- C 类 pdf-458 行：更新为「✅ 已由引擎解出（GAIN +1）；选项 B 实为完整文本描述，非图像缺失，原登记系误判」，指向 AB 类。
- H 类 pdf-458 行：同步为已闭环。
- AA.2「未映射键 14 道」→ 修正为 13 道，pdf-458 移出（已由引擎解出，化学推导与存储键一致）。
- 新增 **`## AB 类`** section：记录本批引擎范围、pdf-458 闭环机制与 eval 证据、以及 pdf-196/190/207 不在引擎范围的理由（引用 K 类「T1 留痕」/ AA.2「B 类张冠李戴」/ F·Y 类「开放论述无唯一解」）。
- 快照核实：pdf-196 / pdf-190 / pdf-207 仍 `gatewayCorrect=false`、`gatewayAnswer=""`（保持 null，无副作用）；pdf-458 `gatewayCorrect=true`。
- `pdf_questions.json` 本批零改动（其工作区 diff 仅为上一批 dr1 对 pdf-161/413 的 question 回填，与本批无关）。

---

## 总体结论

| AC | 类型 | 结果 |
|----|------|------|
| AC-1 pdf-458 判正 | rule | PASS |
| AC-2 零回归红线 | rule | PASS |
| AC-3 不蒙数负例守卫 | rule | PASS |
| AC-4 既有链路不变 | rule | PASS |
| AC-5 真值数据无单题策展 | rubric | PASS（5/5） |
| AC-6 登记册留痕 | rule | PASS |

**全部通过。** 实现严格遵守红线（precision=1、harm=0、LOST=[]、不蒙数、不做单题策展、不改 pdf_questions.json、不碰既有瀑布链），pdf-458 由 701→702 净增益 +1，沉淀 4 个可复用结构域判定原语 + 1 份通用真值数据。
