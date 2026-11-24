// 条件 / 数量分支求值引擎（Task 7，纯函数，不接 UI）
// 按题面条件 / 用量从 ReactionEntry.branches 中选中匹配分支，返回该分支的 A–E 结论集；
// 无匹配分支时安全降级（返回空结论，不抛错）。
// 与 engine/scaffoldChains 的门控状态机解耦：本文件只管「选哪套结论」，门控管「能否进入下一步」。

import type { BranchConditions, BranchResult, ScaffoldBranch } from '../types/scaffold'

/** 可携带分支的条目（最小结构约束，便于传入 ReactionEntry 或任意带 branches 的对象） */
export interface BranchableEntry {
  branches?: ScaffoldBranch[]
}

/**
 * 判断输入条件是否命中某分支：when 中声明的每个字段都须与 conditions 对应字段相等。
 * - when 含某字段而 conditions 缺该字段（undefined）→ 不命中。
 * - when 为空对象 {} → 命中任意输入（默认分支兜底）。
 */
export const matchesBranch = (branch: ScaffoldBranch, conditions: BranchConditions): boolean => {
  for (const key of Object.keys(branch.when) as (keyof BranchConditions)[]) {
    if (conditions[key] !== branch.when[key]) return false
  }
  return true
}

/**
 * 按题面条件 / 用量选中分支，返回该分支的 A–E 结论集。
 * - 命中：返回 branches 中第一个匹配的分支（数据按优先级排列）。
 * - 无匹配 / entry 为 null 或 undefined / 无 branches：返回 { branch: null, conclusion: {}, matched: false }，不抛错。
 */
export const resolveBranch = (
  entry: BranchableEntry | null | undefined,
  conditions: BranchConditions,
): BranchResult => {
  const branches = entry?.branches ?? []
  const branch = branches.find((b) => matchesBranch(b, conditions)) ?? null
  return { branch, conclusion: branch?.conclusion ?? {}, matched: branch !== null }
}

/** 取某条目的全部分支（供 UI 展示分支选项）；无分支或 entry 为空时返回空数组，不抛错。 */
export const getBranches = (entry: BranchableEntry | null | undefined): ScaffoldBranch[] =>
  entry?.branches ?? []
