# 图像依赖 + OCR 残缺选择题处理实施计划

## 一、Repository Research（现状与缺口）

### 当前基线（Task 38/39 后）
- 全量评估：`totalCases=741`、`engineSolved=373`、`harm(adoptedButWrong)=0`、`gatewayPrecision=1`。
- 全题库审计（确定性链路，不含 ChoiceVerifier）：`solvedCorrect=244`、`unsolved=243`。
- 全量测试：TutorBrain 819 + Chem 890 = 1709 通过 / 0 失败。

### 图像依赖题：资产已就绪，但 choice 链路用不到
- 题库中共 **206 题带图**，其中 **106 题已有结构化 `imageAssets`**（choice 中 84 题）。资产类型：`graph` 47、`curve` 39、`molecule` 42。
- 资产质量：对 **74 道当前未解且带资产的 choice 题**逐一探针，**payload 全部有效**——`molecule` 28/28 有 `valid=true`+`smiles`；`curve` 26/26 有 `series` 数据；`graph` 31/31 有 `nodes`/`edges`。
- **核心缺口**：`LlmGatewayService.CallChoice(EngineRequest req)` 是 private 且**不接收 `imageAssets`**（[LlmGatewayService.cs:411](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L411)）。`CallEngineAsync` 顶部的 `TryRouteGraph/TryRouteCurve/TryRouteMolecule` 只在 choice 分支**之前**运行，且：
  - `TryRouteMolecule` 对 `TaskType=="choice"` 直接返回 null（[LlmGatewayService.Molecule.cs:21](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs#L21)）——选择题不让分子特征文本替换选项答案。
  - `TryRouteGraph` 仅窄覆盖「选项是物质组」的 `VerifyOptions` 场景（[LlmGatewayService.Graph.cs:20](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Graph.cs#L20)），绝大多数图像选择题选项是陈述（性质/数值），提取不到物质组 → 返回 null → 落入 choice 分支 → 资产丢失。
- 结论：**74 道带有效资产的题，其资产在 choice 判定链路中完全不可用**。这是最大的增量机会。

### Core 能力现状（决定可覆盖的断言）
- `MoleculeAnalyzer.Analyze`（[MoleculeAnalyzer.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs)）已能输出：分子式、不饱和度 Ω、环数、官能团（12 类 SMARTS）、原子杂化/配位数/H 数/孤对、中心原子、极性近似。
- **缺失**：手性碳计数、等效氢组数、可加成 H₂ 数、sp²/sp³ 碳数统计。需扩展（RDKit `RDKitDotNetCore` 2026.5.29，实施阶段探针验证 `findPotentialStereo`/对称 rank API 是否暴露；不可用则用 SMARTS 降级）。
- `CurveQuery`（[CurveQuery.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/CurveQuery.cs)）已有曲线资产解析能力，但仅服务非 choice 路由；choice 链路未用。
- `GraphSolver` 已有图求解能力，`TryRouteGraph` 已接入 choice 前的窄场景。

### OCR 残缺题：39 道，本质是数据损坏
- 分类标记来自审计：`NOLETTER=33`、`OPTS0=19`（有重叠），共 39 道。
- 根因（回溯 [build_final.py](file:///D:/github/CogniThink-Chem/pdfs/_extract/build_final.py)）：
  1. **选项/directive 丢失**：原始 OCR（`pdfs/_extract/exercise/page_*.json`）抽取时选项未捕获（如 pdf-141 只有 A、pdf-226/255 无 options）。
  2. **答案错位**：`build_final.py` 按 `(page, num)` 用 `difflib` 对齐习题与答案，num 漂移导致 `answer`/`note` 与题干不匹配（如 pdf-135 题干是「硫的性质」，note 却讲 CO₂/SO₂ 除杂；pdf-141 note 也错位）。
  3. **假 choice**：判断题（答案为 √× 串）、填空题、大题小问被标为 `type=choice` 进入 choice 评估集，但无法走字母判分（如 pdf-117/119-123/466-473 是单句判断；pdf-275 是 pH 排序填空；pdf-416/463/544 是大题某小问）。
- **可回溯数据**：`pdfs/_extract/exercise/page_*.json`（151 页原始题面）、`pdfs/_extract/answer/page_*.json`（154 页原始答案）、习题册/答案册 PDF、`test_vl.py`（qwen-vl-plus，可渲染 PDF 页面重 OCR）。部分题原始 JSON 选项完整（说明残缺发生在对齐阶段），部分需 VL 重抽。

## 二、范围与红线（已与用户确认）

### 范围
- **图像**：做 `molecule`（28 道）+ `curve`（26 道）；`graph`（31 道）诚实留痕到后续——graph 流程题需「图推断物种 → 多步化学推理」，假阳性风险最高，现有 `TryRouteGraph` 已覆盖其窄场景。
- **OCR**：对 39 道全部尝试修复；但**只写入能权威核对的答案/选项**，无法确定的标记留痕、绝不蒙数（零 harm）。

### 红线（frozen，沿用前序 Task）
- 引擎/图像数据是唯一真值源：不唯一可判 → 返回 `null`，绝不蒙数。
- 零假阳性、零 harm（adopted-but-wrong）、零回归。
- 化学事实存 JSON；图像真值来自 RDKit / 曲线结构化数据，不硬编码化学事实进逻辑。
- 加法式接线：域外返回 `null` 不改变既有链路。
- OCR 修复只动数据（`pdf_questions.json`），不改引擎逻辑；不迁就答案（题目数值与真值冲突时修题目，不改真值）。
- 不迁就答案：题目数值与引擎真值不一致时修正题目，绝不改引擎或答案。

## 三、Task 40：图像资产接入 choice 链路 + MoleculeChoiceValidator（有机结构题）

### 目标
让带 `molecule` 资产的选择题能用 RDKit 真值判定选项：分子式、官能团/反应、手性碳数、等效氢组数、杂化碳数、可加成 H₂ 数、不饱和度、极性。当前 28 道未解 molecule 真题为奖励，新建可控格式保底题为主。

### 文件与变更
- [LlmGatewayService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)：`CallEngineAsync` 调 `CallChoice` 时传入 `imageAssets`；`CallChoice` 签名改为 `(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets)`。
- [MoleculeAnalyzer.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs)：扩展 `MoleculeAnalysisResult`，增加 `ChiralCenterCount`、`EquivalentHydrogenGroups`、`AddableH2Count`、`Sp2CarbonCount`、`Sp3CarbonCount`；`Analyze` 内计算（RDKit API，探针验证）。
- 新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/MoleculeChoiceValidator.cs`：`TryDecide(EngineRequest req, IReadOnlyList<ImageAsset>? assets)`。
- 新建保底题到 [tier2_questions.json](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/tier2_questions.json)：`t4mol-01`~`t4mol-10`（10 道），每题带 `imageAssets`（molecule，valid+smiles）。
- 新建测试 `tests/CogniThink.TutorBrain.Tests/MoleculeChoiceValidatorTests.cs`。

### 实现步骤
1. **探针验证 RDKit API**：临时探针测试 `RDKFuncs.findPotentialStereo(mol)`、原子对称 rank（用于等效氢）、双键/三键/芳环计数在 `RDKitDotNetCore` 2026.5.29 是否可用。不可用则降级：手性碳用 SMARTS `[C;X4;!$([C]=*)]` 配合「4 个邻居规范 SMILES 互异」判定；等效氢用「逐碳去氢后的规范 SMILES 分组」近似（高中口径足够）。验证后删除探针。
2. **扩展 `MoleculeAnalyzer`**：在 `BuildResult` 中计算新增字段，失败不抛异常（返回默认值但 `Success` 仍由解析决定）。补 Chem 单测锁定教材分子值（如 2-丁醇手性碳=1、乙烷等效氢=1 组、苯加成 H₂=3）。
3. **`MoleculeChoiceValidator`**：
   - 守卫：存在 `type=="molecule"` 且 `payload.valid==true` 的资产；`ChoiceVerifier.ParseOptions` 能切出 4 选项 A-D；答案为单字母（grading 可采纳）。任一不满足 → `null`。
   - 选项断言解析（正则，仅匹配明确可真值化的断言）：
     - 分子式：`CₓHᵧO_z…` / `分子式为…` → 比对 RDKit `MolecularFormula`（归一化角标）。
     - 官能团/反应：`含/含有{羧基|羟基|…}`、`能与 FeCl₃ 显色`、`能发生{酯化|加成|氧化|水解}` → 命中 `FunctionalGroups` 白名单。
     - 手性碳：`有{N}个手性碳` / `含{N}个手性碳原子` → 比对 `ChiralCenterCount`。
     - 等效氢：`一氯代物有{N}种` / `核磁共振氢谱有{N}组峰` / `等效氢有{N}种` → 比对 `EquivalentHydrogenGroups`。
     - 杂化碳：`sp²杂化的碳有{N}个` → 比对 `Sp2CarbonCount`。
     - 加成：`1 mol 最多与{N} mol H₂ 加成` → 比对 `AddableH2Count`。
     - 不饱和度/极性：`不饱和度为{N}` / `是{极性|非极性}分子`。
   - 真值比对：四选项**全部**能干净解析为上述断言类型才继续；任一选项断言无法归入上述类型 → 整题 `null`（不蒙数）。比对后正确/错误方向唯一候选 → 返回字母；否则 `null`。
4. **接线**：`CallChoice` 中在 `ConceptChoiceValidator` 之后、`CountingChoiceValidator` 之前插入 `MoleculeChoiceValidator.TryDecide(req, imageAssets)`（域外 null 不影响链路）。
5. **保底题**：10 道，覆盖分子式/官能团/手性碳/等效氢/杂化/加成/不饱和度/极性，题干给分子名（资产带 smiles），选项断言明确，经探针逐题核对字母与 RDKit 真值一致。
6. **护栏**：全题库扫描——凡 `MoleculeChoiceValidator` 非 null 必与题库答案一致；28 道 molecule 真题诚实「null 或必正确」。

### 可解边界（诚实留痕）
- 「所有碳原子共平面/共线」几何题（pdf-386/387 等）：RDKit 2D 无法可靠判定 → `null`。
- 「红外光谱 3000 cm⁻¹ 以上吸收峰」「同分异构体不可能含苯环」等推理 → `null`。
- 化学用语题（pdf-431~434/438，电子式/模型图）：非分子结构判定，归 `StructurePeriodicValidator` 或诚实 `null`，不由本校验器处理。

## 四、Task 41：CurveChoiceValidator（坐标曲线题）

### 目标
让带 `curve` 资产的选择题能用曲线 series 真值判定选项中的明确数值断言（某点浓度/pH/Ksp、大小比较、极值）。26 道未解 curve 真题为奖励，新建保底题为主。

### 文件与变更
- 新建 `src/CogniThink.TutorBrain.Api/Application/Gateway/CurveChoiceValidator.cs`：`TryDecide(EngineRequest req, IReadOnlyList<ImageAsset>? assets)`。
- 复用 [CurveQuery.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/CurveQuery.cs) 的曲线解析（或提取共享读取函数）。
- 新建保底题 `t4cur-01`~`t4cur-06` 到 tier2_questions.json（带 curve 资产）。
- 新建测试 `tests/CogniThink.TutorBrain.Tests/CurveChoiceValidatorTests.cs`。

### 实现步骤
1. **确认 curve payload 结构**：探针打印 2~3 道题的完整 series（点坐标、标签、轴含义），确定可读取：指定 x 处的 y 值、极值点、两曲线交点、单调区间。
2. **`CurveChoiceValidator`**：
   - 守卫：存在 `type=="curve"` 资产 + 4 选项 A-D + 单字母答案。
   - 仅匹配选项中**明确的数值/比较断言**：`Ksp = 2.5×10⁻⁹` / `浓度为 0.15 mol·L⁻¹` / `pH = 4.2` / `T₁ > T₂` / `Ksp(A) > Ksp(B)` / `b 点{最大|最小}`。解析出「曲线标识 + 断言类型 + 声称值」。
   - 用 curve 资产真值读取对应点/比较，1e-4 容差比对；四选项全可解析且候选唯一 → 返回字母；任一选项无法归入明确数值断言 → 整题 `null`。
3. **接线**：`CallChoice` 中 `MoleculeChoiceValidator` 之后插入（域外 null）。
4. **保底题**：6 道，资产曲线数据可控，选项断言为明确数值读取/比较，探针逐题核对。
5. **护栏**：全题库扫描零假阳性；26 道 curve 真题诚实「null 或必正确」。

### 可解边界（诚实留痕）
- 需要读曲线后再做化学多步推理的（如 pdf-077 读浓度后写离子方程式、pdf-256 读体积分数算速率比）→ 数值可读取但选项含推理 → 整题 `null`。
- 选项断言为定性描述且无明确数值锚点的 → `null`。

## 五、Task 42：OCR 残缺题数据修复（39 道）

### 目标
修复 39 道 NOLETTER/OPTS0 题的数据损坏：假 choice 改 type 剔除出 choice 评估、答案错位重新匹配、选项/directive 丢失补全。只写入能权威核对的，不引入错误答案。

### 文件与变更
- [pdf_questions.json](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json)：修复 39 道的 `type`/`options`/`answer`/`answerKeywords`/`note`。
- 留痕到 [tasks.md](file:///D:/github/CogniThink-Chem/.trae/specs/chem-engine-question-solving/tasks.md)。

### 实现步骤
1. **逐题分类与核对**（对 39 道）：
   - **(a) 假 choice**：答案为 √× 串、或为填空数值/排序、或是大题某小问（选项是 a-d 仪器标号等）。核对 `questions_parsed.json` + 答案册确认后：改 `type` 为非 choice（如 `judge`/`fill`），或在 choice 评估集排除（`BuildTestCases` 已按 type 渲染，改 type 即自动不进 choice 字母判分）。**不删题**，只改类型，避免丢题。
   - **(b) 答案错位**：`note` 与题干不匹配的（pdf-135/141 等）。回溯 `pdfs/_extract/answer/page_*.json`（按 `ans_page`）找该页答案，按 `num` 重新匹配；若原始答案 JSON 也错位，用 `test_vl.py` 渲染答案册对应页 VL 重抽答案。确认后写入正确 `answer` + `answerKeywords`。
   - **(c) 选项/directive 丢失**：pdf-141（仅 A）、pdf-226/255（无 options）等。回溯 `exercise/page_*.json`（按 `src_page`）；原始也缺则 VL 渲染习题册页面重 OCR 补全选项。补全后若能被现有引擎解（奖励）或诚实 null 均可，但不得引入 harm。
2. **权威来源校验**：每道修复的答案/选项必须来自①原始 JSON 完整处、或②答案册 VL 重抽、或③题干原图 VL 重抽，三者至少其一且自洽。无法自洽的**不写入**，保留原状 + tasks.md 留痕。
3. **回归验证**：修复后跑全题库评估，确认 `harm` 不增、`precision` 不下降；修复的题若被引擎采纳必判正。

### 风险与对策
- 答案册 VL 重抽有噪声：对每道重抽结果与 `note`/相邻题交叉验证，不一致的留痕不写入。
- 假 choice 改 type 可能影响评估计数：改 type 后这些题不再走 choice 字母判分，`totalCases` 统计口径不变（仍在题库），但 choice 子集缩小——这是正确的（它们本就不是选择题）。在 tasks.md 记录口径变化。

## 六、验证（每个 Task 完成后 + 整体）
- 全量测试 `dotnet test CogniThink.Chem.sln`：0 失败 0 跳过（TutorBrain + Chem）。
- 新建校验器单测：单元逻辑锁 + 保底题直接解对 + 端到端 `CallEngineAsync` 网关采纳 + **全题库护栏**（凡非 null 必与真值一致 → 0 假阳性）。
- 评估快照（桩 LLM，免网络）：`harm=0`、`gatewayPrecision=1`、与基线 diff `LOST=0`，新增保底题全部 gained 且判对。
- Release 构建 `dotnet build -c Release`：0 错误。
- 临时探针全部清理（`tests/` 下 `_Tmp*.cs` 为 0）。

## 七、风险
- **RDKit wrapper 无手性/对称 API**：实施阶段探针验证；不可用则 SMARTS/规范 SMILES 降级，覆盖高中口径，不硬解复杂立体化学。
- **有机题「共平面/共线」**：RDKit 2D 不可靠 → 诚实 `null`，不纳入覆盖范围。
- **curve 选项自由文本歧义**：只匹配明确数值/比较断言，其余 `null`；宁可漏解不可错解。
- **OCR 答案册噪声**：只写自洽结果，不确定留痕；零 harm 优先于覆盖率。
- **graph 题留痕**：31 道不硬解，现有 `TryRouteGraph` 窄覆盖保留，不放宽。
- **并发修改**：工作树有并行 agent（T1/T2/T3 批次文件），编辑 `tasks.md`/`LlmGatewayService.cs`/`pdf_questions.json` 前重新读取最新内容；追加 Task 编号前 grep `^## Task` 确认最新号。
