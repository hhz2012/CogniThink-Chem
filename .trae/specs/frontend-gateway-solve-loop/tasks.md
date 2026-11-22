# Tasks — P2-4 前端接入网关求解，端到端闭环（最小可用演示）

> 关联 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/frontend-gateway-solve-loop/spec.md)
> 基线：`after_p23`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / **不泄露未采纳结论** / 事实只存 JSON。破线即回退。
> 诚实口径：预期 eval 正确性增益 = 0（前端 + 服务端只读投影，不改求解/判分）。
> 批准门禁：CQ-1=a / CQ-2=a / CQ-3=a / CQ-4=a / CQ-5 零数据改动 经用户确认后，才开始 T1 实现代码。

## Task Breakdown

| ID | 任务 | 状态 | 产出 |
|----|------|------|------|
| T0 | 只读审计：网关端点/运行时三态富化/miniapp 现状/CORS/eval 分布/泄露风险 | completed（spec 阶段已实证） | 证据入 spec §2 |
| T1 | 服务端加性：`StudentSolveView` DTO + 纯函数投影（按 ConclusionState 裁剪）+ `POST /api/gateway/solve` 端点 + 门控单测 | completed | [GatewaySolveProjection.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewaySolveProjection.cs) + [GatewayController.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/GatewayController.cs) `/solve`；[GatewaySolveProjectionTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/GatewaySolveProjectionTests.cs) 5/5 PASS；全量 2221/0；build 0 error |
| T2 | 前端：`tutorApi.solve` + `StudentSolveView` TS 类型 | completed | [tutorApi.ts](file:///d:/github/CogniThink-Chem/miniapp/src/services/tutorApi.ts) 加 `StudentSolveView`/`ReasoningNodeDto` 类型 + `solve()` |
| T3 | 前端：`pages/solve` 页面（结论卡门控 + 渐进 DAG + 标注解释 + 话术 + 复盘）+ home 入口 + app.config 注册 | completed | 新增 [pages/solve](file:///d:/github/CogniThink-Chem/miniapp/src/pages/solve/index.tsx)；改 [app.config.ts](file:///d:/github/CogniThink-Chem/miniapp/src/app.config.ts)、[home/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/pages/home/index.tsx)；tsc 对触及文件 0 错误 |
| T4 | 联调演示 + 全量 eval `after_p24` + 两次红线 diff（after_p23→after_p24 全 0；base_p0→after_p24 LOST/WORSEN/harm=0）+ 泄露断言（Insufficient 不下发 final_result） | completed | `after_p24` 757/757 correct=635；两次 diff RED-LINE PASS；[p24_solve_probe.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p24_solve_probe.py) 不泄露 PASS |
| T5 | 台账闭环：新增 §Q（P2-4 触及范围 + 投影门控策略 + 6 个 Confirmed-未采纳残余登记） | completed | [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) §Q（L739-786） |
| T6 | 红线自检：密钥扫描 / 未改 data/** / 未改求解判分采纳门 / 投影只读不改 EngineResponse / Insufficient 响应无 final_result / eval diff 全 0 / 不蒙数 | completed | [p24_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p24_self_check.py) **13/13 PASS** |
| T7 | fresh-context 只读子代理独立 review → review.md，裁决 PASS/FAIL | completed | [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/frontend-gateway-solve-loop/review.md) — PASS（10/10，0 Issue） |

---

## T0 — 只读审计（spec 阶段已完成）
**Status**: completed — 证据入 spec §2：`/api/gateway/process` 已存在并返回完整 `GatewayResult`；运行时 `ProcessStudentMessageAsync` 已派生 `ConclusionState`（P2-1）与富化 `ExplanationNodes`（P2-2）；miniapp（Taro 4）target 5080、有 `build:h5`、`tutorApi` 未调 gateway；后端 CORS 已开；after_p23 分布 Confirmed 641 / Insufficient 116 / CandidateSet 0，correct=adopted=635，harm=0；残余风险 = 6 个 Confirmed 但 eval 未采纳（运行时无答案键不可识别），对策 = Insufficient 服务端不下发 final_result + Confirmed 诚实标注。

## T1 — 服务端学生面投影端点
**Status**: completed — 新建 [GatewaySolveProjection.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewaySolveProjection.cs)：`StudentSolveView` DTO（加性）+ `GatewaySolveProjector.Project` 纯函数（按 ConclusionState 裁剪，只读不改输入）；[GatewayController.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/GatewayController.cs) 新增 `POST /api/gateway/solve`（复用 ProcessStudentMessageAsync + 投影，不动 /process）。单测 [GatewaySolveProjectionTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/GatewaySolveProjectionTests.cs) 5/5 PASS（Confirmed Ok=true / Confirmed Ok=false / Insufficient 不泄露 / CandidateSet / 纯只读不改输入）。全量 2221 passed / 0 failed；build 0 error，P2-4 触及文件 0 新警告。
**Outputs**:
- 新建 `StudentSolveView` DTO（加性；不修改 `GatewayResult`/`EngineResponse`），字段见 spec §3.1。
- 新建纯函数投影 `GatewayResult → StudentSolveView`（建议放 `LlmGatewayService` 旁或独立 `GatewaySolveProjector`），按 `ConclusionState` 裁剪：`Confirmed` 含 `final_result`+`reasoning_nodes`+`explanation_nodes`；`CandidateSet` 含 `candidates`（当前空）不含 `final_result`；`Insufficient` 不含 `final_result`/`reasoning_nodes`，`explanation_nodes` 为空，透传 `constraint_note`。`reply`/`task_type`/`result_provenance` 始终透传。
- 新端点 `POST /api/gateway/solve`（`GatewayController`），入参 `{ text }`，内部调 `ProcessStudentMessageAsync` 后投影返回。**不动** `/api/gateway/process`。
- 新建单测（建议 `GatewaySolveProjectionTests.cs`）覆盖：Confirmed(Ok=true) / Confirmed(Ok=false) / Insufficient / CandidateSet 四种投影输出；断言 Insufficient 响应 `final_result` 为 null 且不含。
**Constraints**: 纯加性 + 只读；不改 `ProcessStudentMessageAsync`/`BuildGatewayAnswerAsync`/`IsStructuredCorrect`/求解分支；不硬编码化学事实。
**Verification**: build 0 error；新单测全绿；全量 test 0 failed（期望 2216 + N）。

## T2 — 前端 API 客户端
**Status**: completed — [tutorApi.ts](file:///d:/github/CogniThink-Chem/miniapp/src/services/tutorApi.ts) 新增 `ReasoningNodeDto` / `StudentSolveView` 接口（镜像后端 DTO，`conclusion_state` 为联合字面量类型）+ `solve(text)` 方法（`POST /api/gateway/solve`）；不动既有方法签名。tsc 对该文件 0 类型错误。
**Outputs**:
- `miniapp/src/services/tutorApi.ts` 加 `solve(text: string): Promise<StudentSolveView>`，调 `POST /api/gateway/solve`。
- 新增/就地定义 `StudentSolveView` TS 类型，字段镜像后端 DTO（`conclusion_state` 联合字面量类型）。
**Constraints**: 不改既有 tutorApi 方法签名。

## T3 — 前端「题目求解」页
**Status**: completed — 新增 [pages/solve/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/pages/solve/index.tsx) + `index.config.ts` + `index.module.scss`：输入 → solve → 结论卡（按 conclusion_state 门控，Confirmed 标注「引擎确定结论·仅供参考」，Insufficient 显示「信息不足/能力缺口」绝不展示 final_result）→ 渐进揭示 reasoning DAG → 标注「趋势级·不作判定依据」的解释折叠区 → reply 话术 → 同页复盘；[app.config.ts](file:///d:/github/CogniThink-Chem/miniapp/src/app.config.ts) 注册页面；[home/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/pages/home/index.tsx) 加入口。`tsc --noEmit` 对触及文件 0 错误（node_modules/@tarojs 的 .d.ts 报错为 node v26 环境 ABI 不兼容，与本次改动无关；`build:h5` 因 `@tarojs/plugin-doctor` 原生二进制无法加载被环境阻塞，非代码缺陷，按 spec AC-4 回退用 `/solve` HTTP 响应佐证）。
**Outputs**:
- 新建 `miniapp/src/pages/solve/index.tsx` + `index.config.ts`，注册进 `app.config.ts`。
- 页面：输入框 → 调用 solve → 结论卡（按 conclusion_state 门控展示，Confirmed 标注「引擎确定结论」，Insufficient 显示「信息不足/能力缺口」绝不展示 final_result）→ 渐进揭示 `reasoning_nodes` DAG → `explanation_nodes` 折叠区标注「帮助理解，不作判定依据」→ `reply` 话术 → 同页复盘回看。
- `pages/home` 加「题目求解」入口跳 `pages/solve`（最小改动）。
**Verification**: `dev:h5` 可运行；若环境缺依赖则退化为 TS 类型编译通过 + 用 `/solve` 的 curl/Swagger 响应佐证（记录于 review 证据）。

## T4 — 联调 + eval + 红线 diff
**Status**: completed — eval `after_p24`：757/757 failed=0，correct=635，precision=1，harm=0。diff `after_p23`→`after_p24` GAINED/LOST/WORSEN/harm 全 0 → RED-LINE PASS（后端求解/判分零漂移）；diff `base_p0`→`after_p24` GAINED=6（P1-4 携带）/ LOST=0 / WORSEN=0 / harm=0。端到端 HTTP 演示：API Production（LLM off）起于 5080，[p24_solve_probe.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p24_solve_probe.py) POST `/api/gateway/solve`：Confirmed 题 ion-1 → `final_result='可以大量共存'` 正常下发；Insufficient 题 trap-limit-3 → `final_result=null`、reasoning/explanation 为空、`constraint_note` 透传 → 不泄露 PASS。响应留存 [p24_solve_confirmed.json](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p24_solve_confirmed.json) / [p24_solve_insufficient.json](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p24_solve_insufficient.json)。
**Outputs**:
- 选一道 eval 已解对的纯文本题（choice/equilibrium/concept）作演示样例，`/solve` 端到端走通，留存响应/截图证据。
- `run_eval_resume.ps1 -Tag after_p24 -Env Production` → 757/757 failed=0，correct=635。
- `diff_ckpt after_p23 after_p24`：GAINED/LOST/WORSEN/harm 全 0 → RED-LINE PASS。
- `diff_ckpt base_p0 after_p24`：LOST/WORSEN/harm=0（GAINED 维持 6 携带）。
- 泄露断言：对一道 Insufficient 题，`/solve` 响应 `final_result` 为 null（脚本化核对）。
**Verification**: 全过。若 diff 非 0 → 停下根因（必是 T1 意外改了求解/判分），修后复跑。

## T5 — 台账闭环
**Status**: completed — [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §Q（L739-786，插于 §P 与 `## 处理原则` 之间）：Q.1 触及文件清单（content attestation，无 data/**）；Q.2 投影门控策略表（按 ConclusionState 下发字段矩阵 + 运行时 HTTP 实证）；Q.3 eval 零漂移；Q.4 已知残余（6 个 Confirmed 但未采纳，运行时无答案键不可识别，登记为非本 spec 范围）。
**Outputs**: [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §Q（插入于 §P 与 `## 处理原则` 之间）：
- Q.1 触及文件清单（content attestation，无 data/**）。
- Q.2 投影门控策略表（按 ConclusionState 下发字段矩阵）。
- Q.3 已知残余：6 个 Confirmed 但 eval 未采纳，运行时无答案键不可识别，登记为已知非本 spec 范围。

## T6 — 红线自检
**Status**: completed — [p24_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p24_self_check.py) **13/13 PASS**：密钥扫描 0 命中 / 未触及 data/** / 逐 case verdict（gatewayAnswer/Ok/Conclusion/Correct/Adopted）after_p23→after_p24 757 题全相同 / adopted∧¬correct=0 / correct=635 / 投影无对 resp./EngineResponse 写回 / Insufficient 响应 final_result=null 且无 reasoning/explanation 泄露 / Confirmed 正常下发 / 表面积仅加性无意外删除。
**Verification**（脚本化 `p24_self_check.py`，结果入 review 证据）：
- 密钥扫描：P2-4 改动文件 → 0 命中。
- 未改数据：触及文件均不在 `data/` 下（CQ-5）。
- 未改求解判分：`ProcessStudentMessageAsync` / `BuildGatewayAnswerAsync` / `IsStructuredCorrect` 方法体未变（checkpoint 逐 case `gatewayAnswer`/`gatewayOk`/`gatewayConclusion` 与 after_p23 diff=0）。
- 投影只读：投影函数不修改传入的 `EngineResponse`（单测覆盖或 grep 无写回）。
- 不泄露：`/solve` 对 Insufficient 响应无 `final_result`。
- 不蒙数：`after_p24` correct=635，harm=0。
- 表面积：仅加性 DTO + 投影 + 端点 + 前端 + 测试 + 台账 + scratch。

## T7 — 独立 review
**Status**: completed — fresh-context 只读子代理独立复跑 build（0 error）/ 全量 test（2221 passed / 0 failed）/ 新单测（5/5）/ 两次红线 diff（after_p23→after_p24 全 0；base_p0→after_p24 LOST/WORSEN/harm=0，GAINED 维持 6）/ p24_self_check（13/13 PASS）/ 泄露证据直读（Insufficient final_result=null 且无 reasoning/explanation）/ 代码审查 a-d（投影纯只读、/process 未动、未触 data/**、前端门控正确）/ tsc（触及文件 0 错误）→ 裁决 **PASS（10/10 维度，0 Issue，无修复项）**。报告落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/frontend-gateway-solve-loop/review.md)。
**Process**: 委托 fresh-context **只读** 子代理，独立复跑：build / test / eval diff（两次）/ 投影门控断言 / 泄露断言 / 密钥扫描 / 台账核对。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 不泄露未采纳结论 / 未改数据与判决 / 加性只读。任一 FAIL → Issue → 修复 → 复审。子代理仅返回报告文本，由主代理落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/frontend-gateway-solve-loop/review.md)。

---

## 决策日志（CQ）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | 目标端 | a miniapp 新增 pages/solve，H5 演示 / b 独立 web 页 / c 改 chem-home.html | **a（已批准）** | 见 spec §4 CQ-1 |
| CQ-2 | 不泄露实现位置 | a 服务端投影端点裁剪 / b 仅前端门控 | **a（已批准）** | 见 spec §4 CQ-2 |
| CQ-3 | 求解输入范围 | a 纯文本 MVP / b 含图片上传 | **a（已批准）** | 见 spec §4 CQ-3 |
| CQ-4 | 是否动 /process | a 不动，新增 /solve / b 改 /process | **a（已批准）** | 见 spec §4 CQ-4 |
| CQ-5 | 数据 | 零数据改动，不新增 JSON | **已批准** | 见 spec §4 CQ-5 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | fresh-context 只读子代理 | PASS（10/10，0 Issue） | 无修复项；见 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/frontend-gateway-solve-loop/review.md) |
