import { ReactionEntry } from '../../types/reaction'

// 电极反应：原电池 / 电解池的半反应结构化真值。
// 溯源：后端 KB 无独立 Electrode 主类型，电极条目挂在 RX-PL-ELC-*（Displacement/Decomposition/Redox）下，
// 半反应取自条目结构化 HalfReactions 字段（同 id / 同方程式），在前端归类为 reaction-electrode。
const electrochem: ReactionEntry[] = [
  // 锌铜原电池（CuSO₄ 溶液）
  {
    id: 'RX-PL-ELC-001',
    reactionType: 'Electrode',
    tags: ['原电池', '电极反应'],
    equation: 'Zn + CuSO₄ = ZnSO₄ + Cu',
    conditions: [],
    phenomenon: '铜片表面析出红色固体',
    solveData: {
      kind: 'reaction-electrode',
      equation: 'Zn + CuSO₄ = ZnSO₄ + Cu',
      deviceType: '原电池',
      totalEquation: 'Zn + CuSO₄ = ZnSO₄ + Cu',
      halfReactions: [
        { electrode: '负极', equation: 'Zn - 2e⁻ = Zn²⁺', role: '氧化' },
        { electrode: '正极', equation: 'Cu²⁺ + 2e⁻ = Cu', role: '还原' },
      ],
    },
  },
  // 电解氯化铜溶液
  {
    id: 'RX-PL-ELC-003',
    reactionType: 'Electrode',
    tags: ['电解池', '电极反应'],
    equation: 'CuCl₂ = Cu + Cl₂↑',
    conditions: ['通电'],
    phenomenon: '阴极析出红色固体，阳极产生黄绿色气体',
    solveData: {
      kind: 'reaction-electrode',
      equation: 'CuCl₂ = Cu + Cl₂↑',
      conditions: ['通电'],
      deviceType: '电解池',
      totalEquation: 'CuCl₂ = Cu + Cl₂↑',
      halfReactions: [
        { electrode: '阴极', equation: 'Cu²⁺ + 2e⁻ = Cu', role: '还原' },
        { electrode: '阳极', equation: '2Cl⁻ - 2e⁻ = Cl₂↑', role: '氧化' },
      ],
    },
  },
]

export default electrochem
