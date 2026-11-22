# P3-1 数据资产归一与缺口报表自动化 — 独立审查报告（fresh-context / 只读）

> 审查员角色：fresh-context 只读代码审查员。除运行幂等生成脚本所重写的既有产物 `tools/data-assets/{data-asset-manifest.json,data-asset-inventory.md,gap_report.md}`（内容与原产物逐字节一致、末次 sha256 复核确认）外，未修改/创建/删除任何文件；未 git commit。
> 审查范围：`.trae/specs/data-asset-inventory-gap-report/` spec.md / tasks.md 的 AC-1..AC-7 与红线（precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON / 零运行时数据改动）。
> 审查方法：不采信实施者自证，独立复跑全部验证脚本，并独立直读复核关键数字（sha256 基线逐文件比对、grep 产物引用、manifest_hash 幂等与期望值、eval summary 直读、亲自跑两次 diff_ckpt、build 实跑、三脚本只读性通读）。

## 结论：PASS

所有红线维度均独立复核通过，无未关闭的阻断问题。唯一未完成项为全量 `dotnet test` 计数确认（运行挂起，按任务许可跳过；build 强制门禁已过，运行时正确性由 eval + 自检 + diff 独立证明）。

审查中发现 3 个 Minor（非阻断）问题，均在复审后修复或确认无需修复（见 §六 复审后修复 attestation），修复后 `p31_self_check.py` 复跑 **18/18 PASS**、manifest_hash 稳定、52 JSON 零改动。

---

## 一、各验证步骤实际命令输出摘要（cwd 均为 repo root `D:\github\CogniThink-Chem`）

| # | 命令 | 实际输出摘要 | 结果 |
|---|------|--------------|------|
| 1 | `python tools\data-assets\gen_inventory.py` | `assets=52 unmapped=0 png=282 svg=129`；`wrote ...manifest.json and ...inventory.md`；`EXITCODE=0` | ✅ 退出码 0；manifest 52 项、UNMAPPED=0 |
| 2 | `python tools\data-assets\gap_report.py` | `kps=50 main=44 no_fact=4 no_question=0 neither=34`；`wrote ...gap_report.md`；`EXITCODE=0` | ✅ 退出码 0；覆盖 50 知识点（Main=44） |
| 3 | `python _evalscratch\verify\p31_version_binding_demo.py` | `T3 VERSION-BINDING DEMO: PASS`；mutated=`data/knowledge/electrode-half-reactions.json`；`real target sha unchanged`；exactly 1 asset 变化、其余 51 identical；`real manifest untouched on disk`；`EXITCODE=0` | ✅ PASS（仅临时副本改 1 字节，真实数据/manifest 未触碰） |
| 4 | `python _evalscratch\verify\p31_self_check.py` | 18 行 `[PASS]` + `P3-1 SELF-CHECK: ALL PASS`；`EXITCODE=0` | ✅ 18/18 PASS |
| 5 | 独立 sha256 基线比对（自写 python，不依赖自检） | `total_baseline_entries=52 mismatches=0 ALL_MATCH`；末次复核 `entries=52 mismatches=0 ALL_MATCH` | ✅ 52/52 逐文件一致 |
| 6 | 独立 grep 产物引用（`*.cs`） | `No matches found`（tokens: `data-asset-manifest` / `data-asset-inventory` / `gap_report` / `gen_inventory` / `asset_map` / `tools/data-assets`） | ✅ 0 命中 |
| 7 | 独立幂等 + 期望值校验（自跑 gen 两次） | `run1=fc183553…70e5` `run2=fc183553…70e5` `IDEMPOTENT=True` `EXPECTED_MATCH=True` | ✅ 幂等且等于期望值 |
| 8 | 直读 `_evalscratch\checkpoint_after_p31_summary.txt` | `total=757` `gatewayCorrect=635` `precision=1` `harmCount=0` `bothWrong=122` | ✅ 与期望值一致 |
| 9 | `python _evalscratch\verify\diff_ckpt.py after_p24 after_p31` | `GAINED (0)` `LOST (0)` `WORSEN (0)` `harm_old=0 harm_new=0` `RED-LINE: harm=0 & lost=0 & worsen=0 -> PASS`；`EXITCODE=0` | ✅ RED-LINE PASS |
| 10 | `python _evalscratch\verify\diff_ckpt.py base_p0 after_p31` | `GAINED (6): [pdf-415, pdf-530, pdf-535, pdf-542, pdf-543, trap-limit-2]` `LOST (0)` `WORSEN (0)` `harm_old=0 harm_new=0` `RED-LINE … -> PASS`；`EXITCODE=0` | ✅ RED-LINE PASS（GAINED=6 为历史携带，无 P3-1 新增） |
| 11 | `dotnet build CogniThink.Chem.sln -c Release --nologo -v q` | `已成功生成。0 个警告 0 个错误`；`已用时间 00:00:04.57` | ✅ 0 error（强制门禁） |
| 12 | `dotnet test CogniThink.Chem.sln -c Release --no-build --nologo -v q` | 后台运行约 18 分钟，10 秒采样 CPU delta=0（无进展），判定挂起，已 StopCommand 停止；未取得 2221 passed 计数 | ⚠️ 未确认（按任务许可跳过，详见限制披露） |

自检 18 项明细（脚本输出原文）：no secrets 0 hits / 52 JSON unchanged all 52 identical / gatewayCorrect=635 / harmCount=0 / precision=1 / diff after_p24→after_p31 GAINED=0·LOST=0·WORSEN=0·harm_new=0·RED-LINE PASS / diff base_p0→after_p31 LOST=0·WORSEN=0·harm_new=0·RED-LINE PASS / GAINED=6 carried / artifacts 0 hits / generator idempotent fc1835536b12 vs fc1835536b12 / total=757。

---

## 二、代码审查发现（逐脚本只读性 + 未触 data/** + 产物隔离）

通读 `tools/data-assets/` 三脚本（`gen_inventory.py` 247 行 / `gap_report.py` 295 行 / `asset_map.py` 97 行）：

1. **`gen_inventory.py` — 对 data/** 只读，产物只写 tools/data-assets/**
   - 读：`sha256_file` 用 `open(p, "rb")`；JSON 解析 `open(fp, "r", encoding="utf-8")`，均只读。
   - 写：仅写 `data-asset-manifest.json`、`data-asset-inventory.md`，均在 `OUT_DIR=tools/data-assets/`。
   - `manifest_hash` 排除 `generated_at`/`repo_root`，跨次/跨机稳定——实测幂等成立且等于给定期望值。
   - 无 `shutil`/`os.remove`/`os.unlink`；`--repo-root` 仅演示指向临时根，默认不改真实根。
   - **（复审后）** `n_nover` 改为从 `assets.values()` 计算 `declared_version is None` 的数量，不再硬编码（修复 M-1，见 §六）。

2. **`gap_report.py` — 数据只读，唯一输出 gap_report.md**
   - `load(rel)` 统一只读加载 knowledge/highschool/pdf_questions/benchmark。
   - 写：仅 `tools/data-assets/gap_report.md`。
   - 匹配仅通用字符串切分（`_NAME_DELIM` 为通用标点，非化学真值）；缺口标注 CANDIDATE 并含诚实免责声明。

3. **`asset_map.py` — 纯元数据映射，零 I/O**：仅 `RULES` + `lookup()`，无文件读写、无化学真值；覆盖 spec §2 的加载器，无映射 → 生成器标 UNMAPPED 并非零退出。实测 52 项全覆盖、0 UNMAPPED。

4. **写入模式 grep 独立复核**：在 `tools/data-assets/*.py` grep 写模式 → 仅命中写 `MANIFEST_PATH`/`INVENTORY_PATH`/`OUT_PATH`（均在 tools/data-assets/），**无任何对 data/** 的写入**。

5. **产物隔离**：独立 grep `src/**/*.cs` + `tests/**/*.cs` 对 6 个产物 token → **0 命中**（不依赖自检）。

6. **未改运行时方法体**：仓库跨多 spec 累积脏树，无法用单一 git diff 干净隔离 P3-1；但 `diff_ckpt after_p24→after_p31` 逐 case GAINED/LOST/WORSEN 全 0 → 运行时输出与 after_p24 逐 case 一致，行为零漂移，等价于方法体无可观测改动。结合产物 0 引用 + 三脚本只写 tools/，证据充分。

---

## 三、裁决维度判定表（任一 FAIL 即整体 FAIL）

| # | 维度 | 判定 | 独立证据 |
|---|------|------|----------|
| 1 | precision=1 | ✅ PASS | 直读 summary `precision=1`；自检同值。 |
| 2 | harm=0 | ✅ PASS | 直读 summary `harmCount=0`；两次 diff `harm_new=0`。 |
| 3 | LOST=0 | ✅ PASS | 两次 diff 均 `LOST (0)`。 |
| 4 | WORSEN=0 | ✅ PASS | 两次 diff 均 `WORSEN (0)`。 |
| 5 | 不蒙数 | ✅ PASS | diff after_p24→after_p31 GAINED=LOST=WORSEN=0 → 无答案翻转；`total=757` 全量无 skip；base_p0→after_p31 GAINED=6 与历史携带一致、无新增。 |
| 6 | 零运行时数据改动 | ✅ PASS | 独立 sha256：52/52 与 `p31_data_baseline.json` 逐文件一致（mismatches=0），含末次复核；三脚本对 data/** 仅 r/rb 打开。 |
| 7 | 产物隔离 | ✅ PASS | 独立 grep `*.cs` 对 6 产物 token = 0 命中。 |
| 8 | 清单完整 52 项无 UNMAPPED | ✅ PASS | manifest `asset_count=52` / `unmapped_count=0`；gen 输出 `unmapped=0`。 |
| 9 | 生成器幂等 | ✅ PASS | 自跑两次 manifest_hash 相同；且等于期望值 `fc1835536b12b91c06bcb2c81eeca49066a2d31f3e09c9cf1d8b620c7c3970e5`。 |
| 10 | 表面积仅加性 | ✅ PASS | P3-1 仅新增 `tools/data-assets/*`、`_evalscratch/verify/p31_*`、台账 §R 插入；无删除、无运行时 .cs 改动、无 data/** 改动；产物未接入运行时/eval。 |
| 11 | 事实只存 JSON / 不硬编码化学真值 | ✅ PASS | 三脚本通读无逐题化学答案分支；`asset_map` 仅工程元数据，`gen_inventory` 纯读取/哈希/查表，`gap_report` 仅通用字符串切分。 |
| 12 | 版本绑定可精确定位变更文件 | ✅ PASS | T3 demo：临时副本改 1 字节 → 仅该文件 sha256/effective_version/manifest_hash 变化，其余 51 项不变，真实数据/manifest 未触碰。 |

全部 12 维 PASS → 无 FAIL。

---

## 四、Issue 列表

### Critical
无。

### Major
无。

### Minor（非阻断；M-1 / M-2 已在复审后修复，M-3 无需修复）

| ID | 描述 | 影响 | 状态 |
|----|------|------|------|
| M-1 | `gen_inventory.py` 把无 version 资产数**硬编码 "12"** 写入 inventory.md，但 manifest 实际为 **11** 项 declared null（独立实跑 52 JSON 确认真值 = 11）。 | 人读报表一处计数不准；不影响 manifest 机读数据与任何红线。 | ✅ **已修复**：改为 `n_nover = sum(... declared_version is None)` 计算值；重生成后 inventory.md 显示「11 assets」。manifest_hash 不变（仅改 md 渲染）。见 §六。 |
| M-2 | spec 文档两处轻微笔误：① §2.3 称「12 个无 version」但实际列出 11 项；② §3.2 括注「tier=='Main' 的 items（50 个）」，实际 50 items 中 Main=44（gap_report 实测 main=44，独立验证 tiers={'Main':44,'Extended':6}）。 | 文档表述不精确；不影响验收。 | ✅ **已修复**：§2.3 改「11 个」；§3.2 改「共 50 个 items，其中 Main 44 个 / Extended 6 个」。见 §六。 |
| M-3 | 二进制计数：生成器报 SVG=129（on-disk），spec §2.5 记 128 tracked；差异来自 1 个 untracked `vector_svg/pdf-260_p093_n03_0_curve.svg`（非 P3-1 引入）。 | 已诚实披露；非缺陷。 | ✅ **无需修复**：inventory.md 表头已标注「on-disk, informational; includes any untracked files」。 |

---

## 五、最终总体裁决

**PASS。**

全部 12 个裁决维度均经独立复核通过，无任何 FAIL；3 个 Minor 均为非阻断的文档/cosmetic 问题，其中 M-1 / M-2 已在复审后修复并复跑自检 18/18 PASS，M-3 无需修复。唯一未完成的验证项是全量 `dotnet test` 的 2221 passed 计数（运行挂起，按任务许可跳过）；但强制门禁 `dotnet build` 已 0 error 通过，运行时正确性由 eval checkpoint（635/757、precision=1、harm=0）+ 自检 18/18 + 两次 diff_ckpt RED-LINE PASS + 独立 sha256 零改动 共同独立证明，证据链充分，不影响 PASS 裁决。

---

## 六、复审后修复 attestation（主代理执行，非审查员修改）

审查报告落盘前，主代理针对 M-1 / M-2 执行了修复并复跑验证：

| 修复项 | 改动 | 复跑证据 |
|--------|------|----------|
| M-1 | `tools/data-assets/gen_inventory.py`：新增 `n_nover = sum(1 for a in assets.values() if a.get("declared_version") is None)`，inventory.md 文本由硬编码「12」改为 `f"{n_nover}"`。 | 重生成后 inventory.md L12 =「11 assets lack a top-level `version` field」；`p31_self_check.py` 复跑 **18/18 PASS**；manifest_hash 仍为 `fc1835536b12…`（仅 md 渲染变，manifest 内容不变）；52 JSON sha256 与基线全一致。 |
| M-2 | `spec.md` §2.3：「**12 个**无 `version` 字段」→「**11 个**」；§3.2：「tier=='Main' 的 items（50 个…）」→「共 50 个 items，其中 Main 44 个 / Extended 6 个」。 | 独立验证：`python -c` 实跑 52 JSON 顶层 version 缺失计数 = 11（与列出文件一致）；knowledge.json tiers = {'Main':44,'Extended':6}，total=50。 |
| M-3 | 无改动。 | inventory.md 表头已含「includes any untracked files」披露。 |

**修复不触及任何红线**：仅改工具脚本的 md 渲染常量与 spec 文档文字；未改 `data/**` JSON、未改运行时 .cs、未改 eval 管线。复跑 `p31_self_check.py` 全 18 项 PASS（含「52 JSON 不变」「产物 0 引用」「两次 diff RED-LINE PASS」「generator 幂等」）。

---

## 审查限制披露（诚实口径）

1. **全量单元测试未取得计数**：`dotnet test --no-build` 后台运行约 18 分钟，10 秒采样 CPU delta=0 判定挂起，已 StopCommand 停止，未取得 2221 passed 实跑确认。按 delegation「若耗时过长可跳过全量 test，但 build 必须跑」处理——build 已过。正确性不依赖该计数：eval after_p31（权威 gate）已 635 correct / precision=1 / harm=0，且 diff 全 0。
2. **git diff 无法干净隔离 P3-1**：仓库自 P0 以来跨多 spec 累积脏树，无法用单一 commit diff 圈定 P3-1 改动面；「未改运行时方法体」由 `diff_ckpt after_p24→after_p31` 逐 case 行为零漂移 + 产物 0 引用 + 三脚本只写 tools/ 三重证据间接证明，非逐行 git 比对。
3. **基线真实性**：`p31_data_baseline.json` 为 after_p24 时由实施者记录的基线；本次独立复算了当前 52 文件 sha256 并与基线逐文件比对（全一致），证明 P3-1 运行期间零数据改动；基线本身是否真实代表 after_p24 依赖实施者记录（审查未发现反证，且 diff 行为零漂移与之吻合）。
4. **严格只读**：未修改/创建/删除任何源码、数据、测试文件；未 git commit。运行生成器仅重写已存在的幂等产物，末次 sha256 复核确认 data/** 仍零改动。M-1/M-2 修复由主代理在审查报告落盘前执行（见 §六），非审查员操作。

---

**Gaps & Unverified**：全量 `dotnet test` 的 2221 passed 计数未确认（运行挂起，按任务许可跳过）；此为 delegation 明确允许的例外，不构成红线 FAIL。3 个 Minor 均已处理（M-1/M-2 修复、M-3 无需修复），无遗留阻断项。
