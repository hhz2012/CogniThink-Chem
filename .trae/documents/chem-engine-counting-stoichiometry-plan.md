# 化学计量选择题 + 计数/组合推理求解器 实施计划

## 1. 背景与仓库研究结论

用户要求补齐两类引擎覆盖不了的选择题（Task 35 末尾列为"剩余不可解类别"）：

1. **计算/计量题**：需 NA 粒子计数 / 化学计量求解器，Concept 定性事实覆盖不了。
   - 现状：`NaChoiceValidator`（Tier 2-C）已覆盖"NA 粒子计数"选择题（20 道 t2n 题），但**反应化学计量**选择题（给方程式+投料量，选项断言某物质的量/质量/电子数/气体体积）当前无任何校验器，`CallChoice` 链中不存在计量计算。
2. **计数/组合推理**：「有几项正确」「同分异构体数」需计数求解器。
   - 现状：Core 层**完全没有**异构体枚举 / 对称性 / 等价原子能力（`MoleculeAnalyzer` 仅 calcMolFormula/calcNumRings/SMARTS）；全仓 grep 无 isomer/enumerat/automorphism 引擎逻辑。项目文档曾将此类题划为"原理上不可严格推导"。

### 目标题目当前评估状态（9 题全部未解决、无冲突、纯 LLM 也错，均为奖励候选）

| id | 类别 | 当前 | 可解性评估 |
|---|---|---|---|
| pdf-029 | 计量(双反应极值) | 未用引擎/错 | 需多反应守恒代数，**天花板**，诚实 null |
| pdf-067 | 氧化还原计量 | 未用引擎/错 | 需配平+变价分析+构型，跨域，**天花板** |
| pdf-079 | 歧化极值 | 未用引擎/错 | 极值/守恒，**天花板** |
| pdf-254 | 平衡 Q-K | 未用引擎/错 | 属平衡域，不在本次范围 |
| pdf-283 | 水解程度 | 未用引擎/错 | 属平衡域，不在本次范围 |
| pdf-305 | 电解池 | 未用引擎/错 | 题库 answer 与化学真值疑似冲突，**不纳入** |
| pdf-316 | 电化学 | 未用引擎/错 | 属电化学域，不在本次范围 |
| pdf-065 | 几项正确(氧还) | 未用引擎/错 | 子项需氧还判定，本求解器判不了→**诚实 null** |
| pdf-394 | 同分异构体数 | 未用引擎/错 | A/B 可枚举解；C 缺图、D 笼状需事实→**部分可解** |

**关键事实**：现有真题绝大多数需复杂多步推理或数据缺失，无法安全确定性覆盖。按项目既定模式（AC-16~19、AC-27~35）：**新建可控格式题库题让求解器稳定解出（保底），现有真题能解的为奖励，不能解的诚实 null + 留痕**。不追求覆盖全部真题。

## 2. 已完成的技术验证（探针，已删除临时文件）

在实现前用一次性探针验证了核心算法的正确性（对照化学已知值）：

- **RDKit 规范式去重可用**：`RDKFuncs.MolToSmiles` 产生规范 SMILES，`CCO/OCC/C(C)O → CCO` 去重正确；`calcMolFormula` 可取分子式。
- **烷烃构造异构体枚举**：Prüfer 序列生成所有碳骨架树 → 过滤价键>4 → 树转 SMILES（每个子节点都写成 `(子树)` 分支，确保连到当前节点）→ RDKit 规范去重。验证：C4=2、C5=3、C6=5、C7=9、C8=18，**全部与教材值一致**。
- **烷基异构体数**：对每个烷烃异构体，用 ¹³C 同位素标记逐个碳后再规范去重，得到不同碳位点数（= 不同取代位点），排除无 H 的季碳。验证：丁基(C4)=4、戊基(C5)=8，**与教材值一致**。
- **甲基筛选**：重原子度数==1 的碳为甲基。验证：C7H16 含 3 个甲基的异构体 = 3（证实 pdf-394 选项 A 声称"2 种"为假）。

→ 一个**真正计算**的计数求解器在技术上可行，且与高中"不考虑立体异构"约定一致（RDKit 规范 SMILES 默认忽略立体化学）。

化学计量侧：Core 已有 `EquationStoichiometry.Solve`（精确 Rational 系数比换算）、`MoleCalculator`（n=m/M、V=n·Vm）、`EquationBalancer`、`RedoxBalancer.SplitHalfReactions`（电子转移数），可直接复用，无需新 Core 能力。

## 3. 范围决策

### 纳入（本次实现）
1. **Core 新增 `ConstitutionalIsomerCounter`**：烷烃构造异构体枚举 / 烷基异构体计数 / 按甲基数筛选，n≤8，静态缓存（惰性计算一次）。
2. **Gateway 新增 `CountingChoiceValidator`**：
   - 选项断言"分子式的同分异构体有 N 种"（烷烃、烷基/一取代物如羧酸/醇/卤代烃、含指定个数甲基的烷烃）→ 调 Core 计算真值比对；
   - 笼状/苯环/萘等不可枚举的计数 → 查 `counting-facts.json` 事实表（精确匹配）；
   - 「有几项正确」且子项为异构体计数/计量断言 → 逐子项判定后计数，匹配"N 项"选项；
   - 四选项全部可干净判定 + 唯一候选 → 返回字母；任一不可判 → 整题 null（零假阳性）。
3. **Gateway 新增 `StoichiometryChoiceValidator`**：
   - 题干含可配平方程式 + 一个已知投料量；逐选项解析声称的量（物质的量/质量/标况体积/转移电子数），用 `EquationStoichiometry` + `MoleCalculator`（+ `RedoxBalancer` 算电子）计算真值比对；
   - 四选项全部可干净判定 + 唯一候选 → 返回字母；否则 null。
4. 两校验器接入 `CallChoice` 链（ConceptChoiceValidator 之后、ChoiceVerifier 兜底之前——保证零回归：先让现有校验器跑，新校验器只在无人认领时触发，且域外返回 null）。
5. 新建可控格式题库题（`tier2_questions.json` 追加 `t3count-*`、`t3s-*`），答案逐题用 Core 计算/化学核对。
6. 单测 + 全题库护栏（545 真题 + tier2 全量，零假阳性 / 零 harm / 零回归）+ 构建。
7. 评估快照 + `verify` 脚本 + 更新 `tasks.md` 留痕。

### 不纳入（诚实留痕为天花板）
- 多反应耦合 / 极值思想 / 守恒方程组（pdf-029/079）：需新建代数约束求解器，风险高，留待后续。
- 平衡 Q-K、水解程度、电化学（pdf-254/283/316）：属平衡/电化学域，不在本次范围。
- 「有几项正确」中子项需氧还/构型/图像判定（pdf-065）：子项不可判 → null。
- 需结构图的计数（pdf-394 选项 C 环上一氯代物缺图）：null。

## 4. 涉及文件

| 文件 | 变更 |
|---|---|
| `src/CogniThink.Chem.Core/Molecules/ConstitutionalIsomerCounter.cs` | **新建**：烷烃/烷基枚举 + 甲基筛选，静态缓存，n≤8 |
| `data/highschool/counting-facts.json` | **新建**：不可枚举计数事实（立方烷/苯/萘/等效氢等），精确匹配契约 |
| `src/CogniThink.TutorBrain.Api/Application/Gateway/CountingChoiceValidator.cs` | **新建**：`TryDecide(req) → ChoiceVerifier.Outcome?` |
| `src/CogniThink.TutorBrain.Api/Application/Gateway/StoichiometryChoiceValidator.cs` | **新建**：`TryDecide(req) → ChoiceVerifier.Outcome?` |
| `src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs` | **修改**：`CallChoice` 链中 Concept 之后、ChoiceVerifier 之前插入两个新校验器调用 |
| `src/CogniThink.TutorBrain.Api/data/tier2_questions.json` | **修改**：追加 `t3count-*`（~10）、`t3s-*`（~8）题 |
| `tests/CogniThink.Chem.Tests/ConstitutionalIsomerCounterTests.cs` | **新建**：已知值锁（C4~C8、丁基=4、戊基=8、C7-3甲基=3） |
| `tests/CogniThink.TutorBrain.Tests/CountingChoiceValidatorTests.cs` | **新建**：正例 + 全题库零假阳性护栏 + pdf-394 诚实断言 |
| `tests/CogniThink.TutorBrain.Tests/StoichiometryChoiceValidatorTests.cs` | **新建**：正例 + 全题库零假阳性护栏 |
| `.trae/specs/chem-engine-question-solving/tasks.md` | **修改**：追加 Task 36/37 记录与留痕 |

不修改任何现有校验器的判定逻辑；`LlmGatewayService` 仅插入两行调用。

## 5. 实施步骤（依赖顺序）

1. **Core 枚举器**：实现 `ConstitutionalIsomerCounter`（搬入探针验证过的算法，加静态缓存 `ConcurrentDictionary<int, ...>`，n≤8 上限，超出抛/返回空）。写 Core 单测锁住已知值。
2. **构建验证**：`dotnet build` + 跑 Core 单测，确认枚举器无回归。
3. **事实表**：创建 `counting-facts.json` + 数据模型类，放 `data/highschool/`（DataRoot 运行时从仓库根向上查找，无需 csproj 改动）。首批只收录可独立化学核对的条目（如立方烷二/六氯代=3、苯一氯代=1 二氯代=3、萘一氯代=2 等）。
4. **CountingChoiceValidator**：实现选项解析（分子式 + 计数类型 + 声称 N）→ 枚举或查表 → 判定；"几项正确"子项拆分（①②③ / ⑴⑵⑶）→ 逐子项走同一判定原语 → 计数匹配选项。严格四选项全可判 + 唯一才断言。
5. **StoichiometryChoiceValidator**：实现题干方程式抽取（"反应 … 中"含 =/→）+ 已知量抽取 + 逐选项声称量解析 + Core 计算 + 比对（Rational 精确，小数用容差）。电子数用 `RedoxBalancer`。严格全可判 + 唯一才断言。
6. **接线**：`LlmGatewayService.CallChoice` 插入两个调用（Concept 之后、ChoiceVerifier 之前）。
7. **新题**：撰写 `t3count-*`、`t3s-*`，每题答案用 Core 实算核对后写入 tier2_questions.json。
8. **单测 + 护栏**：正例断言字母；全题库遍历两个新校验器，断言零 mismatch（与真值比对）、零异常。
9. **全量测试**：`dotnet test` 两个测试项目（Debug 配置，避开 Release 锁），确认零回归。
10. **评估快照**：跑 `eval_gateway_vs_llm.py` 生成快照，核对新增题正确、无 loss、无新增异常。
11. **留痕**：更新 tasks.md。

## 6. 依赖与注意事项

- **引擎是真值源**：计数用 Core 枚举计算，计量用 Core 精确运算；事实表仅用于枚举不可达的结构（且精确匹配，不模糊）。
- **不蒙数**：任一选项/子项不可干净判定 → 整题返回 null，不部分断言。
- **无硬编码化学事实进逻辑**：特殊结构计数放 `counting-facts.json`（RuleStore 加载）；枚举是算法不是事实。
- **性能**：n=8 枚举 ~27s（262k 序列），Core 静态缓存只算一次；校验器运行时默认只请求 n≤7（高中极少 >C7），避免冷启动卡顿；n≤8 能力保留但由题目触发。
- **RDKit 生命周期**：`ROMol`/`Atom` 均 `IDisposable`，探针已验证 `using` 模式与 `setIsotope`/`getTotalNumHs`/`getDegree`/`getSymbol` 可用。
- **接线顺序**：新校验器放 Concept 之后、兜底之前，确保现有校验器优先 → 对已解题零影响（它们仍返回 null，不抢）。

## 7. 验证

- `ConstitutionalIsomerCounterTests`：C4=2/C5=3/C6=5/C7=9/C8=18、丁基=4/戊基=8、C7 含 3 甲基=3，全部锁定。
- 新校验器正例：每道 `t3count-*` / `t3s-*` 断言 `TryDecide` 非 null 且字母 = 题库 answer。
- 全题库护栏：遍历 `BuildTestCases()`（545 真题 + tier2），对两个新校验器各跑一次，收集非 null 结果，断言与 `Letter(answer)` 零 mismatch、`solved` 数 ≥ 预期、无异常。
- pdf-394：断言枚举能正确判 A=false、B=true（若 C/D 不可判则整题 null，写诚实护栏断言其不产生假阳性）。
- 全量 `dotnet test`（Debug）：两个测试项目全绿，无回归。
- 评估快照：新增题 gatewayCorrect=T，无现有题从 T→F，异常列表不新增。

## 8. 风险与处理

- **枚举与高中约定不一致**：已用教材值（C4~C8 烷烃、丁基/戊基）验证一致；约定为"构造异构、不考虑立体"，RDKit 规范式默认忽略立体 → 一致。若发现某题答案冲突，该题不计入正例（诚实 null）。
- **选项解析脆弱导致漏判/误判**：新题用可控格式；解析失败 → null（安全，不臆断）。全题库护栏确保零误判。
- **n=8 冷启动慢**：静态缓存 + 校验器默认 n≤7；若测试触发 C8，接受一次性成本或在 Core 测试单独长超时运行。
- **接线引入回归**：新校验器域外返回 null + 放在现有校验器之后，理论零回归；全量测试兜底确认。
- **counting-facts.json 事实错误**：仅收录可独立核对的条目；每条在单测里断言（查表结果 = 声称值），错误立即暴露。
