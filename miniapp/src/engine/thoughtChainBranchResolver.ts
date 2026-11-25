// R4 后继：DAG 节点 branchKey 的运行时接线（把题型 DAG 的条件分支接通到现有反应侧分支引擎）。
//
// 背景（见 spec 设计 E 第 2 条）：节点 branchKey 非空时，用现有 resolveBranch(entry, conditions)
// 选中该分支的结论集；无匹配 / 无分支数据 → 安全降级为空，不抛错、不泄答案。
//
// 数据现实（决定本模块的降级策略）：
//   - 反应侧分支真值 ScaffoldBranch 挂在 ReactionEntry.branches 上（data/reactions 目前为空，
//     真实分支数据仅存在于 data/branchExamples 的 2 条教材代表例，when 键为 medium / quantity）。
//   - Challenge 本身不携带 branches；可从 solveData 精确提取的 BranchConditions 只有
//     氧化还原 stage1.medium 与化学平衡 factors[factor==='温度'].change（均为原值搬运，不解释、不编造）。
//   - 其余 branchKey（device-type / solute-type / condition-type / subtype / organic-subtype）
//     在 BranchConditions 中无对应字段，属「题型原生轴」——本题结论已由 solveData 经 NODE_BINDINGS 给出，
//     不走 resolveBranch（返回 null），避免把抽象轴误塞给反应侧分支引擎。
//
// 纯函数纪律：不就地修改、不抛错、越界返回安全空值；previewSafe 恒 null（零答案泄露）。
// 纯加性：不修改现有类型签名、不改现有消费方形状；现有运行时按构造零触碰。

import type { Challenge, RedoxSolveData, EquilibriumSolveData } from '../types/chem'
import type { BranchConditions, BranchResult } from '../types/scaffold'
import type { NodeConclusion } from './thoughtChainBindings'
import { resolveNodeConclusion } from './thoughtChainBindings'
import { resolveBranch, type BranchableEntry } from './scaffoldBranches'
import { getCategoryDAG } from './thoughtChainDAGs'

/**
 * DAG branchKey → BranchConditions 字段（反应侧分支引擎能匹配的键）。
 * 空数组 = 题型原生轴，其结论已由 solveData 给出，不走 resolveBranch。
 * 新增 branchKey 时在此登记其对应的条件字段（无对应则留空数组）。
 */
export const BRANCH_KEY_CONDITION_KEYS: Record<string, (keyof BranchConditions)[]> = {
  medium: ['medium'],
  'temperature-changed': ['temperature'],
  quantity: ['quantity'],
  concentration: ['concentration'],
  // 以下为题型原生轴：BranchConditions 无对应字段，结论由 solveData 直接给出
  'device-type': [],
  'solute-type': [],
  'condition-type': [],
  subtype: [],
  'organic-subtype': [],
}

/**
 * 把节点 branchKey（单键或多键数组）归一化为 BranchConditions 字段集合（去重、保序）。
 * 未登记的键 → 忽略；无任何可映射字段 → 空数组（表示不走 resolveBranch）。
 */
export const branchKeyConditionKeys = (
  branchKey: string | string[] | undefined,
): (keyof BranchConditions)[] => {
  if (!branchKey) return []
  const keys = Array.isArray(branchKey) ? branchKey : [branchKey]
  const out: (keyof BranchConditions)[] = []
  for (const k of keys) {
    const mapped = BRANCH_KEY_CONDITION_KEYS[k]
    if (!mapped) continue
    for (const c of mapped) {
      if (!out.includes(c)) out.push(c)
    }
  }
  return out
}

/** 合并多份条件（后者覆盖前者），跳过 undefined / null，不抛错。 */
const mergeConditions = (...parts: (BranchConditions | undefined)[]): BranchConditions => {
  const out: BranchConditions = {}
  for (const p of parts) {
    if (!p) continue
    for (const k of Object.keys(p) as (keyof BranchConditions)[]) {
      const v = p[k]
      if (v !== undefined && v !== null) {
        // 联合键索引赋值：TS 无法跨字段收窄 out[k]，用断言保留运行时确定性（值必为 string|boolean）
        ;(out as Record<string, unknown>)[k] = v
      }
    }
  }
  return out
}

/**
 * 从 Challenge.solveData 精确提取 BranchConditions（原值搬运，不解释、不编造）。
 * 当前可提取：
 *   - 氧化还原：stage1.medium（'酸性'|'碱性'|'中性'）→ conditions.medium
 *   - 化学平衡：factors 中 factor==='温度' 的 change → conditions.temperature
 * 其余题型暂无可精确映射的字段；形状异常时返回已提取部分，不抛错。
 */
export const deriveBranchConditions = (challenge: Challenge): BranchConditions => {
  const out: BranchConditions = {}
  if (!challenge?.solveData) return out
  try {
    if (challenge.category === 'RedoxReaction') {
      const medium = (challenge.solveData as RedoxSolveData)?.stage1?.medium
      if (typeof medium === 'string') out.medium = medium
    } else if (challenge.category === 'ChemicalEquilibrium') {
      const factors = (challenge.solveData as EquilibriumSolveData)?.factors
      const tempFactor = factors?.find((f) => f.factor === '温度')
      if (tempFactor && typeof tempFactor.change === 'string') out.temperature = tempFactor.change
    }
  } catch {
    // solveData 形状异常：返回已提取部分，不抛错
  }
  return out
}

/**
 * 取 Challenge 关联的分支真值条目。
 * Challenge 目前不携带 branches（分支真值挂在 ReactionEntry，见 types/reaction.ts），
 * 故默认返回 null；调用方可通过 opts.branchEntry 传入关联的 ReactionEntry。
 * 未来若 Challenge 增加可选 branches 字段，可在此读取（保持纯加性）。
 */
export const getBranchEntryForChallenge = (_challenge: Challenge): BranchableEntry | null => null

/** resolveDagNodeBranch 的选项 */
export interface ResolveBranchOptions {
  /** 显式条件（优先级最高，覆盖 solveData 推导值） */
  conditions?: BranchConditions
  /** 关联的分支真值条目（如 ReactionEntry）；缺省时 Challenge 无分支 → 返回 null */
  branchEntry?: BranchableEntry | null
  /** preview 模式恒返回 null（不读分支结论，不泄答案） */
  previewSafe?: boolean
}

/**
 * 接线核心：解析某道题某节点的反应侧分支结论。
 * @returns 命中分支 → BranchResult（matched:true，含 conclusion）；
 *          节点无 branchKey / branchKey 为题型原生轴 / 无分支条目 / 未命中 →
 *          null 或 matched:false 的 BranchResult（安全降级，不抛错）；
 *          previewSafe → 恒 null。
 */
export const resolveDagNodeBranch = (
  challenge: Challenge,
  nodeId: string,
  opts: ResolveBranchOptions = {},
): BranchResult | null => {
  if (opts.previewSafe) return null
  if (!challenge || typeof nodeId !== 'string') return null
  const dag = getCategoryDAG(challenge.category)
  const node = dag?.nodes.find((n) => n.id === nodeId)
  if (!node) return null
  const condKeys = branchKeyConditionKeys(node.branchKey)
  if (condKeys.length === 0) return null // 无 branchKey 或题型原生轴：不走反应侧分支引擎
  const entry = opts.branchEntry ?? getBranchEntryForChallenge(challenge)
  if (!entry) return null // Challenge 无分支数据：安全降级
  const conditions = mergeConditions(deriveBranchConditions(challenge), opts.conditions)
  try {
    return resolveBranch(entry, conditions)
  } catch {
    return null
  }
}

/** 某节点本题的完整运行时实例：solveData 结论 + 反应侧分支结论。 */
export interface DagNodeInstance {
  /** solveData 解析的实例结论（缺失为 null；previewSafe 时恒 null） */
  conclusion: NodeConclusion
  /** 反应侧分支结论（无 branchKey / 无分支数据 / 未命中 → null 或 matched:false） */
  branch: BranchResult | null
}

/**
 * 实例化一个 DAG 节点：合并 solveData 结论与分支结论，供未来 UI 逐步展开消费。
 * previewSafe 时两部分均不泄答案（conclusion 恒 null、branch 恒 null）。
 */
export const instantiateDagNode = (
  challenge: Challenge,
  nodeId: string,
  opts: ResolveBranchOptions = {},
): DagNodeInstance => ({
  conclusion: resolveNodeConclusion(challenge, nodeId, opts.previewSafe ?? false),
  branch: resolveDagNodeBranch(challenge, nodeId, opts),
})
