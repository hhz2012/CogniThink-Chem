# Tasks — P3-2 规则/事实体系归一（明确分工 + 交叉引用）

> 关联 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/rule-fact-system-normalization/spec.md)
> 基线：`after_p31`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> 诚实口径：预期 eval 正确性增益 = 0（纯文档 + 只读工具 + ARCHITECTURE.md 更新，零运行时数据/代码改动）。
> 批准门禁：CQ-1=a（§6#1 确认推荐方向）/ CQ-2=a（明确分工+交叉引用，零漂移）/ CQ-3 零运行时改动 经用户确认后，才开始 T1 实现。**CQ-1 / CQ-2 已获用户确认（2026-11-22）。**

## Task Breakdown

| ID | 任务 | 状态 | 产出 |
|----|------|------|------|
| T0 | 只读审计：两套体系盘点 / 消费者映射 / knowledgeId 外键健康 / 覆盖缺口 / 三碰撞分工 | completed（spec 阶段已实证，证据入 spec §1） | spec §1 |
| T1 | 所有权矩阵生成器：`tools/data-assets/rule_fact_ownership.py` → `rule-fact-ownership.md` + `rule-fact-crossref.json`（29 行：26 主资产 + 3 碰撞；50 KP 交叉引用；0 dangling FK；24 无规则 KP；crossref 幂等 5ef07284） | completed（exit 0；52 JSON sha256 不变） | `tools/data-assets/` 新增 3 文件 |
| T2 | ARCHITECTURE.md 更新：§3.2 标 §6#1 已确认 + 新增 §3.4「规则/事实体系分工」+ §3.1 订正网关不使用 RuleEngine | completed（仅文档，0 .cs） | ARCHITECTURE.md 文档改动 |
| T3 | 台账闭环：新增 §S（触及范围 attestation + 所有权矩阵 + 交叉引用 + ARCHITECTURE 更新 + 零漂移），插于 §R 与 `## 处理原则` 之间 | completed（§S 已插入，位置正确） | data-defect-register.md §S |
| T4 | 全量 eval `after_p32` + 两次红线 diff（after_p31→after_p32 全 0；base_p0→after_p32 LOST/WORSEN/harm=0，GAINED 维持 6） | completed（757/757 failed=0，correct=635；两次 RED-LINE PASS） | `after_p32` checkpoint + diff 全 0 |
| T5 | 红线自检：`p32_self_check.py`（密钥 / 52 JSON sha256 未变 / 无运行时 .cs 改动 / 产物不被运行时引用 / eval diff 全 0 / 生成器幂等 / 不蒙数） | completed（**23/23 ALL PASS**） | `_evalscratch/verify/p32_self_check.py` 全 PASS |
| T6 | fresh-context 只读子代理独立 review → review.md，裁决 PASS/FAIL | completed（16/16 维度 PASS，0 Critical/Major/Minor） | [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/rule-fact-system-normalization/review.md) |

---

## T0 — 只读审计（spec 阶段已完成）
**Status**: completed — 证据入 spec §1：体系 A `data/rules/rules.json` 61 条 if-then 规则（Conservation 3 / Structural 38 / Principle 14 / Conditional 3 / Exception 3），61/61 带 `knowledgeId`，仅 7 条带 tests，`rule-tests.json` 9 条；消费者仅 Core（LoadRules → 5 个推理引擎按 rule-id 取用 + RuleEngine.Decide 仅 RuleRegressionRunner/测试），**网关 0 消费**。体系 B `data/highschool/*.json` 24 文件，两栈都用（网关 15 处 RuleStore.Query + Core 读子集），全部有消费者（无完全死数据），仅 phenomena.json 带 knowledgeId。外键健康：26 distinct knowledgeId 全部有效（0 dangling FK）；50 KP 中 24 个无任何规则（含 KH-RP-09）。三碰撞文件 ions/substances/solubility 跨根 schema+消费者不同。

## T1 — 所有权矩阵 + 交叉引用生成器
**Status**: completed（`assets=29 kps=50 rules=61 facts_files=24 dangling=0 kps_without_rules=24`；coverage `{rules+facts:7, rules-only:19, facts-only:5, neither:19}` 求和=50；crossref.json 连跑两次 sha256 前缀均 `5ef072842ccf5750`，幂等；52 JSON sha256 运行前后不变）
**Outputs**:
- 新建 `tools/data-assets/rule_fact_ownership.py`：
  - 读 `data/rules/rules.json`（精确 knowledgeId）、`data/knowledge/knowledge.json`（50 KP）、`data/highschool/*.json`（24 文件，名称候选匹配）。
  - 所有权矩阵：硬编码 26 资产的 owner_stack / role / 消费者证据（file:line，据 T0 审计固化）；三碰撞文件逐根两行。
  - 交叉引用：每 KP → rules（精确 FK）+ facts（STRICT 名称子串驱动标签，LENIENT 仅上下文，标注 candidate）+ coverage_label；顶层 dangling_rule_fks + kps_without_rules。
  - 写 `rule-fact-ownership.md` + `rule-fact-crossref.json`，均在 `tools/data-assets/`。
  - **只读**打开 data/**（r/rb），产物只写 tools/data-assets/；不含化学真值。
- 首次运行生成两产物，确认 26 资产全覆盖、50 KP 全覆盖、kps_without_rules=24、dangling=[]。
**Constraints**: 不改任何 data/** JSON；生成器不含化学真值。
**Verification**: `python tools/data-assets/rule_fact_ownership.py` 退出码 0；crossref.json 可解析且 kps_without_rules 长度=24；ownership.md 渲染正常。

## T2 — ARCHITECTURE.md 更新
**Status**: completed（§3.2 标题「推荐，待你确认」→「已确认，2026-11-22」+ 引用块更新；§3.1 订正网关仅用 RuleStore、不使用 RuleEngine；新增 §3.4「规则/事实体系分工」；§8 回退说明引用 §3.4；仅文档改动，0 .cs）
**Outputs**:
- §3.2 收敛决策标注：「推荐，待确认」→「已确认（2026-11-22，用户确认 §6#1）」。
- 新增「规则/事实体系分工」小节：声明 rules.json=Core 仲裁/基准规则库、highschool=网关事实库、同域不同角色非冗余、三碰撞分工、链接 rule-fact-ownership.md。
**Constraints**: 仅文档改动，不触代码/数据。
**Verification**: 文档直读；措辞与 spec §1 审计一致。

## T3 — 台账闭环
**Status**: completed（[data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 已新增 §S（P3-2），插于 §R 末尾 `---` 与 `## 处理原则` 之间；S.1–S.5 覆盖触及清单 attestation / 所有权矩阵 / 交叉引用 / ARCHITECTURE 落位 / eval 零漂移）
**Outputs**: [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §S（插于 §R 末尾 `---` 与 `## 处理原则` 之间）：S.1 触及文件清单（无 data/** JSON、无运行时 .cs）/ S.2 所有权矩阵 / S.3 交叉引用 / S.4 ARCHITECTURE §6#1 已确认 / S.5 eval 零漂移。
**Verification**: §S 插入位置正确；触及清单与实际一致。

## T4 — 全量 eval + 红线 diff
**Status**: completed（`run_eval_resume.ps1 -Tag after_p32 -Env Production` → 757/757 failed=0，correct=635，precision=1，harm=0；`diff after_p31→after_p32` GAINED/LOST/WORSEN/harm 全 0 RED-LINE PASS；`diff base_p0→after_p32` LOST/WORSEN/harm=0，GAINED=6（历史携带）RED-LINE PASS）
**Outputs**:
- `run_eval_resume.ps1 -Tag after_p32 -Env Production` → 757/757 failed=0，correct=635。
- `diff_ckpt after_p31 after_p32`：全 0 → RED-LINE PASS（无运行时改动，必为 0）。
- `diff_ckpt base_p0 after_p32`：LOST/WORSEN/harm=0（GAINED 维持 6）。
**Verification**: 两次 diff RED-LINE PASS。若 diff 非 0 → 停下根因（必是意外改了运行时数据或代码），回退后复跑。

## T5 — 红线自检
**Status**: completed（`_evalscratch/verify/p32_self_check.py` **23/23 ALL PASS**：密钥 0 命中 / 0 .cs touched / 52 JSON 与 after_p31 基线全相同 / correct=635 harm=0 precision=1 total=757 / 两次 diff RED-LINE PASS / 产物 0 运行时引用 / 生成器幂等 / crossref 不变量 50/[]/24/sum=50）
**Verification**（脚本化 `_evalscratch/verify/p32_self_check.py`，结果入 review 证据）：
- 密钥扫描：P3-2 新增/改动文件 → 0 命中。
- 未改运行时数据：52 个 data/** JSON 逐文件 sha256 与 `after_p31` 基线（`p31_data_baseline.json`）对比 → 全相同。
- 未改运行时代码：触及文件清单不含任何 .cs（ARCHITECTURE.md / tools / scratch / 台账除外）。
- 产物隔离：ownership/crossref/生成器不被任何运行时 .cs 引用（grep 0 命中）。
- 脚本只读：T1 运行前后 52 JSON sha256 不变；生成器幂等。
- 不蒙数：after_p32 correct=635，harm=0，total=757。
- 两次 diff RED-LINE PASS。

## T6 — 独立 review
**Status**: completed（fresh-context 只读子代理 **16/16 维度 PASS**，0 Critical/Major/Minor；详见 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/rule-fact-system-normalization/review.md)；零运行时数据/代码改动由「52 JSON sha256 与基线逐文件一致 + `diff after_p31→after_p32` 全 0」双重铁证锁定）
**Process**: 委托 fresh-context **只读** 子代理，独立复跑：T1 生成器（26 资产 / 50 KP / kps_without_rules=24 / dangling=[]）/ T4 eval 两次红线 diff / T5 自检全 PASS / 52 JSON sha256 未变 / 产物不被运行时引用 / 代码审查（脚本只读、未触 data/**、未改 .cs / ARCHITECTURE 措辞与审计一致）。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 零运行时数据改动 / 零 .cs 改动 / 产物隔离 / 所有权矩阵完整 / 交叉引用准确。任一 FAIL → Issue → 修复 → 复审。子代理仅返回报告文本，由主代理落盘 review.md。

---

## 决策日志（CQ）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | §6#1 两套栈收敛方向 | a 确认推荐方向（网关为主 / Core 基准回归）/ b 反向收敛 / c 维持不分栈 | **a（已确认，2026-11-22）** | 见 spec §2 |
| CQ-2 | P3-2 归一策略 | a 明确分工+交叉引用（零漂移）/ b 合并单一事实源 / c 仅归档死数据 | **a（已确认，2026-11-22）** | 见 spec §2 |
| CQ-3 | 运行时数据/代码 | 零 data/** JSON 改动、零 .cs 改动、零 eval 管线改动 | **已确认（默认红线）** | 见 spec §2 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | fresh-context 只读子代理 | PASS（16/16，0 Critical/Major/Minor） | 无 Issue；零运行时改动由 52 JSON sha256 一致 + diff 全 0 双重锁定；见 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/rule-fact-system-normalization/review.md) |
