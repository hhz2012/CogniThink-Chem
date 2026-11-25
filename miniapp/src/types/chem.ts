// 高考化学六大难题专项
export type ChemCategory =
  | 'RedoxReaction'        // 氧化还原反应
  | 'IonicEquilibrium'     // 水溶液中的离子平衡
  | 'ChemicalEquilibrium'  // 化学平衡
  | 'Electrochemistry'     // 电化学
  | 'InorganicInference'   // 元素化合物推断
  | 'OrganicChemistry'     // 有机化学

export type Thought =
  | 'Conservation'
  | 'Structure'
  | 'Equilibrium'
  | 'Classification'
  | 'ModelCognition'

/**
 * 全站统一线性图标名（单一权威源）。
 * 数据层的 icon 字段与 UI 渲染层共用此类型，杜绝 emoji 多色不一致。
 * 实际 SVG 路径由 components/SolvePanels/ModelIcon 维护。
 */
export type ModelIconName =
  | 'zap'
  | 'droplet'
  | 'refresh'
  | 'battery'
  | 'test-tube'
  | 'hexagon'
  | 'link'
  | 'check-circle'
  | 'lightbulb'
  | 'layout'
  | 'camera'
  | 'home'
  | 'volume'
  | 'book'
  | 'user'
  | 'search'
  | 'trophy'
  | 'scale'
  | 'atom'
  | 'layers'
  | 'puzzle'
  | 'wrench'
  | 'target'

// 难度等级（与高考难度分布对齐）
export type Difficulty = '基础' | '进阶' | '综合' | '难题' | '压轴'

export interface GuideLevel {
  level: number
  title: string
  cognitiveLayer: 'Knowledge' | 'Application' | 'Ability'
  interaction: 'Probe' | 'Hint' | 'Reveal'
  prompts: string[]
  hint?: string
  knowledgeCard?: { name: string; summary?: string; textbookRef?: string }
}

// ========== 题型差异化解题模型：结构化数据（判别联合） ==========
/** 专属可视化面板类型 */
export type SolveModelKind =
  | 'redox-electron'      // 氧化还原：化合价 + 电子守恒
  | 'ionic-conservation'  // 离子平衡：粒子 + 三大守恒
  | 'equilibrium-shift'   // 化学平衡：条件-移动-K 推演
  | 'electrochem-cell'    // 电化学：装置 + 半反应 + 现象
  | 'inorganic-inference' // 无机推断：突破口 + 顺逆推 + 代回验证
  | 'organic-chemistry'   // 有机化学：认官能团 + 定反应 + 写产物
  | 'generic'             // 通用降级面板

/** 氧化还原：半反应（变价明细核心） */
export interface RedoxHalfReaction {
  species: string          // 物种/元素，如 Mn、Fe
  from: number             // 起始价态
  to: number               // 终态价态
  electrons: number        // 转移电子数（正数，该半反应在配平式中的总电子数）
  role: '氧化' | '还原'    // 氧化=失电子，还原=得电子
  reactantFormula?: string // 反应物中含该元素的物质，如 KMnO₄
  productFormula?: string  // 产物中含该元素的物质，如 MnSO₄
  atomCount?: number       // 参与变价的原子数
}
/** 氧化还原：物质系数 */
export interface RedoxCoefficient {
  formula: string
  value: number
}
/** 氧化还原：电子守恒配平汇总 */
export interface RedoxBalance {
  oxidizedCount: number   // 失电子总数
  reducedCount: number    // 得电子总数
  lcm: number             // 最小公倍数
  ratio: string           // 系数比，如 "1:5"
  coefficients?: RedoxCoefficient[]  // 各物质系数
}
/** 氧化还原：多反应题中的单条反应（判断/比较类用） */
export interface RedoxSubReaction {
  equation: string
  type: string            // 置换/化合/分解/歧化/归中/复分解(非氧还)/其他
  isRedox: boolean
  oxidant?: string        // 氧化剂
  oxidizedProduct?: string // 氧化产物
}
/** 氧化还原：价态规律条目（粒子中元素的价态位置） */
export interface RedoxValenceItem {
  species: string
  element: string
  valence: number
  position: '最低' | '最高' | '中间'
}
/** 氧化还原阶段一：体系粒子（区分大量 / 微量） */
export interface RedoxParticle {
  formula: string
  source: string            // 来源：电离 / 加入 / 水解 / 水电离 等
  abundance: '大量' | '微量'
}
/** 氧化还原阶段一：某氧化剂在不同介质下的还原产物映射 */
export interface RedoxMediumRule {
  species: string           // 如 MnO₄⁻
  acid: string              // 酸性还原产物，如 Mn²⁺
  neutral: string           // 中性还原产物，如 MnO₂
  base: string              // 碱性还原产物，如 MnO₄²⁻
}
/** 氧化还原阶段一：推导的产物及其依据 */
export interface RedoxProduct {
  species: string
  basis: string             // 推导依据（价态 / 介质 / 题目信息）
}
/** 氧化还原阶段一：产物共存 / 沉淀判断 */
export interface RedoxCoexistence {
  ok: boolean
  note: string
  precipitates?: string[]
}
/** 氧化还原阶段一（主链路）：反应可行性 & 产物推理；全部字段可选，面板按存在性动态渲染 */
export interface RedoxStage1 {
  particles?: RedoxParticle[]
  medium?: '酸性' | '碱性' | '中性'
  mediumRules?: RedoxMediumRule[]
  candidates?: { oxidants: string[]; reductants: string[] }
  order?: { property: '氧化性' | '还原性'; strongerFirst: string[]; conclusion: string }
  products?: RedoxProduct[]
  coexistence?: RedoxCoexistence
}
/** 氧化还原：结构化解题数据（v2：核心=变价明细，按考法叠加专属段，全部向后兼容） */
export interface RedoxSolveData {
  kind: 'redox-electron'
  halfReactions: RedoxHalfReaction[]
  balance?: RedoxBalance
  reaction?: {
    equation?: string        // 配平后完整方程式
    ionicEquation?: string   // 离子方程式（题型涉及时）
    type?: string            // 置换/化合/分解/歧化/归中/复分解(非氧还)/其他
    subtype?: string         // 配平/计算/概念判断/性质比较/反应先后/复杂综合
  }
  agents?: {                 // 四剂四产物
    oxidants: string[]
    reductants: string[]
    oxidizedProducts: string[]
    reducedProducts: string[]
  }
  calc?: {                   // 计算类：已知量→求解过程→结果
    given: string
    target: string
    steps: string[]
    result: string
  }
  comparison?: {             // 性质比较类
    property: '氧化性' | '还原性'
    order: string            // 如 "Cl₂ > Fe³⁺ > I₂"
    basis: string
  }
  order?: {                  // 反应先后类
    property: '氧化性' | '还原性'
    strongerFirst: string[]  // 由强到弱
    conclusion: string
  }
  valenceRule?: {            // 价态规律类
    items: RedoxValenceItem[]
  }
  reactions?: RedoxSubReaction[]  // 多反应题（R9 判断、R10 比较）
  /** 阶段一（主链路）：反应可行性 & 产物推理；缺省不渲染阶段一，向后兼容 */
  stage1?: RedoxStage1
}

/** 离子平衡：溶液粒子 */
export interface IonicSpecies {
  formula: string
  source: string            // 来源：电离 / 水解 / 水电离
}
/** 离子平衡：守恒式 */
export interface IonicConservationEq {
  type: '电荷' | '物料' | '质子'
  expression: string
}
export interface IonicSolveData {
  kind: 'ionic-conservation'
  species: IonicSpecies[]
  equations?: IonicConservationEq[]
  /** 比浓度：浓度大小 / pH 排序 / 守恒判断 的真值 */
  comparison?: {
    type: '浓度大小' | 'pH 排序' | '守恒判断'
    order?: string
    conclusion?: string
    basis?: string
  }
  /** 计算类：pH / 电离度等数值求解 */
  calc?: {
    given: string
    target: string
    steps: string[]
    result: string
  }
}

/** 化学平衡：条件-移动推演因素 */
export interface EquilibriumFactor {
  factor: string            // 影响因素：浓度/温度/压强/催化剂 等
  change: string            // 改变方式
  shift: '正向' | '逆向' | '不移动'
  kChanges: '增大' | '减小' | '不变'
  conversion?: string       // 转化率变化描述
}
export interface EquilibriumSolveData {
  kind: 'equilibrium-shift'
  factors?: EquilibriumFactor[]
  /** 写平衡：配平方程式与热效应 / 气体分子数变化 */
  reaction?: {
    equation: string
    deltaH?: '吸热' | '放热'
    gasChange?: '气体分子数增大' | '气体分子数减小' | '气体分子数不变'
  }
  /** 看 K 与转化率：平衡常数 / 转化率 / 浓度商 / Ksp / 等效平衡 / 速率 的计算真值 */
  result?: {
    type: '平衡常数 K' | '转化率' | '浓度商 Q' | '平衡浓度' | 'Ksp 沉淀' | '等效平衡' | '反应速率'
    expression?: string
    steps?: string[]
    value?: string
    conclusion?: string
  }
}

/** 电化学：半反应 */
export interface ElectrochemHalfReaction {
  electrode: '负极' | '正极' | '阴极' | '阳极'
  equation: string
  role: '氧化' | '还原'
}
/** 电化学：装置判别 + 半反应 + 现象/计算 */
export interface ElectrochemSolveData {
  kind: 'electrochem-cell'
  device: {
    type: '原电池' | '电解池' | '可充电电池' | '燃料电池' | '金属腐蚀'
    electrolyte?: string
    electronFlow?: string
  }
  electrodes: {
    negative?: string
    positive?: string
  }
  halfReactions: ElectrochemHalfReaction[]
  totalEquation?: string
  phenomena?: string[]
  calc?: {
    given: string
    target: string
    steps: string[]
    result: string
  }
  conclusion?: string
}

/** 无机推断：找突破口 + 顺逆推 + 代回验证 */
export interface InferenceSolveData {
  kind: 'inorganic-inference'
  subtype?: string  // 框图推断 / 元素周期律推断 / 工业流程 / 离子检验 / 物质鉴别 / 离子共存 / 一步转化 / 性质与用途 / 试剂保存 等
  /** 找突破口：特征条件 → 关键结论 */
  breakthrough?: {
    clue: string       // 特征条件，如 "Y 最外层电子数是电子层数的 3 倍"
    conclusion: string // 推出的关键结论，如 "Y 为 O"
  }
  /** 顺逆推：正向/逆向推理步骤与中间结论 */
  reasoning?: {
    steps: string[]
    conclusion?: string
  }
  /** 代回验证：把结论代回原题条件校验 */
  verification?: {
    basis: string   // 验证依据，如 "Al 元素守恒" / "每步转化成立"
    result: string  // 验证结果
  }
}

/** 有机化学：认官能团 + 定反应 + 写产物 */
export interface OrganicSolveData {
  kind: 'organic-chemistry'
  /** 认官能团：本题涉及的官能团及特征性质 */
  functionalGroups?: {
    name: string       // 如 '碳碳双键'
    structure?: string // 如 'C=C'
    property?: string  // 该官能团的特征性质/反应，如 '使溴水褪色（加成）'
  }[]
  /** 定反应：本题涉及的反应类型与判断结论 */
  reactions?: {
    type?: string       // 如 '加成' / '取代' / '氧化' / '酯化' / '水解'
    reactants?: string  // 反应物描述
    conclusion?: string // 判断结论，如 '属于取代反应'
  }[]
  /** 写产物：产物结构 / 推断结果 / 计算真值 */
  product?: {
    formula?: string    // 产物/目标结构简式，如 'CH₃COOH'
    equation?: string   // 反应方程式
    count?: string      // 同分异构体数目，如 '4 种'
    reasoning?: string  // 推导说明
    conclusion?: string // 最终结论
  }
}

/** 结构化解题数据（按题型判别） */
export type SolveData = RedoxSolveData | IonicSolveData | EquilibriumSolveData | ElectrochemSolveData | InferenceSolveData | OrganicSolveData

export interface Challenge {
  id: string
  category: ChemCategory
  title: string
  problemText: string
  options: string[]
  answerIndex: number
  explanation: string
  thought: Thought
  guide: GuideLevel[]
  difficulty: Difficulty
  unlockNeed?: number // 解锁所需的本专项内已通关序号（从0开始）
  solveData?: SolveData // 题型专属结构化解题数据（缺省降级通用面板）
  questionTarget?: string // 可选：问题指向的量/类型（元骨架 S5「问题关联」的结构化输入；缺省降级，不影响解题）
  problemConditions?: string[] // 可选：题面条件清单（元骨架 S1「读题定向」的结构化输入；缺省降级，不影响解题）
}

export interface Chapter {
  id: string
  name: string
  icon: ModelIconName
  category: ChemCategory
  color: string
  desc: string
  challenges: Challenge[]
}

export interface UserProgress {
  solvedIds: string[]
  wrongIds: string[] // 错题本：答错入册，做对/手动移除
  thoughtCounts: Record<Thought, number>
  inspirations: number // 灵感值（用于伴思引导）
  lastChapterId?: string
}
