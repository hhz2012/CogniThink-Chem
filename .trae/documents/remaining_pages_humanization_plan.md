# 剩余页面人性化收尾 实施计划（P0/P1/P2 一致性补齐）

> 背景：P0（emoji→线性 SVG、首页分段控件）、P1（统一按压反馈、折叠迁移）、P2（表单聚焦态、共享 Loading/Empty）已完成并推送。本轮把**此前未深度处理的页面**补齐到同一标准，完成全站一致性收尾。**本轮不提交**，改完验证后等用户确认。

## 仓库研究结论（已逐文件核实）

### 1. P1 按压反馈缺口（可点击元素无任何 `:active`/pressable/transition 反馈）

| 页面 | 元素 | 现状 | 处理方式 |
|---|---|---|---|
| mine | `.statCard`（错题本卡片，[index.tsx L64](file:///d:/github/CogniThink-Chem/miniapp/src/pages/mine/index.tsx#L64)） | 无 `:active`、无 transition | `@include pressable` |
| atlas | `.catTab`（分类 Tab，[index.tsx L86](file:///d:/github/CogniThink-Chem/miniapp/src/pages/atlas/index.tsx#L86)） | 有 `transition: all`（[scss L101](file:///d:/github/CogniThink-Chem/miniapp/src/pages/atlas/index.module.scss#L101)），无 `:active` | 手动追加 `&:active`（不覆盖原过渡） |
| atlas | `.searchClear`（清空 ✕，[index.tsx L71](file:///d:/github/CogniThink-Chem/miniapp/src/pages/atlas/index.tsx#L71)） | 无 `:active` | 手动 `&:active { opacity: 0.6 }` |
| challenge | `.kpBar`（知识点入口，[tsx L477](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L477)） | 无 transition、无 `:active`（[scss L348](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.module.scss#L348)） | `@include pressable` |
| challenge | `.conservationHead`（守恒校验折叠头，[tsx L820](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L820)） | 无 transition、无 `:active`（[scss L597](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.module.scss#L597)） | `@include pressable` |
| challenge | `.diagRetry`（再试一次按钮，[tsx L789](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L789)） | 无 transition、无 `:active`（[scss L748](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.module.scss#L748)） | `@include pressable` |
| challenge | `.stage2Toggle`（选做推导折叠，[tsx L651](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L651)） | 无 transition、无 `:active`（[scss L909](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.module.scss#L909)） | `@include pressable` |
| challenge | `.dagPreviewToggle`（思路递进图折叠，[tsx L548](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L548)） | 无 transition、无 `:active`（[scss L929](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.module.scss#L929)） | `@include pressable` |

已覆盖、**不动**：challenge 的 `.homeBack`/`.readAloud`/`.hintBtn`/`.option`/`.submitBtn`/`.diagChoice`，atlas 的 `.kpCard`，practice 的 `.homeBack`，mine 的 `.resetBtn`。

### 2. P0 装饰性 emoji 残留（应统一为线性 SVG）

| 位置 | 残留 emoji | 处理 |
|---|---|---|
| solve 小节标题（[tsx L150/L159/L191](file:///d:/github/CogniThink-Chem/miniapp/src/pages/solve/index.tsx#L150)） | 💬 讲解 / 🔗 推理过程 / 🌱 微观解释 | 💬→新增 `message` 图标；🔗→已有 `link`；🌱→已有 `atom`（微观贴切） |
| reaction 提示（[tsx L213](file:///d:/github/CogniThink-Chem/miniapp/src/pages/reaction/index.tsx#L213)） | 💡 先自己想一想… | 改用 `lightbulb`（已有）内联图标 |
| bank 空态（[tsx L66](file:///d:/github/CogniThink-Chem/miniapp/src/pages/bank/index.tsx#L66)） | 🎉 | 见 Atom C（改用共享 Empty，顺带消除 emoji） |

**明确不在范围**：功能符号 `✓ ✗ − + → › ▲ ▼` 是状态标记/箭头，非装饰图标，保持不动；tutor `🎉 关卡通过`、ScaffoldPractice 的 `🎉/💡/📖/🔒` 属庆祝/提示内嵌文案，转换需新增多个图标且价值较低，**留待后续或用户确认**，本轮不做。

### 3. P2 空态统一缺口

- bank 已有空态但用本地 emoji 图标（🎉）+ 本地样式，未复用 P2 新建的共享 `<Empty>` 组件。改用 `<Empty>` 统一视觉（icon 用 `trophy`），并保留原有「去做题」按钮。

## 待改文件模块

- `miniapp/src/types/chem.ts` — `ModelIconName` 联合类型新增 `'message'`
- `miniapp/src/components/SolvePanels/ModelIcon.tsx` — `ICONS` 新增 `message`（Feather 线性聊天气泡路径）
- `miniapp/src/pages/solve/index.tsx` — 3 个小节标题 emoji → `<ModelIcon>` + 文本
- `miniapp/src/pages/solve/index.module.scss` — 小节标题图标+文本的 inline 布局（若需）
- `miniapp/src/pages/reaction/index.tsx` — 💡 → `lightbulb` 图标
- `miniapp/src/pages/bank/index.tsx` — 空态改用共享 `<Empty>`，保留去做题按钮
- `miniapp/src/pages/mine/index.module.scss` — `.statCard` 加 `@include pressable`
- `miniapp/src/pages/atlas/index.module.scss` — `.catTab` 手动加 `:active`、`.searchClear` 加 `:active`
- `miniapp/src/pages/challenge/index.module.scss` — 5 个元素加 `@include pressable`

## 实施步骤（依赖序）

1. **Atom B 前置**：`chem.ts` 加 `'message'` 类型；`ModelIcon.tsx` 加 `message` 的 SVG 路径。（类型+图标必须同步，tsc 会校验。）
2. **Atom B**：solve 3 个小节标题替换为 `ModelIcon` + 文本；reaction 💡 替换为 `lightbulb` 图标。
3. **Atom C**：bank 空态用 `<Empty>` 替换本地 emoji 空态，保留 goBtn。
4. **Atom A**：按表格给 mine/atlas/challenge 的 8 个元素补按压反馈（有 `transition: all` 的 atlas `.catTab` 只手动追加 `&:active`，其余用 `@include pressable`）。
5. `cd miniapp; npx.cmd tsc --noEmit` → 必须 0 错误。
6. 浏览器硬刷新逐页验证（见下），**不提交**。

## 依赖与注意事项

- 纯展示/样式变更，无逻辑/接口改动，不碰引擎红线。
- `@include pressable` 只用于**无 `transition: all`** 的元素；atlas `.catTab` 已有 `transition: all`，必须手动追加 `&:active`，否则 mixin 的 `transition` 会覆盖原过渡（P1 已验证的禁忌）。
- `message` 图标路径用 Feather 风 `message-circle`（线性、`fill:none`、`stroke:currentColor`），与现有图标风格一致；`size` 默认由调用处传入。
- solve 页用 **px 单位**（非 rpx），其 `.sectionTitle` 现有 `font-size:28px`；新增内联 `ModelIcon` 用 px 尺寸（如 18px）保持协调，不混入 rpx。
- bank 空态的「去做题」按钮是 Empty 组件没有的 action 槽，在 bank 本地渲染：`<Empty ... />` 下方单独放 `<View className={styles.goBtn}>`，不改 Empty 组件签名（保持组件通用）。
- 遵守硬约束：零回归；改完 tsc + 浏览器验证才汇报；本轮不提交、不 push。

## 验证

- `cd miniapp; npx.cmd tsc --noEmit` → 0 错误（尤其确认 `message` 类型与 ICONS 同步）。
- 浏览器硬刷新 `http://localhost:10086`：
  - **mine**：点「错题本」卡片 → 有按压缩放/降透明度反馈。
  - **atlas**：点分类 Tab → 按压反馈（不破坏选中态过渡）；点清空 ✕ → 反馈；搜索聚焦环仍正常（P2 不回归）。
  - **challenge**（需选一题进入）：点知识点入口、思路递进图折叠、选做推导折叠、守恒校验头、再试一次按钮 → 均有按压反馈；选项/提交/朗读原有反馈不回归。
  - **solve**：3 个小节标题显示线性 SVG 图标（无 emoji），排版不乱。
  - **reaction**：提示行显示 lightbulb 线性图标（无 💡）。
  - **bank**：清空错题后空态显示共享 Empty（trophy 图标 + 文案 + 去做题按钮），无 🎉。
  - 每页 `document.documentElement.scrollWidth === clientWidth`（无水平溢出），无控制台错误，原有交互（搜索、答题、折叠、跳转）不受影响。

## 风险与回退

- `message` 图标类型/ICONS 不同步 → tsc 会报错，立即修正；若渲染异常，回退为用现有 `book` 图标顶替讲解。
- solve 小节标题加图标后排版错位 → 调整 `.sectionTitle` 为 inline-flex 居中；极端情况回退为纯文本标题（去掉图标）。
- 任一按压态改动影响已有过渡 → 仅回退该元素的改动，不影响其他。
- 全程小步验证，失败只回退单原子。
