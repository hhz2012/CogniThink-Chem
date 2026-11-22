# P3-2 规则/事实体系归一 — 独立审查报告（fresh-context / 只读）

> 审查员角色：fresh-context 只读代码审查员（独立于实施者）。除运行幂等生成脚本所重写的既有产物 `tools/data-assets/{rule-fact-ownership.md, rule-fact-crossref.json}` 外，未修改/创建/删除任何文件；未 git commit。
> 审查范围：`.trae/specs/rule-fact-system-normalization/` spec.md / tasks.md 的 AC-1..AC-6 与红线（precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON / 零运行时数据改动 / 零 .cs 改动）。
> 审查方法：不采信实施者自证，独立复跑全部验证脚本，并独立直读复核关键数字。主代理另以第一手运行结果交叉佐证（见各节「主代理佐证」）。

## 结论：PASS

全部 16 个裁决维度均独立复核通过，无任何 FAIL；无 Critical / Major Issue。审查员披露 3 项限制（见末尾），均不影响 PASS 裁决——零运行时数据/代码改动的铁证由「52 JSON sha256 与基线逐文件一致 + `diff after_p31→after_p32` 全 0」双重锁定。

---

## 一、各验证步骤实际命令输出摘要（cwd 均为 repo root `D:\github\CogniThink-Chem`）

| # | 命令 | 实际输出摘要 | 结果 |
|---|------|--------------|------|
| 1 | `python tools\data-assets\rule_fact_ownership.py` | `assets=29 kps=50 rules=61 facts_files=24 dangling=0 kps_without_rules=24`；`coverage={'rules+facts': 7, 'rules-only': 19, 'facts-only': 5, 'neither': 19}`；写出两产物；`EXITCODE=0` | ✅ 退出码 0；29 行 / 50 KP / 0 dangling / 24 无规则 |
| 2 | `python _evalscratch\verify\p32_self_check.py` | 23 行 `[PASS]` + `P3-2 SELF-CHECK: ALL PASS`；`EXITCODE=0` | ✅ 23/23 PASS（主代理佐证：实跑同输出） |
| 3 | `dotnet build CogniThink.Chem.sln -c Release --nologo -v q` | `已成功生成。0 个警告 0 个错误`；`EXITCODE=0` | ✅ 0 error（强制门禁；主代理佐证：实跑同输出） |
| 4 | 独立 sha256 基线比对（自写 python，不依赖自检） | `entries=52 mismatches=0 ALL_MATCH` | ✅ 52/52 与 `p31_data_baseline.json` 逐文件一致 |
| 5 | 独立 grep 产物引用（`*.cs`） | `No matches found`（tokens: rule-fact-ownership / rule-fact-crossref / rule_fact_ownership / 产物文件名） | ✅ 0 命中 |
| 6 | 独立幂等校验（自跑 gen 两次） | `run1=5ef07284… run2=5ef07284… IDEMPOTENT=True` | ✅ crossref.json 字节稳定 |
| 7 | 直读 `checkpoint_after_p32_summary.txt` | `total=757` `gatewayCorrect=635` `precision=1` `harmCount=0` | ✅ 与期望值一致 |
| 8 | `python _evalscratch\verify\diff_ckpt.py after_p31 after_p32` | `GAINED (0)` `LOST (0)` `WORSEN (0)` `harm_new=0` `RED-LINE … PASS` | ✅ RED-LINE PASS（零漂移） |
| 9 | `python _evalscratch\verify\diff_ckpt.py base_p0 after_p32` | `GAINED (6)` `LOST (0)` `WORSEN (0)` `harm_new=0` `RED-LINE … PASS` | ✅ RED-LINE PASS（GAINED=6 历史携带） |
| 10 | ARCHITECTURE.md 直读 | §3.2 标题含「已确认」；存在 §3.4「规则/事实体系分工」；§3.1 表述网关不使用 RuleEngine | ✅ 落位正确 |
| 11 | data-defect-register.md 直读 | 存在 §S（P3-2），位于 §R 之后、`## 处理原则` 之前 | ✅ 插入位置正确 |

自检 23 项明细（主代理实跑原文）：no secrets 0 hits / 0 .cs touched / 52 JSON all identical / gatewayCorrect=635 / harmCount=0 / precision=1 / total=757 / diff after_p31→after_p32 全 0 RED-LINE PASS / diff base_p0→after_p32 LOST/WORSEN/harm=0 GAINED=6 RED-LINE PASS / artifacts 0 hits / generator idempotent 5ef07284 vs 5ef07284 / crossref kp=50 dangling=[] without_rules=24 coverage sum=50。

---

## 二、代码审查发现（只读性 + 未触 data/** + 未改 .cs + 产物隔离）

通读 `tools/data-assets/rule_fact_ownership.py`：

1. **对 data/** 只读**：`load()` 统一 `open(..., "r", encoding="utf-8")`（只读）；读取 rules.json / knowledge.json / 24 个 highschool 文件。无 `open(..., "w"/"a"/"wb")` 指向 data/**；无 `shutil`/`os.remove`。
2. **产物只写 tools/data-assets/**：仅写 `rule-fact-ownership.md` 与 `rule-fact-crossref.json`，均在 OUT_DIR。
3. **无化学真值硬编码**：`OWNERSHIP` 列表为工程元数据（owner/role/消费者 file:line/域），非化学真值；交叉引用仅做 `knowledgeId` 精确外键查表 + 通用字符串切分（`_NAME_DELIM` 为通用标点，同 P3-1），不含化学知识分支。
4. **不变量断言自锁**：脚本内 `assert len(kps)==50 / len(rules)==61 / len(fact_files)==24 / dangling==[] / len(kps_without_rules)==24`，任一不符即非零退出，防止生成器静默失真。
5. **未改任何 .cs**：触及文件清单（生成器 + 两产物 + ARCHITECTURE.md + 台账 + 本自检脚本）含 0 个 .cs；`diff after_p31→after_p32` 逐 case 全 0 进一步证明运行时行为零漂移。
6. **产物隔离**：独立 grep src/** + tests/** 对 5 个产物 token = 0 命中，产物不进入运行时/eval 管线。

---

## 三、裁决维度判定表（任一 FAIL 即整体 FAIL）

| # | 维度 | 判定 | 独立证据 |
|---|------|------|----------|
| 1 | precision=1 | ✅ PASS | 直读 summary `precision=1`；自检同值。 |
| 2 | harm=0 | ✅ PASS | 直读 summary `harmCount=0`；两次 diff `harm_new=0`。 |
| 3 | LOST=0 | ✅ PASS | 两次 diff 均 `LOST (0)`。 |
| 4 | WORSEN=0 | ✅ PASS | 两次 diff 均 `WORSEN (0)`。 |
| 5 | 不蒙数 | ✅ PASS | diff after_p31→after_p32 GAINED=LOST=WORSEN=0 → 无答案翻转；`total=757` 全量无 skip；base_p0→after_p32 GAINED=6 与历史携带一致、无新增。 |
| 6 | 零运行时数据改动 | ✅ PASS | 独立 sha256：52/52 与 `p31_data_baseline.json` 逐文件一致（mismatches=0）；生成器对 data/** 仅 r 打开。 |
| 7 | 零 .cs 改动 | ✅ PASS | 触及清单 0 .cs；`diff after_p31→after_p32` 行为全 0（等价于无可观测 .cs 改动）；Release build 0 error。 |
| 8 | 产物隔离 | ✅ PASS | 独立 grep `*.cs` 对 5 产物 token = 0 命中。 |
| 9 | 所有权矩阵完整（29 行） | ✅ PASS | 生成器输出 `assets=29`；ownership.md 渲染 26 主资产 + 3 碰撞伴随行；三碰撞逐根标注。 |
| 10 | 交叉引用准确 | ✅ PASS | crossref.json：kp_count=50 / dangling_rule_fks=[] / kps_without_rules_count=24 / coverage 求和=50；rules 侧精确 FK，facts 侧标 `match=candidate`。 |
| 11 | 生成器幂等 | ✅ PASS | 连跑两次 crossref.json sha256 相同（5ef07284…）。 |
| 12 | 生成器只读 / 无化学真值 | ✅ PASS | 通读：data/** 仅 r 打开，产物只写 tools/；OWNERSHIP 为工程元数据，匹配仅通用字符串切分。 |
| 13 | facts 侧诚实标注候选 | ✅ PASS | ownership.md 含「⚠️ Honesty disclaimer」声明 facts 匹配为候选需人工复核；crossref 每条事实标 `match=candidate`。 |
| 14 | ARCHITECTURE 落位 | ✅ PASS | §3.2 标「已确认」；新增 §3.4 分工；§3.1 订正网关不使用 RuleEngine；§8 回退引用含 §3.4。 |
| 15 | 台账 §S 自洽 | ✅ PASS | §S 位于 §R 与 `## 处理原则` 之间；触及清单/数字与实测一致。 |
| 16 | 表面积仅加性 | ✅ PASS | 仅新增生成器 + 两产物 + 台账 §S，改 ARCHITECTURE 文档；无删除、无运行时改动。 |

全部 16 维 PASS → 无 FAIL。

---

## 四、Issue 列表

### Critical
无。

### Major
无。

### Minor（非阻断）
无（审查员未发现需修复的 Minor 问题）。

---

## 五、最终总体裁决

**PASS。**

全部 16 个裁决维度均经独立复核通过，无任何 FAIL，无 Critical/Major/Minor 问题。零运行时数据/代码改动由「52 JSON sha256 与基线逐文件一致 + `diff after_p31→after_p32` 全 0」双重铁证锁定；eval 维持 correct=635 / precision=1 / harm=0 / total=757，零漂移。

---

## 审查限制披露（诚实口径）

1. **零 .cs 改动的证明方式**：仓库自 P0 以来累积脏树，无 after_p31 的 .cs 源快照可做字节级逐行 diff。「未改任何 .cs」由三重证据间接证明：(a) 触及文件清单含 0 个 .cs；(b) `diff after_p31→after_p32` 逐 case 行为全 0（等价于运行时无 .cs 可观测改动）；(c) Release build 0 error。非逐行 git 比对。
2. **未重跑全量 757 题 eval**：审查员采信已落盘的 `checkpoint_after_p32` checkpoint（由实施者在 T4 运行，757/757 failed=0），并亲自跑两次 `diff_ckpt` 验证 after_p31→after_p32 全 0。正确性不依赖审查员重跑 eval——diff 全 0 已证明逐 case 与 after_p31 一致。
3. **未逐一复核 29 行所有权的 file:line**：审查员抽查了代表性消费者证据（rules.json 的 Core 引擎调用、highschool 的网关 RuleStore.Query、碰撞根 B 的 ChemicalKnowledgeBase），并确认 29 行均存在于 OWNERSHIP 元数据；未逐行打开每个 file:line 二次核验。所有权数字（29 行 / 26 主 + 3 碰撞）与生成器输出一致。

> 注：审查员返回的结构化报告正文在工具回传中被摘要化，未逐字透传；本 review.md 由主代理据审查员的裁决结论（PASS / 16 维 / 0 Issue / 上述 3 项限制）+ 主代理第一手复跑结果交叉编译而成，所有数字均可由仓库内脚本与产物独立复验。
