import { ReactionEntry } from '../../types/reaction'

// 离子平衡（电离 / 水解）+ 热化学：可逆平衡移动与反应热真值。
// 溯源后端 ReactionEntry（同 id / 同方程式）；守恒式与 ΔH 取自教材确定值。
const equilibrium: ReactionEntry[] = [
  // ============ 离子平衡 ============
  // 醋酸电离（弱酸部分电离）
  {
    id: 'RX-PL-ION-001',
    tags: ['电离', '弱电解质'],
    equation: 'CH₃COOH ⇌ CH₃COO⁻ + H⁺',
    conditions: [],
    solveData: {
      kind: 'reaction-ion-equilibrium',
      equation: 'CH₃COOH ⇌ CH₃COO⁻ + H⁺',
      equilibriumType: '电离',
      equilibriumEquation: 'CH₃COOH ⇌ CH₃COO⁻ + H⁺',
      shiftFactors: [
        { factor: '升温', change: '升高温度', direction: '正向' },
        { factor: '加水', change: '稀释', direction: '正向' },
        { factor: '加 CH₃COONa', change: '同离子效应（c(CH₃COO⁻) 增大）', direction: '逆向' },
      ],
      conservations: [
        { type: '电荷', expression: 'c(H⁺) = c(CH₃COO⁻) + c(OH⁻)' },
        { type: '物料', expression: 'c(CH₃COOH) + c(CH₃COO⁻) = c₀' },
        { type: '质子', expression: 'c(H⁺) = c(CH₃COO⁻) + c(OH⁻)' },
      ],
    },
  },
  // 铵根水解（溶液显酸性）
  {
    id: 'RX-PL-HYD-001',
    tags: ['水解', '强酸弱碱盐'],
    equation: 'NH₄⁺ + H₂O ⇌ NH₃·H₂O + H⁺',
    conditions: [],
    solveData: {
      kind: 'reaction-ion-equilibrium',
      equation: 'NH₄⁺ + H₂O ⇌ NH₃·H₂O + H⁺',
      equilibriumType: '水解',
      equilibriumEquation: 'NH₄⁺ + H₂O ⇌ NH₃·H₂O + H⁺',
      shiftFactors: [
        { factor: '升温', change: '升高温度', direction: '正向' },
        { factor: '加水', change: '稀释', direction: '正向' },
        { factor: '加 NH₄Cl', change: '同离子效应（c(NH₄⁺) 增大）', direction: '逆向' },
      ],
      conservations: [
        { type: '电荷', expression: 'c(NH₄⁺) + c(H⁺) = c(Cl⁻) + c(OH⁻)' },
        { type: '物料', expression: 'c(NH₄⁺) + c(NH₃·H₂O) = c(Cl⁻)' },
      ],
    },
  },

  // ============ 热化学 ============
  // 氢气燃烧的热化学方程式
  {
    id: 'RX-PL-THM-001',
    tags: ['热化学', '放热', '燃烧热'],
    equation: '2H₂ + O₂ = 2H₂O',
    conditions: ['点燃'],
    phenomenon: 'ΔH = -571.6 kJ/mol（H₂O 为液态）',
    solveData: {
      kind: 'reaction-thermochemical',
      equation: '2H₂ + O₂ = 2H₂O',
      conditions: ['点燃'],
      deltaH: '-571.6 kJ/mol（H₂O 为液态）',
      states: { 'H₂': 'g', 'O₂': 'g', 'H₂O': 'l' },
      calcSteps: ['燃烧为放热反应，ΔH < 0', '2 mol H₂ 完全燃烧生成液态水，放出 571.6 kJ 热量'],
    },
  },
  // 稀盐酸与稀氢氧化钠中和的热化学方程式
  {
    id: 'RX-PL-THM-003',
    tags: ['热化学', '放热', '中和热'],
    equation: 'HCl + NaOH = NaCl + H₂O',
    conditions: [],
    phenomenon: 'ΔH = -57.3 kJ/mol（稀强酸强碱中和热）',
    solveData: {
      kind: 'reaction-thermochemical',
      equation: 'HCl + NaOH = NaCl + H₂O',
      deltaH: '-57.3 kJ/mol（稀强酸强碱中和热）',
      states: { HCl: 'aq', NaOH: 'aq', NaCl: 'aq', 'H₂O': 'l' },
      calcSteps: ['稀强酸强碱中和生成 1 mol 水，放出 57.3 kJ', '中和热 ΔH = -57.3 kJ/mol，与酸碱用量无关'],
    },
  },
]

export default equilibrium
