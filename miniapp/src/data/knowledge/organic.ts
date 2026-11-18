import { KnowledgePoint } from './redox'

// ============================================================
// 有机化学 · 知识体系（OR1-OR8）
// ============================================================
export const organicKnowledge: KnowledgePoint[] = [
  {
    id: 'OR1',
    name: '官能团识别与性质',
    layer: 'Knowledge',
    summary: '官能团决定有机物的化学性质。',
    keyPoints: ['C=C：加成、氧化（使溴水/酸性 KMnO₄ 褪色）', '-OH：取代、酯化、氧化', '-COOH：酸性、酯化', '-CHO：银镜反应、与新制 Cu(OH)₂ 反应'],
    pitfalls: ['只找一种官能团而漏掉其他（如既含双键又含羧基）'],
    relatedChallengeIds: ['O1', 'O6', 'O11'],
    check: {
      question: 'CH₂=CH—COOH 分子中含有的官能团是？',
      options: ['只含碳碳双键', '只含羧基', '碳碳双键和羧基', '羟基和羧基'],
      answerIndex: 2,
      explain: '结构中有 C=C 和 -COOH 两种官能团。',
    },
    teach: {
      steps: ['第一步：按结构简式逐段扫描（链、环、取代基）。', '第二步：圈出所有官能团并写出名称。', '第三步：由官能团推断可能发生的反应类型。'],
      example: 'CH₂=CH—COOH 可发生加成、加聚、酯化、中和。',
    },
  },
  {
    id: 'OR2',
    name: '有机反应类型',
    layer: 'Application',
    summary: '取代、加成、氧化、消去、酯化、水解的判定。',
    keyPoints: ['加成：双键/三键变单键', '取代：一个原子或团被替换', '消去：生成双键并脱去小分子', '氧化：加氧或脱氢'],
    pitfalls: ['把乙烯使溴水褪色误判为氧化反应（属加成）'],
    relatedChallengeIds: ['O2', 'O7', 'O14'],
    check: {
      question: '乙烯使溴水褪色，该反应属于？',
      options: ['取代反应', '加成反应', '氧化反应', '消去反应'],
      answerIndex: 1,
      explain: 'C=C 与 Br₂ 发生加成，生成 1,2-二溴乙烷。',
    },
    teach: {
      steps: ['第一步：对比反应物与产物的结构差异。', '第二步：按"加进去/换出来/脱出去"归类。', '第三步：注意区分"使溴水褪色"（加成）与"使酸性 KMnO₄ 褪色"（氧化）。'],
      example: 'CH₂=CH₂+Br₂→CH₂BrCH₂Br（加成）。',
    },
  },
  {
    id: 'OR3',
    name: '同分异构体',
    layer: 'Ability',
    summary: '按碳链异构→位置异构→官能团异构顺序枚举。',
    keyPoints: ['先写碳链，再移官能团位置', '注意题目是否限定"能与某试剂反应"', '羧酸与酯互为官能团异构'],
    pitfalls: ['遗漏支链结构或重复计数'],
    relatedChallengeIds: ['O3', 'O12'],
    check: {
      question: '分子式为 C₅H₁₀O₂ 且能与 NaHCO₃ 反应放出 CO₂ 的有机物有几种？',
      options: ['3 种', '4 种', '5 种', '6 种'],
      answerIndex: 1,
      explain: '应为羧酸：C₄H₉—COOH，丁基有 4 种，故共 4 种。',
    },
    teach: {
      steps: ['第一步：由分子式和不饱和度判断可能的结构类型。', '第二步：按"碳链→位置→官能团"系统枚举，不重不漏。', '第三步：用题干限定条件筛除不符合的。'],
      example: 'C₄H₉— 有正丁基、仲丁基、异丁基、叔丁基 4 种。',
    },
  },
  {
    id: 'OR4',
    name: '同系物',
    layer: 'Knowledge',
    summary: '结构相似、组成相差一个或若干个 CH₂。',
    keyPoints: ['同系物官能团种类和数目相同', '分子式相差 n 个 CH₂', '同分异构体一定不是同系物'],
    pitfalls: ['把分子式不同但结构不同的物质当作同系物'],
    relatedChallengeIds: ['O9'],
    check: {
      question: '下列各组物质互为同系物的是？',
      options: ['C₂H₄ 和 C₃H₆', 'CH₃OH 和 C₂H₅OH', '葡萄糖和蔗糖', 'CH₃COOH 和 HCOOCH₃'],
      answerIndex: 1,
      explain: '甲醇与乙醇结构相似、相差 CH₂，互为同系物。C₃H₆ 可能是环丙烷，A 不严谨。',
    },
    teach: {
      steps: ['第一步：确认官能团种类与数目是否相同。', '第二步：比较分子式是否相差 n 个 CH₂。', '第三步：排除同分异构体（分子式相同）。'],
      example: 'CH₄、C₂H₆、C₃H₈ 互为同系物。',
    },
  },
  {
    id: 'OR5',
    name: '有机推断',
    layer: 'Ability',
    summary: '由分子式、反应现象反推结构。',
    keyPoints: ['银镜反应/新制 Cu(OH)₂ → 醛基', '与 Na 放 H₂ → 羟基或羧基', '水解生成两种物质 → 酯'],
    pitfalls: ['忽略"能与 NaHCO₃ 反应"排除酚羟基（酚不与 NaHCO₃ 反应）'],
    relatedChallengeIds: ['O4', 'O13'],
    check: {
      question: '某酯 C₄H₈O₂ 水解后，一种产物能发生银镜反应，另一种能与 Na 反应放出 H₂，该酯是？',
      options: ['CH₃COOCH₂CH₃', 'HCOOCH₂CH₂CH₃', 'CH₃CH₂COOCH₃', 'CH₃COOCH(CH₃)₂'],
      answerIndex: 1,
      explain: '水解得甲酸（银镜）和丙醇（与 Na 放 H₂），故为甲酸丙酯。',
    },
    teach: {
      steps: ['第一步：把每个现象翻译成官能团（银镜→醛基，Na→羟基/羧基）。', '第二步：由分子式确定不饱和度与可能骨架。', '第三步：拼接片段，验证所有条件。'],
      example: 'HCOOCH₂CH₂CH₃ 水解 → HCOOH（银镜）+ CH₃CH₂CH₂OH（与 Na 放 H₂）。',
    },
  },
  {
    id: 'OR6',
    name: '有机合成路线',
    layer: 'Ability',
    summary: '逆推目标物，选择合理反应与条件。',
    keyPoints: ['从目标物逆推所需官能团', '注意官能团保护与引入顺序', '常用：烯→醇→醛→酸→酯'],
    pitfalls: ['合成路线中出现无法实现的反应条件'],
    relatedChallengeIds: ['O5'],
    check: {
      question: '以乙烯为原料合成乙酸乙酯，下列反应类型不需要的是？',
      options: ['加成反应', '氧化反应', '取代（酯化）反应', '消去反应'],
      answerIndex: 3,
      explain: '乙烯→乙醇（加成）→乙醛（氧化）→乙酸（氧化）→乙酸乙酯（酯化），不需消去。',
    },
    teach: {
      steps: ['第一步：写出目标物的结构，逆推上一步原料。', '第二步：为每步选择可行反应类型与条件。', '第三步：检查官能团引入顺序与保护策略。'],
      example: '乙烯→乙醇：CH₂=CH₂+H₂O→C₂H₅OH（加成，催化剂加热加压）。',
    },
  },
  {
    id: 'OR7',
    name: '燃烧法确定组成',
    layer: 'Application',
    summary: 'n(C)=n(CO₂)，n(H)=2n(H₂O)。',
    keyPoints: ['CO₂ 的物质的量 = 碳的物质的量', 'H₂O 的物质的量 ×2 = 氢的物质的量', '由质量差判断是否含氧'],
    pitfalls: ['忘记水的氢是 2 倍'],
    relatedChallengeIds: ['O10'],
    check: {
      question: '某有机物燃烧产物中 CO₂ 与 H₂O 的物质的量之比为 1:1，则 C:H 为？',
      options: ['1:1', '1:2', '1:3', '2:1'],
      answerIndex: 1,
      explain: 'n(C):n(H)=n(CO₂):2n(H₂O)=1:2。',
    },
    teach: {
      steps: ['第一步：由 CO₂ 求 n(C)，由 H₂O 求 n(H)。', '第二步：用总质量减去 C、H 质量，判断是否有氧。', '第三步：求最简式并结合相对分子质量确定分子式。'],
      example: '4.6 g 乙醇燃烧生成 0.2 mol CO₂、0.3 mol H₂O → C₂H₆O。',
    },
  },
  {
    id: 'OR8',
    name: '分离与提纯',
    layer: 'Application',
    summary: '不引入新杂质、不损耗目标物。',
    keyPoints: ['乙醇中少量水：加生石灰蒸馏', '乙酸乙酯中乙酸：饱和 Na₂CO₃ 分液', '苯中苯酚：加 NaOH 分液'],
    pitfalls: ['用浓硫酸干燥乙醇（会反应/引入杂质）'],
    relatedChallengeIds: ['O8'],
    check: {
      question: '除去乙醇中少量的水，最合理的方法是？',
      options: ['加浓硫酸蒸馏', '加生石灰后蒸馏', '加金属钠', '过滤'],
      answerIndex: 1,
      explain: '生石灰与水反应生成 Ca(OH)₂，再蒸馏得无水乙醇。',
    },
    teach: {
      steps: ['第一步：分析目标物与杂质的性质差异（沸点、溶解性、酸碱性）。', '第二步：选择只与杂质作用或只改变杂质状态的试剂。', '第三步：选择对应分离操作（蒸馏/分液/过滤/萃取）。'],
      example: '乙酸乙酯（含乙酸）：加饱和 Na₂CO₃ 溶液分液，乙酸生成可溶性钠盐。',
    },
  },
]
