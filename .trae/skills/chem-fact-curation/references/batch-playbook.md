# Batch Playbook（批次策展详细参考）

## 关键文件位置

| 文件 | 说明 |
|---|---|
| `data/highschool/general-choice-facts.json` | 事实库（Concept 单选）。顶层键 `version/source/statements`；每条 `{"id":"GC-XXX-Y","match":[...],"stemMatch":[...],"verdict":bool,"reason":"..."}`，4 空格缩进单行 |
| `tests/CogniThink.TutorBrain.Tests/Batch3ChoiceValidatorTests.cs` | `ConceptTruth` 字典（每题预期答案）；`Concept_Coverage` 断言 `solved == ConceptTruth.Count`（动态）；`Concept_NoOverlap` 硬门 |
| `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` | 题库原文，每题有 `question/options/answer/note/images`；`note` 常含"千问求解"，**不可靠** |
| `src/CogniThink.TutorBrain.Api/data/pdf_images/<file>.png` | 题图，Read 工具可直接读；**路径以题库 JSON 的 `images` 字段为准**（`vl_cache3` 旧目录已不存在） |
| `src/CogniThink.TutorBrain.EvalRunner` | eval 工具，输出 `_evalscratch/snapshot_<tag>.json` + `checkpoint_<tag>.done` |
| `src/CogniThink.TutorBrain.Api/Application/Gateway/` | 路由：`LlmGatewayService.cs`、`LlmGatewayService.Curve.cs`、`ConceptChoiceValidator.cs`（加载 general-choice-facts，静态无状态包装 `FactChoiceMatcher.TryDecide`） |
| `_evalscratch/` | 辅助脚本与快照 |

## 快照结构（必读）

快照**不是** `results` 列表，而是 6 个键：

- solved（已解）：`bothCorrectCases` / `gatewayOnlyCorrectCases` / `pureLlmOnlyCorrectCases`
- decided（已判定，含判错）：上述 3 个 + `bothWrongCases` / `gatewayOnlyWrongCases` / `pureLlmOnlyWrongCases`

`decided=True 且 solved=False` = Concept 返回 null（LLM 判错，0 harm）→ 可安全加事实。生成候选池时只取这种。

## C# 归一化（`ClassificationValidator.Norm`，探针必须字节级复刻）

去空白 → 下标 ₀-₉(U+2080-2089)→0-9 → 上标 ⁰(U+2070)→0、¹(U+00B9)→1、²(U+00B2)→2、³(U+00B3)→3、⁴-⁹(U+2074-2079)→4-9、⁺(U+207A)→+、⁻(U+207B)→- → 全角 U+FF01-FF5E→ASCII(减 0xFEE0) → U+3000→去。**不做 NFKC**。`NormOpt` 额外把 `—`/`–`/`−`→`-`。

⚠️ 写 `match` 关键词前必须用 dbg 转义（`\uXXXX(char)`）查看原始码点。`℃`/`Ⅰ`/`Ⅱ`/`Ⅲ`/`×`/`·`/`＝` 等 NFKC 后看起来一样，但 C# 不转 → 失配。`scripts/probe_template.py` 的 `norm()` 已与 C# 字节级对齐，新探针必须复制此版本，禁止自行重写。

## WRONG 正则（探针必须与 C# 两处完全一致）

```
错误|不正确|不相符|不符|不具有对应|不能达到|无法达到|不能实现|不能用来|不能用|不属于|不合理|不可行|不能说明|不能证明|不能用于|没有关联
```

注意：**`不能完成` 不在此列表** → 题干用"不能完成"的题（如 pdf-406）ask 被误判为 correct，无法用事实唯一选出 → 跳过，不动正则。

## 匹配器语义与测试门

- `FactChoiceMatcher.TryDecide`：每个选项必须**恰好命中 1 条**事实（`matched.Count != 1` → 整题 null）；每条要求 `match` 全在选项 **且** `stemMatch` 全在题干；命中冲突→null；0 命中→null。
- `OptContains`：以数字开头的关键词要求前边界非数字（C# `char.IsDigit`）。
- `ConceptChoiceValidator` 只加载 `general-choice-facts.json` 单库。
- **硬门 `Concept_NoOverlap_WithExperimentOrganicAndStructure`**：凡 Concept 非 null 的题，断言 Experiment/Organic/Structure 全为 null。探针 stray 检查只扫 Concept 库，**不能替代此门** —— 落库后必须跑 Batch3 全绿确认。
- `screen_overlap.py <qids...>` 是预检（非充分条件）：检查候选题 stem 是否命中 experiment/organic/structure 事实库的 stemMatch。

## 选题域经验

- **安全域**（不与 Organic/Experiment/Structure 重叠）：电化学、热化学、动力学、沉淀/溶解平衡、酸碱、元素推断/周期律、气体定律、化工流程、价-类二维图、NA 阿伏加德罗 → 干净。
- **高危重叠域**：有机化合物（→Organic）；晶体结构图（→Structure）；具体实验装置/操作（→Experiment）。
- 路由修复后，choice+曲线题也可纳入策展（在 `CallEngineAsync` 的 `TryRouteGraph` 之前已插入 `ConceptChoiceValidator.TryDecide` 守卫，批次 24 前完成）。
- 化学用语题选项常依赖图示内容，需逐图核验。
- 元素推断题的 note 常来自"千问求解"，**不可靠**，必须从结构图 + stem 约束独立推出元素归属再算 4 个 verdict，再核对答案键；note 仅作弱提示。
- 化学用语题 stem 特征易撞车（多道 stem 都含"化学用语表述错误的是"），必须用年份/卷别（如 `"2023"`/`"甘肃卷"`）加入 stemMatch 区分。

## 不可解清单（避免重复尝试）

- **答案键冲突**：pdf-087、pdf-295（化学 C 假但键标 A）、pdf-306、pdf-284（酸碱分布曲线，4 选项均真但键标 C）、pdf-325（甲苯质谱 m/z=92 是分子离子峰不是 CH₃，键标 B 正确项矛盾）、pdf-410（蛋白质 N 测定，化学 D 假但键标 C）、pdf-511（HSO₃F 结构下仅 B 真但键标 D）、pdf-432（CH₄ 球棍模型图正确，化学上 A 才错但键标 B）、pdf-075（批次28新发现：Xe 氟化物，A/C 两项均假与键标 B 冲突）。
- **多选/畸形**：pdf-303（不定项）、pdf-115（ans=AC）、pdf-090（ans="1,3,1,3"）、pdf-176/178（填空空选项）、pdf-263（计数题）、pdf-319/337/376/382/394/405（有机/畸形，文本缺图）。
- **歧义**：pdf-037（A/D 均真）、pdf-418（A/C 均导致结果偏高，非唯一）、pdf-498（元素归属不唯一）。
- **选项文本缺失/截断**：pdf-307（C/D 缺有机物结构图）、pdf-336（选项 A 缺产物结构图）、pdf-416（子题无上下文）、pdf-540（子题无上下文）。
- **数据不足**：pdf-289（C 需 Ksp(CaF₂) 题干未给）。
- **正则未覆盖**：pdf-406（"不能完成"不在 WRONG 正则）。
- **图像错配**：pdf-138（p046_n05_1 是化合价图非 H₂S 装置）→ 跳过。
- **有机重叠**：pdf-388（OF-385 stemMatch `所有原子/共平面` 命中）→ 跳过。
- **已解应跳过**：每批新解后从候选池剔除（如 pdf-202/431/433/232/021/141/140/014/139 等）。

## 命令速查

- **Python**：`C:\Users\Administrator\.local\bin\python3.14.exe`（不在 PATH）。永远写脚本到 `_evalscratch/*.py` 再运行，禁止内联 `python -c`（PowerShell 引号乱码）。
- **stale-DLL 仪式**：`Get-Process testhost,dotnet -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 2; dotnet build-server shutdown 2>&1 | Out-Null; Start-Sleep 2`
- **Build**：`dotnet build tests\CogniThink.TutorBrain.Tests\CogniThink.TutorBrain.Tests.csproj -c Debug --nologo -v minimal`
- **Batch3**：`dotnet test ... --filter "FullyQualifiedName~Batch3ChoiceValidatorTests" --no-build -c Debug --nologo -v minimal`
- **Chem**：先 build `tests\CogniThink.Chem.Tests\...` 再 test（期望 905/0，不变）
- **全量 TutorBrain**：去掉 `--filter`（注意：当前有 2 个红测来自先前会话未提交的判分改动 `GatewayVsLlmEvaluationService.cs`，与事实无关，Batch3 和 Chem 全绿即可）
- **Eval（后台）**：`dotnet run --project src\CogniThink.TutorBrain.EvalRunner -c Debug --nologo -- --tag <tag> --concurrency 12` → 约 1-2 分钟，完成自动通知
- **cmp**：`& "C:\Users\Administrator\.local\bin\python3.14.exe" _evalscratch\cmp.py <tagA> <tagB>`
- **JSON 验证**：`& "C:\Users\Administrator\.local\bin\python3.14.exe" -c "import json; d=json.load(open(r'data/highschool/general-choice-facts.json',encoding='utf-8')); print('JSON OK',len(d['statements']),d['statements'][-1]['id'])"`
- PowerShell 5 不支持 `&&`/`||`，用 `;`；git 加 `--no-pager`。

## 落库格式（事实库）

每条单行、4 空格缩进：

```json
    {"id": "GC-745-A", "match": ["关键词1", "关键词2"], "stemMatch": ["题干特征1", "题干特征2"], "verdict": true, "reason": "..."}
```

- 末条无尾逗号；插入新条目前**必须给原末条补尾逗号**。
- `match` 是选项子串（经 C# norm 后），`stemMatch` 是题干子串（经 C# norm 后）；两者都用 `opt_contains` 检查存在。
- `reason` 写清化学推理，便于日后审计。

## 错误与规避（血的教训）

1. **json.dump 重写 facts → 格式漂移**：一律 Edit 增量追加。
2. **摘要过时导致锚定错误末条/计数**：追加前必须 Read 尾部 + 跑 `check_json_count.py` 确认真实末条与总数。
3. **xref solved 状态不可靠**：选题前用 `unsolved_pool.py` 直接从快照生成候选池（按 6 键正确读取）。
4. **重复事实撞车**：落库前 grep 事实库确认该题 stemMatch 特征无已有事实；探针 TOTAL PROBLEMS 必须=0 才落库。
5. **答案键矛盾题强行写入 → harm**：遇化学推理与答案键冲突一律跳过。
6. **vl_cache 图片错配/路径错**：`vl_cache3` 目录已不存在；图像路径必须以题库 JSON 的 `images` 字段为准。
7. **正则未覆盖 ask 词 → 不可解**：遇"不能完成"等不在 WRONG 正则的题干 → 跳过，不动正则。
8. **探针 NFKC 与 C# Norm 不一致 → 假阳性**：必须复制 `probe_template.py` 的 `norm()`；写 match 前用 dbg 转义看码点。
9. **内联 `python -c` 引号乱码**：一律写 `.py` 文件运行。
10. **原末条无尾逗号**：插入新条目前必须给原末条补尾逗号。
11. **Concept 事实与其他验证器路由重叠 → NoOverlap 测试变红**：落库后必须跑 Batch3 全绿；冲突立即回退该题。
12. **choice+曲线题被 TryRouteCurve 抢占 → Concept 事实不生效**：已修（批次 24 前），守卫在 `CallEngineAsync` 的 `TryRouteGraph` 之前。
13. **全量 TutorBrain 有 2 个红测**（`MixedAnswer_...`）：来自先前会话未提交的判分改动，与本次事实无关。Batch3（含 NoOverlap + Coverage + 全库零误伤）和 Chem 全绿即可，不要误判为本次引入。
14. **"千问求解"note 不可靠**：必须从结构图 + stem 约束独立推出元素归属/化学，再算 verdict，再核对答案键。
15. **化学用语题 stem 特征撞车**：用年份/卷别加入 stemMatch 区分。
16. **读快照结构错误**：快照是 6 个键，不是 `results` 列表；`unsolved_pool.py` 已按此正确读取。
