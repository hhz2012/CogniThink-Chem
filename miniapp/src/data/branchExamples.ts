// Task 7 教材代表例：条件 / 数量分支真值（可溯源到教材，不凭空编造）
// 例 1：NO₃⁻ / Fe²⁺ —— 有 H⁺ 发生氧化还原，无 H⁺ 不反应（条件分支）
// 例 2：Cl₂ / FeI₂ —— Cl₂ 少量只氧化 I⁻，过量则 I⁻、Fe²⁺ 均被氧化（数量分支，还原性 I⁻ > Fe²⁺ > Br⁻）
// 每条结论按 A–E 原子任务组织，供 resolveBranch 切换条件时返回不同结论集（AC-7 / TR-7.1）。

import type { ReactionEntry } from '../types/reaction'

/** 教材代表例 1：NO₃⁻ / Fe²⁺（条件分支：有 / 无 H⁺ 走向不同） */
export const NO3_FE2_ENTRY: ReactionEntry = {
  id: 'BR-NO3-FE2',
  reactionType: 'Redox',
  tags: ['氧化还原', '条件分支', '硝酸根氧化性'],
  equation: 'Fe²⁺ + NO₃⁻（± H⁺）',
  phenomenon: '酸性下生成无色 NO，遇空气变红棕色；无 H⁺ 时无明显反应',
  branches: [
    {
      key: 'acid',
      label: '酸性（有 H⁺）',
      when: { medium: '酸性' },
      conclusion: {
        A: '体系粒子：Fe²⁺、NO₃⁻、H⁺、H₂O',
        B: '变价角色：Fe²⁺ 是还原剂（Fe +2→+3，失 e⁻）；NO₃⁻ 中 N 是氧化剂（N +5→+2，得 e⁻）；H⁺ 不变价',
        C: '约束条件：酸性环境（有 H⁺）是 NO₃⁻ 表现强氧化性的前提',
        D: '竞争排序：单一氧还对，无竞争',
        E: '产物：3Fe²⁺ + NO₃⁻ + 4H⁺ = 3Fe³⁺ + NO↑ + 2H₂O；生成 Fe³⁺ 和无色 NO',
      },
    },
    {
      key: 'neutral',
      label: '无 H⁺（中性）',
      when: { medium: '中性' },
      conclusion: {
        A: '体系粒子：Fe²⁺、NO₃⁻、H₂O（无 H⁺）',
        B: '变价角色：无变价物种发生反应（N 仍 +5、Fe 仍 +2）',
        C: '约束条件：缺 H⁺，NO₃⁻ 无强氧化性',
        D: '竞争排序：无反应发生，无排序',
        E: '结论：不发生氧化还原，Fe²⁺ 与 NO₃⁻ 可共存（Fe²⁺ 水解但不与 NO₃⁻ 反应）',
      },
    },
  ],
}

/** 教材代表例 2：Cl₂ / FeI₂（数量分支：Cl₂ 少量 / 过量产物不同） */
export const CL2_FEI2_ENTRY: ReactionEntry = {
  id: 'BR-CL2-FEI2',
  reactionType: 'Redox',
  tags: ['氧化还原', '数量分支', '竞争排序', '还原性顺序'],
  equation: 'Cl₂ + FeI₂',
  phenomenon: '少量 Cl₂ 出现 I₂ 的颜色；过量 Cl₂ 则 Fe²⁺ 也被氧化为 Fe³⁺',
  branches: [
    {
      key: 'little',
      label: 'Cl₂ 少量',
      when: { quantity: '少量' },
      conclusion: {
        A: '体系粒子：Fe²⁺、I⁻、Cl₂',
        B: '变价角色：I⁻、Fe²⁺ 均为可被氧化的还原剂；Cl₂ 是氧化剂',
        C: '约束条件：Cl₂ 用量为少量',
        D: '竞争排序：还原性 I⁻ > Fe²⁺，Cl₂ 优先氧化 I⁻',
        E: '产物：Cl₂ + 2I⁻ = 2Cl⁻ + I₂；只生成 I₂，Fe²⁺ 未被氧化',
      },
    },
    {
      key: 'excess',
      label: 'Cl₂ 过量',
      when: { quantity: '过量' },
      conclusion: {
        A: '体系粒子：Fe²⁺、I⁻、Cl₂（足量）',
        B: '变价角色：I⁻、Fe²⁺ 均被氧化；Cl₂ 是氧化剂',
        C: '约束条件：Cl₂ 用量为过量',
        D: '竞争排序：先氧化 I⁻ 再氧化 Fe²⁺（还原性 I⁻ > Fe²⁺）',
        E: '产物：2FeI₂ + 3Cl₂ = 2FeCl₃ + 2I₂；I⁻ 与 Fe²⁺ 均被氧化为 I₂ 和 Fe³⁺',
      },
    },
  ],
}

/** 全部分支代表例（供遍历计数 / 校验使用） */
export const BRANCH_EXAMPLES: ReactionEntry[] = [NO3_FE2_ENTRY, CL2_FEI2_ENTRY]
