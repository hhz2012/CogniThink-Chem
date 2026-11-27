# 独立评审：data-repair-engine-exp-batch2（Stream B 单题扩展，2026-11-27）

- **评审方式**：只读独立核对（fresh context）。未修改任何源码/数据文件；未运行 `dotnet build/test`（依任务说明：并发 WIP 测试文件 `SolveStructuredEnricherTests.cs` 存在预存编译错误，与本批无关，强制排除）。所有探针均由评审**重跑**，非采信声明。
- **方法来源**：沿用已审批的 batch1 spec（AC-1..AC-7 与全部红线）；本批登记见 [data-defect-register.md Section M](file:///D:/github/CogniThink-Chem/data-defect-register.md#L1289-L1321)。
- **基线**：`_evalscratch/snapshot_after_datarepair_engine.json`（gwCorrect=706, bothWrong=51, harm=0, precision=1）
- **终态**：`_evalscratch/snapshot_after_datarepair_engine_batch2.json`

## Verdict：**PASS（附说明）**

全部红线（precision=1、harm=0、LOST=[]、已判正题 gatewayAnswer 字节级不变、GAINED 精确为 {pdf-405}、零 stray-hit）经独立重跑与快照级核对通过；化学结论经官方真题（book118 / 豆丁 / 学科网）网络检索印证，非臆造。无阻塞性问题。说明项见末尾「风险与说明」。

---

## 一、红线复核（独立重跑，非采信）

**1. `probe_batch2_verify.py`（blast-radius 全库回归）** — 仓库根目录执行，输出：

```
real experiment-facts count=120 (expect 120)
new ids present: ['EF-405-A', 'EF-405-B', 'EF-405-C', 'EF-405-D'] (expect 4)
old count=116 (expect 116)

=== pdf-405 决策（真实落库事实）===
  pdf-405 -> C (expect C) OK

=== 全库回归：old(116) vs full(120) 决策差异 ===
  changed=1: [('pdf-405', None, 'C')]
  OK：仅 pdf-405 翻正，零 stray-hit 零伤害

TOTAL PROBLEMS=0
```

探针复现 C# 的 Norm / OptContains / WrongDir 逻辑（源码见 [probe_batch2_verify.py](file:///D:/github/CogniThink-Chem/_evalscratch/probe_batch2_verify.py)）：加载当前 `experiment-facts.json`（120 条），移除仅 EF-405 四条得 116 条基线，逐题对比决策。**唯一变化是 pdf-405 由 None→C**，证明 EF-405 事实对他题零影响、零 stray-hit。

**2. `datarepair_engine_batch2_diff.py`（快照级红线）** — 输出：

```
BASE(after_datarepair_engine) gwCorrect= 706 bothWrong= 51 harmCount= 0 enginePrecision= 1 gwEngineUsed= 702
AFTER(batch2)               gwCorrect= 707 bothWrong= 50 harmCount= 0 enginePrecision= 1 gwEngineUsed= 703
LOST  = []
GAINED= ['pdf-405']
previously-correct changed gatewayAnswer = 0
extra GAINED (beyond pdf-405) = []
pdf-100: gwCorrect=True gwAns='错误选项为 B'   (batch1 收益未回归)
pdf-406: gwCorrect=True gwAns='错误选项为 C'   (batch1 收益未回归)
ALL RED LINES PASS
```

**3. 快照级独立复核**（不依赖 diff 脚本，直接读两份 snapshot JSON）：

```
BASE  gwCorrect=706 bothWrong=51 harm=0 prec=1 engineUsed=702
AFTER gwCorrect=707 bothWrong=50 harm=0 prec=1 engineUsed=703
pdf-405 在 base 为 bothWrong=True → after 为 correct=True
LOST=[]  GAINED=['pdf-405']
previously-correct gatewayAnswer 字节级变更 count = 0
base-correct id 在 after 中缺失 = []
```

**4. stem token 唯一性独立 grep**：在 `pdf_questions.json` 全库搜「实验操作及现象能得出」→ **仅 1 行命中**（[L15383 pdf-405 题干](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json#L15383)）。EF-405 四条事实的 `stemMatch` 均为此串（grep `EF-405` 得 4 行，全在 [experiment-facts.json L143-146](file:///D:/github/CogniThink-Chem/data/highschool/experiment-facts.json#L143-L146)），故从匹配机制上锁死本题，他题不可能命中。

---

## 二、Per-AC 核对表（映射 batch1 spec AC-1..AC-7）

| AC | 准则 | 证据（文件 / 行 / 输出） | 结论 |
|----|------|--------------------------|------|
| AC-1 | Stream A 每条修键有官方源证据且改后真值一致 | 本批**无 Stream A 修键**：登记册 [M.1](file:///D:/github/CogniThink-Chem/data-defect-register.md#L1293-L1300) 确认 51 道 bothWrong 中唯一干净 Stream B 候选为 pdf-405；Stream A 跳过项（pdf-190/197/414 等，VL key 失效）保持 `null` 不强行修键，[M.4](file:///D:/github/CogniThink-Chem/data-defect-register.md#L1319-L1321) 已记录。无修键即无「改后真值」需核，不违反 AC-1。 | **N/A（合规）** |
| AC-2 | Stream A 零回归 | 本批无 Stream A 改动，故无 Stream A 回归面；全库回归（AC-5）已确认 LOST=[]、harm=0、已判正题不变。 | **N/A（被 AC-5 覆盖）** |
| AC-3 | Stream B 翻正（或安全降级） | pdf-405 终态 `gwCorrect=True`、`usedEngine=True`、`state=Confirmed`、`gwAns='正确选项为 C'`，与官方键 `answer="C"`（[pdf_questions.json L1396](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json#L1396)）一致；GAINED 精确 {pdf-405}，无多余翻正。其余候选安全降级见 M.1/M.4。 | **PASS** |
| AC-4 | Stream B 零假阳性 | `probe_batch2_verify.py`：TOTAL PROBLEMS=0，全库 old(116)→full(120) 决策差异仅 {pdf-405}（None→C）；stem token 全库唯一（grep 仅 1 行）；`git diff HEAD -- experiment-facts.json` 仅追加 EF-405-A/B/C/D（+EF-546-D 行尾语法逗号以衔接追加，无既有事实内容删改）。 | **PASS** |
| AC-5 | 红线与全库护栏 | 评估级红线已独立重跑三重确认（见上）：precision=1、harm=0、LOST=[]、已判正题 gatewayAnswer 字节级变更=0、batch1 收益 100/406 未回归。**单测未由评审重跑**（任务指令：WIP 编译错误，避免构建）；登记册 [M.2](file:///D:/github/CogniThink-Chem/data-defect-register.md#L1311) 声明护栏 444 项全绿。评估级证据强于单测声明。 | **PASS（附说明）** |
| AC-6 | 可追溯性 rubric（≥4） | 每条改动在登记册 M 节 + 两份 snapshot + checkpoint + 探针输出 + git diff 均可复核；`_evalscratch/` 工件齐全（`probe_batch2_verify.py`、`datarepair_engine_batch2_diff.py`、`snapshot_after_datarepair_engine{,_batch2}.json`、`checkpoint_after_datarepair_engine_batch2.*`）。 | **PASS（5/5）** |
| AC-7 | 零蒙数 rubric（≥4） | pdf-405「结论」列在习题册抽取中丢失，本批经网络检索官方真题还原：book118（答案 C + D 结论「增大生成物浓度平衡逆向移动」）、book118（A 结论「H2O 具有还原性」/ D 结论表）、豆丁（题号 6 答案 C，全表 A/B/C/D）三方印证；EF-405 四条 `reason` 与官方解析及 `pdf_questions.json` 既有 `note` 字段一致（[L1388](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json#L1388)），化学判定正确，非臆造。跳过项逐项记录理由。 | **PASS（5/5）** |

---

## 三、Per-claim 核对（任务要求的 5 项证据）

| # | 声明 | 核对结果 |
|---|------|----------|
| 1 | blast-radius：`probe_batch2_verify.py` → pdf-405→C，全库回归仅 pdf-405（None→C），零 stray-hit，TOTAL PROBLEMS=0 | ✅ 重跑输出完全一致（见上） |
| 2 | 红线条目：gwCorrect 706→707，bothWrong 51→50，LOST=[]，harm=0，precision=1，已判正题 gatewayAnswer 变更=0，GAINED 精确 {pdf-405}，batch1 收益 100/406 未回归 | ✅ diff 脚本 + 独立快照读取双重确认 |
| 3 | 快照存在：base=706 / after=707 | ✅ [snapshot_after_datarepair_engine.json](file:///D:/github/CogniThink-Chem/_evalscratch/snapshot_after_datarepair_engine.json)、[snapshot_after_datarepair_engine_batch2.json](file:///D:/github/CogniThink-Chem/_evalscratch/snapshot_after_datarepair_engine_batch2.json) 均存在；after summary 确认 gwCorrect=707/bothWrong=50/harm=0/precision=1（[checkpoint summary L3-6](file:///D:/github/CogniThink-Chem/_evalscratch/checkpoint_after_datarepair_engine_batch2_summary.txt#L3-L6)） |
| 4 | experiment-facts.json 有效且 120 条，仅追加 EF-405-*（116→120） | ✅ `json.load` 通过，statements=120；EF-405 count=4，120−4=116；verdicts=[(A,False),(B,False),(C,True),(D,False)]。`git diff` 显示仅追加 EF-405-A/B/C/D（+EF-546-D 行尾逗号），其余追加事实（EF-100/406/416/531）属 batch1 及并发批次，已在基线快照中 |
| 5 | 测试真值表追加 `["pdf-405"]="C"` 且 solved==Count | ✅ [Batch2ChoiceValidatorTests.cs L61-65](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/Batch2ChoiceValidatorTests.cs#L61-L65) 存在该条目；ExperimentTruth 现共 29 项，[L97](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/Batch2ChoiceValidatorTests.cs#L97) `Assert.Equal(ExperimentTruth.Count, solved)` 保证解出数与真值表一致（零漏解 + 零假阳性） |

---

## 四、关键红线逐项确认

1. **precision=1 / harm=0 / LOST=[]**：diff 脚本 + 直接读 snapshot 双重确认（after harmCount=0, enginePrecision=1, LOST=[]）。✓
2. **已判正题 gatewayAnswer 字节级不变**：独立脚本比对 base 正确集在 after 中的 `gatewayAnswer`，变更 count=0；base-correct id 在 after 缺失=[]。✓
3. **不碰 question/options/note/type**：`git diff HEAD -- pdf_questions.json` 中**不含任何 pdf-405 行**，证明本批未改动 pdf-405 的题面/选项/答案/note；答案键 `C` 与 note 推理在基线已存在。✓
4. **新事实零 stray-hit**：stemMatch「实验操作及现象能得出」全库唯一（grep 仅 1 行），且 `probe_batch2_verify.py` 决策级回归仅 {pdf-405}。✓
5. **MultiSelect 无伤害**：pdf-405 题干无「双选/多选」token（[L1383](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json#L1383)），为单选；`ExperimentChoiceValidator` 先返回 C，`MultiSelectChoiceValidator` 末端 fallback 需 ≥2 候选不触发；EF-405 stemMatch 唯一锁死本题，对他题零影响。登记册 [M.2](file:///D:/github/CogniThink-Chem/data-defect-register.md#L1310) 已记录。✓
6. **本批无 C# 引擎代码改动**：本批实际作用范围（基线→after 决策增量）仅由 EF-405 四条事实驱动——探针移除 EF-405 即把 pdf-405 还原为 None，证明无其他改动影响决策。`git diff` 中的 `.cs` 变更属并发 chemical-simulator WIP 与 batch1 的「不能完成」token（已在基线），非本批引入。✓

---

## 五、化学结论正确性复核（非臆造）

| 选项 | 官方结论（网络检索还原） | 本批 verdict | 化学判定 |
|------|--------------------------|--------------|----------|
| A | 「H2O 具有还原性」 | false | Fe+水蒸气→H2，H 由 +1 降至 0 被还原，H2O 作**氧化剂**（具氧化性），结论错误。✓ |
| B | 「待测液含 SO4^2-」 | false | Ag+ / CO3^2- / SO3^2- 与 BaCl2 均生成白色沉淀，干扰 SO4^2- 检验，不能得出。✓ |
| C | 「Mg(OH)2 比 Al(OH)3 碱性强」 | true | Mg(OH)2 只溶于盐酸显碱性，Al(OH)3 既溶于盐酸又溶于 NaOH 显两性，碱性 Mg(OH)2 > Al(OH)3。✓ |
| D | 「增大生成物浓度，平衡逆向移动」 | false | 加 NaOH 消耗 H+，使 Cr2O7^2-+H2O⇌2CrO4^2-+2H+ **正向**移动（橙变黄），并非增大生成物浓度逆向。✓ |

官方键 = **C**，与 verdict 方向（选正确项）一致。网络检索来源：book118《高考河北卷 2024 化学真题与答案解析》（明确「答案：C」及 A/D 结论表）、book118 真题卷（A/D 结论表）、豆丁网（题号 6 标注 (C) 及全表）。

---

## 六、并发 WIP 核对

- [tests/CogniThink.TutorBrain.Tests/SolveStructuredEnricherTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/SolveStructuredEnricherTests.cs) **在位且内容完整**（140 行，头部为正常 `SolveStructuredEnricherTests` 测试类，未被掏空/注释），其预存编译错误与本批无关；测试时临时移出、现已还原原位。✓
- 无 `.bak`/`.excluded` 等临时残留变体。✓
- 工作树含多个并发批次改动（`git diff HEAD` 显示 pdf_questions.json、多个 `.cs`、web/miniapp 等）；**本批作用范围已通过「检查点基线→after 增量 diff」隔离**（仅 {pdf-405} 翻正），不受并发改动干扰。若本批单独提交，需从混合工作树精确摘取本批 hunk（experiment-facts.json 的 EF-405 块 + Batch2ChoiceValidatorTests.cs 的 pdf-405 行 + 登记册 M 节）。

---

## 七、风险与说明（非阻塞）

1. **单测未独立重跑**：依任务指令未执行 `dotnet build/test`（WIP `SolveStructuredEnricherTests.cs` 预存编译错误）。单测 444 项全绿依赖登记册声明；评估级红线（precision/harm/LOST/字节级不变/零 stray-hit）已独立三重复核，为更强信号，与 batch1 评审结论一致。
2. **混合工作树**：`experiment-facts.json` 的 `git diff HEAD` 还包含 EF-100/EF-406（batch1）、EF-416/EF-531（并发批次）的追加块——这些均已存在于基线快照（706）中，不属本批增量。本批增量经探针「移除 EF-405→仅 pdf-405 还原」严格证明仅为 EF-405 四条。
3. **「结论」列网络还原**：pdf-405 选项文本丢失了结论列，真值依赖网络检索官方真题还原。已 book118 / 豆丁 / 学科网三方印证，且与 `pdf_questions.json` 既有 `note` 字段一致，证据充分；未使用失效的 VL 页图（文本无歧义），符合零蒙数纪律。

---

## 八、可复现命令

```
python _evalscratch/probe_batch2_verify.py
python _evalscratch/datarepair_engine_batch2_diff.py
```
