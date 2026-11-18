import { KnowledgePoint } from './redox'

// ============================================================
// 水溶液中的离子平衡 · 知识体系（IE1-IE8）
// ============================================================
export const ionicKnowledge: KnowledgePoint[] = [
  {
    id: 'IE1',
    name: '弱电解质电离平衡',
    layer: 'Knowledge',
    summary: '弱酸弱碱部分电离，存在动态电离平衡。',
    keyPoints: ['加水稀释促进电离，电离度增大', '加入同离子抑制电离（同离子效应）', '升温促进电离'],
    pitfalls: ['稀释后电离度增大但 c(H⁺) 减小'],
    relatedChallengeIds: ['I1', 'I10'],
    check: {
      question: '0.1 mol/L 醋酸加水稀释，下列说法正确的是？',
      options: ['c(H⁺) 增大', '电离度增大', '电离度减小', 'c(CH₃COO⁻) 增大'],
      answerIndex: 1,
      explain: '稀释使各离子浓度减小，平衡正向移动，电离度增大，但 c(H⁺) 仍减小。',
    },
    teach: {
      steps: [
        '第一步：写出电离方程式 CH₃COOH ⇌ CH₃COO⁻ + H⁺。',
        '第二步：判断改变条件使平衡向哪边移动（稀释正向、同离子逆向、升温正向）。',
        '第三步：区分"电离度"与"离子浓度"两个不同结论。',
      ],
      example: '0.1 mol/L 醋酸中 c(H⁺)=1.3×10⁻³，电离度 α=1.3×10⁻³/0.1=1.3%。',
    },
  },
  {
    id: 'IE2',
    name: '水的电离与 pH',
    layer: 'Knowledge',
    summary: 'Kw=c(H⁺)·c(OH⁻)，酸和碱都抑制水的电离。',
    keyPoints: ['常温 Kw=1×10⁻¹⁴', '酸/碱抑制水电离，可水解的盐促进水电离', '碱溶液中 H⁺ 全部来自水的电离'],
    pitfalls: ['把"溶液 c(H⁺)"与"水电离出的 c(H⁺)"混为一谈'],
    relatedChallengeIds: ['I6'],
    check: {
      question: '0.01 mol/L NaOH 溶液中，由水电离出的 c(H⁺) 为？',
      options: ['1×10⁻² mol/L', '1×10⁻⁷ mol/L', '1×10⁻¹² mol/L', '1×10⁻¹⁴ mol/L'],
      answerIndex: 2,
      explain: 'c(OH⁻)=10⁻²，c(H⁺)=Kw/c(OH⁻)=10⁻¹²，碱溶液中 H⁺ 全部来自水。',
    },
    teach: {
      steps: [
        '第一步：先判断溶液显酸性还是碱性，确定谁是被抑制的一方。',
        '第二步：用 Kw 求另一种离子浓度。',
        '第三步：碱溶液中"水电离的 c(H⁺)"= 溶液 c(H⁺)；酸溶液中"水电离的 c(OH⁻)"= 溶液 c(OH⁻)。',
      ],
      example: '0.1 mol/L HCl 中水电离的 c(OH⁻)=10⁻¹⁴/0.1=10⁻¹³ mol/L。',
    },
  },
  {
    id: 'IE3',
    name: '盐类水解',
    layer: 'Application',
    summary: '有弱才水解，越弱越水解，谁强显谁性。',
    keyPoints: ['强酸弱碱盐显酸性，强碱弱酸盐显碱性', '多元弱酸根分步水解，第一步为主', '水解程度一般很小'],
    pitfalls: ['认为 NaHCO₃ 只电离不水解（实际水解强于电离，显碱性）'],
    relatedChallengeIds: ['I2', 'I14'],
    check: {
      question: '下列溶液显碱性的是？',
      options: ['NaCl', 'NH₄Cl', 'Na₂CO₃', 'NaHSO₄'],
      answerIndex: 2,
      explain: 'Na₂CO₃ 中 CO₃²⁻ 水解生成 OH⁻，显碱性。',
    },
    teach: {
      steps: [
        '第一步：找出盐中的弱离子（弱酸根或弱碱阳离子）。',
        '第二步：写出水解方程式，判断生成 H⁺ 还是 OH⁻。',
        '第三步：多元弱酸根按第一步水解为主判断酸碱性。',
      ],
      example: 'CO₃²⁻ + H₂O ⇌ HCO₃⁻ + OH⁻，故 Na₂CO₃ 显碱性。',
    },
  },
  {
    id: 'IE4',
    name: '离子浓度大小比较',
    layer: 'Application',
    summary: '先确定溶质与过程主次，再排序。',
    keyPoints: ['不水解的离子浓度最大', '水解程度小，主体离子仍占多数', '按"主要来源"排后续离子'],
    pitfalls: ['忽略水解程度很小，把 OH⁻ 排在主体离子之前'],
    relatedChallengeIds: ['I3', 'I13'],
    check: {
      question: '0.1 mol/L Na₂S 溶液中离子浓度由大到小的顺序是？',
      options: [
        'c(Na⁺)>c(S²⁻)>c(OH⁻)>c(HS⁻)>c(H⁺)',
        'c(Na⁺)>c(OH⁻)>c(S²⁻)>c(HS⁻)>c(H⁺)',
        'c(Na⁺)>c(S²⁻)>c(HS⁻)>c(OH⁻)>c(H⁺)',
        'c(Na⁺)>c(HS⁻)>c(OH⁻)>c(S²⁻)>c(H⁺)',
      ],
      answerIndex: 0,
      explain: 'Na⁺ 最大；S²⁻ 水解程度小排第二；OH⁻ 来自两步水解排第三；HS⁻ 仅第一步产生；H⁺ 最小。',
    },
    teach: {
      steps: [
        '第一步：列出所有离子及其来源（电离/水解）。',
        '第二步：比较水解与电离的相对强弱，确定主体离子。',
        '第三步：按"来源主次"逐级排序，最后用守恒式校验。',
      ],
      example: 'CH₃COONa 溶液：c(Na⁺)>c(CH₃COO⁻)>c(OH⁻)>c(CH₃COOH)>c(H⁺)。',
    },
  },
  {
    id: 'IE5',
    name: '三大守恒关系',
    layer: 'Application',
    summary: '电荷守恒、物料守恒、质子守恒。',
    keyPoints: ['电荷守恒：正电荷总数=负电荷总数', '物料守恒：某元素总量按比例守恒', '质子守恒 = 电荷守恒 − 物料守恒'],
    pitfalls: ['电荷守恒漏乘离子电荷数（如 CO₃²⁻ 系数应为 2）', '物料守恒漏掉分子形态（如 H₂CO₃）'],
    relatedChallengeIds: ['I5', 'I8', 'I13'],
    check: {
      question: '0.1 mol/L NH₄HSO₄ 溶液的物料守恒式是？',
      options: [
        'c(SO₄²⁻)=c(NH₄⁺)+c(NH₃·H₂O)',
        'c(NH₄⁺)=c(SO₄²⁻)+c(H⁺)',
        'c(H⁺)=c(OH⁻)',
        'c(NH₄⁺)=c(SO₄²⁻)',
      ],
      answerIndex: 0,
      explain: 'N 与 S 元素总量均为 0.1 mol/L，故 c(SO₄²⁻)=c(NH₄⁺)+c(NH₃·H₂O)。',
    },
    teach: {
      steps: [
        '第一步：写电荷守恒，注意多价离子的系数。',
        '第二步：写物料守恒，把元素总量按化学式比例列出。',
        '第三步：两式相减得质子守恒，或直接用"得失 H⁺ 相等"写出。',
      ],
      example: 'Na₂CO₃：c(Na⁺)=2[c(CO₃²⁻)+c(HCO₃⁻)+c(H₂CO₃)]；c(OH⁻)=c(H⁺)+c(HCO₃⁻)+2c(H₂CO₃)。',
    },
  },
  {
    id: 'IE6',
    name: '酸碱混合与缓冲溶液',
    layer: 'Application',
    summary: '按过量判断酸碱性，缓冲液用 pH=pKa+lg(c盐/c酸)。',
    keyPoints: ['pH 之和为 14 的强酸与弱碱等体积混合，弱碱过量显碱性', '弱酸与其盐等浓度混合，电离>水解显酸性', '缓冲液 pH=pKa+lg(c盐/c酸)'],
    pitfalls: ['误认为 pH 之和为 14 等体积混合一定呈中性'],
    relatedChallengeIds: ['I4', 'I7', 'I12'],
    check: {
      question: 'pH=3 的盐酸与 pH=11 的氨水等体积混合后，溶液呈？',
      options: ['酸性', '中性', '碱性', '无法判断'],
      answerIndex: 2,
      explain: '氨水是弱碱，pH=11 时实际浓度远大于 10⁻³，混合后氨水过量，显碱性。',
    },
    teach: {
      steps: [
        '第一步：比较酸与碱的"已电离量"和"实际投料量"。',
        '第二步：弱的一方实际浓度更大，等体积混合后过量。',
        '第三步：由过量物判断酸碱性，再用电荷守恒排列离子顺序。',
      ],
      example: '0.1 mol/L CH₃COOH 与等体积 0.1 mol/L NaOH 混合恰好中和生成 CH₃COONa，显碱性。',
    },
  },
  {
    id: 'IE7',
    name: '溶液稀释与电离度',
    layer: 'Application',
    summary: '强酸稀释 10ⁿ 倍 pH 变 n，弱酸变化小于 n。',
    keyPoints: ['强酸每稀释 10ⁿ 倍，pH 增大 n（不超过 7）', '弱酸稀释促进电离，pH 增大小于 n', '电离度 α=已电离浓度/初始浓度'],
    pitfalls: ['无限稀释时仍套用公式（pH 应趋近 7）'],
    relatedChallengeIds: ['I9', 'I10'],
    check: {
      question: '常温下 pH=2 的盐酸稀释 100 倍，所得溶液 pH 为？',
      options: ['1', '3', '4', '6'],
      answerIndex: 2,
      explain: 'c(H⁺) 由 10⁻² 稀释为 10⁻⁴，pH=4。',
    },
    teach: {
      steps: [
        '第一步：由 pH 求原 c(H⁺)。',
        '第二步：除以稀释倍数得新 c(H⁺)。',
        '第三步：换算 pH；注意弱酸要加"电离度增大"的修正。',
      ],
      example: 'pH=3 的醋酸稀释 10 倍，pH 在 3~4 之间（小于 4），因为稀释促进电离。',
    },
  },
  {
    id: 'IE8',
    name: '酸碱滴定与突跃',
    layer: 'Ability',
    summary: '滴定突跃决定指示剂选择，误差来自终点判断。',
    keyPoints: ['0.1 mol/L 强酸强碱滴定突跃约 pH 4.3~9.7', '突跃范围内变色的指示剂都可用（甲基橙、酚酞）', '滴定误差按"多滴/少滴"分析'],
    pitfalls: ['用甲基橙滴定弱碱（终点偏酸，误差大）'],
    relatedChallengeIds: ['I11'],
    check: {
      question: '0.1 mol/L NaOH 滴定 20 mL 0.1 mol/L 盐酸，±0.1% 误差时 pH 突跃范围约为？',
      options: ['3.3~10.7', '4.3~9.7', '5.3~8.7', '7.0~14.0'],
      answerIndex: 1,
      explain: '差 0.02 mL 时 pH≈4.3，过量 0.02 mL 时 pH≈9.7。',
    },
    teach: {
      steps: [
        '第一步：分别计算"少滴 0.1%"与"多滴 0.1%"时的 pH。',
        '第二步：得到突跃范围，选择变色区间落在其中的指示剂。',
        '第三步：用 c(待测)·V(待测)=c(标准)·V(标准) 计算并分析误差。',
      ],
      example: '少滴 0.02 mL：c(H⁺)=0.1×0.02×10⁻³/0.04≈5×10⁻⁵，pH≈4.3。',
    },
  },
]
