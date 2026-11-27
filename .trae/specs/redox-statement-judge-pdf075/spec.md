# 氧化还原陈述判定扩展（路径 B）— 求解 pdf-075 等氧还计数选择题 - Product Requirements Document

## Overview
- **Summary**: 扩展引擎的氧化还原陈述判定能力——新增「氧还反应计数」「角色/比值」判定原语（复用已有的电子转移计数原语与 Core 的 `RedoxBalancer` / `OxidationStateAnalyzer`），并新增一条「逐选项经 `IStatementJudge` 判定 + 选唯一」的单选陈述路径；同时恢复 pdf-075 错乱的选项文本（以原书为权威，已获用户批准），使符号引擎在无 LLM 的生产环境下端到端解出 pdf-075（→B）。再以 blast-radius 探针对全 757 题回归，扫描并解锁其它自洽的氧化还原选择题。全程保守：任一陈述不可安全判定 → `Undecided`，不蒙数。
- **Purpose**: 把 pdf-075（当前 `bothWrong`，引擎+LLM 双错）变为引擎正确，并沉淀可复用的氧还陈述判定通用能力，扩大引擎对氧还计数/角色/比值选择题的覆盖面，同时严守 precision=1、零 stray-hit、零 harm。
- **Target Users**: 引擎评测、题库修复与后续能力扩展。

## Goals
- **G1**: 恢复 pdf-075 的 `options` 为原书真选项，`answer` 保持 `B`，`answerKeywords` 同步。
- **G2**: 生产环境（`LlmAvailable=False`）符号引擎对 pdf-075 返回 `B`，与存键一致。
- **G3**: 新增通用、保守的氧还陈述判定原语：氧还反应计数（FR-2）、角色/比值（FR-3），零假阳性。
- **G4**: 全库 757 题回归：相对基线 `_evalscratch/snapshot_after_datarepair_engine_batch3.json`，`LOST=[]`、`harmCount=0`、`enginePrecision=1`；`GAINED ⊇ {pdf-075}`，并尽量纳入扫描发现的其它自洽氧还选择题。
- **G5**: 既有已正确题的 `gatewayAnswer` 字节级不变；batch1/2/3 已解题（pdf-100/406/405/197 等）不回归。

## Non-Goals
- 不修改任何题的 `answer` / `note` / `question` / `type`（唯一例外：恢复 pdf-075 的 `options` 文本，已获用户批准；`answer` 不动）。
- 不碰已正确题。
- 不重跑 `build_final.py`。
- 不实现/不依赖 LLM 路径（纯符号引擎）。
- 不把不可安全判定的陈述硬判为真/假（`Undecided` 优先于任何猜测）。
- 不改变既有单选瀑布链的判定顺序与已解题行为（新增路径为加性末端 fallback，不抢跑）。

## Background & Context

### 关键发现：pdf-075 存储选项错乱（与原书不符），但答案键 B 正确
用 Qwen-VL 读原书渲染页 `pdfs/_extract/page_renders/page_021.png` / `page_022.png`，原题（2025·黑龙江省联考，第 4 题）的真实选项为：

| 选项 | 原书原文 | 化学真值 |
|---|---|---|
| A | OH⁻的还原性比 H₂O 强 | 真 |
| B | 反应Ⅳ每生成 1 mol O₂，转移 4 mol 电子 | **假**（实际 6 mol） |
| C | 反应ⅰ～ⅳ中有 3 个氧化还原反应 | 真（ⅲ 非氧还，ⅰ/ⅱ/ⅳ 是） |
| D | 较 He、Ne、Ar、Kr 原子，Xe 原子半径大，电离能小，所以含 Xe 的化合物种类更多 | 真 |

`src/.../data/pdf_questions.json` 当前存的却是**另一套变体选项**（"反应ⅰ中XeF2既是氧化剂又是还原剂" / "反应ⅲ中XeF6只作氧化剂" / "反应ⅳ中Xe元素被氧化和被还原的物质的量之比为1:1"）。该套选项经化学推导**两假（A、C）不自洽**，且全网检索无来源，系 OCR/拼接错乱。

化学真值推导（4 个反应式与原书一致，已对照 `exercise/page_021.json` 与 VL 原件确认）：
- ⅰ `2XeF2 + 2H2O = 2Xe + O2 + 4HF`：Xe +2→0 还原，O -2→0 氧化 → 氧还。
- ⅱ `2XeF2 + 4OH⁻ = 2Xe + O2 + 4F⁻ + 2H2O`：Xe +2→0 还原，O -2→0 氧化 → 氧还；每 mol O₂ 转移 4 mol e⁻。
- ⅲ `XeF6 + 3H2O = XeO3 + 6HF`：Xe +6→+6 无变价 → **非氧还**。
- ⅳ `2XeF6 + 4Na⁺ + 16OH⁻ = Na4XeO6 + Xe + O2 + 12F⁻ + 8H2O`：Xe +6→+8（氧化）与 +6→0（还原），O -2→0 → 氧还；每 mol O₂ 转移 6 mol e⁻。

故 B 是唯一假命题 → 答案 `B`，与存键一致。**键无需改，只需恢复选项。**

### 现有引擎能力（已核）
- `RedoxElectronStatementJudge`（`IStatementJudge`）已能判电子转移计数陈述；`LabelRefRe` 覆盖罗马数字 Ⅳ（U+2160-2169）与题干 ⅰ-ⅳ（U+2170-2179），`NormalizeText` 把 O₂→O2、⁺/⁻→^ 。预期可判真实选项 B 为 False（需在探针中验证 `RedoxBalancer` 对含 `Na4XeO6` / `Na⁺` 的方程 Ⅳ 能正确解析配平）。
- `OxidationStateAnalyzer`（Core）对 Xe 走「唯一未知元素代数和兜底」：XeF₂→+2、XeF₆→+6、XeO₃→+6、Na₄XeO₆→+8、Xe→0 均正确 → 引擎具备处理 Xe 化学的基础。
- `CompositeStatementJudge` 聚合 `IStatementJudge`：冲突→`Undecided`、全不命中→`Undecided`。已注册：`FactBaseStatementJudge`、`CountingStatementJudge`、`RedoxElectronStatementJudge`、`ElectronConfigStatementJudge`、`VseprBondAngleStatementJudge`、`CrystalTypeStatementJudge`、`CoordinateBondStatementJudge`。
- 选择瀑布链（`LlmGatewayService.CallChoice`）：`CountingChoiceValidator` → `StoichiometryChoiceValidator` → `MultiSelectChoiceValidator`（需≥2候选）→ `ChoiceVerifier` 兜底。**当前缺一条「全选项判出 + 候选唯一」的单选陈述路径**：pdf-075 若 4 项全判出（A=T,B=F,C=T,D=T），`MultiSelect` 因候选仅 1 个返回 null，最终仍 null。
- `CountingChoiceValidator.ChooseUnique` 已有「候选唯一→返回字母」逻辑，可复用。

### 登记册先例
`data-defect-register.md` **D 类**："answer 键经核对正确，但 options 文本与官方题面不符 → 留痕 + 从原页恢复选项文本"。pdf-012/065 已回填题干文本；pdf-291/437 有「选项补全」WIP。恢复 pdf-075 选项符合 D 类处置。用户已明确批准触碰 pdf-075 的 `options`。

## Functional Requirements
- **FR-1（数据恢复）**: 将 pdf-075 的 `options` 替换为原书真选项（A/B/C/D 见上表，以 VL 原件为准，规范化为可解析形式）；`answer` 保持 `B`；`answerKeywords` 同步为 `["B", "反应Ⅳ中每生成1 mol O₂转移4 mol电子"]`（或等价可解析文本）。在 `data-defect-register.md` 登记为 D 类并标记已恢复。
- **FR-2（氧还反应计数判定）**: 新增 `RedoxCountStatementJudge : IStatementJudge`，判定 "反应ⅰ～ⅳ中有 N 个氧化还原反应" 类陈述：解析标签范围（ⅰ～ⅳ / ①～④ / (1)~(4) 等），逐个方程经 `OxidationStateAnalyzer` 判断是否有氧还（任一元素氧化态变化），统计真值计数，与声称 N 比对；任一方程不可解析/不可判或标签范围歧义 → `Undecided`。
- **FR-3（角色/比值判定，通用能力）**: 新增 `RedoxRoleRatioStatementJudge : IStatementJudge`，判定：① 角色陈述（"X 是氧化剂/还原剂/既是氧化剂又是还原剂/只作氧化剂/氧化产物/还原产物"）；② 比值陈述（"被氧化与被还原的物质的量之比为 a:b"，含歧化）。复用 `RedoxBalancer.SplitHalfReactions` 的氧化态变化与半反应信息；解析标签方程；物种/系数歧义 → `Undecided`。
- **FR-4（单选择陈述路径）**: 新增（或扩展）一条单选择校验路径：逐选项经 `CompositeStatementJudge` 判定；**全部**选项非 `Undecided` 且满足问法方向的候选**唯一** → 返回该字母；候选 ≥2 → `null`（交由 `MultiSelectChoiceValidator`）；任一选项 `Undecided` → `null`。接在 `StoichiometryChoiceValidator` 之后、`MultiSelectChoiceValidator` 之前。加性、不抢跑。
- **FR-5（注册同口径）**: 把 FR-2/FR-3 新 judge 注册进三处 `CompositeStatementJudge`：`MultiSelectChoiceValidator.Judge`、FR-4 新单选路径、`CountingChoiceValidator.SubItemJudge`，保持判分口径一致。
- **FR-6（pdf-075 的 A/D 判定）**: 真实选项 A（OH⁻还原性>H₂O）与 D（Xe 半径/电离能→化合物多）需判为 True。优先用保守的引擎推导（如还原性比较可由 ⅲ 非氧还 vs ⅳ 氧还推导；周期律趋势可由周期数据推导）；若某条无法在零假阳性预算内安全推导，则用带 `stemMatch` 锚定的窄域事实（参照 `choice-curated-facts.json` 先例）兜底。无论采用哪种机制，都必须经全库 blast-radius 探针验证零 stray-hit。
- **FR-7（全库扫描找额外收益）**: 实现后用 blast-radius 探针对全 757 题回归，凡新能力能保守判对的自洽氧还选择题，纳入 `GAINED`（仍须零 stray-hit、零 harm、零 LOST）。

## Non-Functional Requirements
- **NFR-1（精度红线）**: `enginePrecision=1`；任何新增 judge 在域外/歧义输入上返回 `Undecided`，不得产生假阳性。
- **NFR-2（零伤害）**: 相对基线 `harmCount=0`、`LOST=[]`；此前正确的 `gatewayAnswer` 字节级不变。
- **NFR-3（零回归）**: batch1/2/3 已解题与全部 `gatewayOnlyCorrect` 题行为不变。
- **NFR-4（可测）**: 每个新 judge 有正例、反例、歧义（→Undecided）单测；pdf-075 端到端有单测/护栏真值登记。

## Constraints
- **Technical**: 依赖 `EquationParser` / `EquationBalancer` / `RedoxBalancer` / `OxidationStateAnalyzer` 正确处理 Xe 方程（含 `Na4XeO6`、`Na⁺`、`↑↓` 等）。T2 探针验证；若解析/配平失败，修复 Core 解析器（保持其对既有方程的行为字节级不变）。
- **Business**: 唯一数据改动为 pdf-075 的 `options`（已批）；不碰其它数据文件；不重跑 `build_final.py`。
- **Dependencies**: 基线快照 `_evalscratch/snapshot_after_datarepair_engine_batch3.json`（gwCorrect=708, bothWrong=49）；eval 脚本 `_evalscratch/run_eval_resume.ps1`（用 `& "全路径"` 调用，`-Env Production`）。
- **Process**: Spec Mode — 本 spec + tasks 经用户批准后方可实现；实现后须独立评审（`review.md`）。

## Assumptions
- 原书 page_021/022 为 pdf-075 题面的权威来源（VL 已读），其选项与答案 B 为真值。
- 生产环境 `LlmAvailable=False`，引擎行为确定、可复现。
- `ChoiceVerifier.ParseOptions` 能正确切出 pdf-075 的 4 个选项（恢复选项后）。

## Acceptance Criteria

### AC-1: pdf-075 选项恢复正确
- **Type**: `rule`
- **Given**: pdf-075 当前存储错乱选项
- **When**: 应用 FR-1 恢复
- **Then**: pdf-075 的 `options` 与原书 page_022 的 A/B/C/D 一致（规范化后），`answer=="B"`，`answerKeywords` 含 B 与真实 B 选项文本；`question`/`note`/`type` 未变
- **Pass Condition**: `python` 读取 `pdf_questions.json` 中 pdf-075，4 个选项文本与原书一致，`answer=="B"`；`git diff` 显示仅 pdf-075 的 options（及 answerKeywords）变动
- **Evidence**: VL 转录 page_021/022 + diff 输出

### AC-2: 引擎端到端解出 pdf-075 → B
- **Type**: `rule`
- **Given**: 恢复选项后的 pdf-075
- **When**: 生产环境（`LlmAvailable=False`）符号引擎求解
- **Then**: 引擎返回 `B`，与存键一致（A=True, B=False, C=True, D=True → 选错误项得 B）
- **Pass Condition**: 单测/探针调用选择链对 pdf-075 返回 `B`；eval 快照中 pdf-075 的 `gatewayAnswer=="B"` 且 `gatewayOk==true`
- **Evidence**: 探针输出 + eval 快照记录

### AC-3: 氧还计数/角色比值 judge 保守正确
- **Type**: `rule`
- **Given**: 新增 `RedoxCountStatementJudge`、`RedoxRoleRatioStatementJudge`
- **When**: 输入正例 / 反例 / 歧义或域外陈述
- **Then**: 正例判 True、反例判 False；歧义（标签范围不明、方程不可解析、物种/系数歧义、非氧还方程问角色比值）判 `Undecided`
- **Pass Condition**: 单测覆盖：(a) pdf-075-C 类计数正例（3 个→True）与错例（声称 2→False）；(b) 歧化比值正例/反例；(c) "X 只作氧化剂" 角色正例/反例；(d) 域外/歧义输入返回 Undecided；全部通过
- **Evidence**: xUnit 测试通过记录

### AC-4: 单选择陈述路径行为正确
- **Type**: `rule`
- **Given**: FR-4 新路径接入瀑布链
- **When**: 选择题逐选项经 `CompositeStatementJudge` 判定
- **Then**: 全部非 Undecided 且候选唯一 → 返回该字母；候选 ≥2 → `null`（交 MultiSelect）；任一 Undecided → `null`；不抢跑在既有单选校验器之前
- **Pass Condition**: 单测：候选唯一返回字母；候选 2 返回 null；含 Undecided 返回 null；既有已解题经全库回归无变化
- **Evidence**: xUnit 测试 + 全库回归 diff

### AC-5: 全库回归零伤害、零 LOST、精度 1
- **Type**: `rule`
- **Given**: 实现完成，基线 `snapshot_after_datarepair_engine_batch3.json`
- **When**: 跑生产 eval 并与基线 diff
- **Then**: `LOST=[]`、`harmCount=0`、`enginePrecision=1`；`GAINED ⊇ {pdf-075}`；所有 `gatewayOnlyCorrect` 题的 `gatewayAnswer` 字节级不变；batch1/2/3 已解题（pdf-100/406/405/197 等）仍正确
- **Pass Condition**: diff 脚本输出 `LOST=[]`、`harmCount=0`、`enginePrecision=1`、`GAINED` 含 pdf-075；之前正确的 `gatewayAnswer` 变化数 = 0
- **Evidence**: eval 快照 + diff 脚本输出

### AC-6: 护栏测试通过且无残留 WIP
- **Type**: `rule`
- **Given**: 新增/更新的 xUnit 测试
- **When**: `dotnet build` + 跑测试（临时排除 `SolveStructuredEnricherTests.cs` 既有编译错误后还原）
- **Then**: 0 失败；无 `*.wipbak` 残留；无 testhost 进程锁 DLL
- **Pass Condition**: 测试退出码 0，无失败；工作区无 `*.wipbak`
- **Evidence**: 测试命令输出

### AC-U1: 保守性与可维护性
- **Type**: `rubric`
- **Dimension**: 新增判定逻辑的保守性与代码清晰度
- **Scale**: 1-5
- **Anchors**: 1 = 新增 judge 在域外有假阳性或与既有路径冲突；3 = 功能可用但保守边界模糊/缺少 Undocumented 回退；5 = 每个 judge 的 `Undecided` 边界清晰、注释说明、与既有 `IStatementJudge` 模式一致、零抢跑
- **Pass Threshold**: >= 4
- **Evidence**: 代码审查 + 单测覆盖

## Open Questions
- [ ] A（OH⁻还原性>H₂O）与 D（周期律）最终采用引擎推导还是 `stemMatch` 锚定事实：实现期由全库 blast-radius 探针的零 stray-hit 结果决定（两者皆可，只要满足 AC-5）。
- [ ] 全库扫描是否发现除 pdf-075 外可解锁的自洽氧还选择题：T7/T8 回归时确认。
