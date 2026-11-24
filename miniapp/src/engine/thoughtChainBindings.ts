// 题目实例化解析（R4 Task 7）：把题型 DAG 节点绑定到具体 Challenge 的 solveData / guide。
// 节点内容分两层（见 spec 设计 D）：
//   题型通用层（title/goal/analyze/...）来自 DAG 定义，对所有题相同；
//   题目实例层（每个节点的具体结论）由本表按 nodeId 从 Challenge.solveData 读取。
// 缺失字段 → 返回 null（不抛错、不编造化学事实）；preview 模式不解析（零答案泄露）。

import type {
  Challenge,
  ChemCategory,
  RedoxSolveData,
  IonicSolveData,
  EquilibriumSolveData,
  ElectrochemSolveData,
  InferenceSolveData,
  OrganicSolveData,
} from '../types/chem'
import { getQuestionTarget, resolveQuestionTargetNodes } from './thoughtChainMetaDAG'

/** 节点实例结论：结构化对象 / 数组 / 字符串，或 null（缺失） */
export type NodeConclusion = unknown

/** 单节点解析器：输入 Challenge，输出该节点本题实例结论（缺失为 null） */
export type NodeResolver = (challenge: Challenge) => NodeConclusion

/** 某题型的全部节点绑定：nodeId → 解析器 */
export type CategoryBindings = Record<string, NodeResolver>

const pick = <T>(v: T | undefined | null): NodeConclusion => (v === undefined || v === null ? null : v)

/** S1 读题定向：本题题面条件清单（取自 Challenge.problemConditions；缺失 → null） */
const s1Conclusion: NodeResolver = (ch) => pick(ch.problemConditions)

/** S5 定答案：本题问题所求 + 命中的结论节点 id（取自 questionTarget + 映射表；缺失 → null） */
const s5Conclusion: NodeResolver = (ch) => {
  const target = getQuestionTarget(ch)
  if (!target) return null
  const matchedNodeIds = resolveQuestionTargetNodes(ch, null)
  return { target, matchedNodeIds }
}

// ============ 各题型绑定表 ============

const redoxBindings: CategoryBindings = {
  'redox.read-problem': s1Conclusion,
  'redox.match-answer': s5Conclusion,
  'redox.list-particles': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.stage1?.particles),
  'redox.mark-valence-species': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.stage1?.candidates),
  'redox.judge-medium': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.stage1?.mediumRules),
  'redox.judge-order': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.stage1?.order ?? (ch.solveData as RedoxSolveData | undefined)?.order),
  'redox.infer-products': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.stage1?.products),
  'redox.mark-valence': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.halfReactions),
  'redox.count-electrons': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.halfReactions),
  'redox.lcm': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.balance),
  'redox.balance': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.balance),
  'redox.patch-ho': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.reaction?.ionicEquation),
  'redox.verify': (ch) => pick((ch.solveData as RedoxSolveData | undefined)?.reaction?.equation),
}

const ionicBindings: CategoryBindings = {
  'ionic.read-problem': s1Conclusion,
  'ionic.match-answer': s5Conclusion,
  'ionic.list-species': (ch) => pick((ch.solveData as IonicSolveData | undefined)?.species),
  'ionic.write-conservation': (ch) => pick((ch.solveData as IonicSolveData | undefined)?.equations),
  'ionic.compare-concentration': (ch) => pick((ch.solveData as IonicSolveData | undefined)?.comparison),
  'ionic.verify': (ch) => pick((ch.solveData as IonicSolveData | undefined)?.equations),
}

const equilibriumBindings: CategoryBindings = {
  'eq.read-problem': s1Conclusion,
  'eq.match-answer': s5Conclusion,
  'eq.write-reaction': (ch) => pick((ch.solveData as EquilibriumSolveData | undefined)?.reaction),
  'eq.identify-condition': (ch) => pick((ch.solveData as EquilibriumSolveData | undefined)?.factors),
  'eq.infer-shift': (ch) => pick((ch.solveData as EquilibriumSolveData | undefined)?.factors),
  'eq.analyze-k': (ch) => pick((ch.solveData as EquilibriumSolveData | undefined)?.result),
}

const electrochemBindings: CategoryBindings = {
  'ec.read-problem': s1Conclusion,
  'ec.match-answer': s5Conclusion,
  'ec.identify-device': (ch) => pick((ch.solveData as ElectrochemSolveData | undefined)?.device),
  'ec.write-half-reactions': (ch) => {
    const sd = ch.solveData as ElectrochemSolveData | undefined
    return pick(sd?.halfReactions ?? sd?.totalEquation)
  },
  'ec.analyze-phenomena': (ch) => {
    const sd = ch.solveData as ElectrochemSolveData | undefined
    return pick(sd?.phenomena ?? sd?.conclusion)
  },
  'ec.compute': (ch) => pick((ch.solveData as ElectrochemSolveData | undefined)?.calc),
}

const inorganicBindings: CategoryBindings = {
  'inf.read-problem': s1Conclusion,
  'inf.match-answer': s5Conclusion,
  'inf.breakthrough': (ch) => pick((ch.solveData as InferenceSolveData | undefined)?.breakthrough),
  'inf.forward-backward': (ch) => pick((ch.solveData as InferenceSolveData | undefined)?.reasoning),
  'inf.verify': (ch) => pick((ch.solveData as InferenceSolveData | undefined)?.verification),
}

const organicBindings: CategoryBindings = {
  'org.read-problem': s1Conclusion,
  'org.match-answer': s5Conclusion,
  'org.identify-fg': (ch) => pick((ch.solveData as OrganicSolveData | undefined)?.functionalGroups),
  'org.determine-reaction': (ch) => pick((ch.solveData as OrganicSolveData | undefined)?.reactions),
  'org.write-product': (ch) => pick((ch.solveData as OrganicSolveData | undefined)?.product),
  'org.verify': () => null, // 价键/不饱和度校验无专属 solveData 字段，安全降级
}

/** 题型 → 节点绑定表 */
export const NODE_BINDINGS: Record<ChemCategory, CategoryBindings> = {
  RedoxReaction: redoxBindings,
  IonicEquilibrium: ionicBindings,
  ChemicalEquilibrium: equilibriumBindings,
  Electrochemistry: electrochemBindings,
  InorganicInference: inorganicBindings,
  OrganicChemistry: organicBindings,
}

/**
 * 解析某道题某节点的实例结论。
 * @param previewSafe 为 true 时恒返回 null（preview 模式不读 solveData，不泄答案）。
 * @returns 实例结论；缺失 / 无绑定 / 越界 → null（不抛错）。
 */
export const resolveNodeConclusion = (
  challenge: Challenge,
  nodeId: string,
  previewSafe = false,
): NodeConclusion => {
  if (previewSafe) return null
  if (!challenge || typeof nodeId !== 'string') return null
  const bindings = NODE_BINDINGS[challenge.category]
  const resolver = bindings?.[nodeId]
  if (!resolver) return null
  try {
    return resolver(challenge)
  } catch {
    // 防御：解析器异常（如 solveData 形状异常）也不抛错，安全降级
    return null
  }
}

/** 某节点本题是否有实例结论（非 null） */
export const hasNodeConclusion = (challenge: Challenge, nodeId: string): boolean =>
  resolveNodeConclusion(challenge, nodeId) !== null
