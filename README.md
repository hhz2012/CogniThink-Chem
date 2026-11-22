# 化学伴思 · CogniThink-Chem

化学伴思是一个面向化学学习的「思考陪伴」项目。它不以直接给出答案为终点，而是把解题过程拆解为可检查的推理步骤，让学习者看清每一步「为什么这样想」，从而把知识真正内化为可迁移的思考方式。

> 当前状态：**已从 v0.2 规划长成可评估的求解系统**。D1（C# / .NET 8）已定。已落地：化学原语与结构化推断引擎（Core）、面向自然语言真题的求解网关（Api，引擎为唯一真值源、LLM 只做语言层）、分步导学、能力图谱、诊断教学，以及 757 道 PDF 真题的评估体系。评估基线：解对 621/757，**precision=1、harm=0**；2144 个单元测试全绿。架构与求解栈收敛决策见 [ARCHITECTURE.md](./ARCHITECTURE.md)，下一步路线图见 [结构分析与路线图](./.trae/documents/project-structure-analysis-and-roadmap.md)。`docs/01-03` 为 v0.2 **历史规划**，非现状。

## 项目定位

- **陪伴思考，而非代做作业**：以推理链为核心，强调条件 → 依据 → 结论的显式推导。
- **过程可检查**：每一步推理都应能被追溯、被质疑、被验证，而不是只给一个最终答案。
- **知识驱动**：以中学化学的学科知识与规则体系为约束，避免脱离教材与课程标准的「超纲技巧」。

## 目录结构

```
CogniThink-Chem/
├── ARCHITECTURE.md                 # ★ 现状架构与求解栈收敛决策（先读这个）
├── README.md
├── docs/                           # v0.2 历史规划（已标注历史态，非现状）
│   ├── README.md / 01-03 / decisions.md
├── data/                           # 声明式数据（JSON）
│   ├── highschool/*.json           # 事实库（24 文件）—— 网关选择/判断题真值源
│   ├── knowledge/                  # 反应库 + 知识点
│   ├── rules/rules.json            # 旧规则库（37 条 L0–L4，Core RuleEngine 用）
│   ├── benchmark/problems/         # 基准题（12 类 98 题，Core ProblemSolver 用）
│   └── real-exam/real-exam.json    # 真题（27，Core --verify-real 用）
├── src/
│   ├── CogniThink.Chem.Core/           # 化学原语 + 结构化推断引擎（真值地基）
│   ├── CogniThink.Chem.Cli/            # 演示/自检/导出工具
│   ├── CogniThink.TutorBrain.Api/      # 求解网关 + 导学 + 诊断 + 评估（产品层）
│   │   └── data/pdf_questions.json     # 757 道 PDF 真题（网关主评估集）
│   ├── CogniThink.TutorBrain.EvalRunner/   # 757 题评估运行器
│   └── CogniThink.TutorBrain.PdfOcrRepair/ # PDF OCR 修复工具
├── tests/
│   ├── CogniThink.Chem.Tests/       # Core 测试（911）
│   └── CogniThink.TutorBrain.Tests/ # Api 测试（1233）
├── miniapp/                     # Taro 小程序前端（已接导学 API，待接网关求解）
├── web/                         # 静态演示站（CLI --export-web 生成）
├── .trae/
│   ├── specs/                   # 已交付/进行中的 spec（真正的能力落地记录）
│   └── documents/               # 计划与分析文档（含路线图）
├── data-defect-register.md      # 能力缺口台账
└── CogniThink.Chem.sln
```

## 使用方式

1. 克隆并构建（需要 .NET 8 SDK）：

   ```bash
   git clone https://github.com/hhz2012/CogniThink-Chem.git
   cd CogniThink-Chem
   dotnet build
   dotnet test
   ```

2. 阅读顺序：本 README 了解定位 → [ARCHITECTURE.md](./ARCHITECTURE.md)（现状架构与求解栈）→ [结构分析与路线图](./.trae/documents/project-structure-analysis-and-roadmap.md)（下一步计划）→ [.trae/specs/](./.trae/specs)（已交付能力）。`docs/01-03` 与 `docs/decisions.md` 为 v0.2 历史规划，仅作背景参考。

3. 数据目录可通过环境变量 `COGNITHINK_DATA` 指定；默认从程序目录向上查找仓库内的 `data/`。

## 后续计划

已不再按 v0.2 的 T0–T6 线性推进。当前分四阶段推进，详见 [结构分析与路线图](./.trae/documents/project-structure-analysis-and-roadmap.md)：

- **P0 结构收口**（进行中）：求解栈收敛决策文档、更新文档反映现状、仓库卫生。
- **P1 求解能力补全**：完成 `engine-capability-gap-fix`；立项多部分大题子问分发、纯计算域计算器、自由填空求解器、路由补齐。
- **P2 架构演进**：统一推理 DAG 与结论三态、世界模型最小可用版、图像与文本融合、前端接入网关求解。
- **P3 质量与运营**：数据资产归一、缺口报表自动化、规则/事实体系归一、导学与求解融合。

## 许可

许可证尚未确定（D7 待定），待补充。
