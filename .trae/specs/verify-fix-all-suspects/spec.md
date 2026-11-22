# 全量可疑题修复（千问视觉 OCR 解锁 + 数据修复）- Product Requirements Document

## Overview
- **Summary**: 先用 C# 调用千问多模态 API（`qwen3-vl-plus`）把习题册/答案册所有缺失文本层的扫描页渲染+OCR 成可检索文本，解锁证据；再对 `pdf_questions.json` 中全部 200 道被标记的题（verify 嫌疑 98 + info 级 102）逐题做「库现状 vs OCR 原页 vs 答案册 vs 化学真值」对照，修复所有可修复的数据缺陷，受阻题逐题留痕。
- **Purpose**: 清理题库数据缺陷欠账（题界切分/拼接、题答错配、答案不全、选项缺失/错配、note AI 元话语等），在不破坏 `precision=1 / harm=0 / LOST=0` 红线的前提下提升题库正确性与可解性。
- **Target Users**: 化学题库与求解引擎的维护者。

## Goals
1. 落地一个 C# 千问视觉 OCR 工具：能渲染任意 PDF 页（Docnet）并调用 `qwen3-vl-plus` 返回识别文本，缓存到 `_evalscratch/ocr/`，带幂等缓存。
2. 200 道题逐题有明确检验结论：`修复` / `库正确不动` / `blocked`，零遗漏。
3. 修复全部可修复缺陷；α2 七题严格按「保留正确 options、重切题界/补全答案」执行。
4. 零回归：`dotnet test` 全量 0 failed；eval diff 对绿色基线 `checkpoint_pdf174fix` 满足 `LOST=0 / precision=1 / harm=0`。
5. blocked 题逐题留痕于 `data-defect-register.md`，含缺陷描述 + 证据 + 可执行 Unblock Condition。

## Non-Goals
- 不新增化学求解器能力，不把化学事实硬编码进 `.cs`（化学事实只进 JSON 数据）。修复后仍因引擎能力缺口未解的题保持 `null`，登记为能力缺口，不算数据缺陷未修。
- 不处理本 200 清单之外的题。
- 不把 `answer` 字段当真值；真值以 `answerKeywords[0]` + 官方答案册 + 化学真值三者收敛为准。
- 不删除/覆盖任何经核对正确的 `question / options / answer` 数据（harm=0）。
- 不安装 tesseract；OCR 统一走千问视觉 API。

## Background & Context
- 证据基线：[_evalscratch/verify/verify_review.md](file:///d:/github/CogniThink-Chem/_evalscratch/verify/verify_review.md)（总 545 / findings 300 / 嫌疑 98 / info 102 / 干净 345）；[_evalscratch/verify/triage_50.tsv](file:///d:/github/CogniThink-Chem/_evalscratch/verify/triage_50.tsv) 与 [triage_report.md](file:///d:/github/CogniThink-Chem/_evalscratch/verify/triage_report.md)（50 新发现分诊：α2 7 / β 39 / γ 2 / δ 2）；[verify-50-triage/review.md](file:///d:/github/CogniThink-Chem/.trae/specs/verify-50-triage/review.md)（α2 100% CONFIRMED、β 抽查 LOST=0；α3→α2、删除「清空 options」修法已落实）。
- 台账：[data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 已登记 48 缺陷（含 7 α2 中的若干），多数标注「暂保留 null，需原页/图像」。
- 可行性证据：前序会话已生成 OCR 缓存 `_evalscratch/ocr/page_NNN.txt`（exercise 000–072 连续 + 101–106 / 126–127 / 131–132 / 141–145），答案册 `pdfs/_extract/answer/` 覆盖 000–153；源 PDF 存在；`PdfOcrRepair` 已有 Docnet 渲染 PNG 能力（[Program.cs EnsureRender](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.PdfOcrRepair/Program.cs)）。
- 千问 API 范式已存在：[pdfs/_extract/peek_answer.py](file:///d:/github/CogniThink-Chem/pdfs/_extract/peek_answer.py)（端点 `https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions`，模型 `qwen3-vl-plus`，base64 image_url，Bearer 鉴权）。本机 Python 为 Store stub 不可用，故移植到 C#（可复用 [LlmJudgeService](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/LlmJudgeService.cs) 的 HttpClient 模式）。
- 绿色基线：`checkpoint_pdf174fix`（gatewayCorrect=618、precision=1、harm=0）。`multi-part-compound-answer-fix` 已 completed 且对 `pdf_questions.json` 零新增改动，无活跃冲突。
- 交叉映射：98 嫌疑题的 src/ans 页码已从 `*_recheck.txt` 提取（见 tasks.md 取证底稿）；其中约 38 题的 exercise 源页不在现有 OCR 缓存（含 α2 的 pdf-261 src=93、pdf-357 src=121），需千问 OCR 解锁。

## Functional Requirements
- **FR-1**: 在 `CogniThink.TutorBrain.PdfOcrRepair` 新增千问视觉 OCR 子命令（如 `--qwen-ocr`）：输入页号/页范围或题号 → Docnet 渲染 PNG（默认 DPI 225，复用 `_evalscratch/renders` 缓存）→ POST 千问 `qwen3-vl-plus`（图片 base64 data URI + OCR prompt，temperature=0）→ 写出 `_evalscratch/ocr/page_NNN.txt`，已存在则跳过（幂等）。API key 从环境变量 `QWEN_API_KEY` 读取，不硬编码、不提交。
- **FR-2**: 用该工具补全全部 200 题所需且缺失的 exercise 页 OCR（含 073+、107–120、122–125、128–130、133–140、146+ 等缺口），以及答案册缺页；生成 `_evalscratch/ocr/_coverage.txt` 记录每页状态。
- **FR-3**: 对 200 题逐题建立取证底稿（`_evalscratch/verify/fix_200.tsv`）：列 id / verify 主类 / 缺陷 / exercise OCR 页 / answer 页 / 库四字段现状 / 化学真值 / 判定（修-字段 / 库正确不动 / blocked）/ 证据链接。
- **FR-4**: 修复 `pdf_questions.json`：仅改经核对确有缺陷的字段（question / options / answer / answerKeywords / note），保留正确数据；α2 三题（199/261/357）及任何 options 正确对应本子问的题，**严禁清空 options**；修复时使 answer / answerKeywords / note 与题面一致。
- **FR-5**: 每修一批自验证：对所修题重跑 `dotnet run --project src/CogniThink.TutorBrain.PdfOcrRepair -- --recheck <id>` 确认 finding 消除；全部修完重跑 `--verify` 确认嫌疑数下降。
- **FR-6**: 同步 `data-defect-register.md`：已修条目标注已修/移除；新 blocked 条目新增，含 Unblock Condition。
- **FR-7**: 对依赖结构图/装置图的题，可用千问视觉模型读图辅助判断，但结论必须经化学真值交叉核对；存疑即标 blocked，不臆造。

## Non-Functional Requirements
- **NFR-1（α precision=1）**: 修复后没有任何一道原本库正确的题被改成错误或被误判为缺陷。
- **NFR-2（harm=0）**: 不删除/覆盖任何经核对正确的 `question / options / answer` 数据；尤其不得清空正确对应本子问的 options。
- **NFR-3（LOST=0）**: 不遗漏真缺陷；200 题每题有明确结论。
- **NFR-4**: 化学事实不硬编码进 `.cs`；OCR 工具是纯基础设施，不含化学判定逻辑。
- **NFR-5**: 千问 API key 不进入任何提交文件（`git diff` 可证）；运行时经环境变量注入。
- **NFR-6**: 每处数据改动可追溯到 OCR 原页 / 答案册证据（note 附溯源或 tasks 留证据）。
- **NFR-7**: 千问 API 调用串行/限速，避免触发配额；失败重试有界，失败页标 blocked 不阻塞全局。

## Constraints
- **Technical**: Windows PowerShell 5（无 `&&`，用 `;`）；dotnet 8；`PlatformTarget=x64`（pdfium）；网络可达千问端点。
- **Business**: `precision=1 / harm=0 / LOST=0` 红线不可破；破则回退对应改动。
- **Dependencies**: 千问 API key（运行时 `QWEN_API_KEY`，值取自已提交的 `peek_answer.py`，仅本机注入不扩散）；源 PDF；现有 OCR 缓存与答案册 JSON；eval 管线（EvalRunner + diff_eval.ps1）。

## Assumptions
- 千问 API key 有效、`qwen3-vl-plus` 对化学题页 OCR 质量足以定位题界/答案（必要时人工核对缓存 txt）。
- 原页 OCR 能唯一确定题界；若仍歧义，标 blocked 不猜。
- eval 管线可在本环境以 `ASPNETCORE_ENVIRONMENT=Production`（LLM 关闭）运行。
- 修复题界可能改变题干文本从而影响引擎路由；以 eval diff 红线兜底，不接受任何 LOSS/HARM。

## Acceptance Criteria

### AC-1: C# 千问 OCR 工具可用
- **Type**: `rule`
- **Given**: 一个此前无文本层的扫描页（如 exercise p93，`_evalscratch/ocr/page_093.txt` 不存在），且环境变量 `QWEN_API_KEY` 已设
- **When**: 运行 `dotnet run --project src/CogniThink.TutorBrain.PdfOcrRepair -- --qwen-ocr --pages 93`
- **Then**: 生成 `_evalscratch/ocr/page_093.txt` 非空，且含可识别的题干文字（非仅页眉/广告）
- **Pass Condition**: 文件存在、长度 > 200 字符、含题干关键词
- **Evidence**: 文件路径 + 前若干行内容

### AC-2: 200 题逐题有结论，零遗漏
- **Type**: `rule`
- **Given**: 200 题清单（verify 嫌疑 98 + info 102）
- **When**: 取证底稿 `fix_200.tsv` 完成
- **Then**: 200 行，每行判定 ∈ {修, 库正确不动, blocked}，无 id 缺失
- **Pass Condition**: TSV 行数 = 200 且 id 集合与清单一致
- **Evidence**: `fix_200.tsv` + 计数脚本输出

### AC-3: 已修题三者收敛
- **Type**: `rule`
- **Given**: 任一标记「已修」的题
- **When**: 独立核对库四字段 vs OCR exercise 页 vs 答案册 JSON vs 化学真值
- **Then**: 四者一致，无残留矛盾（题界/选项/答案/note 对齐）
- **Pass Condition**: 逐题复核 100% 通过
- **Evidence**: 逐题 recheck / 复核记录

### AC-4: 全量单元测试零失败
- **Type**: `rule`
- **Given**: 修复完成后的代码/数据
- **When**: `dotnet test CogniThink.Chem.sln -c Release --no-restore`
- **Then**: 0 failed
- **Pass Condition**: 测试汇总 failed=0
- **Evidence**: 终端测试汇总

### AC-5: eval 红线零回归
- **Type**: `rule`
- **Given**: 绿色基线 `checkpoint_pdf174fix`
- **When**: 运行 EvalRunner 产新 tag，`diff_eval.ps1 -OldTag pdf174fix -NewTag <new>`
- **Then**: LOST=0、precision=1、harm=0（adopted-wrong=0）
- **Pass Condition**: diff 输出 LOST 0；新 checkpoint summary precision=1 / harm=0
- **Evidence**: `_evalscratch/diff_<new>.txt` + `checkpoint_<new>_summary.txt`

### AC-6: α2 七题修法合规
- **Type**: `rule`
- **Given**: pdf-188/199/261/357/166/169/177
- **When**: 检查修复后的 `options` 字段与题面对应
- **Then**: 无任何一题被清空了原本正确对应本子问的 options；题界/答案已对齐
- **Pass Condition**: 7/7 满足
- **Evidence**: 逐题 git diff + recheck

### AC-7: blocked 留痕质量
- **Type**: `rubric`
- **Dimension**: blocked 题登记的完整性与可接续性
- **Scale**: 1-5
- **Anchors**: 1 = 仅标 blocked 无说明；3 = 有缺陷描述但缺证据或接续条件；5 = 缺陷描述 + OCR/化学证据 + 可执行 Unblock Condition（需哪页 OCR / 哪张图 / 哪个官方源）齐全，他人可直接接续
- **Pass Threshold**: >= 4
- **Evidence**: `data-defect-register.md` 对应行

### AC-8: 密钥不入库
- **Type**: `rule`
- **Given**: 本 spec 全部改动
- **When**: `git diff` 与新增文件
- **Then**: 无任何文件新增/修改包含千问 API key 明文
- **Pass Condition**: grep 密钥片段在工作树 diff 中 0 命中
- **Evidence**: grep 输出

## Open Questions
- [ ] 千问 API 速率/配额上限？（缓解：串行 + 有界重试 + 缓存；若触发限流则降速，不阻塞全局）
- [ ] 修复题界后是否需要为新解锁的题策展化学事实以提升 GAIN？（本 spec 不策展；保持 null 亦可，GAIN 非验收项，零回归才是）
