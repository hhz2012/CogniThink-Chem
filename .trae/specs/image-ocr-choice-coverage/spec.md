# 图像/OCR 未解选择题覆盖 — 产品需求文档（PRD）

## Overview
- **Summary**：在零假阳性 / 零回归的红线下，处理全量 757 题中剩余 **311 道未解** 里依赖图像解析或 OCR 的题目，通过（1）用独立 C# 离线 OCR 工具修复被截断的选项文本数据、（2）扩展已有 curve/molecule 资产校验器的断言覆盖、（3）审慎评估 graph 资产与无资产图像题的可解子集，提升正确求解数；凡不能唯一判定者一律返回 `null`，绝不蒙数。
- **Purpose**：把前序 Concept 事实策展（Task 48）之后仍未解、且与图像/OCR 相关的题纳入可解范围，同时守住引擎“不蒙数”的正确性契约，并把 OCR 数据修复沉淀为仓库内可复跑的 C# 工具，便于后期维护。
- **Target Users**：高中化学题求解引擎的维护者与使用者（CogniThink TutorBrain）。

## Goals
- 把 OCR 数据修复实现为**独立 C# console 工具项目**（不进 Api 运行时），可离线复跑，原生 OCR 依赖隔离在工具层。
- 修复 `pdf_questions.json` 中选项文本缺失/为空的条目，使题干与 4 个选项完整可见。
- 在不引入假阳性的前提下，提升图像/OCR 相关未解选择题的正确求解数。
- 为 graph 类结构化资产明确“能否安全消费”的结论：有可机械判定子集则建求解器，无则诚实留空并记录。
- 全量测试套件（TutorBrain / Chem / Batch3 护栏）始终 0 failed。

## Non-Goals
- **不** 处理计数题（①②③④ / “有几项” / 同分异构体数）——属 `CountingChoiceValidator` 域，排除出本任务。
- **不** 处理多选/不定项选择（multi）的求解逻辑改造（引擎以单选为主）；仅做数据完整性修复。
- **不** 为追求覆盖率而放宽“四选项全部可判 + 候选唯一”的校验器契约。
- **不** 在逻辑层硬编码任何化学事实；化学真值只能来自 JSON 事实库或图像资产的结构化真值（RDKit / 曲线点集等）。
- **不** 重做历史 VL 抽取管线；仅按需用离线 OCR 补全缺失文本。
- **不** 把 OCR 放进 Api 运行时（不在请求时实时 OCR 图像）——OCR 仅用于离线修复静态数据，避免原生依赖进生产部署、避免 OCR 噪声威胁零假阳性红线。
- 纯文本、无图像标记、选项完整但仍未解的 `other` 题（多需深度推理/答案冲突，含 trap/d 等非 pdf 用例）**不** 纳入本任务主路径（可作为后续 Concept 事实策展的输入，不在此实现）。

## Background & Context
- **基线已刷新（本规格调研产出，HEAD 代码）**：用仓库内现成的 C# 评估工具 `CogniThink.TutorBrain.EvalRunner`（`src/CogniThink.TutorBrain.EvalRunner/Program.cs`）对 `BuildTestCases()` 全量 757 题重跑，结果落盘 `_evalscratch/checkpoint_baseline_now.jsonl` + `snapshot_baseline_now.json`：`total=757, engineSolved=446, coverage=0.5892, precision=1, harmCount=0, bothWrong=311`。此前 spec 草稿中的“324 已解 / 178 未解”是 `p1fix` 之前的过期数字，**已作废**。
- **未解分类（基于 `checkpoint_baseline_now.jsonl`，多标签计数）**：
  - `other` 123（纯文本/非图像，含 trap、d 系列等非 pdf 用例——非本任务重点）
  - `imgNoAsset` 84（有原始图但无结构化资产）
  - `hasStructAsset:graph` 40（已有 graph 资产但无求解器消费 / 消费后返回 null）
  - `hasStructAsset:curve` 31（`CurveChoiceValidator` 返回 null）
  - `hasStructAsset:molecule` 24（`MoleculeChoiceValidator` 返回 null）
  - `ocr残缺` 15（该标签有噪声，真实“文本缺失”按下述“选项字母缺失/为空”扫描判定）
  - `counting` 6 —— **排除**
  - `multi` 10 —— **排除**
  - 分类脚本与结果：`_evalscratch/classify_baseline_now.py` → `_evalscratch/_unsolved_cat_baseline_now.json`。
- **OCR 工具链可行性已验证（本规格调研产出）**：
  - 源 PDF `pdfs/2026一化儿100讲（基础+中档）习题册.pdf` 为扫描件，151 页，pdfplumber 仅能抽到每页 ≤27 字符的页眉，正文不可抽取为文本 → 文本恢复必须走 OCR。
  - `rapidocr-onnxruntime` 因缺少 `msvcp140.dll`（MSVC C++ 运行库）导致 onnxruntime DLL 加载失败，**不可用**；`Tesseract 5.4` + `chi_sim` 语言包已安装于 `C:\Program Files\Tesseract-OCR\`，Python 侧 `pytesseract` + `pypdfium2` 已实证可用。
  - 历史产物：`pdfs/_extract/exercise/page_XXX.json`（VL 逐页抽取）与 `pdfs/_extract/page_renders/`（稀疏整页渲染）均存在；但 VL 抽取文本本身也带有相同截断（如 pdf-381 在 VL page_126 也只有选项 A），故需对整页渲染图重新 OCR。
  - **已实证**：pdf-381 缺失的选项 B/C/D 通过 pypdfium2 以 scale=3 渲染 page_126（题干+A）与 page_127（B/C/D 跨页溢出）并用 Tesseract chi_sim OCR 成功恢复（`_evalscratch/page_126_ocr.txt` / `page_127_ocr.txt`）。OCR 有少量形近字错误（酯→酪、泳衣→沟衣），内容可辨，需逐条人工核对渲染图校正。
- **真实“文本缺失”条目（按选项字母缺失/为空扫描当前 `pdf_questions.json` 全量 545 条得出）**：
  - 全部选项为空：pdf-024（type=other，图内选项）、pdf-288（choice，图内流程选项）、pdf-480（judge，非 choice）。
  - choice 缺 A-D 字母（有明确单选答案，可 OCR 恢复）：**pdf-006**（缺 C/D）、pdf-158（缺 D）、pdf-291（缺 D）、pdf-297（缺 C/D）、pdf-302（缺 B/C/D）、pdf-381（缺 B/C/D）、pdf-398（缺 C/D）、**pdf-424**（缺 C/D）、pdf-432（缺 C/D）、pdf-437（缺 C/D）——共 10 条。
  - 疑似缺 D 但为多选：pdf-491（答案 AC，多选，需核实是否本就 3 项）。
  - 小写/混合标号（pdf-162 用 a-d、pdf-412 用 A-C + a-c；pdf-156/463/544 为 fill 类）：多为填空大题子问，非独立单选，**不** 全局改解析器，仅在数据层对确属独立单选者规范化键为大写 A-D。
- **OCR 实现形态决策（已与用户确认）**：OCR 做成**独立 C# console 工具项目**（建议 `src/CogniThink.TutorBrain.PdfOcrRepair`，参照 `CogniThink.TutorBrain.EvalRunner` 的 `FindRepoRoot` / 参数解析 / 落盘模式），仅在开发期 `dotnet run` 修复 `pdf_questions.json`，**不加入 `CogniThink.TutorBrain.Api` 运行时、不进生产部署**。工具内：用 C# OCR 引擎（优先 Windows.Media.Ocr 或自包含原生 DLL 的 Tesseract .NET 封装）+ PDF 渲染（优先复用仓库已有 282 张 `data/pdf_images/` 渲染图，缺页再用 PDFium/Docnet 渲染）；原生依赖隔离在工具项目的输出目录，不污染 Api。相关经验警示（C# 引入/更换 OCR 易致包清单/项目引用/源码三套不一致 → 类型找不到、命名空间残留、原生 DLL 加载失败）必须遵守：引用闭包一次性收敛、以 csproj 实际引用为准、小粒度编辑。
- **graph 资产 payload 形态**（如 `pdfs/_extract/vectorize/out/pdf-018_graph.json`）：`{nodes:[{id,label}], edges:[{from,to,cond}]}`，仅含拓扑（节点/边/条件），**不含物质身份**。抽样 pdf-018/088/111/125/241 的选项均要求“X 的化学式/性质/反应”等身份推断或多步化学推理 → 机械图校验器无法安全作答（高假阳性风险）。仅“节点可达性 / 边条件匹配”等无身份断言在理论上可机械判定，但抽样未发现此类选项，故可解子集需在实现期全量审计确认。
- **curve/molecule-null 抽样**：多数需“曲线读数 + 化学推理”混合，且校验器要求“四选项全部可判”，故大部分仍会诚实 null；仅当某题四选项均为资产可直接读出的机械断言时可扩展覆盖。
- **引擎链路**：`LlmGatewayService.CallChoice` 依次调用 14 个 `TryDecide`，其中仅 `MoleculeChoiceValidator` / `CurveChoiceValidator` 消费 `imageAssets`；`CallEngineAsync` 按 graph/curve/molecule 路由。所有 `TryDecide` 返回 `ChoiceVerifier.Outcome?`，含 `.AnswerLetter`。
- **测试基线（前序已绿，需在实现前复现锁定）**：TutorBrain、Chem、Batch3 护栏全 0 failed（具体数以实现前重跑为准）。stale dll 陷阱：跑测前须杀 testhost + `dotnet build-server shutdown` + 清 bin/obj 重建。
- **临时探针状态**：`tests/CogniThink.TutorBrain.Tests/_TmpCatProbe.cs` **已被删除**（Glob 确认 `tests/` 下无 `_Tmp*.cs`）；全量求解能力已由正式工具 `EvalRunner` 承载，无需再造探针。

## Functional Requirements
- **FR-0（C# OCR 工具）**：新建独立 C# console 工具项目，能在开发期离线运行：输入指定 pdf 题 id（或“扫描出的全部截断条目”），定位源 PDF 页（优先用已有 `data/pdf_images/` 渲染图，缺页则渲染），OCR 题干+选项区域，输出每页 OCR 文本到 `_evalscratch/`，并生成“建议写回的选项文本”补丁草案（由人核对后应用）。工具不得被 Api 项目引用。
- **FR-1**：对 `pdf_questions.json` 中选项文本缺失/为空的 choice 条目，经 OCR 工具恢复 + 逐条与渲染图人工核对后写回；不臆造、不补全无法可靠辨认的内容（宁可保留空并标记“未恢复”）。
- **FR-2**：扩展 `CurveChoiceValidator` / `MoleculeChoiceValidator` 的断言解析覆盖，仅针对“资产已提供真值、选项为该真值的机械断言变体”的情形；保持“四选项全部可判 + 候选唯一 → 返回答案，否则 null”的契约不变。
- **FR-3**：对全部 graph-null 题审计是否存在“无身份、可机械判定”的选项子集；若存在，新增 `GraphChoiceValidator`（仅消费 graph 资产，严格 null 契约）；若不存在，记录结论并不新增求解器。
- **FR-4**：对 `imgNoAsset` 中的少数候选（如选项全部嵌在图内的 pdf-024/288/480 及装置图题），评估能否经 OCR + 确定性规则生成可唯一判定的结构化资产；不能则留空。
- **FR-5**：每次变更后用 `EvalRunner` 重跑全量 757 题（新 tag），比较求解数与假阳性数（以 `pdf_questions.json.answer` 为 ground truth）。
- **FR-6**：确认 `tests/` 下无 `_Tmp*.cs` 残留（已满足，作为收尾守卫复查）。

## Non-Functional Requirements
- **NFR-1（零回归）**：任一变更后，TutorBrain / Chem / Batch3 全量测试 0 failed。
- **NFR-2（零假阳性）**：任一变更后，EvalRunner 输出的“新解题目”其答案必须等于 ground truth；不允许出现引擎返回错误字母（`precision` 保持 1，`harmCount` 保持 0）。
- **NFR-3（不蒙数）**：任何求解路径无法唯一判定时返回 `null`，不得返回猜测字母。
- **NFR-4（事实与逻辑分离）**：化学真值只存在于 JSON 事实库 / 结构化资产；逻辑层不得硬编码化学事实。
- **NFR-5（可追溯）**：每条数据修复与每个新增断言均有对应渲染图 / OCR 文本 / 资产作为证据，落盘 `_evalscratch/` 或提交说明中可查。
- **NFR-6（OCR 工具可维护）**：OCR 工具为单一 C# 项目、`dotnet run` 可复跑、有 README 级使用说明（在工具目录内或 tasks 记录中）、不依赖 Python 环境；原生依赖版本在 csproj 中锁定。

## Constraints
- **Technical**：Windows PowerShell 5（无 `&&`/`||`）；Tesseract 可执行文件 `C:\Program Files\Tesseract-OCR\tesseract.exe`（含 `tessdata\chi_sim.traineddata`）作为 OCR 引擎的基准/回退；仓库已有 282 张渲染图 `src/CogniThink.TutorBrain.Api/data/pdf_images/`（csproj `Content Include` 拷贝到输出）；沙箱网络受限，优先选用已安装/可离线还原的 NuGet 包，不依赖联网下载模型。注意：Api 输出目录已含 `msvcp140.dll`（RDKit 依赖带入），工具项目若需 VC++ 运行库可沿用同版本。
- **Business（红线，冻结不可违反）**：零假阳性 / 零 harm / 零回归；无唯一解返回 `null` 绝不蒙数；化学事实只存 JSON；计数题不进本任务。
- **Dependencies**：`src/CogniThink.TutorBrain.Api/data/pdf_questions.json`（545 条，含内嵌 `imageAssets`）、`data/highschool/general-choice-facts.json`、源 PDF、历史 `pdfs/_extract/` 产物、`ChoiceVerifier.ParseOptions`（仅解析大写 A-D）、`CogniThink.TutorBrain.EvalRunner`（评估基线）。

## Assumptions
- 以 `pdf_questions.json` 的 `answer` 字段作为 ground truth 来判定“正确求解 / 假阳性”。
- `EvalRunner` 是全量求解的权威工具；分类脚本 `_evalscratch/classify_baseline_now.py` 可在每次变更后重跑以刷新未解集合（后续可考虑把分类能力并入工具，但非本任务必须）。
- OCR 的形近字错误可通过“对照渲染图人工核对 + 少量规则校正”解决；无法可靠校正的字符不臆造。
- graph 资产的身份推断问题不在本任务解决（需化学推理引擎或更大事实库），故 graph 求解器仅可能覆盖极小可机械判定子集，预期收益低。
- C# OCR 工具只修复数据，不参与运行时判定；其输出不直接喂给引擎，须经人工核对写入 `pdf_questions.json`。

## Acceptance Criteria

### AC-0: C# OCR 工具可构建可复跑
- **Type**: `rule`
- **Given**: 新建的独立 C# console OCR 工具项目
- **When**: `dotnet build` 该项目，随后 `dotnet run -- <id>` 对一个截断题（如 pdf-381）执行
- **Then**: 构建 0 错误；运行输出该题所在页的 OCR 文本到 `_evalscratch/`，并给出缺失选项的建议文本；过程不依赖 Python、不联网
- **Pass Condition**: 构建退出码 0；指定题的 OCR 文本文件生成且包含缺失选项内容；Api 项目未引用该工具
- **Evidence**: 构建输出 + 生成的 OCR txt + csproj 引用关系审查

### AC-1: 全量测试零回归
- **Type**: `rule`
- **Given**: 工作区包含本任务的任一变更
- **When**: 杀 testhost → 清 bin/obj → `dotnet build` → 依次跑 TutorBrain、Chem、Batch3 护栏测试
- **Then**: 三个套件均 0 failed，且失败数不高于变更前基线
- **Pass Condition**: 三套测试退出码均为 0，failed=0
- **Evidence**: 测试命令输出（passed/total/failed 计数）

### AC-2: 零假阳性
- **Type**: `rule`
- **Given**: 本任务变更后，EvalRunner 在全量 757 题上求解，以 `pdf_questions.json.answer` 为 ground truth
- **When**: 比对“引擎返回非 null 的字母”与 ground truth
- **Then**: 不存在“引擎返回字母 ≠ ground truth”的题（假阳性为 0）
- **Pass Condition**: EvalRunner summary 的 `precision=1` 且 `harmCount=0`
- **Evidence**: `_evalscratch/checkpoint_<tag>_summary.txt` + 新解列表与答案核对

### AC-3: 不蒙数（无唯一解即 null）
- **Type**: `rule`
- **Given**: 任一校验器（含新增 GraphChoiceValidator，若建）的执行路径
- **When**: 遇到任一选项不可归入支持的断言类型、或候选不唯一、或资产无效
- **Then**: 整题返回 `null`，不返回字母
- **Pass Condition**: 代码审查确认各 `TryDecide` 在不可判分支 `return null`；新增守卫测试覆盖不可判情形返回 null
- **Evidence**: 源码 + 守卫测试

### AC-4: 化学事实不入逻辑层
- **Type**: `rule`
- **Given**: 本任务涉及的所有源码变更
- **When**: 审查校验器与解析逻辑
- **Then**: 不存在硬编码化学事实（物质性质、反应结论、数值真值）；真值仅来自 JSON 事实库或资产结构化真值（RDKit / 曲线点集 / 图拓扑）
- **Pass Condition**: Grep/审查未在逻辑层发现化学事实常量
- **Evidence**: 审查记录

### AC-5: 截断选项数据已修复
- **Type**: `rule`
- **Given**: 全量扫描 `pdf_questions.json` 中选项字母缺失/为空的 choice 条目（基线：3 条全空 + 10 条缺字母 + 核实项）
- **When**: 对每条经 OCR 工具恢复 + 人工核对后写回，并复跑扫描
- **Then**: 凡可从源 PDF 可靠辨认的缺失选项文本已补全；无法可靠辨认的保留空并在证据中标注“未恢复”，不臆造
- **Pass Condition**: 修复后扫描的“缺失/为空选项”条目数 ≤ 修复前，且每条修复有对应 OCR 文本/渲染图证据
- **Evidence**: `_evalscratch/` 修复前后扫描对比 + 每页 OCR txt

### AC-6: 图像/OCR 相关未解覆盖提升（且全为正确解）
- **Type**: `rubric`
- **Dimension**: 图像/OCR 相关未解选择题的正确求解数净增量（仅计零假阳性的正确解）
- **Scale**: 1-5
- **Anchors**: 1 = 覆盖未提升或引入假阳性；3 = 完成数据修复 + C# OCR 工具落地，正确求解数净增 ≥0 且 precision=1/harm=0；4 = 数据修复 + 至少一项校验器覆盖扩展，正确求解数净增 ≥ 3 且无假阳性；5 = 数据修复 + 多项扩展，正确求解数净增 ≥ 5 且无假阳性、graph/imgNoAsset 审计结论明确
- **Pass Threshold**: >= 3
- **Evidence**: EvalRunner 修复前后 summary 对比（engineSolved 增量、precision=1、harmCount=0）

### AC-7: 无临时探针残留
- **Type**: `rule`
- **Given**: 任务收尾
- **When**: 检查 `tests/` 目录
- **Then**: 无 `_Tmp*.cs` 残留
- **Pass Condition**: `Get-ChildItem tests -Filter _Tmp*.cs -Recurse` 结果为 0
- **Evidence**: 命令输出为空（当前已满足，收尾复查）

## Open Questions
- [ ] pdf-491（答案 AC）是否本就为 3 选项多选，无需补 D？（实现期核实渲染图）
- [ ] C# OCR 引擎选 Windows.Media.Ocr 还是 Tesseract .NET 封装？（实现期先做最小可行验证：任选其一跑通 pdf-381；遵循“引用闭包一次性收敛”避免依赖混乱）
- [ ] graph 资产是否存在任何可机械判定的选项子集？（实现期全量审计后定论；若不存在则 GraphChoiceValidator 不建）
- [ ] `imgNoAsset` 中装置图/流程图题，经 OCR 能否恢复图内选项文本并生成可唯一判定资产？（实现期评估后定论）
- [ ] 是否把未解分类脚本 `classify_baseline_now.py` 的能力并入 C# 工具或测试，彻底摆脱 Python？（建议后续迭代，本任务先用现有脚本）
