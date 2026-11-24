import { ReactionEntry, ReactionSolveKind } from '../../types/reaction'
import inorganic from './inorganic'
import redox from './redox'
import electrochem from './electrochem'
import equilibrium from './equilibrium'
import organic from './organic'

/** 前端反应子集：所有模块的代表反应聚合出口（含 solveData 真值）。 */
export const REACTIONS: ReactionEntry[] = [
  ...inorganic,
  ...redox,
  ...electrochem,
  ...equilibrium,
  ...organic,
]

export { inorganic, redox, electrochem, equilibrium, organic }

/** 按 solveData.kind 分组，便于反应卡页按类型取数。 */
export const REACTIONS_BY_KIND = (() => {
  const map = new Map<string, ReactionEntry[]>()
  for (const e of REACTIONS) {
    const k = e.solveData?.kind ?? 'reaction-generic'
    const list = map.get(k)
    if (list) list.push(e)
    else map.set(k, [e])
  }
  return map
})()

/** 按 id 取反应；找不到返回 undefined。 */
export const getReactionById = (id: string): ReactionEntry | undefined =>
  REACTIONS.find((e) => e.id === id)

/** 反应子类型中文标签（反应卡列表分组标题用）。 */
export const REACTION_KIND_LABEL: Record<ReactionSolveKind, string> = {
  'reaction-combination': '化合反应',
  'reaction-decomposition': '分解反应',
  'reaction-displacement': '置换反应',
  'reaction-double-displacement': '复分解反应',
  'reaction-combustion': '燃烧反应',
  'reaction-redox': '氧化还原反应',
  'reaction-electrode': '电极反应',
  'reaction-ion-equilibrium': '离子平衡',
  'reaction-thermochemical': '热化学反应',
  'reaction-organic-substitution': '有机 · 取代',
  'reaction-organic-addition': '有机 · 加成',
  'reaction-organic-elimination': '有机 · 消去',
  'reaction-organic-oxidation': '有机 · 氧化',
  'reaction-organic-reduction': '有机 · 还原',
  'reaction-organic-esterification': '有机 · 酯化',
  'reaction-organic-hydrolysis': '有机 · 水解',
  'reaction-organic-polyaddition': '有机 · 加聚',
  'reaction-organic-polycondensation': '有机 · 缩聚',
  'reaction-generic': '其他反应',
}
