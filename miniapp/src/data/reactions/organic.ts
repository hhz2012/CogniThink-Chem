import { ReactionEntry } from '../../types/reaction'

// 有机反应 9 个子类型：取代 / 加成 / 消去 / 氧化 / 还原 / 酯化 / 水解 / 加聚 / 缩聚。
// 除加聚、缩聚外，均溯源后端 ReactionEntry（同 id / 同方程式）。
// 后端 KB 暂未收录聚合反应，加聚/缩聚 4 条采用人教版教材标准方程式与真值，tag 标注「后端KB待同步」，
// 待后端扩库后按 id 对齐（不影响当前实例化正确性）。
const organic: ReactionEntry[] = [
  // ============ 取代反应 ============
  {
    id: 'RX-OR-HC-002',
    tags: ['取代', '连锁反应'],
    equation: 'CH₄ + Cl₂ = CH₃Cl + HCl',
    conditions: ['光照'],
    phenomenon: '气体颜色变浅，瓶壁出现油状液滴',
    solveData: {
      kind: 'reaction-organic-substitution',
      equation: 'CH₄ + Cl₂ = CH₃Cl + HCl',
      conditions: ['光照'],
      functionalGroups: [{ name: '烷烃 C—H 键', property: '饱和，光照下发生自由基取代' }],
      reaction: { type: '取代', reagents: 'Cl₂', conditions: '光照', conclusion: '甲烷与氯气在光照下发生自由基取代，氢被氯逐步取代' },
      product: {
        formula: 'CH₃Cl',
        equation: 'CH₄ + Cl₂ = CH₃Cl + HCl',
        reasoning: '一个 H 被 Cl 取代生成一氯甲烷，连锁反应可继续到 CCl₄',
        conclusion: '一氯代产物 CH₃Cl 与 HCl',
      },
    },
  },
  {
    id: 'RX-OR-HC-017',
    tags: ['取代', '硝化'],
    equation: 'C₆H₆ + HNO₃ = C₆H₅NO₂ + H₂O',
    conditions: ['浓H₂SO₄', '水浴', '△'],
    phenomenon: '生成有苦杏仁味的无色油状液体',
    solveData: {
      kind: 'reaction-organic-substitution',
      equation: 'C₆H₆ + HNO₃ = C₆H₅NO₂ + H₂O',
      conditions: ['浓H₂SO₄', '水浴', '△'],
      functionalGroups: [{ name: '苯环', property: '易取代、难加成，可硝化引入 -NO₂' }],
      reaction: { type: '取代（硝化）', reagents: '浓 HNO₃', conditions: '浓 H₂SO₄ 催化、水浴加热', conclusion: '苯的硝化反应，硝基取代苯环上的氢' },
      product: {
        formula: 'C₆H₅NO₂',
        equation: 'C₆H₆ + HNO₃ = C₆H₅NO₂ + H₂O',
        reasoning: '苯环上一个 H 被 -NO₂ 取代，生成硝基苯和水',
        conclusion: '产物为硝基苯（苦杏仁味油状液体）',
      },
    },
  },

  // ============ 加成反应 ============
  {
    id: 'RX-OR-HC-007',
    tags: ['加成', '碳碳双键检验'],
    equation: 'C₂H₄ + Br₂ = C₂H₄Br₂',
    conditions: [],
    phenomenon: '溴水（或溴的 CCl₄ 溶液）褪色',
    solveData: {
      kind: 'reaction-organic-addition',
      equation: 'C₂H₄ + Br₂ = C₂H₄Br₂',
      functionalGroups: [{ name: '碳碳双键 C=C', property: '不饱和键，可加成使溴水褪色' }],
      reaction: { type: '加成', reagents: 'Br₂', conditions: '常温（溴水或溴的 CCl₄ 溶液）', conclusion: '乙烯与溴发生加成，碳碳双键打开，溴水褪色（碳碳双键检验）' },
      regioselectivity: '对称烯烃，无区域选择性问题',
      product: {
        formula: 'CH₂BrCH₂Br',
        equation: 'C₂H₄ + Br₂ = CH₂BrCH₂Br',
        reasoning: '双键打开，两个 Br 分别加到两个碳上，只进不出无小分子生成',
        conclusion: '产物为 1,2-二溴乙烷',
      },
    },
  },
  {
    id: 'RX-OR-HC-010',
    tags: ['加成', '水化', '工业制乙醇'],
    equation: 'C₂H₄ + H₂O = C₂H₅OH',
    conditions: ['催化剂', '△', '加压'],
    solveData: {
      kind: 'reaction-organic-addition',
      equation: 'C₂H₄ + H₂O = C₂H₅OH',
      conditions: ['催化剂', '△', '加压'],
      functionalGroups: [{ name: '碳碳双键 C=C', property: '可与水加成（水化）' }],
      reaction: { type: '加成（水化）', reagents: 'H₂O', conditions: '催化剂、加热、加压', conclusion: '乙烯水化法制乙醇（工业制乙醇）' },
      regioselectivity: '马氏规则：-OH 加到含氢较少的碳上（此处两碳等价）',
      product: {
        formula: 'C₂H₅OH',
        equation: 'C₂H₄ + H₂O = C₂H₅OH',
        reasoning: '双键打开，-H 与 -OH 分别加到两个碳上',
        conclusion: '产物为乙醇',
      },
    },
  },

  // ============ 消去反应 ============
  {
    id: 'RX-OR-HX-002',
    tags: ['消去', '卤代烃'],
    equation: 'C₂H₅Br + NaOH = C₂H₄↑ + NaBr + H₂O',
    conditions: ['醇溶液', '△'],
    phenomenon: '产生使溴水褪色的气体',
    solveData: {
      kind: 'reaction-organic-elimination',
      equation: 'C₂H₅Br + NaOH = C₂H₄↑ + NaBr + H₂O',
      conditions: ['醇溶液', '△'],
      functionalGroups: [{ name: '碳卤键 C—Br', property: '卤代烃，β 碳上有氢时可发生消去' }],
      reaction: { type: '消去', reagents: 'NaOH', conditions: '醇溶液、加热', conclusion: '溴乙烷在 NaOH 醇溶液中消去 HBr 生成乙烯；NaOH 水溶液则为水解，需区分' },
      product: {
        formula: 'C₂H₄',
        equation: 'C₂H₅Br + NaOH = C₂H₄↑ + NaBr + H₂O',
        reasoning: '脱去 HBr 小分子，生成碳碳双键；此处只有一种消去可能',
        conclusion: '产物为乙烯',
      },
    },
  },
  {
    id: 'RX-OR-OH-004',
    tags: ['消去', '实验室制乙烯'],
    equation: 'C₂H₅OH = C₂H₄↑ + H₂O',
    conditions: ['浓H₂SO₄', '170℃'],
    solveData: {
      kind: 'reaction-organic-elimination',
      equation: 'C₂H₅OH = C₂H₄↑ + H₂O',
      conditions: ['浓H₂SO₄', '170℃'],
      functionalGroups: [{ name: '羟基 -OH', property: '醇，β 碳有氢时可发生消去' }],
      reaction: { type: '消去（分子内脱水）', reagents: '—', conditions: '浓硫酸、170℃', conclusion: '乙醇分子内脱水制乙烯；140℃ 时为分子间脱水生成乙醚（非消去），温度是关键' },
      product: {
        formula: 'C₂H₄',
        equation: 'C₂H₅OH = C₂H₄↑ + H₂O',
        reasoning: '脱去 H₂O 生成碳碳双键',
        conclusion: '产物为乙烯',
      },
    },
  },

  // ============ 氧化反应（有机） ============
  {
    id: 'RX-OR-OH-003',
    tags: ['氧化', '催化氧化'],
    equation: '2C₂H₅OH + O₂ = 2CH₃CHO + 2H₂O',
    conditions: ['Cu', '△'],
    phenomenon: '铜丝红黑交替，产生刺激性气味',
    solveData: {
      kind: 'reaction-organic-oxidation',
      equation: '2C₂H₅OH + O₂ = 2CH₃CHO + 2H₂O',
      conditions: ['Cu', '△'],
      functionalGroups: [{ name: '羟基 -OH（伯醇 -CH₂OH）', property: '伯醇可催化氧化为醛' }],
      reaction: { type: '氧化（催化氧化）', reagents: 'O₂', conditions: 'Cu、加热', conclusion: '乙醇（伯醇）催化氧化生成乙醛' },
      product: {
        formula: 'CH₃CHO',
        equation: '2C₂H₅OH + O₂ = 2CH₃CHO + 2H₂O',
        reasoning: '伯醇 -CH₂OH 被氧化为醛 -CHO，Cu 作催化剂',
        conclusion: '产物为乙醛',
      },
    },
  },
  {
    id: 'RX-OR-ALD-003',
    tags: ['氧化', '银镜反应', '醛基检验'],
    equation: 'CH₃CHO + 2Ag(NH₃)₂OH = CH₃COONH₄ + 2Ag↓ + 3NH₃ + H₂O',
    conditions: ['水浴', '△'],
    phenomenon: '试管内壁形成光亮的银镜',
    solveData: {
      kind: 'reaction-organic-oxidation',
      equation: 'CH₃CHO + 2Ag(NH₃)₂OH = CH₃COONH₄ + 2Ag↓ + 3NH₃ + H₂O',
      conditions: ['水浴', '△'],
      functionalGroups: [{ name: '醛基 -CHO', property: '可被弱氧化剂氧化，发生银镜反应（醛基检验）' }],
      reaction: { type: '氧化（银镜反应）', reagents: '银氨溶液', conditions: '水浴加热', conclusion: '乙醛被银氨溶液氧化，是醛基检验的特征反应' },
      product: {
        formula: 'CH₃COONH₄',
        equation: 'CH₃CHO + 2Ag(NH₃)₂OH = CH₃COONH₄ + 2Ag↓ + 3NH₃ + H₂O',
        reasoning: '醛基被氧化为羧基（生成乙酸铵），Ag⁺ 被还原为 Ag 单质',
        conclusion: '产物为乙酸铵与银',
      },
    },
  },

  // ============ 还原反应（有机，催化加氢） ============
  {
    id: 'RX-OR-HC-008',
    tags: ['还原', '加成', '催化加氢'],
    equation: 'C₂H₄ + H₂ = C₂H₆',
    conditions: ['Ni', '△'],
    solveData: {
      kind: 'reaction-organic-reduction',
      equation: 'C₂H₄ + H₂ = C₂H₆',
      conditions: ['Ni', '△'],
      functionalGroups: [{ name: '碳碳双键 C=C', property: '可催化加氢被还原' }],
      reaction: { type: '还原（催化加氢）', reagents: 'H₂', conditions: 'Ni、加热', conclusion: '乙烯催化加氢生成乙烷，碳碳双键被还原为单键（同时也是加成反应）' },
      product: {
        formula: 'C₂H₆',
        equation: 'C₂H₄ + H₂ = C₂H₆',
        reasoning: '双键加氢变单键，不饱和度降低',
        conclusion: '产物为乙烷',
      },
    },
  },
  {
    id: 'RX-OR-HC-018',
    tags: ['还原', '加成', '催化加氢'],
    equation: 'C₆H₆ + 3H₂ = C₆H₁₂',
    conditions: ['Ni', '△'],
    solveData: {
      kind: 'reaction-organic-reduction',
      equation: 'C₆H₆ + 3H₂ = C₆H₁₂',
      conditions: ['Ni', '△'],
      functionalGroups: [{ name: '苯环', property: '可催化加氢生成环己烷' }],
      reaction: { type: '还原（催化加氢）', reagents: 'H₂', conditions: 'Ni、加热', conclusion: '苯催化加氢生成环己烷，苯环被还原（同时也是加成反应）' },
      product: {
        formula: 'C₆H₁₂',
        equation: 'C₆H₆ + 3H₂ = C₆H₁₂',
        reasoning: '苯环加氢变为环己烷的饱和环',
        conclusion: '产物为环己烷',
      },
    },
  },

  // ============ 酯化反应 ============
  {
    id: 'RX-OR-ACD-004',
    tags: ['酯化', '取代', '可逆'],
    equation: 'CH₃COOH + C₂H₅OH ⇌ CH₃COOCH₂CH₃ + H₂O',
    conditions: ['浓H₂SO₄', '△'],
    phenomenon: '液面上方出现有香味的无色油状液体',
    solveData: {
      kind: 'reaction-organic-esterification',
      equation: 'CH₃COOH + C₂H₅OH ⇌ CH₃COOCH₂CH₃ + H₂O',
      conditions: ['浓H₂SO₄', '△'],
      functionalGroups: [
        { name: '羧基 -COOH', property: '可与醇发生酯化' },
        { name: '羟基 -OH（醇）', property: '醇羟基参与酯化' },
      ],
      reaction: { type: '酯化', reagents: '乙酸 + 乙醇', conditions: '浓硫酸、加热（可逆）', conclusion: '酯化反应属于取代，本质是「酸脱羟基、醇脱氢」' },
      product: {
        formula: 'CH₃COOCH₂CH₃',
        equation: 'CH₃COOH + C₂H₅OH ⇌ CH₃COOCH₂CH₃ + H₂O',
        reasoning: '羧酸脱 -OH、醇脱 -H 生成水，余下部分连成酯',
        conclusion: '生成乙酸乙酯和水，反应可逆',
      },
    },
  },
  {
    id: 'RX-OR-EST-004',
    tags: ['酯化', '取代'],
    equation: 'CH₃COOH + HOCH₂CH₂OH ⇌ CH₃COOCH₂CH₂OH + H₂O',
    conditions: [],
    solveData: {
      kind: 'reaction-organic-esterification',
      equation: 'CH₃COOH + HOCH₂CH₂OH ⇌ CH₃COOCH₂CH₂OH + H₂O',
      functionalGroups: [
        { name: '羧基 -COOH' },
        { name: '羟基 -OH（二元醇）', property: '乙二醇有两个羟基，可部分酯化' },
      ],
      reaction: { type: '酯化', reagents: '乙酸 + 乙二醇', conditions: '浓硫酸、加热（可逆）', conclusion: '乙酸与乙二醇 1:1 酯化生成乙二醇单乙酸酯' },
      product: {
        formula: 'CH₃COOCH₂CH₂OH',
        equation: 'CH₃COOH + HOCH₂CH₂OH ⇌ CH₃COOCH₂CH₂OH + H₂O',
        reasoning: '二元醇的一个羟基参与酯化，另一个保留',
        conclusion: '产物为乙二醇单乙酸酯',
      },
    },
  },

  // ============ 水解反应 ============
  {
    id: 'RX-OR-EST-001',
    tags: ['水解', '可逆'],
    equation: 'CH₃COOCH₂CH₃ + H₂O ⇌ CH₃COOH + C₂H₅OH',
    conditions: ['浓H₂SO₄', '△'],
    solveData: {
      kind: 'reaction-organic-hydrolysis',
      equation: 'CH₃COOCH₂CH₃ + H₂O ⇌ CH₃COOH + C₂H₅OH',
      conditions: ['浓H₂SO₄', '△'],
      functionalGroups: [{ name: '酯基 -COO-', property: '可水解，酸性条件下可逆' }],
      reaction: { type: '水解', reagents: 'H₂O', conditions: '酸性（稀硫酸）、加热', conclusion: '乙酸乙酯酸性水解生成乙酸和乙醇，反应可逆' },
      product: {
        formula: 'CH₃COOH + C₂H₅OH',
        equation: 'CH₃COOCH₂CH₃ + H₂O ⇌ CH₃COOH + C₂H₅OH',
        reasoning: '酯基断裂，酸性水解生成羧酸和醇',
        conclusion: '产物为乙酸和乙醇（可逆）',
      },
    },
  },
  {
    id: 'RX-OR-EST-002',
    tags: ['水解', '碱性'],
    equation: 'CH₃COOCH₂CH₃ + NaOH = CH₃COONa + C₂H₅OH',
    conditions: ['△'],
    phenomenon: '油层消失',
    solveData: {
      kind: 'reaction-organic-hydrolysis',
      equation: 'CH₃COOCH₂CH₃ + NaOH = CH₃COONa + C₂H₅OH',
      conditions: ['△'],
      functionalGroups: [{ name: '酯基 -COO-', property: '碱性条件下水解彻底（皂化）' }],
      reaction: { type: '水解', reagents: 'NaOH', conditions: 'NaOH 溶液、加热', conclusion: '乙酸乙酯碱性水解彻底，生成乙酸钠和乙醇（皂化）' },
      product: {
        formula: 'CH₃COONa + C₂H₅OH',
        equation: 'CH₃COOCH₂CH₃ + NaOH = CH₃COONa + C₂H₅OH',
        reasoning: '碱性条件下羧酸变为钠盐，水解进行到底',
        conclusion: '产物为乙酸钠和乙醇（彻底）',
      },
    },
  },

  // ============ 加聚反应（教材标准，后端 KB 待同步） ============
  {
    id: 'RX-OR-POL-001',
    tags: ['加聚', '聚合', '后端KB待同步'],
    equation: 'n CH₂=CH₂ → [CH₂-CH₂]ₙ',
    conditions: ['催化剂', '△', '加压'],
    solveData: {
      kind: 'reaction-organic-polyaddition',
      equation: 'n CH₂=CH₂ → [CH₂-CH₂]ₙ',
      conditions: ['催化剂', '△', '加压'],
      functionalGroups: [{ name: '碳碳双键 C=C', property: '单体含双键，可发生加聚' }],
      reaction: { type: '加聚', reagents: '乙烯', conditions: '催化剂、加热、加压', conclusion: '乙烯加聚生成聚乙烯，双键打开彼此连接成链' },
      monomer: 'CH₂=CH₂',
      repeatUnit: '-CH₂-CH₂-',
      product: {
        formula: '[CH₂-CH₂]ₙ',
        equation: 'n CH₂=CH₂ → [CH₂-CH₂]ₙ',
        reasoning: '碳碳双键打开，单体彼此连接成长链，无小分子生成',
        conclusion: '产物为聚乙烯，链节与单体组成相同',
      },
    },
  },
  {
    id: 'RX-OR-POL-002',
    tags: ['加聚', '聚合', '后端KB待同步'],
    equation: 'n CH₂=CHCl → [CH₂-CHCl]ₙ',
    conditions: ['催化剂', '△', '加压'],
    solveData: {
      kind: 'reaction-organic-polyaddition',
      equation: 'n CH₂=CHCl → [CH₂-CHCl]ₙ',
      conditions: ['催化剂', '△', '加压'],
      functionalGroups: [{ name: '碳碳双键 C=C', property: '单体含双键，可发生加聚；取代基位置不变' }],
      reaction: { type: '加聚', reagents: '氯乙烯', conditions: '催化剂、加热、加压', conclusion: '氯乙烯加聚生成聚氯乙烯' },
      monomer: 'CH₂=CHCl',
      repeatUnit: '-CH₂-CH(Cl)-',
      product: {
        formula: '[CH₂-CHCl]ₙ',
        equation: 'n CH₂=CHCl → [CH₂-CHCl]ₙ',
        reasoning: '双键打开成链，Cl 取代基位置不变，无小分子生成',
        conclusion: '产物为聚氯乙烯',
      },
    },
  },

  // ============ 缩聚反应（教材标准，后端 KB 待同步） ============
  {
    id: 'RX-OR-POL-003',
    tags: ['缩聚', '聚合', '后端KB待同步'],
    equation: 'n HOOC-C₆H₄-COOH + n HOCH₂CH₂OH → H-[OC-C₆H₄-COOCH₂CH₂O]ₙ-H + (2n-1)H₂O',
    conditions: ['催化剂', '△'],
    solveData: {
      kind: 'reaction-organic-polycondensation',
      equation: 'n HOOC-C₆H₄-COOH + n HOCH₂CH₂OH → H-[OC-C₆H₄-COOCH₂CH₂O]ₙ-H + (2n-1)H₂O',
      conditions: ['催化剂', '△'],
      functionalGroups: [
        { name: '羧基 -COOH（二元酸）', property: '对苯二甲酸有两个羧基' },
        { name: '羟基 -OH（二元醇）', property: '乙二醇有两个羟基' },
      ],
      reaction: { type: '缩聚', reagents: '对苯二甲酸 + 乙二醇', conditions: '催化剂、加热', conclusion: '二元酸与二元醇缩聚生成聚酯（涤纶），同时脱去水' },
      monomer: '对苯二甲酸 + 乙二醇（HOOC-C₆H₄-COOH 与 HOCH₂CH₂OH）',
      repeatUnit: '-OC-C₆H₄-COOCH₂CH₂O-',
      byproduct: 'H₂O',
      product: {
        formula: 'H-[OC-C₆H₄-COOCH₂CH₂O]ₙ-H',
        reasoning: '羧基与羟基酯化脱水，反复连接成聚酯长链；链节组成比单体少了脱去的水分子',
        conclusion: '产物为聚对苯二甲酸乙二醇酯（涤纶）和水',
      },
    },
  },
  {
    id: 'RX-OR-POL-004',
    tags: ['缩聚', '聚合', '后端KB待同步'],
    equation: 'n HOOC(CH₂)₄COOH + n H₂N(CH₂)₆NH₂ → H-[OC(CH₂)₄CONH(CH₂)₆NH]ₙ-H + (2n-1)H₂O',
    conditions: ['△'],
    solveData: {
      kind: 'reaction-organic-polycondensation',
      equation: 'n HOOC(CH₂)₄COOH + n H₂N(CH₂)₆NH₂ → H-[OC(CH₂)₄CONH(CH₂)₆NH]ₙ-H + (2n-1)H₂O',
      conditions: ['△'],
      functionalGroups: [
        { name: '羧基 -COOH（二元酸）', property: '己二酸有两个羧基' },
        { name: '氨基 -NH₂（二元胺）', property: '己二胺有两个氨基' },
      ],
      reaction: { type: '缩聚', reagents: '己二酸 + 己二胺', conditions: '加热', conclusion: '二元酸与二元胺缩聚生成聚酰胺（尼龙-66），同时脱水成肽键' },
      monomer: '己二酸 + 己二胺（HOOC(CH₂)₄COOH 与 H₂N(CH₂)₆NH₂）',
      repeatUnit: '-OC(CH₂)₄CONH(CH₂)₆NH-',
      byproduct: 'H₂O',
      product: {
        formula: 'H-[OC(CH₂)₄CONH(CH₂)₆NH]ₙ-H',
        reasoning: '羧基与氨基脱水成肽键（-CONH-），反复连接成聚酰胺长链',
        conclusion: '产物为聚酰胺（尼龙-66）和水',
      },
    },
  },
]

export default organic
