// ============================================================
// 氧化还原反应 · 知识体系（知识点 → 检测 → 教学闭环）
// 三层：Knowledge 基础认知 / Application 规律应用 / Ability 综合能力
// ============================================================

export type KnowledgeLayer = 'Knowledge' | 'Application' | 'Ability'

export interface KnowledgeCheck {
  question: string
  options: string[]
  answerIndex: number
  explain: string
}

export interface KnowledgePoint {
  id: string
  name: string
  layer: KnowledgeLayer
  summary: string
  keyPoints: string[]
  pitfalls: string[]
  relatedChallengeIds: string[]
  check: KnowledgeCheck
  teach: { steps: string[]; example: string }
}

export const LAYER_META: Record<KnowledgeLayer, { label: string; icon: string; color: string; desc: string }> = {
  Knowledge: { label: '基础认知', icon: '📖', color: '#2EA7C4', desc: '概念与判断：先弄清"是什么"' },
  Application: { label: '规律应用', icon: '🛠️', color: '#2563eb', desc: '规律与方法：再学会"怎么用"' },
  Ability: { label: '综合能力', icon: '🧠', color: '#7c3aed', desc: '计算与迁移：最后能"解新题"' },
}

export const redoxKnowledge: KnowledgePoint[] = [
  // ==================== 基础认知 ====================
  {
    id: 'K1',
    name: '化合价与价态判断',
    layer: 'Knowledge',
    summary: '氧化还原的一切判断都从"标化合价"开始。',
    keyPoints: [
      '化合物中各元素化合价代数和为 0',
      '单质中元素化合价为 0',
      '常见价态：H +1、O -2、Na +1、Cl -1、S -2',
      '特殊价态：H₂O₂ / Na₂O₂ 中 O 为 -1，Fe₃O₄ 中 Fe 为 +2 和 +3',
    ],
    pitfalls: [
      '把 Na₂O₂ 中的 O 当成 -2（应为 -1）',
      '忽略 Fe₃O₄ 是 FeO·Fe₂O₃ 的混合价态',
    ],
    relatedChallengeIds: ['R14', 'R10'],
    check: {
      question: 'Na₂O₂ 中氧元素的化合价是？',
      options: ['-2', '-1', '0', '-1/2'],
      answerIndex: 1,
      explain: 'Na 为 +1，化合物代数和为 0，2 个 O 共 -2，故每个 O 为 -1。',
    },
    teach: {
      steps: [
        '第一步：写出化合物中各元素，先标"固定价态"（H +1、O -2、Na +1 等）。',
        '第二步：利用"代数和为 0"列方程，求出唯一未知元素的价态。',
        '第三步：遇到过氧化物、超氧化物、混合价态氧化物要单独记忆。',
      ],
      example: 'K₂Cr₂O₇ 中 Cr 的价态：2×(+1) + 2x + 7×(-2) = 0 → x = +6。',
    },
  },
  {
    id: 'K2',
    name: '氧化还原反应的判断',
    layer: 'Knowledge',
    summary: '看反应前后是否有元素化合价发生变化。',
    keyPoints: [
      '有化合价变化的反应 = 氧化还原反应',
      '置换反应一定是氧化还原反应',
      '复分解反应一定不是氧化还原反应',
      '有单质参加的化合/分解反应通常是氧化还原反应',
    ],
    pitfalls: [
      '看到"有氧气参加"就误判为氧化还原（判断依据是化合价，不是"氧"）',
    ],
    relatedChallengeIds: ['R9'],
    check: {
      question: '下列反应中属于氧化还原反应的是？',
      options: ['NaOH + HCl = NaCl + H₂O', 'CaCO₃ =高温= CaO + CO₂↑', 'Fe + CuSO₄ = FeSO₄ + Cu', 'CO₂ + Ca(OH)₂ = CaCO₃↓ + H₂O'],
      answerIndex: 2,
      explain: '只有 C 中 Fe(0→+2)、Cu(+2→0) 化合价变化。A 是复分解，B、D 无价态变化。',
    },
    teach: {
      steps: [
        '第一步：逐项标出反应前后各元素的化合价。',
        '第二步：只要存在一种元素的价态改变，就是氧化还原反应。',
        '第三步：用"置换一定、复分解一定不"快速排除选项。',
      ],
      example: '2NaHCO₃ =加热= Na₂CO₃ + H₂O + CO₂↑，各元素价态均不变，属非氧化还原反应。',
    },
  },
  {
    id: 'K3',
    name: '本质与特征',
    layer: 'Knowledge',
    summary: '特征是化合价升降，本质是电子转移。',
    keyPoints: [
      '外在特征：元素化合价发生升降',
      '内在本质：电子转移（得失或共用电子对偏移）',
      '化合价升高 = 失电子 = 被氧化',
      '化合价降低 = 得电子 = 被还原',
    ],
    pitfalls: ['把"特征"当成"本质"作答（考题常设这种干扰项）'],
    relatedChallengeIds: ['R1', 'R14'],
    check: {
      question: '氧化还原反应的本质是？',
      options: ['元素化合价发生升降', '电子转移（得失或偏移）', '一定有氧气参加', '一定有单质参加'],
      answerIndex: 1,
      explain: '化合价升降是外在特征，电子转移才是本质。',
    },
    teach: {
      steps: [
        '第一步：记忆口诀"升失氧、降得还"。',
        '第二步：区分"特征"（看得到的价态变化）与"本质"（看不见的电子转移）。',
        '第三步：用电子守恒作为一切计算的底层依据。',
      ],
      example: 'Zn + 2HCl = ZnCl₂ + H₂↑：Zn 失 2e⁻ 被氧化，H⁺ 得 2e⁻ 被还原，本质是电子由 Zn 转移给 H⁺。',
    },
  },
  {
    id: 'K4',
    name: '四对核心概念',
    layer: 'Knowledge',
    summary: '氧化剂/还原剂、氧化/还原反应、氧化/还原产物、被氧化/被还原。',
    keyPoints: [
      '还原剂：失电子、化合价升高、被氧化',
      '氧化剂：得电子、化合价降低、被还原',
      '氧化产物：还原剂被氧化后得到的产物',
      '还原产物：氧化剂被还原后得到的产物',
    ],
    pitfalls: [
      '混淆"氧化剂"与"被氧化"（氧化剂本身被还原）',
      '同一物质在同一反应中既作氧化剂又作还原剂（歧化）',
    ],
    relatedChallengeIds: ['R1', 'R6'],
    check: {
      question: '反应 2Na + Cl₂ = 2NaCl 中，氧化剂是？',
      options: ['Na', 'Cl₂', 'NaCl', 'Na⁺'],
      answerIndex: 1,
      explain: 'Cl₂ 中 Cl 由 0 降至 -1，得电子，作氧化剂。Na 失电子作还原剂。',
    },
    teach: {
      steps: [
        '第一步：标价态，找出升高者与降低者。',
        '第二步：升高者所在反应物 = 还原剂；降低者所在反应物 = 氧化剂。',
        '第三步：产物按"谁变来的"归类：来自还原剂的叫氧化产物。',
      ],
      example: '2H₂S + SO₂ = 3S + 2H₂O：H₂S 中 S 升高作还原剂，SO₂ 中 S 降低作氧化剂；S 既是氧化产物又是还原产物。',
    },
  },
  {
    id: 'K5',
    name: '双线桥与单线桥',
    layer: 'Knowledge',
    summary: '两种表示电子转移的规范画法。',
    keyPoints: [
      '双线桥：同种元素从反应物指向生成物，桥上标"得/失 ne⁻"',
      '单线桥：从失电子元素指向得电子元素，桥上只标 ne⁻',
      '箭头不跨过等号（双线桥起止为同种元素）',
      '电子转移总数以双线桥单侧计数为准',
    ],
    pitfalls: ['双线桥箭头两端写成不同元素', '单线桥误写"得/失"字样'],
    relatedChallengeIds: ['R2', 'R7'],
    check: {
      question: '用双线桥表示电子转移时，箭头的起止点是？',
      options: ['同种元素从反应物指向生成物', '从反应物指向另一种反应物', '从生成物指向反应物', '必须从等号左侧跨到右侧不同元素'],
      answerIndex: 0,
      explain: '双线桥要求同种元素、由反应物指向生成物，并在桥上标明得失电子数。',
    },
    teach: {
      steps: [
        '第一步：标出所有变价元素及其变化值。',
        '第二步：双线桥——同种元素画两条桥（升、降各一条），注明"失 ne⁻/得 ne⁻"。',
        '第三步：单线桥——只画一条，从失电子元素指向得电子元素，桥上写"ne⁻"。',
      ],
      example: 'Fe + CuSO₄ = FeSO₄ + Cu：双线桥 Fe(0→+2) 失 2e⁻，Cu(+2→0) 得 2e⁻。',
    },
  },
  {
    id: 'K6',
    name: '电子转移数目计算',
    layer: 'Knowledge',
    summary: '转移电子数 = 单个原子得失电子数 × 变价原子个数。',
    keyPoints: [
      '先确定每种变价元素的单个原子得失电子数',
      '再乘以参与变价的原子个数',
      '歧化反应：转移数 = 升高方向的总失电子数',
      '部分变价时只计算实际变价的那部分原子',
    ],
    pitfalls: ['忘记乘以原子个数（如 1 mol Cl₂ 转移 2 mol e⁻）', '歧化反应错误地把升降相加'],
    relatedChallengeIds: ['R13', 'R5'],
    check: {
      question: '反应 3Cl₂ + 6NaOH = 5NaCl + NaClO₃ + 3H₂O 中，每 3 mol Cl₂ 转移电子数为？',
      options: ['3 mol', '5 mol', '6 mol', '10 mol'],
      answerIndex: 1,
      explain: '1 个 Cl 升至 +5 失 5e⁻，5 个 Cl 降至 -1 共得 5e⁻，转移电子数为 5 mol。',
    },
    teach: {
      steps: [
        '第一步：找出所有变价原子，分别标出单个原子的化合价变化值。',
        '第二步：统计每类变价原子的个数。',
        '第三步：转移电子数 = 单个变化值 × 原子个数（取升高或降低单侧）。',
      ],
      example: '2KMnO₄ + 16HCl = 2KCl + 2MnCl₂ + 5Cl₂↑ + 8H₂O：Mn 得 5e⁻×2 = 10e⁻，即转移 10 mol e⁻。',
    },
  },

  // ==================== 规律应用 ====================
  {
    id: 'K7',
    name: '氧化性/还原性强弱比较',
    layer: 'Application',
    summary: '氧化剂 > 氧化产物；还原剂 > 还原产物。',
    keyPoints: [
      '由已知反应推性质强弱：左强右弱',
      '氧化性：氧化剂 > 氧化产物',
      '还原性：还原剂 > 还原产物',
      '常见氧化性顺序：Cl₂ > Fe³⁺ > I₂ > S',
    ],
    pitfalls: ['把"氧化性"与"还原性"顺序搞反', '忽略同一物质在不同条件下强弱不同（如浓/稀硝酸）'],
    relatedChallengeIds: ['R10'],
    check: {
      question: '已知 2FeCl₃ + 2KI = 2FeCl₂ + 2KCl + I₂，下列氧化性顺序正确的是？',
      options: ['I₂ > Fe³⁺', 'Fe³⁺ > I₂', 'Fe²⁺ > Fe³⁺', 'I⁻ > Fe³⁺'],
      answerIndex: 1,
      explain: 'Fe³⁺ 是氧化剂，I₂ 是氧化产物，氧化剂氧化性强于氧化产物。',
    },
    teach: {
      steps: [
        '第一步：在反应中找出氧化剂和氧化产物（或还原剂与还原产物）。',
        '第二步：套用"左强右弱"——反应物中作氧化剂的 > 产物中作氧化产物的。',
        '第三步：多个反应串联成强弱链，连成完整顺序。',
      ],
      example: '①2FeCl₃+2KI→2FeCl₂+I₂ 得 Fe³⁺>I₂；②2FeCl₂+Cl₂→2FeCl₃ 得 Cl₂>Fe³⁺；串联得 Cl₂>Fe³⁺>I₂。',
    },
  },
  {
    id: 'K8',
    name: '反应先后顺序',
    layer: 'Application',
    summary: '多种还原剂遇同一氧化剂，还原性强的先反应。',
    keyPoints: [
      '强者先行：还原性强的先被氧化',
      '常见还原性顺序：S²⁻ > SO₃²⁻ > I⁻ > Fe²⁺ > Br⁻ > Cl⁻',
      '少量氧化剂只氧化最强还原剂',
      '足量氧化剂才依次氧化后续还原剂',
    ],
    pitfalls: ['少量 Cl₂ 通入 FeBr₂ 时误写成同时氧化 Fe²⁺ 和 Br⁻'],
    relatedChallengeIds: ['R11'],
    check: {
      question: '向 FeBr₂ 溶液中通入少量 Cl₂，优先被氧化的是？',
      options: ['Br⁻', 'Fe²⁺', 'H₂O', 'Cl⁻'],
      answerIndex: 1,
      explain: '还原性 Fe²⁺ > Br⁻，少量 Cl₂ 先氧化 Fe²⁺：2Fe²⁺ + Cl₂ = 2Fe³⁺ + 2Cl⁻。',
    },
    teach: {
      steps: [
        '第一步：列出体系中所有可能的还原剂，按还原性排序。',
        '第二步：判断氧化剂是"少量"还是"足量"。',
        '第三步：少量 → 只氧化最强的那一种；足量 → 依次全部氧化。',
      ],
      example: '足量 Cl₂ 通入 FeBr₂：先 2Fe²⁺+Cl₂=2Fe³⁺+2Cl⁻，后 2Br⁻+Cl₂=Br₂+2Cl⁻，总式 2Fe²⁺+4Br⁻+3Cl₂=2Fe³⁺+2Br₂+6Cl⁻。',
    },
  },
  {
    id: 'K9',
    name: '常见氧化剂/还原剂及产物',
    layer: 'Application',
    summary: '记住高频物质的还原产物，陌生题就能快准稳。',
    keyPoints: [
      'KMnO₄（酸性）：Mn +7 → +2（Mn²⁺）',
      '稀 HNO₃：N +5 → +2（NO）；浓 HNO₃：→ +4（NO₂）',
      'Cl₂：Cl 0 → -1（Cl⁻）',
      'Fe³⁺：Fe +3 → +2（Fe²⁺）',
      'SO₂ / H₂SO₃：S +4 → +6（SO₄²⁻）',
    ],
    pitfalls: ['稀硝酸误生成 NO₂，浓硝酸误生成 NO', 'KMnO₄ 在碱性条件下生成 MnO₂ 而非 Mn²⁺'],
    relatedChallengeIds: ['R14', 'R10', 'R4'],
    check: {
      question: '下列物质在反应中只能作氧化剂的是？',
      options: ['SO₂', 'Fe²⁺', '酸性 KMnO₄', 'I⁻'],
      answerIndex: 2,
      explain: 'KMnO₄ 中 Mn 为最高价 +7，只能降价得电子，只能作氧化剂。',
    },
    teach: {
      steps: [
        '第一步：看核心元素的价态——最高价只氧化、最低价只还原、中间价两性。',
        '第二步：记住高频氧化剂的还原产物（制表记忆）。',
        '第三步：注意介质影响（酸性/碱性/中性产物不同）。',
      ],
      example: '稀硝酸与铜：3Cu + 8HNO₃(稀) = 3Cu(NO₃)₂ + 2NO↑ + 4H₂O，还原产物为 NO。',
    },
  },
  {
    id: 'K10',
    name: '歧化反应与归中反应',
    layer: 'Application',
    summary: '同种元素"一分为二"是歧化，"合二为一"是归中。',
    keyPoints: [
      '歧化：同一元素既升高又降低（如 NO₂、Cl₂ 与碱）',
      '归中：不同价态的同种元素 → 中间价态（如 H₂S + SO₂）',
      '归中反应"只靠拢、不交叉"，产物价态介于反应物之间',
      '歧化反应电子转移数按升高侧计算',
    ],
    pitfalls: ['把归中反应产物写成超出反应物价态范围', '歧化反应配平时两边都乘错系数'],
    relatedChallengeIds: ['R3', 'R5', 'R6'],
    check: {
      question: '反应 3NO₂ + H₂O = 2HNO₃ + NO 属于？',
      options: ['归中反应', '歧化反应', '复分解反应', '置换反应'],
      answerIndex: 1,
      explain: 'NO₂ 中 N(+4) 一部分升至 +5（HNO₃），一部分降至 +2（NO），属歧化反应。',
    },
    teach: {
      steps: [
        '第一步：找出同一元素的两个去向，比较价态。',
        '第二步：一升一降 → 歧化；从两端向中间靠 → 归中。',
        '第三步：用电子守恒配平（歧化：升降两侧相等；归中：高价得电子数 = 低价失电子数）。',
      ],
      example: '归中：2H₂S + SO₂ = 3S↓ + 2H₂O，S(-2) 与 S(+4) 都变为 0，2×2e⁻ 失 = 1×4e⁻ 得。',
    },
  },
  {
    id: 'K11',
    name: '化合价升降法配平',
    layer: 'Application',
    summary: '标价 → 算升降 → 求公倍数 → 配系数 → 补守恒。',
    keyPoints: [
      '先配变价物质，再配不变价物质',
      '得失电子总数相等（最小公倍数）',
      '多元素变价的化合物按整体计算',
      '最后用原子守恒补齐 H、O 等',
    ],
    pitfalls: ['只配平了电子，忘记补 H₂O/H⁺', '多元素变价时漏算某个元素的失电子'],
    relatedChallengeIds: ['R2', 'R5', 'R7'],
    check: {
      question: '配平 Cu + HNO₃(稀) → Cu(NO₃)₂ + NO↑ + H₂O，HNO₃ 的系数是？',
      options: ['4', '6', '8', '10'],
      answerIndex: 2,
      explain: '3Cu + 8HNO₃ = 3Cu(NO₃)₂ + 2NO↑ + 4H₂O，HNO₃ 系数为 8。',
    },
    teach: {
      steps: [
        '第一步：标出所有变价元素的化合价，计算单个原子的升降值。',
        '第二步：求升降值的最小公倍数，确定变价物质的系数。',
        '第三步：用观察法配平不变价物质，最后检查原子和电荷守恒。',
      ],
      example: '4FeS₂ + 11O₂ = 2Fe₂O₃ + 8SO₂：1 mol FeS₂ 失 1+2×5=11e⁻，1 mol O₂ 得 4e⁻，最小公倍数 44。',
    },
  },
  {
    id: 'K12',
    name: '离子方程式配平',
    layer: 'Application',
    summary: '电子守恒定主体，电荷守恒补 H⁺/OH⁻，原子守恒补水。',
    keyPoints: [
      '先配电子守恒（主体物质系数）',
      '再用电荷守恒确定 H⁺ 或 OH⁻ 的系数',
      '最后用 H、O 原子守恒补 H₂O',
      '酸性补 H⁺，碱性补 OH⁻，中性按产物判断',
    ],
    pitfalls: ['只做电子守恒，忘记电荷守恒', '酸性条件下误补 OH⁻'],
    relatedChallengeIds: ['R7', 'R2'],
    check: {
      question: '配平 MnO₄⁻ + Fe²⁺ + H⁺ → Mn²⁺ + Fe³⁺ + H₂O，H⁺ 的系数是？',
      options: ['4', '5', '8', '10'],
      answerIndex: 2,
      explain: 'MnO₄⁻ + 5Fe²⁺ + 8H⁺ = Mn²⁺ + 5Fe³⁺ + 4H₂O，H⁺ 系数为 8。',
    },
    teach: {
      steps: [
        '第一步：用电子守恒配平变价离子（MnO₄⁻ 得 5e⁻，Fe²⁺ 失 1e⁻ → 1:5）。',
        '第二步：数两侧电荷，用 H⁺（酸性）或 OH⁻（碱性）配平电荷。',
        '第三步：用 H、O 原子数补 H₂O 并检查。',
      ],
      example: '酸性 KMnO₄ 滴定 Fe²⁺：MnO₄⁻ + 5Fe²⁺ + 8H⁺ = Mn²⁺ + 5Fe³⁺ + 4H₂O。',
    },
  },

  // ==================== 综合能力 ====================
  {
    id: 'K13',
    name: '电子守恒计算',
    layer: 'Ability',
    summary: '不算中间过程，只看起点与终点的电子得失。',
    keyPoints: [
      'n(失电子) = n(得电子)，可跳过方程式直接列式',
      '金属失电子数 = 金属物质的量 × 化合价升高值',
      '氧化剂得电子数 = 还原产物物质的量 × 单个变化值',
      '多步反应只要起点终点明确，电子守恒仍成立',
    ],
    pitfalls: ['逐条写方程式导致计算冗长易错', '忽略"足量/少量"导致产物价态判断错误'],
    relatedChallengeIds: ['R4', 'R8'],
    check: {
      question: '0.1 mol Cu 与足量稀硝酸反应，转移电子数为？',
      options: ['0.1 mol', '0.2 mol', '0.3 mol', '0.4 mol'],
      answerIndex: 1,
      explain: 'Cu → Cu²⁺ 失 2e⁻，0.1 mol × 2 = 0.2 mol。',
    },
    teach: {
      steps: [
        '第一步：确定氧化剂、还原剂及其最终产物（价态变化值）。',
        '第二步：分别写出失电子总数与得电子总数表达式。',
        '第三步：令二者相等，直接求解未知量。',
      ],
      example: '15.2 g Fe、Cu 混合物溶于足量稀硝酸放出 0.2 mol NO：3x+2y=0.6，56x+64y=15.2，解得 Fe 0.1 mol（5.6 g）。',
    },
  },
  {
    id: 'K14',
    name: '多守恒联合计算',
    layer: 'Ability',
    summary: '电子守恒不够时，联用原子守恒、电荷守恒。',
    keyPoints: [
      '氮元素守恒：n(HNO₃) = n(NO₃⁻剩余) + n(还原产物 N)',
      '钠元素守恒：加 NaOH 沉淀后 n(NaOH) = n(NO₃⁻)',
      '电荷守恒：溶液中正电荷总数 = 负电荷总数',
      '绕过中间价态，直接锁定终态组成',
    ],
    pitfalls: ['试图判断 Fe 最终是 +2 还是 +3 而卡住', '忽略沉淀后溶液溶质只有 NaNO₃'],
    relatedChallengeIds: ['R12'],
    check: {
      question: 'Fe、Fe₂O₃ 混合物溶于 250 mL 2 mol/L HNO₃，生成 1.12 L NO（标况），加 NaOH 使 Fe 完全沉淀，需 NaOH 多少 mol？',
      options: ['0.05 mol', '0.45 mol', '0.5 mol', '0.25 mol'],
      answerIndex: 1,
      explain: 'n(HNO₃)=0.5 mol，n(NO)=0.05 mol，剩余 NO₃⁻=0.45 mol；沉淀后溶质为 NaNO₃，n(NaOH)=0.45 mol。',
    },
    teach: {
      steps: [
        '第一步：先算总投料与已还原部分，得到剩余酸根物质的量。',
        '第二步：分析终点溶液的溶质组成（如 NaNO₃）。',
        '第三步：用元素守恒（Na、N）把"加碱量"与"剩余酸根"直接挂钩。',
      ],
      example: '上题：n(NO₃⁻)=0.5-0.05=0.45 mol → V(NaOH)=0.45/1=450 mL，与 Fe 的价态无关。',
    },
  },
  {
    id: 'K15',
    name: '陌生信息型方程式书写',
    layer: 'Ability',
    summary: '先定角色，再定产物，最后配平守恒。',
    keyPoints: [
      '从题干信息判断谁是氧化剂、谁是还原剂',
      '按"价态变化趋势"推断产物（升到常见高价、降到常见低价）',
      '电子守恒定系数，电荷守恒补 H⁺/OH⁻',
      '注意介质与题干给出的特殊产物',
    ],
    pitfalls: ['产物价态凭空猜测', '忽略题干中"酸性/碱性"条件'],
    relatedChallengeIds: ['R13', 'R4'],
    check: {
      question: '酸性条件下 ClO₂ 将 H₂O₂ 氧化为 O₂，H₂O₂ 在该反应中作？',
      options: ['氧化剂', '还原剂', '催化剂', '既非氧化剂也非还原剂'],
      answerIndex: 1,
      explain: 'H₂O₂ 中 O 由 -1 升至 0（O₂），失电子，作还原剂。',
    },
    teach: {
      steps: [
        '第一步：圈出题干中的"氧化/还原/酸性/碱性"等关键词，确定角色。',
        '第二步：按价态趋势写出产物，并标出每个原子的价态变化。',
        '第三步：电子守恒配主体，电荷守恒补离子，原子守恒补水。',
      ],
      example: '信息：酸性条件下 ClO₂ 氧化 H₂O₂ 生成 O₂，自身还原为 Cl⁻。配平：2ClO₂ + 5H₂O₂ = 2Cl⁻ + 5O₂↑ + 4H₂O + 2H⁺（Cl +4→-1 得 5e⁻×2，O -1→0 失 2e⁻×5）。',
    },
  },
  {
    id: 'K16',
    name: '氧化还原滴定计算',
    layer: 'Ability',
    summary: '找计量关系，用 cV 与电子守恒一步到位。',
    keyPoints: [
      '先写出滴定反应的离子方程式，确定计量比',
      'n = c × V（V 单位换算为 L）',
      '多步滴定用电子守恒串联，不必逐步计算',
      '注意指示剂终点与误差分析',
    ],
    pitfalls: ['忘记把 mL 换算成 L', '计量比用错（如 MnO₄⁻:Fe²⁺ = 1:5）'],
    relatedChallengeIds: [],
    check: {
      question: '25.00 mL 0.1000 mol/L FeSO₄ 用 0.0200 mol/L KMnO₄ 滴定至终点，消耗 KMnO₄ 体积为？',
      options: ['5.00 mL', '10.00 mL', '25.00 mL', '50.00 mL'],
      answerIndex: 2,
      explain: 'MnO₄⁻ + 5Fe²⁺ + 8H⁺ = Mn²⁺ + 5Fe³⁺ + 4H₂O。n(Fe²⁺)=0.0025 mol，n(MnO₄⁻)=0.0005 mol，V=0.0005/0.02=0.025 L=25.00 mL。',
    },
    teach: {
      steps: [
        '第一步：写出配平后的滴定离子方程式，读出计量比。',
        '第二步：由已知浓度和体积求被测物物质的量。',
        '第三步：按计量比求滴定剂物质的量，再除以浓度得体积。',
      ],
      example: 'KMnO₄ 滴定 H₂C₂O₄：2MnO₄⁻ + 5H₂C₂O₄ + 6H⁺ = 2Mn²⁺ + 10CO₂↑ + 8H₂O，计量比 2:5。',
    },
  },
]

/** 知识点 id → 名称 快速索引 */
export const knowledgeById: Record<string, KnowledgePoint> = redoxKnowledge.reduce(
  (acc, kp) => {
    acc[kp.id] = kp
    return acc
  },
  {} as Record<string, KnowledgePoint>
)

/** 题目 id → 涉及知识点 */
export function knowledgeForChallenge(challengeId: string): KnowledgePoint[] {
  return redoxKnowledge.filter((kp) => kp.relatedChallengeIds.includes(challengeId))
}
