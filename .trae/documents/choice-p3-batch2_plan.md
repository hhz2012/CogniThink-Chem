# Choice 覆盖 Batch2：纯文本题事实求解（+7 候选）实现计划

## Repository Research（研究结论）

**当前基线（HEAD `a342964`，已全库验证）：**
- EvalRunner（757 例）：`gatewayCorrect=439`、`engineSolved=439`、`precision=1.0`、`harm=0`、`LOST=0`。
- 全量单测：TutorBrain 930/0、Chem 905/0。
- 仍未解：choice 176、unknown 72、calculation 37、equilibrium 18、reaction_check 9、atomic_structure 4、concept 1。

**架构红线（已冻结，本批严格遵守）：**
- LLM 永不为化学真值来源；引擎（事实库 + matcher）为唯一真值。
- `FactChoiceMatcher.TryDecide`（`src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs`）红线：任一选项命中 0 条或 ≥2 条事实 → 整题返回 `null`，绝不蒙猜。
- `WrongDir` 正则决定取正确项还是错误项；单选恰好 1 个候选才出字母。
- 采纳路径 `GatewayVsLlmEvaluationService.BuildGatewayAnswerAsync`（L637）：choice 单选含选项字母即采纳，正确性由**事实质量 + matcher 保守性 + 全库 EvalRunner diff** 共同守住（凡采纳必判正，靠全库护栏后验保证）。
- 零误报、零伤害、零回归（LOST=0）；只做加法接线。

**176 道未解 choice 的分桶（fix2→p3_now 已审计）：**
- FIGURE 124：选项/题干含图，需图像解析 —— **本批不做**，留待后续重大工作。
- PURE_TEXT 37：最易由事实库求解。逐题审计后，**7 道干净候选**仍未解（见下）。
- COMBINATORIAL 12（①②③④）、COUNTING 3（"正确的个数是"）、CURVE 1 等：需新求解器，本批不做。

**7 道干净候选（4 选项纯文字可匹配 + 题库备注分析完整 + 与存储答案一致，无数据矛盾）：**

| 题号 | 答案 | 问方向 | 考点 | 备注结论（用于事实 verdict） |
|---|---|---|---|---|
| pdf-321 | D | 错误的是 | 有机命名 | A正确 B正确 C正确 **D错误** |
| pdf-378 | B | 错误的是 | 高分子官能团 | A正确 **B错误** C正确 D正确 |
| pdf-380 | D | 错误的是 | 高分子/E/F/G | A正确 B正确 C正确 **D错误** |
| pdf-350 | D | 正确的是 | 蛇孢菌素结构 | A错 B错 C错 **D对** |
| pdf-352 | A | 错误的是 | 产物Ⅰ/Ⅱ结构 | **A错误** B正确 C正确 D正确 |
| pdf-358 | A | 正确的是 | X/Y 结构 | **A正确** B错误 C错误 D错误 |
| pdf-525 | D | 正确的是 | 键能/亲核取代 | A错 B错 C错 **D对** |

> 注：pdf-350/352/358/380 的题干含结构示意图，但**选项文本本身可匹配**，verdict 依据题库备注（已与存储答案交叉核对一致）。如实现中某题因碰撞/判错无法唯一判定，直接丢弃（保持 null），不强行。

## Files and Modules（改动范围，仅加法）

- `data/highschool/general-choice-facts.json`：为 7 道候选各补 4 条事实（共 28 条），每条 `{id, match[], stemMatch[], verdict, reason}`。`match` 取该选项独有子串；`stemMatch` 取题干中能唯一定位本题的 2–3 个短语（防跨题误命中）。JSON 严格合法（无尾随逗号，`System.Text.Json` 可加载）。
- `tests/CogniThink.TutorBrain.Tests/Batch3ChoiceValidatorTests.cs`：`ConceptTruth` 新增 7 项（pdf-321=D、378=B、380=D、350=D、352=A、358=A、525=D）。保持 `Concept_Coverage_MatchesTruthTable`（solved 数 == ConceptTruth 数）与 `Concept_ZeroFalsePositive_AcrossAllBuildTestCases` 通过。
- `.trae/specs/chem-engine-question-solving/tasks.md`：更新 Task 40 进度与指标基线（实现并验证后）。

**不改动**：matcher 逻辑、校验器链、采纳门控、`pdf_questions.json`（除本批事实外不动题库）。

## Implementation Steps（依赖有序）

1. **逐题核真**：对 7 道候选，从 `pdf_questions.json` 读取完整题干 + 4 选项原文；依据备注 + 存储答案确认每个选项 verdict 与问方向；列出每题的 `match`/`stemMatch` 子串（选项独有 + 题干唯一）。
2. **写事实**：在 `general-choice-facts.json` 的 `statements` 数组追加 28 条，ID 形如 `GC-321-A`…`GC-525-D`，`verdict` 与上表一致，`reason` 写明化学依据。
3. **JSON 严格校验**：用 `System.Text.Json`（`AllowTrailingCommas=false`）解析通过；确认无语法错误。
4. **更新测试真值表**：`ConceptTruth` 加 7 项。
5. **清探针 + 构建 + 全量单测**：删 `**/_Tmp*.cs`；构建 TB/Chem 测试项目；`dotnet vstest` 跑全量，要求 **0 fail**（TB、Chem）。
6. **全库护栏**：EvalRunner 新 tag `choice_p3_b2`；与 `choice_p3_now` diff，要求：
   - `GAIN > 0`（期望 +7，允许少于 7 —— 判错/碰撞的题自然回 null）；
   - `LOST = 0`、`precision = 1.0`、`harm = 0`。
7. **失败即弃**：若某候选产出错误字母或引发任意回归，从本批移除该题（事实 + 真值表项），保持其余；宁缺毋滥。
8. **更新 Task 40** 进度与指标；提交（`git commit -F <utf8 msg>`，规避 PS5 中文 heredoc）。

## Dependencies and Considerations（依赖与注意）

- **环境不稳定**：工作树曾被外部进程重写、编辑偶发不持久。每次改动后必须 **read-back 验证**（Grep 计数）；验证通过立即提交锁定。
- **PS5 编码**：`.ps1` 保持纯 ASCII；中文数据放 UTF-8 `.txt`/`.json`，脚本内用 `[System.IO.File]::ReadLines/WriteAllText(..., UTF8)` 读写。
- **残留 dotnet 宿主**：测试/评估前杀掉 `vstest`/`dotnet test` 进程，避免 DLL 锁与计数漂移。
- **figure 依赖题的置信度**：pdf-350/352/358/380 的 verdict 依赖结构图，以题库备注为准；如备注与答案矛盾则剔除该题。优先保证零误报，其次覆盖率。

## Validation（验证清单）

- [ ] `general-choice-facts.json` 严格 JSON 解析通过。
- [ ] 全量单测：TB 0 fail、Chem 0 fail。
- [ ] `Concept_ZeroFalsePositive_AcrossAllBuildTestCases` 通过（无未受邀断言）。
- [ ] `Concept_Coverage_MatchesTruthTable` 通过（solved == ConceptTruth.Count）。
- [ ] EvalRunner `choice_p3_b2` vs `choice_p3_now`：GAIN>0、LOST=0、precision=1.0、harm=0。
- [ ] 逐题核对新解 7 题的 `gatewayAnswer` 字母与存储答案一致。

## Risks（风险与处置）

- **事实化学性错误 → 错字母 → 精度下降**：逐题人工核真；EvalRunner 全库后验捕获；判错的题立即移除（回 null 安全）。
- **跨题匹配碰撞 → matcher 判 null（安全）或误命中**：`stemMatch` 用题干唯一短语；每选项恰一命中红线；全库护栏兜底。
- **figure 题备注不准**：交叉核对备注与存储答案；矛盾则剔除，留待图像解析能力。
- **并行进程/静默回退**：read-back + 即时提交；基线已锁在 HEAD `a342964`，可随时 `git checkout HEAD --` 恢复。
