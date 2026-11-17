// 化学题型
export type ChemCategory =
  | 'ProductInference'
  | 'IonCoexistence'
  | 'StoichiometryCalc'
  | 'Electrochemistry'
  | 'ChemicalEquilibrium'
  | 'ExperimentalInquiry'

export type Thought =
  | 'Conservation'
  | 'Structure'
  | 'Equilibrium'
  | 'Classification'
  | 'ModelCognition'

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
  unlockNeed?: number // 解锁所需的章节进度（章节内序号）
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
