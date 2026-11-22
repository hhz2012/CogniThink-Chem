# verify 50 个新发现嫌疑题真假分诊 - Product Requirements Document

## Overview
- **Summary**: 对 `verify_review.md` §3 列出的 **50 个未在 `data-defect-register.md` 登记的新发现嫌疑题**，逐题判定「库真缺陷」还是「verify 工具假阳性」，产出可追溯的分诊报告与底稿，为后续是否修数据/登记/改进工具提供决策依据。
- **Purpose**: verify 工具的 suspect 信号混杂了大量「官方抽取层不完整」和「规则边界」导致的假阳性（见 Background）。若不先分诊真假，直接登记或修数据会污染登记册、甚至引入 harm；若全部当作误报忽略，又会漏掉真缺陷。本 spec 用人工+化学真值收敛把 50 题分清。
- **Target Users**: 题库数据维护者（决定修哪些题、登记哪些）、verify 工具维护者（决定改进哪些规则）。

## Goals
- 50 题 100% 覆盖，每题归入互斥的分诊桶并附可追溯证据。
- 判为「真缺陷」的题必须有官方源 + 化学真值双重证据，**precision=1**（库正确的题绝不判成真缺陷）。
- 判为「verify 假阳性」的题必须指出误报根因（官方抽取不全 / 规则边界 / askDir 误判），汇总为可执行的工具改进建议。
- 全程**只读**，不修改任何题库数据或引擎代码，守 harm=0。

## Non-Goals
- **不修改** `pdf_questions.json`、`data/highschool/*.json`、引擎 `.cs` 代码（修数据是分诊之后的独立任务，须官方源留痕 + 登记 + eval 验证，不在本 spec 范围）。
- **不实现** verify 工具的改进（β 桶的改进建议仅记录，不编码）。
- **不做** 化学事实策展、不新增求解器。
- **不覆盖** 已在登记册登记的 48 个嫌疑（本 spec 只分诊 50 个新发现）。

## Background & Context
- verify 工具（`src/CogniThink.TutorBrain.PdfOcrRepair --verify`）产出 `verify_report.json`（300 findings / 98 suspectIds）与 `verify_review.md`。98 嫌疑中 48 已登记、50 未登记（本 spec 对象）。
- verify 的 V2/V3 基准是 `_extract/exercise` 的 JSON 抽取；V4 基准是 `_extract/answer` 的 explanation + 自行推断的 askDir；V1/V5 是正则规则。
- **抽样校准结论**（证据见各 `<id>_recheck.txt`，本 spec 调研阶段读取）：
  - `pdf-252`（V1）：`answer=BFH` 是 A–H 八选项多选题的合法多选（官方答案册同写 BFH，解析逐一评 A–H）。verify 多选字母范围仅 `A-G`，把含 H 的合法多选误判污染 → **假阳性，规则边界缺陷**。
  - `pdf-006`（V2）：库有 A/B/C/D，官方 exercise 抽取只有 A/B，但答案册 explanation 明确评价了 C、D 项 → 原题实有 4 选项，是**官方 exercise 抽取漏了 C/D**，库正确 → **假阳性，抽取层不完整**。
  - `pdf-051`（V3）：库题干有完整 5 小问，官方 exercise 抽取只抽到第(1)问开头就截断；答案册 answer 有 5 项与库 5 问完全对应 → 库正确，官方抽取截断 → **假阳性，抽取层不完整**。且该页新鲜文本层为「过短/疑似扫描页」，`_extract/exercise` 对扫描页不可靠。
  - `pdf-015`（V4）：题干问「不符合要求的是」（askDir=WRONG），答案册解析称 B 正确、库 answer=B → 真实矛盾信号，但 askDir 推断与解析评价方向冲突，需独立化学真值判库 answer 是否错 → **真嫌疑，待化学收敛**。
  - `pdf-056`（V5）：note 含「推断本题答案为B」「原题可能B为正确选项」等 AI 抽取残留元话语，触发 V5 关键词；但库 answer=B 与官方答案册一致 → **answer 正确，note 质量问题（非答案缺陷）**。
- 由此确立分诊核心：**V2/V3 不能只信 `_extract/exercise`，必须用答案册 explanation 是否提到库多出的选项/小问来反推原题完整性；扫描页须 OCR 渲染原页确认。**

## Functional Requirements
- **FR-1**: 对 50 个新发现 id 逐题深核：读取 `_evalscratch/verify/<id>_recheck.txt`（库内 / 官方 exercise 抽取 / 官方 answer 册抽取 / findings），必要时对扫描页渲染 PDF 原页并 OCR 确认原题完整性。
- **FR-2**: 每题按统一判定流程归入一个桶：
  - **α 真缺陷**：库 answer / 题面 / 选项与「官方答案册 + 独立化学真值」收敛冲突，确属库数据错误。
  - **β verify 假阳性**：库数据正确，误报根因在官方抽取层不完整或 verify 规则边界（V1 字母范围、V2/V3 抽取截断、V5 关键词误触、askDir 误判等）。
  - **γ 存疑**：证据不足（扫描页无文本层且官方抽取也不全、或依赖图像/流程原图），无法判定真假，需 OCR/原图/人工接续。
  - **δ note 质量问题**：库 answer 正确（与官方+化学一致），但 note 含 AI 抽取残留元话语或转述失真，非答案缺陷，可单独清理。
- **FR-3**: 产出分诊底稿 `_evalscratch/verify/triage_50.tsv`（id / 桶 / 主类 / 判定理由 / 证据链接）与汇总报告 `_evalscratch/verify/triage_report.md`。
- **FR-4**: 汇总报告含：各桶计数与 id 清单、α 桶逐题证据、β 桶的 verify 改进建议（指向具体规则/抽取层）、γ 桶的接续需求。

## Non-Functional Requirements
- **NFR-1（precision=1）**: α 桶零误判——任何库正确的题不得进入 α；α 每题必须有可复核的官方源与化学真值证据。
- **NFR-2（harm=0 / 只读）**: 不写 `pdf_questions.json`、不写数据目录、不改引擎；所有产出落 `_evalscratch/verify/` 与本 spec 目录（均 gitignored 或文档态）。
- **NFR-3（可追溯）**: 每题判定附证据指针（recheck.txt 行 / OCR 页 / 化学推导要点），独立 reviewer 可复现。
- **NFR-4（不硬编码化学事实进 .cs）**: 分诊用的化学判断仅写进报告/底稿，不进入引擎代码。

## Constraints
- **Technical**: 仅使用现有 `--recheck` 产物 + PDF 渲染/OCR（`_evalscratch/renders`、tesseract）；不新增工具能力。
- **Business**: precision=1 / harm=0 / LOST=0 红线不可越；`answer` 字段永不作真值。
- **Dependencies**: `data-defect-register.md`（判定是否已登记）、`_evalscratch/verify/*.txt`（98 份逐题对照）、`pdfs/` 原 PDF 与 `_extract/` 抽取。

## Assumptions
- 50 个新发现 id 列表即 `verify_review.md` §3（脚本复核 count=50，已存 `_new50_skeleton.tsv`）。
- 化学真值由分诊者基于题面+选项+解析独立推理给出；复杂图像/计算题无法文本判定时归入 γ，不强行猜。
- 「官方答案册 answer 字段多字母污染」已被 verify 忽略（仅用 explanation），分诊时同样以 explanation + 化学为准。

## Open Questions
- [ ] γ 桶若超过 20%（10 题），是否需要在本 spec 内追加 OCR 渲染批次来压缩？（默认：γ 记录接续需求即可，OCR 实现不在本 spec；若 α 判定依赖原图则必须在该题内补 OCR，不得凭猜判 α。）
- [ ] δ 桶的 note 清理是否纳入本 spec？（默认：否，仅记录；清理是独立数据任务。）

## Acceptance Criteria

### AC-1: 50 题全覆盖
- **Type**: `rule`
- **Given**: `verify_review.md` §3 的 50 个未登记 id
- **When**: 分诊完成
- **Then**: `triage_50.tsv` 含且仅含这 50 个 id，每题归入 α/β/γ/δ 之一，无重复、无遗漏
- **Pass Condition**: 脚本校验 tsv id 集合 == 50 个新发现集合，count=50
- **Evidence**: `_evalscratch/verify/triage_50.tsv` + 校验脚本输出

### AC-2: α 桶 precision=1（零误判真缺陷）
- **Type**: `rule`
- **Given**: 被判入 α 的题
- **When**: 独立复核证据
- **Then**: 每题存在「官方答案册/exercise 源」与「独立化学真值」两条独立证据，且二者均指向库数据错误；不存在库正确却判 α 的情况
- **Pass Condition**: α 桶每题在 `triage_report.md` 有双证据段落；独立 review 抽查 α 桶 100% 通过
- **Evidence**: `triage_report.md` α 节 + review.md CP-R2

### AC-3: 全程只读零污染
- **Type**: `rule`
- **Given**: 本 spec 执行期间
- **When**: `git status`
- **Then**: `pdf_questions.json`、`data/highschool/*.json`、引擎 `.cs` 未被本 spec 改动（允许既有未提交改动存在，但非本 spec 引入）；产出仅落 `_evalscratch/verify/` 与 `.trae/specs/verify-50-triage/`
- **Pass Condition**: `git status` 无新增的数据文件改动；`_evalscratch/verify/*` 被 gitignore
- **Evidence**: `git status --short` 输出 + `git check-ignore`

### AC-4: β 桶产出可执行的工具改进建议
- **Type**: `rule`
- **Given**: 被判入 β 的题
- **When**: 汇总报告完成
- **Then**: 报告按根因聚合 β 题（如「官方 exercise 抽取漏选项」「V1 多选字母上限 A-G」「V5 关键词误触元话语」），每类给出指向具体规则/抽取层的改进建议
- **Pass Condition**: `triage_report.md` β 节有根因分类 + 改进建议，每条建议可定位到 Program.cs 规则或 _extract 层
- **Evidence**: `triage_report.md` β 节

### AC-5: 分诊可追溯
- **Type**: `rule`
- **Given**: 任意一题的分诊结论
- **When**: 查阅底稿
- **Then**: `triage_50.tsv` 每题含 id/桶/主类/判定理由/证据链接（recheck.txt 或 OCR 页），理由非空
- **Pass Condition**: tsv 每行字段完整，证据链接可打开
- **Evidence**: `triage_50.tsv`

### AC-6: 分诊可靠性（独立复核）
- **Type**: `rubric`
- **Dimension**: α 桶证据充分性与 γ 桶占比
- **Scale**: 1-5
- **Anchors**: 1 = α 桶多数题缺双证据或 γ 桶 >40%；3 = α 桶基本有证据但部分化学推导薄弱，γ 桶 20–40%；5 = α 桶每题双证据扎实、化学推导可复现，γ 桶 ≤20% 且均明确接续需求
- **Pass Threshold**: >= 4
- **Evidence**: `triage_report.md` + review.md CP-U1

### AC-7: 不混淆「修数据」与「改工具」
- **Type**: `rule`
- **Given**: 汇总报告
- **When**: 阅读报告
- **Then**: 报告明确区分 α（需修数据/登记）与 β（需改 verify 工具/抽取层），不把 β 误建议为修数据、也不把 α 漏为工具改进
- **Pass Condition**: 报告 α/β 节边界清晰，无交叉建议
- **Evidence**: `triage_report.md`
