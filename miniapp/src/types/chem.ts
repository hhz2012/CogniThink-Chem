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
  thoughtCounts: Record<Thought, number>
  inspirations: number // 灵感值（用于伴思引导）
  lastChapterId?: string
}
