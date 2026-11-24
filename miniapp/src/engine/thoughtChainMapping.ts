// ChemCategory → ReactionSolveKind → ScaffoldModuleKind 统一映射（R4 Task 5）
// 全栈唯一分类对齐源：把题型侧（6 类）、反应侧（19 kind）、脚手架侧（8 模块）三套分类对齐。
// 对应 spec 设计 C 的映射表。纯数据 + 查询函数，无副作用。

import type { ChemCategory } from '../types/chem'
import type { ReactionSolveKind } from '../types/reaction'
import type { ScaffoldModuleKind } from '../types/scaffold'

/** 一个题型对齐到的反应 kind 集合与脚手架模块集合 */
export interface CategoryAlignment {
  reactionKinds: ReactionSolveKind[]
  scaffoldModules: ScaffoldModuleKind[]
}

/** 9 个有机子类型 reaction kind */
const ORGANIC_KINDS: ReactionSolveKind[] = [
  'reaction-organic-substitution',
  'reaction-organic-addition',
  'reaction-organic-elimination',
  'reaction-organic-oxidation',
  'reaction-organic-reduction',
  'reaction-organic-esterification',
  'reaction-organic-hydrolysis',
  'reaction-organic-polyaddition',
  'reaction-organic-polycondensation',
]

/** ChemCategory → ReactionSolveKind[] + ScaffoldModuleKind[] 对齐表 */
export const CATEGORY_REACTION_SCAFFOLD_MAP: Record<ChemCategory, CategoryAlignment> = {
  RedoxReaction: {
    reactionKinds: ['reaction-redox', 'reaction-combustion', 'reaction-displacement'],
    scaffoldModules: ['redox-reasoning'],
  },
  IonicEquilibrium: {
    reactionKinds: ['reaction-ion-equilibrium', 'reaction-double-displacement'],
    scaffoldModules: ['ionic-system', 'aqueous-equilibrium'],
  },
  ChemicalEquilibrium: {
    reactionKinds: ['reaction-ion-equilibrium', 'reaction-thermochemical'],
    scaffoldModules: ['chemical-equilibrium'],
  },
  Electrochemistry: {
    reactionKinds: ['reaction-electrode'],
    scaffoldModules: ['electrochem-reasoning'],
  },
  InorganicInference: {
    reactionKinds: [
      'reaction-combination',
      'reaction-decomposition',
      'reaction-displacement',
      'reaction-double-displacement',
    ],
    // 工业流程 / 连续转化类推断复用 sequential-reaction 模块
    scaffoldModules: ['inorganic-inference', 'sequential-reaction'],
  },
  OrganicChemistry: {
    reactionKinds: ORGANIC_KINDS,
    scaffoldModules: ['organic-reaction'],
  },
}

/** 取某题型对齐的全部 reaction kind */
export const getReactionKinds = (category: ChemCategory): ReactionSolveKind[] =>
  CATEGORY_REACTION_SCAFFOLD_MAP[category]?.reactionKinds ?? []

/** 取某题型对齐的全部 scaffold 模块 */
export const getScaffoldModules = (category: ChemCategory): ScaffoldModuleKind[] =>
  CATEGORY_REACTION_SCAFFOLD_MAP[category]?.scaffoldModules ?? []

/** 反向：某 reaction kind 归属哪个题型（取首个匹配；未匹配返回 undefined） */
export const getCategoryForReactionKind = (
  kind: ReactionSolveKind,
): ChemCategory | undefined => {
  const entries = Object.entries(CATEGORY_REACTION_SCAFFOLD_MAP) as [ChemCategory, CategoryAlignment][]
  const found = entries.find(([, a]) => a.reactionKinds.includes(kind))
  return found ? found[0] : undefined
}
