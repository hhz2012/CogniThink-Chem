# pdf-263 计数输出引擎扩展 实施计划（datarepair5 / 方向 A）

## 仓库研究结论

**目标题**：`pdf-263`（type=choice，6 选项，answer="3"，answerKeywords=`["3","正确的有3项"]`）。
题干：「关于室温下 pH 相同、体积相同的氨水与氢氧化钠两种稀溶液，下列叙述正确的有几项」。当前 `gatewayUsedEngine=False`，属 bothWrong。

**根因（双重断点）**：
1. 6 个选项经现有 `MultiSelectChoiceValidator.StatementJudge`（9 个子 judge：事实库/计数/氧化还原/电子排布/VSEPR/晶体/配位键等）诊断，**全部返回 Undecided**——现有 judge 不覆盖「pH 相同的一元弱碱（氨水）vs 一元强碱（NaOH）定性比较」这一推理域。
2. 即便选项可判，现有链（`StatementChoiceValidator` / `MultiSelectChoiceValidator`）输出的是**字母串**（如「正确选项为 ABC」），而 pdf-263 的 answer 是**数字**「3」；`IsAnswerCorrect` 对该题走 answerKeywords 匹配（`GatewayVsLlmEvaluationService.cs` L363–384），字母串不含「3」→ 判错。

**影响面扫描（全库）**：命中「正确的有几项/个数」count-of-correct 问法的题**仅 2 道**：
- `pdf-020`：8 子项 + 选项「A.2个 / B.3个 / C.4个 / D.5个」，**已 SOLVED**，走 `CountingChoiceValidator` 路径一（选项为「N 个」形式），在 `CallChoice` 瀑布中位于 MultiSelect 之前即返回，**不会触及新分支**。
- `pdf-263`：6 选项均为具体陈述，现有路径均不覆盖，落到 MultiSelect——即新分支唯一目标。

**判分确认**：pdf-263 answer="3"（非字母），`TryGetChoiceLetter`/`TryGetMultiChoiceLetters` 均失败，走 answerKeywords 分支：`Normalize(gatewayAnswer).ToLower().Contains("3")` 即判正（命中任一关键词即 return true）。故引擎输出只需含数字「3」。

**化学独立确证（正确数 = 3，匹配 answer "3"）**：
核心事实——pH 相同、体积相同的一元弱碱（NH₃·H₂O）与一元强碱（NaOH），弱碱的物质的量浓度更大（部分电离）。
- A 真：加 NH₄Cl 固体——同离子效应抑制 NH₃·H₂O 电离（c(OH⁻)↓，pH↓）；NaOH 中 NH₄⁺ 与 OH⁻ 结合成 NH₃·H₂O（c(OH⁻)↓，pH↓）。两溶液 pH 均减小。
- B 真：稀释 100 倍——强碱 c(OH⁻) 降为 1/100；弱碱稀释促进电离，c(OH⁻) 降幅小于 100 倍，故稀释后氨水 c(OH⁻) 更大。
- C 真：pH 相同 ⇒ c(OH⁻) 相同；氨水中由电荷守恒 c(NH₄⁺)≈c(OH⁻)，NaOH 中 c(Na⁺)=c(OH⁻)，故 c(NH₄⁺)=c(Na⁺)。
- D 假：升温促进 NH₃·H₂O 电离 ⇒ 氨水 c(OH⁻) 增大（非「不变」）；NaOH 完全电离 c(OH⁻) 不变。「均不变」错。
- E 假：与足量 FeCl₃ 反应，Fe(OH)₃ 沉淀量取决于可提供的 OH⁻ 总量；弱碱浓度大、随 OH⁻ 消耗持续电离，总量更多 ⇒ 沉淀更多，非「一样多」。
- F 假：恰好中和消耗 H⁺ 等于 OH⁻ 总物质的量；弱碱浓度大 ⇒ 耗盐酸体积更大，非「相同」。

## 改动文件与模块

1. **`src/CogniThink.TutorBrain.Api/Application/Gateway/MultiSelectChoiceValidator.cs`**（引擎新能力）
   - 在 `TryDecide` 中，全部选项判定完成（L71 之后）、多选阈值检查（L77）之前，插入 **count-of-correct 数字输出分支**：
     - 新增 `LooksLikeCountOfCorrectQuestion(stem, opts)` 窄触发：题干含「正确的有几项 / 正确的有几个 / 正确的个数 / 下列叙述正确的有」，**且**所有选项均非「N 项/N 个/全部」形式（防御性，避免与 `CountingChoiceValidator` 路径一重；路径一前置已截走 pdf-020，此为双保险）。
     - 命中时统计满足问法方向的候选数 N，输出 `FinalResult = "正确的有 N 项"`（或「错误的有 N 项」），直接 return；不经过 `candidates.Count < 2` 的多选阈值（计数题 N 可为 1 或全部）。
     - 未命中时走原 MultiSelect 字母逻辑，字节级不变。
   - 复用现有逐选项判定（已保证全部可判，否则 L67 return null）。

2. **`data/highschool/choice-curated-facts.json`**（让 6 选项可判，策展）
   - 追加 `GC-778-A`~`GC-778-F`（6 条），verdicts：A 真 / B 真 / C 真 / D 假 / E 假 / F 假。
   - `stemMatch` 锁死题干语境（如 `["pH 相同","氨水","氢氧化钠"]`）保证全库唯一；`match` 锚定各选项特征短语。
   - 给原末条 `GC-777-D` 补尾逗号后追加，保持单行 31 空格缩进，**禁用 json.dump 重写**（避免格式漂移）；落库后 `json.loads` 校验通过（146→152 条）。

3. **`tests/CogniThink.TutorBrain.Tests/MultiSelectChoiceValidatorTests.cs`**
   - `NoMarkerVariant` 追加 `pdf-263` 登记（期望输出含数字「3」，判分匹配 answer="3"）。

4. **`data-defect-register.md`**：追加 datarepair5 批次记录（引擎扩展 + 策展）。

**不改动**：`pdf_questions.json`（pdf-263 的 type/options/answer/answerKeywords 均正确，无需动）。

## 实施步骤（依赖序）

1. **探针 dry-run（不落库）**：临时复刻 6 条草稿事实 + 计数分支逻辑，确认——
   - pdf-263 输出「正确的有 3 项」且判分匹配「3」；
   - 6 条草稿事实各只命中 pdf-263 目标选项（全库唯一性扫描 `TOTAL PROBLEMS=0`，零 stray）；
   - pdf-020 输出不变（仍走路径一，判对）。
2. **报请批准**（本计划）。
3. **落库**：追加 6 条事实 → 改 `MultiSelectChoiceValidator` → 加测试登记。
4. **验证门**：解锁 DLL → `dotnet build -c Release`（0 错误）→ 全库测试（绿，不退化）→ `run_eval_resume.ps1 -Tag datarepair5` → `cmp datarepair4 datarepair5`。

## 依赖与注意

- 新分支置于 `CallChoice` 瀑布末端（MultiSelect 内），不抢在既有单选校验器之前；已解题字节级不变（红线 TR-2.1 同类）。
- 判分对数字答案的匹配已由 `IsAnswerCorrect` answerKeywords 分支覆盖，无需改判分逻辑。
- 策展事实沿用 pdf-303/338/463/524 已验证范式（`stemMatch` 锁域 + 探针唯一性扫描）。
- 既有工作区 sim 引擎 WIP 非本批引入，评估时只关注 MultiSelect+Batch3 不退化。

## 验证（验收门禁，全部通过才算完成）

- `dotnet build -c Release`：0 错误。
- 全库测试 `dotnet test`：通过（上批 1835/0，本批 +1 登记，不退化）；MultiSelect 全绿。
- eval（Production / LlmAvailable=False，757 例）：`cmp datarepair4 datarepair5` 输出——
  - solved **723 → 724（+1）**，bothWrong 34 → 33；
  - `LOST=[]`、`harmCount=0`、`precision=1`；
  - `GAINED` 精确含 `pdf-263`，且无其他题进入 GAINED。
- 探针：pdf-263 `gatewayAnswer` 含「3」且 `gatewayCorrect=true`；pdf-020 仍判对。

## 风险与应对

- **策展事实误伤他题**：探针全库唯一性扫描（`TOTAL PROBLEMS=0`）把关，沿用已验证的 `stemMatch` 三词锁域。
- **计数分支误触 pdf-020**：窄触发（选项非「N 项/N 个」）+ `CountingChoiceValidator` 路径一前置截走（pdf-020 到不了 MultiSelect），双重保险；全库测试验证 pdf-020 仍判对。
- **判分空格不匹配**：`answerKeywords` 含独立「3」，`Contains("3")` 必中（与「正确的有3项」有无空格无关，foreach 任一命中即 return true）。
- **若探针显示选项仍 Undecided / 输出不含 3**：停止落库，回到确证环节调整事实 match 锚点，不得蒙数。
