# 化学伴思 · CogniThink-Chem

化学伴思是一个面向化学学习的「思考陪伴」项目。它不以直接给出答案为终点，而是把解题过程拆解为可检查的推理步骤，让学习者看清每一步「为什么这样想」，从而把知识真正内化为可迁移的思考方式。

> 当前状态：**规划已收口，知识层（T1）部分实现**。D1（C# / .NET 8）已定；D2–D5 已给出推荐方案（见 [docs/decisions.md](./docs/decisions.md)，待确认）。已完成 T0 工程骨架；T1 覆盖清单（49 条）+ 规则库（37 条）+ 仲裁 + 回归已落地，但 T1-6 例外清单（目标 ≥50 条，现 3 条）与 T1-9 缺口报表未达标；T2 世界模型与机理引擎已暂停，等待继续。

## 项目定位

- **陪伴思考，而非代做作业**：以推理链为核心，强调条件 → 依据 → 结论的显式推导。
- **过程可检查**：每一步推理都应能被追溯、被质疑、被验证，而不是只给一个最终答案。
- **知识驱动**：以中学化学的学科知识与规则体系为约束，避免脱离教材与课程标准的「超纲技巧」。

## 目录结构

```
CogniThink-Chem/
├── docs/                       # 规划、决策与设计文档
│   ├── README.md               # 文档索引
│   ├── 01-overall-plan.md      # 整体规划：定位、架构、知识范围、规则优先级、虚拟环境、机理、推理、题库、风险、决策状态
│   ├── 02-modules.md           # 功能模块拆分：职责、输入输出、依赖
│   ├── 03-tasks.md             # 阶段任务清单：优先级、依赖、验收标准、当前进度
│   └── decisions.md            # 决策记录（D1 已定；D2–D10 状态与推荐）
├── data/                       # 声明式数据（JSON）
│   ├── knowledge/knowledge.json    # 覆盖清单：49 个知识点（Main 43 / Extended 6）
│   └── rules/rules.json            # 规则库：37 条（L0–L4）
│       rules/rule-tests.json       # 规则回归用例
├── src/
│   ├── CogniThink.Chem.Core/       # 领域模型与引擎（知识点、规则、仲裁、评测）
│   └── CogniThink.Chem.Cli/        # 演示与自检入口
├── tests/CogniThink.Chem.Tests/    # xUnit 测试
├── CogniThink.Chem.sln
└── README.md
```

## 使用方式

1. 克隆并构建（需要 .NET 8 SDK）：

   ```bash
   git clone https://github.com/hhz2012/CogniThink-Chem.git
   cd CogniThink-Chem
   dotnet build
   dotnet test
   ```

2. 阅读顺序：本 README 了解定位 → [docs/01-overall-plan.md](./docs/01-overall-plan.md) → [docs/02-modules.md](./docs/02-modules.md) → [docs/03-tasks.md](./docs/03-tasks.md)；决策与推荐见 [docs/decisions.md](./docs/decisions.md)。

3. 数据目录可通过环境变量 `COGNITHINK_DATA` 指定；默认从程序目录向上查找仓库内的 `data/`。

## 后续计划

- 完成 T2：世界模型（能级/电离能/水合）与机理引擎（氧化还原、溶解平衡），并把 L0 守恒校验接入真实实现。
- 继续 T3–T5：题目解析、推理 DAG、试剂顺序、题库管线、分级提示。
- D2–D5 确认后，补齐 LLM 适配、题库来源与交互形态。

## 许可

许可证尚未确定（D7 待定），待补充。
