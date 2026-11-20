# choice P3 batch3 纯文本选择题事实策展（+4）实现计划

## 仓库研究结论

- 当前基线 `checkpoint_choice_p3_b2`：未判正 choice 共 **169** 道。其中 **FIGURE**（`case.images` / `case.imageAssets` / `structuredAssetKind` 非空）= **132**，**PURE_TEXT**（无图无资源）= **37**。
- 37 纯文本中：4 选项完整 32、单字母答案 25；同时满足（4 选项 + 单字母 answer）的候选 **21** 道，全部 `gatewayUsedEngine=False`（引擎从未介入，需新增事实）。
- 逐题核验源 `pdf_questions.json`（区分大小写按行提取，避开 PS5 `ConvertFrom-Json` 对重复 `A`/`a` 键的崩溃），21 道按可策展性分流：

  | 分类 | 题 | 处置 |
  |------|-----|------|
  | 高置信可策展 | pdf-105(D)、pdf-106(B) | 本轮策展 |
  | 中置信可策展（错则回退） | pdf-095(A)、pdf-403(B) | 本轮策展，护栏验证 |
  | 组合/计数型（选项为 ①②③④ 组合或"正确的个数"，匹配键通用全库碰撞） | pdf-009、pdf-012、pdf-020、pdf-065、pdf-263、pdf-337 | 排除（不在事实策展范围） |
  | 题干/选项隐含结构图但资源未挂载 | pdf-237、pdf-319、pdf-331、pdf-349、pdf-376、pdf-382、pdf-384、pdf-394、pdf-436 | 排除（需图像解析） |
  | 氧化还原多步电子转移、备注不全 | pdf-075 | 排除 |
  | 备注与 answer 矛盾（备注称 D 不合理，存储 answer=A） | pdf-399 | 排除（数据矛盾，不蒙数） |

- **天花板提示**：132 道图片题中有 **113** 道具备完整 4 选项 + 单字母 answer，但属图像解析范畴（Task 43 路线），不在本轮"非图片纯文本"范围。纯文本事实策展的低风险余量经此轮后基本枯竭。

### 4 道策展题的化学真值（源自备注/答案键，非 LLM 猜测）

- **pdf-105**（stem「实验方案、现象和结论都正确的是」→ 选 verdict==true）：A 错、B 错、C 错、**D 对** → answer D。
- **pdf-106**（stem「能达到探究目的的是」→ 选 verdict==true）：A 错、**B 对**、C 错、D 错（由单选 answer=B 推定）→ answer B。
- **pdf-095**（stem「劳动项目与所述化学知识没有关联的是」→「没有关联」命中 WrongDir → 选 verdict==false）：A 为无关联项（false）、B/C/D 有关联（true）→ answer A。注：此题为配对题，"化学知识"列在数据中缺失（数据缺陷），但备注对 4 选项 verdict 完整且与 answer 一致。
- **pdf-403**（stem「能得出相应结论的是」→ 选 verdict==true）：A 错、**B 对**、C 错、D 错（C/D 备注被 OCR 截断，由单选 answer=B 推定）→ answer B。

## 文件与模块

- `data/highschool/general-choice-facts.json`：追加 **16** 条 `GC-*` 事实（4 题 × 4 选项），`statements` 504 → 520。
- `tests/CogniThink.TutorBrain.Tests/Batch3ChoiceValidatorTests.cs`：`ConceptTruth` +4（pdf-105/106/095/403），10 → 14。

## 实现步骤

1. 为 4 题各写 4 条事实：`match` 取选项文本中唯一片语（逐字拷贝，匹配器两侧归一化一致），`stemMatch` 限定题干（须全库唯一），`verdict` 按上表核对，`reason` 简短。追加到 `statements` 数组末尾，给前一条补尾逗号。
2. JSON 校验：`ConvertFrom-Json` 通过 + 手动扫描 `]`/`}` 前无尾随逗号（`System.Text.Json` 严格，PS5 的 `ConvertFrom-Json` 宽松会漏）。
3. `ConceptTruth` 增 4 条；确认无重复键、字母与存储 answer 一致。
4. 删 `_Tmp*.cs` 探针 → 杀 dotnet/testhost 残留 → build → 全量测试：TutorBrain 期望 **941/0**（937+4）、Chem **905/0**；重点确认 `Batch3ChoiceValidatorTests` 四护栏（`Concept_ZeroFalsePositive` / `Concept_Coverage` / `Concept_AdoptedAnswers` / `Concept_NoOverlap`）全绿。
5. `EvalRunner` 全库 `tag=choice_p3_b3`，diff vs `choice_p3_b2`：要求 **GAIN≈4、LOST=0、precision=1.0、harm=0**；逐题核对 4 道 `gatewayAnswer` 字母与存储 answer 一致。
6. **失败纪律**：任一题产出错字母或致任何回归，删除该题 4 条事实及其 `ConceptTruth` 条目（回退为 `null`，安全），不强行。
7. 更新 `.trae/specs/chem-engine-question-solving/tasks.md` Task 40 进度与指标；用 UTF-8 msg 文件提交。

## 依赖与注意事项

- 匹配器按内容匹配（`match[]` ⊆ 归一化选项 ∧ `stemMatch[]` ⊆ 归一化题干），事实 `id` 仅装饰；须保证 `stemMatch` 全库唯一、`match` 关键词在该题选项内唯一且不与他题碰撞。
- `WrongDir` 正则含「没有关联」→ 选 `verdict==false`；「都正确的是」「能达到探究目的的是」「能得出相应结论的是」不命中 → 选 `verdict==true`。须据此设 verdict。
- 红线不变：LLM 不作化学真值源（备注/答案键为唯一真值）、凡采纳必判正（`IsAnswerCorrect`）、无唯一解返回 `null`、零假阳性零 harm 零回归、仅增量接线不改引擎逻辑。
- PS5 限制：`.ps1` 保持 ASCII、中文数据走 UTF-8 `.txt`；`git commit` 用 `-F` UTF-8 文件（PS5 中文 heredoc 失败）。

## 验证

- JSON 严格解析通过、无尾随逗号；facts 数 504 → 520（Grep 计数读回校验）。
- TutorBrain **941/0**、Chem **905/0**；`Batch3ChoiceValidatorTests` 全绿（覆盖数 == `ConceptTruth.Count`==14）。
- EvalRunner diff vs `choice_p3_b2`：GAIN 4、LOST 0、precision 1.0、harm 0；附 4 道字母核对表（105=D、106=B、095=A、403=B）。

## 风险

- **pdf-095 配对题数据缺陷**（知识列缺失）：若 `match`/`stemMatch` 碰撞可能误命中他题 → 由 `Concept_ZeroFalsePositive` 护栏 + 全库 EvalRunner diff 兜住，错则回退该题。
- **pdf-403 备注截断**：C/D verdict 为推定（单选 answer=B）；若实际为多选误标，答案键仍保证字母，护栏验证后错则回退。
- **外部进程重放工作树改动**：每步读回校验（Grep 计数），通过后及时提交锁定状态。
