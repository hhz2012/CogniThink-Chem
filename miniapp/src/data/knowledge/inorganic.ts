import { KnowledgePoint } from './redox'

// ============================================================
// 元素化合物推断 · 知识体系（IN1-IN8）
// ============================================================
export const inorganicKnowledge: KnowledgePoint[] = [
  {
    id: 'IN1',
    name: '物质性质与用途',
    layer: 'Knowledge',
    summary: '性质决定用途，因果必须真实对应。',
    keyPoints: ['用途必须由所述性质直接决定', '常见：Al₂O₃ 高熔点→耐火材料', 'SiO₂ 光学性质→光导纤维'],
    pitfalls: ['把"颜色"当作蚀刻铜的原因（实为 Fe³⁺ 氧化性）'],
    relatedChallengeIds: ['N1'],
    check: {
      question: '下列性质与用途对应关系正确的是？',
      options: ['SiO₂ 硬度大 → 制造光导纤维', 'NaHCO₃ 受热分解 → 治疗胃酸过多', 'Al₂O₃ 熔点高 → 制造耐火材料', 'FeCl₃ 溶液呈黄色 → 蚀刻铜电路板'],
      answerIndex: 2,
      explain: 'Al₂O₃ 熔点高达 2050℃，适合作耐火材料，因果对应。',
    },
    teach: {
      steps: ['第一步：读用途，问"这个用途真正依赖什么性质"。', '第二步：与选项给出的性质比对，判断因果是否成立。', '第三步：用排除法锁定唯一正确项。'],
      example: 'NaHCO₃ 治胃酸过多是因为能与盐酸反应，不是因为受热分解。',
    },
  },
  {
    id: 'IN2',
    name: '离子检验与鉴别',
    layer: 'Application',
    summary: '先排除干扰，再用特征试剂检验。',
    keyPoints: ['SO₄²⁻：稀盐酸酸化后加 BaCl₂', 'Cl⁻：硝酸酸化后加 AgNO₃', '检验顺序要先除干扰'],
    pitfalls: ['用硝酸酸化检验 SO₄²⁻（会氧化 SO₃²⁻）'],
    relatedChallengeIds: ['N2', 'N14'],
    check: {
      question: '检验溶液中是否含 SO₄²⁻，正确的操作是？',
      options: ['先加 BaCl₂ 再加稀硝酸', '先加稀盐酸酸化，再加 BaCl₂', '先加 Ba(NO₃)₂ 再加稀盐酸', '直接加 AgNO₃ 溶液'],
      answerIndex: 1,
      explain: '盐酸排除 CO₃²⁻、SO₃²⁻、Ag⁺ 干扰，再加 BaCl₂ 有白色沉淀即含 SO₄²⁻。',
    },
    teach: {
      steps: ['第一步：列出会与检验试剂产生相同现象的干扰离子。', '第二步：选择只除去干扰、不引入新干扰的酸化试剂。', '第三步：加特征试剂，由现象得出结论。'],
      example: 'Cl⁻ 检验：稀硝酸酸化排除 CO₃²⁻，再加 AgNO₃ 得白色沉淀。',
    },
  },
  {
    id: 'IN3',
    name: '离子共存',
    layer: 'Application',
    summary: '离子间不发生反应才能大量共存。',
    keyPoints: ['生成沉淀、气体、弱电解质不能共存', '氧化性离子与还原性离子不能共存', '注意隐含的酸碱性条件'],
    pitfalls: ['忽略 H⁺+NO₃⁻ 的强氧化性会氧化 Fe²⁺'],
    relatedChallengeIds: ['N10'],
    check: {
      question: '下列各组离子能大量共存的是？',
      options: ['H⁺、Fe²⁺、NO₃⁻、Cl⁻', 'Na⁺、K⁺、OH⁻、HCO₃⁻', 'Ba²⁺、Na⁺、NO₃⁻、Cl⁻', 'Al³⁺、NH₄⁺、OH⁻、SO₄²⁻'],
      answerIndex: 2,
      explain: 'C 组离子互不反应，可共存。A 中硝酸氧化 Fe²⁺，B 中 OH⁻ 与 HCO₃⁻ 反应，D 中 OH⁻ 与 Al³⁺、NH₄⁺ 反应。',
    },
    teach: {
      steps: ['第一步：逐对检查是否生成沉淀、气体或弱电解质。', '第二步：检查是否存在氧化还原冲突。', '第三步：注意题干隐含条件（酸性/碱性/无色）。'],
      example: 'H⁺+NO₃⁻ 相当于硝酸，能氧化 Fe²⁺：3Fe²⁺+NO₃⁻+4H⁺=3Fe³⁺+NO↑+2H₂O。',
    },
  },
  {
    id: 'IN4',
    name: '元素周期律与推断',
    layer: 'Application',
    summary: '位—构—性关系，半径与金属性递变。',
    keyPoints: ['同周期从左到右原子半径减小', '同主族从上到下原子半径增大', '非金属性越强，气态氢化物越稳定'],
    pitfalls: ['比较半径时忽略电子层数优先'],
    relatedChallengeIds: ['N4', 'N12'],
    check: {
      question: '短周期中，最外层电子数是电子层数 3 倍的元素是？',
      options: ['C', 'N', 'O', 'F'],
      answerIndex: 2,
      explain: 'O 有 2 个电子层、最外层 6 个电子，6=2×3。',
    },
    teach: {
      steps: ['第一步：由特征条件（电子层数、最外层电子数、半径、化合价）锁定元素。', '第二步：确定元素在周期表中的位置。', '第三步：用周期律比较半径、金属性、氢化物稳定性等。'],
      example: '地壳中含量最多的元素是 O；黄绿色气体单质是 Cl₂。',
    },
  },
  {
    id: 'IN5',
    name: '无机框图推断',
    layer: 'Ability',
    summary: '抓特征颜色、状态、反应作为突破口。',
    keyPoints: ['常见淡黄色固体：Na₂O₂、S、AgBr', '特征转化：Na₂O₂→Na₂CO₃/NaOH', '用元素守恒跟踪转化链'],
    pitfalls: ['只凭颜色不看反应条件导致误判'],
    relatedChallengeIds: ['N3', 'N8'],
    check: {
      question: 'Na₂O₂ 与 CO₂ 反应的产物是？',
      options: ['NaOH 和 O₂', 'Na₂CO₃ 和 O₂', 'NaHCO₃', 'Na₂O 和 O₂'],
      answerIndex: 1,
      explain: '2Na₂O₂+2CO₂=2Na₂CO₃+O₂。',
    },
    teach: {
      steps: ['第一步：圈出颜色、状态、气味等特征信息。', '第二步：匹配常见物质的特征性质，确定突破口。', '第三步：沿转化关系顺推/逆推，用守恒验证。'],
      example: '淡黄色固体 + 强碱 + 酸式盐 → 判断为 Na 元素体系：Na₂O₂→Na₂CO₃→NaOH→NaHCO₃。',
    },
  },
  {
    id: 'IN6',
    name: '气体制备与除杂',
    layer: 'Application',
    summary: '先除杂后干燥，除杂试剂不能消耗目标气体。',
    keyPoints: ['Cl₂ 中 HCl：饱和食盐水', 'CO₂ 中 HCl：饱和 NaHCO₃', '最后用浓硫酸/碱石灰干燥'],
    pitfalls: ['用 NaOH 除 Cl₂ 中的 HCl（会把 Cl₂ 也吸收）'],
    relatedChallengeIds: ['N6'],
    check: {
      question: '除去 Cl₂ 中混有的 HCl，应通过的试剂是？',
      options: ['NaOH 溶液', '饱和食盐水', '浓硫酸', '水'],
      answerIndex: 1,
      explain: 'Cl₂ 在饱和食盐水中溶解度小，HCl 易溶被除去。',
    },
    teach: {
      steps: ['第一步：列出气体中的杂质（如 HCl、H₂O）。', '第二步：为每种杂质选择只除杂质不损耗主体的试剂。', '第三步：按"先除杂、后干燥"排序。'],
      example: '实验室制 Cl₂：饱和食盐水除 HCl → 浓硫酸干燥。',
    },
  },
  {
    id: 'IN7',
    name: '化学试剂保存',
    layer: 'Knowledge',
    summary: '按见光分解、腐蚀、与空气反应分类保存。',
    keyPoints: ['Na、K 保存在煤油中', '见光分解的用棕色瓶（浓硝酸、氯水、AgNO₃）', '碱性溶液用橡胶塞'],
    pitfalls: ['NaOH 溶液用玻璃塞（生成 Na₂SiO₃ 粘住）'],
    relatedChallengeIds: ['N9'],
    check: {
      question: '新制氯水应如何保存？',
      options: ['无色透明瓶中', '棕色瓶中避光保存', '带玻璃塞的瓶中', '敞口放置'],
      answerIndex: 1,
      explain: '氯水中 HClO 见光分解，需棕色瓶避光保存。',
    },
    teach: {
      steps: ['第一步：判断试剂的关键性质（见光分解/易挥发/与空气反应/腐蚀玻璃）。', '第二步：匹配保存方式。', '第三步：检查瓶塞与瓶色是否恰当。'],
      example: '浓硝酸见光分解：4HNO₃=光照=4NO₂↑+O₂↑+2H₂O，需棕色瓶。',
    },
  },
  {
    id: 'IN8',
    name: '工业流程分析',
    layer: 'Ability',
    summary: '按操作步骤判断目的与产物，元素守恒跟踪。',
    keyPoints: ['浸取→过滤→转化→分离的常规逻辑', 'Al₂O₃、SiO₂ 溶于 NaOH，Fe₂O₃ 不溶', '关注"过量"对产物的影响'],
    pitfalls: ['把滤渣成分判断错（忽略 SiO₂ 也溶于碱）'],
    relatedChallengeIds: ['N5', 'N13'],
    check: {
      question: '铝土矿（Al₂O₃、Fe₂O₃、SiO₂）加足量 NaOH 后过滤，滤渣主要是？',
      options: ['Al₂O₃', 'Fe₂O₃', 'SiO₂', 'Fe₂O₃ 和 SiO₂'],
      answerIndex: 1,
      explain: 'Al₂O₃ 与 SiO₂ 均溶于 NaOH，Fe₂O₃ 不反应，留在滤渣中。',
    },
    teach: {
      steps: ['第一步：拆解流程每一步的加入物与分离操作。', '第二步：判断每种成分的去向（溶液/滤渣/气体）。', '第三步：用元素守恒串联，分析"过量试剂"带来的影响。'],
      example: '铝土矿→NaOH 溶解→滤液 NaAlO₂→通 CO₂ 得 Al(OH)₃→灼烧→电解得 Al。',
    },
  },
]
