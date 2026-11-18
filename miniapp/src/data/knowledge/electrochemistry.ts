import { KnowledgePoint } from './redox'

// ============================================================
// 电化学 · 知识体系（EC1-EC8）
// ============================================================
export const electrochemistryKnowledge: KnowledgePoint[] = [
  {
    id: 'EC1',
    name: '原电池原理与电极判断',
    layer: 'Knowledge',
    summary: '负氧正还，电子由负极经导线流向正极。',
    keyPoints: ['较活泼金属作负极，发生氧化反应', '正极发生还原反应', '电子流向：负极→导线→正极'],
    pitfalls: ['把电子流向说成"正极流向负极"'],
    relatedChallengeIds: ['C1', 'C9'],
    check: {
      question: 'Zn-Cu 稀硫酸原电池中，负极是？',
      options: ['Zn', 'Cu', 'H⁺', 'SO₄²⁻'],
      answerIndex: 0,
      explain: 'Zn 比 Cu 活泼，失电子作负极：Zn-2e⁻=Zn²⁺。',
    },
    teach: {
      steps: ['第一步：比较电极材料活泼性，活泼者为负极。', '第二步：写出负极氧化、正极还原反应。', '第三步：标出电子与电流方向（电子负→正，电流正→负）。'],
      example: 'Zn 失 2e⁻，2H⁺ 在 Cu 表面得 2e⁻ 生成 H₂。',
    },
  },
  {
    id: 'EC2',
    name: '电极反应式书写',
    layer: 'Application',
    summary: '按电解质酸碱性选择 H⁺、OH⁻、H₂O 配平。',
    keyPoints: ['酸性用 H⁺ 与 H₂O 配平', '碱性用 OH⁻ 与 H₂O 配平', '正极得电子、负极失电子'],
    pitfalls: ['碱性环境仍写 H⁺（如氢氧燃料电池负极）'],
    relatedChallengeIds: ['C2', 'C14'],
    check: {
      question: '碱性氢氧燃料电池的负极反应式是？',
      options: ['2H₂-4e⁻=4H⁺', '2H₂+4OH⁻-4e⁻=4H₂O', 'O₂+2H₂O+4e⁻=4OH⁻', 'H₂-2e⁻=2H⁺'],
      answerIndex: 1,
      explain: '碱性条件下 H₂ 失电子，生成的 H⁺ 立即与 OH⁻ 结合成水。',
    },
    teach: {
      steps: ['第一步：确定电极反应方向（正极得、负极失）。', '第二步：写出核心变价物质的变化。', '第三步：用介质离子（H⁺/OH⁻）配电荷，用 H₂O 配原子。'],
      example: '酸性正极：O₂+4H⁺+4e⁻=2H₂O；碱性正极：O₂+2H₂O+4e⁻=4OH⁻。',
    },
  },
  {
    id: 'EC3',
    name: '电解池与放电顺序',
    layer: 'Application',
    summary: '阳极阴离子放电，阴极阳离子放电。',
    keyPoints: ['惰性阳极：S²⁻>I⁻>Br⁻>Cl⁻>OH⁻>含氧酸根', '阴极：Ag⁺>Fe³⁺>Cu²⁺>H⁺>...', '活泼金属作阳极时阳极自身溶解'],
    pitfalls: ['电解 NaCl 时误认为阳极放 O₂'],
    relatedChallengeIds: ['C3', 'C10'],
    check: {
      question: '惰性电极电解饱和 NaCl 溶液，阳极产物是？',
      options: ['H₂', 'Cl₂', 'O₂', 'Na'],
      answerIndex: 1,
      explain: 'Cl⁻ 放电顺序优先于 OH⁻，阳极生成 Cl₂：2Cl⁻-2e⁻=Cl₂↑。',
    },
    teach: {
      steps: ['第一步：确定阳极材料（惰性/活泼）。', '第二步：按放电顺序列出可能放电的离子。', '第三步：写出两极反应并合并得总反应。'],
      example: '2NaCl+2H₂O=电解=2NaOH+H₂↑+Cl₂↑。',
    },
  },
  {
    id: 'EC4',
    name: '电化学计算',
    layer: 'Ability',
    summary: '串联电路电子守恒，阴阳极转移电子数相等。',
    keyPoints: ['n(e⁻)阴 = n(e⁻)阳', 'Cu~2e⁻，O₂~4e⁻，Cl₂~2e⁻', '串联时全电路电子数相同'],
    pitfalls: ['忘记把体积换算成物质的量'],
    relatedChallengeIds: ['C4', 'C8', 'C13'],
    check: {
      question: '电解 CuSO₄ 溶液，阴极增重 6.4 g，转移电子数为？',
      options: ['0.1 mol', '0.2 mol', '0.4 mol', '0.05 mol'],
      answerIndex: 1,
      explain: 'n(Cu)=0.1 mol，Cu~2e⁻，转移 0.2 mol e⁻。',
    },
    teach: {
      steps: ['第一步：由已知电极产物求转移电子数。', '第二步：用电子守恒转到另一电极。', '第三步：由物质的量换算质量或气体体积。'],
      example: '6.4 g Cu（0.1 mol）→0.2 mol e⁻→0.05 mol O₂→1.12 L。',
    },
  },
  {
    id: 'EC5',
    name: '电解后溶液复原',
    layer: 'Application',
    summary: '出什么加什么，电解 CuSO₄ 加 CuO 复原。',
    keyPoints: ['分析电解过程中离开溶液的元素组合', '析出 Cu 和 O₂ → 加 CuO', '析出 H₂ 和 Cl₂ → 加 HCl'],
    pitfalls: ['只加 CuSO₄ 而忽略缺少的 H₂O'],
    relatedChallengeIds: ['C6', 'C12'],
    check: {
      question: '惰性电极电解 CuSO₄ 溶液后，要恢复原状应加入？',
      options: ['CuSO₄', 'CuO', 'Cu(OH)₂', 'H₂O'],
      answerIndex: 1,
      explain: '电解移出 Cu 和 O（相当于 CuO），加 CuO 与生成的 H₂SO₄ 反应恰好复原。',
    },
    teach: {
      steps: ['第一步：写出阴阳极产物，判断溶液中少了什么。', '第二步：把缺少的元素组合成稳定氧化物或酸。', '第三步：按量加入并验证恰好复原。'],
      example: '2CuSO₄+2H₂O=2Cu+O₂↑+2H₂SO₄，加 2 mol CuO 恰好复原。',
    },
  },
  {
    id: 'EC6',
    name: '二次电池与充放电',
    layer: 'Application',
    summary: '放电是原电池，充电是电解池。',
    keyPoints: ['放电：负极氧化、正极还原', '充电：原负极接电源负极作阴极', '铅蓄电池放电消耗 H₂SO₄'],
    pitfalls: ['充电时把原负极接到电源正极'],
    relatedChallengeIds: ['C7'],
    check: {
      question: '铅蓄电池放电时，正极材料是？',
      options: ['Pb', 'PbO₂', 'PbSO₄', 'H₂SO₄'],
      answerIndex: 1,
      explain: 'PbO₂ 中 Pb 由 +4 降至 +2，得电子作正极。',
    },
    teach: {
      steps: ['第一步：标出放电时各物质价态变化，判断正负极。', '第二步：写出两极反应，注意介质。', '第三步：充电时正接正、负接负。'],
      example: '正极：PbO₂+4H⁺+SO₄²⁻+2e⁻=PbSO₄+2H₂O。',
    },
  },
  {
    id: 'EC7',
    name: '金属腐蚀与防护',
    layer: 'Application',
    summary: '吸氧/析氢腐蚀，牺牲阳极与外加电流法。',
    keyPoints: ['中性或弱酸性环境发生吸氧腐蚀', '吸氧腐蚀正极：O₂+2H₂O+4e⁻=4OH⁻', '牺牲阳极法：接更活泼金属'],
    pitfalls: ['外加电流法中把被保护金属接电源正极'],
    relatedChallengeIds: ['C11'],
    check: {
      question: '钢铁吸氧腐蚀的正极反应式是？',
      options: ['Fe-2e⁻=Fe²⁺', 'O₂+2H₂O+4e⁻=4OH⁻', '2H⁺+2e⁻=H₂↑', 'Fe-3e⁻=Fe³⁺'],
      answerIndex: 1,
      explain: '吸氧腐蚀中 O₂ 在正极得电子生成 OH⁻。',
    },
    teach: {
      steps: ['第一步：由环境判断腐蚀类型（潮湿空气→吸氧）。', '第二步：写正负极反应，负极是 Fe 失电子。', '第三步：选择防护方式（牺牲阳极/外加电流/覆盖层）。'],
      example: '镀锌铁管中 Zn 作负极被腐蚀，保护 Fe（牺牲阳极法）。',
    },
  },
  {
    id: 'EC8',
    name: '电解原理应用',
    layer: 'Ability',
    summary: '氯碱工业、精炼铜、电镀。',
    keyPoints: ['电解饱和食盐水：阳极 Cl₂、阴极 H₂、溶液生成 NaOH', '精炼铜：粗铜作阳极，纯铜作阴极', '电镀：镀层金属作阳极'],
    pitfalls: ['精炼铜时把粗铜接在阴极'],
    relatedChallengeIds: ['C10', 'C3'],
    check: {
      question: '惰性电极电解饱和食盐水，阴极产物是？',
      options: ['Cl₂', 'H₂', 'O₂', 'Na'],
      answerIndex: 1,
      explain: '阴极 H⁺ 得电子生成 H₂，同时阴极区生成 NaOH。',
    },
    teach: {
      steps: ['第一步：确定两极材料与放电离子。', '第二步：写两极反应。', '第三步：归纳工业目的（制碱、制氯气、精炼、电镀）。'],
      example: '2NaCl+2H₂O=电解=2NaOH+H₂↑+Cl₂↑（氯碱工业）。',
    },
  },
]
