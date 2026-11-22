# Spec — P2-4 前端接入网关求解，端到端闭环（最小可用演示）

> 关联路线图：[project-structure-analysis-and-roadmap.md](file:///d:/github/CogniThink-Chem/.trae/documents/project-structure-analysis-and-roadmap.md) §P2-4。
> 基线：`after_p23`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / **不泄露未采纳结论** / 事实只存 JSON。破线即回退。
> 诚实口径：本 spec **预期 eval 正确性增益 = 0**。P2-4 是前端接入 + 服务端只读投影，不改引擎求解、不改采纳门、不改判分；价值 = 把 P2-1/P2-2/P2-3 已产出的结论三态 / 推理 DAG / 趋势解释端到端展示出来，并在学生面强制「不泄露未采纳结论」。

## 1. 背景与目标

路线图 P2-4：「在 miniapp（或 web）增加『题目求解』入口：调用 `/api/gateway/process`，展示引擎结论 + 分级讲解 + 推理节点（P2-1 的 DAG）。验收：一条完整链路（输入题目 → 求解 → 分级提示 → 复盘）可在前端演示；不泄露未采纳结论。」依赖 P2-1（已交付）。

P2-1/P2-2/P2-3 已在运行时网关路径上产出可观测元数据，但当前：
- 前端 `miniapp` 尚未调用网关求解接口（只调导学 `/api/problem`、`/api/tutor/submit-step`）；
- 网关 `/api/gateway/process` 返回**完整** `GatewayResult`（含 `engine_response.final_result` 等全部字段），若前端直接展示，会把「信息不足 / 能力缺口」（`ConclusionState=Insufficient`）时的 `final_result` 当作答案露出 —— 违反「不泄露未采纳结论」。

本 spec 目标：在 miniapp 新增「题目求解」页，调用一个**学生面投影端点**（按 `ConclusionState` 裁剪字段），端到端演示「输入 → 求解 → 分级揭示 → 复盘」，并从服务端源头保证未采纳结论不泄露。

## 2. T0 只读审计（证据）

| 证据 | 位置 | 结论 |
|------|------|------|
| 网关主入口已存在 | [GatewayController.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/GatewayController.cs) `POST /api/gateway/process` | 返回完整 `GatewayResult`（`task_type` / `engine_request` / `engine_response` / `reply`） |
| 运行时已派生三态 + 富化解释 | [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) L123 / L127 | `ProcessStudentMessageAsync` 已调 `ConclusionStateDeriver.Derive` 与 `WorldModelEnricher.Enrich`；`engine_response` 含 `conclusion_state` / `reasoning_nodes` / `explanation_nodes` / `result_provenance` |
| 三态契约 | [ReasoningContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs) | `ConclusionState { Confirmed, CandidateSet, Insufficient }`；`Confirmed` 含 Ok=true（唯一结论）与 Ok=false（确定判错）；`Insufficient` = 原静默 null |
| 教学话术已门控 | [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) `GenerateReplyAsync` L2758 | 按 NeedMoreCondition / Ok=null（拒答不蒙数）/ Ok=true / Ok=false 分支；不输出确定性化学结论（concept 除外，已标记非真值） |
| miniapp 现状 | [tutorApi.ts](file:///d:/github/CogniThink-Chem/miniapp/src/services/tutorApi.ts) / [app.config.ts](file:///d:/github/CogniThink-Chem/miniapp/src/app.config.ts) | Taro 4（React/TS），target 5080，有 `build:h5`；现有页 home/atlas/bank/mine/challenge/tutor；`tutorApi` 未调 gateway（grep 仅 package-lock 命中） |
| 后端 CORS 已开 | [Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Program.cs) L90-91 / L124 | `AllowAnyOrigin`，H5 跨域可用；`JsonStringEnumConverter` 已注册（三态序列化为字符串） |
| API 端口 | [launchSettings.json](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Properties/launchSettings.json) | `http://localhost:5080` = miniapp `TUTOR_API_BASE`，同一后端 |
| eval 分布（运行时等价） | `after_p23` checkpoint | Confirmed=641（Ok:true 615 + Ok:false 26）/ Insufficient=116 / CandidateSet=0；correct=adopted=635，harm=0 |

**关键风险（诚实披露）**：eval 中存在 **6 个 `ConclusionState=Confirmed` 但未被答案键采纳（答错）** 的题。运行时无答案键，无法在服务端识别这 6 个。对策：
1. 对 `Insufficient`（116 题）从服务端源头不下发 `final_result` 作为答案 —— 可 100% 拦截「能力缺口却露答案」；
2. 对 `Confirmed` 结论，前端**诚实标注**「引擎确定结论（已通过引擎确定性校验）」而非「标准答案」，不宣称 100% 正确；
3. 这 6 个残余属 precision=1 红线的 eval 期保证（系统不采纳错），运行时无法消除，已在台账登记为已知残余，不在本 spec 范围。

## 3. 设计

### 3.1 服务端：新增学生面投影端点 `/api/gateway/solve`（加性，只读）

- 新建 `StudentSolveView` DTO（加性，不修改 `GatewayResult` / `EngineResponse`）：
  - `conclusion_state: "Confirmed" | "CandidateSet" | "Insufficient"`
  - `task_type: string`
  - `final_result: string?` —— **仅当 `Confirmed` 时下发**；`CandidateSet`/`Insufficient` 为 null
  - `reasoning_nodes: ReasoningNode[]` —— **仅当 `Confirmed` 时下发**（DAG 溯源）
  - `candidates: string[]` —— `CandidateSet` 时下候选（当前恒空，预留）
  - `explanation_nodes: ReasoningNode[]` —— `Confirmed`/`CandidateSet` 时下，前端标注「趋势级，帮助理解，不作判定依据」；`Insufficient` 时为空
  - `reply: string` —— 教学话术（`GenerateReplyAsync` 已门控，始终下发）
  - `constraint_note: string?` —— `Insufficient` 时透传，提示缺什么条件
  - `result_provenance: string?` —— 透传（观测用，P2-3）
- 投影逻辑（纯函数，可单测）：`GatewayResult → StudentSolveView`，按 `ConclusionState` 裁剪。
- 新端点 `POST /api/gateway/solve`，入参同 `/process`（`{ text }`），内部调 `ProcessStudentMessageAsync` 后投影返回。
- **不动** `/api/gateway/process`（保持完整 `GatewayResult` 供调试/评估）；**不动** `ProcessStudentMessageAsync` / `BuildGatewayAnswerAsync` / `IsStructuredCorrect` / 任何求解与判分代码。

### 3.2 前端：miniapp 新增「题目求解」页（`pages/solve`）

- `tutorApi.ts` 加 `solve(text): Promise<StudentSolveView>`，调 `POST /api/gateway/solve`。
- 新增 `miniapp/src/types/` 或就地定义 `StudentSolveView` TS 类型（镜像 DTO）。
- 新增页 `miniapp/src/pages/solve/index.tsx` + `index.config.ts`，注册进 `app.config.ts`。
- 页面交互（分级揭示 = 分级讲解 + 复盘）：
  1. 输入题干 → 调用 solve；
  2. 结论卡：`Confirmed` → 显示 `final_result`，标注「引擎确定结论」；`Insufficient` → 显示「信息不足 / 能力缺口，无法给出确定答案」+ `constraint_note`，**不显示任何 final_result**；`CandidateSet` → 显示候选列表（预留）；
  3. 渐进揭示推理 DAG：点击「展开推理过程」→ 渲染 `reasoning_nodes`（step / description / premises / rule_id 或 knowledge_id / conclusion / evidence_level）；
  4. 趋势解释：`explanation_nodes` 折叠区，明确标注「帮助理解，不作判定依据」；
  5. 教学话术 `reply` 展示；
  6. 复盘：完整链路（输入 → 结论 → DAG → 解释）可在同页回看。
- 入口：在 `pages/home` 加「题目求解」入口跳转 `pages/solve`（或 tab 注册，取最小改动）。
- **演示目标端**：`npm run dev:h5`（浏览器），免小程序审核；同时不破坏 weapp/tt 构建。

### 3.3 不改的边界（红线）

- 不改 `data/**` 任何 JSON（CQ-5 零数据改动）。
- 不改引擎求解器、`CallEngineAsync` 分支判决、`FinalResult`/`Ok`/`TaskType` 赋值。
- 不改采纳门 `BuildGatewayAnswerAsync` / `IsStructuredCorrect` / eval 判分。
- 不新增化学事实硬编码到 `.cs` / `.tsx`。
- 不把 `explanation_nodes`（Trend）当作判定依据展示。

## 4. 关键决策点（CQ，需用户批准）

| ID | 问题 | 选项 | 推荐 | 理由 |
|----|------|------|------|------|
| CQ-1 | 目标端 | a miniapp 新增 pages/solve，H5 演示 / b 新建独立 web 页 / c 改造 chem-home.html | **a** | 复用 miniapp 骨架 + tutorApi + build:h5，免审核，成本最低；与路线图 §6#4 「miniapp 接入成本低」一致 |
| CQ-2 | 不泄露未采纳结论的实现位置 | a 服务端投影端点 `/solve` 按 ConclusionState 裁剪（推荐）/ b 仅前端门控（原始字段仍下发） | **a** | 纵深防御：服务端不向学生面下发 Insufficient 的 final_result，从源头保证；eval 走进程内不走 HTTP，零影响 |
| CQ-3 | 求解输入范围 | a 纯文本题干（MVP，与 /process 一致）/ b 支持图片上传（覆盖 129 图像题） | **a** | 图像题需 imageAssets 注入，运行时 HTTP 未暴露；本 spec 不膨胀，留 P2-4b 或后续 |
| CQ-4 | 是否动 /process 现有返回 | a 不动 /process，新增 /solve（推荐）/ b 改 /process 返回投影 | **a** | 零破坏既有契约；/process 保留完整结构供调试 |
| CQ-5 | 数据 | 零数据改动，不新增 JSON | **已批准** | 审计证实无新增事实需求 |

## 5. 验收标准（AC）

- **AC-1**：`dotnet build` 0 error（P2-4 触及文件 0 新警告，既有警告不计）。
- **AC-2**：后端单测覆盖投影门控：`Confirmed`（含 Ok=true 与 Ok=false）→ 含 `final_result` + `reasoning_nodes`；`Insufficient` → `final_result` 为 null / 不含；`CandidateSet` → 含 `candidates`、不含 `final_result`；`explanation_nodes` 在 `Insufficient` 为空。
- **AC-3**：全量 eval `after_p24`：757/757 failed=0，correct=635；`diff_ckpt after_p23 after_p24` GAINED/LOST/WORSEN/harm 全 0；`diff_ckpt base_p0 after_p24` LOST/WORSEN/harm=0（GAINED 维持 6 携带）。证明后端求解/判分零漂移。
- **AC-4**：前端 `pages/solve` 可在 `dev:h5` 运行：输入一道 eval 已解对的纯文本题 → 展示结论卡 + 渐进 DAG + 标注解释 + 教学话术，链路完整可演示。
- **AC-5**：不泄露未采纳结论：对一道 `Insufficient` 题（eval 中 Ok=null / 未采纳），`/solve` 响应不含 `final_result` 答案，前端不展示任何确定答案；`Confirmed` 结论标注「引擎确定结论」非「标准答案」。
- **AC-6**：台账 `data-defect-register.md` 新增 §Q（P2-4 触及范围 + 投影门控策略 + 已知 6 个 Confirmed-未采纳残余登记）。
- **AC-7**：T7 fresh-context 只读子代理 review → PASS。

## 6. Non-Goals

- 不做图片上传与图像题求解（留后续）。
- 不做新的后端求解能力（不改引擎）。
- 不做导学流 submit-step 与求解的融合（那是 P3-3）。
- 不做小程序发布/审核（仅 dev:h5 演示）。
- 不消除 6 个 Confirmed-未采纳残余（需答案键，运行时不可得；属 eval 期 precision 保证范畴）。

## 7. 风险与回退

- 前端构建（Taro）在本环境是否可跑：若 `dev:h5` 依赖缺失，AC-4 退化为「前端代码 + 类型编译通过 + 用 `/solve` 的 curl/Swagger 响应截图演示」，不阻塞后端红线验证。
- LLM 在演示环境是否可用：`GenerateReplyAsync` 与 `ParseStudentInputAsync` 有 rule/keyword 兜底，LLM 关闭时仍可走确定性路径演示已解对题（演示题优先选路由确定性高的 choice/equilibrium 文本题）。
- 回退：若 AC-3 diff 非 0 → 必是 T1 意外改了求解/判分路径，立即回退该改动，复跑。
