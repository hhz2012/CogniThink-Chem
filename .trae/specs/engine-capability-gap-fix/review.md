# 独立 Review 门禁报告 — engine-capability-gap-fix

> **审查者**：fresh-context 只读子代理（T9）
> **审查范围**：本 spec 全部改动（事实落库 + 方程书写路由代码 + 台账），基线 `verify_fix_all` → 终态 `t5_kb`。
> **审查纪律**：只读（仅新建本文件）；独立复跑验证脚本与 git diff，**不信实现者自述**，所有结论附可复现证据。
> **总体裁决**：**PASS**（7 维度全部通过，无 precision/harm FAIL；但有 1 项跨域关键发现须后续处置，见末尾）。

---

## 0. 独立复跑概览（不信自述）

下表为本次 Review 实际执行的验证命令与结果（均为子代理亲自运行，非引用实现者输出文本）：

| 验证项 | 命令 / 方式 | 独立结果 |
|--------|-------------|----------|
| 翻转矩阵（Python，json.loads 容忍重复键） | `python _evalscratch/verify/diff_ckpt.py verify_fix_all t5_kb` | GAINED=8 / LOST=0 / WORSEN=0 / harm 0→0 → **RED-LINE PASS** |
| 翻转矩阵（PowerShell regex/ConvertFrom-Json 法） | `diff_eval.ps1 -OldTag verify_fix_all -NewTag t5_kb` | OLD=756 NEW=756 / GAINED=8 / LOST=0（少 1 行为 PDF 重复键 A/a，与 spec parseFail=1 一致，不影响翻转） |
| T1 落库后探针（仅加载已落库事实，无重复计数） | `python _evalscratch/verify/probe_t1_postland.py` | **TOTAL PROBLEMS=0**（20/20 stray-hit 唯一、5/5 决策正确、NoOverlap 跨域干净） |
| T3 判断事实核验（去重，仅落库事实） | 内联只读脚本复刻 `probe_t3.py` 聚合+stray 逻辑 | **TOTAL PROBLEMS=0**（10/10 命中 pdf-480 唯一目标子题，聚合 `√××√×××××√` 与答案键一致） |
| 61 集合闭包（独立解析 diagnose，不写文件） | 内联只读脚本解析 `gap63_diagnose.txt` | 63 条（含 2 合成探针）→ real=61；`8∪6∪7∪40=61` 无重叠无遗漏 → **PASS** |
| K 类台账覆盖 | `python _evalscratch/verify/t8_coverage.py` | 61 中未在 K 类出现 = 无；未解题 53/53、解出 8/8 全覆盖 → **PASS** |
| 构建 | `dotnet build CogniThink.Chem.sln -c Release --no-restore -v minimal` | **0 警告 / 0 错误** |
| 测试 | `dotnet test CogniThink.Chem.sln -c Release --no-build --no-restore` | **2150 passed / 0 failed / 0 skipped**（TutorBrain 1237 + Chem 913） |
| 密钥扫描 | 全仓跟踪文件 `git grep -E 'sk-[A-Za-z0-9._-]{20,}'`（排除 bin/obj） | 仅 `ocr_test.py:7` 1 处命中（**非本 spec 引入**，详见维度 7 关键发现） |
| 题数据漂移核对（757 题） | 内联脚本逐题对比 baseline↔t5_kb 的 answer/question/options | **0 漂移**；仅 8 题 correct 翻转且全部 False→True |

> Python 解释器：`C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`（`python` 在 PATH 中为 Windows Store 桩，不可用）。

---

## 1. 维度逐项裁决

### 维度 1：precision=1（无已解对题被改判错）— **PASS**

证据：
- 从 [checkpoint_t5_kb.jsonl](file:///D:/github/CogniThink-Chem/_evalscratch/checkpoint_t5_kb.jsonl) 原始数据独立核算：`gatewayTruthAdopted=629`，其中 `adopted && !correct` = **0** → 无任何被采纳但判错的结论，precision=1。
- 逐题 correct 状态翻转核对：`verify_fix_all`↔`t5_kb` 间仅 **8 题**发生 correct 变化，且**全部为 False→True**（pdf-044/090/147/155/188/319/354/480）；**0 题 True→False** → 无已解对题被改判错。
- 8 道 GAINED 在基线均为 `correct=False`（真实翻转，非「已解对被重判」）：
  - `pdf-090: baseline_correct=False -> t5_kb_correct=True`（其余 7 题同构）。

### 维度 2：harm=0（无正确数据被删/覆盖；无错误结论被采纳；options 未被清空）— **PASS**

证据：
- `diff_ckpt.py`：`harm_old=0 → harm_new=0`，`WORSEN(null→new-harm)=0`。
- 题数据完整性：独立逐题对比 757 题在两个 checkpoint 的 `answer` / `question` / `options`，**全部 0 漂移** → 本 spec（baseline→t5_kb）未删除或覆盖任何正确题数据、未清空任何 options。
  - 注：工作树相对 HEAD 存在 `pdf_questions.json` 改动（28 题），但该改动在 `verify_fix_all` 基线生成时即已存在、且 baseline 与 t5_kb 间完全一致 → 属于基线之前/另一在途 spec 的数据修复，**不构成本 spec 的 harm**，本 spec diff 完整性成立（详见「上下文说明」）。
- 无错误结论被采纳：`adopted-but-wrong=0`（维度 1 已证）。

### 维度 3：无回归（eval diff LOST=0，权威核对不信 summary）— **PASS**

证据：
- 两种独立 diff 法一致：`diff_ckpt.py`（757/757）与 `diff_eval.ps1`（756/756，1 行因重复键被 ConvertFrom-Json 跳过）均报 **LOST=0 / GAINED=8**。
- 逐案确认：`LOST count: 0`（无任何 baseline `correct=True` 的题在 t5_kb 变为 False）。
- 终态指标（独立核算，不信 summary 文件）：t5_kb `total=757 / gatewayCorrect=629 / truthAdopted=629 / usedEngine=633 / pureLlmCorrect=0 / harm=0` → LLM 关闭、precision=1、harm=0。

### 维度 4：不蒙数（探针 TOTAL PROBLEMS=0；无唯一解返回 null；跳过留痕）— **PASS**

证据：
- **T1**：独立复跑 [probe_t1_postland.py](file:///D:/github/CogniThink-Chem/_evalscratch/verify/probe_t1_postland.py) → `TOTAL PROBLEMS=0`：20 条落地事实（GC-762/763、EF-545/546、OF-361）每条在全库仅命中唯一目标 (题, 选项)；5 道目标题决策模拟全部正确（pdf-090→D、pdf-188→C、pdf-147→B、pdf-155→C[WrongDir]、pdf-319→D）；curated 目标与 experiment/organic/structure 无跨域重叠。
- **T3**：独立去重复核（`probe_t3.py` 的 `existing+drafts` 因已落库会重复计数，故用仅落库事实重算）→ `TOTAL PROBLEMS=0`：JS-017~026 每条仅命中 pdf-480 唯一目标子题，聚合序列 `√××√×××××√` 与答案键完全一致。
- **T4/T5 不蒙数**：[LlmGatewayService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) 的 `equation_writer` 分支在求解器未命中时返回 `Ok=null`（不臆造）；t5_kb 中 6 道 KB 未覆盖的方程书写题（pdf-084/526/527/355/356/010）全部 `correct=False / adopted=False / ans=''`（诚实 null，未被蒙错），仅 pdf-044/354 解对。
- **跳过留痕齐全**（[t1_curated_ids.txt](file:///D:/github/CogniThink-Chem/_evalscratch/verify/t1_curated_ids.txt)、[t3_judgment_ids.txt](file:///D:/github/CogniThink-Chem/_evalscratch/verify/t3_judgment_ids.txt)）：pdf-196（options 空）、pdf-416（缺父上下文）、pdf-531（多选无标注）、pdf-405（缺结论列）、pdf-134/478（图像依赖子题 + 方程解析失败）、pdf-117（题面不完整）均给出跳过原因并保持 null，与 t8 集合 `skipped_trace(6)` 自洽（pdf-117 不属 61 缺口集合，另行登记）。

### 维度 5：事实只存 JSON（无 .cs 化学事实硬编码）— **PASS**

证据（独立 `git diff`）：
- 本 spec 仅改 2 个引擎 `.cs` 文件，均为**纯文本模式匹配 + 求解器分发**，无任何化学方程式/真值硬编码：
  - [LlmGatewayService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)（+36/-0）：`ClassifyTaskDeterministic` 在 `calculation` 规则前插入「书写/写出 +（离子方程式|化学方程式|方程式|酯化反应）」→ `equation_writer`；`CallEngineAsync` 新增 `case "equation_writer"` 跑 `KnownEquationWriterSolver → ReactionKbWriterSolver`，命中则复用 `reaction_check` 采纳门控（必经答案键校验），未命中 `Ok=null`。
  - [ReactionKbWriterSolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReactionKbWriterSolver.cs)（+2/-1）：taskType 白名单加入 `equation_writer`。
- 所有化学事实均落 JSON，且为**纯增量插入**（原末条补尾逗号 + 新增条目，无正确数据删除）：
  - `data/highschool/choice-curated-facts.json` +8（GC-762/763）、`experiment-facts.json` +8（EF-545/546）、`organic-facts.json` +4（OF-361）、`judgment-statement-facts.json` +10（JS-017~026，16→26）、`substances.json` `organics` +2（HOCH2CH2OH / CH3COOCH2CH2OH 白名单）。
  - `data/knowledge/reactions/reactions-inorganic.json` +1（RX-IN-ION-034）、`reactions-organic.json` +1（RX-OR-EST-004）。
  - 已独立确认当前落库计数：judgment 26、choice-curated 88（含 GC-762/763）、experiment 100、organic 65，与证据一致。
- 测试改动仅为真值表 id→字母同步（Batch3 +pdf-090/188、Batch2 +pdf-147/155），非引擎逻辑硬编码。

### 维度 6：台账自洽（61 道 id 无遗漏；数字与 checkpoint/diagnose 自洽）— **PASS**

证据（独立复算，不依赖实现者输出文件）：
- 独立解析 [gap63_diagnose.txt](file:///D:/github/CogniThink-Chem/_evalscratch/verify/gap63_diagnose.txt)：64 个块（63 条目 + 1 行汇总头 `found=63 / parseFail=1`），命中合成探针 `trap-limit-2/3` → **真实缺口 = 61**；与期望 61 集合比对：未出现=无、超出=无。
- 集合闭包恒等式成立：`61 = solved(8) ∪ skipped_trace(6) ∪ t2(7) ∪ remaining(40)`，两两无重叠、`real \ accounted = 40`、`accounted \ real = 空` → **差集为空，0 静默遗漏**。
- K 类台账覆盖：[t8_coverage.py](file:///D:/github/CogniThink-Chem/_evalscratch/verify/t8_coverage.py) 独立复跑 → 61 道全部在 K 类区段有处置结论，0 遗漏（K 类额外出现的 `trap-limit-2/3` 为探针说明，非数据缺陷）。
- 台账内容扎实（抽查 [data-defect-register.md](file:///D:/github/CogniThink-Chem/data-defect-register.md) K.0–K.4）：每簇含根因 + 当前处置（保持 null）+ 建议接续方向，逐题列表；rubric 锚点 ≥4 满足。
- 数字自洽：baseline `gatewayCorrect=621` + 本 spec 解出 8 = `629`，与 t5_kb 独立核算的 `gatewayCorrect=629` 完全一致；[final_gained_ids.txt](file:///D:/github/CogniThink-Chem/_evalscratch/verify/final_gained_ids.txt) 的 8 题与 diff 输出逐字吻合。

### 维度 7：红线（密钥不入库；WRONG 正则未改；守恒近似未接入 choice）— **PASS（附 1 项关键发现）**

- **WRONG 正则未改**：PASS。本 spec 的 2 个 `.cs` diff 不含任何正则改动；`FactChoiceMatcher.cs` / `MultiSelectChoiceValidator.cs` 未被本 spec 触碰（git diff 确认本 spec 在 `Application/Gateway` 仅改上述 2 文件）。
- **守恒近似未接入 choice**：PASS。本 spec 未改任何 choice 求解器逻辑。
- **密钥不入库（本 spec 改动）**：本 spec 自己改动的所有文件（2 `.cs` + 7 数据 JSON + `data-defect-register.md` + 测试真值表）经独立扫描**全部 clean，无密钥明文**。
- **⚠️ 关键发现（跨域，非本 spec 引入，但实现者自述不实）**：
  1. 全仓跟踪文件中存在 1 处真实形态密钥：[ocr_test.py](file:///D:/github/CogniThink-Chem/ocr_test.py) 第 7 行 `API_KEY = "sk-ws-..."`。该文件**已被 git 跟踪**（`git ls-files` 命中），由旧提交 `c2077c9`（2026-11-19，HEAD 祖先）引入；本 spec 工作树**未改动**它（`git diff HEAD -- ocr_test.py` = 0 行、未暂存）。因此**本 spec 未引入该密钥**，但它确实存在于仓库提交中。
  2. 实现者在 [tasks.md 第 302 行](file:///D:/github/CogniThink-Chem/.trae/specs/engine-capability-gap-fix/tasks.md) 声称「全仓（排除 bin）扫描 `sk-…` → **0 命中**，无千问 API key 入库」——该自述**与事实不符**（至少 `ocr_test.py` 1 处命中）。说明红线 grep 自验证未被正确执行/核对，恰是独立 Review 须存在的原因。
  3. 处置建议（后续，不阻塞本 spec 门禁）：立即轮换/作废该 key；将 `ocr_test.py` 移出跟踪（`git rm --cached` + 加入 `.gitignore`），密钥改由环境变量注入；并修正 tasks.md 中不实的「0 命中」表述。

---

## 2. 上下文说明（不影响本 spec 裁决，但须告知）

当前工作树是**多 spec 并发**的混合状态，除本 spec 外还包含：
- `multi-part-compound-answer-fix`（其 `spec.md`/`tasks.md` 被改，且 `src/.../data/pdf_questions.json` 有 28 题数据改动）；
- `choice-coverage-expansion`（测试真值表含 pdf-388/394/459，`MultiSelectChoiceValidatorTests` 含 pdf-115/475）；
- 一批已暂存的大规模删除（5503 文件，主要为 `_evalscratch/*`、`pdfs/_extract/*` 等清理）。

本 Review **严格限定在本 spec 范围**（`verify_fix_all` → `t5_kb`）：
- 已独立确认 baseline 与 t5_kb 两个 checkpoint 间 757 题的 answer/question/options **零漂移** → 本 spec 未篡改任何题数据，8 个 GAIN 纯来自事实/KB/路由。
- 工作树相对 HEAD 的 `pdf_questions.json` 改动在基线生成时即已存在、且两 checkpoint 间一致 → 属另一在途 spec，不影响本 spec diff 的 LOST=0 / precision=1 结论。
- 独立复跑 `dotnet test` 在当前混合工作树上仍 **2150 passed / 0 failed**，说明本 spec 改动与并发改动无编译/测试冲突。

---

## 3. 总体结论

| 维度 | 裁决 |
|------|------|
| 1. precision=1 | PASS |
| 2. harm=0 | PASS |
| 3. 无回归 LOST=0 | PASS |
| 4. 不蒙数 | PASS |
| 5. 事实只存 JSON | PASS |
| 6. 台账自洽 | PASS |
| 7. 红线 | PASS（本 spec 改动无密钥/未碰 WRONG 正则/未接入守恒近似；**附关键发现**：仓库预存 `ocr_test.py` 密钥泄漏 + 实现者「0 命中」自述不实，须后续处置） |

**最终裁决：PASS。** 7 维度全部通过，无 precision/harm FAIL，满足 T9 rubric `pass threshold ≥ 4`（无 precision/harm FAIL）。建议放行本 spec；同时将「`ocr_test.py` 密钥轮换与移出跟踪 + 修正 tasks.md 不实表述」列为紧邻后续动作（不阻塞本次门禁，但不应带入下游合并）。
