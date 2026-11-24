// DAG 不变量校验纯函数（R4 Task 2）
// 校验一张 ThoughtChainDAG 的结构合法性，供发布前校验 / 单测使用。
// 纯函数：不就地修改、不抛错；非法输入返回 { ok:false, errors:[...] }。
// 校验项：
//   1) 节点 id 唯一；
//   2) 所有 dependsOn 引用的 id 均存在；
//   3) 至少一个根节点（dependsOn 为空）；
//   4) 有向无环（拓扑排序存在）；
//   5) index 线性化不破坏依赖序（若 A dependsOn B，则 index(B) < index(A)）。

import type { DAGValidationResult, ThoughtChainDAG, ThoughtChainNode } from '../types/thoughtChain'

export const validateDAG = (dag: ThoughtChainDAG | null | undefined): DAGValidationResult => {
  const errors: string[] = []

  if (!dag) {
    return { ok: false, errors: ['DAG 为 null 或 undefined'] }
  }
  if (!Array.isArray(dag.nodes)) {
    return { ok: false, errors: ['DAG.nodes 不是数组'] }
  }
  if (dag.nodes.length === 0) {
    return { ok: false, errors: ['DAG.nodes 为空'] }
  }

  const nodes = dag.nodes
  const idSet = new Set<string>()
  const byId = new Map<string, ThoughtChainNode>()

  // 1) id 唯一 + 建索引
  for (const n of nodes) {
    if (!n || typeof n.id !== 'string' || n.id.length === 0) {
      errors.push(`存在非法节点：缺少 id`)
      continue
    }
    if (idSet.has(n.id)) {
      errors.push(`节点 id 重复：${n.id}`)
    }
    idSet.add(n.id)
    byId.set(n.id, n)
  }

  // 2) dependsOn 引用存在；收集依赖关系
  const adj = new Map<string, string[]>() // nodeId -> 其依赖的前置 id 列表
  for (const n of nodes) {
    const deps = Array.isArray(n.dependsOn) ? n.dependsOn : []
    adj.set(n.id, deps)
    for (const d of deps) {
      if (!byId.has(d)) {
        errors.push(`节点 ${n.id} 的 dependsOn 引用了不存在的 id：${d}`)
      }
    }
  }

  // 3) 至少一个根节点
  const roots = nodes.filter((n) => Array.isArray(n.dependsOn) && n.dependsOn.length === 0)
  if (roots.length === 0) {
    errors.push('不存在根节点（所有节点 dependsOn 都非空）')
  }

  // 4) 有向无环（Kahn 算法：反复移除入度为 0 的节点）
  // 入度 = dependsOn 中存在的引用数
  const indeg = new Map<string, number>()
  for (const n of nodes) {
    let cnt = 0
    for (const d of adj.get(n.id) ?? []) {
      if (byId.has(d)) cnt++
    }
    indeg.set(n.id, cnt)
  }
  const queue: string[] = []
  for (const n of nodes) {
    if ((indeg.get(n.id) ?? 0) === 0) queue.push(n.id)
  }
  let visited = 0
  while (queue.length > 0) {
    const cur = queue.shift()!
    visited++
    // 找所有依赖 cur 的节点（反向），入度 -1
    for (const n of nodes) {
      if ((adj.get(n.id) ?? []).includes(cur)) {
        const d = (indeg.get(n.id) ?? 0) - 1
        indeg.set(n.id, d)
        if (d === 0) queue.push(n.id)
      }
    }
  }
  if (visited < nodes.length) {
    errors.push(`存在环：仅 ${visited}/${nodes.length} 个节点可被拓扑排序`)
  }

  // 5) index 不破坏依赖序
  for (const n of nodes) {
    const deps = adj.get(n.id) ?? []
    for (const d of deps) {
      const dep = byId.get(d)
      if (!dep) continue // 已在 2) 报错
      if (typeof dep.index !== 'number' || typeof n.index !== 'number') continue
      if (dep.index >= n.index) {
        errors.push(`依赖序与 index 冲突：节点 ${n.id}(index=${n.index}) 依赖 ${d}(index=${dep.index})，但前置 index 应更小`)
      }
    }
  }

  return { ok: errors.length === 0, errors }
}

/** 批量校验一组 DAG，返回首个失败或全部通过 */
export const validateDAGs = (dags: ThoughtChainDAG[]): DAGValidationResult => {
  const all: DAGValidationResult[] = (dags ?? []).map(validateDAG)
  const failed = all.filter((r) => !r.ok)
  if (failed.length === 0) return { ok: true, errors: [] }
  return { ok: false, errors: failed.flatMap((r) => r.errors) }
}
