# pdf-322 分子资产 curate 实施计划

> 阶段：Plan Mode 研究完成，待审批。审批前**不修改** `pdf_questions.json` 或任何生产代码。

## 1. Repository Research（研究结论）

### 1.1 pdf-322 现状（基线 = `_evalscratch/checkpoint_t2alias.jsonl`）
- `gatewayCorrect=false`，`gatewayOk=null`，`taskType="unknown"`，`imageAssets=[]`，`structuredAssetKind=null`，`gatewayResultProvenance=null`。→ **baseFalse，未被分子路由命中**。
- 银行字段（[pdf_questions.json:12428-12436](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json#L12428-L12436)）：
  - `question` = `"(2024·浙江卷·★★)中官能团名称为______。"`（题面碎片，仅保留子问(4)的题干）
  - `answer` = `"(1)醚键、酰胺基,(2)碳碳双键、酮羰基(或羰基)、碳溴键(或溴原子),(3)硝基、酯基,(4)硝基、酮羰基(或羰基)、氨基、碳氯键(或氯原子),(5)2,4,6-三溴苯酚,(6)羟基(或酚羟基)邻苯二酚"`
  - `type="fill"`，`src_page=112`，`ans_page=108`，`num=4`。

### 1.2 图源：结构真实存在，可恢复
- 复合题 4 在 [page_renders/page_112.png](file:///d:/github/CogniThink-Chem/pdfs/_extract/page_renders/page_112.png)（书页脚注 108，第 12 章有机化学）含子问 (1)–(8) 的真实键线式。银行把它拆为：**pdf-322 = 子问(1)–(6)**，pdf-323 = 子问(7)，pdf-324 = 子问(8)。
- 6 个结构与 `answer` 的 (1)–(6) 逐一吻合（见 §3）。→ 早年 `data-defect-register.md` 把 pdf-322 归为「缺结构图 / 范围外」的**理由已失效**（图源现可直接视觉恢复）。

### 1.3 路由语义（决定 curate 能否生效）
- `TryRouteMolecule`（[LlmGatewayService.Molecule.cs:23](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs#L23)）：`imageAssets?.FirstOrDefault(a => a.Type == "molecule")` → **只取第一个 molecule 资产**。
- 多部分分发（[LlmGatewayService.MultiPartDispatch.cs:38](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs#L38)）调子问时传 `imageAssets:null`；但其激活条件是整题 `Ok != true` 且 `MultiPartUtility.IsMultiPart(rawText)`。pdf-322 的 `question` 字段**无 (1)–(6) 标记**（标记只在 answer 里）→ `IsMultiPart=false` → **不走分发**，整题走 `CallEngineAsync(req, imageAssets)`（[LlmGatewayService.cs:106](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L106)）。
- 判分（[GatewayVsLlmEvaluationService.cs:532 MatchMolecule](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L532)）：引擎识别的**任一**官能团名 / 命名是 `c.Answer` 子串即命中（`IsStructuredCorrect` 经 `NormForStructured` 规范化后比对）。

### 1.4 词表已填平（早年 NO-GO 理由消除）
- 共享词表 [FunctionalGroupVocabulary.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/FunctionalGroupVocabulary.cs) 已覆盖 pdf-322 所需全部盲区：`酰胺基/肽键`、芳基 `醚键`、伯仲叔 `氨基`（排除酰胺/硝基 N）、`羰基` alias、`碳溴键/碳氯键/卤原子` 等卤素 alias。`MoleculeAnalyzer`（[L112-117](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs#L112-L117)）遍历该词表输出所有命中名。
- 命名器 [MoleculeNamer.NamePhenol](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeNamer.cs#L21) 覆盖单一取代基类型的取代酚 → (5) 输出 `2,4,6-三溴苯酚`。
- 这些词表/alias 改动目前在工作区**未提交**（见 §5 依赖项）。

### 1.5 dry-fire 证据（真实管线，只读，临时测试已删除）
用 `RuntimeHelpers.GetUninitializedObject` 构造网关，逐结构作为 `asset[0]`（与 `FirstOrDefault` 同口径）调 `CallEngineAsync`，再用 `IsStructuredCorrect`（eval 同口径）对 pdf-322 answer 判分：

| 子问 | 名称 | SMILES | 公式 | 引擎识别 FG / 命名 | 命中 |
|---|---|---|---|---|---|
| (1) | 曲美托嗪（内酰胺） | `O=C1N(Cc2cc(OC)c(OC)c(OC)c2)CCOC1` | C14H19NO5 | 苯环、醚键、**酰胺基**、肽键 | ✅ |
| (2) | 溴代环己烯酮 | `O=C1C(Br)=CCCC1` | C6H7BrO | 碳碳双键、酮羰基、羰基、卤原子、**溴原子、碳溴键** | ✅ |
| (3) | 北京卷（硝/酯） | `CC(=O)Oc1cc(Cl)c(F)c([N+](=O)[O-])c1` | C8H5ClFNO4 | 苯环、**酯基、硝基**、卤原子、氯/氟… | ✅ |
| (4) | 浙江卷（硝/酮羰/氨/氯） | `Nc1ccc(C(=O)c2ccc(Cl)cc2)cc1[N+](=O)[O-]` | C13H9ClN2O3 | 苯环、酮羰基、羰基、**氨基、硝基**、碳氯键… | ✅ |
| (5) | 2,4,6-三溴苯酚 | `Oc1c(Br)cc(Br)cc1Br` | C6H3Br3O | name=**2,4,6-三溴苯酚**；苯环、羟基、溴原子 | ✅ |
| (6) | 邻苯二酚 | `Oc1ccccc1O` | C6H6O2 | 苯环、**羟基** | ✅ |

- 6/6 全部 `Ok=True`、`AssetKind=molecule`、`ResultProvenance=molecule`、`IsStructuredCorrect=True`。
- 模拟「挂 6 个，asset[0]=(1)」：`structuredHit=True`，FG=`苯环、醚键、酰胺基、肽键` → 确认 `FirstOrDefault` 只分析第一个，且命中正当。
- (1) 的两个候选里，**内酰胺版**识别出 `酰胺基`（与子答案 (1)「醚键、酰胺基」一致）；碳酰胺版（`O=C1OCCN...`）只出 `酯基/氨基`、不出 `酰胺基` → 采用内酰胺版（与源卷所绘的 C(=O)–N 内酰胺环一致）。
- rdkit 预检脚本：[_evalscratch/pdf322_validate.py](file:///d:/github/CogniThink-Chem/_evalscratch/pdf322_validate.py)（gitignore 内只读证据）。

## 2. Files and Modules
- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json`：在 pdf-322 条目内追加 `imageAssets`（6 个 molecule 资产）。**不改** question/answer/answerKeywords/note/type（不动银行真值）。保持现有 2 空格缩进。
- 不碰 `images/` 原始扫描图、不改判分逻辑、不新增生产代码。

## 3. Implementation Steps（依赖顺序）
1. **Edit `pdf_questions.json`**：在 pdf-322 的 `"type": "fill"` 之后、`"answer"` 之前插入 `imageAssets` 数组，6 个资产按子问 (1)–(6) 顺序，每个为 `{"type":"molecule","payload":{"name":"<子问名>","smiles":"<上表SMILES>","valid":true}}`。
2. **JSON 校验**：`python -c "import json; json.load(open(...))"` 通过（或脚本校验）。
3. **全库 eval**：`powershell -ExecutionPolicy Bypass -File _evalscratch\run_eval_resume.ps1 -Tag t2curate3 -Env Production`（后台，LLM-off 纯引擎）。
4. **diff**：`powershell -ExecutionPolicy Bypass -File _evalscratch\diff_eval.ps1 -OldTag t2alias -NewTag t2curate3` → 预期 **GAIN=1（仅 pdf-322）、LOST=0、harm=0**。
5. **mp27**：`python _evalscratch\verify_mp27.py t2alias t2curate3` → 预期 **27/27**。
6. **全量测试**：`dotnet test CogniThink.Chem.sln --nologo -v q` → 全绿（含已有的词表/alias 测试 2652）。
7. **汇报**，留工作区不主动 commit。

## 4. 挂载策略与语义护栏
- **挂全部 6 个真实结构**（数据忠实：pdf-322 的复合题确含这 6 个分子），顺序 (1)–(6) 与源卷一致。
- 当前路由只分析 `asset[0]`=(1)，其识别 FG `醚键、酰胺基` 是子问(1) 的真实答案 → 命中**化学正当**（真实结构→真值 FG→确实在答案中），非巧合子串。
- (5) 的 `name=2,4,6-三溴苯酚` 是最强兜底（名称维度精确命中）；即使 (1) 转录存疑，只要 (5) 在前或词表正常，仍可命中——但本计划按源卷顺序挂 (1) 为首。
- **已知语义局限（如实说明，不视为 harm）**：eval 对该复合题是 OR 子串判分，一个子问命中即整题翻正（与已 curate 的 pdf-323/521/523 同模式）。子问 (2)–(6) 的结构已入库但当前 `FirstOrDefault` 路由不消费；要逐子问全解需改路由（属求解器增强，**超本计划范围**，另立计划并做零假阳性扫描）。precision 不受影响：凡采纳必经答案键命中。

## 5. Dependencies and Considerations
- **前置依赖（工作区未提交）**：词表扩展 + alias（`FunctionalGroupVocabulary.cs` 新建、`MoleculeAnalyzer.cs`/`MoleculeValidator.cs` 改、测试 2652 全绿）是 pdf-322 命中的必要前提——dry-fire 已在含这些改动的工作区验证通过。**实施 pdf-322 前需与用户确认**：是否先 commit 词表/alias 这一批，再做 pdf-322 curate（避免两类改动混在一个 diff）。
- 不臆造：6 个 SMILES 均从 `page_112.png` 真实键线式恢复，并经 rdkit 解析 + 引擎 FG 与对应子答案双向吻合；恢复不了的结构宁可不挂（本批 6 个全部可恢复）。
- 不改路由（`FirstOrDefault` 是求解器行为，改它属超范围）。

## 6. Validation（验收）
- `pdf_questions.json` 解析通过，pdf-322 的 `imageAssets` 为 6 个有效 molecule 资产。
- 全库 eval：GAIN=1、LOST=0、harm=0；仅 pdf-322 由 False→True，其余 0 变化。
- mp27：27/27 不退化。
- `dotnet test` 全绿。

## 7. Risks
- **结构转录准确性**：6 个结构已 rdkit 解析 + dry-fire FG 吻合；(1) 已用内酰胺版（出 `酰胺基`）排除碳酰胺版。若实施后 eval 未翻正，优先回查 `asset[0]` 的 SMILES 与 `FirstOrDefault` 顺序。
- **多子问 OR 判分**：整题因子问(1)命中即翻正，是 eval 现有设计（同 pdf-323 模式）；已在 §4 说明，不构成 harm。
- **词表/alias 未提交导致基线漂移**：实施前确认提交节奏（§5），避免 diff 混合。
- **红线**：全程 precision=1、harm=0、LOST=0、mp27=27/27；不主动 commit/push。
