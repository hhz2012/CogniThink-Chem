# P1 控件人性化优化 实施计划

> 背景：上一轮控件审查产出了 P0→P2 分级方案，P0（emoji→线性 SVG 统一、首页 Picker→分段控件）已完成并提交（324618a）。本轮继续 P1。
>
> ⚠️ 说明：原审查的 P1 详细清单未持久化到 memory（仅存了摘要），上下文丢失。本计划基于审查摘要的结论 + 对当前代码的只读审计**重建**，如与你记忆中的 P1 范围不符，请在审批时指出，我会校准后再动手。

## 仓库研究结论

**1. 按压反馈（:active）覆盖严重不均**
各 `module.scss` 中 `:active` 命中数：home 8、challenge 6、bank 3、reaction 3、atlas 1、mine 1、practice 1；**solve、knowledge、tutor 三页为 0**，且 **components/** 下全部组件（TabBar、ScaffoldPractice、ThoughtChip 等）均无 `:active`。
而 onClick 密集区：challenge 17 处、ScaffoldPractice 13 处、home 7、bank/reaction 各 5、tutor 4。大量高频可点击元素按下无任何视觉反馈，手感"死"。审查摘要明确点出"全项目可点击元素普遍缺少按压反馈，solve 页完全没有"。

**2. 折叠/展开不统一**
共享组件 [Collapse](file:///d:/github/CogniThink-Chem/miniapp/src/components/Collapse/index.tsx) 是受控动画外壳（`open` prop，grid 0fr→1fr，自带 a11y + prefers-reduced-motion），但只被 5 处引入（challenge、knowledge、solve、ScaffoldPractice、RedoxElectronPanel）。仍有多处用 `{flag && <View>}` 条件渲染**无动画**地硬切：
- `reaction` L110 `expanded` 记录（步骤卡展开）— 未引入 Collapse
- `bank` L15 `allOpen`（题库总览）— 未引入 Collapse
- `home` L44 `moreOpen`（更多专项）— 未引入 Collapse
- `solve` L20 `showDag`（推理过程折叠头 L151）— 自定义头部 + 疑似条件渲染
- `knowledge` L21 `openId`（知识点卡片 L97）— 自定义头部
- `challenge` L147/L149、`RedoxElectronPanel` L31 — 各自状态

各页折叠头的箭头、内边距、按压态也不统一。审查摘要点出"现有 Collapse 未被统一复用，各处折叠展开样式不统一"。

**3. 其他（列为 P2，本轮不做）**
- `solve` L47、`tutor` L229 的 `Textarea` 为原生控件，H5 下样式与手搓风格不统一（边框/聚焦态）。
- 空状态/加载态、Input 聚焦态等细化体验。

## 范围（本轮 P1）

- **Atom 1 — 统一按压反馈**：新增一个共享 SCSS mixin/占位类，给所有缺失 `:active` 的可点击元素补齐一致的按压态（scale + opacity，带 transition）。重点 solve / knowledge / TabBar / tutor / ScaffoldPractice。
- **Atom 2 — 折叠动画统一复用**：把 reaction / bank / home（及 solve/knowledge 中仍为条件渲染的折叠）迁移到 `<Collapse>`，统一箭头旋转与头部按压态。

**不做（P2，需你确认是否纳入本轮）**：Textarea 原生样式、空/加载态、Input 聚焦态。

## 待改文件模块

- `miniapp/src/styles/`（若有全局 scss 入口）或各 module.scss — 新增共享按压 mixin（Atom 1）
- `pages/solve/`（index.tsx + index.module.scss）— .btn/.collapseHead/卡片按压态；showDag 折叠迁移 Collapse
- `pages/knowledge/`（index.tsx + index.module.scss）— cardHead/card 按压态；确认 openId 折叠是否已用 Collapse
- `pages/reaction/`（index.tsx + index.module.scss）— 引入 Collapse 包裹步骤卡；按压态
- `pages/bank/`（index.tsx + index.module.scss）— 引入 Collapse 包裹题库总览；allHead 按压态
- `pages/home/`（index.tsx + index.module.scss）— moreHead 迁移 Collapse（已有 pill 按压态，本轮只补折叠）
- `pages/tutor/`（index.module.scss）— 可点击元素按压态
- `components/TabBar/`（index.module.scss）— .tab 按压态
- `components/ScaffoldPractice/`（index.module.scss）— 卡片/选项按压态

## 实施步骤（依赖序）

1. **按压态基础设施**：确认全局样式入口；新增 `@mixin pressable`（`transition: transform/opacity .12s; &:active { transform: scale(.97); opacity: .85 }`），放在可被各 module.scss 引入的位置。若项目无共享 scss 变量文件机制，则退化为在每个受影响 module.scss 内联相同规则（保持值一致）。
2. **Atom 1 逐页补齐**：solve → knowledge → TabBar → tutor → ScaffoldPractice → 其余遗漏点。每处只加 `:active`，不改布局/颜色。
3. **Atom 2 折叠迁移**：reaction → bank → home → solve(showDag) → knowledge(确认)。将 `{flag && <View>...</View>}` 替换为 `<Collapse open={flag}>...</Collapse>`（保留内部结构，仅外层包裹）；统一箭头 `transform: rotate` 过渡与头部 `:active`。
4. 每完成一个原子跑 `npx.cmd tsc --noEmit`（必须 0 错误）。
5. 全量验证后 `git add` + 提交到本地 `main`。

## 依赖与注意事项

- Collapse 为受控组件、纯加性包裹，不改变子树 DOM 结构，迁移风险低；但会多一层外壳 View，需确认无 margin 塌陷/宽度变化。
- `:active` 必须加在**最外层可点击容器**，避免与子元素已有 transform 叠加导致跳变。
- 纯 CSS/结构改动，无逻辑变更、无接口变更 → 不影响引擎红线。
- 遵守硬约束：零回归、改完 tsc + 浏览器截图验证才提交、只提交本地 main、不跑全量批回归。

## 验证

- `cd miniapp; npx.cmd tsc --noEmit` → 0 错误。
- 浏览器硬刷新 `http://localhost:10086`，逐页截图：home / solve / knowledge / reaction / bank / tutor / TabBar（每页可见）。
  - 按压反馈：长按可点击元素可见 scale/opacity 变化（用 DOM 测量或截图对比）。
  - 折叠：展开/收起有平滑卷帘动画，箭头旋转；无布局跳动。
  - 每页 `document.documentElement.scrollWidth === clientWidth`（无水平溢出），无元素被裁剪。
- 确认原有功能（点击切换、答题流程）不受影响。

## 风险与回退

- 按压态在某些元素上视觉突兀（如整行卡片 scale 导致相邻抖动）→ 回退为仅 opacity 或仅该元素不应用；逐个截图确认。
- Collapse 包裹引发某页布局偏移 → 该页退回条件渲染，保留其余页迁移。
- 全程小步提交原子，任一原子验证失败只回退该原子，不影响已提交部分。
