# 攻硬骨头·数据修复（第二批）实施计划

> 状态：**已完成**。基线快照 `_evalscratch/snapshot_redox_stmt.json`：`gatewayCorrect=709`、`bothWrong=48`、`harmCount=0`、`enginePrecision=1`。任何改动均对基线（或上一批快照）做集合 diff，全程 `LOST=[]`、`harm=0`、`precision=1`。
>
> **本批执行结果（累计，相对基线）**：`GAINED=[pdf-255, pdf-307, pdf-325, pdf-336, pdf-498]`、`LOST=[]`、`HARM=[]`；`gatewayCorrect` 709→**714**（+5）、`bothWrong` 48→**43**（−5），`harmCount=0`、`enginePrecision=1`。
> - **pdf-498**：修键 D→B + 重写 note + 策展 GC-764（数据改动经用户批准）；eval `redox_stmt`→`hardbone498` GAIN +1。
> - **pdf-255**：键 C 本正确，缺陷为曲线图缺失致 null；读图 page_090 + 答案册 page_087 核 4-verdict，策展 GC-765；无数据改动；GAIN +1。
> - **pdf-325**：键 B 本正确，OCR 丢甲苯结构图致选项 B 文本荒谬；订正选项 B / answerKeywords / note（经用户批准）+ 策展 GC-766；GAIN +1。
> - **pdf-307**：键 D 本正确（选错误项）；独立电化学核验 + 策展 GC-767；无数据改动；GAIN +1。
> - **pdf-336**：键 A 本正确（选错误项）；读图 page_115 + VL 缓存动力学曲线核 4-verdict（A 假、B/C/D 真），策展 GC-768；无数据改动；GAIN +1。
> - **pdf-338**：⏭️ **已跳过（命题缺陷）**——官方 `answer/page_115.json` 标 C，但官方解析自相矛盾（其论证恰是选项 D 的表述却误标 D 错），按题给前提 C、D 双真，4 选项不能唯一判真；不策展、不改键，仅登记。
>
> **验证门**：`Batch3ChoiceValidatorTests` 271→**275** 全绿（`Concept_NoOverlap` / `Coverage` 通过）；全库 Chem **974/0** + TutorBrain **1826/0** 稳定，合计 **2800/0**。
>
> **环境发现**：`vl_test.py` 中的 qwen3-vl-plus key 返回 **HTTP 401**（已失效/吊销），VL API 当前不可用。本批全程以 **Read 工具直接读渲染页/裁剪图** 替代，5 道增益均因此闭环；`vl_cache3/pdf-336.json` 的动力学曲线解读（产物 A 先增后减=动力学产物）对 pdf-336 判 B 真起了关键辅助。

## 1. 背景与目标

上一轮 `general-choice-facts` 策展池已饱和：`unsolved_pool.py redox_stmt` 产出的 12 道未解干净单选，逐一独立核实后**无一可安全策展**（缺图、键冲突、选项截断、歧义两真、多选、畸形等）。用户选定方向：**攻硬骨头·数据修复**——从原书 PDF 恢复缺失的曲线/结构/选项文本与订正冲突答案键，让 Production 确定性 eval 真正涨分。

**本计划目标**：在 `precision=1 / harm=0 / LOST=[] / 不蒙数` 红线下，以原书渲染页 + 官方答案册为铁证，逐题修复高置信数据缺陷并把缺失信息**以文本回填**，再策展 Concept 事实让引擎端到端命中，实现 `bothWrong→gatewayOnlyCorrect` 的真实增益；每处数据改动先获用户明确批准。

## 2. 可行性研究结论（已实证）

| 闸门 | 结论 | 证据 |
|---|---|---|
| 原书 PDF 可渲染为图 | ✅ `pypdfium2` 在默认 python (3.12.10) 可用；`pdfs/_extract/render_pages.py` 已渲染 **116 页**，覆盖 12 候选中的 9 页（p46/p65/p89/p106/p107/p113/p115/p116/p139） | `_test_env.py`；`_probe_pages.py` |
| 渲染页可读 | ✅ Read 工具可直接读 `page_renders/page_NNN.png`；本计划已读 p065 确认题干/结构 | Read `page_065.png` |
| VL API 可达 | ✅ `qwen3-vl-plus` 端点网络连通（沙箱内即通）；`pdfs/_extract/vl_test.py` 提供识别配图 bbox / 读图调用范式 | `_test_vl_net.py` → HTTP 404（连通，仅根路径无路由） |
| 官方答案册可用 | ✅ `pdfs/_extract/answer/page_XXX.json` 共 154 页，含逐题 `answer` + `explanation`，是订正答案键的铁证 | 读 `answer/page_067.json` |
| 基线零漂移 | ✅ `snapshot_redox_stmt.json` 健在，709/48/0/1 | `_snap_keys.py` |
| 先例范式 | ✅ pdf-075（修选项 + 引擎）、pdf-319（修键 + 回填选项名）、pdf-337（挂结构图）、pdf-115/394（策展自解）均在 `data-defect-register.md` 留痕 | 登记册 C/D 类 |

**关键认知**：仅把图像挂到 `images` 字段只能让 pure-LLM 路径答对（Production 引擎不读图），**不涨 gwCorr**。要让 Production eval 涨分，必须把缺失信息**以文本恢复进 `question`/`options`/`note`**，再策展 Concept 事实或走引擎（pdf-075 / pdf-319 先例）。

## 3. 已验证的首个缺陷：pdf-498（铁证，本批 Tier 1）

**证据**：官方答案册 `pdfs/_extract/answer/page_067.json` 第 7 题：

```
answer: "B"
explanation: "Z有6个共价键且为短周期主族元素，推为S；Z与M同周期，M简单阳离子半径最小且带3个正电荷，
             推为Al；短周期含1、2、3周期，第一周期仅H、He两种元素，符合题意，故M为Al，选B。"
```

**题库存储（`pdf_questions.json`）现状**：
- `answer = "D"`，`answerKeywords = ["D", "同一周期中,元素第一电离能处在Z和M之间的元素有3种"]`
- `note = "[千问求解] … Z为Mg … D对（Z=Mg、M=Al，其间第一电离能…有Si、P、S共3种）"`

**冲突诊断**：
1. 存储 `question` 文本为「W、X、Y 在同一周期，Z、M 同处另一周期」——与原书一致；W/X/Y 为第二周期，Z/M 为第三周期。
2. 官方推 **Z=S**（6 共价键）、**M=Al**（第三周期简单阳离子半径最小且 +3）。`[千问求解]` note 猜 Z=Mg，**猜错**，进而误判 D 正确。
3. 以官方 Z=S、M=Al 独立判 4 选项：
   - **B**（简单离子半径 Z>W>X>M = S²⁻>…>Al³⁺）：真 → 官方答案。
   - **D**（Z 与 M 之间第一电离能的元素有 3 种）：第三周期 I₁ 序 Al<Si<S<P，介于 Al 与 S 之间的仅 Si（P 的 I₁ > S），= 1 种 ≠ 3 → **假**。故存储键 D 错误。
   - A、C 由官方选 B 唯一蕴含为假（执行期再独立核验，不能只靠「答案唯一反推」）。
4. 域为**元素周期律**（半径/电离能/含氧酸酸性），与 Experiment / Organic / Structure 验证器无重叠（执行期用 `screen_overlap` 实证确认）。

**修复内容（需用户批准）**：
- `pdf_questions.json` pdf-498：`answer` D→B；`answerKeywords` 同步为 `["B", "简单离子半径:Z>W>X>M"]`；`note` 重写为官方解析（Z=S、M=Al、B 对、A/C/D 错）。`question`/`options`/`images` 不动。
- 策展 4 条 Concept 事实（A 假/B 真/C 假/D 假），`stemMatch` 取全库唯一短语（如「M的简单阳离子半径是同周期中最小的」），追加到 `general-choice-facts.json` + `ConceptTruth`，下一 ID **GC-758** 起。
- 预期：`bothWrong→gatewayOnlyCorrect`，**GAINED=[pdf-498]**。

## 4. 候选分诊（执行期逐题验证，不预支结论）

> 下表「预期」仅是基于登记册与初步读页的**假设**，不构成承诺；执行期必须独立判 4-verdict + `screen_overlap`，不唯一或有重叠即跳过。

| id | 页 | 域 | 缺陷 | 执行期动作 | 前置门 |
|---|---|---|---|---|---|
| **pdf-498** | p65 | 周期律 | 键 D 错（官方 B）；note 错 | 修键+重写 note+策展（Tier 1，见 §3） | 重叠检查 |
| pdf-255 | p89 | 热化学/平衡 | 关键曲线图未入库（现图仅题干文字碎片） | 读 p89 + 答案册，恢复曲线结论为文本/事实；判 4 选项唯一 | 曲线可唯一判 + 重叠 |
| pdf-338 | p116 | 有机（亲电取代） | C/D 歧义两真 | 读原页确认 D 真伪；若 D 假则恢复+策展；若确为多选则转 MultiSelect 登记 | 重叠（有机域高风险） |
| pdf-325 | p113 | 化学用语/命名 | 键 B 与化学冲突；A 结构图未捕获 | 读原页恢复选项 B 真实文本 + A 结构；核键 | 重叠 |
| pdf-307 | p107 | 有机 | C/D 选项结构式截断 | VL 裁剪恢复 C/D 结构为文本；判 4 选项 | 重叠（有机域高风险） |
| pdf-336 | p115 | 有机 | A 产物结构 + 动力学曲线缺失 | 读原页恢复；判选项 | 重叠（有机域高风险） |

**本批暂不动**（登记在册，明确排除）：
- **pdf-138**（p46，实验装置图）：与 Experiment 验证器重叠 → 不在 Concept 策展范围。
- **pdf-303**（p106，题干明示「不定项选择」）：正确归为多选，不在单选策展范围。
- **pdf-540**（p139）：福建卷第(3)小问碎片，缺流程上下文。
- **pdf-263 / pdf-376 / pdf-382**：数据中 `images=[]`，无页码锚点，需先在原书定位（逐页 VL/文本检索），成本高，列下批。
- 其余 36 道 wrong（填空/计算/多选/判断）：非单选策展域，需结构化/自由文本答题能力，属引擎扩展批次，不在本计划。

## 5. 逐题工作流（获批后，每题独立闭环）

对每一道进入执行的题：

1. **取原页证据**：若 `page_renders/page_NNN.png` 已存在则直接 Read；否则 `python pdfs/_extract/render_pages.py 3 NNN` 渲染。必要时用 `vl_test.py` 范式裁剪配图或做 OCR 核对。
2. **取官方答案**：在 `pdfs/_extract/answer/page_*.json` 检索题干特征短语，取得官方 `answer` + `explanation`（铁证）。
3. **独立判 4-verdict**：不依赖存储 note（[千问求解] 已证明不可靠）；从原页 + 官方解析 + 化学推导独立判 A/B/C/D 真假。**4 选项不能唯一判真 → 跳过**（不蒙数）。
4. **重叠预检**：`screen_overlap`（或等价全库 grep）确认该题未被 Experiment / Organic / Structure 验证器命中；Concept 事实 `stemMatch` 全库唯一、无 stray-hit。
5. **用户批准数据改动**：把「改哪个字段、从什么改成什么、证据链接」列清，经用户明确同意后才 Edit。
6. **Edit 数据**：仅改批准的字段（`answer`/`answerKeywords`/`note`/`options`/`question`），保持 JSON 合法；不动其他题、不动已正确题。
7. **策展事实**（仅当修复后可由 Concept 唯一解）：Edit 追加 `general-choice-facts.json` + `choice-curated-facts.json` + `ConceptTruth`，4 空格单行缩进，末条加尾逗号。
8. **验证门**：JSON 校验 → stale-DLL 清理 → `dotnet build` → `Batch3ChoiceValidatorTests` 全绿（含 `Concept_NoOverlap`、`Coverage` 硬门）→ `dotnet test` 全库 0 failed → eval（Production）`tag=<n>` → 与 `snapshot_redox_stmt` diff：`LOST=[]`、`harm=0`、`precision=1`、`GAINED` 恰为本批修复集。
9. **登记**：在 `data-defect-register.md` 增补/更新该题记录（证据 + 修复 + eval 结果）。

## 6. 验证与红门（硬指标，不达标即回滚）

- 集合 diff（基线 `snapshot_redox_stmt` → 新快照）：
  - `correct_ids` = `bothCorrectCases` ∪ `gatewayOnlyCorrectCases`
  - `LOST` = 基线 − 新 必须为 `[]`
  - `GAINED` = 新 − 基线 必须恰等于本批修复的题 id
- `harmCount=0`、`enginePrecision=1`、已正确题 `gatewayAnswer` 零变动。
- `Batch3ChoiceValidatorTests`：`Concept_NoOverlap`（Concept 不得与 Experiment/Organic/Structure 断言同一题）与 `Coverage`（Concept 解集 == `ConceptTruth`）必须全绿。
- 任一门失败 → `git checkout` 回滚对应文件，排查后再试，绝不带病提交。

## 7. 风险与回滚

- **最大风险**：把正确键改成错键（污染真值）。缓释：答案键改动**一律以官方答案册 JSON 为铁证**（如 pdf-498 的 page_067.json），无官方源不动选择题键；每题独立判 4-verdict 确认唯一性。
- **有机域重叠**：Concept 事实与 Organic 验证器撞车 → `Concept_NoOverlap` 测试会拦下；执行期先用 `screen_overlap` 预检，重叠即弃。
- **曲线/结构文本化失真**：VL/Read 读图可能误读 → 交叉核对官方解析；无法唯一判则跳过，不蒙。
- **回滚**：仅改 `pdf_questions.json` / 两个事实 JSON / `ConceptTruth`，均 `git diff` 可逐条核对；出现 LOST 或红门失败即 `git checkout -- <file>` 回滚。
- **红线**：不碰判分逻辑、不碰引擎代码（除非另立引擎批次）、不碰已正确题、不主动 commit。

## 8. 范围外（明确不做）

- 不新增引擎 solver（若某硬骨头需新 solver，另立 spec 批次）。
- 不做填空/计算/多选/判断题的策展（非单选域）。
- 不恢复 36 道非单选 wrong（需结构化答题能力，另案）。
- 不动 `images` 之外的资产目录结构；不重新跑 `build_final.py`。
- 无官方证据的选择题答案键一律不动。
