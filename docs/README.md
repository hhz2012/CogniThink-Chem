# 化学伴思 文档索引

> ⚠️ **本目录为 v0.2 历史规划**，描述的是项目初期的规划态（T0–T6、M0–M12），与当前实现已大幅脱节，**仅作历史背景参考，不作为现状依据**。
>
> 当前真实架构 → 仓库根 [ARCHITECTURE.md](../ARCHITECTURE.md)；下一步计划 → [结构分析与路线图](../.trae/documents/project-structure-analysis-and-roadmap.md)；已交付能力 → [.trae/specs/](../.trae/specs)；能力缺口台账 → [data-defect-register.md](../data-defect-register.md)。

本目录存放 CogniThink-Chem 的历史规划、决策与设计文档。

| 文档 | 内容 | 适合谁读 |
| --- | --- | --- |
| [01-overall-plan.md](./01-overall-plan.md) | 目标与定位、总体架构、知识覆盖清单、规则优先顺序、虚拟环境与机理、推理能力、题库、技术选择与风险、决策状态 | 项目负责人、评审者 |
| [02-modules.md](./02-modules.md) | 功能模块拆分：职责、输入输出、依赖关系、跨模块数据契约、实现约定、模块优先级 | 设计与开发 |
| [03-tasks.md](./03-tasks.md) | 阶段任务清单：优先级、依赖、验收标准、当前进度、关键路径、阻塞项 | 排期与执行 |
| [decisions.md](./decisions.md) | 决策记录：D1 已定；D2–D5 推荐方案与待确认事项；D6–D10 待定 | 决策者、开发 |

**建议阅读顺序**：01 → 02 → 03 → decisions。若只想了解"要做什么"，读 01 的 1、8、9、11 节；若准备开工，先看 03 的 T0（决策项）与关键路径。

## 状态（历史）

- 规划版本：v0.2（D1 收口）—— **此为 2026-11 中旬的历史快照，不再更新**。
- 决策 D1（C# / .NET 8）已定；D2–D5 当时为建议采纳待确认；D6–D10 当时待定。决策现状以各 spec 与代码为准。
- 该版本记录的"实现进度"（T0 完成 / T1 部分 / T2 暂停 / T3–T6 未开始）已完全过时。当前真实进度见 [ARCHITECTURE.md](../ARCHITECTURE.md) 与 [.trae/specs/](../.trae/specs)。
