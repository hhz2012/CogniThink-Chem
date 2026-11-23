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
  | 'generic'             // 通用降级面板

/** 氧化还原：半反应 */
export interface RedoxHalfReaction {
  species: string          // 物种/元素，如 Mn、Fe
  from: number             // 起始价态
  to: number               // 终态价态
  electrons: number        // 转移电子数（正数）
  role: '氧化' | '还原'    // 氧化=失电子，还原=得电子
}
/** 氧化还原：电子守恒配平汇总 */
export interface RedoxSolveData {
  kind: 'redox-electron'
  halfReactions: RedoxHalfReaction[]
  balance?: {
    oxidizedCount: number   // 失电子总数
    reducedCount: number    // 得电子总数
    lcm: number             // 最小公倍数
    ratio: string           // 系数比，如 "1:5"
  }
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
  equations: IonicConservationEq[]
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
  factors: EquilibriumFactor[]
}

/** 结构化解题数据（按题型判别） */
export type SolveData = RedoxSolveData | IonicSolveData | EquilibriumSolveData

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
}

export interface Chapter {
  id: string
  name: string
  icon: string
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
