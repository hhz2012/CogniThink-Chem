# P2-4 独立只读审查报告 — 前端接入网关求解，端到端闭环

**审查角色**：fresh-context 只读代码审查员（未修改/创建/删除任何文件，未 git commit）
**审查对象**：spec `frontend-gateway-solve-loop`（P2-4）
**审查日期**：2026-11-22
**基线**：`after_p23`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）；红线锚点 `base_p0`（629 正确）

---

## 一、各验证步骤实际命令输出摘要

### 1. Build（AC-1）
**命令**：`dotnet build CogniThink.Chem.sln -c Release --nologo`
**结果**：退出码 0。
```
已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:04.43
```
**判定**：0 error ✓；P2-4 新文件（GatewaySolveProjection.cs / GatewayController.cs / GatewaySolveProjectionTests.cs）0 新警告 ✓（增量构建未重放既有警告，但新文件无可归因警告）。

### 2. 全量测试（AC-2 支撑）
**命令**：`dotnet test CogniThink.Chem.sln -c Release --nologo --no-build`
**结果**：退出码 0。
- `CogniThink.TutorBrain.Tests.dll`：通过 1308，失败 0，跳过 0（37 s）
- `CogniThink.Chem.Tests.dll`：通过 913，失败 0，跳过 0（38 s）
- **合计 2221 passed / 0 failed**
**判定**：0 failed ✓；总通过数 2221 ≥ 2221 ✓。

### 3. 新单测（AC-2）
**命令**：`dotnet test CogniThink.Chem.sln -c Release --nologo --no-build --filter "FullyQualifiedName~GatewaySolveProjectionTests"`
**结果**：退出码 0。
```
No test matches the given testcase filter ... in CogniThink.Chem.Tests.dll   ← 无害（符合预期）
已通过! - 失败: 0，通过: 5，已跳过: 0，总计: 5 - CogniThink.TutorBrain.Tests.dll
```
**判定**：5 passed ✓（TutorBrain.Tests 承载）；Chem.Tests 的 "No test matches" 无害 ✓。

### 4. 红线 diff（两次，AC-3）
**命令 A**：`python _evalscratch\verify\diff_ckpt.py after_p23 after_p24`
```
[after_p23 vs after_p24] old=757(skip 0) new=757(skip 0)
GAINED (0): []   LOST (0): []   WORSEN null->new-harm (0): []   IMPROVE harm->cleared (0): []
harm_old=0  harm_new=0
RED-LINE: harm=0 & lost=0 & worsen=0 -> PASS
```
**命令 B**：`python _evalscratch\verify\diff_ckpt.py base_p0 after_p24`
```
[base_p0 vs after_p24] old=757(skip 0) new=757(skip 0)
GAINED (6): ['pdf-415', 'pdf-530', 'pdf-535', 'pdf-542', 'pdf-543', 'trap-limit-2']
LOST (0): []   WORSEN (0): []   harm_old=0  harm_new=0
RED-LINE: harm=0 & lost=0 & worsen=0 -> PASS
```
**判定**：前者 GAINED/LOST/WORSEN/harm 全 0 ✓；后者 LOST/WORSEN/harm=0、GAINED 维持 6（P1-4 携带，题目清单与台账 Q.3 一致）✓。后端求解/判分零漂移。

### 5. 自检（T6）
**命令**：`python _evalscratch\verify\p24_self_check.py`
**结果**：退出码 0，**13/13 PASS**，末尾 `SELF-CHECK: ALL PASS`。
覆盖项：密钥扫描 0 命中 / 未触及 data/** / 逐 case verdict 不变（common=757 all identical）/ adopted∧¬correct=0 / correct=635 / 投影无写回 / Insufficient 响应 conclusion_state=Insufficient & final_result=null & reasoning_nodes 空 & explanation_nodes 空 / Confirmed 响应 conclusion_state=Confirmed & final_result 非空 / 表面积仅加性无意外删除。
**判定**：13/13 PASS ✓。

### 6. 不泄露证据（独立直读，AC-5）
独立读取两个留存响应 JSON（不依赖自检脚本结论）：
- `_evalscratch/verify/p24_solve_insufficient.json`：
  - `conclusion_state` = `"Insufficient"` ✓
  - `final_result` = `null` ✓
  - `reasoning_nodes` = `[]` ✓
  - `explanation_nodes` = `[]` ✓
  - `constraint_note` = `"【高中限定】当前计算引擎仅支持部分定量计算场景，此题需人工分析"`（透传）✓
  - 无任何确定答案泄露 ✓
- `_evalscratch/verify/p24_solve_confirmed.json`：
  - `conclusion_state` = `"Confirmed"` ✓
  - `final_result` = `"可以大量共存"`（非空）✓
**判定**：Insufficient 不下发 final_result/reasoning/explanation ✓；Confirmed 正常下发 final_result ✓。

### 7. eval 汇总（独立直读，支撑「不蒙数」）
`_evalscratch/checkpoint_after_p24_summary.txt`：
```
tag=after_p24 total=757 llmAvailable=False
gatewayCorrect=635 gatewayTruthCorrect=635
gatewayEngineUsed=639 engineSolved=635 coverage=0.8388 precision=1
harmCount=0
bothCorrect=0 gatewayOnly=635 pureLlmOnly=0 bothWrong=122
```
**判定**：correct=635 ✓；precision=1 ✓；harmCount=0 ✓；`pureLlmCorrect=0`、`engineSolved=635`、`bothWrong=122` 未被蒙数（122 错题为未解/未采纳，非猜测采纳，harm=0）✓。

---

## 二、代码审查发现（只读）

### 7a. `src/CogniThink.TutorBrain.Api/Application/Gateway/GatewaySolveProjection.cs`
- `StudentSolveView` DTO 为**加性**新类，不修改 `GatewayResult` / `EngineResponse`。
- `GatewaySolveProjector.Project(GatewayResult)` 为**纯只读函数**：仅读取 `result.EngineResponse` 字段构造新 `view`；全文无对 `resp.*` / `result.EngineResponse.*` 的写回赋值（自检正则 0 命中，源级复核一致）。
- 按 `ConclusionState` 裁剪正确：
  - `Confirmed`：下发 `FinalResult` + `ReasoningNodes` + `ExplanationNodes`（含 Ok=true 与 Ok=false 的确定判错）✓
  - `CandidateSet`：`Candidates`（当前 `new List<string>()` 空，预留）+ `ExplanationNodes`；**不**设置 `FinalResult`（保持默认 null）✓
  - `Insufficient`（含 `default` 兜底）：仅透传 `ConstraintNote`；`FinalResult` 保持 null、`ReasoningNodes`/`ExplanationNodes` 保持默认空列表 ✓
- `reply` / `task_type` / `result_provenance` 始终透传 ✓。
- 备注（非 Issue）：`view.ReasoningNodes = resp.ReasoningNodes` 为引用共享而非深拷贝，但投影函数本身不修改输入，且控制器投影后 `result` 即被丢弃、`view` 仅用于 JSON 序列化，无后续突变路径；单测 `Projector_DoesNotMutateInputEngineResponse` 已覆盖输入不变性。不构成红线问题。

### 7b. `src/CogniThink.TutorBrain.Api/Api/Controllers/GatewayController.cs`
- **仅新增** `POST /api/gateway/solve`（`Solve` 方法）：入参复用 `ProcessRequest`，内部调 `ProcessStudentMessageAsync` 后 `GatewaySolveProjector.Project(result)` 返回。
- `POST /api/gateway/process`（`Process` 方法）**未改动**，仍返回完整 `GatewayResult`（供调试/评估）✓。
- `Parse` / `GetPrompts` 未动 ✓。
- 投影类型引用范围 grep 确认：`GatewaySolveProjector` / `StudentSolveView` 仅出现在 GatewaySolveProjection.cs（定义）、GatewayController.cs（使用）、GatewaySolveProjectionTests.cs（测试）、前端 tutorApi.ts / pages/solve/index.tsx 及文档——**未注入** LlmGatewayService 或任何求解器/判分代码 ✓。

### 7c. 未触碰 data/** 与求解/判分方法体
- **data/*** 零改动（CQ-5）：P2-4 触及文件清单（`p24_self_check.py` TOUCHED 列表，与 tasks.md Q.1 / spec §3 完全一致）共 10 项，**无一项位于 `data/` 下**；自检第 2 项断言通过 ✓。
  - 注：工作树存在 `data/highschool/concept-facts.json` 等其他在途 spec 的未提交改动，但这些不在 P2-4 触及清单内，且 after_p23→after_p24 eval 零漂移，与 P2-4 无关。
- **方法体未变**：无 after_p23 的 git tag/分支，无法做隔离 P2-4 的源级 diff；改用 spec AC-3 规定的**行为级铁证**：自检第 3 项逐 case 对比 `gatewayAnswer`/`gatewayOk`/`gatewayConclusion`/`gatewayCorrect`/`gatewayTruthAdopted`，after_p23↔after_p24 **757/757 全相同**；叠加两次 diff_ckpt 全 0。若任一方法体被改且影响输出，必在 757 题中显现漂移——实际为 0，故行为等价 ✓。

### 7d. 前端 `miniapp/src/pages/solve/index.tsx`
- **Insufficient 绝不展示 final_result**：`final_result` 仅在 `isConfirmed` 分支渲染；Insufficient 分支只渲染「无法给出确定答案」+ `constraint_note` + 「未采纳结论不会被展示，避免误导」，全文无 `view.final_result` 渲染 ✓。
- **Confirmed 诚实标注**：状态标签「✓ 引擎确定结论」+ 免责声明「已通过引擎确定性校验，仅供参考，不宣称 100% 正确。」✓。
- **explanation 标注不作判定依据**：解释折叠区门控 `!isInsufficient`，区内显式标注「趋势级，帮助理解，不作判定依据。」；`EVIDENCE_LABEL.Trend` 亦标注 ✓。
- 推理 DAG 区门控 `isConfirmed` ✓；教学话术 `reply` 展示 ✓；同页复盘 ✓。
- **表面积**：`app.config.ts` 注册 `pages/solve/index` ✓；`home/index.tsx` 加入口「🧪 题目求解 · 引擎推理链」跳转 `/pages/solve/index` ✓；`tutorApi.ts` 新增类型 + `solve(text)`，既有方法签名未动 ✓。
- **tsc 类型核查**：复跑 `tsc --noEmit -p tsconfig.json`，错误全部位于 `node_modules/@tarojs/.../*.d.ts`（node v26 与 @tarojs 已知 ABI 环境问题）；过滤 P2-4 触及文件（`pages/solve` / `services/tutorApi` / `app.config` / `pages/home`）**0 匹配** ✓。
- **build:h5**：按 spec AC-4 回退口径，因 node v26 与 `@tarojs/plugin-doctor` 原生二进制 ABI 不兼容被环境阻塞（已知，非代码缺陷）；用 `/solve` HTTP 响应留存佐证端到端链路，不阻塞后端红线验证 ✓。

---

## 三、裁决维度判定

| # | 维度 | 判定 | 证据 |
|---|------|------|------|
| 1 | precision=1（adopted∧¬correct=0） | **PASS** | 自检第 4 项：harm=0；eval summary `precision=1`、`harmCount=0` |
| 2 | harm=0 | **PASS** | 两次 diff_ckpt 均 `harm_old=0 harm_new=0`；summary `harmCount=0` |
| 3 | LOST=0 | **PASS** | 两次 diff_ckpt 均 `LOST (0): []` |
| 4 | WORSEN=0 | **PASS** | 两次 diff_ckpt 均 `WORSEN (0): []` |
| 5 | 不蒙数（correct=635，无猜测） | **PASS** | 自检第 5 项 correct=635；summary `gatewayCorrect=635`、`pureLlmCorrect=0`、`engineSolved=635`；diff 零漂移 |
| 6 | 不泄露未采纳结论 | **PASS** | 服务端：insufficient.json `final_result=null` 且 reasoning/explanation 空；投影 Insufficient 分支不下发；前端 Insufficient 分支绝不渲染 `final_result` |
| 7 | 未改数据与判决 | **PASS** | P2-4 TOUCHED 清单无 data/**；757/757 verdict 字段 after_p23↔after_p24 全相同（行为级证明求解/采纳门/判分未变） |
| 8 | 加性只读 | **PASS** | 投影无写回输入（正则 0 命中 + 单测覆盖）；`/process` 未动；新类型未注入求解路径（grep 确认） |
| 9 | 表面积可控 | **PASS** | TOUCHED 清单 10 项与 spec §3 / 台账 Q.1 完全一致；自检第 13 项「无意外删除」通过 |
| 10 | 台账 §Q 自洽 | **PASS** | §Q.1 触及清单（无 data/**）/ Q.2 门控字段矩阵 + HTTP 实证 / Q.3 eval 零漂移（GAINED 6 清单与 diff 一致）/ Q.4 6 个 Confirmed-未采纳残余登记，均与 spec 及实测证据吻合 |

**任一 FAIL 即整体 FAIL** → 10/10 全部 PASS。

---

## 四、Issue 列表

**无。**

（无红线破线、无精度回退、无泄露、无数据/判决改动、无表面积越界、无台账不自洽。）

---

## 五、最终总体裁决

# **PASS**

P2-4「前端接入网关求解，端到端闭环」满足全部红线与验收标准：后端为纯加性只读投影（新增 `/solve` 端点，不动 `/process` 与求解/判分），eval 零漂移（correct=635，precision=1，harm=0，LOST=0，WORSEN=0），不泄露未采纳结论（服务端源头裁剪 + 前端门控双保险），前端类型编译对触及文件 0 错误（build:h5 为已知环境 ABI 阻塞，非代码缺陷），台账 §Q 自洽。全量 2221 测试通过，新投影单测 5/5 通过，红线自检 13/13 通过。

---

**审查限制披露（非 Issue，不影响裁决）**：因仓库无 after_p23 的 git tag/分支（工作树为多 spec 累积未提交态），无法对 `LlmGatewayService.cs` 等做隔离 P2-4 的源级逐行 diff；「求解/判分方法体未变」系通过 spec AC-3 规定的行为级证据（757/757 verdict 字段全相同 + 两次 diff 全 0）+ 投影类型未注入求解路径（grep）共同证明。该证据强度满足 spec 自身规定的验收口径。

**Gaps & Unverified**：无决策相关缺口。所有委托要求的验证步骤（build / 全量 test / 新单测 / 两次红线 diff / 自检 / 泄露证据直读 / 代码审查 a-d / tsc）均已独立复跑并记录真实输出；`build:h5` 按 spec AC-4 回退口径未复跑（已知环境阻塞），已用 HTTP 响应留存佐证。
