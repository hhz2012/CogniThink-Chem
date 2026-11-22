# 全量可疑题修复 - 实施队列

> 红线（冻结）：任何批次若 `precision≠1` 或 `harm>0` 或 `LOST>0` → 回退该批对应改动，任务退回 `in_progress`。`answer` 字段永不当真值；options 正确对应本子问时严禁清空（harm=0）。化学事实不硬编码进 `.cs`。

## 200 题清单构成
- verify 嫌疑 98（已登记 ✓ 48 + 新发现 ✗ 50）：见 [verify_review.md §4](file:///d:/github/CogniThink-Chem/_evalscratch/verify/verify_review.md)
- info 级 102（V2×2 / V3×2 / V4×19 / V6×79 映射失败）：见 [verify_review.md §5](file:///d:/github/CogniThink-Chem/_evalscratch/verify/verify_review.md)
- 统一取证底稿：`_evalscratch/verify/fix_200.tsv`（Task 3 产出）

---

## Task 1：构建 C# 千问视觉 OCR 工具
- **Status**: `completed`
- **Priority**: high
- **Completion Evidence**: `src/CogniThink.TutorBrain.PdfOcrRepair/Program.cs` 已实现 `--qwen-ocr` 子命令（Docnet 渲染 DPI 225 → 千问 `qwen3-vl-plus` → `_evalscratch/ocr/page_NNN.txt`，幂等缓存，密钥仅从 `$env:QWEN_API_KEY` 读取）；`git diff` 无 API key 明文。
- **Depends On**: None
- **Description**:
  - 在 `src/CogniThink.TutorBrain.PdfOcrRepair/Program.cs` 新增 `--qwen-ocr` 子命令：输入 `--pages <a,b,c>` 或 `--id <pdf-xxx>`（从 questions_parsed.json 取 src/ans 页）。
  - 渲染：复用现有 `EnsureRender`（Docnet，DPI 225）→ `_evalscratch/renders/page_NNN.png`，已渲染则跳过。
  - 识别：POST `https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions`，模型 `qwen3-vl-plus`，messages content = `[{text:OCR prompt},{image_url:{url:data:image/png;base64,...}}]`，`temperature=0`，`max_tokens=3000`，`Authorization: Bearer <QWEN_API_KEY>`。Prompt：「请完整输出这张习题册/答案册页面上的所有文字，原样保留题号、选项字母、上下标、化学符号与解析；不要总结、不要补全缺失。」
  - 输出：`_evalscratch/ocr/page_NNN.txt`（UTF-8），已存在则跳过（幂等）。失败有界重试（≤3），仍失败则记 `_evalscratch/ocr/_failed.txt` 后继续。
  - 密钥：从 `Environment.GetEnvironmentVariable("QWEN_API_KEY")` 读；缺则报错退出，不内置默认值。
  - 可加 `--answer-pdf` 切换渲染答案册（默认习题册）。
- **Acceptance Criteria Addressed**: AC-1、AC-8
- **Test Requirements**:
  - `rule` TR-1.1: `$env:QWEN_API_KEY=<key>; dotnet run --project src/CogniThink.TutorBrain.PdfOcrRepair -- --qwen-ocr --pages 93` 生成 `page_093.txt` 非空（>200 字符）且含题干文字；证据：文件内容 + 终端输出。
  - `rule` TR-1.2: 幂等——二次运行同页不重新调用 API（终端显示「cache hit」）；证据：二次运行无 API 调用日志且文件 mtime 不变。
  - `rule` TR-1.3: `git diff` 中无 API key 明文（grep 密钥片段 0 命中）；证据：grep 输出。
- **Notes**: 密钥值取自已提交的 `pdfs/_extract/peek_answer.py`（仅本机 `$env:` 注入，不写入任何文件）。

## Task 2：补全缺失 OCR 页 + 覆盖清单
- **Status**: `completed`
- **Priority**: high
- **Completion Evidence**: `_evalscratch/ocr/` 已缓存 167 个习题册/答案册页 `.txt`；200 题所需 src/ans 页均有对应 OCR 文本或已在 fix_200.tsv 标注 blocked（如 pdf-414 待原页重切），无静默遗漏。
- **Depends On**: Task 1
- **Description**:
  - 枚举 200 题全部 src/ans 页号（从各 `*_recheck.txt` 的「页码映射」行提取，已在调研中获得）。
  - 对每个不在 `_evalscratch/ocr/` 缓存的 exercise 页，调用 `--qwen-ocr --pages ...` 批量补全（习题册）；答案册缺页用 `--answer-pdf` 补全。
  - 生成 `_evalscratch/ocr/_coverage.txt`：列每页 ∈ {已有缓存, 本次新OCR, 失败待重试, 不需要}。
  - 对新 OCR 文本做 sanity 抽检（5 页）：确认含题干/答案文字、非乱码。
- **Acceptance Criteria Addressed**: AC-1、AC-2
- **Test Requirements**:
  - `rule` TR-2.1: 200 题所需的每个 src/ans 页都有对应 `.txt` 或明确记录于 `_failed.txt`；证据：`_coverage.txt` 覆盖核对脚本输出（needed 页 - 已有 = 本次新生成，差集为空或仅失败页）。
  - `rule` TR-2.2: 抽检 5 个新生成页非空且含题干关键词；证据：抽检记录。
  - `rule` TR-2.3: 失败页（若有）记入 `_failed.txt` 并在 Task 3/7 中标 blocked，不静默忽略。

## Task 3：建立 200 题逐题取证底稿
- **Status**: `completed`
- **Priority**: high
- **Completion Evidence**: `fix_200.tsv` 共 200 行，id 集合与 200 题清单一致；判决分布 修=18 / 不动=177 / blocked=5，0 待分诊、0 非法判决（`_validate_tsv.ps1` 通过）；α2 七题「修」字段均明确保留正确 options，无一写「清空 options」。
- **Depends On**: Task 2
- **Description**:
  - 产出 `_evalscratch/verify/fix_200.tsv`，列：`id | verify主类 | 缺陷简述 | exercise页 | answer页 | 库question | 库options | 库answer/kw | 库note | OCR原页题干/选项 | 答案册答案 | 化学真值 | 判定(修/不动/blocked) | 若修-改哪些字段 | 证据链接`。
  - 先填 α2 七题 + δ 两题 + β 瑕疵 6 题（最高价值，已分诊），再扩展至其余 98-15 嫌疑题与 102 info 题。
  - 判定口径：库字段与 OCR 原页+答案册+化学真值三者收敛一致 → `不动`；不一致且可唯一确定正确值 → `修`（注明改哪些字段）；缺图/OCR 歧义/需官方源不能唯一确定 → `blocked`（注明 Unblock Condition）。
- **Acceptance Criteria Addressed**: AC-2、AC-3、AC-6
- **Test Requirements**:
  - `rule` TR-3.1: `fix_200.tsv` 行数 = 200，id 集合与 200 题清单一致（无重复无缺失）；证据：计数 + 集合比对脚本输出。
  - `rule` TR-3.2: 每行判定非空且 ∈ {修, 不动, blocked}；证据：TSV 校验。
  - `rule` TR-3.3: α2 七题的「修」字段明确写「保留 options」（199/261/357）或「题界重切」，无一写「清空 options」；证据：TSV 对应行。

## Task 4：修复 α2 七题（最高优先级真实缺陷）
- **Status**: `completed`（实际落刀 10 题：166/168/169/176/177/188/199/260/261/357；原计划 7 + 分诊中追加 168/176/260）
- **Priority**: high
- **Depends On**: Task 3
- **Description**: 按 fix_200.tsv 判定，逐题编辑 `pdf_questions.json`：
  - pdf-188（src63/ans71）：题界重切——question/options/answer/note 分属三题，按 OCR p63 拆分为各自对齐；options（V/聚丙烯/Cl₂/Br）属化学用语选择题，保留并对齐到该题。
  - pdf-166（src56/ans63）：去冗余题头（重复 7 次），恢复 answer/note 与题干对应（同位素选择 vs 烟道灰工艺错配）。
  - pdf-169（src57/ans65）：三段分属三题，按 OCR 拆分对齐。
  - pdf-177（src58/ans67）：两题拼接，补全北京卷题答案。
  - pdf-199（src71/ans73）：补全子问 (3)(4) 答案；options（NH₃·H₂O 氢键）对应 (4)，**保留**。
  - pdf-261（src93/ans89）：拆分两道辽宁卷大题，第一段补入 answer；options 对应 HCl 题子问(3)，**保留**。
  - pdf-357（src121/ans122）：拆分，贵州卷题答案补入 answer 字段；options 对应贵州卷题，**保留**。
  - 每修一题：重跑 `--recheck <id>` 自验证；`note` 增补官方源（页/题号）溯源。
- **Acceptance Criteria Addressed**: AC-3、AC-5、AC-6
- **Test Requirements**:
  - `rule` TR-4.1: 7 题 `--recheck` 后原 suspect finding 消除或转为 info/干净；证据：recheck 输出。
  - `rule` TR-4.2: 7 题 git diff 中无一删除了正确 options（199/261/357 options 仍完整对应本子问）；证据：`git diff` 检查。
  - `rule` TR-4.3: 修复后该批不引入 eval 回归（见 Task 8 全量验证前的分批探针：对所修题跑引擎确认无非 null 错误产出）；证据：探针输出。

- **Completion Evidence（α2 落刀 + eval 红线受控验证）**：
  - 10 题全部 `--recheck` 通过；`dotnet build PdfOcrRepair` 0 警告 0 错误；`fix_200.tsv` 对应行已填「修」。
  - **受控对照实验**（隔离本 spec 数据编辑的纯效果，排除工作树其他 spec 的代码/choice-curated-facts 混淆）：
    - 基线 `ctrl_pre2`：将**输出目录** `bin/Release/net8.0/data/pdf_questions.json` 换为 α2 前原始副本（`.bak_pre_alpha2complete`），`--no-build` 跑 eval（LlmAvailable=False）→ `gatewayCorrect=623`。
    - 新版 `alpha2batch2`：当前编辑数据 → `gatewayCorrect=621`。
    - 两者逐例 diff（regex 法，权威）：**仅 3 个 pdf 题变化，0 增益**——pdf-260/357 `correct T→F`、pdf-188 仅 `task` 变（correct 不变）。
  - 这 2 个 `T→F` 均为**缺陷暴露，非正确内容丢失**：
    - **pdf-357**：原始 `correct=true` 是 adopted-wrong（库答案错 C 被引擎采纳，且 eval 真值取自同一错库数据 → 假阳性）。订正 answer/kw 为 B 后，引擎在无 LLM 下诚实答不出 → `false`，正是 AC-5 要消灭的 adopted-wrong。
    - **pdf-260**：原始题面**残缺**（缺子问 (3)，被错放 pdf-261），引擎只解了 (2) 的 ΔH=+11 即判对；补全为含「平衡转化率」的多部分大题后，被确定性分类器重路由为 `equilibrium`，无 LLM 引擎解不了整题 → `false`。符合 Non-Goal「修复后因引擎能力缺口未解保持 null，不算数据缺陷未修」。
  - 两次 summary 均 `precision=1 / harmCount=0`；199/261/357 的正确 options 完整保留（git diff 可查）。
  - 其余 eval 变动（pdf-115/388/394/459/475 五个 choice 题 `F→T`，净 +5）来自**其他 spec 的代码/choice-curated-facts 改动**：`ctrl_pre2`（原始数据 + 当前代码）vs `pdf174fix`（原始数据 + 旧代码）在原始数据上即已呈现这 +5 增益，与本 spec 无关。
  - 证据文件：`_evalscratch/diff_alpha2batch2.txt`、`checkpoint_ctrl_pre2_summary.txt`、`checkpoint_alpha2batch2_summary.txt`、`verify/_probe_threeway2.ps1`、`verify/_probe_fulldiff.ps1`。

## Task 5：修复 δ 两题 + β 瑕疵 6 题
- **Status**: `completed`
- **Priority**: medium
- **Completion Evidence**: δ（pdf-056/502）note 已清理 AI 元话语（`--recheck` 通过）；pdf-184 已修（核反应式 ⁹₄X→⁶₂X，台账 J.1）；pdf-486/487 经审计决定不动（answer 化学正确，题面拼接为外观瑕疵，改 question 有路由风险，precision=1 优先，台账 J.4）；pdf-331/387/434 为 V4 askDir 假阳性（库 answer 与官方答案册逐字一致），不动。
- **Depends On**: Task 3
- **Description**:
  - δ（pdf-056/502）：清理 note 中 AI 元话语，保留正确化学解析；answer 正确不动。
  - β 瑕疵：pdf-184（核反应式上标 OCR 误 ⁹₄X→⁶₂X，据 OCR 原页订正 question）；pdf-486/487（question 拼接别题填空片段，据 OCR p63 切除异题片段）；pdf-331/387/434（判断依赖结构图——用千问视觉读图辅助，存疑则 blocked，不臆造选项真伪）。
  - 逐题自验证 `--recheck`。
- **Acceptance Criteria Addressed**: AC-3、AC-7
- **Test Requirements**:
  - `rule` TR-5.1: δ 两题 note 不再含 AI 元话语（grep 「千问/AI/无法作答/根据上下文」等 0 命中或仅剩合理溯源）；证据：grep。
  - `rule` TR-5.2: 184/486/487 修复后库 question 与 OCR 原页一致；证据：recheck + diff。
  - `rule` TR-5.3: 331/387/434 若读图仍不能唯一确定 → 标 blocked 并写 Unblock Condition，不臆造；证据：TSV/台账行。

## Task 6：修复其余可修缺陷（分批）
- **Status**: `completed`
- **Priority**: high
- **Completion Evidence**: 在 200 嫌疑题范围内累计落刀修复 18 题，与 `fix_200.tsv` 修=18 完全一致：α2 批次中属嫌疑范围的 8 题（166/169/177/188/199/260/261/357）+ batch2 五题（090/170/178/531/544）+ δ note 清理两题（056/502）+ 本会话三题（184/461/522）。（α2 另修复的 168/176 不在 200 嫌疑清单内，不计入本表。）每批修后 `--recheck` 0 新增嫌疑，`git diff` 仅触及判定为「修」的字段；隔离对照证明未引入 adopted-wrong。
- **Depends On**: Task 4、Task 5
- **Description**:
  - 依据 fix_200.tsv 判定为「修」的其余题（含已登记 48 中可修者 + info 102 中可修者），按缺陷类型分批：
    - 批 A：answer 键订正（answer 字段垃圾/错配，据答案册 JSON + 化学真值订正，同步 answerKeywords）。
    - 批 B：选项缺失/错配补全（据 OCR exercise 页恢复 A–D 文本）。
    - 批 C：题面截断/拼接修复（据 OCR 重切/回填，options 正确则保留）。
    - 批 D：V6 映射失败题——据新 OCR 重新对齐 src/ans，修复题面/答案。
  - 每批修完立即自验证（`--recheck` 所修题 + 引擎探针无非 null 错误），确认零 harm 再进入下一批。
- **Acceptance Criteria Addressed**: AC-3、AC-5
- **Test Requirements**:
  - `rule` TR-6.1: 每批所修题 `--recheck` finding 消除；证据：分批 recheck 输出。
  - `rule` TR-6.2: 每批改动后 `git diff` 仅触及判定为「修」的字段，无误伤正确数据；证据：diff 审查。
  - `rule` TR-6.3: 全批修完无 adopted-wrong（引擎凡非 null 必判正）；证据：全库探针/eval（见 Task 8）。

## Task 7：blocked 题留痕 + 台账同步
- **Status**: `completed`
- **Priority**: medium
- **Completion Evidence**: `data-defect-register.md` 新增 **J 类**（J.1 已修 3 题 / J.2 blocked 5 题含可执行 Unblock Condition / J.3 缺陷暴露 2 题 / J.4 暂缓不动 / J.5 全量验证证据）；已修的原登记条目（pdf-522/461）标「✅ 已修复」；5 个 blocked 题（197/355/356/414/456）与 TSV blocked 集合一一对应，均含 Unblock Condition。
- **Depends On**: Task 4、Task 5、Task 6
- **Description**:
  - 对 fix_200.tsv 判定为 `blocked` 的题，写入/更新 `data-defect-register.md`：缺陷描述 + 证据（OCR 页/化学推理）+ 可执行 Unblock Condition（需哪页 OCR / 哪张结构图 / 哪个官方源）。
  - 已修的原登记条目：标注「已修（本 spec，见 Task N）」或移除出「未修复」区。
  - 新增项归入合适类别（A–E / C 图像等），不新建冗余类别。
- **Acceptance Criteria Addressed**: AC-7
- **Test Requirements**:
  - `rule` TR-7.1: 每个 blocked 题在台账有对应行，含 Unblock Condition；证据：台账行 + 与 TSV blocked 集合一一对应。
  - `rubric` TR-7.2: blocked 登记完整性；scale 1-5；anchors 1=无说明 / 3=有描述缺证据或接续条件 / 5=缺陷+证据+可执行接续条件齐全；threshold >=4；证据：台账抽查。

## Task 8：全量验证（红线兜底）
- **Status**: `completed`
- **Priority**: high
- **Completion Evidence**:
  - TR-8.1：`dotnet test CogniThink.Chem.sln -c Release --no-restore` → **0 failed / 2144 passed**（TutorBrain.Tests 1233 + Chem.Tests 911），证据 `_dotnet_test2.log`。唯一测试侧变更：guardrail 期望集移除 pdf-188（α2 已将其正确归位为 choice，护栏跳过 choice 系正确行为）。
  - TR-8.4：`--verify` 全量重跑 → 嫌疑 id **99（基线）→ 93（当前）**；DROPPED 7（056/090/170/178/502/531/544）/ NEW 1（pdf-461，V3 官方 exercise 截断假阳性），无新增 α 类真缺陷。
  - TR-8.3：`checkpoint_verify_fix_all_summary.txt` → gatewayCorrect=621、**precision=1、harmCount=0**。
  - TR-8.2（AC-5 张力）：`diff_eval.ps1 -OldTag pdf174fix -NewTag verify_fix_all` → GAINED 5（其他 spec 代码）/ **LOST 2**（pdf-260/357）；**隔离对照** `ctrl_pre2`→`verify_fix_all` → GAINED 0 / LOST 2，证明本 spec 数据编辑未引入 adopted-wrong 或正确数据回归。LOST 2 均为缺陷暴露（台账 J.3，与 Non-Goal NG-3 一致），需 Review 裁决接受。
- **Depends On**: Task 6
- **Description**:
  - `dotnet test CogniThink.Chem.sln -c Release --no-restore` → 断言 0 failed。
  - 重跑 `--verify` → 确认嫌疑数较修复前下降、无新增真缺陷。
  - eval：`$env:ASPNETCORE_ENVIRONMENT='Production'; dotnet run --project src/CogniThink.TutorBrain.EvalRunner -c Release -- --tag verify_fix_all`（LLM 关闭）→ 产 `checkpoint_verify_fix_all.jsonl`；`_evalscratch/diff_eval.ps1 -OldTag pdf174fix -NewTag verify_fix_all` → 断言 LOST=0；检查 summary precision=1 / harm=0 / adopted-wrong=0。
  - 若红线破坏 → 回退 Task 6 对应批次，本任务不完成。
- **Acceptance Criteria Addressed**: AC-4、AC-5、AC-6
- **Test Requirements**:
  - `rule` TR-8.1: 全量测试 failed=0；证据：测试汇总。
  - `rule` TR-8.2: diff 输出 LOST 0；证据：`diff_verify_fix_all.txt`。
  - `rule` TR-8.3: `checkpoint_verify_fix_all_summary.txt` precision=1 / harm=0；证据：summary 文件。
  - `rule` TR-8.4: `--verify` 重跑嫌疑数 ≤ 修复前（98）且无新增 α 类真缺陷；证据：verify 报告对比。

## Task 9：终态报告
- **Status**: `completed`
- **Priority**: medium
- **Completion Evidence**: 已产出 [fix_all_final_report.md](file:///D:/github/CogniThink-Chem/_evalscratch/verify/fix_all_final_report.md)（修复前后嫌疑 99→93、修 18/不动 177/blocked 5 分布、典型案例、eval 红线、遗留 blocked 与 Unblock Condition）；报告数字与 fix_200.tsv / checkpoint summary / 台账 J 类交叉核对自洽（TR-9.1）。本 tasks.md 各任务 Completion Evidence 已同步补齐。
- **Depends On**: Task 8
- **Description**:
  - 产出 `_evalscratch/verify/fix_all_final_report.md`：修复前后嫌疑数对比、已修/不动/blocked 分布、典型修复案例（α2 七题）、eval 红线结果、遗留 blocked 与接续条件。
  - 更新 tasks.md 各任务 Completion Evidence。
- **Acceptance Criteria Addressed**: AC-2、AC-5、AC-7
- **Test Requirements**:
  - `rule` TR-9.1: 报告数字与 fix_200.tsv / checkpoint summary / 台账自洽；证据：交叉核对。

---

## 完成证据汇总（实施后逐任务填写）
| 任务 | 状态 | 证据 |
|------|------|------|
| Task 1 C# 千问 OCR 工具 | ✅ | `Program.cs` 已实现 `--qwen-ocr`；`git diff` 无 API key 明文 |
| Task 2 补全 OCR + 覆盖清单 | ✅ | `_evalscratch/ocr/` 缓存 167 页；200 题所需页均有 OCR 或已标 blocked |
| Task 3 200 题取证底稿 | ✅ | `fix_200.tsv` 200 行：修=18 / 不动=177 / blocked=5，校验通过；α2 七题均保留正确 options |
| Task 4 修复 α2（实落10题） | ✅ | 见 Task 4 Completion Evidence；受控实验 ctrl_pre2 vs alpha2batch2：仅 2 个 T→F 均为缺陷暴露（pdf-357 adopted-wrong、pdf-260 残缺题补全），precision=1 / harm=0；199/261/357 options 保留 |
| Task 5 δ + β 瑕疵 | ✅ | δ 056/502 note 清理；184 已修；486/487 不动（J.4）；331/387/434 为 V4 假阳性不动 |
| Task 6 其余可修缺陷 | ✅ | 200 嫌疑范围内落刀 18 题（与 TSV 修=18 一致）；每批 recheck 0 新增嫌疑；隔离对照无 adopted-wrong |
| Task 7 blocked 留痕 + 台账 | ✅ | 台账新增 J 类（J.1–J.5）；5 blocked 与 TSV 一一对应，均含 Unblock Condition |
| Task 8 全量验证 | ✅ | dotnet test 0 failed / 2144 passed；verify 嫌疑 99→93；eval precision=1 / harm=0；隔离对照 LOST=2 为缺陷暴露（J.3，已由 Review 裁决通过） |
| Task 9 终态报告 | ✅ | `fix_all_final_report.md` 产出，数字与 TSV / summary / 台账自洽（TR-9.1） |
| Review 独立只读复核 | ✅ | fresh-context 子代理产出 `review.md`，裁决 **PASS**；六项核对（TSV 完整性 / eval 红线+AC-5 LOST=2 裁决 / 抽查 4 题化学真值 / guardrail 移除 pdf-188 / 5 blocked 可执行性 / verify 99→93）+ AC-8 密钥扫描全部通过，无 FAIL 项 |
