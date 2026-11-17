import { Chapter, Challenge } from '../types/chem'

// 精选题目（从 CogniThink-Chem 题库抽象，供闯关使用）
// 每章 4 题，共 6 章 24 题

const productInference: Challenge[] = [
  {
    id: 'P1', category: 'ProductInference', title: '铁与盐酸反应',
    problemText: '向铁粉中加入足量稀盐酸，充分反应后，溶液中的主要阳离子是？',
    options: ['Fe³⁺', 'Fe²⁺', 'Fe³⁺ 和 Fe²⁺', 'H⁺'],
    answerIndex: 1,
    explanation: '铁与非氧化性酸（盐酸）反应生成亚铁盐，Fe + 2HCl = FeCl₂ + H₂↑，主要阳离子为 Fe²⁺。',
    thought: 'Conservation',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['这是产物推断题。题目要求什么？', '反应物是铁和什么酸？是氧化性酸还是非氧化性酸？'],
        hint: '先判断酸的氧化性强弱，这决定铁被氧化到几价。' },
      { level: 2, title: '建立模型', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['铁与酸反应的一般规律是什么？', '盐酸属于哪一类酸？'],
        hint: '回忆铁与稀盐酸的反应方程式。' },
      { level: 3, title: '知识检索', cognitiveLayer: 'Knowledge', interaction: 'Reveal',
        prompts: ['铁与非氧化性酸反应生成 Fe²⁺；与强氧化性酸（如稀硝酸）反应生成 Fe³⁺。'],
        knowledgeCard: { name: '铁的变价', summary: 'Fe 有 +2、+3 价，弱氧化剂生成 Fe²⁺，强氧化剂生成 Fe³⁺。', textbookRef: '必修一·第三章' } },
      { level: 4, title: '规则应用', cognitiveLayer: 'Application', interaction: 'Hint',
        prompts: ['盐酸是非氧化性酸，把铁氧化到几价？'],
        hint: '非氧化性酸只能把 Fe 氧化到 +2 价。' },
      { level: 5, title: '精确求解', cognitiveLayer: 'Application', interaction: 'Hint',
        prompts: ['写出反应方程式，判断溶液中的阳离子。'] },
      { level: 6, title: '守恒校验', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['方程式原子守恒吗？电子得失守恒吗？'],
        hint: '检查 Fe 从 0→+2 失去 2e⁻，2 个 H⁺ 各得 1e⁻，电子守恒。' },
    ],
  },
  {
    id: 'P2', category: 'ProductInference', title: '铜与硝酸反应',
    problemText: '铜片加入稀硝酸中，产生的气体是？',
    options: ['H₂', 'NO', 'NO₂', 'N₂'],
    answerIndex: 1,
    explanation: '稀硝酸是强氧化性酸，与铜反应生成 NO：3Cu + 8HNO₃(稀) = 3Cu(NO₃)₂ + 2NO↑ + 4H₂O。',
    thought: 'Classification',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['这是产物推断题。铜与稀硝酸反应，生成什么气体？'],
        hint: '区分稀硝酸和浓硝酸的还原产物。' },
      { level: 3, title: '知识检索', cognitiveLayer: 'Knowledge', interaction: 'Reveal',
        prompts: ['稀硝酸还原产物为 NO；浓硝酸还原产物为 NO₂。'],
        knowledgeCard: { name: '硝酸的氧化性', summary: '硝酸是强氧化性酸，浓度不同还原产物不同。', textbookRef: '必修一·第五章' } },
      { level: 4, title: '规则应用', cognitiveLayer: 'Application', interaction: 'Hint',
        prompts: ['题目明确是稀硝酸，还原产物是什么？'] },
    ],
  },
  {
    id: 'P3', category: 'ProductInference', title: '铝与氢氧化钠',
    problemText: '铝片加入足量 NaOH 溶液中，反应生成的气体是？',
    options: ['O₂', 'H₂', 'CO₂', 'NH₃'],
    answerIndex: 1,
    explanation: '铝与强碱反应生成偏铝酸盐和氢气：2Al + 2NaOH + 2H₂O = 2NaAlO₂ + 3H₂↑。',
    thought: 'Conservation',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['铝与 NaOH 溶液反应，生成物有哪些？'],
        hint: '铝是两性金属，能与强碱反应。' },
      { level: 3, title: '知识检索', cognitiveLayer: 'Knowledge', interaction: 'Reveal',
        prompts: ['铝与强碱反应：2Al + 2NaOH + 2H₂O = 2NaAlO₂ + 3H₂↑。'],
        knowledgeCard: { name: '铝的两性', summary: '铝既能与酸反应，又能与强碱反应，均放出氢气。', textbookRef: '必修一·第三章' } },
    ],
  },
  {
    id: 'P4', category: 'ProductInference', title: '钠与水反应',
    problemText: '金属钠投入水中，反应生成的产物是？',
    options: ['Na₂O 和 H₂', 'NaOH 和 H₂', 'Na₂O₂ 和 O₂', 'NaOH 和 O₂'],
    answerIndex: 1,
    explanation: '钠与水剧烈反应：2Na + 2H₂O = 2NaOH + H₂↑，生成氢氧化钠和氢气。',
    thought: 'Conservation',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['钠与水反应，产物是什么？'],
        hint: '钠是活泼金属，与水反应生成碱和氢气。' },
    ],
  },
]

const ionCoexistence: Challenge[] = [
  {
    id: 'C1', category: 'IonCoexistence', title: '碱性溶液共存',
    problemText: '在强碱性溶液中，下列各组离子能大量共存的是？',
    options: ['Na⁺、K⁺、CO₃²⁻、Cl⁻', 'Mg²⁺、Cu²⁺、NO₃⁻、SO₄²⁻', 'NH₄⁺、Fe³⁺、Cl⁻、SO₄²⁻', 'H⁺、Ag⁺、NO₃⁻、Cl⁻'],
    answerIndex: 0,
    explanation: '强碱性溶液含大量 OH⁻。B 中 Mg²⁺/Cu²⁺ 与 OH⁻ 生成沉淀；C 中 NH₄⁺ 与 OH⁻ 生成 NH₃·H₂O，Fe³⁺ 生成沉淀；D 中 H⁺ 与 OH⁻ 中和。A 可共存。',
    thought: 'Classification',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['强碱性溶液意味着大量存在什么离子？'],
        hint: '强碱性 → 大量 OH⁻。' },
      { level: 3, title: '知识检索', cognitiveLayer: 'Knowledge', interaction: 'Reveal',
        prompts: ['不能与 OH⁻ 大量共存的离子：H⁺、弱碱阳离子（Mg²⁺、Cu²⁺、Fe³⁺等）、NH₄⁺、酸式酸根。'],
        knowledgeCard: { name: '离子共存', summary: '判断离子能否大量共存，看是否生成沉淀、气体、弱电解质或发生氧化还原。', textbookRef: '必修一·第二章' } },
    ],
  },
  {
    id: 'C2', category: 'IonCoexistence', title: '酸性溶液共存',
    problemText: '在强酸性溶液中，下列各组离子能大量共存的是？',
    options: ['Na⁺、K⁺、SO₄²⁻、Cl⁻', 'HCO₃⁻、CO₃²⁻、Na⁺、K⁺', 'S²⁻、CH₃COO⁻、Na⁺、Cl⁻', 'OH⁻、NO₃⁻、Na⁺、K⁺'],
    answerIndex: 0,
    explanation: '强酸性含大量 H⁺。B 中 HCO₃⁻/CO₃²⁻ 与 H⁺ 反应；C 中 S²⁻/CH₃COO⁻ 与 H⁺ 生成弱电解质；D 中 OH⁻ 中和。A 可共存。',
    thought: 'Classification',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['强酸性溶液大量存在什么离子？'],
        hint: '强酸性 → 大量 H⁺。' },
    ],
  },
  {
    id: 'C3', category: 'IonCoexistence', title: '无色溶液共存',
    problemText: '在无色透明溶液中，下列离子能大量共存的是？',
    options: ['Cu²⁺、K⁺、Cl⁻、SO₄²⁻', 'Fe³⁺、Na⁺、NO₃⁻、Cl⁻', 'Na⁺、K⁺、NO₃⁻、SO₄²⁻', 'MnO₄⁻、Na⁺、Cl⁻、K⁺'],
    answerIndex: 2,
    explanation: '无色溶液排除有色离子：Cu²⁺(蓝)、Fe³⁺(黄)、MnO₄⁻(紫红)。C 全为无色且互不反应。',
    thought: 'Classification',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['"无色透明"这个条件排除了哪些离子？'],
        hint: '常见有色离子：Cu²⁺蓝、Fe³⁺黄、Fe²⁺浅绿、MnO₄⁻紫红。' },
    ],
  },
  {
    id: 'C4', category: 'IonCoexistence', title: '沉淀生成',
    problemText: '下列离子组在溶液中能大量共存的是？',
    options: ['Ba²⁺、Na⁺、SO₄²⁻、Cl⁻', 'Ag⁺、K⁺、NO₃⁻、Cl⁻', 'Ca²⁺、Na⁺、Cl⁻、NO₃⁻', 'Pb²⁺、K⁺、I⁻、NO₃⁻'],
    answerIndex: 2,
    explanation: 'A 中 BaSO₄ 沉淀；B 中 AgCl 沉淀；D 中 PbI₂ 沉淀；C 中 CaCl₂、Ca(NO₃)₂ 均可溶。',
    thought: 'Classification',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['判断共存，看是否生成沉淀。常见沉淀有哪些？'],
        hint: 'BaSO₄、AgCl、PbI₂ 都是难溶物。' },
    ],
  },
]

const stoichiometry: Challenge[] = [
  {
    id: 'S1', category: 'StoichiometryCalc', title: '物质的量浓度',
    problemText: '将 0.5 mol NaOH 溶于水配成 500 mL 溶液，其物质的量浓度为？',
    options: ['0.5 mol/L', '1 mol/L', '2 mol/L', '0.25 mol/L'],
    answerIndex: 1,
    explanation: 'c = n/V = 0.5 mol / 0.5 L = 1 mol/L。',
    thought: 'Conservation',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['求物质的量浓度，用什么公式？已知 n 和 V。'],
        hint: 'c = n / V，注意 V 单位换算成升。' },
      { level: 3, title: '知识检索', cognitiveLayer: 'Knowledge', interaction: 'Reveal',
        prompts: ['物质的量浓度公式：c = n / V，单位 mol/L，体积单位必须是升(L)。'],
        knowledgeCard: { name: '物质的量浓度', summary: 'c = n/V，1 L = 1000 mL。', textbookRef: '必修一·第二章' } },
    ],
  },
  {
    id: 'S2', category: 'StoichiometryCalc', title: '气体摩尔体积',
    problemText: '标准状况下，2.24 L H₂ 的物质的量为？',
    options: ['0.1 mol', '0.2 mol', '1 mol', '2.24 mol'],
    answerIndex: 0,
    explanation: '标况下气体摩尔体积 Vm = 22.4 L/mol，n = V/Vm = 2.24/22.4 = 0.1 mol。',
    thought: 'Conservation',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['标况下气体的摩尔体积是多少？'],
        hint: '标况（0℃, 101kPa）下 Vm = 22.4 L/mol。' },
    ],
  },
  {
    id: 'S3', category: 'StoichiometryCalc', title: '质量计算',
    problemText: '0.5 mol H₂O 的质量为？',
    options: ['9 g', '18 g', '0.5 g', '36 g'],
    answerIndex: 0,
    explanation: 'm = n × M = 0.5 mol × 18 g/mol = 9 g。',
    thought: 'Conservation',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['已知 n，求 m。H₂O 的摩尔质量是多少？'],
        hint: 'M(H₂O) = 2×1 + 16 = 18 g/mol。' },
    ],
  },
  {
    id: 'S4', category: 'StoichiometryCalc', title: '化学方程式计算',
    problemText: '2 mol H₂ 完全燃烧，生成 H₂O 的质量为？（2H₂ + O₂ = 2H₂O）',
    options: ['18 g', '36 g', '72 g', '9 g'],
    answerIndex: 1,
    explanation: '由方程式，2 mol H₂ 生成 2 mol H₂O，m = 2 mol × 18 g/mol = 36 g。',
    thought: 'Conservation',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['根据方程式，H₂ 与 H₂O 的计量数之比是多少？'],
        hint: '2H₂ → 2H₂O，物质的量之比 1:1。' },
    ],
  },
]

const electrochemistry: Challenge[] = [
  {
    id: 'E1', category: 'Electrochemistry', title: '原电池正负极',
    problemText: '铜锌原电池（电解质稀硫酸）中，锌片是？',
    options: ['正极，发生还原反应', '负极，发生氧化反应', '正极，发生氧化反应', '负极，发生还原反应'],
    answerIndex: 1,
    explanation: '锌比铜活泼，锌作负极，失电子发生氧化反应：Zn - 2e⁻ = Zn²⁺；铜作正极，H⁺ 得电子还原。',
    thought: 'Structure',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['原电池中，活泼金属作正极还是负极？'],
        hint: '较活泼金属作负极，发生氧化反应。' },
      { level: 3, title: '知识检索', cognitiveLayer: 'Knowledge', interaction: 'Reveal',
        prompts: ['原电池：负极氧化（失电子），正极还原（得电子）；活泼金属作负极。'],
        knowledgeCard: { name: '原电池原理', summary: '负极氧化、正极还原，电子由负极流向正极。', textbookRef: '必修二·第六章' } },
    ],
  },
  {
    id: 'E2', category: 'Electrochemistry', title: '电子流向',
    problemText: '铜锌原电池中，电子的流向是？',
    options: ['锌→铜（外电路）', '铜→锌（外电路）', '锌→铜（内电路）', '铜→锌（内电路）'],
    answerIndex: 0,
    explanation: '外电路中电子由负极（锌）经导线流向正极（铜）；内电路中阳离子向正极移动。',
    thought: 'Structure',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['电子在原电池中怎么流动？从哪个极出发？'],
        hint: '电子从负极流出，经外电路到正极。' },
    ],
  },
  {
    id: 'E3', category: 'Electrochemistry', title: '电解池阴阳极',
    problemText: '电解 CuCl₂ 溶液时，阳极发生的反应是？',
    options: ['Cu²⁺ + 2e⁻ = Cu', '2Cl⁻ - 2e⁻ = Cl₂↑', '2H₂O - 4e⁻ = O₂↑ + 4H⁺', 'Cu - 2e⁻ = Cu²⁺'],
    answerIndex: 1,
    explanation: '电解池中阳极发生氧化反应（失电子），Cl⁻ 放电：2Cl⁻ - 2e⁻ = Cl₂↑。',
    thought: 'Structure',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['电解池阳极发生什么反应？'],
        hint: '阳极氧化（失电子），Cl⁻ 比 OH⁻ 更容易放电。' },
    ],
  },
  {
    id: 'E4', category: 'Electrochemistry', title: '金属腐蚀',
    problemText: '钢铁在潮湿空气中发生电化学腐蚀时，正极反应是？',
    options: ['Fe - 2e⁻ = Fe²⁺', '2H⁺ + 2e⁻ = H₂↑', 'O₂ + 2H₂O + 4e⁻ = 4OH⁻', 'Fe - 3e⁻ = Fe³⁺'],
    answerIndex: 2,
    explanation: '钢铁在中性/弱酸性潮湿环境中发生吸氧腐蚀，正极 O₂ 得电子：O₂ + 2H₂O + 4e⁻ = 4OH⁻。',
    thought: 'Structure',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['潮湿空气是酸性还是中性环境？正极是析氢还是吸氧？'],
        hint: '中性/弱酸性 → 吸氧腐蚀，正极 O₂ 得电子。' },
    ],
  },
]

const equilibrium: Challenge[] = [
  {
    id: 'Q1', category: 'ChemicalEquilibrium', title: '平衡移动方向',
    problemText: '反应 2SO₂ + O₂ ⇌ 2SO₃（正反应放热），达到平衡后升高温度，平衡将？',
    options: ['向正反应方向移动', '向逆反应方向移动', '不移动', '无法判断'],
    answerIndex: 1,
    explanation: '正反应放热，升温时平衡向吸热方向（逆反应）移动。',
    thought: 'Equilibrium',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['升温对放热反应的平衡有什么影响？'],
        hint: '勒夏特列原理：升温向吸热方向移动。' },
      { level: 3, title: '知识检索', cognitiveLayer: 'Knowledge', interaction: 'Reveal',
        prompts: ['勒夏特列原理：改变影响平衡的条件，平衡向减弱这种改变的方向移动。升温→向吸热方向。'],
        knowledgeCard: { name: '化学平衡移动', summary: '升温向吸热方向；加压向气体分子数减少方向；增大反应物浓度向正方向。', textbookRef: '必修二·第六章' } },
    ],
  },
  {
    id: 'Q2', category: 'ChemicalEquilibrium', title: '浓度影响',
    problemText: '反应 N₂ + 3H₂ ⇌ 2NH₃ 达到平衡后，增大 N₂ 浓度，平衡将？',
    options: ['向正反应方向移动', '向逆反应方向移动', '不移动', '无法判断'],
    answerIndex: 0,
    explanation: '增大反应物浓度，平衡向正反应方向移动（减弱反应物浓度增大的趋势）。',
    thought: 'Equilibrium',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['增大反应物浓度，平衡怎么移动？'],
        hint: '勒夏特列原理：增大反应物浓度，向正反应方向移动。' },
    ],
  },
  {
    id: 'Q3', category: 'ChemicalEquilibrium', title: '压强影响',
    problemText: '反应 2NO₂ ⇌ N₂O₄ 达到平衡后，增大压强（缩小体积），平衡将？',
    options: ['向正反应方向移动', '向逆反应方向移动', '不移动', '无法判断'],
    answerIndex: 0,
    explanation: '正反应气体分子数减少（2→1），加压向分子数减少方向（正反应）移动。',
    thought: 'Equilibrium',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['加压对气体分子数变化的反应有什么影响？'],
        hint: '加压向气体分子数减少的方向移动。' },
    ],
  },
  {
    id: 'Q4', category: 'ChemicalEquilibrium', title: '平衡常数',
    problemText: '反应 A + B ⇌ C 的平衡常数 K，下列说法正确的是？',
    options: ['K 越大，正反应程度越大', 'K 越小，正反应程度越大', 'K 与温度无关', 'K 与浓度有关'],
    answerIndex: 0,
    explanation: '平衡常数 K 只与温度有关，K 越大表示正反应进行程度越大。',
    thought: 'Equilibrium',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['平衡常数 K 的大小反映什么？K 与什么有关？'],
        hint: 'K 只与温度有关，K 越大正反应程度越大。' },
    ],
  },
]

const experiment: Challenge[] = [
  {
    id: 'X1', category: 'ExperimentalInquiry', title: '蒸馏装置',
    problemText: '蒸馏实验中，温度计水银球的位置应在？',
    options: ['液面以下', '蒸馏烧瓶支管口处', '冷凝管中', '锥形瓶中'],
    answerIndex: 1,
    explanation: '温度计测量的是馏分的温度，水银球应放在蒸馏烧瓶支管口处，与蒸气出口平齐。',
    thought: 'ModelCognition',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['蒸馏时温度计测的是什么温度？'],
        hint: '测的是馏出蒸气的温度，不是液体温度。' },
      { level: 3, title: '知识检索', cognitiveLayer: 'Knowledge', interaction: 'Reveal',
        prompts: ['蒸馏操作：温度计水银球位于蒸馏烧瓶支管口处；冷凝水下进上出。'],
        knowledgeCard: { name: '蒸馏', summary: '分离沸点不同的液体混合物，温度计测蒸气温度。', textbookRef: '必修一·第一章' } },
    ],
  },
  {
    id: 'X2', category: 'ExperimentalInquiry', title: '萃取分液',
    problemText: '萃取溴水中的溴，可选用的萃取剂是？',
    options: ['酒精', '四氯化碳', '乙酸', '水'],
    answerIndex: 1,
    explanation: '萃取剂需与原溶剂（水）不互溶且溴在其中溶解度大。CCl₄ 与水不互溶，可萃取溴。酒精、乙酸与水互溶。',
    thought: 'Classification',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['萃取剂需要满足什么条件？'],
        hint: '与水不互溶，且溶质在其中溶解度大。' },
    ],
  },
  {
    id: 'X3', category: 'ExperimentalInquiry', title: '气体收集',
    problemText: '收集 NO 气体应采用的方法是？',
    options: ['向上排空气法', '向下排空气法', '排水法', '排饱和食盐水法'],
    answerIndex: 2,
    explanation: 'NO 易与 O₂ 反应生成 NO₂，不能用排空气法；NO 难溶于水，用排水法收集。',
    thought: 'Classification',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['NO 有什么性质？能和空气中的成分反应吗？'],
        hint: 'NO 易被 O₂ 氧化，不能用排空气法。' },
    ],
  },
  {
    id: 'X4', category: 'ExperimentalInquiry', title: '焰色反应',
    problemText: '焰色反应中，观察钾的焰色应透过？',
    options: ['红色玻璃', '蓝色钴玻璃', '绿色玻璃', '普通玻璃'],
    answerIndex: 1,
    explanation: '钾的焰色为紫色，但易被钠的黄色干扰，需透过蓝色钴玻璃滤去黄光。',
    thought: 'Structure',
    guide: [
      { level: 1, title: '审题分类', cognitiveLayer: 'Ability', interaction: 'Probe',
        prompts: ['为什么观察钾的焰色需要特殊处理？'],
        hint: '钠的黄色焰色会干扰钾的紫色，需用蓝色钴玻璃滤光。' },
    ],
  },
]

export const chapters: Chapter[] = [
  {
    id: 'ch-product', name: '产物推断', icon: '⚗️', category: 'ProductInference',
    color: '#0f766e', desc: '根据反应物推断生成物', challenges: productInference,
  },
  {
    id: 'ch-coexist', name: '离子共存', icon: '💧', category: 'IonCoexistence',
    color: '#2563eb', desc: '判断离子能否大量共存', challenges: ionCoexistence,
  },
  {
    id: 'ch-calc', name: '定量计算', icon: '🧮', category: 'StoichiometryCalc',
    color: '#dc2626', desc: '物质的量与化学方程式计算', challenges: stoichiometry,
  },
  {
    id: 'ch-elec', name: '电化学', icon: '⚡', category: 'Electrochemistry',
    color: '#7c3aed', desc: '原电池与电解池', challenges: electrochemistry,
  },
  {
    id: 'ch-eq', name: '化学平衡', icon: '🔄', category: 'ChemicalEquilibrium',
    color: '#ea580c', desc: '平衡移动与平衡常数', challenges: equilibrium,
  },
  {
    id: 'ch-exp', name: '实验探究', icon: '🧪', category: 'ExperimentalInquiry',
    color: '#16a34a', desc: '装置、操作与现象', challenges: experiment,
  },
]

export const allChallenges: Challenge[] = chapters.flatMap((c) => c.challenges)

export function getChallengeById(id: string): Challenge | undefined {
  return allChallenges.find((c) => c.id === id)
}
