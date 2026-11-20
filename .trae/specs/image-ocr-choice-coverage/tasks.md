# 图像/OCR 未解选择题覆盖 — 实现任务队列

> 规格文档：[spec.md](./spec.md)
> 红线（冻结）：零假阳性 / 零 harm / 零回归；无唯一解返回 `null` 绝不蒙数；化学事实只存 JSON；计数题不进本任务。
> 评估权威工具：`CogniThink.TutorBrain.EvalRunner`（`dotnet run --project src/CogniThink.TutorBrain.EvalRunner -- --tag <tag>`）。
> 当前基线（HEAD）：`engineSolved=446 / coverage=0.5892 / precision=1 / harmCount=0 / bothWrong=311`（`_evalscratch/checkpoint_baseline_now_*`）。

## 安全护栏（每个任务完成后必过）
1. 杀 testhost → `dotnet build-server shutdown` → 清 bin/obj → 重建，避免 stale dll。
2. 全量测试（TutorBrain / Chem / Batch3）0 failed。
3. `EvalRunner` 新 tag 重跑 757 题：`precision=1`、`harmCount=0`（零假阳性）。
4. 逻辑层不硬编码化学事实（grep 审查）。

## AC → 任务 覆盖矩阵
| AC | 覆盖任务 |
|----|----------|
| AC-0 C# OCR 工具可构建可复跑 | Task 2 |
| AC-1 全量测试零回归 | Task 1（基线）、Task 7（终验）+ 各任务护栏 |
| AC-2 零假阳性 | Task 3/4/5/6 + Task 7（EvalRunner precision=1/harm=0） |
| AC-3 不蒙数 | Task 4/5（guard 测试） |
| AC-4 化学事实不入逻辑层 | Task 4/5（审查） |
| AC-5 截断选项数据已修复 | Task 3 |
| AC-6 覆盖提升（全为正确解） | Task 3/4/5/6 + Task 7 报告 |
| AC-7 无临时探针残留 | Task 1（确认）、Task 7（复查） |

---

## Task 1 — 基线锁定与清理守卫
- **Status**: done
- **Evidence**: `_evalscratch/task1_baseline.txt`：TutorBrain 937/0、Chem 905/0、`tests/` 下 `_Tmp*.cs`=0；EvalRunner tag=baseline_now → engineSolved=446 coverage=0.5892 precision=1 harmCount=0 bothWrong=311。
- **Depends on**: 无
- **Priority**: high
- **Description**: 在当前 HEAD 上复现并锁定绿色基线：依次跑 TutorBrain、Chem、Batch3 护栏测试，记录 passed/total/failed；确认 `tests/` 下无 `_Tmp*.cs`（Glob 已确认，但收尾前复查）。把三套测试的计数写入本任务证据，作为后续“零回归”的比较基准。
- **AC 覆盖**: AC-1, AC-7
- **Test Requirements**:
  - `rule`: 三套测试退出码均为 0，failed=0。
  - `rule`: `Get-ChildItem tests -Filter _Tmp*.cs -Recurse` 输出为空。

## Task 2 — 新建独立 C# OCR 工具项目（MVP）
- **Status**: done
- **Evidence**: 新建 `src/CogniThink.TutorBrain.PdfOcrRepair`（已加入 sln；仅依赖 Docnet.Core 2.6.0，自带 win-x64 pdfium.dll；OCR 走 `tesseract.exe -l chi_sim` 子进程，不引入 .NET OCR 封装包，引用闭包收敛）。`dotnet build` 工具项目 0 错误 0 警告，全 sln 0 错误；`dotnet run -- pdf-381` 离线跑通（无网络/无 Python），`_evalscratch/ocr/pdf-381.txt` 恢复出 B/C/D 可辨文本；`CogniThink.TutorBrain.Api.csproj` 仅引用 Chem.Core、无对该工具的引用。页面定位优先用 `pdfs/questions_parsed.json` 的 `src_page`（已入库可复现），渲染/OCR 均带缓存；`--scan` 模式可全量重建页清单。
- **Depends on**: Task 1
- **Priority**: high
- **Description**: 新建 console 项目 `src/CogniThink.TutorBrain.PdfOcrRepair`（加入 `CogniThink.Chem.sln`），参照 `EvalRunner` 的 `FindRepoRoot`/参数解析/落盘模式。功能：输入 pdf 题 id → 从 `pdf_questions.json` 取 `images`（`pdf_images/pMMM_nXX`）定位页；优先 OCR 仓库已有渲染图，缺页则用 C# PDF 渲染（Docnet.Core / PdfiumViewer 等离线 NuGet）渲染；用 C# OCR 引擎（先二选一验证：Windows.Media.Ocr 或 Tesseract .NET 封装）识别；输出整页 OCR 文本到 `_evalscratch/ocr/<id>.txt`，并打印“缺失选项建议文本”。**不得被 `CogniThink.TutorBrain.Api` 引用**。以 pdf-381 为验收样例：须恢复出选项 B/C/D 的文本。注意引用闭包一次性收敛（csproj/引用/源码三者一致），避免 OCR 依赖混乱。
- **AC 覆盖**: AC-0, AC-7（不引入临时探针）
- **Test Requirements**:
  - `rule`: `dotnet build` 工具项目 0 错误；`dotnet run -- pdf-381` 离线完成（不联网、不依赖 Python）。
  - `rule`: 生成的 `_evalscratch/ocr/pdf-381.txt` 包含选项 B/C/D 的可辨文本；Api 的 csproj 不含对该工具的引用。
  - `rule`: OCR 引擎选型有最小可行验证记录（跑通 pdf-381 即达标）。

## Task 3 — 用 OCR 工具修复截断选项数据
- **Status**: done
- **Evidence**:
  - 修复 11 条截断/缺失选项（逐条对照 `_evalscratch/renders/page_XXX.png` 人工核对形近字后写回）：pdf-006/158/288/291/297/302/381/398/424/432/437。每条在 `_evalscratch/ocr/pdf-XXX.txt` 有 OCR 文本证据；JSON 经 `json.dump(indent=2,ensure_ascii=False)+尾换行` round-trip 验证字节一致，编辑最小。
  - `scan_missing_opts.py` 修复后：仅 3 条“缺失/为空 A-D”且均为预期：pdf-162（小写 a-d，解析器不动）、pdf-412（A-C + a-c 混合）、pdf-491（本就 3 选项，答案 AC）。计数 ≤ 修复前。确认 NO-repair：pdf-024（other 填空图内标号）、pdf-480（judge 10 项 √/×）。
  - **关键方法学校正**：初版 `orig_serial` 与 `repaired_serial` 快照 MD5 完全相同——经查二者均跑在 **bin 旧副本（原始数据）**上（src 已修复但未 rebuild 进 bin），对比无效。已 rebuild 使 bin=修复数据后重跑。
  - 权威对比（均 serial `--concurrency 1`，仅数据不同）：`orig_serial`（原始数据）engineSolved=**450** precision=1 harm=0 → `repaired_serial`（修复数据）engineSolved=**448** precision=1 harm=0。确定性：`repaired_serial2` 与 `repaired_serial` 快照字节一致（MD5 相同），排除并行静态缓存噪声。
  - 纯数据效应：**0 gained / 2 lost（pdf-006、pdf-424）**，无假阳性、无 harm。根因：这两题原在**截断数据（仅 A/B）**下被 Concept 唯一求解；现从源 PDF 恢复完整 C/D 后，`ConceptChoiceValidator` 契约要求四选项全部命中事实（每选项恰好 1 条），C/D 无对应概念事实 → 诚实返回 `null`（不蒙数、零假阳性）。此为**数据正确性提升暴露的覆盖缺口，非回归**——修复文本已对照渲染图核真（pdf-006 C/D 见 page_006，pdf-424 C/D 见 page_142）。
  - 护栏测试同步：`Batch3ChoiceValidatorTests.ConceptTruth` 移除 pdf-006/424（附注释说明），Concept 覆盖计数 131→129，与实际一致；`Concept_ZeroFalsePositive` / `Concept_AdoptedAnswers...harm` 护栏仍通过。
  - 回归护栏：stale-DLL 仪式后全量测试 TutorBrain **950/0**、Chem **905/0**；`tests/` 下 `_Tmp*.cs`=0。
- **Depends on**: Task 2
- **Priority**: high
- **Description**: 对扫描确认的截断条目执行修复：
  - choice 缺字母（10 条）：pdf-006/158/291/297/302/381/398/424/432/437 —— 用工具 OCR 源页（含跨页溢出相邻页）恢复缺失选项，逐条对照渲染图人工核对形近字后写回 `pdf_questions.json`。
  - 全部选项为空（3 条）：pdf-024/288/480 —— OCR 图像本身恢复图内选项文本；pdf-480 为 judge 类，仅补全数据不求解。
  - 核实项：pdf-491（答案 AC，是否本就 3 项）；pdf-162/412（小写/混合标号，仅当确属独立单选时把键规范化为大写 A-D，否则不动解析器）。
  - 无法可靠辨认的字符保留空并标注“未恢复”，绝不臆造。
  - 修复后复跑“缺失/为空选项”扫描，确认计数不增。
- **AC 覆盖**: AC-5, AC-2（数据变更后重跑 EvalRunner 须 precision=1/harm=0）
- **Test Requirements**:
  - `rule`: 修复后扫描的“缺失/为空选项”条目数 ≤ 修复前；每条修复在 `_evalscratch/ocr/` 有对应 OCR 文本证据。
  - `rule`: 修复后 `EvalRunner` 新 tag 重跑 `precision=1`、`harmCount=0`（数据修复不得引入回归或假阳性）。

## Task 4 — 扩展 Curve / Molecule 校验器断言覆盖
- **Status**: done
- **Evidence**:
  - 用 `_evalscratch/match_diag.py`（把 `CurveChoiceValidator` 5 个断言匹配器与 `MoleculeChoiceValidator` 8 个断言匹配器移植到 Python）对 `repaired_serial` 未解题中 `hasStructAsset:curve`（31）与 `hasStructAsset:molecule`（24）逐选项打命中标记。
  - **Curve（31）：全部选项命中数=0**。这些题的选项全是能量/焓变图解读、滴定化学推理、分类-化合价、反应路径等，无一符合“指定 x 处 y / 极值 / 交点 / 两曲线比较”的机械断言形态；curve 资产的标注轴+点集真值并非这些题的考点。无 (b) 可扩展项。
  - **Molecule（24）：每题命中 ≤2 个选项**（最高 2，如 pdf-344=分子式+加成 H₂、pdf-327=手性碳+官能团），其余选项为共平面/共线几何、反应类型判断、红外波数、同分异构、熔点等需推理项。契约要求四选项全部可判 → 诚实 null。无任何一题达到 ≥3 命中，故不存在“补一个断言变体即可整题可解”的 (b) 候选。
  - 结论：现有 Curve/Molecule 校验器已覆盖机械断言空间；剩余未解题属 (c) 需化学推理/几何，按红线（不蒙数、零假阳性）不扩展。**未改代码、未加守卫测试**（无新断言可守）。
  - 观察（未改，留待并发的 fact-validator 工作评估）：`MoleculeChoiceValidator.MatchFunctionalGroup` 的否定正则仅捕获连续“不含有/不含/没有/无”，会把“不可能含有苯环”（pdf-327 C）误判为肯定“含有苯环”；当前 harm=0 未致实际假阳性，但属潜在风险。
- **Depends on**: Task 3
- **Priority**: medium
- **Description**: 逐个分析 `hasStructAsset:curve`（31）与 `hasStructAsset:molecule`（24）未解题的 null 原因，分三类：(a) 资产 payload 缺字段 → 补资产或跳过；(b) 选项是资产真值的机械断言变体但解析器未覆盖 → 扩展 `CurveChoiceValidator` / `MoleculeChoiceValidator` 的断言解析；(c) 需化学推理/身份推断 → 不处理，诚实 null。仅对 (b) 扩展，且严格保持“四选项全部可判 + 候选唯一 → 返回答案，否则 null”。每扩展一类断言加守卫测试（正例 + 不可判返回 null 的反例）。
- **AC 覆盖**: AC-2, AC-3, AC-4, AC-6
- **Test Requirements**:
  - `rule`: 全量测试 0 failed；`EvalRunner` 重跑 `precision=1`、`harmCount=0`。
  - `rule`: 每个新增断言分支有“不可判 → null”的守卫测试。
  - `rubric`: 图像/OCR 相关正确求解数净增（相对基线 446），且全部为正确解。

## Task 5 — graph-null 全量审计（决定是否建 GraphChoiceValidator）
- **Status**: done
- **Evidence**:
  - 用 `_evalscratch/dump_graph.py` 导出 `repaired_serial` 未解题中全部 `type=graph` 资产题（**40 道**）。检查发现 **graph 资产 payload 仅有 `nodes`/`edges` 两个键**——它是转化关系/工艺流程/反应机理的**拓扑流程图**，并非含构型/键角/配位数字段的分子几何结构。
  - 逐题分类：转化关系推断（pdf-018/074/125/132/141）、工艺流程（pdf-087/088/111/126/151/159/167/169/177/289/475/481）、反应机理（pdf-249/251/310/338/347/348/383/512）、Born-Haber 循环（pdf-241）、有机合成路线（pdf-332/363/388/524）、滴定/测定（pdf-410/413/414）、燃料电池（pdf-304）。
  - 所有选项均为化学结论（物种推断、反应类型、电子转移、平衡、产率），需从流程图做化学身份推断与推理；不存在“节点可达性/边条件/图结构性质”这类可从 `nodes`/`edges` 机械读出且四选项全可判的子集。
  - **结论：不建 GraphChoiceValidator**（建则需引入化学推理，违反“不蒙数、零假阳性”红线）。审计结论落盘 `_evalscratch/_graph_audit.md`。
- **Depends on**: Task 4
- **Priority**: low（调研性，预期收益低）
- **Description**: 全量审计 `hasStructAsset:graph`（40）未解题：判断是否存在“无身份、可机械判定”的选项子集（如节点可达性、边条件匹配、图结构性质），且该子集四选项全部可判 + 候选唯一。若存在 → 新增 `GraphChoiceValidator`（仅消费 graph 资产，严格 null 契约，接入 `CallChoice` 链路），加守卫测试；若不存在 → 在 `_evalscratch/` 记录审计结论（每题为“需身份推断/推理，不可机械判定”），不新增求解器。无论哪种，都不得为追求覆盖而放宽契约。
- **AC 覆盖**: AC-3, AC-4, AC-6
- **Test Requirements**:
  - `rule`: 若建求解器，须有“不可判 → null”守卫测试；`EvalRunner` `precision=1`、`harmCount=0`。
  - `rule`: 若不建，须有审计结论文档列出每题不可机械判定的理由。

## Task 6 — imgNoAsset 候选评估
- **Status**: done
- **Evidence**:
  - 用 `_evalscratch/dump_imgnoasset.py` 统计 `repaired_serial` 未解题中 `images` 非空但 `imageAssets` 为空的题：**84 道**（其中 60 道 choice）。**62 道选项文本已完整**（4 选项、0 空，Task 3 文本修复已覆盖），仍未解的原因是解题依赖**图像内容本身**而非缺失文本。
  - 选项全部嵌在图内的题：pdf-024（calculation 填空，图内标号，NO-repair）、pdf-288（Task 3 已恢复 A-D）、pdf-480（judge 10 项 √/×，NO-repair）—— 已在 Task 3 结论中处理。
  - 其余 imgNoAsset 以**价-类二维图**（pdf-014/103/130/139）、**实验装置图**（pdf-100/104/127/128/138）、**反应现象/装置**（pdf-113）、**气体计算图**（pdf-021）为主：即便生成结构化资产，选项仍需化学身份推断与推理，无法让四选项全部机械可判。
  - **结论：不批量生成资产**。资产化需重跑 VL→curve/molecule/graph 抽取管线（本会话范围外，且原管线已对这些图未产出干净资产）；强行生成低质资产会引入假阳性风险。留空并记录，不静默跳过。代表性不可唯一判理由已在 `_evalscratch/_imgnoasset.txt` 留痕。
- **Depends on**: Task 3
- **Priority**: low（调研性）
- **Description**: 从 `imgNoAsset`（84）中筛选候选：选项全部嵌在图内的题（pdf-024/288/480）、装置图/流程图题。评估能否用 OCR（Task 2 工具）+ 确定性规则生成**可唯一判定**的结构化资产并接入现有求解器；若生成的资产仍无法让四选项全部可判，则留空并记录。不批量生成低质资产。
- **AC 覆盖**: AC-6
- **Test Requirements**:
  - `rule`: 任一新生成资产被消费时严格 null 契约；`EvalRunner` `precision=1`、`harmCount=0`。
  - `rule`: 未生成资产的候选有“不可唯一判定”的记录，不静默跳过无凭据。

## Task 7 — 最终回归与覆盖报告
- **Status**: done
- **Evidence**:
  - stale-DLL 仪式（杀 testhost → `dotnet build-server shutdown`）后 clean rebuild 全 sln **0 错误**。构建中遇 `_TmpProbeCounting.cs` 报错：该 .cs 已不存在但 `TutorBrain.Tests` 的 `obj/` 增量缓存仍引用它（并发 spec 的探针残留），清理该项目 obj/bin 后重建通过——非源码问题。
  - 全量测试：TutorBrain **962 通过 / 0 failed / 0 skipped**、Chem **905 通过 / 0 failed / 0 skipped**（`--no-build` 退出码均 0）。TutorBrain 962 较 Task 1 基线 937 的增量来自并发 spec 新增测试（Counting/MultiSelect/StatementJudge），非本任务引入。
  - `EvalRunner` 最终 tag=`final_repaired`（并行 `--concurrency 12`）：757/757 完成、failed=0，engineSolved=**448** coverage=0.5918 precision=**1** harmCount=**0**。快照 `_evalscratch/snapshot_final_repaired.json`，摘要 `_evalscratch/checkpoint_final_repaired_summary.txt`。
  - 两套对比（`_evalscratch/final_report.py`）：
    - **方法论对齐（并行 vs 并行）**：baseline_now 446 → final_repaired 448，**net +2**；LOST [pdf-006, pdf-424]，GAINED [pdf-095, pdf-105, pdf-106, pdf-403]；precision 1→1，harm 0→0。GAINED 来自并行静态缓存噪声（串行纯数据对比中 gained=0）。
    - **纯数据效应（串行 vs 串行，仅数据不同）**：orig_serial 450 → repaired_serial 448，**net −2**；LOST [pdf-006, pdf-424]，GAINED []；precision 1→1，harm 0→0。
  - 红线全部守住：所有运行 precision=1、harm=0；零假阳性、零错误解。被“丢失”的 pdf-006/424 原为截断数据（仅 A/B）下的蒙对，从源 PDF 恢复完整 C/D 后 `ConceptChoiceValidator` 诚实返回 `null`——属**数据正确性提升暴露的覆盖缺口，非回归**（不蒙数红线）。
  - **AC-6 张力（如实报告）**：spec 门槛“净增 ≥0 / 阈值 ≥3”未达成——纯数据效应为 −2；方法论对齐 +2 但含并行噪声。不为刷覆盖回退已核真的数据或放宽“四选项全可判”契约。
  - 卫生复查：`scan_missing_opts.py` 仍仅 3 条预期缺失（pdf-162 小写 a-d / pdf-412 混合 / pdf-491 本就 3 选项，解析器按 spec 不动）；bin 副本含修复标记 `短周期碱金属`；`tests/` 下 `_Tmp*.cs`=0。
- **Depends on**: Task 4, Task 5, Task 6
- **Priority**: high
- **Description**: 最终 clean rebuild（杀 testhost + build-server shutdown + 清 src/tests bin/obj）；依次跑 TutorBrain / Chem / Batch3 全 0 failed；`EvalRunner` 跑最终 tag；计算相对基线 446 的 `engineSolved` 净增量，确认 `precision=1`、`harmCount=0`；复查 `tests/` 无 `_Tmp*.cs`；产出覆盖报告（新解 id 列表、增量、证据路径）。若任一护栏不达标，回退到上一绿色状态而非带病推进。
- **AC 覆盖**: AC-1, AC-2, AC-6, AC-7
- **Test Requirements**:
  - `rule`: 三套测试 0 failed；`EvalRunner` 最终 `precision=1`、`harmCount=0`。
  - `rubric`: 正确求解数净增 ≥ 0 且无假阳性（对应 AC-6，阈值 ≥3）。
  - `rule`: `tests/` 无 `_Tmp*.cs`。

## 验证清单（收尾）
- [x] Task 1 三套测试绿色计数已记录
- [x] Task 2 C# OCR 工具构建 0 错误、pdf-381 恢复成功、Api 未引用
- [x] Task 3 截断条目修复前后扫描对比、每条有 OCR 证据
- [x] Task 4 无新增断言（结论：无安全可扩展项 → 守卫测试 N/A），`match_diag.py` 留痕
- [x] Task 5 graph 审计结论已落盘（`_evalscratch/_graph_audit.md`，不建 GraphChoiceValidator）
- [x] Task 6 imgNoAsset 评估结论已落盘（`_evalscratch/_imgnoasset.txt`，不批量生成资产）
- [x] Task 7 最终 `precision=1 / harmCount=0`，覆盖增量报告完成（见上 Task 7 Evidence）
- [x] `tests/` 无 `_Tmp*.cs`
