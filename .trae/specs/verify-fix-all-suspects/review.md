# verify-fix-all-suspects 独立只读复核（fresh context）

> 复核员：独立子代理（只读，未修改任何源/数据/测试/spec 文件，仅产出本 review.md）。
> 复核日期：2026-11-21。
> 红线（冻结）：`precision=1` / `harm=0` / `LOST=0`（不遗漏真缺陷）；`answer` 字段永不当真值；options 正确对应本子问时严禁清空。

---

## ① 总体裁决：**PASS**（AC-5 字面 LOST=2 经裁决为「缺陷暴露」，可接受，理由见 §3.2）

三条冻结红线的真实数据完整性含义全部守住：
- `precision=1`：隔离对照 `ctrl_pre2 → verify_fix_all` 的 **GAINED=0**，本 spec 数据编辑未让任何错题被误判为对（无新增 adopted-wrong）；`checkpoint_verify_fix_all_summary.txt` 实测 `precision=1`。
- `harm=0`：`harmCount=0`；抽查的 4 处「修」diff 均未误删/覆盖正确 options（pdf-357 的正确 options 完整保留，见 §3.3）。
- `LOST=0`（不遗漏真缺陷）：`fix_200.tsv` 200/200 每题有明确判决，0 待分诊；残留嫌疑均已审计为假阳性或 blocked 留痕（见 §3.6）。

---

## ② 六项核对结果与证据

### 1. fix_200.tsv 完整性 ✅ 通过

证据：`_evalscratch/verify/fix_200.tsv`，用 `Get-Content -Encoding UTF8` 按 `\t` 分列统计（列4 = 判定）：

| 指标 | 要求 | 实测 | 结果 |
|------|------|------|------|
| 数据行数 | 200 | **200**（文件共 201 行 = 1 表头 + 200 数据） | ✅ |
| 判决分布 | 修=18 / 不动=177 / blocked=5 | 修=**18** / 不动=**177** / blocked=**5** | ✅ |
| blocked 集合 | {197,355,356,414,456} | **pdf-197, pdf-355, pdf-356, pdf-414, pdf-456** | ✅ |
| 唯一 id 数 | 200 | **200**（重复 id 为空） | ✅ |
| 待分诊 | 0 | 判定列仅出现 {修, 不动, blocked}，无其它值/空值 | ✅ |

判决值无非法项、无遗漏，AC-2 满足。

### 2. eval 红线 + AC-5 LOST=2 裁决 ✅ 通过（LOST=2 判为缺陷暴露）

**checkpoint summary**（`_evalscratch/checkpoint_verify_fix_all_summary.txt`，`Get-Content -Encoding UTF8`）：

```
tag=verify_fix_all total=757 llmAvailable=False
gatewayCorrect=621 gatewayTruthCorrect=621
coverage=0.8203 precision=1
harmCount=0
```

→ `gatewayCorrect=621`、**precision=1**、**harmCount=0**，与终态报告自洽。✅

**两份 diff**（regex 法权威）：

| diff 文件 | GAINED | LOST | LOST 明细 |
|-----------|--------|------|-----------|
| `_evalscratch/verify/diff_pdf174fix_verifyfixall.txt` | 5（pdf-115/388/394/459/475） | 2 | pdf-260, pdf-357 |
| `_evalscratch/verify/diff_ctrlpre2_verifyfixall.txt` | **0** | 2 | pdf-260, pdf-357 |

`ctrl_pre2`（原始数据 + 当前代码）→ `verify_fix_all`（当前数据 + 当前代码）是**隔离对照**：同代码、仅数据差异，**GAINED=0** 直接证明本 spec 的数据编辑**未引入任何 adopted-wrong 或正确数据回归**；唯一 LOST 的 2 题正是本 spec 修正了数据的题。

**AC-5 张力裁决（关键）**：spec 字面要求 diff LOST=0，实际隔离对照 LOST=2。裁决 **可接受**，理由：

1. **pdf-357 = 消除 adopted-wrong（假阳性）**：原库 `answerKeywords[0]="C"`，而题干问「下列说法**错误的是**」；选项 C「分子中碳原子的杂化轨道类型是 sp2 和 sp3」经化学判定为**真**，故 C 不可能是该题答案——原库为错键。原 eval 判 `correct=true` 是因为 eval 真值取自同一错库数据（自洽假阳性）。订正为 B 后，无 LLM 引擎诚实答不出 → `false`。这正是 AC-5 要消灭的 adopted-wrong，**不是正确内容丢失**。化学独立验证见 §3.3。
2. **pdf-260 = 残缺题补全后暴露引擎能力缺口**：原题面**残缺**（缺子问 (3)，被错放 pdf-261），引擎只解了 (2) 的 ΔH=+11 即误判正；补全为含「平衡转化率」的完整多部分大题后被确定性分类器重路由为 `equilibrium`，无 LLM 引擎无力解整题 → `false`。符合 spec Non-Goal NG-3「修复后仍因引擎能力缺口未解的题保持 null，不算数据缺陷未修」，**不是数据回归**。
3. 两题均已登记台账 [J.3](file:///d:/github/CogniThink-Chem/data-defect-register.md)（L294-301），留痕可追溯。
4. `precision=1 / harm=0` 始终守住（summary 实测）。

故 LOST=2 是「修正真缺陷后暴露引擎能力上限」的正确信号，而非数据被改错/弄丢。**AC-5 以「零数据回归」的实质含义判定通过**；字面计数差异已在台账显式披露，不构成 FAIL。

### 3. 抽查 18 处「修」中 4 题（≥3 要求）：git diff + 化学真值交叉验证 ✅ 通过

源数据文件：`src/CogniThink.TutorBrain.Api/data/pdf_questions.json`（因含重复键不可 `ConvertFrom-Json`，采用按 `"id"` 切块后 `git diff --no-index` 逐字段比对，过滤 molblock 长行）。

#### pdf-184（本会话新增修，台账 J.1）
- **diff 触及字段**：仅 `question`（`⁹₄X → ⁶₂X`，1 个上标数字）+ `note` 全文重写；`answer=B`、`options`、`answerKeywords` **均不在 diff 中**（未改动）。
- **未误删正确数据**：options 完整保留，answer B 保留。✅
- **化学真值交叉验证**：OCR 原页 [_evalscratch/ocr/page_061.txt](file:///d:/github/CogniThink-Chem/_evalscratch/ocr/page_061.txt) **L28** 确认教材原题用变量 `$_b^a\text{X}$`（即需解 a,b），库原填 `⁹₄X`（a=9,b=4）违反守恒；由质量/电荷守恒 `¹⁶₈O + ³₂He → ¹³₈O + ⁶₂X` 得 a=6, b=2 → X=⁶₂He，订正 `⁶₂X` 正确。note 四项判定（A 中子数=4≠2 错 / B 同位素对 / C 半衰期短不能示踪错 / D 非化学键不稳定错）与守恒律一致。✅

#### pdf-461（本会话新增修，台账 J.1）
- **diff 触及字段**：`question`（截断题干 → 补全子问 (1)–(4) + LAEM 膜/Ksp 条件）、`answerKeywords`、`note`、`type`（other→fill）、`answer`、`images`（新增流程图）。无 options（流程图填空大题）。
- **化学真值交叉验证**：官方答案册 [pdfs/_extract/answer/page_040.json](file:///d:/github/CogniThink-Chem/pdfs/_extract/answer/page_040.json) **num=5** answer = `(1)2Al2O3(熔融)电解/冰晶石→4Al+3O2↑ (2)Ga³⁺+4Cl⁻=[GaCl4]⁻ (3)铁 [FeCl4]⁻ (4)高 NaCl`，与库订正后的 `answer`/`answerKeywords` **逐字一致**（仅箭头/上下标排版差异），化学正确。✅

#### pdf-522（本会话新增修，台账 J.1）
- **diff 触及字段**：`answerKeywords`、`answer`（占位「题目不完整，无法作答」→「2,4,6-三溴苯酚」）、`note` 重写溯源；`question`、`type` 未动。
- **化学真值交叉验证**：结构图 `pdf_images/p112_n05_4.png` 的 `imageAssets` molblock 显示对称三取代酚（羟基 1 位、Br 2/4/6 位，SMILES `Oc1c(Br)cc(Br)c(Br)c1`，name `2,4,6-tribromo phenol`）；对称三取代只可能是 2,4,6（2,4,5 不对称，已排除 RDKit 键感知伪影）。placeholder→真值，无 options 可伤。✅

#### pdf-357（α2 批次，台账 J.3，AC-6 关键题）
- **diff 触及字段**：`question`（删除首段酯化②前缀，重定位 pdf-356）、`answerKeywords`（C→B）、`note`、`type`（fill→choice）、`answer`（酯化答案→B）；**`options` A–D 不在 diff 中 → 完整保留**（AC-6 合规，harm=0）。✅
- **化学真值交叉验证**：recheck 底稿 [_evalscratch/verify/pdf-357_recheck.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/pdf-357_recheck.txt) **L53-63**（OCR exercise p121）确认贵州卷灵芝酸 B 题的选项 A–D 与库**逐字一致**，证明库 options 本就正确、保留无误；L130-134 显示官方 `exercise`/`answer` 抽取错位到酯化题（原 answer `C5H8O4...` 实为 [page_122.json](file:///d:/github/CogniThink-Chem/pdfs/_extract/answer/page_122.json) num=4 的酯化答案），证实原数据确为错配。化学独立判定：题干问「错误的是」，C「sp2 和 sp3」为真、B「仅含 3 个手性碳原子」为假（灵芝酸 B 为稠环三萜，手性碳远多于 3）→ 正确答案为 **B**，原 kw=C 为错键（adopted-wrong）。订正正确。✅

> 4/4 抽查题均满足「仅改应改字段、未误删正确 options、化学真值经 OCR/答案册/守恒律交叉验证正确」。

### 4. guardrail 测试变更 ✅ 通过（移除 pdf-188 正确）

证据：`git diff HEAD -- tests/CogniThink.TutorBrain.Tests/AtomicStructureSolverTests.cs`：

- `brokenKeys` HashSet：`{ "pdf-166", "pdf-188", "pdf-190" }` → `{ "pdf-166", "pdf-190" }`（pdf-188 移除）。
- 期望 `firedIds` 数组：移除 `"pdf-188"`。
- 注释更新为「pdf-188 原为 fill…α2 题界重切后已归位为 choice…本护栏跳过 choice 不再触发求解器，故移出白名单」。
- 除上述三处外，测试逻辑无其它改动。

**正确性判定**：实测当前库 `pdf-188` 的 `type=choice`（块抽取确认），而护栏 [AtomicStructureSolverTests.cs L185](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/AtomicStructureSolverTests.cs#L185) 有 `if (c.Type == "choice") continue;` → pdf-188 不再触发求解器，故从 `brokenKeys` 与期望 `firedIds` 移除是**正确行为**，非掩盖回归。测试期望集与生产行为一致。✅

### 5. 5 个 blocked 的 Unblock Condition 可执行性 ✅ 通过（AC-7 ≥4）

证据：台账 [data-defect-register.md J.2](file:///d:/github/CogniThink-Chem/data-defect-register.md)（L282-292），与 `fix_200.tsv` blocked 集合 **{197,355,356,414,456} 一一对应**：

| id | Unblock Condition（摘要） | 可执行？ |
|----|---------------------------|----------|
| pdf-197 | 原页恢复正确题干+选项+键（化学复核 A/B/C 假、D 真与「错误的是」单选矛盾，疑多选或错位） | ✅ 具体（指明矛盾点） |
| pdf-355 | 原页拆分为两条题录，核对后半选择题答案键 | ✅ 具体 |
| pdf-356 | 原页拆分 + 图像轨道恢复 X/Y/Z 结构 + 核对后半答案键 | ✅ 具体（含图像恢复要求） |
| pdf-414 | 原页恢复大题上下文并核对该小问键 | ✅ 具体 |
| pdf-456 | 原页/OCR 重切题界，核对正确选项与答案键 | ✅ 具体 |

每条均含「缺陷描述 + 化学证据（矛盾点）+ 可执行接续动作（需哪页原页/哪张图/核对哪个键）」，他人拿到 exercise 原页即可直接接续，AC-7 rubric ≥4 满足。✅

### 6. verify 嫌疑下降 ✅ 通过

- `_evalscratch/verify/verify_findings.tsv`：数据行 **297**，`suspect+error` 唯一 id 数 = **93**（`severity` 列匹配 suspect/error 后去重）。✅
- `_evalscratch/verify/_suspect_baseline.txt`：**99 行**（修复前基线）。✅
- 净下降 6（99→93），与终态报告一致。新增 flag 仅 pdf-461，据台账 J.1/报告 §2 为 **V3 官方 exercise 截断假阳性**（库题干反完整、与 OCR p32/p33 题 5 吻合），无新增 α 类真缺陷。残留 5 个 error 级（162/252/412/414/518）中 414 属 blocked、其余经审计为 V1 格式假阳性，LOST=0（不遗漏真缺陷）。✅

---

## ③ 附加核对（非必查项，佐证红线）

- **AC-8 密钥不入库**：`Program.cs` 新增 `--qwen-ocr` 子命令从 `Environment.GetEnvironmentVariable("QWEN_API_KEY")` 读密钥，鉴权头为 `$"Bearer {apiKey}"`（无字面量）；对全工作树 `git diff HEAD` 做 `Bearer\s+[A-Za-z0-9]{8,}` 正则扫描 → **0 命中**，无 API key 明文进入提交。✅
- **测试全量**：终态报告 `_dotnet_test2.log` 记 0 failed / 2144 passed（本次未重跑，环境约束注明不跑全量；仅核对测试侧变更正确性，见 §2.4）。

---

## ④ 结论

spec「verify-fix-all-suspects」实施**通过独立复核，裁决 PASS**。200 题零遗漏、18 处修精准（抽查 4 题化学真值全部交叉验证正确、无误伤 options）、5 个 blocked 留痕可接续、verify 嫌疑 99→93 无新增真缺陷、precision=1/harm=0 守住。唯一与 AC-5 字面的差异（隔离对照 LOST=2）经裁决为 pdf-357（消除 adopted-wrong）与 pdf-260（补全残缺题暴露引擎能力缺口）的**缺陷暴露，非数据回归**，与 spec Non-Goal NG-3 一致并已登记台账 J.3，可接受。无 FAIL 项，无可执行 finding。
