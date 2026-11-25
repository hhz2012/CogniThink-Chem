import { ReactionEntry } from '../../types/reaction'

// 无机基本反应类型：化合 / 分解 / 置换 / 复分解 / 燃烧
// 真值溯源后端 ReactionEntry（同 id / 同方程式）；方程式以易读排版呈现，条件与现象取自后端 KB。
const inorganic: ReactionEntry[] = [
  // ============ 化合反应（多变一） ============
  {
    id: 'RX-IN-NA-001',
    tags: ['化合', '氧化还原'],
    equation: '2Na + Cl₂ = 2NaCl',
    conditions: ['点燃'],
    phenomenon: '剧烈燃烧，产生大量白烟',
    solveData: {
      kind: 'reaction-combination',
      equation: '2Na + Cl₂ = 2NaCl',
      conditions: ['点燃'],
      phenomenon: '剧烈燃烧，产生大量白烟',
      reactantCount: 2,
      productCount: 1,
      isRedox: true,
      valenceChanges: ['Na：0 → +1', 'Cl：0 → -1'],
    },
  },
  {
    id: 'RX-IN-FE-007',
    tags: ['化合', '氧化还原', '强氧化剂'],
    equation: '2Fe + 3Cl₂ = 2FeCl₃',
    conditions: ['点燃'],
    phenomenon: '产生棕褐色烟',
    solveData: {
      kind: 'reaction-combination',
      equation: '2Fe + 3Cl₂ = 2FeCl₃',
      conditions: ['点燃'],
      phenomenon: '产生棕褐色烟',
      reactantCount: 2,
      productCount: 1,
      isRedox: true,
      valenceChanges: ['Fe：0 → +3', 'Cl：0 → -1'],
    },
  },
  {
    id: 'RX-IN-C-001',
    tags: ['化合', '氧化还原'],
    equation: 'C + O₂ = CO₂',
    conditions: ['点燃'],
    phenomenon: '发出白光，生成使石灰水变浑浊的气体',
    solveData: {
      kind: 'reaction-combination',
      equation: 'C + O₂ = CO₂',
      conditions: ['点燃'],
      phenomenon: '发出白光，生成使石灰水变浑浊的气体',
      reactantCount: 2,
      productCount: 1,
      isRedox: true,
      valenceChanges: ['C：0 → +4', 'O：0 → -2'],
    },
  },

  // ============ 分解反应（一变多） ============
  {
    id: 'RX-IN-HO-002',
    tags: ['分解', '电解'],
    equation: '2H₂O = 2H₂↑ + O₂↑',
    conditions: ['通电'],
    phenomenon: '两极产生气体',
    solveData: {
      kind: 'reaction-decomposition',
      equation: '2H₂O = 2H₂↑ + O₂↑',
      conditions: ['通电'],
      energyType: '通电',
      products: ['H₂', 'O₂'],
    },
  },
  {
    id: 'RX-IN-C-011',
    tags: ['分解', '工业制生石灰'],
    equation: 'CaCO₃ = CaO + CO₂↑',
    conditions: ['高温'],
    solveData: {
      kind: 'reaction-decomposition',
      equation: 'CaCO₃ = CaO + CO₂↑',
      conditions: ['高温'],
      energyType: '高温',
      products: ['CaO', 'CO₂'],
    },
  },

  // ============ 置换反应（单质 + 化合物 → 新单质 + 新化合物） ============
  {
    id: 'RX-IN-FE-006',
    tags: ['置换', '湿法炼铜'],
    equation: 'Fe + CuSO₄ = FeSO₄ + Cu',
    conditions: [],
    phenomenon: '铁表面覆盖红色固体，溶液由蓝变浅绿',
    solveData: {
      kind: 'reaction-displacement',
      equation: 'Fe + CuSO₄ = FeSO₄ + Cu',
      activityBasis: '金属活动性顺序：Fe 排在 Cu 之前，故 Fe 能把 Cu 从其盐溶液中置换出来',
      displacedElement: 'Cu',
      valenceNote: 'Fe 在置换反应中显 +2 价，生成 FeSO₄（亚铁），不是 +3 价',
    },
  },
  {
    id: 'RX-IN-AL-003',
    tags: ['置换', '铝热反应', '金属冶炼'],
    equation: '2Al + Fe₂O₃ = Al₂O₃ + 2Fe',
    conditions: ['高温'],
    phenomenon: '剧烈反应，火星四射，生成熔融铁',
    solveData: {
      kind: 'reaction-displacement',
      equation: '2Al + Fe₂O₃ = Al₂O₃ + 2Fe',
      activityBasis: '铝热反应：Al 活动性强于 Fe，高温下 Al 置换出 Fe₂O₃ 中的 Fe',
      displacedElement: 'Fe',
      valenceNote: 'Al 被氧化为 +3 价（生成 Al₂O₃），Fe 由 +3 被还原为 0 价',
    },
  },
  {
    id: 'RX-IN-CL-007',
    tags: ['置换', '卤素活泼性'],
    equation: 'Cl₂ + 2KBr = 2KCl + Br₂',
    conditions: [],
    phenomenon: '溶液变橙红',
    solveData: {
      kind: 'reaction-displacement',
      equation: 'Cl₂ + 2KBr = 2KCl + Br₂',
      activityBasis: '卤素活动性顺序：Cl₂ > Br₂ > I₂，Cl₂ 能把 Br⁻ 氧化为 Br₂',
      displacedElement: 'Br₂',
      valenceNote: 'Cl 由 0 被还原为 -1，Br⁻ 由 -1 被氧化为 0',
    },
  },

  // ============ 复分解反应（两化合物互相交换成分） ============
  {
    id: 'RX-IN-ION-002',
    tags: ['复分解', '沉淀', '硫酸根检验'],
    equation: 'Ba²⁺ + SO₄²⁻ = BaSO₄↓',
    conditions: [],
    phenomenon: '产生白色沉淀（不溶于稀硝酸）',
    solveData: {
      kind: 'reaction-double-displacement',
      equation: 'Ba²⁺ + SO₄²⁻ = BaSO₄↓',
      drivingForce: '沉淀',
      ionicEquation: 'Ba²⁺ + SO₄²⁻ = BaSO₄↓',
      solubilityNote: 'BaSO₄ 难溶不拆；BaCl₂、Na₂SO₄ 等可溶强电解质拆为离子',
    },
  },
  {
    id: 'RX-IN-S-010',
    tags: ['复分解', '产气'],
    equation: 'Na₂SO₃ + H₂SO₄ = Na₂SO₄ + H₂O + SO₂↑',
    conditions: [],
    phenomenon: '产生刺激性气味气体',
    solveData: {
      kind: 'reaction-double-displacement',
      equation: 'Na₂SO₃ + H₂SO₄ = Na₂SO₄ + H₂O + SO₂↑',
      drivingForce: '气体',
      ionicEquation: 'SO₃²⁻ + 2H⁺ = H₂O + SO₂↑',
      solubilityNote: 'Na₂SO₃、H₂SO₄、Na₂SO₄ 可溶强电解质拆；H₂O 不拆，SO₂ 标 ↑',
    },
  },

  // ============ 燃烧 ============
  {
    id: 'RX-OR-HC-001',
    tags: ['燃烧', '氧化反应'],
    equation: 'CH₄ + 2O₂ = CO₂ + 2H₂O',
    conditions: ['点燃'],
    phenomenon: '产生淡蓝色火焰',
    solveData: {
      kind: 'reaction-combustion',
      equation: 'CH₄ + 2O₂ = CO₂ + 2H₂O',
      conditions: ['点燃'],
      phenomenon: '产生淡蓝色火焰',
      combustible: 'CH₄',
      oxidizer: 'O₂',
      complete: true,
      products: ['CO₂', 'H₂O'],
    },
  },
  {
    id: 'RX-OR-HC-015',
    tags: ['燃烧', '氧化反应'],
    equation: '2C₆H₆ + 15O₂ = 12CO₂ + 6H₂O',
    conditions: ['点燃'],
    phenomenon: '火焰明亮并伴有浓烈黑烟',
    solveData: {
      kind: 'reaction-combustion',
      equation: '2C₆H₆ + 15O₂ = 12CO₂ + 6H₂O',
      conditions: ['点燃'],
      phenomenon: '火焰明亮并伴有浓烈黑烟',
      combustible: 'C₆H₆',
      oxidizer: 'O₂',
      complete: true,
      products: ['CO₂', 'H₂O'],
    },
  },
]

export default inorganic
