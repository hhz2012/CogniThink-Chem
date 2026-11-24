// 本题 DAG 节点跳过判定（R4 Task 3 补全：skipWhen 运行时求值）
// 单一真值源：engine/thoughtChainBindings.ts 的 NODE_BINDINGS。
// 规则（不编造、不引入新化学推理）：节点声明了 skipWhen 且本题该节点无实例结论 → 跳过。
// 4 个 skipWhen 键的结论均取自 solveData 某字段，字段缺失即代表本题不需该步：
//   - redox.judge-order   single-pair            ← stage1.order 缺失（单一氧还对，无需判先后）
//   - redox.patch-ho      no-ionic-equation      ← reaction.ionicEquation 缺失（非离子方程式，无需补氢氧）
//   - ionic.verify        only-concentration-rank ← equations 缺失（只比浓度大小，无守恒式可校验）
//   - ec.compute          no-calc                ← calc 缺失（非计算题，无需计算段）
// 若某题该步数据缺失但本应执行，图上灰显提示数据缺口——这是数据状态的真实反映，不假装执行。

import type { Challenge } from '../types/chem'
import { getCategoryDAG } from './thoughtChainDAGs'
import { hasNodeConclusion } from './thoughtChainBindings'

/** 取本题被跳过的 DAG 节点 id 集合（异常降级返回空集，不抛错） */
export const resolveSkippedNodeIds = (challenge: Challenge): Set<string> => {
  const skipped = new Set<string>()
  if (!challenge) return skipped
  let nodes
  try {
    nodes = getCategoryDAG(challenge.category)?.nodes
  } catch {
    return skipped
  }
  if (!nodes) return skipped
  for (const node of nodes) {
    if (node.skipWhen && !hasNodeConclusion(challenge, node.id)) {
      skipped.add(node.id)
    }
  }
  return skipped
}

/** 单节点判定便利函数 */
export const isNodeSkipped = (challenge: Challenge, nodeId: string): boolean =>
  resolveSkippedNodeIds(challenge).has(nodeId)
