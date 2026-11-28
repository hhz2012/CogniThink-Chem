# 引擎条件约束完善 + 模拟条件面板 实现计划

## 仓库研究结论

**trace 链路已完整支持条件，只是前端没使用：**
- 端点 `POST /api/chemistry/trace` 绑定 `ChemicalSystemInput`（`src/.../Api/Controllers/ChemistryController.cs:202-208`），其 `Conditions` 是嵌套的 `ChemicalConditions` 对象。
- `ChemicalSimulator.Simulate`（`src/.../Application/Chemistry/ChemicalSimulator.cs:27`）在 `RunStage` 中对每个候选反应调用 `CheckConditions` → `EvaluateCondition`（`ChemicalSimulator.cs:244-340`）做条件 gating；不满足时写入 `skipped` 字典，最终输出到 `SimulationTrace.SkippedReasons`（`ChemicalSimulatorContracts.cs:160`）。
- 旧适配器 `ChemicalSimulationEngine`（`ChemicalSimulationEngine.cs:133-167`）把扁平 `SimulationRequest` 映射到 `ChemicalConditions`，字段一一对应。

**`ChemicalConditions` 现有字段**（`ChemicalSimulatorContracts.cs:10-34`）：Aqueous(默认 true)、Heating、Ignition、Catalyst、Electricity、Light、Concentrated、Dilute、Air(默认 true)、Acidic、Alkaline、MaxSteps、ReturnConsidered。

**前端现状**（`reaction-demo/index.html`）：`fetchTrace` 只传 `conditions: { aqueous: true }`（L450），其余条件恒为 false → 所有需要加热/点燃/催化剂/光照/通电/浓/稀/酸/碱的反应被**静默跳过**；`renderCards`（L350）只渲染 `data.events`，完全不读 `skippedReasons`；另有一个前端临时过滤 `filterSlowProcesses`（L434-440）处理铁生锈类缓慢氧化。

**反应数据实测**（`data/knowledge/reactions/*.json`，共 261 条，`Conditions` 为原子 token 列表）：
- token 分布：`△`44、`点燃`25、`高温`10、`催化剂`8、`光照`6、`浓`5、`通电`5、`浓H2SO4`5、`水浴`4、`Ni`3、`Cu`2、`CO2过量`2、`酸性`2、`FeBr3`1、`冰晶石`1、`SO2过量`1、`加压`1、`稀`1、`水溶液`1、`MnO2`1、`高压`1、`O2不足`1、`放电`1、`170℃`1、`醇溶液`1。
- 无任何"速率/缓慢"字段；`RX-IN-NA-003 钠常温缓慢氧化`（条件为空）、`RX-PL-ELC-005 钢铁吸氧腐蚀`（条件为空）会被当作快速反应发生——即铁生锈问题的同类根因。

**由数据暴露的 5 个真实缺口：**
1. 温度不分档：`高温`(10) 与 `△`(44) 同映射到 `Heating` 一个布尔 → 开"加热"就能让需"高温"的反应发生。
2. 催化剂物质不全且不校验存在性：`IsCatalystSubstance`（`ChemicalSimulator.cs:343`）只硬编码 MnO2/V2O5/Ni/Pt，缺 Cu/FeBr3/浓H2SO4；且只看 `Catalyst==true`，不检查该催化剂是否真在体系中。
3. 无溶剂类型：`醇溶液`(1)/`水溶液`(1) 无对应建模，无法区分卤代烃水解 vs 消去。
4. 相对用量后缀不解析：`TryParseRelativeAmount`（`ChemicalSimulator.cs:346`）只识别前缀"过量X"，而数据用后缀 `CO2过量`/`SO2过量`/`O2不足` → 落入"未知条件→默认通过"，失去约束力。
5. 缓慢过程无标记：数据无速率字段，常温缓慢氧化/腐蚀会混入快速 trace。

---

## 改动范围（用户已选「完整方案」）

### 引擎层（C#，不改 `data/**.json`、不动判分文件）

**1. 温度分档** — `ChemicalConditions` 新增 `HighTemperature` 布尔；`EvaluateCondition` 中：
- `高温/煅烧/灼烧/℃`（含 `170℃`）→ 要求 `HighTemperature`；
- `△/加热/水浴` → 要求 `Heating`（保持现状）；
- `加热` 不再满足 `高温`。
- 同步在旧 DTO `SimulationRequest`（`ChemicalSimulationEngine.cs:23`）加 `HighTemperature`，并在 `MapToInput`（L137）映射，保持 `/simulate` 旧端点兼容。

**2. 催化剂存在性校验 + 扩充物质集** — `ChemicalSimulator.cs`：
- `IsCatalystSubstance` 扩充为 MnO2、V2O5、Ni、Pt、Cu、FeBr3、浓H2SO4（浓H2SO4 需结合"浓"条件）；
- 当条件 token 是**具体催化剂物质**时：要求 `conditions.Catalyst == true` **且**该物质存在于 `state`（非环境、>0）；否则返回 `"需要催化剂 X，但体系中未加入 X"`；
- 通用 `催化剂`/`酶` token 仍只要求 `Catalyst == true`（不强制具体物质，避免过度约束）。

**3. 溶剂类型** — `ChemicalConditions` 新增 `Alcoholic` 布尔（默认 false）；`EvaluateCondition`：
- `醇溶液` → 要求 `Alcoholic`；
- `水溶液` → 要求 `Aqueous`（已有）。
- 同步 `SimulationRequest` + `MapToInput`。

**4. 相对用量后缀解析** — `TryParseRelativeAmount` 同时支持前缀（`过量X`）和后缀（`X过量`/`X足量`/`X少量`/`X不足`）；`不足` 与 `少量` 同判定（targetRatio ≤ otherMin）。修复 `CO2过量`/`SO2过量`/`O2不足` 当前被放过的问题。

**5. 反应速率 / 缓慢过程过滤（引擎层根治铁生锈问题）**：
- `ChemicalConditions` 新增 `IncludeSlow` 布尔（默认 false）；
- 新增 `IsSlowProcess(Reaction)` 判定：反应 `Conditions` 为空（常温即可）**且** name/phenomenon/tags 命中 `缓慢|生锈|锈蚀|腐蚀|风化|吸氧腐蚀` → 标记为缓慢；
- 在 `RunStage` 候选筛选中（`CheckConditions` 之后或之前），若 `!conditions.IncludeSlow && IsSlowProcess(reaction)`，跳过并记 reason `"该反应为长期缓慢过程，默认不在快速模拟中展示（开启 includeSlow 可查看）"`；
- 判定严格限制为"无条件 + 关键词"，避免误杀带"点燃/加热"条件的快速氧化/燃烧。
- 同步 `SimulationRequest` + `MapToInput`（`ReturnConsidered` 旁映射 `IncludeSlow`）。

**6. 其余条件 token 补全** — `EvaluateCondition` 新增：`放电`→`Electricity`；`加压`/`高压`→新增 `Pressurized` 布尔（合成氨 `N2+3H2` 需高温高压催化剂）；`冰晶石` 暂作为条件提示（不强制存在，记 reason 说明），避免过度扩张。同步 DTO + 映射。

**7. trace 返回带方程式的 skipped 详情** — `SimulationTrace`（`ChemicalSimulatorContracts.cs:148`）新增 `SkippedDetails: List<SkippedReactionDetail>`（record：`ReactionId`、`Equation`、`Reason`）；在 `ChemicalSimulator.Simulate` 末尾用 `_kb.Reactions` 查 equation 组装。前端据此渲染灰显卡片。（现有 `SkippedReasons` 字典保留，不破坏兼容。）

### 前端层（`reaction-demo/index.html`）

**8. 条件开关面板** — 在 `inputSpecies` 行（L143）下方新增一排条件复选框：加热(△)、高温、点燃、催化剂、光照、通电、浓、稀、酸性、碱性、加压、醇溶液、水溶液(默认勾)、空气(默认勾)、含缓慢过程。新增 `collectConditions()` 读取复选框状态构造 `conditions` 对象。

**9. `fetchTrace` 传参** — `body.conditions` 改为 `collectConditions()` 的输出（含水溶液/空气默认 true），替换 L450 的硬编码 `{ aqueous: true }`。

**10. 展示未发生反应（灰显卡片 + 原因）** — `renderCards`（L350）在已发生卡片之后，追加渲染 `data.skippedDetails`：灰显折叠卡片，显示方程式 + 原因（如"需要高温，当前未提供高温条件"）。无 skipped 时不渲染该区。

**11. 移除前端临时过滤** — 删除 `isSlowOxidation`/`filterSlowProcesses`（L433-440）及 `fetchTrace` 中对它的调用（L455-456），改由引擎层 `IncludeSlow` 统一控制；`fetchTrace` 的空结果判断相应简化。

---

## 依赖顺序（自底向上，避免编译断裂）

1. `ChemicalSimulatorContracts.cs`：加 `ChemicalConditions` 新字段（HighTemperature/Alcoholic/IncludeSlow/Pressurized）+ `SkippedDetails` 记录类型。
2. `ChemicalSimulator.cs`：`EvaluateCondition` 温度分档/催化剂存在性/溶剂/放电加压；`TryParseRelativeAmount` 后缀；`IsSlowProcess` + `RunStage` 过滤；`Simulate` 末尾组装 `SkippedDetails`。
3. `ChemicalSimulationEngine.cs`：`SimulationRequest` 加同名字段 + `MapToInput` 映射（旧端点兼容）。
4. `reaction-demo/index.html`：条件面板 UI + `collectConditions` + `fetchTrace` 传参 + `renderCards` 渲染 skipped + 删前端过滤。
5. `dotnet build` 编译通过后浏览器验证。

## 依赖与注意事项

- 不修改 `data/**.json`、不动判分/测试基线文件：速率用引擎规则推断，条件 token 直接匹配现有数据，零数据迁移。
- 保持 `/simulate`（旧 `SimulationRequest`）与 `/trace`（`ChemicalSystemInput`）两条链路字段同步，避免行为分叉。
- 温度分档是**行为变更**：之前开 Heating 能触发的高温反应，现需 HighTemperature。需跑现有引擎测试确认无回归（见验证）。
- 催化剂存在性校验对**具体物质催化剂**才强制；通用"催化剂"不强制，避免用户没输入催化量时正常演示被阻断。

## 验证

1. `dotnet build src/CogniThink.TutorBrain.Api/CogniThink.TutorBrain.Api.csproj` 编译 0 错误。
2. 若存在 `ChemicalSimulator`/`ChemicalSimulationEngine` 单元测试，`dotnet test` 跑相关测试，确认温度分档等行为变更未破坏既有断言；若有断言依赖旧行为，评估后同步更新测试预期。
3. 浏览器场景验证（静态服 :8002 + API :5099，0 console 报错）：
   - `Cu:1, H2SO4(浓):1`：不勾加热→浓 H2SO4 与 Cu 反应被灰显跳过（原因含"需要加热"）；勾加热+浓→发生，生成 CuSO4/SO2。
   - `CaCO3:1`：勾加热但不勾高温→不分解（灰显"需要高温"）；勾高温→分解出 CO2。
   - `KClO3:1`：勾催化剂+加热但不加 MnO2→灰显"需要催化剂 MnO2，体系中未加入"；输入加 MnO2→分解出 O2。
   - `C2H5Br:1, NaOH:1`：勾水溶液→水解（C2H5OH）；勾醇溶液+加热→消去（乙烯）。
   - `Fe:1` + 默认水溶液+空气：默认不出现生锈阶段（被引擎过滤）；勾"含缓慢过程"→出现并标记为长期缓慢。
   - `Na:1` + 空气：默认过滤常温缓慢氧化（Na2O）；保留点燃条件下的燃烧。
   - 回归：默认 `CaCl2:1, Na2CO3:1, HCl:3` 仍为 3 阶段全快速，现象正确。
4. 灰显 skipped 卡片原因文本准确、无乱码；两视口不溢出。

## 风险与处理

- **温度分档回归**：现有测试/基准可能依赖"开 Heating 即触发高温反应"。→ 先跑测试定位；若断言确实依赖旧行为，该断言本身不严谨，同步更新预期并在提交说明中标注。
- **催化剂存在性过严**：用户可能只勾"催化剂"而不输入 MnO2。→ 仅对条件中**写明具体物质**的反应强制存在；通用"催化剂"token 不强制，给提示而非阻断。
- **缓慢判定误杀**：name 含"氧化"的快速反应被误过滤。→ 判定必须同时满足「`Conditions` 为空（常温）」+ 关键词；带点燃/加热条件的燃烧/氧化不进入该分支，天然安全。
- **前端复选框过多挤占空间**：条件开关约 13 个，手机宽度可能换行。→ 用 flex-wrap 紧凑 chip 样式，允许换行，不溢出主容器。
