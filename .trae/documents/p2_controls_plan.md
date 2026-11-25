# P2 控件人性化优化 实施计划

> 背景：P0（emoji→线性 SVG、首页分段控件）、P1（统一按压反馈、折叠动画复用）已完成并提交。P1 计划明确将以下三项延后到 P2，本轮实施。

## 仓库研究结论

**1. Textarea 原生控件外观与手搓风格脱节（缺聚焦态）**
- `pages/solve` 的 `<Textarea>`（[index.tsx L47](file:///d:/github/CogniThink-Chem/miniapp/src/pages/solve/index.tsx#L47)，class `.input`）：仅 `background #f4f7f9 + border-radius`，**无边框、无 outline 重置、无聚焦态**。H5 聚焦时出现浏览器默认 outline，与卡片风格脱节。
- `pages/tutor` 的 `<Textarea>`（[index.tsx L229](file:///d:/github/CogniThink-Chem/miniapp/src/pages/tutor/index.tsx#L229)，class `.textarea`）：有 `border: 2rpx solid $color-border`，但**无 outline 重置、无聚焦态**，聚焦时边框不变。
- 全局 [compat.scss](file:///d:/github/CogniThink-Chem/miniapp/src/styles/compat.scss) 只做了 `box-sizing`，未归一化原生 `input/textarea` 的 outline / appearance / 默认底色。

**2. 加载态 / 空态不统一，且缺共享组件**
- 无共享 `Loading` / `Empty` 组件（`components/` 下 14 个组件均无）。
- `tutor` 列表：`{loading && <Text>加载中…</Text>}` 仅居中纯文本（`.loadingText`），无 spinner；且 `problems` 为空时**无空态**，列表区直接留白。
- `solve`：求解中（`loading=true` 且 `view=null`）结果区无任何反馈，仅按钮文字变"求解中…"。
- `atlas` 已有良好的本地空态（`.empty`：图标 + 标题 + 副标题），可作为统一参考模式。

**3. Input 聚焦态缺失**
- `atlas` 的 `.searchBox`（内含 `<Input>`）有 `border: 1rpx solid $color-divider`，但**无 `:focus-within` 聚焦环**；`.searchInput` 无 outline 重置，聚焦时搜索框无视觉反馈。

## 范围（本轮 P2）

- **Atom 1 — 表单控件聚焦态统一**：新增 `@mixin field-focus`（outline 重置 + 主色边框/光晕过渡）；全局归一化原生 `input/textarea` 外观（outline/appearance/底色），让各页面 module.scss 接管外观。
- **Atom 2 — 共享加载/空态组件**：新建 `<Loading>`（纯 CSS spinner + 文本）与 `<Empty>`（ModelIcon + 标题 + 副标题），对齐 atlas 既有空态模式。
- **Atom 3 — 逐页接入**：solve/tutor 的 Textarea 聚焦态；tutor 列表 Loading + Empty；solve 求解中 Loading；atlas 搜索框聚焦环。

## 待改文件模块

- `miniapp/src/styles/variables.scss` — 新增 `@mixin field-focus`。
- `miniapp/src/styles/compat.scss` — 全局归一化 `input/textarea` 原生外观。
- `miniapp/src/components/Loading/`（新建 index.tsx + index.module.scss）
- `miniapp/src/components/Empty/`（新建 index.tsx + index.module.scss）
- `pages/solve/`（index.tsx + index.module.scss）— `.input` 聚焦态 + 求解中 Loading
- `pages/tutor/`（index.tsx + index.module.scss）— `.textarea` 聚焦态 + 列表 Loading/Empty
- `pages/atlas/`（index.module.scss）— `.searchBox` 聚焦环

## 实施步骤（依赖序）

1. **基础设施**：`variables.scss` 加 `@mixin field-focus`（`outline:none; transition: border-color/box-shadow; &:focus { border-color: $color-primary; box-shadow: 0 0 0 4rpx rgba(46,167,196,.15) }`）；`compat.scss` 全局 `input, textarea { outline: none; -webkit-appearance: none; background: transparent; }`。
2. **Atom 2 组件**：新建 `<Loading>`（CSS 旋转环 + 可选 `text`，居中，尊重 prefers-reduced-motion）；新建 `<Empty>`（`title` 必填、`sub?`、`icon?` 默认 `search`，复用 ModelIcon，样式对齐 atlas `.empty`）。
3. **solve**：`.input` 加 `border: 2rpx solid transparent` + `@include field-focus`（用 box-shadow 光晕，border 变化不撑大因为 border-box）；`{loading && <View className={styles.card}><Loading text="引擎求解中…" /></View>}` 插入结果区。
4. **tutor**：`.textarea` 加 `@include field-focus`；`{loading && <Loading />}` 替换纯文本；`{!loading && !error && problems.length === 0 && <Empty title="暂无题目" sub="服务地址可能未配置题目，或稍后再来看看" />}`。
5. **atlas**：`.searchBox` 加 `transition: border-color/box-shadow` + `&:focus-within { border-color: $color-primary; box-shadow: 0 0 0 4rpx rgba(46,167,196,.15) }`；`.searchInput` 依赖全局 outline 重置（无需单独改）。
6. `cd miniapp; npx.cmd tsc --noEmit` → 必须 0 错误。
7. 浏览器硬刷新逐页验证后提交本地 `main`。

## 依赖与注意事项

- 纯样式 + 展示组件，无逻辑/接口变更，不碰引擎红线。
- 聚焦光晕用 `box-shadow` 而非改 `border-width`，配合全局 `border-box`，**不引起布局跳动**。
- `compat.scss` 全局动 `input/textarea` 的 `background: transparent`：当前仅 atlas/solve/tutor 三处输入控件，均有父级或自身背景（solve `.input` 有 `#f4f7f9`、tutor `.textarea` 有 `#f8fafc`、atlas `.searchBox` 有 `$color-bg-card`），透明后透出父背景，无回归。浏览器验证会覆盖。
- `<Empty>` 复用 ModelIcon（已有 `search`/`book` 等），不新增图标资源；`<Loading>` 用纯 CSS 动画，无外部依赖。
- `field-focus` 对已有 `transition: all` 的元素不适用（会覆盖）——本批目标（solve `.input`、tutor `.textarea`、atlas `.searchBox`）均无 `transition: all`，安全。
- 遵守硬约束：零回归、改完 tsc + 浏览器验证才提交、只提交本地 main、不跑全量批回归。

## 验证

- `cd miniapp; npx.cmd tsc --noEmit` → 0 错误。
- 浏览器硬刷新 `http://localhost:10086`：
  - **solve**：聚焦 textarea → 主色边框 + 光晕，无浏览器默认 outline；失焦恢复；点"开始求解"后结果区出现 Loading（求解中），无布局跳动。
  - **tutor**：聚焦推理 textarea → 同上；列表加载时显示 spinner Loading（而非纯文本）；`problems` 空时显示 Empty（需构造空态或断网验证，至少确认组件渲染无错）。
  - **atlas**：聚焦搜索框 → 搜索框出现主色边框 + 光晕；输入/清空正常。
  - 每页 `document.documentElement.scrollWidth === clientWidth`（无水平溢出），无元素裁剪，无控制台错误。
- 确认原有功能（输入、提交、搜索、答题流程）不受影响。

## 风险与回退

- `compat.scss` 全局 `input/textarea { background: transparent }` 使某处未单独设背景的输入框变透明 → 当前三处均有背景，若验证发现遗漏，回退为仅重置 `outline`/`appearance`，不动 `background`。
- Loading 纯 CSS 动画在某些端卡顿 → 加 `prefers-reduced-motion` 停止旋转，仅保留文本。
- 全程小步验证，任一原子失败只回退该原子，不影响已完成部分。
