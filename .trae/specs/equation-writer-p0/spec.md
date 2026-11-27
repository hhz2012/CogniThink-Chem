# 方程引擎增强 P0：equation_writer 错题零假阳性恢复 — 产品需求文档（PRD）

## Overview
- **Summary**：在 `precision=1` / `harm=0` / 零回归红线下，针对全量 757 题中当前 9 道 `equation_writer` 未解且**引擎返回 `null`**（未产出任何答案）的题，增强方程 KB 检索书写器：为反应知识库条目新增**窄化锚点 `questionCues`** 字段，并在命中裁决中引入**带锚点条目的优先级**，破解富文本工艺题「多物种共存 → 多条 KB 同时命中 → 因 `matches.Count != 1` 而归 null」的阻塞；同时为库中缺失的目标反应补录 KB 条目。每一条改动都经**全库 757 探针**验证「凡命中必判正」，域外保持 `null`。
- **Purpose**：在不牺牲正确性的前提下恢复可唯一判定的方程书写题；明确区分「引擎能力缺口（可安全恢复）」与「数据缺陷 / 答案键歧义 / 域外（诚实留 null）」，延续项目记忆 line 53 的 P0 方向（方程引擎 KB 检索书写器 + 规范模板，窄门控保证 precision=1）。
- **Target Users**：高中化学题求解引擎（CogniThink TutorBrain）的维护者与使用者。

## Goals
- 为 `ReactionEntry` 增加可空 `questionCues[]` 窄化锚点字段（默认空 → 既有条目行为零变化），锚点是题干必须出现的子串（如目标产物名 / 特征试剂）。
- 在 `ReactionKbWriterSolver` 的分子 / 离子两条检索路径中：条目命中需「反应物全出现 ∧ 条件满足 ∧ 全部 cues 出现」；当存在至少一条带 cues 的命中时，**仅在带 cues 的命中中裁决唯一性**（带锚点条目优先，打破富文本多命中阻塞）；无带 cues 命中时回退到既有「全库唯一命中」行为（零回归）。
- 为 9 道目标题中库内缺失的反应补录 `data/knowledge/reactions/*.json` 条目（分子 / 离子），并配以使其唯一裁决的 cues。
- 探针驱动：每补一条目 / 改一处裁决，跑全库 757 探针，断言 `fired ⇒ IsAnswerCorrect=true`（0 假阳性）；只保留通过探针的目标题，未通过的诚实留 `null` 并记录排除理由。
- 全量测试始终 0 failed；全库 eval 相对基线 `t2route`：仅目标题翻 False→True（GAIN），0 回归（True→False），0 harm，`mp27` 27/27 不变。

## Non-Goals
- **不**全局打开 `ReactionKbWriterSolver.MultiHitJoinRelax`（探针实证：开启后全库出现 4 例假阳性 pdf-167/244/418/456，不可接受）。
- **不**为非 cue、非目标的既有条目改变命中行为（LOSS=0 的构造保证）。
- **不**改动 `pdf_questions.json` 的 `answer` / `answerKeywords`（数据缺陷与键歧义不在本轮修正；pdf-084 答案为 `'C'` 属数据缺陷，留 null）。
- **不**修复 pdf-166 子问 2 那种答案键带「[或…]」后缀、引擎输出无法作为子串命中的题（除非探针证明其**另一**干净子问方程可命中；可命中则覆盖该子问，不可命中则留 null）。
- **不**新增通用产物推断 / 配平器（产物不唯一时强行推断必假阳性）。
- **不**在逻辑层硬编码化学真值（真值只存 JSON；cue 是「题干锚点」不是化学事实）。
- **不**主动 commit/push（改完留工作区，待用户指示）。

## Background & Context
- **基线（已确认）**：checkpoint `t2route`：total=757，gatewayCorrect=673，precision=1，harmCount=0，bothWrong=84，mp27=27/27。前序 commit `de7c025` 已 push，工作区干净。本 spec 的所有 diff 均以 `t2route` 为对照。
- **9 道错题特征（实证，`_evalscratch/dump_wrong_eq.py`）**：全部 `gwUsedEngine=False`、`gwAnswer=''`、`conclState=Insufficient` —— 即**方程链对它们全部返回 null**，不是产出了错误答案。因此新增正确产出只会令其翻 True（GAIN），不存在「覆盖掉已有正确结论」的风险；但新增产出若在**别的题**上误触发并判错，即为假阳性（harm），是红线。
- **判分机制（已核实，`GatewayVsLlmEvaluationService`）**：非选择题 `AnswerKeywords` 由 `SplitPdfAnswerKeywords` 把 `answer` 按 `,，;；、` / 换行 / 双空格拆成多个碎片关键词，`IsAnswerCorrect` 走 OR 子串命中（任一碎片命中即判正）；两侧经 `NormalizeChemicalNotation` → `NormalizeConnectorsAndLatex` 对称归一（连接符坍缩为 `=`、去 ↑↓△、展开 LaTeX 上下标 `^{3+}`→`3+`、`\rightleftharpoons`→`=`、去空格）。故引擎产出一条化学正确的方程式，只要其归一化形式包含某碎片关键词即可判正。
- **方程链（已核实，`LlmGatewayService.CallCompile` L788-829）**：`BondEnergySolver → ProductWriterSolver → KnownEquationWriterSolver → ReactionKbWriterSolver`；命中后置 `reaction_check` 走采纳门控（`HasReactionCheckTruthSignature`：结论含「离子方程式：/化学方程式：」前缀，采纳前必过 `IsAnswerCorrect` → precision=1）。
- **阻塞根因（实证）**：`ReactionKbWriterSolver.KbLookupMolecular/Ionic` 要求「反应物全部出现在题干 ∧ 恰好 1 条命中」。富文本工艺题（如 pdf-010 含 Fe2O3/盐酸/FeCl3/NaOH/Fe(OH)3…）导致多条 KB 同时命中 → `matches.Count != 1` → null。探针 `Probe_SolverDirect_AllCases`（EQ_PROBE=1）：当前全库命中 8 题、全部判正、0 假阳性，9 目标全部 null；`Probe_MultiHitRelax_RiskScan` 打开 `MultiHitJoinRelax` 后命中 14 题但 4 例假阳性 → 全局放宽不可行，需**窄化**而非**放宽**。
- **9 目标逐题初判（待 Task 1 探针锁定）**：
  - **可寻址候选**（答案含干净方程碎片 / 反应物组合独特，补条目 + cues 后有望唯一裁决并判正）：pdf-010（Fe2O3+6HCl，库已有 RX-IN-FE-010 但被多命中阻塞）、pdf-097（H3PO4+3LiOH→Li3PO4，分子，缺条目）、pdf-156（2NH4Cl+Ca(OH)2→，缺条目）、pdf-164（BaCl2+TiCl4+2(NH4)2C2O4+H2O→BaTiO(C2O4)2，单条干净关键词，缺条目，反应物独特）、pdf-166 子问 4（Zn2(OH)2CO3+Fe3++H+→，第二条干净碎片，缺条目）、pdf-462（Al3++2H2O⇌AlOOH↓+3H+，离子，LaTeX 归一化可对齐，缺条目）、pdf-484 子问 3（2Co2++MnO2+4H+→ 与 2Co3++2Cl-→，离子，缺条目）、pdf-532 子问 2（2I-+2MnO4-+8H+→，离子，缺条目）。
  - **不寻址**：pdf-084（答案 `'C'`，taskType 误分类为 equation_writer 的数据缺陷，无方程关键词）。
- **既有 spec 继承**：`image-equation-engine-151`（落地了 `ReactionKbWriterSolver` 与唯一命中契约，本 spec 是其窄化增强，不破其 AC-2 唯一命中契约——带 cues 时仍要求**带 cues 命中中**唯一）、`a_class_reaction_check_fix_plan`（采纳门「化学方程式/离子方程式」签名）。

## Functional Requirements
- **FR-1（探针锁定可恢复集，先行）**：新增一个 `EQ_PROBE=1` 门控的只读探针（不影响常规 `dotnet test` 绿态），对 9 目标逐题输出：当前匹配的全部 KB 条目 id 列表（解释为何被多命中阻塞或为何无匹配）、候选 cue / 新条目加入后的命中裁决结果、产出是否 `IsAnswerCorrect`；并对全库 757 输出「命中但判错」清单（必须为空）。据此锁定最终可恢复集与每题所需 cue。
- **FR-2（`questionCues` 字段，加性零回归）**：在 `ReactionEntry`（`CogniThink.Chem.Core.Reactions`）增加 `List<string> QuestionCues`（JSON 名 `questionCues`，默认空）；`ReactionLibrary` 载入时读取。空 cues 的条目与当前行为完全一致。
- **FR-3（带锚点优先的唯一命中裁决）**：在 `KbLookupMolecular` 与 `KbLookupIonic` 中，条目匹配条件改为「反应物全出现 ∧ 条件满足 ∧（cues 为空 ∨ 全部 cues 出现于题干）」；若带 cues 的命中非空，则只在带 cues 命中中裁决（恰好 1 条才产出）；若带 cues 命中为空，则回退到既有「全体命中中恰好 1 条」逻辑。域外 / 歧义一律 null。
- **FR-4（补录目标 KB 条目 + cues）**：为 FR-1 锁定的可恢复目标，在 `data/knowledge/reactions/*.json` 补录配平正确的分子 / 离子条目，配以使其在目标题干唯一裁决、且不在其他题误触发的 cues；每补一条即跑 FR-1 探针确认 `fired⇒判正` 且全库 0 假阳性，不通过则收紧 cue 或撤回该条目。
- **FR-5（采纳门与判分兼容）**：产出结论沿用 `化学方程式：/离子方程式：` 前缀与 `ExpandMatchVariants`（去空格 / 逆序 / 去 `^` 变体），确保经归一化后可命中答案碎片；不改变采纳门与判分逻辑。
- **FR-6（排除留痕）**：对探针未通过、数据缺陷、答案键歧义的目标题，引擎保持 null；在 `_evalscratch/` 记录排除理由（题 id、原因）。
- **FR-7（回归与覆盖报告）**：实现收尾后全量 eval 新 tag，与 `t2route` diff，确认 GAIN / 0 LOSS / 0 harm / mp27 27/27。

## Non-Functional Requirements
- **NFR-1（零假阳性 = precision=1）**：实现后，全库 757 中方程链每一次非 null 产出都必须 `IsAnswerCorrect=true`；探针与 eval 双侧 0 假阳性。
- **NFR-2（零回归 = LOSS=0）**：相对 `t2route`，无任何题从 gatewayCorrect True 翻 False；带 cues 新条目对 cues 未出现的题行为恒等于基线（构造保证）。
- **NFR-3（不蒙数）**：无法唯一裁决（带 cues 命中 0 条或 >1 条）一律 null。
- **NFR-4（事实/逻辑分离）**：化学真值（方程式、产物）只存 `reactions/*.json`；`questionCues` 是题干文本锚点，非化学事实；逻辑层不新增化学事实常量。
- **NFR-5（可追溯）**：每条新条目 / 每次裁决改动都有 FR-1 探针输出落盘 `_evalscratch/` 为证。

## Constraints
- **Technical**：Windows PowerShell 5（无 `&&`/`||`，用 `;` 或 `if ($LASTEXITCODE -eq 0)`）；.NET 8；改 `data/*.json` 后必须 rebuild（数据复制到 bin）；stale-DLL 仪式：杀 testhost → `dotnet build-server shutdown` → 清 bin/obj → 重建；eval 驱动 `powershell -ExecutionPolicy Bypass -File _evalscratch\run_eval_resume.ps1 -Tag <tag> -Env Production`；diff `diff_eval.ps1 -OldTag t2route -NewTag <new>`；mp27 `python _evalscratch\verify_mp27.py t2route <new>`。
- **Business（红线，冻结）**：precision=1 / harm=0 / LOSS=0 / mp27=27/27 不退化；不臆造真值；不动 `images/` 原始扫描图；不主动 commit/push。
- **Dependencies**：`ReactionLibrary` / `ReactionEntry`（Chem.Core）、`ReactionKbWriterSolver`（Api）、`data/knowledge/reactions/*.json`、`GatewayVsLlmEvaluationService.IsAnswerCorrect` + 采纳门、基线 `_evalscratch/snapshot_t2route.json`。

## Assumptions
- `pdf_questions.json` 的 `answer` 字段为判分真值；`SplitPdfAnswerKeywords` 的碎片拆分是既定判分契约（本 spec 不改判分）。
- `reactions/*.json` 既有条目已配平且产物正确；若目标反应已有条目（如 RX-IN-FE-010），优先复用并加 cue，不重复建条目。
- `NormalizeConnectorsAndLatex` 已能把 Unicode / LaTeX 方程与答案碎片归一到可子串比较（实证 pdf-462 LaTeX 关键词可与 Unicode 产出对齐）；探针不通过的题不纳入。
- 9 目标的真实方程以银行 `note` / 官方解析与化学真值三者收敛为准；不臆造。

## Acceptance Criteria

### AC-1: 基线锁定与零回归
- **Type**: `rule`
- **Given**: 工作区基于 `t2route`（commit `de7c025`），实现前确认 `dotnet test` 全绿 + 探针显示当前 8 命中 / 0 假阳性
- **When**: 完成全部实现并 clean rebuild 后跑全量测试 + 全库 eval（新 tag，如 `t2eq`），与 `t2route` diff
- **Then**: `dotnet test CogniThink.Chem.sln` failed=0；eval `precision=1`、`harmCount=0`；diff 中 LOSS=0（无 True→False）；`verify_mp27 t2route t2eq` = 27/27
- **Pass Condition**: 测试退出码 0 且 failed=0；diff 报告 LOSS=0 / harm=0；mp27 27/27
- **Evidence**: 测试输出 + `_evalscratch/checkpoint_t2eq_summary.txt` + diff 报告 + `verify_mp27` 输出

### AC-2: 带锚点优先的唯一命中契约（不破既有契约）
- **Type**: `rule`
- **Given**: `ReactionEntry.QuestionCues` 已加、`KbLookupMolecular/Ionic` 已实现 cue 过滤与优先裁决
- **When**: 任意题干走方程检索
- **Then**: 带 cues 命中非空时，仅在带 cues 命中中恰好 1 条才产出（>1 或 0 → null）；带 cues 命中为空时，回退到既有「全库唯一命中」逻辑；空 cues 条目行为与基线完全一致
- **Pass Condition**: 代码审查确认裁决逻辑；守卫测试覆盖：cues 未全出现 → 该条目不匹配；带 cues 多命中 → null；无带 cues 命中时既有唯一命中不变
- **Evidence**: 源码 + 新增守卫单测

### AC-3: 全库零假阳性探针（每步强制）
- **Type**: `rule`
- **Given**: FR-1 的 EQ_PROBE 探针对全库 757 运行
- **When**: 任何 KB 条目 / cue / 裁决改动之后
- **Then**: 探针输出的「fired 但 IsAnswerCorrect=False」清单为空（0 假阳性）；当前已判正的 8 题仍判正（无回归）
- **Pass Condition**: 探针汇总 `newlyFiredWrong=0` 且既有 8 题 `isCorrect=True`
- **Evidence**: `_evalscratch/` 探针 TSV + 控制台汇总

### AC-4: 目标题恢复且仅恢复正确解
- **Type**: `rule`
- **Given**: FR-1 锁定的可恢复目标集
- **When**: 最终 eval 相对 `t2route` diff
- **Then**: 至少 3 道目标题（从 pdf-010/097/156/164/166/462/484/532 中）翻 False→True，且其引擎产出经归一化命中答案碎片；pdf-084 保持 False（不臆造）；未纳入目标保持 null
- **Pass Condition**: diff 报告 GAIN 列表含 ≥3 个目标 id 且均判正；pdf-084 不在 GAIN；无目标题出现 adopted-but-wrong
- **Evidence**: diff 报告 + 探针 TSV 中目标行 `isCorrect=True`

### AC-5: 域外 null 与任务类型门控不变
- **Type**: `rule`
- **Given**: 实现后的方程链
- **When**: 对 757 中任一非「明确问方程式」题、或 taskType 不在方程链允许集的题
- **Then**: `ReactionKbWriterSolver` 返回 null（`text` 不含「方程式」/ taskType 不在 `{concept,unknown,calculation,equilibrium,reaction_check,equation_writer}` → null 的既有门控保留）
- **Pass Condition**: 探针显示方程链非 null 产出的题集合，是基线命中集 ∪ 目标恢复集的子集，无域外新增
- **Evidence**: 探针 TSV 命中 id 清单

### AC-6: 逻辑层不硬编码化学事实
- **Type**: `rule`
- **Given**: 本轮所有源码变更
- **When**: 审查 `ReactionKbWriterSolver` / `ReactionEntry` 改动
- **Then**: 无新增硬编码化学方程式 / 产物 / 数值真值常量；新方程真值全部位于 `reactions/*.json`；`questionCues` 仅为题干文本子串
- **Pass Condition**: Grep 确认逻辑层无化学事实常量；新增真值条目均可在 JSON 中定位
- **Evidence**: 审查记录 + JSON 条目清单

### AC-7: 覆盖净增且全为正确解
- **Type**: `rubric`
- **Dimension**: equation_writer 正确求解净增量（仅计零假阳性的正确解，且 0 回归）
- **Scale**: 1-5
- **Anchors**: 1 = 未恢复任何目标题，或引入假阳性 / 回归；3 = 恢复 ≥3 目标题且 precision=1 / harm=0 / LOSS=0 / mp27 27/27；4 = 恢复 ≥5 目标题，全为正确解，排除理由留痕完整；5 = 恢复全部 8 道可寻址目标（pdf-084 除外），探针证据完整，无任何副作用
- **Pass Threshold**: >= 3
- **Evidence**: eval 修复前后 summary 对比 + diff 报告 + 探针 TSV

### AC-8: 不可恢复题排除留痕
- **Type**: `rule`
- **Given**: 探针未通过 / 数据缺陷 / 答案键歧义的目标题
- **When**: 实现收尾
- **Then**: `_evalscratch/` 有排除记录，列出每题 id 与原因（如 pdf-084 数据缺陷、某题多命中无法唯一裁决、某题答案键带 [或] 后缀无法子串命中）；引擎对这些题保持 null
- **Pass Condition**: 排除记录文件存在且覆盖所有未恢复目标题；探针确认这些题 `fired=False`
- **Evidence**: `_evalscratch/` 排除记录 + 探针 TSV

## Open Questions
- [ ] 带 cues 优先裁决是否会在某些非目标题上因 cues 意外出现而触发？（由 AC-3 全库探针兜底；出现即收紧 cue 或撤回条目）
- [ ] pdf-484 / pdf-532 的离子方程是否需要 `askIonic` 路径（题干是否含「离子方程式」）？若题干只问「化学方程式」则走分子路径，产物离子式可能不匹配——由 FR-1 探针逐题确认问法与产出对齐。
- [ ] 是否需要为 `questionCues` 增加「cues 之间为 AND、cue 内支持多候选（任一出现）」的表达？（首版用 AND 子串；若某题需 OR 再扩，不影响零回归）
