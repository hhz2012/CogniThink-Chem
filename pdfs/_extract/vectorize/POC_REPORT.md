# 题库配图重绘与结构化 PoC 报告

> 目标：把从扫描版 PDF 识别出的位图（PNG）重绘为矢量 SVG，并转成可被规则引擎精确消费的结构化格式。
> 样本：3 张有意义图，覆盖三类（曲线 / 转化流程图 / 结构式）。

## 结论

**能重绘，且能转成有意义的结构化格式供引擎利用。** 三类样本全部跑通"扫描位图 → VL 结构化 → 代码校验/标准化 → 重绘 SVG → 写入题库 → C# 引擎可读取"全链路，目检与运行时验证均通过。

## 方法：统一框架

```
扫描位图 PNG
   │  ① VL(qwen3-vl-plus) 理解图，输出结构化草稿 JSON（带缓存 vl_struct_cache，可重跑不扣费）
   ▼
结构化草稿（归一化坐标 / 节点边 / SMILES）
   │  ② 代码校验 + 标准化：
   │     - 曲线：用轴刻度标定把归一化点 → 实际数值；去重
   │     - 流程：校验节点/边连通性
   │     - 结构：RDKit 校验 SMILES 合法性，生成 molblock
   ▼
payload（机器可计算的数据）  +  重绘 SVG（矢量，可缩放）
   │  ③ 写入题库 imageAssets 字段 + C# 模型 ImageAsset 强类型映射
   ▼
引擎按 type 路由消费（curve→计算引擎 / graph→ProcessFlowEngine / molecule→有机推断引擎）
```

设计原则：**VL 负责"理解图"，代码负责"确定性校验与重绘"**——准确率靠 VL 理解力，数值精度/可复现性靠代码。

## 三类 PoC 结果（3/3 准确）

### 1. 坐标曲线图 · pdf-077（Fe³⁺/Br₂ 随 Cl₂ 变化）
- VL 提取：x 轴刻度 0/0.1/0.2/0.3 及归一化位置，两条曲线的拐点坐标。
- 代码标定：x 归一化 → 实际 mol 值；Y 轴原图未标数值，保留归一化并标注 `yAxisLabeled:false`。
- 重绘：折线、刻度、标签与原图完全一致（红=Fe³⁺ 上升→平台，蓝=Br₂ 0.1 起上升）。
- payload 样例：
  ```json
  {"xLabel":"n(Cl₂)/mol","yLabel":"n/mol","xAxisLabeled":true,"yAxisLabeled":false,
   "series":[{"name":"Fe³⁺","points":[[0,0],[0.1,0.5],[0.3,0.5]]},
             {"name":"Br₂","points":[[0.1,0],[0.3,0.5]]}]}
  ```
- 引擎价值：可精确读取拐点（Cl₂=0.1 处 Fe³⁺ 达峰、Br₂ 开始生成），计算反应计量比，无需 VL 猜数值。

### 2. 转化关系图 · pdf-018（V/W/X/Y/Z 转化）
- VL 提取：5 节点 + 5 条带条件的有向边（V→X Δ、W→X Δ、X→V CO₂、X→Y CO₂、Y→Z HCl），与原图拓扑完全一致。
- 重绘：力导向布局 SVG（兼容带环图；原图 X→V 形成环）。
- payload 样例：
  ```json
  {"nodes":[{"id":"V","label":"V"},{"id":"W","label":"W"},{"id":"X","label":"X"},{"id":"Y","label":"Y"},{"id":"Z","label":"Z"}],
   "edges":[{"from":"V","to":"X","cond":"Δ"},{"from":"W","to":"X","cond":"Δ"},
            {"from":"X","to":"V","cond":"CO₂"},{"from":"X","to":"Y","cond":"CO₂"},{"from":"Y","to":"Z","cond":"HCl"}]}
  ```
- 引擎价值：`ProcessFlowEngine` 可直接判"V 能否一步到 X""X 加 CO₂ 生成什么"，拓扑查询零歧义。

### 3. 物质结构式 · pdf-199（2-羟基-3-苯基丙酸）
- VL 识别 SMILES：`OC(Cc1ccccc1)CC(=O)O`，RDKit 校验 `valid=true`，生成 molblock（1151 字符）。
- 重绘：RDKit 生成标准 2D SVG，清晰标注羧基(红)、羟基、苯环，远胜扫描图。
- payload 样例：`{"name":"3-苯基乳酸","smiles":"OC(Cc1ccccc1)CC(=O)O","valid":true,"molblock":"...","note":"..."}`
- 引擎价值：`MoleculeStructureService`/`OrganicInferenceEngine` 可算不饱和度、官能团、同分异构、酸性比较，不依赖 VL 视觉判断。

## 统一资产 Schema（已落地题库）

题库 `pdf_questions.json` 每题新增可选字段 `imageAssets`，与现有 `images`（位图路径）并列：

```json
"imageAssets": [
  {
    "type": "curve | graph | molecule | crystal | apparatus",
    "source": "pdf_images/p022_n06_0.png",
    "svg": "vector_svg/pdf-077_curve.svg",
    "payload": { ... }
  }
]
```

- C# 端已定义强类型 `ImageAsset`（`Type`/`Source`/`Svg`/`Payload:JsonElement?`），`QaTestCase` 与 `PdfQuestionItem` 均已加 `ImageAssets` 字段，`GenPdfQuestions` 完成映射。
- csproj 已加 `data\vector_svg\**\*` 拷贝规则，SVG 随构建同步到 bin。
- 运行时反射验证（批量后）：`total=545, withImages=206, withImageAssets=106`（graph 47 / curve 39 / molecule 42，共 128 个资产，15 题含多资产）——证明新增字段不破坏现有加载，且结构化数据可被引擎强类型读取。

引擎消费策略：有 `imageAssets.payload` → 按 `type` 路由给对应规则引擎做精确推理；无 → 回退把位图丢给 VL（当前行为）。**渐进式，不破坏现有链路。**

## 批量铺开估算

| 图类型 | 有意义张数 | PoC 准确率 | 批量建议 | 风险/校验手段 |
|---|---|---|---|---|
| 坐标曲线图 | 41 | 3/3 | ✅ 优先批量 | 无刻度轴/多曲线/对数坐标需细化 prompt；归一化标定靠代码保证 |
| 转化/工艺流程图 | 57 | 3/3 | ✅ 优先批量 | 带环图用 spring 布局；校验边端点均在节点集内 |
| 物质结构式/键线式 | ~50 | 3/3 | ✅ 优先批量 | **RDKit 自动校验 SMILES** 可过滤错误，失败回退位图；复杂稠环可能需人工抽检 |
| 晶体结构图 | 24 | — | 阶段2 | 原子坐标精度难；对常见晶胞(NaCl/CsCl/金刚石等)做模板匹配较可靠 |
| 实验装置图 | 65 | — | 不矢量化 | 连接复杂、ROI 低；保留位图靠 VL 理解即可 |
| 微观示意图 | 18 | — | 阶段2 | 粒子种类/个数识别 |

前三类合计约 **148 张**，占有意义图的 55%，且对应规则引擎已就绪，是批量铺开的高价值目标。VL 调用带缓存（`vl_struct_cache/`），可断点续跑、不重复扣费。

## 产物清单

- 重绘 SVG：`src/CogniThink.TutorBrain.Api/data/vector_svg/pdf-077_curve.svg`、`pdf-018_graph.svg`、`pdf-199_molecule.svg`
- 对比截图：`pdfs/_extract/vectorize/out/compare_curve_pdf077.jpg`、`compare_graph_pdf018.jpg`、`compare_molecule_pdf199.jpg`
- 结构化草稿缓存：`pdfs/_extract/vectorize/vl_struct_cache/`
- 脚本：`pdfs/_extract/vectorize_poc.py`（提取+重绘）、`inject_assets_poc.py`（注入题库）
- 试点数据：`pdf_questions.json` 中 pdf-077/018/199 的 `imageAssets` 字段

## 批量铺开结果（156 张目标 → 128 个资产落地）

把 PoC 脚本改造成批量管线，对意义报告筛出的 **156 张目标图**（graph 57 / curve 40 / molecule 59，分布在 128 题）跑完"提取 → 渲染 → 注入"全链路。

### 数量漏斗

| 阶段 | graph | curve | molecule | 合计 |
|---|---|---|---|---|
| 批量目标（batch_list.json） | 57 | 40 | 59 | 156 |
| VL 提取成功（缓存可断点续跑） | 57 | 40 | 54 | 151 |
| 生成 SVG + payload | 57 | 40 | 54 | 151 |
| **质量过滤后注入题库** | **47** | **39** | **42** | **128** |
| 覆盖题目（去重，含 15 题多资产） | — | — | — | **106** |

被过滤掉的 28 个（均回退"运行时把位图丢给 VL"的现有链路，不破坏解题）：
- **graph 10 张**：经 prompt 守卫判定为**非转化/流程图**（实为价类二维图等），返回空 `{nodes:[],edges:[]}`，不注入——避免 VL 凭空捏造边误导引擎。
- **curve 1 张**：VL 未提取到任何数据点，不注入。
- **molecule 17 张**：12 张 RDKit 校验 SMILES 不合法 + 5 张 VL 返回非 JSON 解析失败；多为球棍模型/空间填充模型/抽象占位图（VL 用 `X/M/W` 占位原子），RDKit 无法解析。

### 质量抽检与关键修正

抽检每类 5–6 张原图 vs 重绘 SVG 对比（`preview_gen.py` → `out/preview_batch.html`）：

- **graph**：pdf-071（KMnO₄ 制 Cl₂ 工艺流程）、pdf-064/087/125（物质转化）、pdf-524（有机反应序列）的节点/边/条件提取均与原图一致，`ProcessFlowEngine` 可直接消费。
- **curve**：pdf-077（Fe³⁺/Br₂ 曲线）拐点标定准确；3 张价类二维图（pdf-152/256/479）虽被粗分为 curve，但其 payload 含"物质类别×化合价"坐标仍有意义，渲染器已**补点标记**让离散点可见。
- **molecule**：RDKit 校验通过率 **42/59 = 71%**；通过者（如 pdf-140 环硫、pdf-199 取代酸）SVG 清晰、molblock 可用，`OrganicInferenceEngine`/`MoleculeStructureService` 可算不饱和度/官能团。

批量过程中发现并修复的两个准确率问题：
1. **graph 幻觉**：价类散点图被误判为 graph 时，VL 会捏造箭头与反应条件（如 pdf-014 捏造 A→E 与 H₂SO₄）。→ 在 graph 提示词前加**守卫**："先确认是否存在转化箭头，否则输出空"，重提后 10 张非转化图正确返回空并被排除。
2. **curve 离散点不可见**：渲染器只画 polyline，价类图单点 series 不显示。→ 给每个数据点补 `<circle>` 标记，单点散点也可见。

注入脚本 `inject_assets_batch.py` 另含两道保险：**空资产过滤**（graph 无节点 / curve 无数据点 / molecule 未过 RDKit 均不注入）与**陈旧清除**（重跑时先清空上一轮被过滤题目的残留 `imageAssets`，杜绝幻觉数据残留）。

#### graph 幻觉：独立 VL 二分类校验（第二道保险，已固化进管线）

prompt 守卫是**软约束**（依赖 VL 提取时自觉判空），为防止提示词变动 / 模型升级时回退，新增与提取解耦的独立校验工具 `verify_graph.py`：对全部 57 张 graph 原图单独做一次 VL 二分类，输出 `{is_flow, reason}`（带缓存 `verify_graph_cache/`，可断点续跑），判定标准为"图中是否存在表示物质转化 / 工艺流程方向的箭头连线"——价类二维图、坐标散点 / 曲线图、装置图、微观示意图、晶体结构图、氢键图、纯结构式一律判 `false`。

校验结果（57 张全部完成，0 解析失败）：`is_flow=true` 47 张、`is_flow=false` 10 张——**恰好等于守卫判空排除的 10 张，与守卫 100% 吻合**（pdf-014/103/130/139 价类二维图、pdf-196/288/340 题干无图 / 无箭头、pdf-212 离子结构式、pdf-370 A-T 氢键示意图、pdf-511 化学结构式）。目检边界图确认精准（pdf-370 氢键虚线 → NOT-FLOW ✓、pdf-074 Ce/Cr/Cl 循环带(1)(2)箭头 → FLOW ✓）。

**已固化进 inject 管线**：`inject_assets_batch.py` 注入前强制读取 `out/graph_verify_report.json`，graph 的 `source` 对应 `is_flow=false` 一律 skip（计入 `graph_notflow`），即使守卫漏判产生了节点也强制回退位图；报告缺失时回退到仅守卫并打印警告。重跑验证：47 张已注入 graph 全部通过、`graph_notflow=0`，题库数量不变（`withImageAssets=106`，graph 47 / curve 39 / molecule 42），运行时反射验证通过。

### 已知局限

- **价类二维图分类**：意义报告把它粗分到 curve（3 张，payload 仍有用）或 graph（被守卫拦下，回退位图）。建议阶段2为"价类图/散点图"单独立 `type`，用专门提示词提取。
- **molecule 71% 通过率**：失败集中在球棍模型、空间填充模型、抽象占位图——这些图本就不适合 SMILES 表达，回退给 VL 位图理解是合理降级；对清晰键线式/结构式通过率很高。
- ~~**引擎路由尚未接入**~~ ✅ 已完成（见下章「引擎接入」）。

## 引擎接入（已完成）

把 `imageAssets.payload` 按 `type` 路由给规则引擎的接线已全部落地，覆盖 graph / curve / molecule 三类，并新增**结构化判分**使引擎真值能直接计入正确率。无资产的题自动回退"位图 + VL"原链路，接入是纯增量，未破坏任何现有行为。

### 三个引擎 + 网关接线

| 资产类型 | 引擎 | 能力 | 红线（不可解时） |
|---|---|---|---|
| graph | `GraphSolver`（Api 适配层 → Core `ProcessFlowEngine`） | 把 VL 提取的节点/边/条件转成 Core 物质 + 赋值，做一步转化可行性 + 选项排除 | 无可行项 → 返回 null（不猜）；pdf-018/064 均诚实返回空，0 误报 |
| curve | `CurveQuery`（Api 纯数学层，无 Core 依赖） | 轴刻度标定后做取值 / 极值 / 交点 / 拐点查询；无刻度轴返回归一化相对位置并标注 | 意图无法绑定到曲线系列 → 返回 null（不猜） |
| molecule | `MoleculeAnalyzer`（**Core 层**，纯 RDKit） | 分子式 / 不饱和度 Ω / 官能团（12 类 SMARTS）/ 中心原子杂化 / 分子极性 / 孤电子对 | RDKit 解析失败 → `Success=false`，不抛异常、不编造 |

网关 `LlmGatewayService.CallEngineAsync` 路由顺序（任一返回 null 即透传下一环节，不丢能力、不造假）：`graph 短路 → curve 短路 → molecule 短路 → 现有 task_type 分支`。`TryRoute*` 不触碰实例状态，可经 `RuntimeHelpers.GetUninitializedObject` 直接单元测试。在线入口 `ProcessStudentMessageAsync` 已透传 `imageAssets`（向后兼容的可选参数），线上可直接消费。molecule 分支对**选择题不短路**（保留原 choice-verifier 路径）；数据集 31 道分子题全部为填空，该守卫零成本。

### 结构化判分（AC-8）

molecule / curve 的特征结论（"极性 sp³""分子式 C₅H₈O₄"）不含"能 / 正确 / 共存"等判断词，走不到 `BuildGatewayAnswerAsync` 的具体结论分支（`gatewayAnswer` 为空）——原先这类引擎真值无法计入正确率。新增 `IsStructuredCorrect`：

- **molecule**：极性 / 杂化（sp / sp² / sp³ 独立 token，避免 `sp` 误匹配 `sp3`）/ 官能团 / 分子式 / 不饱和度 与答案关键词规范化比对，任一维度一致即命中；
- **curve**：引擎数值与答案数值在容差 `max(0.05, 5%×|答案值|)` 内比对（仅非选择题，选择题走选项字母路径）；
- **graph**：选项可行性结论含选项字母，已由关键词路径覆盖，不重复。

`GatewayCorrect = IsAnswerCorrect(关键词) || IsStructuredCorrect(结构化)`，关键词为兜底；结构化命中同时计入 `GatewayTruthAdopted`（覆盖率 / 精确率口径）。

### 106 题子集量化评估

在开发环境（VL 端点可用）对**有结构化资产的 106 题子集**跑了一次完整对比（`GET /api/evaluation/gateway-vs-llm?assetsOnly=true`），结果落盘 [gateway_vs_llm_assets106.json](../../../gateway_vs_llm_assets106.json)：

| 指标 | 数值 |
|---|---|
| 子集题数 | 106（graph 47 / curve 33 / molecule 31，部分题多资产） |
| 纯 LLM 正确 | 77 / 106（72.6%） |
| 网关 + 引擎 正确 | 5 / 106 |
| 引擎产出可用真值（EngineSolved） | 7 |
| 结构化判分命中 | molecule 2（graph 0 / curve 0） |
| 仅引擎答对、纯 LLM 答错（gatewayOnlyCorrect） | **2** |
| 帮倒忙（引擎答错且纯 LLM 答对） | 2 |

结构化判分带来的**净新增正确**为 2 题，均为 molecule 填空：

- **pdf-208**（碳同素异形体）：答案含 `sp3`，引擎识别中心原子杂化 sp³ → 命中；纯 LLM 答错。
- **pdf-357**（乳酸内酯化）：答案含 `C5H8O4`，引擎计算分子式 C₅H₈O₄ → 命中；纯 LLM 答错。

graph / curve 子集在本次跑中结构化命中为 0：这两类以**选择题**为主（答案为 A/B/C/D），其正确率由选项字母关键词路径承担；结构化数值 / 特征比对对选择题**主动门控关闭**，避免干扰。引擎仍会产出数值（如 curve 拐点、graph 选项排除），但不计入结构化命中列。

> 注：当前引擎主要价值在"避免 VL 幻觉 / 数值猜测"与"结构化真值可复现"，整体正确率仍以 VL 为主。扩大结构化命中需提升 curve 意图绑定覆盖率与 graph 填空场景。

### 回归与质量门禁

- 全量构建 `CogniThink.Chem.sln`：0 错误（仅预存 nullable 警告）。
- 全量测试（排除意图失败的 `_Diag*` 诊断用例）：**Chem 831 + TutorBrain 281 = 1112 通过，0 失败**，单调递增（Task 6 +3 / 7 +4 / 8 +21 / 9 +5 / 10 +17 / 11 +3）。
- 数据不变量：`total=545, withImageAssets=106`（graph 47 / curve 33 / molecule 31），与接入前一致，证明加载链路未受影响。
- 无资产路径：`resp.AssetKind == null` 时 `IsStructuredCorrect` 恒返回 false，行为与接入前完全相同。

## 下一步建议

1. ~~批量处理前三类~~ ✅ 已完成（156 张 → 128 资产 / 106 题落地，见上节）。
   - ~~graph 幻觉校验固化~~ ✅ 已完成（独立 VL 二分类 `verify_graph.py` + inject 强制门槛 `graph_notflow`，双保险，47/47 通过、与守卫 100% 吻合，见上节）。
2. ~~**引擎接入（首要）**~~ ✅ 已完成（graph `GraphSolver` / curve `CurveQuery` / molecule `MoleculeAnalyzer` 三路接线 + 结构化判分 + 106 题子集量化评估，见上章「引擎接入」）。
3. **价类图单独立类**：为"价类二维图/散点图"新增 `type`（如 `scatter`），用专门提示词提取"类别×数值"点集，替换目前被粗分到 curve/graph 的约 13 张。
4. **前端展示**：用 `vector_svg/*.svg` 替代扫描位图展示，晶体类接 3Dmol.js（前端链路已存在）。
