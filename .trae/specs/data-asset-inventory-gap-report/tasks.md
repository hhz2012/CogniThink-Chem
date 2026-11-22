# Tasks — P3-1 数据资产归一与缺口报表自动化

> 关联 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/data-asset-inventory-gap-report/spec.md)
> 基线：`after_p24`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> 诚实口径：预期 eval 正确性增益 = 0（纯工具 + 纯新增产物，不改运行时数据/求解/判分/eval 管线）。
> 批准门禁：CQ-1=a / CQ-2=a / CQ-3=a / CQ-4=a / CQ-5=a / CQ-6 零数据改动 经用户确认后，才开始 T1 实现代码。

## Task Breakdown

| ID | 任务 | 状态 | 产出 |
|----|------|------|------|
| T0 | 只读审计：两数据根 52 JSON 盘点 / 消费者映射 / version 字段缺口 / 三文件名碰撞 / 现有缺口报表现状 / .bak 与二进制资产 | completed（spec 阶段已实证） | 证据入 spec §2 |
| T1 | 清单生成器：`tools/data-assets/{gen_inventory.py, asset_map.py}` → 生成 `data-asset-manifest.json` + `data-asset-inventory.md`（52 JSON 全覆盖，无 UNMAPPED；含碰撞标注 + 二进制计数） | completed（52 项 / 0 UNMAPPED / exit 0；52 JSON sha256 不变） | `tools/data-assets/` 新增 6 文件 |
| T2 | 缺口报表：`tools/data-assets/gap_report.py` → 生成 `gap_report.md`（50 知识点 × 事实 × 题，候选缺口标注置信度，只读不改数据） | completed（strict/lenient 双匹配；1 strong candidate `KH-RP-09 电解池`；52 JSON 不变） | `tools/data-assets/gap_report.py` + `gap_report.md` |
| T3 | 版本绑定演示 + 同步自检：临时副本改 1 字节 → 重跑生成器 → 仅该文件 sha256/manifest_hash 变化；确认 52 JSON 运行后 sha256 全不变 | completed（`p31_version_binding_demo.py` PASS；真实数据/manifest 未触碰） | 演示证据：`_evalscratch/verify/p31_version_binding_demo.py` |
| T4 | 全量 eval `after_p31` + 两次红线 diff（after_p24→after_p31 全 0；base_p0→after_p31 LOST/WORSEN/harm=0，GAINED 维持 6） | completed（correct=635 / precision=1 / harm=0；两次 RED-LINE PASS） | `after_p31` checkpoint + diff 全 0 |
| T5 | 台账闭环：新增 §R（P3-1 触及范围 + 产物 + 碰撞登记 + .bak 决策 + 零数据改动 attestation），插于 §Q 与 `## 处理原则` 之间 | completed（§R 已插入 L788–845） | [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) §R |
| T6 | 红线自检：`p31_self_check.py`（密钥 / 52 JSON sha256 未变 / 未改运行时路径 / 产物不被运行时引用 / eval diff 全 0 / 不蒙数 / 表面积仅新增） | completed（**18/18 PASS**） | `_evalscratch/verify/p31_self_check.py` 全 PASS |
| T7 | fresh-context 只读子代理独立 review → review.md，裁决 PASS/FAIL | completed（12/12 维度 PASS，0 Critical/Major；3 Minor 已处理：M-1/M-2 修复、M-3 无需修复；复审后自检 18/18 PASS） | [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/data-asset-inventory-gap-report/review.md) |

---

## T0 — 只读审计（spec 阶段已完成）
**Status**: completed — 证据入 spec §2：两数据根共 52 JSON（根 A `data/` 44 个，根 B `Api/data/` 8 个）；消费者映射覆盖 6 个加载器（Core ChemDataLoader/DataRoot/RuleStore/BenchmarkLoader/ReactionLibrary，Api ChemicalKnowledgeBase/GatewayVsLlmEvaluationService/HalfReactionBalancer，Cli VerifyRealCommand）；11 个 JSON 无 `version` 字段（含 pdf_questions/tier2/6 chemistry/electrode-half-reactions 等裸 list）；三文件名跨根碰撞（ions/substances/solubility，schema 与消费者均不同）；现有缺口报表仅 `_evalscratch/analyze_gap.py` 一次性探针；5 个 untracked `.bak` 不被加载；410 个 tracked 二进制资产（PNG/SVG）为合法运行时资产。

## T1 — 清单生成器 + 双产物
**Status**: completed（52 项 / 0 UNMAPPED / exit 0；52 JSON sha256 不变；manifest_hash 幂等 fc1835536b12…）
**Outputs**:
- 新建 `tools/data-assets/asset_map.py`：据 spec §2 审计固化 52 项的 `consumer` / `load_mechanism` / `validation_entry` 映射（dict，key = 相对 repo root 路径）。
- 新建 `tools/data-assets/gen_inventory.py`：
  - 扫描两数据根全部 `*.json`（硬编码两根路径，不递归到 `_evalscratch`/`bin`/`pdfs`）；
  - 对每个 JSON 计算 sha256 / byte_size / item_count / schema_kind / declared_version（读顶层 `version`，无则 null）/ effective_version（declared 或 `hash:<12hex>`）；
  - 查 `asset_map.py`，无映射 → 标 `UNMAPPED` 并使退出码非 0（强制补全）；
  - 写 `data-asset-manifest.json`（含 `binding` 区段：question_sets / fact_library / knowledge_base / manifest_hash）；
  - 渲染 `data-asset-inventory.md`（Markdown 表 + §2.2 碰撞标注 + 二进制资产计数：282 PNG + 128 SVG）。
  - **只读**打开 `data/**`（`'r'`），产物只写 `tools/data-assets/`。
- 首次运行生成 `data-asset-manifest.json` + `data-asset-inventory.md`，确认 52 项全覆盖、无 UNMAPPED。
**Constraints**: 不改任何 `data/**` JSON；生成器不含化学真值（纯读取/哈希/查表）。
**Verification**: `python tools/data-assets/gen_inventory.py` 退出码 0；manifest 含 52 项；inventory.md 渲染正常。

## T2 — 缺口报表自动化
**Status**: completed（strict/lenient 双匹配；50 知识点覆盖，Main=44；1 strong candidate `KH-RP-09 电解池`；52 JSON 不变）
**Outputs**:
- 新建 `tools/data-assets/gap_report.py`：
  - 读 `data/knowledge/knowledge.json`，取全部 items（重点输出 `tier=="Main"` 主干子集）；
  - 读 `data/highschool/*.json` 全部集合字段（`statements`/`entries`/`rules`/`conditions`/`formulas`/`facts`/`table`/`steps`/`series`/`cations`/`anions` 等），对知识点 `name` + `textbookRef` 关键词做子串匹配 → 事实覆盖（命中文件 + 条目 id）；
  - 读 `src/.../data/pdf_questions.json` + `data/benchmark/problems/*.json`，在题面文本（`question`/`answerKeywords`/options 值）做同样匹配 → 题覆盖（命中题 id）；
  - 生成 `gap_report.md`：每知识点一行（id / name / module / tier / 事实覆盖计数 / 题覆盖计数 / 缺口标签），末尾三类缺口汇总（无事实 / 无题 / 两者皆无）+ 统计；
  - 页眉明确标注「候选缺口·名称关键词匹配·非 knowledge_id 外键关联·需人工复核·不作引擎行为依据」。
  - **只读**打开所有数据文件，不写回。
- 首次运行生成 `gap_report.md`。
**Constraints**: 只读不改数据；匹配结果标注候选性质，不宣称确定性。
**Verification**: `python tools/data-assets/gap_report.py` 退出码 0；覆盖 50 个知识点；报表无空字段。

## T3 — 版本绑定演示 + 同步自检
**Status**: completed（`p31_version_binding_demo.py` PASS；临时副本改 1 字节仅 1 项变化，真实数据/manifest 未触碰）
**Outputs**:
- 演示：复制 `data/highschool/general-choice-facts.json` 到系统临时目录，改 1 字节，临时跑生成器指向该副本 → 确认 manifest 中该项 sha256/effective_version 与 `manifest_hash` 变化，其余 51 项不变（脚本化断言 + 留证）。临时副本不入 `data/`，不影响 eval。
- 同步自检：T1+T2 运行前后，对 52 个 `data/**` JSON 逐文件 sha256 对比 → 全不变（证明脚本只读）。
**Verification**: 演示脚本断言通过；52 项 sha256 前后一致。

## T4 — 全量 eval + 红线 diff
**Status**: completed（correct=635 / precision=1 / harm=0；after_p24→after_p31 全 0；base_p0→after_p31 LOST/WORSEN/harm=0，GAINED 维持 6）
**Outputs**:
- `run_eval_resume.ps1 -Tag after_p31 -Env Production` → 757/757 failed=0，correct=635。
- `diff_ckpt after_p24 after_p31`：GAINED/LOST/WORSEN/harm 全 0 → RED-LINE PASS（P3-1 未碰运行时数据/求解/判分，必为 0）。
- `diff_ckpt base_p0 after_p31`：LOST/WORSEN/harm=0（GAINED 维持 6 携带）。
**Verification**: 两次 diff RED-LINE PASS。若 diff 非 0 → 停下根因（必是意外改了运行时数据或代码），回退后复跑。

## T5 — 台账闭环
**Status**: completed（§R 已插入 data-defect-register.md L788–845：R.1–R.7）
**Outputs**: [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §R（插入于 §Q 末尾 `---` 与 `## 处理原则` 之间，即当前 L786 之后、L788 之前）：
- R.1 触及文件清单（content attestation：仅新增 `tools/data-assets/*` + `_evalscratch/verify/p31_self_check.py` + 台账本身；**无 `data/**` JSON 改动、无运行时代码改动**）。
- R.2 数据资产清单产物（manifest/inventory 覆盖 52 JSON + 碰撞标注 + 二进制计数）。
- R.3 缺口报表产物（50 知识点 × 事实 × 题，候选缺口标注）。
- R.4 版本绑定策略（manifest 旁路指纹，零运行时 JSON 改动；binding 区段结构）。
- R.5 三文件名跨根碰撞登记（ions/substances/solubility，归一化留 P3-2）。
- R.6 5 个 `.bak` 处置决策（CQ-3：仅登记不动）。
- R.7 eval 零漂移（after_p24→after_p31 diff 全 0）。

## T6 — 红线自检
**Status**: completed（`p31_self_check.py` 18/18 PASS：密钥 0 命中 / 52 JSON 不变 / 产物 0 引用 / 两次 diff RED-LINE PASS / 幂等 / total=757）
**Verification**（脚本化 `_evalscratch/verify/p31_self_check.py`，结果入 review 证据）：
- 密钥扫描：P3-1 新增文件 → 0 命中。
- 未改运行时数据：52 个 `data/**` JSON 逐文件 sha256 与 `after_p24` 基线（T0 已记录）对比 → 全相同。
- 未改运行时路径：`ProcessStudentMessageAsync` / `CallEngineAsync` / `BuildGatewayAnswerAsync` / `IsStructuredCorrect` / 6 个数据加载器 方法体未变（checkpoint 逐 case `gatewayAnswer`/`gatewayOk`/`gatewayConclusion` 与 after_p24 diff=0）。
- 产物隔离：`data-asset-manifest.json` / `inventory.md` / `gap_report.md` 不被任何运行时 .cs 引用（grep 0 命中）。
- 脚本只读：T1/T2 运行前后 52 JSON sha256 不变。
- 不蒙数：`after_p31` correct=635，harm=0。
- 表面积：仅新增 `tools/data-assets/` + scratch 自检 + 台账 §R，无意外删除。

## T7 — 独立 review
**Status**: completed（fresh-context 只读子代理 12/12 维度 PASS，0 Critical/Major；3 Minor 已处理；复审后复跑自检 18/18 PASS；详见 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/data-asset-inventory-gap-report/review.md)）
**Process**: 委托 fresh-context **只读** 子代理，独立复跑：T1 生成器（manifest 52 项无 UNMAPPED）/ T2 缺口报表（50 知识点覆盖）/ T3 版本绑定演示 / T4 eval 两次红线 diff / T6 自检全 PASS / 52 JSON sha256 未变 / 产物不被运行时引用 / 代码审查（脚本只读、未触 data/**、未改运行时路径）。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 零运行时数据改动 / 产物隔离 / 清单完整。任一 FAIL → Issue → 修复 → 复审。子代理仅返回报告文本，由主代理落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/data-asset-inventory-gap-report/review.md)。

---

## 决策日志（CQ）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | 版本绑定实现位置 | a manifest 旁路不改运行时 JSON / b 包 dict 加 version 改 loader | **a（已批准）** | 见 spec §4 CQ-1 |
| CQ-2 | 生成产物是否入库 | a 均 tracked / b 仅跟踪脚本 | **a（已批准）** | 见 spec §4 CQ-2 |
| CQ-3 | 5 个 .bak 处置 | a 仅登记不动 / b 移 _evalscratch/_archive / c 删除 | **a（已批准）** | 见 spec §4 CQ-3 |
| CQ-4 | 缺口匹配口径 | a 名称匹配候选缺口 / b knowledge_id 外键 | **a（已批准）** | 见 spec §4 CQ-4 |
| CQ-5 | 工具放置位置 | a tools/data-assets/ tracked / b _evalscratch | **a（已批准）** | 见 spec §4 CQ-5 |
| CQ-6 | 运行时数据 | 零 data/** JSON 改动，零 eval 管线改动 | **已批准（默认红线）** | 见 spec §4 CQ-6 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | fresh-context 只读子代理 | PASS（12/12，0 Critical/Major；3 Minor 已处理） | M-1 生成器硬编码计数 12→11（已修复）、M-2 spec §2.3/§3.2 笔误（已修复）、M-3 svg 含 untracked（已披露无需修复）；见 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/data-asset-inventory-gap-report/review.md) |
