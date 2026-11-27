# 数据修复（OCR 题面/配图恢复）批次 — 实施计划

> 基线快照：`_evalscratch/snapshot_after_keyfix_b38.json`（solved=700, prec=1, harm=0, LOST=[]）。
> 目标快照：`after_dr1`。所有中间脚本/留痕存 `_evalscratch/`（已 gitignore）。

## Task 1: 工具链确认与候选清单建立
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 确认离线 eval 入口与参数（复用前序 `run_eval_resume.ps1` 或等价），跑一次基线确认当前工作区 == `after_keyfix_b38`（零漂移）。
  - 核对目标缺陷题的 `src_page`/`ans_page` 渲染图是否在位；缺页用 PyMuPDF（`pip install pymupdf`，若网络允许）或复用既有渲染方式补齐到 `pdfs/_extract/page_renders/`。
  - 对所有目标页跑 tesseract `chi_sim` OCR，原文存 `_evalscratch/ocr_page_XXX.txt`。
  - 产出候选修复清单：逐题标注缺陷类型（题面碎片/键错位/缺图）、OCR 是否恢复出良构题干、子问与键是否对齐、键是否可独立核实。作为 Task 2/3/4 的输入。
- **Acceptance Criteria Addressed**: AC-2（前置证据）、AC-7
- **Test Requirements**:
  - `rule` TR-1.1：离线 eval 可运行并产出快照；目标页 OCR 原文全部生成（`_evalscratch/ocr_page_*.txt` 存在且非空）；缺渲染页已补齐或明确登记为阻塞。
  - `rubric` TR-1.2：候选清单完整度；维度=目标缺陷题是否逐题有分类与证据；scale 1-5；anchors 1=半数无题无证据，3=逐题有分类但部分缺 OCR，5=逐题有 OCR 证据+可执行决策；threshold >= 4；evidence=`_evalscratch/` 候选清单文件。
- **Notes**: 候选清单建议写 `_evalscratch/dr1_candidates.md`。

## Task 2: 题面恢复（Tier 1 — 只改 question，不动键）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 对候选清单中"题面碎片 + OCR 恢复出良构题干 + 子问结构与 answer 键对齐"的题，人工清理 OCR 噪声（化学式上下标、错字如 烫烧→焙烧、HoSe→H₂Se 等）后，Edit 写回 `question` 字段。
  - 用唯一题干片段作 Edit 锚点，精确替换；不动 `answer`/`answerKeywords`/`images`/`type`。
  - 子问与键对不上、OCR 证据不足的题跳过，登记理由。
- **Acceptance Criteria Addressed**: AC-1、AC-2、AC-5
- **Test Requirements**:
  - `rule` TR-2.1：每题恢复后 `question` 的子问编号与 `answer` 的 `(1)(2)(3)` 分段一一对应；OCR 原文留痕；对齐核对记录在 `_evalscratch/`。
  - `rule` TR-2.2：Tier 1 范围内无任何题的 `answer`/`answerKeywords` 被改动（`git diff` 核对）。
  - `rubric` TR-2.3：题面恢复完成度；维度=可恢复碎片题中成功恢复的比例；scale 1-5；anchors 1=≤1 题，3=约半，5=≥80%；threshold >= 3；evidence=候选清单标记。
- **Notes**: 清理后的题干若改变了 taskType 分类从而让引擎产出新结论，由 Task 5 离线 eval 的 harm=0 门兜底。

## Task 3: 答案键订正（Tier 2 — 可独立核实的错位键）
- **Status**: `completed (0 candidates)`
- **Priority**: medium
- **Depends On**: Task 1
- **Description**:
  - 对候选清单中"键与题干明显错位、且正确答案可由答案册 OCR 或题干 note 独立推出"的题（非 choice），订正 `answer` 与 `answerKeywords`（同步两处，口径同 `SplitPdfAnswerKeywords`）。
  - 改前写命中探针（复用 `NormalizeChemicalNotation` 归一化口径）：新正确答案必须命中；≥2 个易混错误答案（含旧错误键）必须不命中。探针通过才落库。
  - 无法独立核实（如 pdf-190 需逐子问标准答案、pdf-519 键存疑）的题跳过并登记。
- **Acceptance Criteria Addressed**: AC-3、AC-5、AC-7
- **Test Requirements**:
  - `rule` TR-3.1：每个键订正都有探针输出留存，显示正确命中、错误不命中。
  - `rule` TR-3.2：每题 `answer` 与 `answerKeywords` 内容一致（同步改）。
  - `rule` TR-3.3：无 choice 题被改键（`git diff` 核对 type）。
- **Notes**: 复用前序 `_evalscratch/spike_key_fix.py` 模式；预期 gwCorr +0（引擎 null），价值在真值完整性。

## Task 4: 配图恢复（Tier 3 — 条件性，键核实后才做）
- **Status**: `deferred (image-track spec, not this batch)`
- **Priority**: low
- **Depends On**: Task 1（若 VL API 不可达则整体取消并登记）
- **Description**:
  - 对"缺结构图/装置图"且答案键已核实正确的题，用 `pdfs/_extract/extract_images.py`（或其 VL 调用）对源页检测配图 bbox → 裁剪存 `src/.../data/pdf_images/` → 在该题 `images` 字段回链。
  - 键存疑（如 pdf-519 真实曲美他嗪不含硫 vs 键"硫醚"）的题**不做配图恢复**（避免迎合错键），登记待答案册核对。
- **Acceptance Criteria Addressed**: AC-2、AC-5
- **Test Requirements**:
  - `rule` TR-4.1：回链的图片文件真实存在于 `data/pdf_images/`；`images` 字段路径正确。
  - `rule` TR-4.2：仅对键核实正确的题执行；键存疑题跳过并登记。
- **Notes**: 本任务为条件性/低优先；若 VL API 不可达或收益不明确，可整项取消（需用户批准，记 cancelled）。即时 gwCorr 增益不确定（需分子/装置引擎能消费），以 Task 5 实测为准。

## Task 5: 验证门与登记收尾
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 2、Task 3、Task 4（或其取消）
- **Description**:
  - JSON 校验：`pdf_questions.json` 解析成功、545 条。
  - stale-DLL 清场 + build：`dotnet build` 0 errors（数据变更不涉及代码，但确认加载无异常）。
  - 离线确定性 eval（LLM-off）→ 快照 `after_dr1`；与 `after_keyfix_b38` cmp：要求 `LOST=[]`、`harm=0`、`precision=1`；记录 GAINED（可为 0）。
  - 更新 `data-defect-register.md`：已修复题标注依据；跳过题标注理由。
  - 范围核对：`git diff --stat` 确认无引擎/判分 `.cs` 逻辑改动、无 choice 键改动。
  - 不主动 commit。
- **Acceptance Criteria Addressed**: AC-1、AC-4、AC-6、AC-7、AC-8
- **Test Requirements**:
  - `rule` TR-5.1：`json.load` 解析成功且条目数 = 545。
  - `rule` TR-5.2：`cmp after_keyfix_b38 after_dr1` 输出 LOST=[]、harm=0、precision=1。
  - `rule` TR-5.3：`data-defect-register.md` 已更新，每条改动可追溯。
  - `rule` TR-5.4：`git diff --stat` 文件集合 ⊆ {`pdf_questions.json`, `data/pdf_images/*`, `data-defect-register.md`, `_evalscratch/*`}；无 `.cs` 判分逻辑改动。
  - `rule` TR-5.5：`after_dr1` 聚合 harmCount=0 且 gatewayTruthCorrect ≥ 700。
- **Notes**: 若 TR-5.2 出现 LOST 或 harm>0 → 立即回滚对应改动，排查，不得带伤通过。
