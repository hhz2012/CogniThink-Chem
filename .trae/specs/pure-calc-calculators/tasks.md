# P1-3 纯计算域计算器 - 实施计划

> 状态口径：spec 已批准、工作已完成（review PASS，合入 HEAD `cf4835c`）；`completed` 附完成证据；红线 `precision=1/harm=0/LOST=0/不蒙数`。

## Task 0：候选审计与范围冻结（read-only，已完成）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 从 `checkpoint_base_now.jsonl` 取 128 道未解，剔除 27 道 K.1 多部分，对剩余 101 道按域分类；逐题核对题干↔答案键对齐性与数值可确定性。
  - 产出：[p13_scope_report.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_scope_report.txt)、[p13_calc_candidates.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_calc_candidates.txt)、[p13_inspect_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_inspect_ids.txt)。
  - 冻结结论：独立可解目标 = **trap-limit-2 + pdf-517 共 2 道**；其余候选排除并登记台账（见 T6）。
- **Completion Evidence**:
  - 审计脚本运行成功，输出 3 份证据文件；vetting 表见 [spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/pure-calc-calculators/spec.md) Background。
- **Acceptance Criteria Addressed**: AC-1（目标集）、AC-5（台账输入）
- **Test Requirements**:
  - `rule` TR-0.1: 128 未解 = 27 K.1 + 101 非 K.1，数字与 checkpoint summary 自洽；证据：p13_scope_report.txt 首行。
  - `rule` TR-0.2: 目标 2 道当前 `gatewayCorrect=false` 且答案键为数值/比值且与题干对齐；证据：p13_inspect_ids.txt 对应段。
- **Notes**: 本任务只读，未改任何 `.cs`/JSON/题库。

## Task 1：水解 pH 计算器（FR-1）+ 探针
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T0
- **Description**:
  - 新增薄求解器（建议 `HydrolysisPhCalculator`）：检测「pH + 弱酸盐/弱碱盐 + 数值浓度」→ `RuleStore` 查 `acid-constants.json` 得 Ka → 算 Kb=Kw/Ka → `[OH⁻]=√(Kb·c)` → pH（按题干 lg 提示取位）→ 输出含数值答案的 `EngineResponse`；缺数据/查不到常数/非目标域 → null。
  - 写 Python 探针（C# 对齐归一化 + 目标题计算模拟 + 全库 stray 扫描），验证 pdf-517 诚实计算值 ≈9.76（与答案键 10.3 冲突→不匹配，预期不 gain 不 harm）、全库除 pdf-517 域外无 stray 误命中；**TOTAL PROBLEMS=0 才落代码**。
- **Acceptance Criteria Addressed**: AC-2、AC-4、AC-6（基础设施，本次不贡献 AC-1 gain）
- **Test Requirements**:
  - `rule` TR-1.1: 探针在 pdf-517 上用权威 Ka(HClO)=3.0e-8 诚实输出 pH≈9.76，**不匹配**冲突键 `10.3`（不 gain、不被采纳、无 harm），绝不输出 10.3 强配；证据：探针输出 + [p13_truth_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_truth_check.py)。
  - `rule` TR-1.2: 探针对全库 757 题 stray 扫描，仅 pdf-517 命中，其余 0 命中；证据：探针 stray 报告。
  - `rule` TR-1.3: 求解器代码无硬编码 Ka/Ksp 等化学常数（仅数学常数 Kw 等带注释）；证据：grep + 代码审查。
- **Notes**: 复用 `acid-constants.json` 既有 HClO 数据，不新增 JSON。

## Task 2：氧化还原比例计算器（FR-2）+ 探针
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T0
- **Description**:
  - 新增薄求解器（建议 `RedoxRatioCalculator`）：检测「配平氧化还原方程式 + 氧化剂/还原剂 + 质量比/物质的量比」→ 解析计量数与变价 → 算氧化剂:还原剂比例（歧化按变价原子数分配）→ 质量比 = 量比 × 摩尔质量比 → 输出含比值的 `EngineResponse`；读不出/非目标域 → null。
  - 写 Python 探针，验证 trap-limit-2 输出 1:2、与答案键 `['1','2','1:2']` 化学一致、全库无 stray；**TOTAL PROBLEMS=0 才落代码**。
- **Acceptance Criteria Addressed**: AC-1（trap-limit-2）、AC-2、AC-4、AC-6
- **Test Requirements**:
  - `rule` TR-2.1: 探针在 trap-limit-2 上计算氧化剂:还原剂质量比 = 1:2，化学正确；证据：探针输出。
  - `rule` TR-2.2: 探针对全库 757 题 stray 扫描，仅 trap-limit-2 命中，其余 0 命中；证据：探针 stray 报告。
  - `rule` TR-2.3: 求解器从题干解析方程，不硬编码 trap-limit-2 的方程或比例；证据：代码审查。
- **Notes**: 注意 `IsAnswerCorrect` 对 `['1','2','1:2']` 的 OR 子串匹配——探针核对的是化学真值 1:2，不靠宽松判分。

## Task 3：兜底接入与窄域护栏（FR-3）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T1, T2
- **Description**:
  - 在 `LlmGatewayService.CallCalculation` 末尾（既有求解器全返回 null 后）按序调用 T1、T2 求解器；保留既有分支顺序不变，仅追加兜底。
  - 确认 `BuildGatewayAnswerAsync` 对 calculation 的 Ok=true 结论经 `IsAnswerCorrect` 答案键校验后才采纳（AC-6）；若发现盲目采纳路径，加门控使 calculation 采纳必经校验，不放松现有正确题的判定。
- **Acceptance Criteria Addressed**: AC-2、AC-6
- **Test Requirements**:
  - `rule` TR-3.1: 既有求解器分支顺序与行为不变（回归预检查：629 道已解对题 taskType 与结论不漂移）；证据：路由对照脚本。
  - `rule` TR-3.2: 非目标题上 T1/T2 均返回 null，不产出占位结论；证据：探针 + 终态 checkpoint 抽查。
- **Notes**: 接入后先跑 `dotnet build` 再跑探针全库扫描，确认无 stray。

## Task 4：构建与单元测试
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T3
- **Description**:
  - `dotnet build CogniThink.Chem.sln -c Release`；`dotnet test CogniThink.Chem.sln -c Release`，确认 0 failed。
  - 跑前 `Get-FileHash` 确认 bin 下 `data/*.json` 与源一致（防 stale-DLL）。
- **Acceptance Criteria Addressed**: AC-3
- **Test Requirements**:
  - `rule` TR-4.1: `dotnet test` failed=0，passed ≥ 2150；证据：测试汇总。
  - `rule` TR-4.2: bin 副本 `acid-constants.json` 哈希与源一致；证据：Get-FileHash 输出。
- **Notes**: 若新增求解器需配套单元测试，覆盖目标题 + 至少 1 个域外反例（返回 null）。

## Task 5：全量 eval 与回归 diff
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T4
- **Description**:
  - 跑 eval 产 `after_p13` checkpoint；`diff_ckpt.py base_p0 after_p13` 核对 GAINED/LOST/WORSEN/IMPROVE + harm。
  - 确认 GAINED 恰为 trap-limit-2（=1），pdf-517 保持 `gatewayCorrect=false` 且无 harm；LOST=0，WORSEN=0，precision=1，harm=0。
- **Acceptance Criteria Addressed**: AC-1、AC-2、AC-6
- **Test Requirements**:
  - `rule` TR-5.1: diff GAINED=1 且为 trap-limit-2；pdf-517 不在 GAINED；LOST=0；WORSEN=0；summary precision=1 / harm=0；证据：diff 输出 + checkpoint summary。
  - `rule` TR-5.2: trap-limit-2 终态 `gatewayTruthAdopted=true`，pdf-517 终态 `gatewayCorrect=false`（未采纳）；证据：checkpoint 对应记录。
- **Notes**: 任一红线破 → 回退对应 Task，重新探针。

## Task 6：排除候选台账闭环（FR-5）
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: T0
- **Description**:
  - 在 `data-defect-register.md` 登记排除候选：**pdf-517（答案键冲突：真值 pH≈9.76 vs 键 10.3）**、pdf-030（变量无解/答案键疑似错位）、pdf-160（子问剥离缺上下文）、pdf-165（答案键错位）、pdf-172（答案键错位）、pdf-463（选择字母 + 缺上下文，归属 P1-2/P1-4），含根因 + 归属 spec。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rubric` TR-6.1: 处置可追溯性；scale 1-5；anchors 1/3/5 同 AC-5；threshold >= 4；证据：`data-defect-register.md` 新增行与审计 id 集合比对。
- **Notes**: 与 T5 可并行；不阻塞主目标。

## Task 7：独立只读评审 → review.md
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T5, T6
- **Description**:
  - 委派 fresh 上下文的只读子代理，按 [review.md 模板](file:///d:/github/CogniThink-Chem/.trae/specs/engine-capability-gap-fix/review.md) 范式审查：AC-1～AC-6 逐项独立取证（不采信实现者自证），重点核查 diff 红线、stray、采纳门、事实只存 JSON。
  - 全部 checkpoint PASS → review.md 记 `pass`；任一 actionable finding → 转 Issue 回 Implement 修复后复审。
- **Acceptance Criteria Addressed**: AC-1～AC-6
- **Test Requirements**:
  - `rule` TR-7.1: review.md 全部 CP 项 PASS，无 actionable finding；证据：review.md。
- **Notes**: 评审只读，不改代码。

---

## 状态同步说明（2026-11-22 审计）

Task 0–7 的实际工作已全部完成并合入 HEAD `cf4835c`，[review.md](./review.md) 裁决 PASS（CP-1～CP-7 全过，0 Issue）。详情段此前遗留的 `pending` 标记已同步翻转为 `completed`（纯台账同步，零行为变更）。地面真值：离线 eval `after_p13`（757/757）GAINED=trap-limit-2、LOST=WORSEN=harm=0、precision=1。
