// DAG 门控状态机（R4 Task 6）：把现有 A–F 线性门控泛化到推导图 DAG。
// 与 engine/scaffoldChains.ts 的线性门控并存、不互相替换；语义对齐 StepGateState。
// 解锁规则：节点在其全部 dependsOn 为 passed（或被 skipWhen 跳过视为 passed）时变为 unlocked。
// 纯函数：不就地修改、不抛错；未知 / 越界节点返回安全值（'locked' 或原状态副本）。

import type { StepGateState } from '../types/scaffold'
import type { ThoughtChainDAG, ThoughtChainNode } from '../types/thoughtChain'

/** 节点 id → 门控状态 */
export type DAGGateStates = Record<string, StepGateState>

/** 跳过条件求值：返回 true 表示该节点的 skipWhen 命中，视为已过（不渲染、不阻塞下游） */
export type SkipResolver = (node: ThoughtChainNode) => boolean

const findNode = (dag: ThoughtChainDAG, id: string): ThoughtChainNode | undefined =>
  dag.nodes.find((n) => n.id === id)

/**
 * 重算解锁状态（纯函数，返回新对象）：
 * - 被 skip 的节点 → 'passed'（视为已过）；
 * - 'locked' 节点若全部前置依赖 passed（或被 skip）→ 'unlocked'；
 * - 其余状态不变。
 */
export const recomputeUnlocks = (
  dag: ThoughtChainDAG,
  states: DAGGateStates,
  skipResolver?: SkipResolver,
): DAGGateStates => {
  const next: DAGGateStates = { ...states }
  const isDepDone = (depId: string): boolean => {
    const dep = findNode(dag, depId)
    if (!dep) return false
    if (skipResolver && skipResolver(dep)) return true
    return next[depId] === 'passed'
  }
  for (const n of dag.nodes) {
    if (skipResolver && skipResolver(n)) {
      next[n.id] = 'passed'
      continue
    }
    if (next[n.id] === 'locked') {
      const deps = n.dependsOn ?? []
      if (deps.every(isDepDone)) next[n.id] = 'unlocked'
    }
  }
  return next
}

/** 初始门控：根节点 unlocked，其余 locked；被 skip 的节点置 passed */
export const initialDagGateStates = (
  dag: ThoughtChainDAG,
  skipResolver?: SkipResolver,
): DAGGateStates => {
  const states: DAGGateStates = {}
  for (const n of dag.nodes) states[n.id] = 'locked'
  return recomputeUnlocks(dag, states, skipResolver)
}

/** 安全读取某节点状态；未知 id 返回 'locked'（不抛错） */
export const getDagGateState = (states: DAGGateStates, nodeId: string): StepGateState =>
  states[nodeId] ?? 'locked'

/** 某节点当前是否可达（非 locked）；内部会重算解锁以反映最新依赖状态 */
export const isDagUnlocked = (
  dag: ThoughtChainDAG,
  states: DAGGateStates,
  nodeId: string,
  skipResolver?: SkipResolver,
): boolean => {
  if (!findNode(dag, nodeId)) return false
  const s = recomputeUnlocks(dag, states, skipResolver)
  return s[nodeId] !== 'locked'
}

/** 标记某节点 passed 并重算下游解锁；locked / 已 passed / 未知节点返回原状态副本 */
export const markDagPassed = (
  states: DAGGateStates,
  dag: ThoughtChainDAG,
  nodeId: string,
  skipResolver?: SkipResolver,
): DAGGateStates => {
  if (!findNode(dag, nodeId)) return { ...states }
  const cur = states[nodeId] ?? 'locked'
  if (cur === 'locked' || cur === 'passed') return { ...states }
  return recomputeUnlocks(dag, { ...states, [nodeId]: 'passed' }, skipResolver)
}

/** 标记某节点 failed（仅当当前 unlocked）；其余返回原状态副本 */
export const markDagFailed = (
  states: DAGGateStates,
  dag: ThoughtChainDAG,
  nodeId: string,
): DAGGateStates => {
  if (!findNode(dag, nodeId)) return { ...states }
  const cur = states[nodeId] ?? 'locked'
  if (cur !== 'unlocked') return { ...states }
  return { ...states, [nodeId]: 'failed' }
}

/** 重试：failed → unlocked，并重算解锁；其余返回原状态副本 */
export const resetDagStep = (
  states: DAGGateStates,
  dag: ThoughtChainDAG,
  nodeId: string,
  skipResolver?: SkipResolver,
): DAGGateStates => {
  if (!findNode(dag, nodeId)) return { ...states }
  const cur = states[nodeId] ?? 'locked'
  if (cur !== 'failed') return { ...states }
  return recomputeUnlocks(dag, { ...states, [nodeId]: 'unlocked' }, skipResolver)
}
