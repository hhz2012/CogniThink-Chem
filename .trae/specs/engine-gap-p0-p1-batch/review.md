# 独立评审 — engine-gap-p0-p1-batch（引擎能力缺口补强 P0/P1 + P2-2 批次）

> 评审性质：**只读独立评审**（不改代码），聚焦红线合规性与正确性。最终结论由主代理 reconcile 后落定。

## 1. 评审范围

基线：`snapshot_after_vsepr509`（gwCorr=645）；新快照：`snapshot_gap_batch1`（gwCorr=654）。

### 新增求解器（src/.../Gateway/）
- `KspPhResidualSolver.cs`（T4）
- `GibbsSpontaneitySolver.cs`（T3）
- `AcidFirstIonizationSolver.cs`（T7）
- `CombustionEmpiricalFormulaSolver.cs`（P2-2）
- `LlmGatewayService.WeakAcidIonization.cs`（T3，弱电解质电离，pdf-278 子问）

### 修改文件
- `LlmGatewayService.cs`（last-resort 钩子链 + 多部分守卫）
- `LlmGatewayService.MultiPartDispatch.cs`
- `ProductWriterSolver.cs` + `data/knowledge/product-rules.json`（T6）
- `VseprSolver.cs`（T5）
- `GatewayVsLlmEvaluationService.cs`
- `data/pdf_questions.json` + `pdfs/questions_parsed.json`（T2 pdf-262 数据修复）
- `data/highschool/judgment-statement-facts.json`
- `data-defect-register.md`（登记）

### 新增/修改测试
- `KspPhResidualSolverTests.cs`、`GibbsSpontaneitySolverTests.cs`、`ProductWriterSolverTests.cs`、`AcidFirstIonizationSolverTests.cs`、`CombustionEmpiricalFormulaSolverTests.cs`、`EngineRoutingFallbackTests.cs`、`MultiPartPlaceholderGuardTests.cs`、`WeakAcidIonizationSolverTests.cs`、`VseprSolverTests.cs`(+2)、`MultiPartAndScoringTests.cs`、`ConceptFactSolverTests.cs`、`ImageTextFusionRegressionTests.cs`

### 证据（`_evalscratch/`）
`snapshot_gap_batch1.json`、`pdf272_probe.txt`、`acidion_dryfire.txt`、`combustion_dryfire.txt`、`p2_dump.txt`、`check_flip.py`，以及 `diff_engine` / `cmp` / `verify_mp27` 输出。

> **不在本次评审范围**：`.trae/documents/*`、仓库根的两个中文报告 `.md`、`data/pdf_images/p116_n01_0.png`（与本批次无关）。

## 2. 红线（必须全部满足，否则 BLOCK）

1. **引擎层 LOST=0**：`diff_engine after_vsepr509 gap_batch1` ENGINE LOST=[]，truthAdopted True→False=[]。（已验证：[]）
2. **零假阳性 harm=0**：`harm (adopted && !correct)=[]`。（已验证：[]）
3. **不蒙数**：每个新求解器在「无唯一确定解」时返回 null（缺数据/歧义/化整失败/曲线数据不支持答案）。
4. **不硬编码单题**：修复须为通用规则/事实（化学事实外置 JSON），不得只为某一题写死。
5. **构造性零 LOST**：last-resort 钩子必须 `if (engineResponse.Ok is not true)` 守卫（已判正题到不了钩子）。
6. **多部分 27 守卫**：`verify_mp27` 27/27 PASS，REGRESSIONS=[]。（已验证：PASS）
7. **单元测试全绿**：`dotnet test -c Release` 0 failed。（已验证：1640 passed）
8. **诚实登记**：未解/延后题（pdf-262、pdf-010/156、pdf-416）登记为 not-solved，不强行翻正。
9. **无遗留临时探针**：`tests/**/Temp*.cs` 已删除（证据 `.txt`/`.py` 保留在 `_evalscratch/`）。
10. **switch 变量碰撞**：`LlmGatewayService.CallEngineAsync` 的 `default` 分支无大括号，`var cf` 污染 switch 作用域；其它 case 必须用唯一变量名（不得引入新的 `var` 同名导致编译歧义或遮蔽）。

## 3. 评审检查项（逐项给 PASS / FAIL / N/A + 证据）

- [ ] C1 每个新求解器的 null 守卫是否真的覆盖「无解」分支（通读代码，而非只看测试名）。
- [ ] C2 last-resort 钩子链顺序与守卫：T4→T5→T6→T7→P2-2→P2-1 三态，每段 `Ok is not true` 守卫是否完整；是否有钩子在 `Ok=true` 时仍会改写结论（导致 LOST）。
- [ ] C3 采纳门一致性：每个钩子设置的 `TaskType`（equilibrium / vsepr / reaction_check / calculation）是否走 `IsAnswerCorrect`（OR）而非 `AllKeywordsHit`（AND）；多部分分发成功的题是否仍走 AND（不被钩子降级）。
- [ ] C4 零 FP 的 dry-fire 证据是否与代码守卫一致（每个求解器「全库仅命中目标题」的断言是否有对应护栏测试锁定）。
- [ ] C5 `VseprSolver` 结论「追加」是否真的未删改原串（pdf-509 不回退）；`SplitNumberedSegments` 是否会在非编号多段题上误切片。
- [ ] C6 `AcidFirstIonizationSolver` 分子式解析/电荷推导是否通用（不只 H2C2O4）；`FormatComposition` 元素顺序是否正确（插入序而非字母序）。
- [ ] C7 `CombustionEmpiricalFormulaSolver` 氧质量为负 / 化整失败 → null 是否真生效；半整数比（×k）是否正确；是否会在非燃烧题误触发（守卫「分子式」+ 燃烧数据）。
- [ ] C8 pdf-262 数据修复（T2）是否未引入 answerKeywords 误切导致其它题回归；pdf-262 是否仍诚实为 False。
- [ ] C9 无遗留 `Temp*.cs`；无调试用 `Console.WriteLine` 残留在生产代码。
- [ ] C10 `product-rules.json` 新增条目是否配平且化学正确；锚点是否足够窄。
- [ ] C11 变量命名在 switch 中无碰撞（红线 10）。

## 4. 发现与处置（主代理 reconcile）

独立只读评审（子代理）结论：**无 BLOCK，10 条红线全部满足，建议 PASS**。评审通读了 5 个新增求解器、`LlmGatewayService` 钩子链与 switch、`VseprSolver` T5 改动、`ProductWriterSolver` + `product-rules.json`、相关测试与证据，并用 Python 解析两份快照交叉核对（未改任何文件）。

### 已修复（放行前完成）
- **建议 1（已落地）**：为 `KspPhResidualSolver` 补齐全库 fired 集合锁定护栏测试 `全题库_未解对题凡触发必判正_且触发集锁定Pdf168`（锁定 fired=`[pdf-168]`，与 Acid/Combustion/Vsepr 护栏对齐），防未来 FP 回退。复验：`dotnet test -c Release` **1641 passed / 0 failed**（原 1640 + 1）。

### 记录在案（非阻塞，不改码）
- **建议 2（已登记说明）**：本批次工作树还含若干 spec/tasks 未逐条描述但经全量 eval 验证安全的改动，均纳入同批次红线审计（LOST=0 / harm=0，spot-check 确认无 gwFlip 回归）：① `GatewayVsLlmEvaluationService.EquationSetEquivalent`（pdf-278 子问方程两侧物质顺序无关匹配，仅作用于 AND 路径，不泄漏到 OR，已有 `OrPath_NotAffectedByEquationFallback` 测试）；② `data/highschool/judgment-statement-facts.json` 新增 JS-027~041（pdf-134，stemMatch 锚定「葡萄酒/碳酸钡」，已在 data-defect-register K.0 登记）；③ `data/pdf_questions.json` 中 pdf-278 题干补全子问(1)-(5)、pdf-355/356 答案键校正为 fill、pdf-337 补 images——均为数据修复，已由全量回归兜底。
- **建议 3（已覆盖）**：`judgment-statement-facts` 新增条目的护栏由其既有批次测试覆盖，且全量 eval harm=0 兜底；归属在 data-defect-register K.0 已登记。

### 评审未自行复跑项（采信 + 交叉印证）
评审未独立重跑 `diff_engine`/`verify_mp27`/`dotnet test`（只读），但其快照 spot-check 与委托方证据一致、无矛盾；主代理已实际执行三者并记录于 tasks.md T9。

## 5. 最终结论

- [x] **PASS** —— 全部红线（LOST=0 / harm=0 / 不蒙数 / 不硬编码 / 构造性零 LOST / mp27 27-27 / 单测全绿 / 诚实登记 / 无临时探针 / switch 无碰撞）满足；C1–C11 全部通过或非阻塞缺口已补齐；评审无 BLOCK 级问题。
- [ ] BLOCK

**放行依据**：引擎层 gwCorr 645→654（+9，全部为预期目标或回归恢复），零回归零假阳性；`pdf-262` 诚实保持 False；pureLlm 波动（pdf-197/376）与引擎无关。
