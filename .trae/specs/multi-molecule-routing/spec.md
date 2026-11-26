# 多分子路由聚合 - Product Requirements Document

## Overview
- **Summary**: 改造 `LlmGatewayService.TryRouteMolecule`，使非选择题在拥有多个 `molecule` 资产时分析**全部**资产并聚合特征（官能团/命名取并集、结论覆盖所有分子），而非当前只分析第一个（`FirstOrDefault`）。判分保持现有 OR 语义不变。
- **Purpose**: 让 pdf-322 这类 6 子问复合官能团题的引擎结论覆盖全部 6 个分子（当前只反映第 1 个子问），消除"资产已入库但路由不消费"的语义缺口，同时零回归。
- **Target Users**: 题库/评估维护者（eval 结论完整性）；最终体现为引擎对复合分子题输出完整结构特征。

## Goals
- **G1**: `TryRouteMolecule` 对非选择题分析其**全部** `molecule` 资产（不再 `FirstOrDefault` 只取第一个）。
- **G2**: 聚合多个分子的结构特征到单个 `EngineResponse`：官能团名取并集、分子名收集全部可命名者、`FinalResult` 覆盖所有分子的特征摘要。
- **G3**: 判分逻辑（`MatchMolecule` / `IsStructuredCorrect`）保持 OR 不变；不引入新的误伤渠道。
- **G4**: 全库零假阳性回归：相对基线 `t2curate3`，`LOST=0`、`harm=0`、`precision=1` 不退化，`mp27` 27/27 保持，`dotnet test` 全绿。

## Non-Goals
- **NG1**: 不把多分子题判分改为 AND（逐子问严格判分）——已与用户确认推迟到数据补齐子问标记 / 分子→子问映射后的独立 spec。
- **NG2**: 不修改 `pdf_questions.json` 的 `question/answer/note` 字段；不新增分子资产（本次纯求解器增强，不改题库数据）。
- **NG3**: 不动 `images/` 原始扫描图。
- **NG4**: 不改选择题路径（`TryRouteMolecule` 对 `choice` 早返回的行为不变）。
- **NG5**: 不重构 `MoleculeAnalyzer` 的单分子分析逻辑（仅消费其输出做聚合）。
- **NG6**: 不引入 LLM 依赖（Production 下 LLM-off，纯 RDKit 确定性）。

## Background & Context
- 当前 `TryRouteMolecule`（[LlmGatewayService.Molecule.cs:23](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs#L23)）：`imageAssets?.FirstOrDefault(a => a.Type == "molecule")` → 只取第一个分子资产分析。
- 判分 `MatchMolecule`（[GatewayVsLlmEvaluationService.cs:532](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L532)）：OR 子串——名称/极性/杂化/官能团/分子式/不饱和度任一与规范化答案匹配即命中。
- **pdf-322 现状**：6 个 `molecule` 资产已入库（前序 commit `4a474a3`），但 `question` 字段无子问 `(N)` 标记 → `MultiPartUtility.IsMultiPart=false` → 不走多部分分发，整题走 `CallEngineAsync` 只分析 `asset[0]`。`asset[0]`（曲美托嗪）识别出醚键/酰胺基，作为子串命中拼接答案 → 当前已 `gatewayCorrect=True`，但结论只反映子问(1)，子问(2)–(6) 结构虽入库却不被消费。
- **爆炸半径**（`_evalscratch/blast_radius_scan.py` 实测）：全库 545 题中 28 题有 1 个分子资产、7 题有 ≥2 个。其中 `pdf-433/434/438/439` 为选择题（`taskType=choice`），被 `TryRouteMolecule` 的 choice 守卫（L20-21）早跳过 → 不受影响；**实际受影响的非选择题仅 `pdf-199`、`pdf-208`、`pdf-322`，且三者当前均 `gatewayCorrect=True`**（`_evalscratch/inspect_multi.py` 实测）。
- **回归可证明为零**：改动只在"已有 ≥2 个分子资产的非选择题"上额外分析后续资产并取特征并集；OR 并集只会**增加**命中特征，不会把已 True 的题翻 False（`harm`/`LOST` 不可能产生），受影响 3 题均已 True 也无 `baseFalse→True` 渠道。价值在结论文本完整性，而非覆盖率数字。仍须全库 eval + mp27 实证。
- `MoleculeAnalyzer.Analyze` 为静态无状态（本地 `ROMol`，`finally` 中 `Dispose`，见 [MoleculeAnalyzer.cs:79](file:///D:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs#L79)），可安全逐资产调用。
- **红线（全程继承）**：`precision=1`、`harm=0`、`LOST=0`、`mp27=27/27` 不退化；不臆造真值；不动 `images/`；不主动 commit/push（除非用户明确要求）。

## Functional Requirements
- **FR-1**: `TryRouteMolecule` 对非选择题（通过 choice 守卫后）遍历**全部** `type=="molecule"` 的 `imageAssets`，逐资产调用 `MoleculeAnalyzer.Analyze`。
- **FR-2**: 至少一个资产解析成功即返回聚合响应；**全部**失败 → 返回 `null`（如实回退，不臆造）。
- **FR-3**: 聚合响应的 `MoleculeFunctionalGroups` = 各成功资产 `FunctionalGroups` 的**并集**（去重、保序）。
- **FR-4**: 聚合响应的 `MoleculeName` = 各成功资产非空 `MoleculeName` 的拼接（分隔符分隔），供名称维度匹配。
- **FR-5**: 聚合响应的 `FinalResult` = 各成功资产 `FinalResult` 的合并（逐分子分块、按资产顺序标记，使结论覆盖全部子问）。
- **FR-6**: 当只有 1 个 `molecule` 资产时，行为与改动前**逐字段一致**（无行为变化、无回归）。
- **FR-7**: `AssetKind` 保持 `"molecule"`，结构化判分路径不变（仍走 `MatchMolecule` OR）。
- **FR-8**: 选择题（`ChoiceOptionRe` 匹配或 `TaskType=="choice"`）仍早返回 `null`，不消费任何分子资产。

## Non-Functional Requirements
- **NFR-1**: 确定性（Production 下 LLM-off 结果稳定，checkpoint 对比可复现）。
- **NFR-2**: 不臆造：解析失败的资产不贡献任何特征（不补默认值）。
- **NFR-3**: 性能：多分子题数量极少（3 题），逐资产 RDKit 分析开销可忽略。
- **NFR-4**: 不抛未捕获异常：`payload` 结构异常按当前 `catch` 处理。

## Constraints
- **Technical**: 仅改 `LlmGatewayService.Molecule.cs`（`TryRouteMolecule` 聚合 + 可能新增私有聚合辅助方法）；不改判分代码。`MoleculeAnalyzer` 无状态可复用。
- **Business**: `precision=1` / `harm=0` / `LOST=0` / `mp27` 27/27 红线；不臆造真值。
- **Dependencies**: 基线 checkpoint `t2curate3`（673 correct）作为对比参照；mp27 守卫 `_evalscratch/verify_mp27.py`；diff `_evalscratch/diff_eval.ps1`。

## Assumptions
- **A1**: 多分子资产的顺序与子问顺序一致（银行按子问顺序挂载）；聚合结论按资产顺序输出。
- **A2**: OR 并集不会在当前题库引入新的 `baseFalse→True` 误伤（受影响 3 题均已 True），但仍以全库 diff 实证为准。
- **A3**: 现有单元测试覆盖单分子路由；聚合改动需补充多分子聚合单测。

## Acceptance Criteria

### AC-1: 全部资产被分析
- **Type**: `rule`
- **Given**: 一道非选择题拥有 N≥2 个 `type=="molecule"` 的 `imageAssets`
- **When**: 网关处理该题
- **Then**: `TryRouteMolecule` 对全部 N 个资产都调用 `MoleculeAnalyzer.Analyze`（而非仅第一个）
- **Pass Condition**: 单测构造 2 个不同分子资产的请求，断言聚合响应的 `MoleculeFunctionalGroups` 同时包含两个分子各自的官能团
- **Evidence**: 新增单元测试通过

### AC-2: 官能团并集去重保序
- **Type**: `rule`
- **Given**: 两个分子资产，FG 分别为 {醚键, 酰胺基} 与 {羟基, 硝基, 醚键}
- **When**: 聚合
- **Then**: 响应 `MoleculeFunctionalGroups` = {醚键, 酰胺基, 羟基, 硝基}（并集、去重、保留首次出现顺序）
- **Pass Condition**: 单测断言并集正确
- **Evidence**: 单元测试

### AC-3: 单分子行为逐字段不变
- **Type**: `rule`
- **Given**: 恰好 1 个 `molecule` 资产
- **When**: 路由
- **Then**: 响应与改动前逐字段一致（`FinalResult` / `MoleculeFunctionalGroups` / `MoleculeName` 等不退化）
- **Pass Condition**: 现有 molecule 路由单测全绿 + 新增"单资产==旧行为"对比单测
- **Evidence**: `dotnet test` 全绿

### AC-4: 选择题不消费分子资产
- **Type**: `rule`
- **Given**: `choice` 题（含多个分子资产）
- **When**: 路由
- **Then**: `TryRouteMolecule` 返回 `null`，不分析任何分子资产
- **Pass Condition**: 现有 choice 守卫单测通过
- **Evidence**: `dotnet test` 全绿

### AC-5: 解析失败不臆造
- **Type**: `rule`
- **Given**: 2 个资产，一个解析成功、一个失败（坏 SMILES / 坏 payload）
- **When**: 聚合
- **Then**: 失败资产不贡献任何特征；响应仅含成功资产特征；若全部失败则返回 `null`
- **Pass Condition**: 单测覆盖"部分失败"与"全部失败"两种情形
- **Evidence**: 单元测试

### AC-6: 全库零回归
- **Type**: `rule`
- **Given**: 改动后代码 + 基线 checkpoint `t2curate3`
- **When**: 全库 eval（新 tag，`-Env Production` LLM-off）并 `diff_eval.ps1 -OldTag t2curate3 -NewTag <new>`，跑 `verify_mp27.py t2curate3 <new>`
- **Then**: `LOST=0`、`harm=0`、`precision=1`、`mp27=27/27`；任何 `baseFalse→True` 翻转须人工核验为合法求解（受影响 3 题均已 True，预期无翻转）
- **Pass Condition**: diff 报告 0 误伤、0 LOST；`verify_mp27` 输出 27/27
- **Evidence**: `diff_eval.ps1` 输出 + `verify_mp27.py` 输出

### AC-7: 多分子结论覆盖完整性
- **Type**: `rubric`
- **Dimension**: 多分子题引擎结论对各子问分子特征的覆盖度
- **Scale**: 1-5
- **Anchors**: 1 = 只反映第 1 个分子（当前现状）；3 = 覆盖部分分子但有缺漏；5 = 结论按资产顺序覆盖全部成功解析的分子，官能团/命名无缺漏
- **Pass Threshold**: >= 4
- **Evidence**: 抓取 pdf-322 的 `gatewayConclusion`，人工核验覆盖 6 分子特征（预期含：醚键/酰胺基、碳碳双键/酮羰基、硝基/酯基/碳卤键、氨基/硝基/羰基、2,4,6-三溴苯酚、邻苯二酚）；pdf-199/208 同理覆盖各自全部资产

## Open Questions
- [x] 判分语义保持 OR vs 收紧 AND → 已与用户确认：**保持 OR + 聚合特征**（本 spec）。AND 逐子问严格判分列为后续独立 spec（NG1）。
- [ ] （实现期定，不阻塞）聚合 `FinalResult` 的分块格式：每块前是否加 `(n)` 序号——倾向加，便于阅读且不影响判分；实现时确定。
