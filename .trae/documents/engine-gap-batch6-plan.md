# 引擎补强计划（engine-gap-batch6）——窄补强国境耗尽审计与架构转向

> 状态：**执行完成**。基线 `b5part2`：gwCorr=**669/757**（88.4%），precision=1，harm=0，LOST=0，mp27=27/27。
> 本批定位：对 batch5 之后的 leads 池做二次精筛，判定是否还存在可在红线内安全拿下的窄 solver/fact 增益；若枯竭，则给出架构转向的决策依据。
> **执行结果（2026-11-26）**：登记册已追加 Y 类；基线锁定快照 `checkpoint_b6baseline` 与 `b5part2` diff **GAINED=0 / LOST=0**，`verify_mp27` **27/27 PASS**，gwCorr 669=669、precision=1、harm=0 —— 本批零生产改动，无漂移。
> 红线（与 batch5 一致，不可破）：`precision=1`、`harm=0`、引擎层 `LOST=0`、`mp27=27/27` 不退化；不臆造真值、不动 `images`、不改引擎判分逻辑；每项改动先全库零假阳性扫描，无法零 FP 即剔除。
> Plan Mode 约束：获批前只写本计划文档 + `_evalscratch/` 证据，不动 `src/`、`data/`、`tests/` 生产内容。

---

## 1. 现状与基线

| 指标 | 基线 `b5part2`（Production，LLM-off 纯引擎） |
|---|---|
| gatewayCorrect | **669 / 757** |
| precision | 1 |
| harm | 0 |
| 引擎 LOST | 0 |
| mp27 | 27 / 27 |
| pureLlm 缓存（`checkpoint_gap_datafix1`，LLM-on） | pureLlmCorrect = 633 |

两快照问题集一致（common=757，题干未变），故 pureLlm 缓存仍有效，可作为「LLM 能从文本解出」的参照系。

**leads 定义**：`pureLlmOnly`（LLM 对、引擎错）∪ `bothWrong`（都错）。这是引擎可补强的唯一水面（`bothCorrect` 588 + `engineOnly` 81 已被引擎覆盖，无增益空间）。

证据工件：[_evalscratch/leads_b6.txt](file:///d:/github/CogniThink-Chem/_evalscratch/leads_b6.txt)（由 [analyze_leads_b6.py](file:///d:/github/CogniThink-Chem/_evalscratch/analyze_leads_b6.py) 生成）。

---

## 2. 待补强分布漏斗

### 2.1 leads 总览（88 题）

| 类别 | 数量 |
|---|---|
| bothCorrect | 588 |
| engineOnly | 81 |
| **pureLlmOnly** | **45** |
| **bothWrong** | **43** |
| **leads 合计** | **88** |

按 taskType 分布：choice 38、unknown 21、equilibrium 11、equation_writer 9、calculation 7、judgment 1、reaction_check 1。

### 2.2 初筛（排除已覆盖/已延期/图像/选择）→ 23 候选

剔除集合（与既有登记一致，不重复劳动）：
- `B5_SOLVED` = {pdf-117, 172, 177, 179, 324, 483}（batch5 已解）
- `X_DEFERRED` = {pdf-164, 171, 462, 541}（子问上下文剥离 / 护栏冲突）
- `W_DEFERRED` = {pdf-084, 190, 263, 376, 197, 306, 160}（数据修复批次延期）
- 含 `images` 的题（图像依赖）
- 含 `options` 或 `type∈{choice,single}` 的题（选择域，转 chem-fact-curation 轨，非引擎 gwCorr）

→ 候选池剩 **23 题**。

### 2.3 二次精筛（全库 757 题零 FP 扫描）→ 0 幸存

对 23 候选逐一判读（证据：[classify_leads_b6.txt](file:///d:/github/CogniThink-Chem/_evalscratch/classify_leads_b6.txt)、[bank_dump_b6.txt](file:///d:/github/CogniThink-Chem/_evalscratch/bank_dump_b6.txt)、[scan_fp_risk_b6_full.py](file:///d:/github/CogniThink-Chem/_evalscratch/scan_fp_risk_b6_full.py)）。判读维度：子问数（MultiPart AND 门）、是否真无图、答案洁净度（单字母/多子问串/长文）、引擎阻塞态、全库唯一命中（零 FP）。

**关键全库扫描证据**（universe = 完整 757 题 checkpoint，非仅 pdf_questions.json 的 545 条）：

| 探针短语 | 命中数 | 命中 id | 证明 |
|---|---|---|---|
| `中官能团名称为` | **4** | pdf-322 / 323 / 519 / 523 | 同题干前缀，4 题答案**各不相同**（醚键/酰胺基 vs 氨基/羧基 vs 氨基/硫醚 vs 羟基/异丙醇）→ 结构图被剥离，**无法用通用 fact 键控**（会跨题碰撞） |
| `中含氧官能团` | 1 | pdf-521 | 单命中但答案「羟基、羧基」依赖缺失结构，无锚点 |
| `调 pH` | 3 | pdf-168 / 463 / 464 | pdf-464 是子问 (6) 碎片，母题流程上下文缺失，不能唯一定位 |
| `焙烧` | 5 | pdf-163 / 168 / 170 / 177 / 203 | pdf-163 子问 (4) 母题上下文跨多题，无唯一锚点 |
| `曲美托嗪` | 1 | pdf-519 | 唯一命中，但答案键「氨基、硫醚」与真实曲美他嗪（C₁₄H₂₂N₂O₃，**不含硫**）矛盾 → **答案键本身存疑**（已登记 U.2） |
| `量子点` | 4 | pdf-030 / 533 / 534 / 537 | pdf-537 自由文本答案无唯一锚点 |
| `25.00%` | 1 | pdf-030 | 题干用变量 `a g`/`b mL` 要求「列计算式不化简」，答案却是固定数字 → 键题不对齐（已登记 K.2） |

**23 候选逐题归属**（全部已在前序登记册 K/M/U/E/W/X 类留痕，本批不重复登记题级细节，仅做聚合结论）：

| 排除根因 | 题 id | 数量 |
|---|---|---|
| 多子问 AND 门（含不可约自由文本/缺陷子问，无一「只差 1 子问」） | pdf-207, 010, 097, 166, 484, 176, 198 | 7 |
| 选择/裸字母键污染（答案为单字母，无/残选项） | pdf-070, 180, 333, 377 | 4 |
| 缺结构图（官能团题，结构被剥离；`vector_svg/` 无对应分子图） | pdf-322, 323, 519, 520, 521, 523 | 6 |
| 子问上下文被剥离（母题流程缺失） | pdf-464 | 1 |
| 自由文本推理（无唯一确定结论） | pdf-163, 537 | 2 |
| 答案键与题干不对齐/跨题污染（数据缺陷） | pdf-030, 161, 165, 198(子2答案"C") | 3（pdf-198 与多子问类交叉，不重复计数） |

> pdf-198 同时属「多子问 AND 门」与「子问答案缺陷」：子问 (1)「离子晶体/分子晶体」是干净概念，但子问 (2) 答案为裸字母 `C`（原题选项已在 OCR 中丢失），且全题唯一 answerKeyword 是整串 `(1)离子晶体,分子晶体 (2)C` → 引擎无法在不迎合缺陷键的前提下判正。即使加晶体类型事实，AND 门仍被子问 (2) 阻塞。

**幸存判定**：0 个「单题 · 无图 · 答案洁净短文本 · 全库唯一可零 FP 键控」的候选。→ **无引擎探针对象，r3 自然完成（空集）。**

---

## 3. 结论：窄 solver/fact 补强国境已耗尽

batch5 摘走了最后一批「单题无图、确定答案、引擎漏分类/缺事实」的果子（+6）。本批对剩余 88 leads 的二次精筛证明：

1. **没有新的单题安全候选**：23 个初筛候选全部命中既有 NO-GO 类别（多子问 AND 门 / 缺结构图 / 上下文剥离 / 自由文本 / 数据缺陷），且全库 757 题扫描确认无法零 FP 键控。
2. **配平器不是瓶颈**（继承 batch5 X.3 实测）：`EquationBalancer` 对 pdf-164/171/462 方程均配出正确系数，阻塞是反应物上下文被剥离 → 属架构缺口，非窄 solver 可解。
3. **官能团 cluster 是数据缺陷，不是引擎缺口**：6 题共享被剥离的结构图，无 molecule 资产；pdf-519 答案键还与真实化学矛盾，迎合错键会引入 harm（登记册 U.2 已裁定不臆造）。

因此，**在不破坏红线的前提下，继续逐题加窄 solver/fact 的预期增益 ≈ 0**。继续硬做只会：要么 0 收益，要么为凑数放宽守卫 → 引入假阳性 → 违反 precision=1。

---

## 4. 架构转向：两条候选轨道（batch7+，需各自独立 spec）

窄增益枯竭后，真实杠杆在两个架构性方向。下表对比供决策：

| 维度 | 轨道 1：多子问母题上下文恢复 | 轨道 2：图像 / 结构 ingest |
|---|---|---|
| 解锁对象 | X_DEFERRED（pdf-164/171/462）+ pdf-464 + K.1 多子问簇（27 题）中近失题 | B/C/U 类图像依赖题（~51）+ 6 道官能团缺结构题 |
| 已验证的近端收益 | **有**：配平器实测可用，仅缺上下文 → 安全恢复后 pdf-164/171/462 潜在 +3 | 无已验证近端；官能团题需先修错键（pdf-519）+ OCR/重建结构 |
| 域归属 | 文本/确定性引擎内（可被现有零 FP 全库扫描校验） | 引入视觉/OCR 或 SVG 解析，超出当前确定性引擎 |
| 红线可控性 | 中：管道改动需严格「上下文不跨题泄漏」守卫，但可全库扫描验证 | 高：视觉输出有误差面，precision=1 难保证；需答案键门控兜底 |
| 工作量 | 高（需新的母题上下文聚合机制 + 路由改造） | 高（OCR/重建结构 + molecule 资产 + 官能团解析器） |
| 既有重叠 | 与 K.1（最大未解簇）、X.3 Cluster 2 同根 | 与 chem-fact-curation「攻 SVG 硬骨头」轨部分重叠 |

**推荐**：优先启动 **轨道 1** 作为 batch7。理由：① 有已验证的近端收益（+3 可信目标）；② 全程在确定性文本引擎内，红线可由既有全库零 FP 扫描守住；③ K.1 是最大未解簇，上下文恢复一旦打通杠杆面最大。轨道 2 可并行预研（梳理 `vector_svg/` 已提取但未接线的分子图），但不宜在 precision=1 约束下硬上。

> 最终方向请用户裁定（见 §7 决策点）。若选轨道 1，需另立 Spec Mode 计划（涉及管道/路由改造，超出单计划单审批门的控制范围）。

---

## 5. batch6 范围（零风险，保护绿色基线）

鉴于无安全窄候选，batch6 **不做任何 `src/`/`data/`/`tests/` 生产改动**，只做两件零风险的事：

1. **固化审计结论**：本计划文档即为 gap 分布分析交付物（§1–§4），留 `_evalscratch/` 证据。
2. **登记册留痕**：在 `data-defect-register.md` 追加 **Y 类**「batch6 二次精筛：窄补强国境耗尽」区段，聚合记录本批结论与证据指针（纯登记，不改题录、不改键、不动生产数据），防止下一轮重复审计这 23 题。
3. **基线锁定**：重跑一次 Production（LLM-off）eval 打新快照 `checkpoint_b6baseline.jsonl`，与 `b5part2` 做 diff，预期 **GAIN=0 / LOST=0 / harm=0 / precision=1 / mp27=27/27**——证明本批（及工作区前批未提交改动）未引入漂移。

**范围外（明确不做）**：
- 不改引擎判分逻辑、不动 `images`、不臆造真值。
- 不启动轨道 1/轨道 2 的实现（留待用户决策后的独立 spec）。
- 不为凑数对 pdf-519 等存疑键加 fact（迎合错键 = harm）。

---

## 6. 实现步骤（依赖序，全部可验证、可回滚）

### Step 1 — 证据固化（已完成）
- `analyze_leads_b6.py` → `leads_b6.txt`：88 leads 聚类 + 23 候选短名单。
- `classify_leads_b6.py` → `classify_leads_b6.txt`：23 候选子问数/图像/答案洁净度/引擎阻塞态。
- `dump_bank_entries.py` → `bank_dump_b6.txt`：候选原始题录（题干/答案/图像/选项）。
- `scan_fp_risk_b6_full.py`：全库 757 题零 FP 扫描（输出见上）。

### Step 2 — 登记册追加 Y 类（获批后）
- 在 `data-defect-register.md` 的 X 类之后、「处理原则」之前插入 `## Y 类` 区段：
  - 记录本批漏斗 88→23→0；
  - 聚合 23 候选的排除根因表（题级细节已在 K/M/U/E/W/X 留痕，Y 类只给聚合 + 证据指针，不重复）；
  - 声明「窄 solver/fact 增益枯竭，下一轮走架构轨道 1/2」。
- 纯 Markdown 追加，不触及任何题录 JSON / 代码。

### Step 3 — 基线锁定 eval（获批后）
- 跑 Production 纯引擎 eval：`powershell -ExecutionPolicy Bypass -File _evalscratch\run_eval_resume.ps1 -Tag b6baseline -Env Production`。
- `diff_eval b5part2 b6baseline`：确认 GAIN=0、LOST=0。
- `verify_mp27 b5part2 b6baseline`：27/27。
- 若有任何 LOST 或 harm≠0 → 说明工作区存在未预期改动，立即排查，不进入下一步。

### Step 4 — 回填计划状态
- 本计划文档头部状态由「待审批」→「执行完成」，填入实测数字。
- 确认无 `_Probe*` / 探针 .cs 残留（本批未创建探针，仅 Python 证据脚本，均在 `_evalscratch/` gitignore）。

---

## 7. 验证

- **红线**：本批不碰生产代码/数据 → precision=1/harm=0/LOST=0 由构造保证；Step 3 的 eval diff 提供实证。
- **登记册改动**：仅追加 Markdown 区段，`git diff` 审查确认未改任何题录字段。
- **无探针残留**：`tests/` 下无 `_Probe*` 文件（本批未创建）。

---

## 8. 风险与回退

- **风险 1**：工作区含前批未提交改动（MoleculeAnalyzer、pdf_questions 等），Step 3 eval 若与 b5part2 有 diff → 回退：先确认 diff 来源是前批工作而非本批引入；本批不提交任何东西，若发现漂移则停止并报告，不强行「修复」。
- **风险 2**：用户裁定走轨道 1/2 后，其实现风险（管道跨题泄漏 / 视觉误差）→ 回退：轨道 1/2 必须各自独立 spec，在其 spec 内定义红线验证；batch6 不预做任何实现，故无回退负担。
- **风险 3**：「窄增益枯竭」结论被未来新数据（如新 OCR 回填、新题录入）推翻 → 处理：本结论绑定当前 757 题快照；题库变更后重跑 `analyze_leads_b6.py` 即可刷新。

---

## 9. 决策点（需用户确认）

请在以下方向中裁定 batch7 的起点（batch6 本身仅做审计留痕 + 基线锁定，不改生产）：

- **A（推荐）**：启动轨道 1「多子问母题上下文恢复」独立 spec（近端 +3 可信，确定性域内可控）。
- **B**：启动轨道 2「图像/结构 ingest」预研（先梳理 `vector_svg/` 未接线分子图 + 评估官能团题修键可行性）。
- **C**：两者都不启动，batch6 仅作为审计结论封存，等待新数据/新题。

---

## 10. 证据工件（`_evalscratch/`，gitignore）

- `analyze_leads_b6.py` / `leads_b6.txt`：88 leads 聚类 + 23 候选短名单。
- `classify_leads_b6.py` / `classify_leads_b6.txt`：23 候选细分类（子问/图像/答案/阻塞态），机械幸存 6→语义复核 0。
- `dump_bank_entries.py` / `bank_dump_b6.txt`：候选原始题录全文。
- `scan_fp_risk_b6_full.py`：全库 757 题零 FP 扫描（官能团前缀 4 题碰撞、pdf-519 键化学矛盾等关键证据）。
