# 剩余 144 题：引擎修复方案（刷新版，2026-11-21）

> **状态**：**已落地 P1+P2+P3（累计 GAIN +3，逐 id diff 验证零 LOSS/零 harm，precision=1）**；其中 P3 为 pdf-174 上下文恢复（见 §10）。本文件 §0–§9 为原方案，§10 为实际落地记录与证据。
> **本文件替代** `.trae/documents/remaining-151-image-equation-engine-plan.md`（151 方案的历史归因仍可参考，但其基线已过期）。
> **红线（贯穿全程，违反任一条即回滚）**：precision = 1、harmCount = 0、LOSS = 0；无唯一解时 solver 必须返回 `null`，不得编造；化学事实只能进 JSON/结构化资产，逻辑层不得硬编码答案；**改动 `answer` 键必须有官方答案册/原页溯源**；基线 `ie_crystal178` 不得回归。

---

## 0. 基线对账：为什么是 144，不是 147 / 151

旧方案（151）的基线是 `checkpoint_after_pdf485`：gatewayCorrect = **606**、bothWrong = **151**、harmCount = 0、precision = 1。

从 151 到此刻，仓库又落地了两批引擎修复（均已 eval 验证零 LOSS / 零 harm）：

| 批次 | 解出 id | GAIN | 依据 checkpoint |
|------|---------|------|----------------|
| 方程引擎（`ReactionKbWriterSolver` 落地，G 类纳入 3 道） | pdf-038, pdf-417, pdf-477 | **+3** | `after_pdf485` → `ie_final`（606→609，151→148） |
| 晶胞引擎（`CrystalCellValidator` + 资产，见 `.trae/specs/crystal-cell-validator/`） | pdf-170, pdf-178, pdf-206, pdf-222 | **+4** | `ie_final` → `ie_crystal178`（609→613，148→144） |
| **合计** | **7 题** | **+7** | 151 → **144** |

逐 id 正则 diff（`_evalscratch/current_unsolved_ie_crystal178.txt`，144 行；jsonl 行含重复键 `A`/`a`，用 `ConvertFrom-Json` 会抛错，必须 `-match` 正则提取，已规避）确认：
- `after_pdf485`→`ie_crystal178`：GAIN(false→true) = **7** = {pdf-170, pdf-417, pdf-178, pdf-038, pdf-206, pdf-477, pdf-222}；LOSS = **0**。
- `ie_final`→`ie_crystal178`：GAIN = **4** = {pdf-170, pdf-178, pdf-206, pdf-222}（正好晶胞 4 题）。

> **关于用户提到的"147"**：147 = 151 − 4（只计晶胞解出）。但方程引擎在 `after_pdf485`→`ie_final` 之间已额外解出 3 题（pdf-038/417/477），故**当前真实 bothWrong = 144**。本方案以真实当前状态 144 为基线；若用户希望严格按"151−4=147"口径，则需把已解的 3 道方程题从分母中扣除后再算——但这不改变剩余未解集合，剩余集合就是下面这 144 道。

**当前基线（锚定，所有 diff 都对照它）**：`checkpoint_ie_crystal178`
- total = 757，gatewayCorrect = **613**，bothWrong = **144**
- harmCount = **0**，precision = **1.000**，engineSolved = 613，coverage = 0.8098

---

## 1. 核心结论（先读这段）

1. **低垂果实已摘完**。151 方案设想的引擎可覆盖空间（晶胞 ~5 + 方程 3–8）已基本兑现：晶胞解出 4（170/178/206/222）、方程解出 3（038/417/477）。`ReactionKbWriterSolver` 与 `CrystalCellValidator` 两个新引擎均已上线并通过全量 eval + 独立复审。
2. **剩余 144 道的主体是数据缺陷 / 域外，不是引擎能力缺口**。`data-defect-register.md` 已把绝大多数归入 A/B/C/D/E/F/G 类并裁定"诚实 `null`"。在 precision=1 红线下，强行覆盖这些题要么缺官方答案键、要么缺题面文本/图像上下文、要么是有机/电化学推断域外——都会引入假阳性。
3. **仍有少量高置信候选**（需官方溯源 + 数据清理，不是纯引擎改动）：
   - **pdf-203**（NiO 晶胞 Ni²⁺ 个数）：题干干净、答案键错位成 `N>H>B>Fe`（同 pdf-170 模式）、当前无晶胞资产 → 复用 pdf-170 的 NiO 资产 + 修键（官方溯源）→ 高置信 +1。
   - **pdf-174**（MnO₂ 还原酸浸离子方程）：题干干净单问、答案键错位成 `3d¹⁰4s¹`（与 pdf-178 同大题拼接缺陷）、官方答案已知（page_060 num4 sub-2）→ 修键 + 方程引擎补模板/上下文 → 中置信 +1。
   - **pdf-204**（铁酸铋 N(Fe):N(Bi)）：答案键 `2:1` 正确无错位，缺晶胞资产 → 录入铁酸铋资产 + 求解器支持计数比 → 中置信 +1。
   - **pdf-044 / pdf-010 / pdf-532 / pdf-484**（G② 已登记的"题干完整方程题"）：需 KB 补全唯一命中 + 路由打通，且 pdf-010/044/084 的答案键是垃圾值（C/B/D）需先修键 → 低置信、高工作量，0–4。
4. **勘误（实现前核实纠正）**：初稿曾判断 pdf-203/204/205 被错挂"白锡"晶胞资产、需做 P0 清理——**经直接读取 `pdf_questions.json` 原文核实为误报**：白锡资产只属于紧邻的 pdf-206，初稿探针用固定 3200 字符窗口从 pdf-203 截取时溢出到 pdf-206，跨了对象边界造成归属污染。三题当前均**无** `imageAssets`（与 HEAD 一致），文件本身正确，**无需清理、无 P0 批次**。教训：跨对象提取必须按对象边界截断（对应经验：主键/边界稳定性），不能用固定长度窗口。
5. **现实预期**：在不放宽红线的前提下，剩余可争取 GAIN ≈ **1–6**；其中高置信、可快速落地的仅 pdf-203（+1）。其余约 **138+ 道应维持诚实 `null`** 并补登/完善缺陷登记册，等数据管道（原页 OCR / 图像资产抽取 / 题面拆分）接续。

---

## 2. 144 题全景归因（刷新）

归因以 `data-defect-register.md`（已详细登记）为骨架，结合本次对当前 144 未解集合的逐题探针复核。下表是**高层分布**（精确到类，个位数可能随修键微调）：

| 类别 | 登记册章节 | 大致量级 | 引擎可覆盖？ | 本轮处置 |
|------|-----------|---------|-------------|---------|
| A.2 答案键垃圾/错配（无官方源） | A.2 | ~6 | 否（键不准则无可判正输出） | 维持 null；等原页核对 |
| B 组合/计数题子项原文缺失 | B | ~4（已回填 4 道仍 null，策展自解另计） | 需原文回填 + 策展 | 已处理，维持 |
| C 依赖图像/原页（含晶胞未覆盖） | C | ~20 | 晶胞子集可覆盖（见 §4）；其余图像域外 | 晶胞候选进 §4；其余等图像管道 |
| D note/选项损坏但键正确 | D | ~4 | 否（文本损坏致无法对齐） | 维持 null；等选项补全 WIP |
| E 题干拼接 / 大题碎片 | E | ~10 | 否（文本不可恢复） | 维持 null；等原页拆分 |
| F 路由缺口 + 事实缺失（已策展自解 2） | F | 0（已解 2） | 已解 | 完成 |
| G 方程书写题（已纳入 3，其余排除） | G | ~27（其中 3 已解） | 见 §3 | 候选进 §3；其余维持 null |
| 多选 / 组合正确项（需专用校验器） | — | ~15 | 现有校验器已就绪，缺子项文本/资产 | 等数据 |
| 元素推断 / 结构推理（非机械） | — | ~30 | 域外（需推理/图像） | 维持 null |
| 其他（计算 / 平衡 / 碎片无锚点） | — | ~28 | 多数域外 | 维持 null |

> 精确逐题清单见 `_evalscratch/current_unsolved_ie_crystal178.txt`（144 个 id，纯列表）；分类以 `data-defect-register.md` 为准。本方案不重复抄录 144 道，只聚焦"还能动"的子集。

### 2.1 勘误：无错挂晶胞资产（初稿窗口提取误报，已核实纠正）

初稿 §2.1 称 pdf-203/204/205 被错挂"白锡"晶胞资产、需做 P0 清理。**经直接读取 `pdf_questions.json` 原文核实为误报**：

- 白锡资产（`pdf_images/p073_n07_2.png`）只属于紧邻的 **pdf-206**（文件 L5939 起），与 pdf-203/204/205 无关。
- 三题对象块内均**无** `imageAssets` 字段：pdf-203 以 `images:[p072_n04_4.png]`、pdf-204 以 `[p073_n05_0.png]`、pdf-205 以 `[p073_n06_1.png]` 结尾。
- `git show HEAD` 亦确认提交版三题无 imageAssets，与工作树一致——**文件本身正确，无需清理，无 P0 数据清理批次**。

误报根因：探针用固定 3200 字符窗口从 `"id":"pdf-203"` 向后截取，窗口溢出到 pdf-204/205/206，把 pdf-206 的资产归给了 pdf-203。教训：跨对象提取必须按对象边界（匹配的闭合 `}`）截断，不能用固定长度窗口，避免主键/归属污染（对应经验：join-key/边界稳定性）。

---

## 3. 方程引擎：现状与剩余空间

### 3.1 已落地（不再重复建设）

- `src/CogniThink.TutorBrain.Api/Application/Gateway/ReactionKbWriterSolver.cs` 已存在并接入 `LlmGatewayService`（`IEquationWriter` 多写器聚合）。
- 能力：确定型离子模板（中和 / 酸+碳酸盐 / 酸+碳酸氢盐 / 碱+碳酸氢盐 / 碱式碳酸盐+酸 / 电离）+ KB 唯一命中检索（离子 `KbLookupIonic` / 分子 `KbLookupMolecular`）。
- 已解 3 道（pdf-038 电离+碱式碳酸+碳酸氢根、pdf-417 中和+酸碳酸根、pdf-477），`ie_final` 已含此 +3。
- 可靠性加固（记号归一化 `==`/`→`/`⇌`→`=`）已落地，但探针证伪"纯靠归一化即可解锁"——当前无题纯粹被记号阻挡（求解器不产出，无输出可归一化）。

### 3.2 剩余方程候选（按置信度排序）

依据 `data-defect-register.md` G 类逐条裁定 + 本次题面探针：

| id | 题干探针（关键事实） | 当前 answer | 阻塞根因 | 解锁条件 | 置信/预期 |
|----|---------------------|------------|---------|---------|----------|
| **pdf-174** | 干净单问："还原酸浸时 MnO₂ 反应的离子方程式" | `3d¹⁰4s¹` ❌（错位，与 pdf-178 同大题拼接缺陷） | ① answer 键错位；② 反应物 SO₃²⁻ 在未捕获的大题上下文中，KB 0 命中 | 修键（官方溯源 page_060 num4 sub-2：`MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O`）+ 补大题上下文/锚点 或 加确定型模板 | 中，+1（需上下文恢复） |
| **pdf-044** | 完整题干，3 子问方程（KHSO₄+Ba(OH)₂ 至中性 / FeBr₂+Cl₂ 少量 / 银氨+HCl） | `B` ❌（垃圾键） | ① answer 键垃圾；② 路由=`calculation` 未触达引擎；③ KB 无唯一命中 | 修键（官方溯源）+ 路由打通 + KB 补 3 条唯一命中 | 低，0–1（3 子问使唯一命中守卫易失败） |
| **pdf-010** | Fe₂O₃+HCl 方程 + FeCl₃ 水解制胶体（多子问） | `C` ❌（垃圾键） | ① 键垃圾；② 一题 3 方程 + KB 不覆盖 FeCl₃ 水解胶体 | 修键 + KB 补全 | 低，0–1 |
| **pdf-532** | 同时含"化学方程式"与"离子方程式" | — | `askIonic && askMolecular` 歧义守卫直接 null；KB 无 Cl₂+CaBr₂ / I⁻+MnO₄⁻ | 拆问法判定 + KB 补全 | 低，0–1 |
| **pdf-484** | 2 个方程（Co²⁺+MnO₂ / Co³⁺+Cl⁻），路由=`calculation` 未触达 | — | 路由 + KB | 同 pdf-044 | 低，0–1 |
| pdf-084 | 回收催化剂方程（CO+Co₂O₃→Co₂(CO)₈） | `D` ❌（垃圾键） | 键垃圾 + KB 无 | 修键 + KB | 低，0–1 |
| pdf-133 / 481 | 多段复合答案中的子方程 | 复合串 | 子串定位 + 复合答案匹配风险 | 留 null（红线风险高） | 0 |
| pdf-304 / 313 | 电极反应 / 电化学推断 | 复合串 | 域外推理 + 单空格不可子串定位 | 留 null | 0 |
| pdf-354/355/356/526/527 | 酯化/聚合（有机产物选择性推断） | 有机产物 | 域外，非确定型模板可唯一覆盖 | 留 null | 0 |

**方程侧预期**：高置信 0；若投入 KB 补全 + 路由 + 修键，可争取 pdf-174（+1）及 pdf-044/010/532/484/084 中的 0–3 道，但工作量大、每道都要官方溯源 + 全库唯一命中校验，性价比低。**建议方程侧只做 pdf-174（与 pdf-178 同大题，溯源现成），其余列入"等数据管道"。**

---

## 4. 晶胞引擎：现状与剩余空间

### 4.1 已落地（4 题，不再重复）

pdf-170（NiO）、pdf-178（Mn₃Sb）、pdf-206（白锡/灰锡）、pdf-222（白磷 P₄）。`CrystalCellValidator` 支持均摊计数、配位数（含"周围+最近"意图扩宽）、密度（含多物种 `formulaUnits`）。

### 4.2 剩余晶胞候选

探针复核当前 144 中仍带"晶胞/晶体"关键词的题：

| id | 题干探针 | 当前 answer | 资产现状 | 阻塞 | 解锁条件 | 置信/预期 |
|----|---------|------------|---------|------|---------|----------|
| **pdf-203** | "NiO 晶胞如图，晶胞中含有 Ni²⁺ 的个数为 __"（干净单问） | `N>H>B>Fe` ❌（电负性排序，明显错位，同 pdf-170 模式） | 无晶胞资产（需新增） | ① 键错位；② 缺 NiO 晶胞资产 | 复用 pdf-170 的 NiO 资产（Ni 顶点×8+面心×6）→ 修键为 `4`（官方溯源）→ 求解器已能产 `4` | **高，+1** |
| **pdf-204** | "铁酸铋晶胞如图（4 个 Fe 在体对角线，O 未画），N(Fe):N(Bi)=__" | `2:1` ✅（键正确！） | 无晶胞资产（需新增） | 缺铁酸铋资产；求解器需支持"原子数目比" | 录入铁酸铋资产（Fe 体对角线位置 + Bi 位置，坐标源自 note 核算：Fe=8、Bi=4）→ 求解器输出比值并命中 `2:1` | 中，+1（资产录入是关键路径） |
| **pdf-205** | "AlₓCoᵧᵧ 立方晶胞：Co 位置 + 一个 Al 周围最近 O 的个数"（双问） | `Al` ❌（错位） | 无晶胞资产（需新增） | ① 键错位；② 缺 AlₓCoᵧᵧ 资产；③ 双问（位置文本+配位数），输出复合 | 录入 AlₓCoᵧᵧ 资产 → 修键 → 求解器产"位置；CN" | 低–中，0–1（双问复合匹配风险） |
| pdf-215 | CO₂ 晶体甲/乙：作用力 + 熔点比较 | 复合 | 无正确资产 | 非纯计数（需化学推理） | 留 null | 0 |
| pdf-218 / 220 | 晶胞选择题"下列说法错误的是" | 多选字母 | 无 | 选择型晶胞题需推理，非机械计数 | 留 null（或等多选路线） | 0 |
| pdf-498 / 511 | 化合物结构图推元素 | 单选字母 | 无 | 元素推断域外 | 留 null | 0 |

**晶胞侧预期**：高置信 pdf-203（+1）；中置信 pdf-204（+1，资产抽取为关键路径）；pdf-205 待定（0–1）。

---

## 5. 优先级 / 批次 / 预期 GAIN 与风险

| 批次 | 内容 | 预期 GAIN | 风险 | 前置条件 |
|------|------|----------|------|---------|
| **P1 pdf-203（高置信快速赢）** | 复用 NiO 资产 + 修键 `N>H>B>Fe`→`4`（官方溯源）+ 单测 + eval diff | **+1** | 低（完全复刻 pdf-170 已验证模式） | 找到 pdf-203 的官方答案溯源 |
| **P2 pdf-204（资产抽取）** | 抽取铁酸铋晶胞资产 + 求解器支持原子数目比输出 + eval diff | **+1** | 中（资产坐标抽取准确性） | 渲染图可抽取坐标；官方 `2:1` 已确认 |
| **P3 方程 pdf-174** | 修键（page_060 溯源现成）+ 补 MnO₂ 还原模板/上下文 + eval diff | **+1** | 中（需恢复大题上下文或加锚点） | 官方溯源（已有） |
| **P4 方程 KB 补全（可选，低性价比）** | pdf-044/010/532/484/084：修键 + 路由 + KB 唯一命中 | 0–3 | 高（每道需官方源 + 全库唯一命中校验，易触发红线） | 每题官方溯源 |
| **P5 登记册补登** | 把本轮确认的缺陷（pdf-203/204/205 键错位归属、资产缺失等）补登 `data-defect-register.md` | 0 | 无 | 无 |

**合计预期 GAIN（P1+P2+P3）= +3，上限（含 P4）≈ +6**。P5 是零 GAIN 的质量守卫，建议做。

---

## 6. 红线守卫与回归验证（每个批次强制执行）

1. **修键前**：必须有 `pdfs/_extract/answer/page_*.json` 或原页 OCR 官方溯源；无溯源不改 `answer`。化学推导仅作辅助，不作改键依据（沿用登记册处理原则 #2）。
2. **每批改动后**：`dotnet build-server shutdown` → rebuild Release → 全量 `dotnet test`（0 失败）→ `dotnet test --filter FullyQualifiedName~CrystalCell` 与 `~Reaction` 绿色。
3. **EvalRunner**：用**新 tag**（不复用旧 tag，避免 resume 跳过产生误导）跑全量 757；逐 id 正则 diff 对照 `ie_crystal178`：要求 **LOSS=0、harmCount=0、precision=1**；GAIN 仅来自本批目标 id，其余 756−N 行不变。
4. **零回归护栏**：`CrystalCellFullBankGuardTests` 断言每个非 null 晶胞输出都 `IsAnswerCorrect=true`（防错挂资产产生假阳性）；方程侧同理。
5. **资产一致性**：每张录入资产坐标必须与 `pdf_images/*.png` 渲染图目视核对（沿用晶胞 spec AC-6）。

---

## 7. 与既有 spec / 计划的关系

- **supersedes**：`.trae/documents/remaining-151-image-equation-engine-plan.md`（基线已过期；其 §2 详细归因仍可作为历史参考）。
- **已完成**：`.trae/specs/crystal-cell-validator/`（GAIN +4，已独立复审 PASS）。
- **在途**：`.trae/specs/image-equation-engine-151/`、`.trae/specs/remaining-148-reliability-engine/`、`.trae/specs/hard-bones-coverage-push/`——本方案不与它们抢写同一文件；若进入实现，P1/P2/P3 应单独立项或并入最相关 spec，避免与在途工作的工作树改动冲突（当前工作树已有大量未提交改动，实现前需先理清各 spec 的提交边界）。
- **登记册**：`data-defect-register.md` 是归因真相源，本方案的分类与其对齐；P5 只做增量补登，不重写。

---

## 8. 诚实 `null` 总原则（防回退到硬编码）

剩余 ~138+ 道维持 `null` 的理由，按优先级：
1. 无官方答案键 / 键垃圾（A 类）——改键需官方溯源，溯源不到则不动。
2. 题面文本缺失（子项、选项、大题上下文）——文本轨道无法判，等原页 OCR / 数据管道。
3. 依赖图像（晶胞坐标、装置图、表格、结构图）——等图像资产抽取；晶胞子集已在 §4 列候选。
4. 域外推理（有机产物选择性、电化学、元素推断、选择型晶胞）——非确定型机械计算，强行覆盖必引入假阳性。
5. 多子问复合答案 + 单空格不可子串定位——匹配风险高于收益。

**绝不**：逐题硬编码答案、放宽唯一命中守卫、基于低质量 OCR 猜测回填、向逻辑层注入化学事实。

---

## 9. 开放问题（需用户裁定）

1. **口径确认**：本方案以真实当前 bothWrong=144 为基线。若用户坚持"147"口径（151−4 晶胞），剩余集合不变，仅分母表述不同——是否接受以 144 为准？
2. **实现范围**：是只做 P1（pdf-203 高置信 +1），还是一并推进 P2/P3（pdf-204 资产录入、pdf-174 方程，再 +2）？P4（方程 KB 补全）性价比低，是否列入？
3. **工作树冲突**：当前工作树有多个在途 spec 的未提交改动（含 `pdf_questions.json` 99 行改动）。进入实现前是否需要先提交/暂存现有改动，避免 P1/P2 的 diff 与在途工作混在一起？
4. **官方溯源获取**：pdf-203 的官方答案册页未知（pdf-170 用 page_059 num2，但是不同题）。是否允许先跑探针确认 NiO 资产产"4"，再补官方溯源后修键？还是必须溯源先到位才动？

---

## 10. 实施结果（已落地，2026-11-21）

> §9 开放问题已在实现中裁定：范围=P1+P2；口径以真实 144 为准；pdf-203 官方溯源落实为 page_059 num 2（与 pdf-170 同一 NiO 计数问法），溯源先到位再修键；工作树未提交（按红线不主动 commit）。

### 实际产出

| 批次 | id | 内容 | GAIN | eval diff 证据 | 红线 |
|------|----|------|------|----------------|------|
| P1 | pdf-203 | 核实题面错位：真实题面=NiO 晶胞 Ni²⁺ 个数（exercise/page_072 num 4），原 answer `N>H>B>Fe` 实为同页 **num 3** 电负性选择题选项 D 文本；复用 NiO 岩盐资产（Ni 顶点×8+面心×6，源 `p072_n04_4.png`）+ 修键为 `4`（官方溯源 `answer/page_059.json` num 2，与 pdf-170 同一问法） | +1 | `ie_crystal178`→`pdf203fix`：GAIN 1={pdf-203}，LOSS 0，other 0；gatewayCorrect 613→614 | precision=1, harm=0, LOSS=0 ✓ |
| P2 | pdf-204 | 录入铁酸铋 crystal 资产（Fe 体内×4+面心×8，Bi 面心×8，源 `p073_n05_0.png`，坐标源自官方解析 `answer/page_075.json` num 5）；扩展 `CrystalCellValidator` 新增「原子数目比」路径（从 `N(X):N(Y)` 记法结构化提取物种 → 最简整数比），产出 `2:1`。answer 键原已正确，**未改键** | +1 | `pdf203fix`→`pdf204fix`：GAIN 1={pdf-204}，LOSS 0；全库 guard 验证新路径无伪命中；`ie_crystal178`→`pdf204fix` 累计 GAIN 2={pdf-203, pdf-204} | precision=1, harm=0, LOSS=0 ✓ |
| P3 | pdf-174 | 上下文恢复：回填大题前言（习题册 `page_057` 孤儿表头 + 第 58 页 OCR 工艺标签 `H2SO4/Na2SO3`，官方溯源 `answer/page_060` num 4）到 `pdf_questions.json` 的 `question` 字段（仅改 question，545 对象数不变）；`ReactionKbWriterSolver` 新增 `HasSulfite` + 确定型模板 `ManganeseDioxideSulfiteIonic`。answer 键**未改** | +1 | `checkpoint_pdf204fix`→`pdf174fix`：P3 所得 GAIN={pdf-174}（同 diff 另含 pdf-304/313 为并发 T8 `HalfReactionBalancer` 所得，非 P3）；LOSS 0；gatewayCorrect 615→618 | precision=1, harm=0, LOSS=0 ✓ |
| **合计** | | | **+3** | gatewayCorrect 613→**618**，bothWrong 144→**139** | 全量测试 2139 pass / 0 fail ✓ |

### P3 已落地（pdf-174，上下文恢复 + 确定型模板）

- **answer 键**已由其他在途 spec 修正为 `MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O`（官方溯源 `answer/page_060.json` num 4 sub-2，登记册 **G①** 已记录），本批**未改键**。
- **原阻塞**：题干"还原酸浸时 MnO₂ 反应的离子方程式"未含反应物 **SO₃²⁻**——该上下文在未捕获的大题前言中 → `ReactionKbWriterSolver` KB 0 命中。
- **上下文恢复（官方溯源）**：从两处官方源拼回大题前言，回填 `pdf_questions.json` 的 pdf-174 `question` 字段（仅改 question，545 对象数不变、JSON 合法）：① 习题册抽取孤儿表头 `pdfs/_extract/exercise/page_057.json` 末条（type `"other"`，管道丢弃的大题引入句）：`4.（2025·云南卷·★★★）从褐铁矿型金-银矿(含 Au、Ag、Fe2O3、MnO2、CuO、SiO2 等)中提取 Au、Ag，并回收其他有价金属的一种工艺如下：`；② 习题册 PDF 第 58 页 OCR（`src/CogniThink.TutorBrain.PdfOcrRepair`，Tesseract）恢复工艺流程图试剂标签 `H2SO4、Na2SO3`（Tesseract 噪声 `z`↔`2`、`s`↔`3`，已与 `answer/page_060.json` num 4 核对：解析明确 MnO₂ 氧化 SO₃²⁻→SO₄²⁻，第(4)问提及 Na₂SO₃）。回填后题干同时含 MnO₂ + 亚硫酸盐 + 酸三个锚点。
- **确定型模板**（`ReactionKbWriterSolver`）：新增 `HasSulfite` 判定 + `ManganeseDioxideSulfiteIonic` 离子模板，当文本同时含 MnO₂ + 亚硫酸盐 + 酸时产出 `MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O`，否则返回 `null`。**选模板而非 KB 条目**：`KbLookupIonic` 要求反应物字面在题面；若放宽 `H⁺→HasAcid` 会与既有 `RX-IN-ION-023`（SO₃²⁻+2H⁺→SO₂）形成**多命中**→唯一命中守卫返回 `null`。模板是 pdf-038/417/477 已验证的触发机制，爆炸半径最小；化学事实不硬编码在逻辑层（模板仅做确定型锚定，官方溯源见上）。
- **纠正原 deferral 理由**：原结论"方程侧无全库 guard"**已过时**——`FullBankGuard_EveryNonNullOutput_IsAnswerCorrect` 存在并迭代全库断言每个非 null 输出必判正，是零伪命中预言机。
- **验证**：`dotnet build` 0 err / 0 warn；`dotnet test` 全量 **2139 pass / 0 fail**（TutorBrain 1228 + Chem 911）；新增单测覆盖正例（锚点齐→产出方程）与负例（缺任一锚点→`null`）；全库 guard `fires>=4` 全绿。eval 新 tag `pdf174fix` vs 基线 `checkpoint_pdf204fix`：逐 id 正则 diff **GAIN={pdf-174, pdf-304, pdf-313}，LOSS=0，other=0**，gatewayCorrect 615→618。其中 **pdf-174 为 P3 本批所得**（`gatewayAnswer` 由空→`离子方程式：MnO2 + SO3^2- + 2H+ = ...`）；**pdf-304 / pdf-313 非 P3 所得**——其 `taskType` 为 `concept_fact`、输出为半反应，由并发在途的 `HalfReactionBalancer` spec 落地（见登记册 G-⑨ / §T8），本求解器对 `concept_fact` 返回 `null`，物理上不可能触发它们。快照 `snapshot_pdf174fix.json`：gatewayCorrect=618，enginePrecision=1，harmCount=0，bothWrong 142→139。**全部红线成立**。

### P5 登记册补登（已做）

- `data-defect-register.md`：A.1 表新增 pdf-203（键订正+资产，GAIN +1），"改键同时解出"例数 3→4；C 类表新增 pdf-203、pdf-204（✅ 已解）。

### 可复现验证命令

```
dotnet build-server shutdown; dotnet build CogniThink.Chem.sln -c Release --nologo -v q   # 0 err / 0 warn
dotnet test CogniThink.Chem.sln -c Release --no-build --nologo                              # 2127 pass / 0 fail
dotnet test --filter "FullyQualifiedName~CrystalCell"                                        # 22/22（含 pdf-203→4、pdf-204→2:1）
dotnet run --project src/CogniThink.TutorBrain.EvalRunner -c Release --no-build -- --tag pdf204fix --concurrency 24 --batch-size 100
```

逐 id diff：对 `_evalscratch/checkpoint_ie_crystal178.jsonl` 与 `checkpoint_pdf204fix.jsonl` 用正则提取 `id`/`gatewayCorrect`（jsonl 含重复键 `A`/`a`，禁用 `ConvertFrom-Json`），确认仅 {pdf-203, pdf-204} false→true，其余 755 行不变。

> 注：build 期间曾遇在途 WIP 文件 `HalfReactionBalancer.cs`（未跟踪，非本批引入）编译错误，由并发修复后恢复；本批未触碰该文件，且其未接入网关（grep 全仓仅自身引用），不影响运行时与 eval 结果。
