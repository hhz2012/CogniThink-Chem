// ========== 反应脚手架：反应类型判别 + 结构化真值数据（判别联合） ==========
// 与题型侧 types/chem.ts 的 SolveModelKind / SolveData 平行：
// 题型侧 solveData 挂在 Challenge 上，反应侧 ReactionSolveData 挂在反应条目上（Task 3 建 data/reactions）。
// 真值形状按反应 kind 区分，供 engine/reactionNarration 的 narrateReaction 实例化使用。

import {
  RedoxHalfReaction,
  RedoxBalance,
  ElectrochemHalfReaction,
} from './chem'
import type { ScaffoldBranch } from './scaffold'

/** 反应脚手架模型 kind：9 个非有机主类型 + 9 个有机子类型 + 通用降级 */
export type ReactionSolveKind =
  | 'reaction-combination'          // 化合反应
  | 'reaction-decomposition'        // 分解反应
  | 'reaction-displacement'         // 置换反应
  | 'reaction-double-displacement'  // 复分解反应
  | 'reaction-combustion'           // 燃烧
  | 'reaction-redox'                // 氧化还原（归中/歧化/复杂，不属四类基本反应）
  | 'reaction-electrode'            // 电极反应
  | 'reaction-ion-equilibrium'      // 离子平衡（电离/水解/沉淀溶解）
  | 'reaction-thermochemical'       // 热化学
  // 有机反应子类型（主类型 Organic 细分为以下 9 类）
  | 'reaction-organic-substitution'     // 取代
  | 'reaction-organic-addition'         // 加成
  | 'reaction-organic-elimination'      // 消去
  | 'reaction-organic-oxidation'        // 氧化
  | 'reaction-organic-reduction'        // 还原
  | 'reaction-organic-esterification'   // 酯化
  | 'reaction-organic-hydrolysis'       // 水解
  | 'reaction-organic-polyaddition'     // 加聚
  | 'reaction-organic-polycondensation' // 缩聚
  | 'reaction-generic'              // 通用降级

/** 反应条目通用真值字段（所有 kind 共享） */
interface ReactionTruthBase {
  /** 完整配平方程式（L2 引擎写法） */
  equation?: string
  /** 反应条件，如 ["点燃"]、["△","浓"]、["催化剂","加热"] */
  conditions?: string[]
  /** 教材描述的实验现象 */
  phenomenon?: string
}

// ---------- 无机主类型 ----------

export interface ReactionCombinationData extends ReactionTruthBase {
  kind: 'reaction-combination'
  reactantCount?: number   // 反应物种数
  productCount?: number    // 产物种数（多变一应为 1）
  isRedox?: boolean        // 是否涉及价态变化
  valenceChanges?: string[] // 价态变化要点
}

export interface ReactionDecompositionData extends ReactionTruthBase {
  kind: 'reaction-decomposition'
  energyType?: '加热' | '高温' | '通电' | '催化剂' | '常温'
  products?: string[]      // 分解产物
}

export interface ReactionDisplacementData extends ReactionTruthBase {
  kind: 'reaction-displacement'
  activityBasis?: string    // 活动性判据（金属活动性顺序等）
  displacedElement?: string // 被置换出的单质
  valenceNote?: string      // 价态注意，如 "Fe 置换显 +2"
}

export interface ReactionDoubleDisplacementData extends ReactionTruthBase {
  kind: 'reaction-double-displacement'
  drivingForce?: '沉淀' | '气体' | '水' // 复分解发生条件
  ionicEquation?: string    // 离子方程式
  solubilityNote?: string   // 溶解性/拆写说明
}

export interface ReactionCombustionData extends ReactionTruthBase {
  kind: 'reaction-combustion'
  combustible?: string   // 可燃物
  oxidizer?: string      // 助燃物（通常 O₂）
  complete?: boolean     // 是否完全燃烧
  products?: string[]    // 燃烧产物
}

/** 氧化还原：复用题型侧的半反应与配平汇总结构（避免双份维护） */
export interface ReactionRedoxData extends ReactionTruthBase {
  kind: 'reaction-redox'
  halfReactions?: RedoxHalfReaction[]
  balance?: RedoxBalance
  oxidants?: string[]
  reductants?: string[]
  directionBasis?: string  // 强弱律验证方向的依据
}

/** 电极反应：复用题型侧的半反应结构 */
export interface ReactionElectrodeData extends ReactionTruthBase {
  kind: 'reaction-electrode'
  deviceType?: '原电池' | '电解池' | '可充电电池' | '燃料电池'
  halfReactions?: ElectrochemHalfReaction[]
  totalEquation?: string
}

export interface ReactionIonEquilibriumData extends ReactionTruthBase {
  kind: 'reaction-ion-equilibrium'
  equilibriumType?: '电离' | '水解' | '沉淀溶解' | '络合'
  equilibriumEquation?: string
  shiftFactors?: { factor: string; change: string; direction: '正向' | '逆向' | '不移动' }[]
  conservations?: { type: '电荷' | '物料' | '质子'; expression: string }[]
}

export interface ReactionThermochemicalData extends ReactionTruthBase {
  kind: 'reaction-thermochemical'
  deltaH?: string                    // 如 "-571.6 kJ/mol"
  states?: Record<string, string>    // 各物质状态 g/l/s/aq
  calcSteps?: string[]               // ΔH 计算步骤（键能/盖斯定律）
}

// ---------- 有机子类型 ----------

/** 有机反应共享真值字段（9 个子类型共用） */
interface OrganicTruthBase extends ReactionTruthBase {
  functionalGroups?: {
    name: string
    structure?: string
    property?: string
  }[]
  reaction?: {
    type?: string        // 子类型，如 '加成'
    reagents?: string    // 试剂
    conditions?: string  // 条件
    conclusion?: string  // 反应类型判断结论
  }
  product?: {
    formula?: string    // 产物结构简式
    equation?: string   // 反应方程式
    reasoning?: string  // 推导说明
    conclusion?: string // 最终结论
  }
}

export interface ReactionOrganicSubstitutionData extends OrganicTruthBase {
  kind: 'reaction-organic-substitution'
}
export interface ReactionOrganicAdditionData extends OrganicTruthBase {
  kind: 'reaction-organic-addition'
  regioselectivity?: string  // 区域选择性，如 '马氏规则'
}
export interface ReactionOrganicEliminationData extends OrganicTruthBase {
  kind: 'reaction-organic-elimination'
}
export interface ReactionOrganicOxidationData extends OrganicTruthBase {
  kind: 'reaction-organic-oxidation'
}
export interface ReactionOrganicReductionData extends OrganicTruthBase {
  kind: 'reaction-organic-reduction'
}
export interface ReactionOrganicEsterificationData extends OrganicTruthBase {
  kind: 'reaction-organic-esterification'
}
export interface ReactionOrganicHydrolysisData extends OrganicTruthBase {
  kind: 'reaction-organic-hydrolysis'
}
export interface ReactionOrganicPolyadditionData extends OrganicTruthBase {
  kind: 'reaction-organic-polyaddition'
  monomer?: string     // 单体
  repeatUnit?: string  // 重复单元（链节）
}
export interface ReactionOrganicPolycondensationData extends OrganicTruthBase {
  kind: 'reaction-organic-polycondensation'
  monomer?: string
  repeatUnit?: string
  byproduct?: string   // 缩合脱去的小分子，如 'H₂O'
}

export interface ReactionGenericData extends ReactionTruthBase {
  kind: 'reaction-generic'
}

/** 反应结构化解题/讲解真值数据（按反应 kind 判别） */
export type ReactionSolveData =
  | ReactionCombinationData
  | ReactionDecompositionData
  | ReactionDisplacementData
  | ReactionDoubleDisplacementData
  | ReactionCombustionData
  | ReactionRedoxData
  | ReactionElectrodeData
  | ReactionIonEquilibriumData
  | ReactionThermochemicalData
  | ReactionOrganicSubstitutionData
  | ReactionOrganicAdditionData
  | ReactionOrganicEliminationData
  | ReactionOrganicOxidationData
  | ReactionOrganicReductionData
  | ReactionOrganicEsterificationData
  | ReactionOrganicHydrolysisData
  | ReactionOrganicPolyadditionData
  | ReactionOrganicPolycondensationData
  | ReactionGenericData

/** 反应条目（前端反应子集 data/reactions 的条目形状；Task 3 按此填充，narrateReaction 消费） */
export interface ReactionEntry {
  id: string
  /** 有机子类型等精细标签，可选 */
  tags?: string[]
  equation?: string
  conditions?: string[]
  phenomenon?: string
  /** 本条反应的实例化真值；缺省时 narrateReaction 返回 null，调用方回退 step.analyze */
  solveData?: ReactionSolveData
  /** 条件 / 数量分支真值（Task 7）；缺省表示本条反应无分支，resolveBranch 返回空结论 */
  branches?: ScaffoldBranch[]
}
