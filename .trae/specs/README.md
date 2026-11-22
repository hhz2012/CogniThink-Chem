# Spec 索引：已交付能力与进行中工作

> 本文件是 `.trae/specs/` 下所有 spec 的总览。每个 spec 目录含 `spec.md`（需求）、`tasks.md`（任务），已完成的另有 `review.md`（独立评审）。
> 现状架构见 [ARCHITECTURE.md](../../ARCHITECTURE.md)，整体路线图见 [结构分析与路线图](../documents/project-structure-analysis-and-roadmap.md)。

## 已完成（有 review.md，能力已落地）

| Spec | 交付能力 | 评审 |
|------|----------|------|
| [chem-engine-question-solving](./chem-engine-question-solving/spec.md) | 打通「LLM 解析 → 化学引擎计算 → LLM 讲解」求解真题链路；恢复选择题选项/答案到评估链路；扩展 `IsAnswerCorrect` 支持选项字母判正 | ✅ review |
| [choice-coverage-expansion](./choice-coverage-expansion/spec.md) | 41 道选择题事实覆盖缺口的可行性分诊、安全策展与回归控制 | ✅ review（R1 pass） |
| [choice-generalization-data-repair](./choice-generalization-data-repair/spec.md) | 选择题求解能力泛化 + 数据缺陷修复 | ✅ review |
| [crystal-cell-validator](./crystal-cell-validator/spec.md) | 晶胞（Crystal Cell）轨道：确定性晶胞计算求解器 | ✅ review |
| [data-asset-inventory-gap-report](./data-asset-inventory-gap-report/spec.md) | 数据资产盘点与缺口报告（题库/事实/JSON 资产清单） | ✅ review |
| [engine-capability-gap-fix](./engine-capability-gap-fix/spec.md) | LLM 关闭评估下引擎能力缺口题分阶段修复（事实策展 + 方程重路由 + 占位守卫） | ✅ review |
| [free-fill-concept-solver](./free-fill-concept-solver/spec.md) | 自由填空概念求解（ConceptFact 兜底，+5） | ✅ review |
| [frontend-gateway-solve-loop](./frontend-gateway-solve-loop/spec.md) | 前端网关求解循环契约 | ✅ review |
| [hard-bones-coverage-push](./hard-bones-coverage-push/spec.md) | 硬骨头覆盖推进（难解题型的覆盖攻坚），含 final-report | ✅ review |
| [image-equation-engine-151](./image-equation-engine-151/spec.md) | 剩余 151 题：图像引擎与方程引擎修复 | ✅ review |
| [image-text-fusion](./image-text-fusion/spec.md) | 图像文本融合（结构化资产解释链路） | ✅ review |
| [knowledge-qa-framework](./knowledge-qa-framework/spec.md) | 化学知识问答与统一解题框架体系 | ✅ review |
| [multi-part-compound-answer-fix](./multi-part-compound-answer-fix/spec.md) | 多段复合答案（含 (1)…(2)… 或多关键词）根因关闭与碎片上下文回填 | ✅ review |
| [multi-part-subquestion-dispatch](./multi-part-subquestion-dispatch/spec.md) | 多部分子问题分发（AND 语义判分，opt-in） | ✅ review |
| [pure-calc-calculators](./pure-calc-calculators/spec.md) | 纯计算域计算器（水解 pH / 氧还物质的量比） | ✅ review |
| [remaining-148-reliability-engine](./remaining-148-reliability-engine/spec.md) | 剩余 148 题：可靠性加固与引擎能力扩展 | ✅ review |
| [routing-unknown-backfill](./routing-unknown-backfill/spec.md) | 未知题型路由回填 | ✅ review |
| [rule-fact-system-normalization](./rule-fact-system-normalization/spec.md) | 规则事实系统归一化（零数据/代码改动，52 JSON sha256 一致） | ✅ review |
| [tutor-solve-fusion](./tutor-solve-fusion/spec.md) | 导学与求解融合（加性推理链 DAG 锚定，零漂移） | ✅ review |
| [unified-reasoning-dag](./unified-reasoning-dag/spec.md) | 统一推理 DAG 与结论三态契约（Confirmed/CandidateSet/Insufficient） | ✅ review |
| [verify-50-triage](./verify-50-triage/spec.md) | verify 50 个新发现嫌疑题真假分诊 | ⚠️ review 含未闭合 FAIL（见下方注 1） |
| [verify-fix-all-suspects](./verify-fix-all-suspects/spec.md) | 全量可疑题修复：千问视觉 OCR 解锁习题册/答案册缺失文本层 + 数据修复 | ✅ review |
| [world-model-minimal](./world-model-minimal/spec.md) | 世界模型最小解释（命中链可溯源） | ✅ review |

## 进行中 / 待执行（无 review.md）

| Spec | 内容 | 状态 |
|------|------|------|
| [engine-imageasset-routing](./engine-imageasset-routing/spec.md) | 图形结构化数据接入解题管线 | ⏳ 部分旁路覆盖（见下方注 2），无 review |
| [image-ocr-choice-coverage](./image-ocr-choice-coverage/spec.md) | 图像/OCR 未解选择题覆盖 | ⏳ 工作已合入 `0ddb5f6`，缺 review（见下方注 3） |

> **注 1（verify-50-triage）**：其 [review.md](./verify-50-triage/review.md) 结论为 FAIL（L52）——分诊报告 `_evalscratch/verify/triage_report.md` 的 α3 子标签标错（pdf-199/261/357 应为 α2）且含「清空 options」的有害修法建议。2026-11-22 审计 grep 确认该报告仍含「清空 options」，即 review 要求的 5 项修正未落地。该 spec 的 tasks.md 虽全部 `completed`，但 review FAIL 未闭合；**此项不在本次文档同步范围内，建议后续单独核对并修正报告**。
>
> **注 2（engine-imageasset-routing）**：本 spec 12 个任务均 `pending`、无 review。其中 **molecule / curve 图像资产 → 选择题判分的能力已由主设计 `chem-engine-question-solving` 的 Task 43（`MoleculeChoiceValidator`）与 Task 44（`CurveChoiceValidator`）旁路落地**；**未覆盖**：`graph` 结构化资产路由（`GraphSolver` 适配层，graph 47 题结构化命中 0）与本 spec 规划的「ImageAsset 迁移共享命名空间」等架构调整。结论：molecule/curve 目标已旁路达成，graph 及架构迁移留作下一阶段 backlog；任务标记保持 `pending`（如实反映未执行）。详见该 spec [tasks.md](./engine-imageasset-routing/tasks.md) 顶部裁决说明。
>
> **注 3（image-ocr-choice-coverage）**：选项回填 + 事实策展工作已以提交 `0ddb5f6` 合入并集成（EvalRunner 终态 engineSolved 448→459，LOST=0），但本 spec 缺 `review.md`，建议补独立只读评审收口。
