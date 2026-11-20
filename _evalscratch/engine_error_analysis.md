# 引擎"答错"根因分析（engine_dump_v3 / after_stage6）

> 生成时间：Stage 6 完成后。数据来源：`_evalscratch/engine_dump_v3.json`（723 条）与规范活体快照 `_evalscratch/after_stage6_eq.json`（723 条，4 个 bucket）。
> 方法：严格基于可追溯证据（dump 字段、快照字段、源码行号、复现测试），不臆测。

## 0. 结论速览（TL;DR）

1. **引擎从未输出"被采用的错误化学答案"**：全局 `harm=0`、`adoptedWrong=0`、`enginePrecision=1.0`。采纳门（`BuildGatewayAnswerAsync` 的严格剔除口径）拦住了所有未通过校验的引擎结论。
2. 表面上"引擎尝试但未答对"的 **6 例**，逐一排查后根因**均不在引擎化学推理**：
   - 3 例是曲线读取引擎产出**不可用的中间量**（非化学错误，且被门控滤掉）；
   - 3 例是**数据集答案键本身错位/错误**（引擎实际答对了，被判分误判为错）。
3. dump 记录的 **48 个 `NullReferenceException`** 在**当前代码中无法复现**（离线复跑 + 活体快照均无异常），来自已删除的 `_EngineDumpV3` 探针或其生成时的旧路径。但代码中存在**一个确定的潜在 NRE 站点**（`LlmGatewayService.cs:442`），活体 LLM 未触发，属于应加固的健壮性缺口。
4. 其余"未答对"是**覆盖缺口**（引擎域外返回 null，按红线设计），不是"答错"。

---

## 1. 口径与数据来源

| 口径 | 含义 |
|---|---|
| `engineSolved` | `gatewayTruthAdopted=true AND gatewayCorrect=true`（引擎真值被采纳且判分正确） |
| `harm` | `gatewayTruthAdopted=true AND gatewayCorrect=false AND pureLlmCorrect=true`（引擎把对的拉成错的） |
| `adoptedWrong` | `gatewayTruthAdopted=true AND gatewayCorrect=false`（采纳了错误结论） |
| dump 的 `newCorrect` | `(adoptedAns != null && IsAnswerCorrect(adoptedAns, c)) || structuredHit` |

全局（after_stage6_eq）：`engineSolved=266`，`harm=0`，`adoptedWrong=0`，`pureLlmCorrect=551`。

dump 矩阵（`newUsedEngine × threw × newCorrect`）：

```
[408] False|False|False   引擎未介入（域外/null，安全）
[10]  False|True|False    dump 探针记录抛错（choice，10 条）
[6]   True|False|False    引擎返回真值但未通过校验 ← 本分析重点
[261] True|False|True     引擎解对
[13]  True|True|False     dump 探针记录抛错（reaction_check，13 条）
[25]  True|True|True      dump 探针记录抛错但最终判对（ion_check，25 条）
```

---

## 2. 分类一：引擎采用了错误答案 → **0 例**

`harm=0`、`adoptedWrong=0`。**没有任何一道题被引擎的错误答案"带偏"。** 这是最关键的结论：用户感知的"引擎答错"在"被采纳并交付给学生"的层面为零。

作用机制：[LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) 的 `CallEngineAsync` 只产出结论；是否采纳由 [GatewayVsLlmEvaluationService.cs BuildGatewayAnswerAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs)（L536–633）逐 taskType 加门：
- 选择题：结论必须含独立选项字母才采纳（L575–578）；
- equilibrium/ion_check 选择题：裸结论不含字母 → null（L583–596）；
- atomic_structure / vsepr / equilibrium 非选择：必须 `IsAnswerCorrect` 通过才采纳（L558–589）；
- reaction_check：必须含真值签名（氧化剂/离子方程式/√×判决串等）才采纳，占位结论（"已匹配到相关反应"）天然排除（L618–622, L654–682）。

---

## 3. 分类二：引擎返回"未通过校验的真值结论" → **6 例**（全部被滤掉，安全）

这 6 例 `newUsedEngine=true, newCorrect=false, threw=false`，即引擎给了 `Ok=true` 的结论，但没被采纳。逐一排查：

### 3.1 曲线读取引擎误触发，产出不可用中间量 —— 3 例（pdf-037 / pdf-077 / pdf-272）

| id | taskType | 引擎实际返回（gatewayConclusion） | 题目真正要的答案 |
|---|---|---|---|
| pdf-037 | choice | `曲线「丙」的最大值点为 (x=0.35, y=0.85)…`（归一化坐标） | 选出选项 **D**（关于图丁的定性结论） |
| pdf-077 | choice | `x=0.12 时，曲线「Fe³⁺」的 y = 0.5`（归一化读数） | 选出选项 **C**（需多步化学计量推理） |
| pdf-272 | equilibrium | `x=2.5 时，曲线「H₂C₂O₄」的 y = 0.8`（归一化读数） | 写电离方程 + 计算 Ka1 + 写离子方程式 |

**根因**：`TryRouteCurve`（[LlmGatewayService.cs:317](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L317)）在含曲线资产的题上抢占，返回原始归一化坐标。这类坐标是**中间量**，既不能映射到选项字母（pdf-037/077 是选择题），也不能替代多步定量/定性推导（pdf-272）。equilibrium 分支已优先于曲线（[L306–310](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L306-L310)），但 pdf-272 的确定性求解器未命中，仍回退到了曲线读数。

**性质**：不是化学答错（数值本身可能对），而是**产出了不可用的结论类型**。**安全**：3 例均 `gatewayTruthAdopted=false`，被门控滤掉（选择题不含字母 / 非选择 `IsAnswerCorrect` 不过），未交付给学生。`pureLlmCorrect` 均为 true，学生答案不受影响。

### 3.2 数据集答案键与题干错位 —— 2 例（pdf-108 / pdf-173），引擎实际答对

| id | 题干实际问的 | 引擎返回 | 数据集 answerKeywords（来自 raw pdf_questions.json） |
|---|---|---|---|
| pdf-108 | 基态 **Ni** 的价电子排布式 | `3d⁸4s² …`（化学正确，Ni=[Ar]3d⁸4s²） | `2Al2O3(熔融)电解/冰晶石→4Al+3O2↑, Ga³⁺+4Cl⁻=[GaCl4]⁻, 铁 [FeCl4]⁻, 高 NaCl`（属于另一道"提 Ga/电解铝"工艺题） |
| pdf-173 | 基态 **Cu** 原子的价层电子排布式 | `3d¹⁰4s¹ …`（化学正确，Cu=[Ar]3d¹⁰4s¹） | `B`（note 是关于 K⁺/Al³⁺ 半径、O/S 电负性的另一道选择题解析） |

**根因**：[pdf_questions.json](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json) 中这两条的 `question` 与 `answerKeywords`/`note`/`answer` **来自不同题目**（PDF 抽取错位噪声）。引擎结论正确，但 `IsAnswerCorrect` 拿错位的关键词比对 → 判错 → 未采纳。

**性质**：**数据 bug，非引擎错**。引擎化学推理正确。

### 3.3 数据集答案键自相矛盾且错误 —— 1 例（pdf-507），引擎实际答对

| 字段 | 内容 |
|---|---|
| 题干 | SnCl₂ 分子的 VSEPR 模型名称 |
| 引擎返回 | `平面三角形 平面正三角` |
| 数据集 answerKeywords | `(1)四面体形 (2)5sp³` |
| 数据集**自身 note** | `SnCl₂中心Sn有2个σ键和1对孤电子对，价层电子对数为3，VSEPR模型为平面三角形` |

**根因**：`answerKeywords` 写"四面体形"，但其 `note` 明确写"价层电子对数为 3 → 平面三角形"，**答案键与自身解析矛盾**。化学上 SnCl₂ 为 AX₂E¹（2 σ键 + 1 孤对），价层电子对数 = 3，VSEPR 电子构型 = 平面三角形 —— **引擎正确，数据集答案键错误**。

**性质**：**数据答案键错误，非引擎错**（建议人工复核该题标准答案）。

### 3.4 小结：6 例中引擎化学推理全部正确或产出不可用中间量，无"化学答错"

| 子类 | 例数 | 引擎化学是否错 | 是否被采纳（安全） |
|---|---|---|---|
| 曲线引擎产出不可用中间量 | 3 | 否（数值可能对，类型不可用） | 否（被门控滤掉） |
| 数据集答案键错位 | 2 | 否（引擎答对） | 否（被判分误判） |
| 数据集答案键错误 | 1 | 否（引擎答对） | 否（被判分误判） |

> 附带说明：`enginePrecision=1.0` 是相对于数据集答案键的口径。pdf-108/173/507 证明数据键本身存在噪声，因此"100% 精确率"应理解为"被采纳的引擎结论未与**正确**的数据键冲突"；对数据键正确性的全量审计超出本次范围。

---

## 4. 分类三：引擎抛异常 —— dump 记录 48 例，**当前代码不复现**

### 4.1 现象

dump 中 `threw=true` 共 48 条，**全部**是同一条消息：`NullReferenceException: Object reference not set to an instance of an object.`（dump 只存了 `ex.Message`，未存堆栈，因此 48 条字面相同）。按 taskType：ion_check 25、reaction_check 13、choice 10。

### 4.2 复现实验（当前代码）

写了临时探针 `_TmpExceptionStacks`（已删除，见下文），两种方式复现这 48 题：

1. **离线复跑**（本环境 `llmAvailable=False` → 走 `FallbackParse` 构造干净请求）：**48/48 全部 `NO_THROW`**，正常返回结论（25 ion 全对、13 reaction 返回占位结论、10 choice 返回 null-Ok）。见 `_evalscratch/exception_stacks.txt`。
2. **对照规范活体快照** `after_stage6_eq.json`：这 48 题在活体评估中**全部有正常的 `gatewayConclusion` 与判分结果**，无任何异常迹象：
   - 25 ion：`gatewayCorrect=True, gatewayTruthAdopted=True`（全对）；
   - 13 reaction：返回"已匹配到相关反应"/"已解析出相关物质与离子"占位结论，`gatewayTruthAdopted=False`（被滤掉，正确）；
   - 10 choice：`gatewayOk=null`、`gatewayConclusion` 为空（引擎域外返回 null，正确）。

**结论**：这 48 个异常**不是当前引擎的现存缺陷**。它们来自已删除的 `_EngineDumpV3` 探针（其调用路径与 `gatewayUsedEngine`/`threw` 字段口径均不同于 `EvaluateCaseAsync`）或该探针生成时的旧代码。**当前生产路径（`ProcessStudentMessageAsync` → `CallEngineAsync`）对这 48 题不抛异常。**

### 4.3 但定位到一个确定的潜在 NRE 站点：`LlmGatewayService.cs:442`

用确定性空字段探针（无 LLM，直接调 `CallEngineAsync`）复现，拿到带行号的真实堆栈：

```
[NRE] ion_check Reaction=null: NullReferenceException @
  LlmGatewayService.CallIonCheck(EngineRequest req) in ...LlmGatewayService.cs:line 442
[NRE] ion_check Left=null Right=null: ArgumentNullException @ 同 line 442
[OK]  ion_check clean / reaction_check clean: 正常返回
```

对应源码（[LlmGatewayService.cs:440-442](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L440-L442)）：

```csharp
private EngineResponse CallIonCheck(EngineRequest req)
{
    var ions = req.Reaction.Left.Concat(req.Reaction.Right).ToList();  // L442：未判空
```

- `req.Reaction == null` → `req.Reaction.Left` 解引用 → **NullReferenceException**（与 dump 的异常类型完全吻合）；
- `req.Reaction.Left == null` 或 `Right == null` → `.Concat` 抛 `ArgumentNullException`。

### 4.4 为什么活体不触发、为什么 dump 记录了

- `ParseStudentInputAsync` 在 LLM 解析成功时会调 `NormalizeRequest`（[L162-194](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L162-L194)），其中 **只兜底了 `req.Reaction ??= new EngineReaction()`（L166），没有兜底 `Reaction.Left`/`Right`**。因此：
  - 若 LLM 输出 `"reaction": null` → `NormalizeRequest` 修复为非 null → 活体安全；
  - 若 LLM 输出 `"reaction": {"left": null, ...}` → `Left` 仍为 null → 会抛 `ArgumentNullException`（但活体快照中 ion_check 全部正常，说明实际 LLM 未输出 null left）。
- `FallbackParse`（LLM 不可用路径，[L210-243](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L210-L243)）直接 `new EngineRequest {...}`，`Reaction`/`Left`/`Right` 走 C# 字段初始值（非 null）→ 安全。
- dump 的探针很可能**绕过了 `NormalizeRequest`**，直接把 LLM 反序列化的请求（含 null Reaction/Left）喂给 `CallEngineAsync` → 触发 L442 的 NRE。这解释了为何 dump 记录 NRE 而当前活体路径不复现。

### 4.5 加固建议（低风险，防止未来 LLM 输出漂移）

在 `NormalizeRequest` 中补齐：
```csharp
req.Reaction ??= new EngineReaction();
req.Reaction.Left ??= new List<string>();
req.Reaction.Right ??= new List<string>();
req.Reaction.Conditions ??= new List<string>();
req.Smiles ??= new List<string>();
req.Known ??= new EngineKnown();
req.Options ??= new EngineOptions();
```
并在 `CallIonCheck` L442 前加防御性判空。这是健壮性加固，不改变任何现有行为（当前字段初始值已是 non-null），零回归。

---

## 5. 分类四：引擎不会答 / 返回 null（覆盖缺口，非"答错"）

按红线"无唯一解 → Ok=null，不蒙数"，引擎域外返回 null 是**设计行为**，不是错误。各 taskType 覆盖情况（after_stage6_eq）：

| taskType | 总数 | 引擎解出 | 未解出 | 缺口性质 |
|---|---|---|---|---|
| choice | 467 | 147 | 320 | 多数需图像读取/多步推理/引擎未建模知识点（物质分类、价类二维图、转化链、化学用语等），引擎域外返回 null |
| calculation | 60 | 20 | 40 | 求解器覆盖窄；部分被分类到 calculation 的"标况非气态陷阱"题（d5-1/d5-2：SO3/己烷标况非气态）未走到 `StoichiometryStateTrapSolver`（该求解器仅在 concept/unknown 触发，L351-352） |
| equilibrium | 31 | 13 | 18 | 多步平衡计算/图表定量题，现有求解器（守恒式、酸性排序）覆盖窄 |
| reaction_check | 47 | 34 | 13 | 未命中真值签名的题返回"已匹配到相关反应"占位结论并被滤掉（pdf-010/119/134…）；多为流程题中写方程式/判可行性，引擎无法唯一判定 |
| concept | 29 | 0 | 29 | **按红线设计**：纯概念不走引擎（`ConceptFactSolver` 命中的会改判 concept_fact） |
| unknown | 27 | 0 | 27 | **按红线设计**：分类链无法判定 → 不强制路由 |
| structure_verify | 3 | 1 | 2 | 天花板（需文本→结构 + 对称性/手性分析，无现成引擎），见 `_evalscratch/ac28_ceiling.md` |
| balance | 1 | 1 | 0 | — |
| ion_check | 26 | 26 | 0 | 全覆盖 |
| atomic_structure | 12 | 6 | 6 | 电子排布/轨道表示式中未命中锚点的返回 null（pdf-188/481/488/489）；pdf-108/173 引擎答对但被错位数据键误判（见 3.2） |
| vsepr | 9 | 7 | 2 | 查表未覆盖的微粒/比较推理题返回 null；pdf-507 引擎答对但被错误数据键误判（见 3.3） |
| concept_fact | 11 | 11 | 0 | 全覆盖 |

代表性未解出题（引擎返回 null/空）：
- choice：pdf-004（分类）、pdf-006（C₆₀ 球碳盐）、pdf-014（价类二维图）、pdf-017（物质转化链）—— 均需 LLM 级推理，引擎无对应模型；
- calculation：trap-limit-2（氧化剂还原剂质量比，实为反应题被误分类）、d5-1/d5-2（标况陷阱未触达）；
- reaction_check：pdf-010/119/134/163/164/175（流程题写方程式/判可行，返回占位结论被滤）。

---

## 6. 修复优先级建议

| 优先级 | 项 | 动作 | 预期收益 | 风险 |
|---|---|---|---|---|
| P1（数据质量） | pdf-108/173 答案键错位、pdf-507 答案键错误 | 校正 `pdf_questions.json` 这 3 条的 `answerKeywords`/`note`/`answer` 与题干对齐 | 消除 3 例假阴性，让引擎正确结论被正确采纳，覆盖数 +3 | 需人工核对标准答案 |
| P2（健壮性） | `CallIonCheck` L442 未判空 + `NormalizeRequest` 未兜底 Left/Right | 补 `??= new()` 与防御判空（§4.5） | 防止未来 LLM 输出 null 字段时抛异常（当前未触发，属提前加固） | 零回归（不改现有行为） |
| P2（防误触发） | 曲线引擎在 choice/多步 equilibrium 题抢占 | 对曲线资产题，仅当查询意图明确（ValueAtX/极值/交点）且结论能映射到答案时才 `Ok=true`，否则返回 null 回退 | 减少 3 例"产出不可用中间量"，让曲线引擎更克制 | 需回归曲线已命中集（T1/CurveGatewayTests 等），避免掉覆盖 |
| P3（覆盖） | calculation 中标况陷阱题未触达求解器 | 将 `StoichiometryStateTrapSolver` 的触发从 concept/unknown 扩展到含"标况/22.4L/分子数"的 calculation | 覆盖 d5-1/d5-2 等陷阱题 | 需防误命中正常计算题 |
| P3（覆盖） | choice/reaction_check/equilibrium 大面域外 | 按已有 Stage 路线逐类扩展求解器 | 提升覆盖 | 持续工作，非本次范围 |
| 不做 | 强制 concept/unknown 走引擎 | 违反红线，不做 | — | — |

---

## 7. 附：证据文件

- `_evalscratch/engine_dump_v3.json` — 原始 dump（723 条，含 threw/exception 字段，但 exception 仅存 message）
- `_evalscratch/engine_dump_v3_summary.txt` — dump 汇总（EXCEPTION_IDS 48 条）
- `_evalscratch/after_stage6_eq.json` — 规范活体评估快照（723 条，4 bucket）
- `_evalscratch/exception_stacks.txt` — 48 题离线复跑结果（全 NO_THROW）
- `_evalscratch/nre_probe.txt` — 空字段探针结果（定位 L442）
- `_evalscratch/ac28_ceiling.md` — structure_verify/titration 天花板留痕
