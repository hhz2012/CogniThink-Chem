# PdfOcrRepair 校验与「重读混乱题」能力扩展 实现计划

## Repository Research（现状与约束）

**现有工具** [`Program.cs`](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.PdfOcrRepair/Program.cs)（479 行）：
- 子命令：`<pdf-id>`（渲染+OCR 单题页、打印「缺失选项建议」，只建议不写回）；`--scan`（全量渲染+OCR → `_page_manifest.json`）。
- PDF 读取仅走 `Docnet.Core` 渲染 → `tesseract.exe` OCR，**无文本层抽取、无任何校验逻辑**。
- `QuestionEntry` 只反序列化 `id/question/options/images/answer`，**不加载 `answerKeywords/note/type`**（校验必需字段缺失）。
- 依赖闭包仅 `Docnet.Core`（自带 pdfium.dll），OCR 走子进程；csproj 明确禁止引入新 .NET OCR 封装包。

**仓库已存在、但 C# 工具尚未利用的官方真值源**（前序 Python 管道从 PDF 文本层抽取，干净可靠）：
- [`pdfs/questions_parsed.json`](file:///d:/github/CogniThink-Chem/pdfs/questions_parsed.json)：每条 `id → src_page / ans_page / num`（权威页码映射）。
- `pdfs/_extract/exercise/page_{N}.json`：每页结构化官方题面（`num / type / question / options`）。
- `pdfs/_extract/answer/page_{N}.json`：每页官方答案（`answer` 字段常被污染成 `"A,B,C,D"`；**`explanation` 是可靠官方解析源**）。

**缺陷分类基准** [`data-defect-register.md`](file:///d:/github/CogniThink-Chem/data-defect-register.md)：A 类（答案键冲突/垃圾）、B 类（子项原文缺失）、C 类（依赖图像）、D 类（note/选项文本损坏但键对）、E 类（题干拼接/大题碎片）。校验工具应能自动检出 A / D / E，并把 B / C 标为「待人工/待图像轨道」。

**红线（全程守住）**：`precision=1`、`harmCount=0`、`LOST=0`、零误报；化学事实只写 JSON 绝不硬编码进 `.cs`；答案键改动须有官方源留痕。**本工具为只读/只报告，绝不自动写回 `pdf_questions.json`**——从机制上保证不可能引入 harm/LOST。

**已确认的 API 能力**：反射 `Docnet.Core.dll` 证实 `IPageReader.GetText()` 存在 → 可直接读取 PDF 文本层，实现真正的「读取 PDF」，无需对有文本层的页走 OCR。

## Files and Modules

- [`src/CogniThink.TutorBrain.PdfOcrRepair/Program.cs`](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.PdfOcrRepair/Program.cs)：在现有文件内扩展 `--verify` 与 `--recheck` 两个子命令、文本层抽取 helper、扩展数据模型。**不新增文件、不新增 NuGet 依赖**（保持 Docnet-only 闭包）。
- `_evalscratch/verify/`（新建 scratch 目录，已 gitignore）：输出校验报告与重读对照，不污染仓库。

## Implementation Steps（依赖有序）

1. **扩展数据模型**：`QuestionEntry` 增加 `answerKeywords (List<string>?)`、`note (string?)`、`type (string?)`（带 `JsonPropertyName`）；`LoadQuestions` 反序列化完整模型。`images` 保留。
2. **加载页码映射**：新增 `LoadParsedMap` → `Dictionary<string,(int srcPage,int ansPage,int num)>`，读 `questions_parsed.json`。
3. **加载官方抽取**：新增 `LoadExercisePages`（glob `page_*.json` → `page→List<ExerciseItem{num,question,options}>`，解析 `raw` JSON 串字段）与 `LoadAnswerPages`（→ `page→List<AnswerItem{num,answer,explanation,module}>`）。
4. **PDF 文本层抽取**：新增 `GetPageText(pdfPath,pageIndex)` 调 Docnet `GetText()`；返回文本及「是否有文本层」标记（空/过短 → 标记无文本层）。
5. **`--verify` 子命令**（核心）：对全库 545 题逐题跑检查电池，收集 `DefectFinding{id,severity,class,reason,evidence}`：
   - **V1 答案键损坏**：`answer` 垃圾值（`A,B,C,D` / `1,3,1,3` / 不匹配 `^[A-G]+(,[A-G]+)*$`）；`answerKeywords` 为空；`answer` 与 `answerKeywords[0]` 不一致（两者皆单字母时）。
   - **V2 选项缺失/错配**：按 `src_page+num` 定位官方 exercise 页，比对 A–D 完整性与文本（归一空白后比对）；库缺选项或与官方不一致 → 标嫌疑并附官方原文片段。
   - **V3 题干截断/拼接**：库 `question` 与官方 exercise `question` 比对；差异过大或检出双题干拼接（如出现两个「下列…的是」）→ 标 E 类嫌疑。
   - **V4 官方答案交叉核对**：按 `ans_page+num` 取答案册 `explanation`；用 R1/R2/R8 规则核对 explanation 的「X正确/错误」结论与库 `answer/answerKeywords/note` 是否矛盾；答案册 `answer` 字段本身垃圾仅单独提示，**不作真值**。
   - **V5 note 自相矛盾**：移植 `_evalscratch/scan_analyze.ps1` 的 R5（误标/答案应为/推断本题答案关键词）、R7（note 过短 <12 疑似截断）、R8（note 指向其他字母）。
   - **V6 映射/字段缺失**：id 不在 parsed map、`src_page/ans_page` 缺失、`num` 在官方页找不到、关键字段缺失 → 标「待人工」，不臆造结论。
   - `askDir`（CORR/WRONG）从题干关键词推断（`错误的是/不正确的是/不能/不属于` → WRONG，否则 CORR），因为 `pdf_questions.json` 未存该字段。
   - 输出 `_evalscratch/verify/verify_report.json`（全量 findings）+ 控制台汇总（按 class 计数 + 嫌疑 id 清单）。**全程只读，不写回题库**。
6. **`--recheck <id...>` 子命令**（重读混乱题）：
   - 支持 `--recheck pdf-197 pdf-355 ...` 或 `--recheck --from-report`（消费 verify_report 的嫌疑清单）。
   - 对每题：解析页码 → **用 `GetPageText()` 新鲜抽取 PDF 文本层**（绕过缓存 `_extract`，实现「重新读取」）；文本层缺失或加 `--ocr` 时，复用既有 `EnsureRender+EnsureOcr(--force)` 重渲染重 OCR。
   - 生成 `_evalscratch/verify/<id>_recheck.txt`：[库内字段 | 新鲜 PDF 文本 | 官方 exercise | 答案册 explanation] 对照，高亮矛盾处；控制台只给「建议」，不写回。
7. **Main 分发重构**：解析子命令（`<id>` / `--scan` / `--verify` / `--recheck`），保证既有两条路径行为**零改动**（防回归）。
8. **构建与冒烟**：`dotnet build -c Release`；跑一次 `--verify` 产出报告，确认无异常、计数合理。

## Dependencies and Considerations

- **零新依赖**：保持 `Docnet.Core` 单包闭包，文本层走 Docnet `GetText()`，OCR 仍走 `tesseract.exe` 子进程（符合 csproj 注释与 Experience 约束）。
- **只读保证**：工具仅写入 `_evalscratch/verify/`；绝不触碰 `pdf_questions.json` / `data/highschool/*.json` / 引擎代码 → 不可能改变判分，从机制上守住 `harm=0 / LOST=0`。
- **重复键 caveat**：`pdf_questions.json` 存在重复键，`System.Text.Json` 默认取末值（与 Api 加载 eval 时行为一致）。校验工具**不做去重**（避免改变 eval 真值），但在字段缺失时标 V6 而非猜测。
- **文本层兜底**：先 `GetText()`；某页文本层空/过短 → 回退到缓存的 `_extract/exercise` 官方抽取（权威）并标「该页无文本层，结果来自缓存抽取」，绝不基于空文本断言错配。
- **范围限定**：仅校验 `pdf_questions.json`（与 `questions_parsed.json` 的 pdf 范围一致）；不含 `tier2_questions.json`。

## Validation

- `dotnet build src/CogniThink.TutorBrain.PdfOcrRepair -c Release` 通过，无警告升级错误。
- `dotnet run --project ... -- --verify` 跑完 545 题，写出 `verify_report.json`，无崩溃。
- **零误冒烟**：登记册已知缺陷（pdf-100/197/227/355 等）必须出现在嫌疑清单且 class 正确；人工挑选的干净题（pdf-001~010）**不得被误报**（V1–V5 全清）。
- **无回归**：`dotnet run -- pdf-381` 与 `--scan` 行为不变（diff Program.cs 既有路径确认）。
- **零数据污染**：`git status` 仅显示 `Program.cs` 与本计划文档改动；`pdf_questions.json` / 数据 JSON 均未被修改。
- 全量测试（可选，工具不进生产）：`dotnet test CogniThink.Chem.sln -c Release` 确认计数与基线一致（未触碰生产代码，预期无变化）。

## Risks

- **误报风险**：保守规则 + 每条 finding 附 evidence 片段；干净题冒烟（pdf-001~010）必须全清才交付。
- **GetText 乱码/空页**：<阈值字符视为无文本层 → 回退缓存 `_extract` + 标页待 OCR，不据此断言错配。
- **答案册 explanation 标签被 OCR 污染**（如 pdf-100）：V4 仅标「潜在矛盾」（severity=suspect/info），不断言；`answer` 字段永不作真值。
- **重复键丢字段**：缺字段 → 标 V6「数据字段缺失」，不猜测补全。
- **明确不做（防越界）**：不自动改 `pdf_questions.json`、不策展化学事实、不改 `answerKeywords`、不加引擎能力——全部留给人工按登记册复核。
