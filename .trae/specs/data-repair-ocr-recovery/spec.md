# 数据修复（OCR 题面/配图恢复）批次 — 产品需求文档

## Overview
- **Summary**：对 `pdf_questions.json` 中已登记的数据缺陷题，用仓库内现成的本地 OCR（tesseract chi_sim）+ 云端 VL（qwen3-vl-plus）工具链，从源 PDF（扫描图）恢复被截断的题干、核对/订正错位的答案键、补回缺失的结构图，在 `precision=1 / harm=0 / LOST=0 / 不蒙数` 红线下提升判分真值的完整性，并为后续引擎/LLM 解题清障。
- **Purpose**：当前 57 道未解中 16 道是数据缺陷（题干截断/子问缺失/答案键错位/缺结构图），引擎对它们全返回 null。修答案键已被前序批次证明不产生即时 gwCorr 增益（+0），但题面/配图恢复是让任何求解器（引擎或未来 LLM）有题可解的前置条件——是剩余未解里杠杆最高的方向。
- **Target Users**：题库维护者、后续引擎扩展批次的执行者、评估指标的消费者。

## Goals
- G1：恢复"题面碎片"缺陷的完整 `question` 文本（OCR 自源页，清理噪声），使题干成为良构问题；仅恢复 OCR 证据充分、且子问结构与现有答案键对得上的题。
- G2：对答案键与题干明显错位的题，仅在能从答案册 OCR 或题干 note 独立核实正确答案、并经命中探针验证时订正 `answer`/`answerKeywords`（同步两处）。
- G3：对"缺结构图"缺陷，用既有 VL 配图管线从源页提取分子/装置图并回链 `images`，**仅在答案键核实正确后**执行；键存疑的题不迎合错键。
- G4：全程不破坏既有 700 道已解题，不引入 harm，不降低 precision。

## Non-Goals
- 不改引擎/判分逻辑代码（不动 `LlmGatewayService`、各 solver、`IsAnswerCorrect`、WRONG 正则等）。
- 不改选择题（choice）的答案键（高风险：若键本正确而按错答改键=污染真值，前序计划明确排除）。
- 不对全 757 题做 OCR；只处理已登记的目标缺陷题。
- 不放宽 red line（不接受 precision<1 或 harm>0）。
- 不承诺本批次产生 gwCorr +N（题面恢复为未来解题清障，即时增益取决于引擎能力，以实测为准；GAINED 非必须，LOST=[] 是硬门）。

## Background & Context
- 全库 757 题，当前基线 `after_keyfix_b38`：solved=700、precision=1、harm=0、LOST=[]；剩余 57 道未解（引擎全返回 null，且本快照 `llmAvailable=False`，无 LLM 兜底）。
- 57 道分诊：fill 26 / choice 24 / calc 3 / other 3 / judge 1。其中 16 道为数据缺陷，已登记在 `data-defect-register.md`（根目录）与 `.trae/documents/data-defect-repair-plan.md`（已于 2026-11-26 执行，修了 5 道高置信答案键 pdf-172/179/164/117/324）。
- 前序计划的关键结论：修答案键 → gwCorr +0（引擎本就 null），价值在真值完整性与清障；本批次延续该口径。
- 源 PDF 现状：`pdfs/` 下习题册/答案册为**扫描图**（无文本层，每页 text_len≈27 仅水印），必须 OCR。仓库已有：`pdfs/_extract/page_renders/` 113 张页面渲染图、`C:\Program Files\Tesseract-OCR\tesseract.exe`（含 `chi_sim`）+ `pytesseract 0.3.13`、`pdfs/_extract/extract_images.py`（云端 VL 配图 bbox 检测 + 裁剪，key 已内置）。
- 概念验证已通过（`_evalscratch/_ocr_spike.py`）：OCR 第 54 页完整恢复了 pdf-160 在 JSON 中被截断的子问 (1)(2)(3)；第 112 页恢复了官能团题及结构片段。证据：`_evalscratch/ocr_page_054.txt`、`ocr_page_112.txt`。
- 分诊证据：`_evalscratch/triage_33_dump.txt`（33 道非 choice 逐题快照状态）、`_evalscratch/_qp_compare.py`（确认 `questions_parsed.json` 与 `pdf_questions.json` 同源截断，无可抄的更完整文本）。

## Functional Requirements
- **FR-1（题面恢复）**：对目标缺陷题，OCR 其 `src_page`（及必要时相邻页），人工清理 OCR 噪声后写回 `question` 字段；恢复后题干的子问编号/数量必须与 `answer` 字段的 `(1)(2)(3)` 结构一致，否则该题跳过（不蒙）。
- **FR-2（键订正）**：仅当正确答案可由答案册 OCR 或题干 note 独立推出时，同步改 `answer` 与 `answerKeywords`；改前用探针（复用 `NormalizeChemicalNotation` 口径）验证：正确答案命中、≥2 个易混错误答案不命中。
- **FR-3（配图恢复）**：对缺图题，用 VL 管线从源页检测配图 bbox → 裁剪保存到 `data/pdf_images/` → 在该题 `images` 字段回链；仅在答案键经核实正确后执行。
- **FR-4（证据留痕）**：每题修复在 `_evalscratch/` 留存 OCR 原文、清理后题干、键核对依据、探针输出；并在 `data-defect-register.md` 登记。
- **FR-5（跳过规则）**：OCR 证据不足、子问与键对不上、键无法独立核实、或属 choice 键的题，一律跳过并登记理由，不强行修复。

## Non-Functional Requirements
- **NFR-1（零回归）**：离线确定性评估（LLM-off）相对 `after_keyfix_b38` 必须 `LOST=[]`、`harm=0`、`precision=1`。
- **NFR-2（JSON 完整）**：`pdf_questions.json` 保持合法、545 条、字段结构不变；仅改 `question`/`answer`/`answerKeywords`/`images`。
- **NFR-3（可追溯）**：每条修复都有 OCR 原文 + 决策依据，可复核。
- **NFR-4（安全）**：不把正确键改成错键（FR-2 探针是硬门）；不迎合存疑错键（pdf-519 等）。

## Constraints
- **Technical**：仅用本地 tesseract OCR + 既有 VL 管线 + 手工清理；不引入新依赖到生产代码（探针/清理脚本留 `_evalscratch/`，gitignore）。
- **Business**：red line 不变（precision=1 / harm=0 / LOST=0 / 不蒙数）。
- **Dependencies**：源页渲染图在位（缺页需先补渲染）；VL API 网络可达；答案册 PDF 可 OCR。

## Assumptions
- A1：源 PDF 页面渲染图（`page_renders/`）对目标页基本覆盖；缺页可通过安装 PyMuPDF 或复用既有渲染方式补齐（实现任务确认）。
- A2：tesseract chi_sim 对中文题干的 OCR 质量经人工清理后可用（概念验证已证）。
- A3：离线 eval 脚本（`_evalscratch/run_eval_resume.ps1` 或等价）可在本环境运行，产出快照并做集合 diff（与前序批次一致）。
- A4：目标缺陷题清单以 `data-defect-register.md` 当前登记为准（约 16 道非 choice 数据缺陷 + 已执行批次未覆盖的键错位题）。

## Acceptance Criteria

### AC-1：JSON 完整性
- **Type**：`rule`
- **Given**：本批次所有编辑已落库到 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json`
- **When**：用 `json.load` 解析该文件
- **Then**：解析成功，条目数 = 545，无字段结构破坏
- **Pass Condition**：解析成功且 545 条
- **Evidence**：`python -c "import json; d=json.load(open(...)); print(len(d))"` 输出 545

### AC-2：题面恢复的证据与对齐
- **Type**：`rule`
- **Given**：一道被恢复 `question` 的缺陷题
- **When**：检查 `_evalscratch/` 中该题的 OCR 原文与清理后题干
- **Then**：清理后题干的子问编号结构与该题 `answer` 字段的 `(1)(2)(3)` 分段一一对应；OCR 原文留存
- **Pass Condition**：每题均有 OCR 原文留痕且子问-键结构对齐
- **Evidence**：`_evalscratch/` 下该题 OCR txt + 对齐核对记录

### AC-3：键订正的命中安全
- **Type**：`rule`
- **Given**：一道被订正 `answer`/`answerKeywords` 的题
- **When**：运行订正前的命中探针（`NormalizeChemicalNotation` 口径）
- **Then**：新正确答案命中；至少 2 个易混错误答案（含旧错误键）不命中
- **Pass Condition**：正确命中、错误不命中，探针输出留存
- **Evidence**：`_evalscratch/` 下探针脚本输出

### AC-4：引擎侧零回归
- **Type**：`rule`
- **Given**：本批次编辑落库后
- **When**：跑离线确定性 eval（LLM-off）得快照 `after_dr1`，与基线 `after_keyfix_b38` 做集合 diff
- **Then**：`LOST=[]`（无已解题变未解）、`harm=0`、`precision=1`
- **Pass Condition**：LOST=[] 且 harm=0 且 precision=1
- **Evidence**：`cmp` 脚本输出；快照 `_evalscratch/snapshot_after_dr1.json`

### AC-5：缺陷修复覆盖率
- **Type**：`rubric`
- **Dimension**：本批次目标数据缺陷题中，被成功修复（题面恢复或键订正或配图回链，且通过 AC-2/AC-3 门）的比例
- **Scale**：1-5
- **Anchors**：1 = 仅修复 ≤2 题或多数因证据不足跳过；3 = 修复约一半目标题，跳过有明确理由；5 = 修复 ≥80% 可修复目标题，跳过项均登记且理由充分
- **Pass Threshold**：>= 3
- **Evidence**：`data-defect-register.md` 修复记录 + 跳过清单

### AC-6：登记册更新
- **Type**：`rule`
- **Given**：本批次结束
- **When**：检查 `data-defect-register.md`
- **Then**：已修复题标注"已修复 + 依据"；跳过题标注理由；无未登记的静默改动
- **Pass Condition**：每条改动在登记册可追溯
- **Evidence**：`data-defect-register.md` diff

### AC-7：范围纪律
- **Type**：`rule`
- **Given**：本批次全部改动
- **When**：检查 git diff 触及的文件
- **Then**：仅 `pdf_questions.json`（+`data/pdf_images/` 配图）、`data-defect-register.md`、`_evalscratch/` 探针/留痕被改；无引擎/判分逻辑 `.cs` 代码改动；无 choice 题答案键改动
- **Pass Condition**：diff 文件集合符合上述范围
- **Evidence**：`git status` / `git diff --stat`

### AC-8：不引入新 harm
- **Type**：`rule`
- **Given**：本批次编辑落库后
- **When**：检查 `after_dr1` 快照的 harmCount 与 gatewayTruthCorrect
- **Then**：harmCount=0；gatewayTruthCorrect 不低于基线 700（AC-4 LOST=[] 已隐含，此处独立核对）
- **Pass Condition**：harmCount=0 且 gatewayTruthCorrect ≥ 700
- **Evidence**：快照聚合字段

## Open Questions
- [ ] 离线 eval 脚本的当前可运行入口与参数（需在 Plan 阶段确认，复用前序 `run_eval_resume.ps1`）。
- [ ] 缺渲染图的目标页（如 055）如何补齐：安装 PyMuPDF 还是复用既有渲染脚本？（Plan 阶段确认）。
- [ ] 配图恢复的 VL API 在本环境网络是否稳定可达（实现时先探针）。
