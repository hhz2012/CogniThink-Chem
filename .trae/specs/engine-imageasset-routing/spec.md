# 图形结构化数据接入解题管线 - Product Requirements Document

## Overview
- **Summary**: 让题库中已落地的 `imageAssets.payload`（graph 47 / curve 39 / molecule 42，共 128 资产 / 106 题）真正被解题管线消费：为每类新建或接入确定性推理引擎，接入在线网关与离线评估，并补充结构化结论判分，量化对比"结构化推理 vs 纯 VL 看图"。
- **Purpose**: 图形数据已重绘为结构化真值，但当前管线只把位图丢给 VL 视觉猜测，结构化数据是死数据。接入后用确定性引擎 / 精确数值替代 VL 猜测，提升图形题正确率与可解释性。
- **Target Users**: 在线网关解题服务（生产）+ 评估管线（离线量化）。

## 已确认的范围决策（用户拍板）
- **Q1 处理方式**：三类都新建/接引擎（graph 接现成 Core 引擎并加适配；curve 新建曲线查询引擎；molecule 新建结构分析引擎）。
- **Q2 上线范围**：同时改在线网关 `ProcessStudentMessageAsync`，并加离线评估分支。
- **Q3 评估范围**：先跑有资产的 106 题子集。
- **Q4 判分口径**：为结构化结论补充专门判分（在现有 `IsAnswerCorrect` 基础上增加结构化输出比对）。

## Goals
- 每类图形有一条确定性消费路径：graph→赋值/可行性验证，curve→精确数值查询，molecule→结构特征计算。
- 在线网关能接收 `imageAssets` 并按 type 路由到对应引擎；无资产时行为不变。
- 引擎不能唯一求解时如实返回（不蒙数），不引入 harm。
- 评估管线产出 106 题子集上"结构化推理 vs 纯 VL"的量化对比。

## Non-Goals
- 不重写 VL 提取管线（payload 生产侧已完成）。
- 不改变 `imageAssets` 的数据 schema（消费侧适配既定契约）。
- 不扩展到 crystal / apparatus（无资产、无引擎）。
- 不追求覆盖每一道题的每一种问法——引擎只回答该类图可确定的子问题，最终判断可结合现有 LLM/引擎；无法确定时如实返回。

## Background & Context（勘察证据）

### 数据现状（已运行时验证）
- `pdf_questions.json`：`total=545, withImages=206, withImageAssets=106`，资产 `graph 47 / curve 39 / molecule 42`（反射调用 `GenPdfQuestions` 验证）。
- graph 幻觉双保险已固化（`graph_notflow` 门槛），当前 0 漏网。

### 管线现状（代码证据）
- 网关 [LlmGatewayService.ProcessStudentMessageAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L86) 三步：LLM 解析(带位图) → `CallEngineAsync` 路由 → LLM 生成回复。`CallEngineAsync` 路由不含 graph/curve/molecule。
- `ProcessStudentMessageAsync` 只收 `imagePaths`（位图），不收结构化资产；`ImageAsset` 类型定义在 Evaluation 命名空间（[GatewayVsLlmEvaluationService.cs L9](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L9)），网关层不可见。
- `QaTestCase.ImageAssets` 被加载（L1325）但全仓库无代码读取它（grep 仅 3 处：定义/赋值/PdfQuestionItem 定义）。
- 评估入口 `GET /api/evaluation/gateway-vs-llm` → [EvaluateAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L143)：每题跑纯 LLM(带图) 与网关(带图→引擎)，判分用 `IsAnswerCorrect`（关键词/方向/选项字母）。
- Api 已 ProjectReference 引用 Chem.Core（csproj L20-22），可直接调用 Core 类型。

### 引擎契约与 payload 对齐（勘察）

**payload 结构**（`out/imageAssets_batch.json` 抽样 + 全量统计）：
- graph (47 非空, 292 边, 137 边带 cond)：`{"nodes":[{"id","label"}], "edges":[{"from","to","cond"}]}`。**节点标签多样**：占位标签（甲/乙/丙、A/B/C）、已知物质（PbSO₄、H2O2、CaO2·8H2O）、操作/现象（电解、沉淀、滤渣①、溶液A），需区分。
- curve (39, 34 有标注轴)：`{"xLabel","yLabel","xAxisLabeled","yAxisLabeled","xTicks":[{"value","x"}],"yTicks":[{"value","y"}],"series":[{"name","points":[[x,y]...]}]}`。**点坐标**：有标注轴的轴方向为实际数值（如 pdf-077 x=实际 mol），无标注轴方向为归一化相对位置（0-1，无实际数值意义）。
- molecule (42 valid)：`{"name","smiles","valid":true,"molblock","note","error"}`，无预计算特征。

**Chem.Core 现成引擎**：
- [ProcessFlowEngine.Infer(ProcessFlowRequest{CandidateSpecies, Steps})](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ProcessFlowEngine.cs#L39) → `ProcessFlowResult{Assignment, StepResults, ...}`。`Steps` 的 `From/To` 必须是单字母 A-Z（`IsLabelToken` L207）。
- [OrganicInferenceEngine.Infer(OrganicInferenceRequest{CandidateSpecies, Steps})](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/OrganicInferenceEngine.cs#L24) → 转化链赋值（候选须过白名单）。
- [MoleculeStructureService](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeStructureService.cs)：仅分子式→molblock 固定映射（前端渲染），不解题。
- Chem.Core 已引用 RDKit（`GraphMolWrap`），可在 Core 内新建基于 RDKit 的结构计算。

**真实问法抽样**（106 子集中）：graph 多为选择题——"选项验证转化可行性"（pdf-015）或"先赋值再判断说法"（pdf-018）/成分推断（pdf-064）；curve 多为选择题涉及精确数值（pdf-077 问 n(Cl₂)=0.12 时的浓度比）；molecule 涉及杂化/极性/官能团/酸性比较（pdf-199），部分 molecule 配图与题干计算弱相关（pdf-140）。

## Functional Requirements

### 共享
- **FR-1**: `ImageAsset` 类型移至网关层可见的共享命名空间；`ProcessStudentMessageAsync` 增加可选 `imageAssets` 入参（默认 null，向后兼容）；无资产时整条链路行为与改动前完全一致。
- **FR-2**: 网关 `CallEngineAsync` 增加 graph / curve / molecule 三个 task_type 路由分支，分别调用对应引擎；引擎返回统一 `EngineResponse`（`Ok` 三态 + `FinalResult` + `DerivationSteps` 溯源）。
- **FR-3**: 引擎不能唯一求解 / 数据不足时返回 `Ok=null`（不蒙数），不产生错误结论；沿用"严格真值口径"（`BuildGatewayAnswerAsync` 仅采用可用真值）。

### graph
- **FR-4**: 新增 `GraphSolver`：把 graph payload 适配为 Core 引擎请求。子能力：(a) 候选物种 + 拓扑 → 标签→物质赋值（调 ProcessFlowEngine / OrganicInferenceEngine）；(b) 选项物质组 + 拓扑 → 逐选项验证转化可行性（调 Core 反应可行性/知识库）。
- **FR-5**: 适配层处理节点标签多样性：识别占位标签（甲/乙/丙、A/B/C 等）并映射为单字母；已知物质/操作节点不参与赋值映射。

### curve
- **FR-6**: 新建 `CurveQuery` 引擎（Core 或 Api 层）：输入 curve payload + 查询意图（读某 x 的 y / 找极值点 / 找交点 / 读某点坐标），输出精确结果。有标注轴方向给实际数值（线性插值），无标注轴方向给相对位置并明确标注精度限制。
- **FR-7**: 查询意图由上层（LLM 解析题干或规则）提供；引擎本身只做确定性数值计算，不猜题意。

### molecule
- **FR-8**: 新建 `MoleculeAnalyzer` 引擎（基于 RDKit）：输入 molecule payload（smiles/molblock），输出不饱和度、分子式、官能团列表、中心原子杂化类型（VSEPR）、极性（对称性近似）等结构特征。
- **FR-9**: 结构特征作为精确上下文参与解题（如杂化/极性判断）；不臆造知识库外结论。

### 评估与判分
- **FR-10**: 评估管线对 106 题子集，把 `c.ImageAssets` 传入网关走结构化推理，与纯 VL 路径对比。
- **FR-11**: 补充结构化判分：graph 赋值/选项可行性结果、curve 数值结果、molecule 结构特征结果，分别与标准答案比对（数值带容差，文本带规范化匹配），命中即判正。
- **FR-12**: 评估摘要产出覆盖率（引擎能求解的比例）、结构化正确率、VL 正确率、harm 数，区分每类资产。

## Non-Functional Requirements
- **NFR-1**: `dotnet build` 0 error；运行时反射验证 `withImageAssets=106` 等数量不变。
- **NFR-2**: 引擎结论可溯源（沿用 `InferenceStep`/知识库 id；新建引擎输出计算步骤）。
- **NFR-3**: 不增加现有题目错误率（harm 不上升）；无解宁可空返。
- **NFR-4**: 在线网关改动向后兼容（新参数默认 null = 旧行为）。

## Constraints
- **Technical**: payload schema 既定，消费侧适配。`IsLabelToken` 只认 A-Z。curve 无标注轴方向只有相对值。RDKit 仅能算结构特征，不能替代反应知识库。
- **Business**: 化学引擎是唯一真值源的红线不可破——结构化数据喂给确定性引擎或作为精确输入，禁止 LLM 基于 payload 自行输出化学结论。
- **Dependencies**: Chem.Core 引擎 + 知识库（RX-IN-*/RX-PL-*、白名单、反应可行性规则）；RDKit；VL 端点（评估对比需调 LLM）。

## Assumptions
- graph 占位标签与题干表述一致，可建立映射；已知物质节点可通过化学式/白名单识别。
- curve 查询意图可从题干+选项解析得到（由网关 LLM 解析环节提供查询参数）。
- molecule 的杂化/极性等可由 RDKit + VSEPR 规则近似得到，满足高中题判定需求。
- 106 子集评估的 LLM 调用量与费用可接受。

## Acceptance Criteria

### AC-1: 共享接入层（类型迁移 + 网关参数 + 无资产回归）
- **Type**: `rule`
- **Given**: 无 `imageAssets` 的请求
- **When**: 调用网关
- **Then**: 行为与改动前完全一致（位图 + VL 路径不受影响）
- **Pass Condition**: 无资产题回归运行结果与基线一致；`ImageAsset` 可被网关层引用
- **Evidence**: build 成功 + 回归对比

### AC-2: graph 赋值端到端跑通
- **Type**: `rule`
- **Given**: 一道 graph 题，知识库可唯一赋值
- **When**: 走 GraphSolver 赋值路径
- **Then**: 返回 `Assignment`（标签→物质）与可溯源方程式，`Ok=true`
- **Pass Condition**: 至少 1 道真实题跑通，赋值非空且带溯源步骤
- **Evidence**: 运行日志 + 结论样本

### AC-3: graph 选项验证端到端跑通
- **Type**: `rule`
- **Given**: 一道 graph 选择题（如 pdf-015）
- **When**: 走 GraphSolver 选项验证路径
- **Then**: 逐选项给出可行/不可行判定，能选出正确选项
- **Pass Condition**: 判分命中正确选项字母
- **Evidence**: 运行结果

### AC-4: 引擎无解时不蒙数
- **Type**: `rule`
- **Given**: graph/curve/molecule 任一题，引擎无法唯一确定
- **When**: 走对应引擎
- **Then**: 返回 `Ok=null` 或带"无法确定"的空结论，不计正确
- **Pass Condition**: 无臆造错误结论被判正
- **Evidence**: 边界题运行结果

### AC-5: curve 精确数值查询
- **Type**: `rule`
- **Given**: 一道有标注轴的 curve 题（如 pdf-077）
- **When**: 查询拐点/某点数值
- **Then**: 返回与 payload 一致的精确数值（实际值方向）；无标注轴方向返回相对位置并标注限制
- **Pass Condition**: 查询结果与 payload 点集吻合（数值误差 < 1e-6）
- **Evidence**: 单元测试 + 真实题运行

### AC-6: molecule 结构特征计算
- **Type**: `rule`
- **Given**: 一道 molecule 题（如 pdf-199 SOCl₂）
- **When**: 走 MoleculeAnalyzer
- **Then**: 返回不饱和度、杂化类型、极性等特征，与化学事实一致
- **Pass Condition**: SOCl₂ 杂化=sp³、极性等特征正确
- **Evidence**: 单元测试断言 + 真实题运行

### AC-7: 评估产出 106 子集量化对比
- **Type**: `rule`
- **Given**: 评估入口运行
- **When**: 在 106 有资产题子集上完成
- **Then**: 摘要含每类资产的覆盖率、结构化正确率、VL 正确率、harm 数
- **Pass Condition**: 摘要字段齐全，能区分结构化命中与 VL 命中
- **Evidence**: 评估接口输出 / 报告文件

### AC-8: 结构化判分生效
- **Type**: `rule`
- **Given**: 引擎返回结构化结论
- **When**: 判分
- **Then**: 赋值/数值/特征与标准答案比对命中即判正（数值带容差）
- **Pass Condition**: 至少 1 道题通过结构化判分命中正确（区别于纯关键词）
- **Evidence**: 判分日志

### AC-9: 构建与运行时无回归
- **Type**: `rule`
- **Given**: 全部改动完成
- **When**: `dotnet build` + 运行时反射加载
- **Then**: 0 error，`total=545/withImageAssets=106` 不变
- **Pass Condition**: build 0 error，数量与基线一致
- **Evidence**: build 输出 + 反射验证输出

## Open Questions（已全部由用户决策关闭，记录备查）
- [x] Q1 三类处理方式 → 三类都新建/接引擎
- [x] Q2 上线范围 → 同时改在线网关
- [x] Q3 评估范围 → 106 题子集
- [x] Q4 判分口径 → 补充结构化判分
