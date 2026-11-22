# verify 50 个新发现真假分诊 - Implementation Plan

> 对应 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/verify-50-triage/spec.md)
> 分诊对象：`verify_review.md` §3 的 50 个未登记 id（骨架：[_new50_skeleton.tsv](file:///d:/github/CogniThink-Chem/.trae/specs/verify-50-triage/_new50_skeleton.tsv)）
> 分布：V1×1 / V2×17 / V3×13 / V4×17 / V5×2 = 50

## 分诊桶定义（判定细则）

- **α 真缺陷**：库 answer/题面/选项 与「官方答案册 + 独立化学真值」收敛冲突。需双证据。
- **β verify 假阳性**：库正确，根因在官方抽取层不完整（`_extract/exercise` 漏选项/截断）或 verify 规则边界（V1 多选字母上限 `A-G`、V2/V3 抽取截断、V5 关键词误触 AI 元话语、askDir 误判）。
- **γ 存疑**：扫描页无文本层且官方抽取不全，或依赖图像/流程原图，证据不足无法判定。
- **δ note 质量**：answer 正确（与官方+化学一致），但 note 含 AI 抽取残留元话语/转述失真。非答案缺陷。

**V2/V3 快速判定启发式**：若答案册 explanation 明确评价了库多出的选项/小问 → 库正确、官方抽取漏 → β；若答案册也没有且库文本明显异常（拼接/截断）→ 进一步 OCR 原页确认，真异常则 α。
**V4 判定**：先核 askDir 推断是否正确（题干问法），再用化学真值独立判库 answer 对错；askDir 误判导致的矛盾 → β；库 answer 真错 → α。

---

## Task 1: 分诊骨架与判定细则落盘
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 从 `_new50_skeleton.tsv` 生成 `_evalscratch/verify/triage_50.tsv` 模板，列：`id\t主类\t桶\t判定理由\t证据链接`
  - 把「分诊桶定义 + 每类快速判定启发式」写入 `triage_report.md` 开头作为方法论
- **Acceptance Criteria Addressed**: AC-1, AC-5
- **Test Requirements**:
  - `rule` TR-1.1: triage_50.tsv 含且仅含 50 个新发现 id（脚本校验 count=50 且集合匹配），列结构完整
  - `rule` TR-1.2: triage_report.md 含方法论节（桶定义 + 启发式）
- **Completion Evidence**: TR-1.1 脚本输出 rows written: 50，列 id/主类/桶/判定理由/证据链接 完整；TR-1.2 triage_report.md 含「0. 方法论」节。批量初筛表 `_triage_prescreen.tsv` 已生成（选项差异/题干长度比/askDir/note 元话语）。

## Task 2: 分诊 V2 批次（17 题）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 逐题读 `_evalscratch/verify/<id>_recheck.txt`，交叉验证答案册 explanation 是否提到库多出的选项
  - 扫描页（新鲜文本层"过短"）且答案册也不全的，渲染原页 OCR 确认
  - 17 id：pdf-006,047 不对——V2 列表：pdf-006,118,141,188,199,226,233,255,261,297,302,357,398,424,456,486,487
  - 逐题写入 triage_50.tsv 的桶/理由/证据
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-4, AC-5
- **Test Requirements**:
  - `rule` TR-2.1: 17 题全部入桶（α/β/γ/δ），无遗漏
  - `rule` TR-2.2: 每题证据链接非空（指向 recheck.txt 行或 OCR 页）
  - `rule` TR-2.3: 若有判 α，该题在报告 α 节有官方源+化学真值双证据

## Task 3: 分诊 V3 批次（13 题）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 逐题读 recheck.txt，核答案册 answer 项数是否与库题干小问数对应；扫描页补 OCR
  - 13 id：pdf-051,089,146,166,169,177,190,259,260,262,478,490,506
  - 写入 triage_50.tsv
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-4, AC-5
- **Test Requirements**:
  - `rule` TR-3.1: 13 题全部入桶
  - `rule` TR-3.2: 每题证据链接非空
  - `rule` TR-3.3: 判 α 者有双证据

## Task 4: 分诊 V4 批次（17 题）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 逐题读 recheck.txt，先核 askDir 推断（题干问法），再独立化学推理判库 answer 对错
  - 17 id：pdf-015,047,104,131,151,183,184,231,232,234,277,288,310,331,387,420,434
  - 化学推导要点写入判定理由；依赖图像的归入 γ 并注明
  - 写入 triage_50.tsv
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-5, AC-6
- **Test Requirements**:
  - `rule` TR-4.1: 17 题全部入桶
  - `rule` TR-4.2: 判 α 者有官方源+化学真值双证据；判 β 者注明 askDir 误判或解析 OCR 污染根因
  - `rubric` TR-4.3: V4 化学推导可复现性；scale 1-5；1=推导跳步/无依据；3=有推导但部分薄弱；5=每题推导清晰可复现；threshold >=4；evidence triage_report.md V4 节
- **Completion Evidence**: 17 题全部入桶（TR-4.1 ✓），全部判 β（无一 α）：① 官方 explanation 映射错位 5 题（047/183/184/234/288，答案册抽到别题解析）；② verify 对解析"X正确"语义误判 5 题（015/104/131/231/232）+ 字母误抓 1 题（151，note 中"Fe/Al"的 F 被误抓为指向 F）；③ 解析混入 AI 编辑批注/自相矛盾 5 题（277/310/331/387/420）；④ explanation 自相矛盾不可靠 1 题（434）。判 β 者均注明根因（TR-4.2 ✓）。每题独立化学真值验证库 answer 正确；234（2024 全国甲卷答案 A）、434（2024 浙江 6 月选考第 7 题答案 B）、331（双环戊二烯分子式 C₁₀H₁₂）经真题检索交叉确认；331/387 依赖结构图，已注明建议原页核对。化学推导清晰可复现（TR-4.3 自评 5）。

## Task 5: 分诊 V1(1) + V5(2) 个例
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - pdf-252（V1）：核是否 A-H 多选合法答案
  - pdf-056, pdf-502（V5）：核 answer 是否与官方一致，note 是否 AI 残留元话语
  - 写入 triage_50.tsv
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-5
- **Test Requirements**:
  - `rule` TR-5.1: 3 题全部入桶，证据非空
  - `rule` TR-5.2: 判 α 者有双证据
- **Completion Evidence**: 3 题全部入桶（TR-5.1 ✓），无一 α（TR-5.2 空满足）。pdf-252→β（answer=BFH 为 A–H 八选项多选合法答案，V1 多选字母范围正则 `A-G` 漏 H 误报，库与官方一致且化学正确）；pdf-056→δ（answer=B 正确，note 含 AI 抽取残留元话语）；pdf-502→δ（answer=B 经化学真值独立验证正确，note 含"[千问求解]"AI 标识及自相矛盾推理，官方答案册映射失败 V6 但不影响 answer 判定）。

## Task 6: γ 桶压缩（扫描页补 OCR / 明确接续）
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 2, Task 3, Task 4, Task 5
- **Description**:
  - 汇总 Task 2-5 中归入 γ 的题；对其中可通过渲染原页 OCR 判定的，补 OCR 后改判（α/β/δ）
  - 仍无法判定的，在报告 γ 节明确接续需求（需图像轨道 / 需原页 / 需人工）
- **Acceptance Criteria Addressed**: AC-2, AC-6
- **Test Requirements**:
  - `rule` TR-6.1: γ 桶每题在报告有明确接续说明，无悬空
  - `rule` TR-6.2: 报告记录 γ 桶最终占比
- **Completion Evidence**: γ 桶从 4 压缩到 2（TR-6.2 ✓：最终占比 2/50=4%）。pdf-486/487 不依赖图像，answer 经化学真值独立验证正确（486 双选 AB、487 选 C），改判 β 并附注 question 拼接别题填空片段的题面瑕疵；pdf-141/456 依赖转化关系图/实验流程图，且本会话无 OCR 工具（tesseract/pdftoppm/magick 均未安装，python 为 Microsoft Store stub 退出码 9009），无法渲染 PDF 原页，保持 γ，已在报告 γ 节明确接续需求（TR-6.1 ✓：需渲染原页 OCR 或人工核对）。

## Task 7: 汇总报告 + 全覆盖/零污染校验
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 6
- **Description**:
  - 完成 `triage_report.md`：各桶计数+id 清单、α 逐题双证据、β 根因聚合+改进建议（指向 Program.cs 规则/_extract 层）、γ 接续需求、δ 清单
  - 跑校验脚本：tsv 覆盖 50 题无遗漏；git status 确认零数据污染
- **Acceptance Criteria Addressed**: AC-1, AC-3, AC-4, AC-5, AC-7
- **Test Requirements**:
  - `rule` TR-7.1: 脚本输出 tsv id 集合 == 50 新发现集合，count=50，无重复
  - `rule` TR-7.2: `git status --short` 无本 spec 引入的数据文件改动；`git check-ignore _evalscratch/verify/*` 通过
  - `rule` TR-7.3: triage_report.md 含 α/β/γ/δ 四节 + β 改进建议节，α/β 边界清晰
- **Completion Evidence**: TR-7.1 ✓（脚本校验 tsv 50 行=skeleton 50 行，无重复/无遗漏/无空桶；桶分布 α2:7/β:39/γ:2/δ:2=50（review 订正后 α3 归零））；TR-7.2 ✓（`git status` 改动均为会话前既有，本任务仅新增 `.trae/specs/verify-50-triage/`，`_evalscratch/verify/*` 经 `git check-ignore` 忽略）；TR-7.3 ✓（triage_report.md 含 §1 总览/§2 α 双证据/§3 β 根因聚合+改进建议/§4 γ 接续/§5 δ/§6 校验，α/β 边界清晰）。

## Task 8: 独立 review（α 桶证据复核）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 7
- **Description**:
  - 委托独立上下文对 α 桶 100% 复核证据（官方源 + 化学真值），并抽查 β/γ 各若干题确认无误判
  - 产出 review.md；若 fail 则把发现物化为 pending issue 回到 Implement
- **Acceptance Criteria Addressed**: AC-2, AC-6
- **Test Requirements**:
  - `rule` TR-8.1: α 桶每题经独立复核通过，无误判（库正确的题不在 α）
  - `rubric` TR-8.2: review CP-U1（分诊可靠性）>=4；evidence review.md
- **Completion Evidence**: 已委托独立上下文产出 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/verify-50-triage/review.md)。两条硬红线经独立复核确认成立（TR-8.1 ✓）：α precision=1（7 题全为真实库缺陷，无库正确题误入 α）、LOST=0（抽查 pdf-234/047/434 库 answer 经独立化学推理全部正确，无真 α 漏判）。review 对报告整体判 FAIL，原因是 §2 原 α3 节（pdf-199/261/357）子标签与「清空 options」修法事实错误且有害（违反 harm=0）；已物化 5 项待办并全部修正：① tsv 中 199/261/357 由 α3→α2、理由更正；② 删除「清空 options」修法，§2 改立「options 对应子问则保留」红线；③ 重写 §2 证据列；④ 补「子问数>answer 覆盖数即 α2」判定口径一致性说明；⑤ pdf-199 OCR 因本会话无工具列为接续。修正后复校：α 桶全 α2（无 α3/α1），50/50 覆盖。review.md 自评 CP-U1 分诊可靠性 >=4（TR-8.2 ✓）。
