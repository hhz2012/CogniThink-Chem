// 通用 A–F 原子任务骨架模型
// 统摄轴 2（解题侧）与轴 1（反应讲解侧）：所有高中化学难题统一拆成串行依赖的 6 个原子判断任务
//   A 粒子 → B 角色 → C 条件 → D 排序 → E 产物 → F 作答
// 前序未通过校验则后续锁定（门控状态机，见 engine/scaffoldChains.ts）。
// 纯数据 / 逻辑层，不接 UI；字段风格与 engine/solveModels.ts 的 SolveModelStep 保持一致。

/** 原子任务种类：A–F 六步；轴 1 反应侧仅用 A–E 子集 */
export type AtomicTaskKind = 'A' | 'B' | 'C' | 'D' | 'E' | 'F'

/** 每步的学生作答交互形态 */
export type AtomicTaskInteraction =
  | 'multi-check'   // 勾选粒子 / 多勾选变价物种 / 勾选产物
  | 'single-select' // 单选约束条件 / 单选 ABCD 作答
  | 'order'         // 竞争排序（单选优先级 / 轻量拖拽）

/** 单个原子判断任务（字段对齐 SolveModelStep：index/title/goal） */
export interface AtomicTask {
  /** 步骤序号，从 1 开始（与 SolveModelStep.index 对齐，便于后续复用叙述逻辑） */
  index: number
  /** 任务种类 A–F */
  kind: AtomicTaskKind
  /** 步骤标题 */
  title: string
  /** 这一步的目标（通用讲题文案，不含具体题目的答案） */
  goal: string
  /** 学生作答的交互形态 */
  interaction: AtomicTaskInteraction
  /** 该步的校验检查项 / 判断项要点（引擎校验与阶梯释放的依据） */
  checks: string[]
  /** 常见坑（spec「常见坑」列，供答错后的阶梯释放使用，可缺省） */
  pitfall?: string
}

/**
 * 门控状态（引擎是唯一裁判）：
 * - locked：前序未通过，不可访问
 * - unlocked：已解锁，等待学生作答
 * - passed：作答通过，已解锁下一步
 * - failed：作答未通过，仍锁定在当前步可重试
 */
export type StepGateState = 'locked' | 'unlocked' | 'passed' | 'failed'

/**
 * 轴 2 解题侧的首页模块。
 * 原 5 模块 + R4 新增 3 模块（化学平衡 / 无机推断 / 有机反应），共 8 个，
 * 与 engine/thoughtChainMapping.ts 的 CATEGORY_REACTION_SCAFFOLD_MAP 对齐。
 * 独立于 types/chem.ts 的 SolveModelKind，避免破坏现有 6 题型代码。
 */
export type ScaffoldModuleKind =
  | 'redox-reasoning'       // ⚡ 氧化还原推理
  | 'ionic-system'          // 🧪 离子体系推理（共存 / 离子方程式）
  | 'aqueous-equilibrium'   // 💧 水溶液平衡（电离水解 / 粒子浓度排序）
  | 'sequential-reaction'   // 🔁 连续反应推理（铝铁硫氮氯）
  | 'electrochem-reasoning' // 🔋 电化学推理
  | 'chemical-equilibrium'  // ⚖️ 化学平衡移动（R4 新增）
  | 'inorganic-inference'   // 🧩 无机推断（R4 新增）
  | 'organic-reaction'      // 🧬 有机反应（R4 新增）

/** 一条串行原子任务链（A–F 或 A–E 子集） */
export interface ScaffoldChain {
  /** 所属模块（轴 2）或反应侧标识（'reaction'，A–E 子集） */
  module: ScaffoldModuleKind | 'reaction'
  /** 链的展示名 */
  name: string
  /** 串行任务数组，严格按 A→F（或 A→E）顺序排列 */
  tasks: AtomicTask[]
}

// ========== 条件 / 数量分支（Task 7：一等模型概念） ==========
// 同一套粒子组合，条件一变或用量一变，整套 A–E 结论推翻。
// 分支真值挂在 ReactionEntry.branches 上（见 types/reaction.ts），由 engine/scaffoldBranches.resolveBranch 求值。
// 纯数据 / 逻辑层，不接 UI。

/** 题面条件输入（同时承载条件分支与数量分支的判定依据） */
export interface BranchConditions {
  /** 介质环境，如 '酸性' / '碱性' / '中性' / '熔融'（条件分支依据） */
  medium?: string
  /** 反应物用量，如 '少量' / '过量' / '等物质的量'（数量分支依据） */
  quantity?: string
  /** 浓度，如 '稀' / '浓' */
  concentration?: string
  /** 温度，如 '常温' / '加热' / '高温' */
  temperature?: string
  /** 是否通电 */
  electrolyzed?: boolean
  /** 膜隔离，如 '质子膜' / '阴离子膜' / '无' */
  membrane?: string
}

/**
 * A–E 结论集：键为原子任务 kind，值为该步在当前分支下的结论文本。
 * F（匹配选项作答）与分支无关，不在此列。
 */
export type BranchConclusion = Partial<Record<AtomicTaskKind, string>>

/**
 * 单条分支：声明匹配的条件子集（when）+ 该条件下的 A–E 结论集。
 * - 条件分支（ConditionBranch）：when 含 medium / concentration / temperature / electrolyzed / membrane 等。
 * - 数量分支（QuantityBranch）：when 含 quantity（少量 / 过量）。
 * 匹配规则：when 中声明的每个字段都须与输入 conditions 对应字段相等才命中（部分匹配）；
 * when 为空对象 {} 视为默认分支，匹配任意输入（可作安全兜底）。
 */
export interface ScaffoldBranch {
  /** 分支唯一键，如 'acid' / 'no-acid' / 'little' / 'excess' */
  key: string
  /** 展示名，如 '酸性（有 H⁺）' */
  label: string
  /** 匹配条件子集 */
  when: Partial<BranchConditions>
  /** 该分支下的 A–E 结论集 */
  conclusion: BranchConclusion
}

/** resolveBranch 的返回结果 */
export interface BranchResult {
  /** 命中的分支；无命中时为 null（安全降级，不抛错） */
  branch: ScaffoldBranch | null
  /** 命中分支的 A–E 结论集；无命中时为空对象 */
  conclusion: BranchConclusion
  /** 是否命中分支 */
  matched: boolean
}

// ========== 学生作答 / 引擎校验（Task 8，轴 2 解题侧） ==========
// 学生在 A–F 每步作答（勾选 / 单选 / 排序），由确定性引擎判定 pass / fail：
//   pass → 解锁下一步（门控放行）；fail → 锁定当前步并触发阶梯释放提示。
// 判定完全由 engine/scaffoldValidators 的纯函数完成；LLM 仅在答错后做反问引导，
// 不参与也不影响 pass / fail（TR-8.2）。

/** 单个候选项 */
export interface StepOption {
  key: string
  label: string
}

/** 学生在某步的作答（形态与 AtomicTask.interaction 一一对应） */
export type StudentAnswer =
  | { type: 'multi-check'; selected: string[] } // 勾选粒子 / 多勾选变价物种 / 勾选产物
  | { type: 'single-select'; selected: string } // 单选约束条件 / 单选 ABCD 作答
  | { type: 'order'; ordered: string[] } // 竞争排序（单选优先级 / 轻量拖拽）

/** 某步的真值：候选项 + 正确答案（按交互形态区分） */
export type StepGroundTruth =
  | { type: 'multi-check'; options: StepOption[]; correct: string[] }
  | { type: 'single-select'; options: StepOption[]; correct: string }
  | { type: 'order'; options: StepOption[]; correct: string[] }

/** 引擎对一步作答的判定结果（确定性，不含 LLM 输出） */
export interface StepValidation {
  /** 是否通过 */
  passed: boolean
  /** 反馈文案：pass 给确认，fail 给引导，均由引擎模板生成 */
  feedback: string
  /** 正确答案的展示标签（答错时供对照） */
  expectedLabel?: string
}

// ========== 错误触发的阶梯释放（Task 9，渐撤脚手架） ==========
// 复用 tutorEngine 的三层内容结构（hint 一级 / scaffold 二级 / reveal 三级），
// 按学生在某步的「连续错误次数」递进释放：
//   错 1 次 → hint + 辨析选择题（反问，不给结论）
//   错 2 次 → 正反对比卡片（对例 + 错例）
//   错 3 次 → 规则讲解抽屉（揭示该步规则 / 依据，接近 reveal）
// 每档均可由学生关闭（渐撤），关闭后该步仍可独立作答校验。
// 纯数据 / 逻辑层，不接 UI；释放触发由引擎纯函数决定（见 scaffoldValidators.releaseTier）。

/** 阶梯释放档位 */
export type ScaffoldReleaseTier = 1 | 2 | 3

/** 辨析选择题（错 1 次释放）：反问，不给结论，逼学生自己辨析 */
export interface DiscriminationQuestion {
  /** 反问题干 */
  stem: string
  /** 辨析选项（仅引导思考，不判分、不揭示正确项） */
  options: string[]
}

/** 正反对比卡片（错 2 次释放）：一个对例 + 一个错例对照 */
export interface ContrastCard {
  /** 对例：正确做法 / 结论 */
  correct: { title: string; detail: string }
  /** 错例：典型错误做法 / 结论 */
  wrong: { title: string; detail: string }
}

/** 规则讲解抽屉（错 3 次释放）：揭示该步规则 / 依据（接近 reveal） */
export interface RuleDrawer {
  /** 规则标题 */
  title: string
  /** 规则正文 */
  body: string
}

/**
 * 一步的阶梯释放内容（三层，与 tutorEngine 的 hint / scaffold / reveal 对齐）。
 * 全部字段必填，保证每步在任意错误次数下都有内容可释放（永不崩）。
 */
export interface StepScaffoldReleases {
  /** 错 1 次：一级提示 */
  hint: string
  /** 错 1 次：辨析选择题（反问，不揭示结论） */
  probe: DiscriminationQuestion
  /** 错 2 次：二级支架 — 正反对比卡片 */
  scaffold: ContrastCard
  /** 错 3 次：三级揭示 — 规则讲解抽屉 */
  reveal: RuleDrawer
}

/** 一步的练习配置：链上的原子任务 kind + 该步真值 + 阶梯释放三档内容 */
export interface StepPractice {
  /** 对应链上的原子任务种类 A–F */
  kind: AtomicTaskKind
  /** 该步真值；动态生成的降级步（如 C/D，引擎无法可靠产出 correct）为 null，前端渲染只读揭示、不渲染作答控件 */
  truth: StepGroundTruth | null
  /** 阶梯释放内容：按连续错误次数 1 / 2 / 3 递进释放 */
  releases: StepScaffoldReleases
}

/** 一条 A–F 精熟练习：模块链 + 各步真值 / 提示（可附条件取分支结论作参照） */
export interface ScaffoldPractice {
  id: string
  /** 练习标题 */
  title: string
  /** 所属模块（取对应 A–F 链） */
  module: ScaffoldModuleKind
  /** 可选：题面条件，用于 resolveBranch 取该分支 A–E 结论作 E 步参照 */
  conditions?: BranchConditions
  /** 可选：关联的分支真值条目 id（data/branchExamples），用于取该分支结论作参照 */
  branchEntryId?: string
  /** 与 A–F 链一一对应的各步练习配置（长度须与链一致） */
  steps: StepPractice[]
}

// ========== 动态生成产物（dynamic-scaffold-generation spec） ==========
// 后端 buildScaffoldForQuestion 产出的 GeneratedScaffold 是 ScaffoldPractice 的超集，
// 带可追溯审计字段。前端消费时可只取 ScaffoldPractice 字段；声明完整类型便于 provenance / 降级审计。

/** 降级为只读揭示的步及原因（引擎无法可靠产出该步 correct） */
export interface DegradedStep {
  kind: AtomicTaskKind
  reason: string
}

/** 发布前校验门结果摘要 */
export interface ScaffoldValidationSummary {
  ok: boolean
  errors: string[]
}

/**
 * 生成产物：ScaffoldPractice 超集。
 * 后端 JSON 字段与本类型一一对应（camelCase）；多余字段前端忽略不崩。
 */
export interface GeneratedScaffold extends ScaffoldPractice {
  /** 来源题目 id（eval case id） */
  sourceQuestionId: string
  /** 每步 correct 来源引擎字段（如 "RedoxAnalysis.Changes"）；键为 AtomicTaskKind 字符串；降级步不出现 */
  provenance: Record<string, string>
  /** 降级为只读揭示的步及原因 */
  degradedSteps: DegradedStep[]
  /** 发布前校验门结果 */
  validation: ScaffoldValidationSummary
}
