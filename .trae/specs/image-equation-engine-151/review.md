# image-equation-engine-151 - Independent Review

> 复核对象：`.trae/specs/image-equation-engine-151/`（方程引擎为 151 道未解图像/方程题判正）。
> 复核日期：2026-11-21。上下文：实现队列已排空（Task 1-5/7/8 completed，Task 6 按 spike 门控 cancelled），本文件为首次独立 Review。

## Checkpoints

- [x] CP-R1: 全解决方案 clean build 0 错误，全量测试 0 failed
  - **Type**: `rule`
  - **Covers**: AC-1, AC-10
  - **Evidence**: `dotnet build CogniThink.Chem.sln -c Release` → exit 0，`0 个错误`；`dotnet test tests/CogniThink.TutorBrain.Tests -c Release --no-build` → 失败 0 / 通过 1183；`dotnet test tests/CogniThink.Chem.Tests -c Release --no-build` → 失败 0 / 通过 911。（独立复核子代理在叠加并行 spec 的同一工作树重跑得 1198/911，差异为并行 spec 新增测试，均 0 failed。）

- [x] CP-R2: 方程求解器满足「锚点齐全 + 唯一命中才产出，否则 null」契约
  - **Type**: `rule`
  - **Covers**: AC-2, AC-3, AC-12
  - **Evidence**: 读 `src/CogniThink.TutorBrain.Api/Application/Gateway/ReactionKbWriterSolver.cs`：`KbLookupMolecular`(L256) 与 `KbLookupIonic`(L286) 均 `if (matches.Count != 1) return null`；锚点门 L26（含「方程式」）、题型白名单 L31、歧义 guard L39（同题既问离子又问分子→null）；模板不命中即 return null（L182-185/L197/L217）；`ReactionKbWriterSolverTests` 含 `FullBankGuard_EveryNonNullOutput_IsAnswerCorrect`（遍历 757 题，凡非 null 必判正）与零命中/多命中/条件冲突反例。

- [x] CP-R3: 仅纳入探针逐题通过的方程题，GAINED 恰好为该集合
  - **Type**: `rule`
  - **Covers**: AC-4, AC-9
  - **Evidence**: `_evalscratch/equation_solver_direct.tsv` 恰好 3 行（pdf-038/417/477），均 `baselineCorrect=False` + `isCorrect=True`；`cmp.py after_pdf485 review_current` → `GAINED(3): ['pdf-038','pdf-417','pdf-477']`，与探针集合完全一致；其余 27 道排除理由记入 `data-defect-register.md` G 类（9 类）。

- [x] CP-R4: 晶胞 spike 未通过 → Task 6（CrystalCellValidator）取消，4 道晶胞题诚实留 null
  - **Type**: `rule`
  - **Covers**: AC-5, AC-6(N/A), AC-12
  - **Evidence**: `_evalscratch/crystal_spike_report.md` 结论「spike 不通过本轮实施门槛 → Task 6 取消」；`tasks.md` Task 6 状态 `cancelled`；快照 `snapshot_review_current.json` 中 pdf-170/178/206/222 均不在 `gatewayOnlyCorrectCases`（保持未判正）。注：工作树存在并行兄弟 spec `crystal-cell-validator` 的 `CrystalCellValidator.cs`（非本 spec 引入），其在当前快照贡献 0 GAIN / 0 harm，不破坏本 spec 红线。

- [x] CP-R5: 逻辑层无按题 id/答案文本硬编码，化学事实落在 JSON 知识库
  - **Type**: `rule`
  - **Covers**: AC-7
  - **Evidence**: 独立复核子代理在 `src/` 内 grep `pdf-038|pdf-417|pdf-477` 仅命中 `pdf_questions.json` 数据条目，求解器逻辑层无按题分支；`SpeciesByName` 为通用试剂名→物种映射（与既有 `ProductWriterSolver` 模式一致）；化学事实（RX-IN-ION-033 等）位于 `data/knowledge/reactions/*.json`。

- [x] CP-R6: 缺陷登记补全（G/C/A.2/E 类），且 GAIN 三题 answer/option 字段未被改动
  - **Type**: `rule`
  - **Covers**: AC-8, AC-13
  - **Evidence**: `data-defect-register.md` 含 G 类（27 道方程排除，9 类理由，L114 起）、C 类晶胞 4 题等；`git --no-pager diff --unified=8 -- src/.../pdf_questions.json` 显示工作树仅 pdf-170（answer `B→4`）、pdf-485（answer `1:1→2:1（或1:2）`）有 answer 改动，**pdf-038/417/477 无任何字段改动**；两处改动均非本 spec GAIN 题且带官方源留痕（pdf-170 属兄弟 crystal spec，pdf-485 引擎在基线已判正），全局 harm=0 证实未引入假精度。

- [x] CP-R7: 全新全量 eval 确认 precision=1、harm=0、LOSS=0
  - **Type**: `rule`
  - **Covers**: AC-10, AC-11, AC-1
  - **Evidence**: 本会话在当前合并工作树全新跑 `dotnet run --project src/CogniThink.TutorBrain.EvalRunner -c Release -- --tag review_current`（757/757 completed, 0 failed）；`checkpoint_review_current_summary.txt`：`gatewayCorrect=609 / precision=1 / harmCount=0 / bothWrong=148`；`cmp.py ie_final review_current` → `LOST (0): []`, `GAINED(0): []`, net +0（确认后续策展编辑未破坏 ie 收益）。

- [x] CP-U1: 净覆盖收益质量（收益题全部判正、零误伤、零回退）
  - **Type**: `rubric`
  - **Covers**: AC-9
  - **Scale**: 1-5
  - **Anchors**: 1 = 收益≤0 或含假阳性/回退；3 = 收益≥3 但存在可疑未核题；5 = 收益≥3 且全部经探针逐题核实、precision=1、harm=0、LOSS=0
  - **Pass Threshold**: >= 4
  - **Score**: 5
  - **Rationale**: 净增 +3（pdf-038/417/477），全部经 `equation_solver_direct.tsv` 逐题 `isCorrect=True` 探针核实；`cmp.py after_pdf485 review_current` 显示 precision=1、harm=0、LOSS=[]，无任何回退或误伤；达到并超过 AC-9 rubric 阈值（≥3）。
  - **Evidence**: `_evalscratch/equation_solver_direct.tsv`、`_evalscratch/checkpoint_review_current_summary.txt`、`cmp.py` 输出。

## Review History

### Review R1
- **Result**: `pass`
- **Date**: 2026-11-21
- **Reviewer**: 独立只读子代理（全新上下文，未修改任何文件），复核前由主线程独立重跑 build / 全量测试 / 全新 eval / cmp 作为预核。
- **Evidence**:
  - 主线程预核：build 0 错误；TutorBrain 1183/0、Chem 911/0；`review_current` eval precision=1/harm=0；`cmp after_pdf485→review_current` GAINED=[pdf-038,417,477] LOSS=[]；`cmp ie_final→review_current` net +0。
  - 独立子代理复核：重跑 cmp 两组（结论一致）、重读快照摘要、build 0 错误、全量测试 2109 passed/0 failed、git diff 确认 GAIN 三题 answer 未动、Glob 确认无 `_Tmp*.cs` 残留、逐条核验全部 AC 与源码红线。
- **Findings**（均为 `advisory`，不阻塞）:
  - ADV-1: `crystal_spike_report.md` §2 称「pdf_questions.json 中 0 条 type=crystal 资产」对当前合并树已失真（兄弟 spec 后写入了 3 条），建议注明晶胞资产/校验器由独立 spec 承接。不阻塞红线。
  - ADV-2: `_evalscratch/ie_final_report.md` §6 对工作树 answer 改动表述不完整（漏提 pdf-170），两处改动均非本 spec GAIN 题且为合法官方源订正。不阻塞。
  - ADV-3: 实测 TutorBrain 测试数（1198，含并行 spec 新增测试）与报告（1183）略有出入，不影响「0 failed」结论。不阻塞。
- **Actionable Findings**: 无。
- **Blocked By**: 无。

### Post-Review Remediation（2026-11-21，不影响 R1 结论）
按用户要求处理 R1 的 3 条 `advisory`（文档卫生，均不阻塞红线），仅修改 `_evalscratch/` 留痕报告，未触碰引擎/数据/测试：
- **ADV-1 已修复**：`_evalscratch/crystal_spike_report.md` §2 原称「`pdf_questions.json` 中 0 条 `type=crystal`」对当前合并树已失真 → 补注：spike 时点确为 0 条；当前合并树中并行兄弟 spec `crystal-cell-validator` 已写入 **4 条**（pdf-170/206×2/222，pdf-178 未写入），由该兄弟 spec 独立承接，当前快照 0 GAIN / 0 harm，不影响本 spike 结论。
- **ADV-2 已修复**：`_evalscratch/ie_final_report.md` §6 原仅提 pdf-485 的 answer 改动 → 补全：工作树现有两处 answer 改动（pdf-485 `1:1`→`2:1（或1:2）`、pdf-170 `B`→`4`），均非本 spec 引入、均非本 spec 3 道 gain 题（pdf-038/417/477 无字段改动），均带官方源留痕，全局 harm=0。
- **ADV-3 已修复**：`_evalscratch/ie_final_report.md` §7 表中 1183 为 `ie_final` 冻结时点计数（当时正确）→ 补注：当前合并树叠加并行 spec 新增测试，实测 TutorBrain **1198** / Chem **911** / 合计 **2109**，均 0 failed，差异源于并行 spec，不影响结论。
- 上述数字均由本会话真实工具输出核实（`git diff` + `dotnet test` 实跑）。

## Review Result Contract Routing
- 本次 Review 结果 = `pass`：全部 checkpoint 通过，每个 AC 均有独立证据，无 actionable finding，无 blocked check。按 Spec 模式完成条件，本 spec 工作流结束。
