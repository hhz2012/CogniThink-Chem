# verify-50-triage 独立复核报告（α 桶 100% 证据复核 + β 漏检抽查）

> 复核对象：[_evalscratch/verify/triage_50.tsv](file:///d:/github/CogniThink-Chem/_evalscratch/verify/triage_50.tsv) 与 [triage_report.md](file:///d:/github/CogniThink-Chem/_evalscratch/verify/triage_report.md)
> 依据：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/verify-50-triage/spec.md) 桶定义与 NFR-1（α precision=1）红线
> 复核方式：逐题读 `_evalscratch/verify/<id>_recheck.txt`，独立核对库 question/options/answer/note 四字段是否自洽、是否分属不同题；对 β 抽查题做独立化学真值推理。全程只读，未写回任何题库数据。

---

## 一、复核范围与方法

- **α 桶 100% 复核（7 题）**：pdf-188, pdf-199, pdf-261, pdf-357, pdf-166, pdf-169, pdf-177。逐题读 recheck.txt，独立判断：
  1. 库四字段（question / options / answer / note）是否互相矛盾、是否分属不同题；
  2. 报告 §2 给出的「双证据」是否成立、是否充分；
  3. 给出 CONFIRMED / QUESTIONABLE / WRONG 结论。
- **β 漏检抽查（3 题，均为曾被初步标记为 α 候选的 V4 题）**：pdf-234, pdf-047, pdf-434。独立化学推理验证库 answer 是否真正确，确认 LOST=0。
- **判定口径**：α2 = 题界切分/拼接/题答错配；α3 = fill 题 options 混入别题残留。α precision=1 的红线是「没有一道库正确的题被误判为 α」。

---

## 二、α 桶逐题复核表

| id | 原判定 | 复核结论 | 证据要点（基于 recheck.txt 库内字段） |
|---|---|---|---|
| pdf-188 | α2 | **CONFIRMED** | question 前半为「(3) 基态 Fe²⁺ 的 3d 轨道表示式」填空、后半为「下列化学用语表述正确的是」选择；options = V/聚丙烯/Cl₂/Br 化学用语题选项；但 answer=`sp3`、note 通篇讲 NH₂⁻/H₂O/SO₄²⁻/NH₃BH₃ 的 sp³ 杂化 → **question/options/answer/note 分属至少三题**，题界切分错误确凿。 |
| pdf-166 | α2 | **CONFIRMED** | question 把同一烟道灰工艺题干重复 7 次（每小问前重复题头，小问 (1)–(6)②）；options=null；但 answer=`C`、note 讲 ¹⁴N/¹⁵N 同位素 A/B/C/D 项 → **answer/note（同位素选择）与题干（烟道灰工艺填空）完全错配**，且题面冗余 7 次。题答错配确凿。 |
| pdf-169 | α2 | **CONFIRMED** | question 开头是某题「(3) 加入 NaOH 调 pH=6…滤液中 Al³⁺ 浓度」片段，后拼接「3.(2025·黑吉辽蒙卷)废渣回收 Zn/Pb」完整大题（小问 1–7）；但 answer=`C`、note 讲 V 电子排布/聚丙烯/Cl₂ 电子云/Br 排布（即 pdf-188 的化学用语选择题）→ **三段分属三题**，题界切分错误确凿。 |
| pdf-177 | α2 | **CONFIRMED** | question 开头是某题「(5) 推断 Fe³⁺/Cu²⁺/MnO₂ 氧化性顺序」片段，后拼接「5.(2024·北京卷)黄铜矿生产纯铜…(1) 焙烧前粉碎」另一题；answer=`MnO2 > Fe3+ > Cu2+`、note 匹配开头的 (5) 片段 → **两题拼接**，(5) 片段带答案、北京卷题无答案，题界切分错误确凿。 |
| pdf-199 | α3 | **CONFIRMED（α 成立，但 α3 理由与修法错误，见注 1）** | 库 options A–D 是 NH₃·H₂O 氢键结构式，**正好对应 question 子问 (4)「NH₃·H₂O 的结构式为___（填字母）」**，并非「别题残留」。真实缺陷：answer=`(1)极性 sp³ (2)C` 仅覆盖 (1)(2)，子问 (3)(4) 无答案，且 question 可能溢出了本不该属于本条目的子问 → 属 **α2（题界切分/答案不全）**，非 α3。 |
| pdf-261 | α3 | **CONFIRMED（α 成立，但 α3 理由与修法错误，见注 1）** | 库 options A–D（增大 HCl 流速/升温/增大配比/更高效催化剂）**正好对应 question 子问 (3)「下列措施可提高 M 点 HCl 转化率的是___(填标号)」**，并非别题残留。真实缺陷：question 拼接了两道辽宁卷大题（CH₄/H₂S 曲线题 + HCl 氧化题），answer 覆盖后者、note「含有 He 的曲线为 M」覆盖前者 → **answer 与 note 分属两道题**，属 α2，非 α3。 |
| pdf-357 | α3 | **CONFIRMED（α 成立，但 α3 理由与修法错误，见注 1）** | 库 options A–D（官能团/手性碳/杂化/反应类型）**正好对应 question 后半「(2024·贵州卷) 灵芝酸 B…下列说法错误的是( )」**，并非别题残留。真实缺陷：question 拼接了「全国专题训练酯化填空」与「贵州卷灵芝酸 B 选择」两道题，answer/note 只覆盖前者、后者（含 options）缺答案 → 属 **α2（题界拼接 + 答案不全）**，非 α3。 |

> **注 1（α3 三节的共同问题）**：报告 §2 α3 表对 pdf-199/261/357 的「证据 1（库内结构矛盾）」写的是「options 混入别题错位残留 / 仅 options 脏 / 应清空 options」。经逐题核对，**三题的 options 都正确对应本条目 question 中的某个带标号子问，不是别题残留**。因此：
> - α 成立（三条目确有库真缺陷：题界拼接 / 答案不全 / answer-note 错配），**precision=1 未被破坏**——没有库正确的题进入 α；
> - 但 **α3 子标签与「清空 options」修法是事实错误**：若按报告修法清空 options，将**删除正确数据**，违反 harm=0。必须在 implement 前修正。

---

## 三、β 漏检抽查表（LOST 检查）

| id | 原判定 | 库 answer 是否经独立化学验证正确 | 漏检风险 |
|---|---|---|---|
| pdf-234 | β | **正确**。独立推理：W⁺→{H,Li,Na}、Y⁻→{H,F,Cl}；W+X=Y⁻ 电子数；[ZY₆]⁻ 中 Z 为 +5 价、Y=F（形成 PF₆⁻）→ W=Li、X=N、Y=F、Z=P（即锂电池电解质 LiPF₆，自洽）。A：N 与 P 同属ⅤA 族 ✓；B：非金属性 F>N>P，故 X>Y 错；C：HF 比 PH₃ 稳定，Z>Y 错；D：半径 Li>N>F，Y>X 错 → 选正确的为 **A**，与库一致。官方 answer=`B` 及 explanation（Al/Fe/H/Cl/O 推断）系别题映射错位。 | **无** |
| pdf-047 | β | **正确**。A：浓硝酸中 Cu₂O 被硝酸氧化为 Cu²⁺、硝酸还原为 NO₂，所给歧化式错误；B：热碱中 S 歧化 `3S+6OH⁻≜2S²⁻+SO₃²⁻+3H₂O` 正确；C：醋酸铵为弱酸弱碱盐，应写 CH₃COO⁻+NH₄⁺ 双水解，仅写 NH₄⁺ 水解错误；D：左侧电荷 +4、右侧 0，不守恒错误 → 选正确的为 **B**，与库一致。官方 explanation（铁腐蚀/析氢/发蓝）系别题映射错位。 | **无** |
| pdf-434 | β | **正确**。2024 浙江 6 月选考第 7 题真题，题干问「不正确的是」，真题答案为 **B**（Cl₂ 共价键电子云图画错）；库 answer=`B` 与官方 answer 字段一致。explanation 自相矛盾（称「A 错误」却给出正确电子式 `:Ö::C::Ö:`，又称「B 正确」）不可靠，verify 据此与 askDir=WRONG 矛盾而误报。B/C/D 为图，文本无法核但与官方一致。 | **无** |

> 三题曾是初步 α 候选（库 answer 疑似错误），深核后均确认库 answer 化学正确、官方 explanation 映射错位或自相矛盾。**LOST=0**，β 判定成立，无真 α 漏判。

---

## 四、总体结论

**结论：FAIL（需回到 implement 修正）**

两条红线本身均成立：
- ✅ **α precision=1**：7 道 α 题全部是真实库缺陷（题界拼接 / 题答错配 / 答案不全），**没有一道库正确的题被误判为 α**。
- ✅ **LOST=0**：抽查的 3 道 β（pdf-234/047/434）库 answer 经独立化学真值验证全部正确，无真 α 漏判。

但报告存在**必须修正后方可 implement** 的问题，否则会触发 harm（删除正确数据）：

### 必须修正项（物化待办）

1. **[α3 子标签错误] pdf-199、pdf-261、pdf-357 的子标签 α3 → α2**
   - 理由更正：从「options 混入别题残留」改为「**题界切分错误**（条目溢出了额外子问 / 拼接了异题）+ **答案不全或 answer 与 note 错配**」。
   - 三题 options 均正确对应本子问，**不得**再以「options 脏」为由处理。

2. **[修法有害] 删除报告中「清空 options」的建议**
   - 对 α2 类缺陷，正确修法是「原页重切题界，使各子问与 answer/note 一一对应；options 若确属本子问则**保留**；若整段为异题溢出则随该子问一并移出本条目不清空」。
   - 按现报告「清空 options」执行将删除正确选项数据，违反 harm=0。

3. **[证据列失实] 重写 triage_report.md §2 的 α3 表**
   - 「证据 1（库内结构矛盾）」列不得再写「options 脏 / 别题残留」；应改写为真实矛盾（如：子问数 > answer 覆盖数、answer 与 note 分属不同题、question 拼接异来源异主题段落等）。
   - 同步更新 triage_50.tsv 中这三行的「主类 / 判定理由」字段（α3→α2，理由更正）。

4. **[证据加固，可选但建议] pdf-199 补 OCR 原页**
   - pdf-199 因四个子问同属「分子结构/氢键」主题，外观最像合法题组，是 7 题中 α 证据最弱的一题。建议渲染 exercise p71（扫描页）OCR 确认子问 (3)(4) 是否为本条目溢出，以巩固 α2 证据；在 OCR 前至少应明确标注其缺陷为「answer 仅覆盖 (1)(2)、(3)(4) 缺答」而非「options 脏」。

5. **[一致性] 统一 β 与 α 的判定口径**
   - 报告对 β V3 题（如 pdf-190/490）以「汇编小题多来源、answer 与子问对应」判库正确；对 α3 三题则未应用同一「answer 项数是否与子问数对应」标尺。建议在报告方法论中明确：子问数 > answer 覆盖数即构成缺陷，避免 β/α 边界漂移。

> 修正以上 5 项后，α 桶 precision=1 与 LOST=0 的结论可正式签收为 PASS；在修正前，报告不得直接交付 implement（尤其不得执行「清空 options」）。
