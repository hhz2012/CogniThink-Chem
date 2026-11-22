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
| [hard-bones-coverage-push](./hard-bones-coverage-push/spec.md) | 硬骨头覆盖推进（难解题型的覆盖攻坚），含 final-report | ✅ review |
| [image-equation-engine-151](./image-equation-engine-151/spec.md) | 剩余 151 题：图像引擎与方程引擎修复 | ✅ review |
| [knowledge-qa-framework](./knowledge-qa-framework/spec.md) | 化学知识问答与统一解题框架体系 | ✅ review |
| [multi-part-compound-answer-fix](./multi-part-compound-answer-fix/spec.md) | 多段复合答案（含 (1)…(2)… 或多关键词）根因关闭与碎片上下文回填 | ✅ review |
| [remaining-148-reliability-engine](./remaining-148-reliability-engine/spec.md) | 剩余 148 题：可靠性加固与引擎能力扩展 | ✅ review |
| [verify-50-triage](./verify-50-triage/spec.md) | verify 50 个新发现嫌疑题真假分诊 | ✅ review |
| [verify-fix-all-suspects](./verify-fix-all-suspects/spec.md) | 全量可疑题修复：千问视觉 OCR 解锁习题册/答案册缺失文本层 + 数据修复 | ✅ review |

## 进行中 / 待执行（无 review.md）

| Spec | 内容 | 状态 |
|------|------|------|
| [engine-capability-gap-fix](./engine-capability-gap-fix/spec.md) | LLM 关闭评估下 63 道引擎能力缺口题的分阶段修复（事实策展 + 方程重路由 + 占位守卫） | 🔄 进行中（P1-1，当前活跃） |
| [engine-imageasset-routing](./engine-imageasset-routing/spec.md) | 图形结构化数据接入解题管线 | ⏳ 待确认完成状态 |
| [image-ocr-choice-coverage](./image-ocr-choice-coverage/spec.md) | 图像/OCR 未解选择题覆盖 | ⏳ 待确认完成状态 |

> 注：`engine-imageasset-routing` 与 `image-ocr-choice-coverage` 无 review.md，需核对其 tasks.md 是否已全部完成；若已完成应补 review。
