import { Chapter, Challenge } from '../types/chem'
import redox from './challenges/redox'
import ionicEq from './challenges/ionic'
import chemEq from './challenges/equilibrium'
import electrochem from './challenges/electrochemistry'
import inorganic from './challenges/inorganic'
import organic from './challenges/organic'

// ============================================================
// 高考化学六大难题专项 · 逐级挑战题库（聚合器）
// 各专项题目数据分模块存放在 ./challenges/ 目录下
// 每个专项 14 题（基础→进阶→综合→难题→压轴），共 84 题
// ============================================================

export const chapters: Chapter[] = [
  {
    id: 'ch-redox', name: '氧化还原反应', icon: '⚡', category: 'RedoxReaction',
    color: '#dc2626', desc: '电子守恒·配平·产物推断', challenges: redox,
  },
  {
    id: 'ch-ion', name: '水溶液离子平衡', icon: '💧', category: 'IonicEquilibrium',
    color: '#2563eb', desc: '电离·水解·离子浓度比较', challenges: ionicEq,
  },
  {
    id: 'ch-eq', name: '化学平衡', icon: '🔄', category: 'ChemicalEquilibrium',
    color: '#ea580c', desc: '平衡移动·K·Ksp·等效平衡', challenges: chemEq,
  },
  {
    id: 'ch-elec', name: '电化学综合', icon: '🔋', category: 'Electrochemistry',
    color: '#7c3aed', desc: '原电池·电解池·综合计算', challenges: electrochem,
  },
  {
    id: 'ch-inorg', name: '元素化合物推断', icon: '🔬', category: 'InorganicInference',
    color: '#16a34a', desc: '框图推断·离子检验·工业流程', challenges: inorganic,
  },
  {
    id: 'ch-org', name: '有机化学', icon: '🧬', category: 'OrganicChemistry',
    color: '#0891b2', desc: '官能团·反应类型·合成推断', challenges: organic,
  },
]

export const allChallenges: Challenge[] = chapters.flatMap((c) => c.challenges)

export function getChallengeById(id: string): Challenge | undefined {
  return allChallenges.find((c) => c.id === id)
}
