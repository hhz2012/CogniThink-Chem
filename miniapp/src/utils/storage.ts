import Taro from '@tarojs/taro'
import { UserProgress, Thought } from '../types/chem'

const STORAGE_KEY = 'chem-challenge-progress-v1'

export const THOUGHT_META: Record<Thought, { label: string; icon: string; color: string; desc: string }> = {
  Conservation: { label: '守恒思想', icon: '⚖️', color: '#dc2626', desc: '抓住反应前后不变的量——原子、电子、电荷、质量，以不变应万变。' },
  Structure: { label: '结构决定性质', icon: '🔬', color: '#ea580c', desc: '从微观结构（电子排布、化学键、空间构型）推导宏观性质与反应规律。' },
  Equilibrium: { label: '动态平衡', icon: '🔄', color: '#2563eb', desc: '平衡是动态的，外界条件改变时平衡移动，用勒夏特列原理判断方向。' },
  Classification: { label: '分类比较', icon: '🗂️', color: '#16a34a', desc: '将物质/反应分门别类，通过同类比较与递变规律快速推断未知性质。' },
  ModelCognition: { label: '证据推理·模型认知', icon: '🧩', color: '#7c3aed', desc: '基于实验证据建构模型，用模型解释现象并预测结果。' },
}

export const defaultProgress: UserProgress = {
  solvedIds: [],
  wrongIds: [],
  thoughtCounts: { Conservation: 0, Structure: 0, Equilibrium: 0, Classification: 0, ModelCognition: 0 },
  inspirations: 10,
}

export function loadProgress(): UserProgress {
  try {
    const raw = Taro.getStorageSync(STORAGE_KEY)
    if (raw) return { ...defaultProgress, ...raw }
  } catch (_) {
    // ignore
  }
  return { ...defaultProgress }
}

export function saveProgress(p: UserProgress): void {
  Taro.setStorageSync(STORAGE_KEY, p)
}

export function resetProgress(): void {
  Taro.setStorageSync(STORAGE_KEY, defaultProgress)
}

export function markSolved(p: UserProgress, challengeId: string, thought: Thought): UserProgress {
  if (p.solvedIds.includes(challengeId)) return p
  const next: UserProgress = {
    ...p,
    solvedIds: [...p.solvedIds, challengeId],
    thoughtCounts: {
      ...p.thoughtCounts,
      [thought]: p.thoughtCounts[thought] + 1,
    },
    inspirations: p.inspirations + 2, // 答对奖励 2 灵感值
  }
  saveProgress(next)
  return next
}

/** 记录错题（答错时入册），已存在则不重复 */
export function markWrong(p: UserProgress, challengeId: string): UserProgress {
  if (p.wrongIds.includes(challengeId)) return p
  const next: UserProgress = { ...p, wrongIds: [...p.wrongIds, challengeId] }
  saveProgress(next)
  return next
}

/** 从错题本移除（重做做对 / 手动移除） */
export function unmarkWrong(p: UserProgress, challengeId: string): UserProgress {
  if (!p.wrongIds.includes(challengeId)) return p
  const next: UserProgress = { ...p, wrongIds: p.wrongIds.filter((id) => id !== challengeId) }
  saveProgress(next)
  return next
}

export function consumeInspiration(p: UserProgress): UserProgress | null {
  if (p.inspirations <= 0) return null
  const next = { ...p, inspirations: p.inspirations - 1 }
  saveProgress(next)
  return next
}
