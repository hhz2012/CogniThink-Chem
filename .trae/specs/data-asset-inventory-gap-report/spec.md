# Spec — P3-1 数据资产归一与缺口报表自动化（清单 + 自动缺口报表 + 版本绑定）

> 关联路线图：[project-structure-analysis-and-roadmap.md](file:///d:/github/CogniThink-Chem/.trae/documents/project-structure-analysis-and-roadmap.md) §P3-1。
> 基线：`after_p24`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> 诚实口径：本 spec **预期 eval 正确性增益 = 0**。P3-1 是**纯工具 + 纯新增产物**：不改任何运行时加载的 `data/**` JSON、不改求解器/采纳门/判分、不改 eval 管线。价值 = (1) 让 52 个 JSON 数据资产「谁消费 / 什么版本 / 怎么校验」有唯一可机读的清单；(2) 把「主干知识点无事实 / 无题」缺口报表从手工探针升级为可复跑的自动化产物；(3) 给 `pdf_questions.json` 与事实库建立**指纹级版本绑定**，使回归 diff 可精确溯源到「哪个数据文件变了」。

## 1. 背景与目标

路线图 P3-1：「产出『数据资产清单』：每个 JSON 文件的消费者、版本、校验入口。自动化『主干知识点无事实/无题』缺口报表（规划 T1-9/T4-10，当前仅手工）。题库版本化：`pdf_questions.json` 与事实库版本绑定，回归可版本对比。」

P0–P2 全部闭环（P0 结构收口、P1 五项能力补全、P2-1/P2-2/P2-3/P2-4 架构演进与前端闭环），eval 稳定在 635 正确 / precision=1。但数据侧仍有三类可观测性缺口：

1. **无数据资产清单**：52 个 JSON 分散在两个根（repo-root `data/` 与 `src/CogniThink.TutorBrain.Api/data/`），消费者散落在 Core/Api/Cli 的 6+ 个加载器里；新成员无从知道「改这个 JSON 会影响谁」。
2. **缺口报表未自动化**：路线图 §4.2 M9 明确「缺『主干无规则/无题』缺口报表」，当前只有 `_evalscratch/analyze_gap.py` 这类一次性探针（读某个 snapshot 分类 bothWrong），无 maintained、可复跑、覆盖知识点×事实×题三者交叉的报表。
3. **题库无版本绑定**：`pdf_questions.json`（941 KB / list[545]）、`tier2_questions.json`（list[109]）及 6 个 `chemistry/*.json` 全是**裸 list、无 `version` 字段**；2 个 highschool 文件也缺 `version`。回归 diff 时无法回答「这次 eval 变化是事实库改了还是题库改了」。

本 spec 目标：用**纯工具 + 纯新增产物**（不动任何运行时数据与代码路径）一次性补齐这三项，且 eval 零漂移可证。

## 2. T0 只读审计（证据）

### 2.1 数据资产清单（52 个 JSON，两数据根）

**根 A：repo-root `data/`（44 个 JSON，全部 git-tracked；`.gitignore` 不忽略 `data/**`）**

| 目录 | 文件数 | 顶层结构 | 有 `version` 字段 | 消费者（.cs，行号） | 加载机制 | 校验/测试入口 |
|------|--------|----------|-------------------|----------------------|----------|----------------|
| `data/benchmark/problems/` | 12 | dict `{version, items}`（+偶有 `note`） | ✅ 全有（1.0） | Core [BenchmarkLoader.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Benchmark/BenchmarkLoader.cs) L21-38 | 枚举目录 + `ChemDataLoader.ReadItems<ProblemSpec>` | Core `BenchmarkTests` |
| `data/highschool/` | 24 | 22 个 dict 带 `version`；**2 个无 version**（`electrode-reaction-facts.json` = `{_comment, correct, incorrect}`；`infeasible-equation-patterns.json` = `{_comment, rules}`） | 22/24（concept-facts=1.5、judgment-statement-facts=1.2、其余 1.0） | Core [RuleStore.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Rules/RuleStore.cs) L32-68（Api 网关校验器经 `FactChoiceMatcher` 系消费）；[HalfReactionBalancer.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/HalfReactionBalancer.cs) 消费 electrode-half-reactions | `RuleStore.Load()` 定位 `highschool` 目录，`Query<T>(key)` 按文件名读 + 缓存 | 各 `*ValidatorTests`（ConceptChoice / Experiment / Organic / …） |
| `data/knowledge/` | 1 + 3(reactions) | `knowledge.json` = dict `{version:0.2, note, items}`；`electrode-half-reactions.json` = **list[1] 无 version**；`reactions/*.json` = dict `{version:1.0, items}` | knowledge.json ✅(0.2)；3 reactions ✅(1.0)；**electrode-half-reactions ❌** | `knowledge.json`/`rules*` → Core [ChemDataLoader.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Data/ChemDataLoader.cs) L45-73（经 [DataRoot.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Data/DataRoot.cs) L10-44 marker 向上查找）；`reactions/*` → Core [ReactionLibrary.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Reactions/ReactionLibrary.cs) L66-81；`electrode-half-reactions` → Api `HalfReactionBalancer.cs` | DataRoot marker walk-up（`COGNITHINK_DATA` 或向上找含 `knowledge/knowledge.json` 的 `data` 目录）/ BaseDirectory | Core 推断引擎测试 |
| `data/rules/` | 2 | dict `{version:0.1, note, items}` | ✅（0.1） | Core `ChemDataLoader.cs` → `RuleEngine` | DataRoot walk-up | `rule-tests.json` 即测试用例集 |
| `data/real-exam/` | 1 | dict `{version:1.0, items}` | ✅（1.0） | Cli [VerifyRealCommand.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Cli/Commands/VerifyRealCommand.cs) | `--verify-real` 加载 | Cli 真题回归 |

**根 B：`src/CogniThink.TutorBrain.Api/data/`（8 个 JSON，全部 git-tracked）**

| 目录/文件 | 顶层结构 | 有 `version` | 消费者（.cs，行号） | 加载机制 | 校验/测试入口 |
|-----------|----------|--------------|----------------------|----------|----------------|
| `chemistry/*.json`（6：substances/ions/reactions/ion-interactions/knowledge-links/solubility） | **5 个裸 list**（substances list[186]、ions list[56]、reactions list[253]、ion-interactions list[61]、knowledge-links list[6]）；solubility = dict `{rules, exceptions}` | **❌ 全无** | Api [ChemicalKnowledgeBase.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Chemistry/ChemicalKnowledgeBase.cs) L46-70（[Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Program.cs) L56-58 启动加载） | `builder.Environment.ContentRootPath` → 绝对路径读 | 启动时若缺文件告警「缺少知识文件：data\chemistry\substances.json」 |
| `pdf_questions.json` | **list[545]**，941 KB | ❌ | Api [GatewayVsLlmEvaluationService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) L1771-1863 | `AppContext.BaseDirectory/data/*.json` | 757 题 eval 主评估集（扩展后 757 case） |
| `tier2_questions.json` | **list[109]** | ❌ | 同上 `GatewayVsLlmEvaluationService` | 同上 BaseDirectory | tier2 评估集 |

> 条目数核实：`pdf_questions.json` 顶层 list 长度 = 545；eval 实际跑 757 case（含子问/扩展，派生逻辑在 `GatewayVsLlmEvaluationService`，不影响本 spec 的指纹绑定——指纹算的是源文件 sha256）。

### 2.2 严重结构债：三文件名跨根碰撞（同文件名、不同 schema、不同消费者）

| 文件名 | 根 A `data/highschool/` | 根 B `Api/data/chemistry/` |
|--------|------------------------|---------------------------|
| `ions.json` | dict（cations/anions/gases），821 B，consumer = RuleStore | **list[56]**，6 KB，consumer = ChemicalKnowledgeBase |
| `substances.json` | dict（organics/otherInorganics），1 KB，consumer = RuleStore | **list[186]**，38 KB，consumer = ChemicalKnowledgeBase |
| `solubility.json` | dict `{version, source, table}`，2.5 KB，consumer = RuleStore | dict `{rules, exceptions}`，6.5 KB，consumer = ChemicalKnowledgeBase |

后果：仅看文件名无法判断加载的是哪一份；`DataRoot` walk-up 与 `ContentRoot` 指向不同根时加载的对象不同（运行时 API 从 repo root 启动时 `ChemicalKnowledgeBase` 找不到 `data\chemistry\*` 会告警，但 Core 的 `RuleStore` 走 walk-up 能找到根 A 的）。**归一化（P3-2）会处理合并/分工；本 spec（P3-1）只在清单中如实标注碰撞，不合并。**

### 2.3 `version` 字段缺口（版本绑定的直接障碍）

- 52 个 JSON 中 **11 个无 `version` 字段**：`electrode-reaction-facts`、`infeasible-equation-patterns`（highschool，dict 但无 version）、`electrode-half-reactions`（knowledge，裸 list）、全部 6 个 `chemistry/*.json`、`pdf_questions.json`、`tier2_questions.json`。
- 其中 `pdf_questions`/`tier2`/`chemistry/*`/`electrode-half-reactions` 是**裸 JSON 数组**——无法在不改顶层类型的前提下加 `version`（`JsonSerializer.Deserialize<List<T>>` 对 object 顶层会抛异常），必须改 loader 才能包 dict。
- **关键设计结论**：版本绑定**不能靠改运行时 JSON**（会触发反序列化风险 + eval 漂移），必须靠**旁路 manifest 记录指纹**（见 §3.3）。

### 2.4 现有缺口报表 = 一次性探针，非 maintained

- [`_evalscratch/analyze_gap.py`](file:///d:/github/CogniThink-Chem/_evalscratch/analyze_gap.py)：读某个 `snapshot_current_probe.json`，对 `bothWrongCases` 按 taskType / 选项完整度 / 资产分类。是**一次性诊断脚本**（gitignored，无版本对比，不覆盖知识点×事实×题交叉）。
- 无任何「主干知识点（`knowledge.json` 中 `tier=Main`）× 事实库覆盖 × 题库覆盖」的 maintained 报表。

### 2.5 杂项资产

- **二进制资产（tracked，410 个）**：`Api/data/pdf_images/`（282 PNG）+ `Api/data/vector_svg/`（128 SVG），consumer = 网关图像资产路由（TryRouteGraph/Curve/Molecule）。是合法运行时资产，清单中作为单独类目登记（count + consumer），**不在本 spec 处理**（属 P0-3 仓库卫生范畴）。
- **5 个 stray `.bak`（untracked，不被加载）**：`Api/data/pdf_questions.json.bak_alpha2` / `.bak_alpha2batch2_current` / `.bak_pre_alpha2batch2` / `.bak_pre_alpha2complete` / `.bak_pre_verifyfixbatch`，各约 918 KB。`git ls-files *.bak*` 为空 → 未入库；无任何 .cs 引用。是历史快照（仓库自 P0 以来无 commit，这些可能是早期 pdf_questions 变体的唯一留存）。**不自动删除/移动**，作为 CQ-3 交用户决策。

## 3. 设计

### 3.1 数据资产清单（自动生成，机读 + 人读双产物）

新建工具目录 `tools/data-assets/`（tracked，非 `_evalscratch`），含：

- **生成器** `gen_inventory.py`：扫描两个数据根的全部 JSON（+ 二进制资产计数），对每个 JSON 计算：
  - `path`（相对 repo root）、`sha256`、`byte_size`、`item_count`（list 长度或 dict 条目数）、`schema_kind`（`dict-versioned` / `dict-unversioned` / `bare-list`）；
  - `declared_version`（读 JSON 顶层 `version`，无则 `null`——**绝不编造**）；
  - `effective_version`（= `declared_version` 若非 null，否则 `"hash:<sha256 前 12 位>"`——诚实指纹）；
  - `consumer` / `load_mechanism` / `validation_entry`（来自一份**手写映射表** `asset_map.py`，由本 spec 据 §2 审计填好；生成器不做代码 grep，避免脆弱）。
- **机读产物** `data-asset-manifest.json`（tracked）：`{ manifest_schema_version, generated_at, assets: { <path>: {...} }, binding: {...} }`。是版本绑定与回归 diff 的单一事实源。
- **人读产物** `data-asset-inventory.md`（tracked）：从 manifest 渲染的 Markdown 表（路径 / 消费者 / 版本 / 校验入口 / 结构 / sha256 前 12 位），含 §2.2 碰撞标注与二进制资产计数。

`asset_map.py` 的消费者映射 = 本 spec §2 审计的固化（6 个加载器覆盖全部 52 JSON；若有遗漏，生成器会把该资产标 `consumer: "UNMAPPED"` 并在退出码告警，强制补全）。

### 3.2 缺口报表自动化（主干知识点 × 事实 × 题）

新建 **maintained** 脚本 `tools/data-assets/gap_report.py` → 生成 `gap_report.md`（tracked）：

- **主干知识点集** = `data/knowledge/knowledge.json` 中 `tier == "Main"` 的 items（共 50 个 items，其中 `Main` 44 个 / `Extended` 6 个；主干 44 个含 `id`/`name`/`module`/`textbookRef`/`summary`）。
- **事实覆盖判定**：对每个主干知识点，在 `data/highschool/*.json` 的所有事实条目（`statements`/`entries`/`rules`/`conditions`/`formulas` 等集合字段）中，用知识点 `name` 及其 `textbookRef` 关键词做子串匹配 → 命中即「有事实覆盖」，列出命中文件与条目 id；未命中 → 「候选缺口：无事实」（**标注置信度 = 名称匹配，非 id 关联，可能漏报**）。
- **题覆盖判定**：对每个主干知识点，在 `pdf_questions.json`（545）+ `data/benchmark/problems/*.json`（98）的题面（`question`/`answerKeywords`/options 文本）中做同样名称匹配 → 命中即「有题覆盖」，列出题 id；未命中 → 「候选缺口：无题」。
- **输出**：每个主干知识点一行（id / name / 模块 / 事实覆盖（文件数+条目数）/ 题覆盖（题号数）/ 缺口标签）；末尾汇总「无事实」「无题」「两者皆无」三类清单 + 统计。
- **诚实约束**：匹配是**名称/关键词子串**，非结构化 id 关联（当前 fact 与 question 均无 `knowledge_id` 外键）。报表只产出**候选缺口**，明确标注「需人工复核，不作引擎行为依据」。脚本不修改任何数据，不进入 eval 管线 → **零 eval 影响**。

### 3.3 题库版本绑定（manifest 旁路，零运行时改动）

`data-asset-manifest.json` 顶层结构如下（`manifest_hash` 位于**顶层**，对数据内容取指纹，计算时排除 `generated_at`/`repo_root` 等非数据元数据，保证同数据跨次运行/跨机器稳定一致）：

```json
{
  "manifest_schema_version": "1.0",
  "generated_at": "<ISO-8601 UTC，仅元数据，不参与 hash>",
  "repo_root": "<绝对路径，仅元数据，不参与 hash>",
  "asset_count": 52,
  "unmapped_count": 0,
  "assets": { "<path>": { "sha256", "effective_version", "consumer", ... } },
  "binding": {
    "question_sets": {
      "src/CogniThink.TutorBrain.Api/data/pdf_questions.json": { "effective_version": "hash:<12hex>", "sha256": "...", "item_count": 545, "byte_size": 941690 },
      "src/CogniThink.TutorBrain.Api/data/tier2_questions.json": { "effective_version": "hash:<12hex>", "sha256": "...", "item_count": 109 }
    },
    "fact_library": {
      "data/highschool/general-choice-facts.json": { "declared_version": "1.0", "sha256": "...", "item_count": 967 },
      "... 其余 23 个 highschool + electrode-half-reactions": { ... }
    },
    "knowledge_base": {
      "data/knowledge/knowledge.json": { "declared_version": "0.2", "sha256": "...", "item_count": 50 }
    }
  },
  "manifest_hash": "<本 manifest 数据内容 sha256，用于 checkpoint 戳记 / 回归对比>"
}
```

- **回归可版本对比**：两次 eval checkpoint 各自带一份 manifest（或至少 manifest_hash），diff manifest 即可精确指出「哪个数据文件变了」（sha256 不同）。
- **零运行时风险**：manifest 不被任何运行时/eval 代码加载（生成器独立运行）；不改任何 `data/**` JSON 的结构或内容。eval 判分路径完全不感知 manifest 的存在 → diff 必为 0。
- **可选戳记（非必须，不进本 spec 实现）**：未来 eval runner 可把 `manifest_hash` 写进 checkpoint 元数据，使 checkpoint 自带数据指纹。本 spec 不做（避免动 eval 管线），仅在 Non-Goals 登记。

### 3.4 不改的边界（红线）

- 不改任何 `data/**` 或 `src/.../data/**` 下的 JSON 内容/结构（CQ-6：零数据改动）。
- 不改任何运行时加载器 / 求解器 / `CallEngineAsync` / 采纳门 `BuildGatewayAnswerAsync` / 判分 / eval 管线。
- 不把生成产物（manifest/inventory/gap_report）接入运行时或 eval。
- 不硬编码化学事实到 `.cs` / `.py`（脚本只做读取、哈希、匹配，不含化学真值）。
- 不删除/移动 5 个 `.bak`（CQ-3 用户决策前不动）。
- 不碰 `.gitignore`（仓库卫生属 P0-3，不在本 spec）。

## 4. 关键决策点（CQ，需用户批准）

| ID | 问题 | 选项 | 推荐 | 理由 |
|----|------|------|------|------|
| CQ-1 | 版本绑定实现位置 | a manifest 旁路记录指纹，**不改任何运行时 JSON**（推荐）/ b 给裸 list 包 dict 加 `version` 并改 loader | **a** | b 会改 `JsonSerializer.Deserialize<List<T>>` 顶层类型，触发反序列化风险与 eval 漂移；a 零运行时改动，指纹级绑定已满足「回归可版本对比」 |
| CQ-2 | 生成产物是否入库跟踪 | a manifest + inventory.md + gap_report.md 均 tracked（推荐）/ b 仅跟踪脚本，产物每次本地生成不入库 | **a** | 入库才能让 manifest 成为「数据指纹历史」，回归 diff 可追溯；产物随数据变更重生成（T6 自检强制同步） |
| CQ-3 | 5 个 stray `.bak`（untracked，不加载） | a 仅在清单登记，**不动**（推荐）/ b 移到 `_evalscratch/_archive/`（gitignored）/ c 删除 | **a** | 仓库自 P0 无 commit，这些可能是早期 pdf_questions 变体唯一留存；不动零风险，保留用户选择权 |
| CQ-4 | 缺口报表匹配口径 | a 知识点名 / textbookRef 关键词子串匹配，输出「候选缺口」并标注置信度（推荐）/ b 严格 knowledge_id 外键关联 | **a** | b 要求给所有 fact/question 加 `knowledge_id` 外键（大规模数据改动 + eval 风险），不在 P3-1；a 诚实标注候选性质，零数据改动 |
| CQ-5 | 工具与产物放置位置 | a 新建 `tools/data-assets/`（tracked，推荐）/ b 放 `_evalscratch/`（gitignored，会丢失） | **a** | maintained 工具 + 需跟踪的产物不能在 gitignored 目录 |
| CQ-6 | 运行时数据 | 零 `data/**` JSON 改动，零 eval 管线改动 | **已批准（默认红线）** | 审计证实 P3-1 全部价值可由旁路工具实现，无需碰运行时数据 |

## 5. 验收标准（AC）

- **AC-1**：`tools/data-assets/gen_inventory.py` 可从 repo root 一键运行，生成 `data-asset-manifest.json` + `data-asset-inventory.md`；manifest 覆盖全部 52 个 JSON（无 `UNMAPPED` consumer 告警），含 52 项 sha256 + effective_version + consumer + load_mechanism + validation_entry；inventory.md 含碰撞标注与二进制资产计数。
- **AC-2**：`tools/data-assets/gap_report.py` 可一键运行，生成 `gap_report.md`；覆盖 `knowledge.json` 全部 50 个 items（含 tier=Main 的主干子集），每条输出事实覆盖 / 题覆盖 / 缺口标签；明确标注「候选缺口·名称匹配·需人工复核」；**脚本不修改任何数据文件**（运行前后所有 `data/**` JSON 的 sha256 不变，T6 自检断言）。
- **AC-3**：全量 eval `after_p31`：757/757 failed=0，correct=635；`diff_ckpt after_p24 after_p31` GAINED/LOST/WORSEN/harm 全 0；`diff_ckpt base_p0 after_p31` LOST/WORSEN/harm=0（GAINED 维持 6 携带）。证明运行时数据/求解/判分零漂移。
- **AC-4**：版本绑定可演示：对 manifest 里任取一个事实文件（如 `general-choice-facts.json`）人为复制一份到临时位置改一个字节，重跑生成器 → 该文件 sha256/effective_version 变化、`manifest_hash` 变化，其余 51 项不变；证明 diff 可精确定位变更文件。（临时副本不入 `data/`，不影响 eval。）
- **AC-5**：台账 `data-defect-register.md` 新增 §R（P3-1 触及范围 + 清单/缺口/版本绑定产物 + 三文件名碰撞登记 + .bak 处置决策 + 零数据改动 attestation），插入于 §Q 末尾 `---` 与 `## 处理原则` 之间。
- **AC-6**：红线自检脚本 `p31_self_check.py`（`_evalscratch/verify/`）全 PASS：密钥扫描 0 命中 / 未改任何 `data/**` JSON（52 项 sha256 与 after_p24 时一致）/ 未改运行时代码路径 / 生成产物均在 `tools/data-assets/` 且不被运行时引用 / eval diff 全 0 / 不蒙数 / 表面积仅新增。
- **AC-7**：T7 fresh-context 只读子代理 review → PASS。

## 6. Non-Goals

- 不合并/归一化 `data/rules/rules.json`（Core RuleEngine）与 `data/highschool/*`（网关）两套规则体系——那是 **P3-2**，且依赖路线图 §6#1「两套栈收敛方向」用户决策。
- 不解决三文件名跨根碰撞的合并（P3-2 范畴）；本 spec 只在清单中如实标注。
- 不把 `manifest_hash` 写进 eval checkpoint（需动 eval 管线）——登记为未来可选增强，不在本 spec。
- 不给 fact/question 加 `knowledge_id` 外键（大规模数据改动 + eval 风险）——gap 报表用名称匹配，标注候选性质。
- 不做仓库卫生（`.gitignore` / 二进制资产整理 / `.bak` 清理）——属 P0-3 或用户决策。
- 不新增任何化学事实、不改任何 JSON 内容。

## 7. 风险与回退

- **最大风险 = 生成器/脚本意外修改运行时数据**：对策 = 所有脚本以**只读**方式打开 `data/**`（`open(..., 'r')`，绝不 `'w'`/`'a'`）；产物只写 `tools/data-assets/`；T6 自检对 52 个 JSON 逐文件 sha256 与基线对比，任一变化即 FAIL。
- **名称匹配漏报/误报**：gap 报表明确标注「候选·需人工复核」，不作引擎行为依据，不影响 precision=1（报表不进 eval）。
- **`asset_map.py` 消费者映射滞后**：生成器对无映射资产标 `UNMAPPED` 并非零退出，强制同步；新增数据文件时必须同步补映射（纪律由 T6 自检守护）。
- **回退**：若 AC-3 diff 非 0 → 必是意外改了运行时数据或代码路径，立即 `git checkout` 回退该改动（无 commit 则手工还原），复跑 eval 至 diff=0。
