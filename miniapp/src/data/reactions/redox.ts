import { ReactionEntry } from '../../types/reaction'

// 氧化还原（归中 / 歧化 / 复杂氧还）：半反应与电子守恒配平真值。
// 真值溯源后端 ReactionEntry（同 id / 同方程式）；价态、电子数按配平式由教材确定。
const redox: ReactionEntry[] = [
  // 实验室制氯气：MnO₂ 氧化浓盐酸
  {
    id: 'RX-IN-CL-006',
    tags: ['氧化还原', '实验室制氯气'],
    equation: 'MnO₂ + 4HCl = MnCl₂ + Cl₂↑ + 2H₂O',
    conditions: ['△', '浓'],
    phenomenon: '产生黄绿色气体',
    solveData: {
      kind: 'reaction-redox',
      equation: 'MnO₂ + 4HCl = MnCl₂ + Cl₂↑ + 2H₂O',
      conditions: ['△', '浓'],
      halfReactions: [
        { species: 'Mn', from: 4, to: 2, electrons: 2, role: '还原', reactantFormula: 'MnO₂', productFormula: 'MnCl₂' },
        { species: 'Cl', from: -1, to: 0, electrons: 2, role: '氧化', reactantFormula: 'HCl', productFormula: 'Cl₂' },
      ],
      balance: {
        oxidizedCount: 2,
        reducedCount: 2,
        lcm: 2,
        ratio: '1:2',
        coefficients: [
          { formula: 'MnO₂', value: 1 },
          { formula: 'HCl', value: 4 },
          { formula: 'MnCl₂', value: 1 },
          { formula: 'Cl₂', value: 1 },
          { formula: 'H₂O', value: 2 },
        ],
      },
      oxidants: ['MnO₂'],
      reductants: ['HCl'],
      directionBasis: 'MnO₂ 氧化性强于 Cl₂，浓盐酸中 Cl⁻ 还原性增强，故加热下 MnO₂ 能氧化 Cl⁻ 制 Cl₂',
    },
  },
  // 亚铁离子被酸性高锰酸钾氧化（滴定经典）
  {
    id: 'RX-IN-ION-012',
    tags: ['氧化还原', '高锰酸钾'],
    equation: '5Fe²⁺ + MnO₄⁻ + 8H⁺ = 5Fe³⁺ + Mn²⁺ + 4H₂O',
    conditions: ['酸性'],
    phenomenon: '紫红色褪去',
    solveData: {
      kind: 'reaction-redox',
      equation: '5Fe²⁺ + MnO₄⁻ + 8H⁺ = 5Fe³⁺ + Mn²⁺ + 4H₂O',
      conditions: ['酸性'],
      halfReactions: [
        { species: 'Fe', from: 2, to: 3, electrons: 5, role: '氧化', reactantFormula: 'Fe²⁺', productFormula: 'Fe³⁺' },
        { species: 'Mn', from: 7, to: 2, electrons: 5, role: '还原', reactantFormula: 'MnO₄⁻', productFormula: 'Mn²⁺' },
      ],
      balance: {
        oxidizedCount: 5,
        reducedCount: 5,
        lcm: 5,
        ratio: '5:1',
        coefficients: [
          { formula: 'Fe²⁺', value: 5 },
          { formula: 'MnO₄⁻', value: 1 },
          { formula: 'H⁺', value: 8 },
          { formula: 'Fe³⁺', value: 5 },
          { formula: 'Mn²⁺', value: 1 },
          { formula: 'H₂O', value: 4 },
        ],
      },
      oxidants: ['MnO₄⁻'],
      reductants: ['Fe²⁺'],
      directionBasis: '酸性条件下 MnO₄⁻ 氧化性强于 Fe³⁺，故 MnO₄⁻ 能氧化 Fe²⁺；NO₃⁻ 等也需酸性才显强氧化性',
    },
  },
  // 铁离子氧化碘离子（离子共存经典）
  {
    id: 'RX-IN-ION-022',
    tags: ['氧化还原', '不相容'],
    equation: '2Fe³⁺ + 2I⁻ = 2Fe²⁺ + I₂',
    conditions: [],
    phenomenon: '溶液变棕黄',
    solveData: {
      kind: 'reaction-redox',
      equation: '2Fe³⁺ + 2I⁻ = 2Fe²⁺ + I₂',
      halfReactions: [
        { species: 'Fe', from: 3, to: 2, electrons: 2, role: '还原', reactantFormula: 'Fe³⁺', productFormula: 'Fe²⁺' },
        { species: 'I', from: -1, to: 0, electrons: 2, role: '氧化', reactantFormula: 'I⁻', productFormula: 'I₂' },
      ],
      balance: {
        oxidizedCount: 2,
        reducedCount: 2,
        lcm: 2,
        ratio: '1:1',
        coefficients: [
          { formula: 'Fe³⁺', value: 2 },
          { formula: 'I⁻', value: 2 },
          { formula: 'Fe²⁺', value: 2 },
          { formula: 'I₂', value: 1 },
        ],
      },
      oxidants: ['Fe³⁺'],
      reductants: ['I⁻'],
      directionBasis: 'Fe³⁺ 氧化性强于 I₂，I⁻ 还原性强于 Fe²⁺，故反应正向进行；体现 Fe³⁺ 与 I⁻ 不能共存',
    },
  },
]

export default redox
