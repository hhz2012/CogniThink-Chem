// 反应脚手架模型注册表（与题型侧 engine/solveModels.ts 平行）
// 为每个 ReactionSolveKind 定义标准拆解步骤（思考骨架），steps 复用题型侧 SolveModelStep 类型。
// 无真值数据时降级展示各步通用 analyze（永不崩）；真值实例化由 Task 2 的 narrateReaction 负责。
// 与题型侧对齐：氧化还原/电化学的重叠步骤标题保持一致，便于复用叙述逻辑。

import { SolveModelStep, ModelLayer } from './solveModels'
import { ReactionSolveKind } from '../types/reaction'

/** 反应脚手架模型 */
export interface ReactionSolveModel {
  kind: ReactionSolveKind
  /** 模型名，如「多变一化合模型」 */
  name: string
  /** 该反应类型的标准拆解步骤（思考骨架） */
  steps: SolveModelStep[]
  /** 该反应类型的专属校验检查项 */
  checks: string[]
}

// 有机反应共享前置步骤（所有有机子类型模型都先经过这两步，再进入子类型专属步骤）
const ORGANIC_SHARED_STEPS: SolveModelStep[] = [
  {
    index: 1,
    title: '认官能团',
    goal: '识别反应物中的官能团，确定可能的反应位点',
    layer: 'Knowledge',
    analyze:
      '先看反应物的结构简式，逐一识别其中的官能团（碳碳双键、羟基、醛基、羧基、酯基、卤素原子等）；回忆各官能团的特征反应，初步判断这条反应可能发生在哪类官能团上。',
  },
  {
    index: 2,
    title: '定反应类型',
    goal: '由试剂与反应条件判断所属的有机反应子类型',
    layer: 'Application',
    analyze:
      '反应条件是判断反应类型的关键信号：光照→烷烃卤代（取代）；Ni/H₂→加成（还原）；浓硫酸/△→酯化或消去；Cu/O₂/△→醇氧化；NaOH 水溶液→水解；NaOH 醇溶液/△→消去；浓硝酸/浓硫酸→硝化（取代）。结合官能团与条件锁定子类型。',
  },
]

const layer = (l: ModelLayer): ModelLayer => l

export const REACTION_SOLVE_MODELS: Record<ReactionSolveKind, ReactionSolveModel> = {
  // ➕ 化合反应：多变一
  'reaction-combination': {
    kind: 'reaction-combination',
    name: '多变一化合模型',
    steps: [
      {
        index: 1,
        title: '认特征',
        goal: '判断是否「多变一」：反应物 ≥ 2、产物 = 1',
        layer: layer('Knowledge'),
        analyze:
          '数出反应物和产物的种数：化合反应的特征是「多变一」——两种或两种以上物质生成一种物质。注意结晶水合物（如 CuSO₄·5H₂O）不是化合反应，而是水合物的形成与分解。',
      },
      {
        index: 2,
        title: '判条件',
        goal: '确定反应条件对产物的影响',
        layer: layer('Application'),
        analyze:
          '同样的反应物在不同条件下产物可能不同：碳在氧气充足时生成 CO₂、不足时生成 CO；铁在氧气中点燃生成 Fe₃O₄；铜与氯气反应生成 CuCl₂。先把条件看准，再定产物。',
      },
      {
        index: 3,
        title: '析价态',
        goal: '若有价态变化则属氧化还原，标注氧化剂与还原剂',
        layer: layer('Application'),
        analyze:
          '标出各元素反应前后的化合价：有单质参与的化合反应通常是氧化还原反应（如 2Na+Cl₂=2NaCl）；而像 CaO+H₂O=Ca(OH)₂ 这类没有单质参与、各元素价态不变的化合反应则不是氧化还原。',
      },
      {
        index: 4,
        title: '验守恒',
        goal: '配平并检查原子守恒',
        layer: layer('Ability'),
        analyze:
          '在确定产物后配平方程式，使左右两边各元素的原子个数相等；标注反应条件和必要的状态符号，最后复核一遍配平系数是否最简、原子是否守恒。',
      },
    ],
    checks: ['反应物是否 ≥ 2、产物是否 = 1（多变一）', '条件不同时产物是否判断正确', '有单质参与的是否标出氧化还原', '配平后原子是否守恒'],
  },

  // ✂️ 分解反应：一变多
  'reaction-decomposition': {
    kind: 'reaction-decomposition',
    name: '一变多分解模型',
    steps: [
      {
        index: 1,
        title: '认特征',
        goal: '判断是否「一变多」：反应物 = 1、产物 ≥ 2',
        layer: layer('Knowledge'),
        analyze:
          '分解反应的特征是「一变多」——一种物质生成两种或两种以上物质。注意与结晶水合物失去结晶水的过程区分，后者本质上也是分解反应的一种。',
      },
      {
        index: 2,
        title: '判条件',
        goal: '确定分解所需的外加能量类型',
        layer: layer('Knowledge'),
        analyze:
          '分解反应通常需要外加能量：加热（KMnO₄、KClO₃）、高温（CaCO₃ 分解）、通电（电解水）、催化剂（H₂O₂ 在 MnO₂ 下分解）。条件不同，分解的方式和产物都可能不同。',
      },
      {
        index: 3,
        title: '定产物',
        goal: '按条件确定分解产物',
        layer: layer('Application'),
        analyze:
          '根据反应物和条件确定产物：KMnO₄ 受热分解为 K₂MnO₄、MnO₂ 和 O₂；KClO₃ 催化加热分解为 KCl 和 O₂；CaCO₃ 高温分解为 CaO 和 CO₂。注意变价元素在产物中的价态（如锰的 +6、+4 价）。',
      },
      {
        index: 4,
        title: '验守恒',
        goal: '配平，标注气体 ↑ 与沉淀 ↓',
        layer: layer('Ability'),
        analyze:
          '配平方程式使原子守恒；分解产生的单质气体（如 O₂、CO₂、NH₃）要标注 ↑；生成难溶物标注 ↓。最后复核系数最简、原子守恒。',
      },
    ],
    checks: ['反应物是否 = 1、产物是否 ≥ 2（一变多）', '分解条件是否标注正确', '产物与价态是否与条件匹配', '配平后原子是否守恒，气标是否正确'],
  },

  // 🔁 置换反应：单质 + 化合物 → 新单质 + 新化合物
  'reaction-displacement': {
    kind: 'reaction-displacement',
    name: '活动性置换模型',
    steps: [
      {
        index: 1,
        title: '认特征',
        goal: '判断是否「单质 + 化合物 → 新单质 + 新化合物」',
        layer: layer('Knowledge'),
        analyze:
          '置换反应的通式是 A + BC → B + AC：一种单质与一种化合物反应，生成另一种单质和另一种化合物。常见的有金属置换金属（活泼金属置换不活泼金属）、金属置换氢（活泼金属置换酸中的氢）、非金属置换金属（H₂、C 还原金属氧化物）。',
      },
      {
        index: 2,
        title: '判活动性',
        goal: '用活动性顺序判断反应能否发生及方向',
        layer: layer('Application'),
        analyze:
          '依据金属活动性顺序（K Ca Na Mg Al Zn Fe Sn Pb (H) Cu Hg Ag Pt Au）：排在前面的金属能把后面的金属从其化合物溶液中置换出来；排在氢前面的金属能置换出酸中的氢。注意 K、Ca、Na 等极活泼金属会先与水反应，不能直接置换盐溶液中的金属。',
      },
      {
        index: 3,
        title: '写产物',
        goal: '写出被置换出的单质与新化合物，注意价态',
        layer: layer('Application'),
        analyze:
          '写出置换产物：被置换出的单质 + 新生成的化合物。特别注意铁在置换反应中通常显 +2 价（生成 FeSO₄、FeCl₂ 等亚铁化合物），而不是 +3 价；铝热反应中 Al 置换出金属单质并生成 Al₂O₃。',
      },
      {
        index: 4,
        title: '验方向与守恒',
        goal: '强置换弱，配平原子与电子守恒',
        layer: layer('Ability'),
        analyze:
          '验证方向是否符合「强置换弱」的活动性规律；配平方程式，检查原子守恒；若涉及电子转移，确认失电子总数等于得电子总数；标注反应条件（高温、加热等）。',
      },
    ],
    checks: ['是否符合「单质+化合物→新单质+新化合物」', '活动性顺序是否支撑反应发生', '铁的价态是否为 +2（亚铁）', '配平与电子守恒是否成立'],
  },

  // 🔀 复分解反应：两化合物互相交换成分
  'reaction-double-displacement': {
    kind: 'reaction-double-displacement',
    name: '交换成分复分解模型',
    steps: [
      {
        index: 1,
        title: '认特征',
        goal: '判断是否「两化合物互相交换成分」',
        layer: layer('Knowledge'),
        analyze:
          '复分解反应的通式是 AB + CD → AD + CB：两种化合物互相交换成分，生成另外两种化合物。反应中各元素的化合价都不发生变化，这是复分解反应区别于氧化还原反应的关键。',
      },
      {
        index: 2,
        title: '判发生条件',
        goal: '检查是否生成沉淀 / 气体 / 水 之一',
        layer: layer('Application'),
        analyze:
          '复分解反应发生的条件是生成物中有沉淀、气体或水（三者至少其一）。要熟记溶解性表判断沉淀；常见气体有 CO₂（H⁺ 与 HCO₃⁻ 或 CO₃²⁻ 反应）、NH₃（铵盐与碱反应）；水由 H⁺ 与 OH⁻ 结合生成。',
      },
      {
        index: 3,
        title: '拆离子写式子',
        goal: '写离子方程式，强酸强碱可溶盐拆写',
        layer: layer('Application'),
        analyze:
          '写离子方程式时，强酸（HCl、H₂SO₄、HNO₃）、强碱（NaOH、KOH、Ba(OH)₂）和可溶盐要拆成离子形式；弱电解质、沉淀、气体、氧化物和水不能拆，保留化学式。注意 BaSO₄、AgCl 等难溶盐不拆。',
      },
      {
        index: 4,
        title: '验守恒',
        goal: '电荷守恒 + 原子守恒',
        layer: layer('Ability'),
        analyze:
          '配平离子方程式后必须同时检查两项：一是原子守恒（各元素原子个数左右相等），二是电荷守恒（方程式两边正负电荷的代数和相等）。两项都满足才配平正确。',
      },
    ],
    checks: ['各元素化合价是否不变（非氧化还原）', '是否满足沉淀/气体/水之一的发生条件', '离子方程式拆写是否正确', '电荷与原子是否双守恒'],
  },

  // 🔥 燃烧
  'reaction-combustion': {
    kind: 'reaction-combustion',
    name: '燃烧氧化模型',
    steps: [
      {
        index: 1,
        title: '认可燃物与助燃物',
        goal: '确定可燃物与助燃剂（通常为 O₂）',
        layer: layer('Knowledge'),
        analyze:
          '先确定可燃物（C、S、P、H₂、烃、醇等）和助燃物（通常是 O₂）。记住特殊情况：镁不仅能在 O₂ 中燃烧，还能在 CO₂、N₂ 中燃烧；氢气在氯气中也能燃烧。助燃物不一定是氧气。',
      },
      {
        index: 2,
        title: '判完全/不完全',
        goal: '由氧气量判断氧化产物',
        layer: layer('Application'),
        analyze:
          '氧气是否充足决定燃烧产物：碳充分燃烧生成 CO₂、不充分生成 CO；烃类完全燃烧生成 CO₂ 和 H₂O；硫燃烧生成 SO₂（注意不是 SO₃，SO₃ 需催化剂条件下由 SO₂ 进一步氧化）。先判断完全还是不完全，再写产物。',
      },
      {
        index: 3,
        title: '析价态与能量',
        goal: '燃烧均为氧化还原且放热',
        layer: layer('Application'),
        analyze:
          '燃烧反应都是氧化还原反应，且都是放热反应（ΔH < 0）。标出各元素的价态变化：可燃物中被氧化的元素化合价升高，氧气中氧由 0 价降为 -2 价。同时回忆特征现象（火焰颜色、发光等）。',
      },
      {
        index: 4,
        title: '配平燃烧式',
        goal: '用 C/H/O 原子守恒配平',
        layer: layer('Ability'),
        analyze:
          '配平烃（CxHy）燃烧通式：先配 C（x 个 CO₂），再配 H（y/2 个 H₂O），最后配平 O₂ 的系数。配平后检查 C、H、O 原子个数左右相等；标注点燃条件。',
      },
    ],
    checks: ['可燃物与助燃物是否判断正确（含特殊助燃物）', '完全/不完全燃烧产物是否正确', '燃烧是否标为放热、氧化还原', 'C/H/O 配平是否正确'],
  },

  // ⚡ 氧化还原（归中/歧化/复杂氧还）—— 与题型侧重叠步骤对齐
  'reaction-redox': {
    kind: 'reaction-redox',
    name: '电子守恒氧还模型',
    steps: [
      {
        index: 1,
        title: '标价态',
        goal: '标出所有变价元素反应前后的化合价',
        layer: layer('Knowledge'),
        analyze:
          '写出反应方程式，逐一标出各元素反应前后的化合价，重点盯住化合价发生变化的元素。注意同一元素可能有多种价态（如 Fe²⁺/Fe³⁺、N 的 +1 到 +5），过氧化物中氧为 -1 价（不是 -2）。',
      },
      {
        index: 2,
        title: '算电子',
        goal: '计算每个变价元素的转移电子数与总数',
        layer: layer('Application'),
        analyze:
          '对每个变价元素，用终态价态减去起始价态得到价态差，取绝对值乘以一个分子中该元素的变价原子数，就是一个分子转移的电子数。化合价升高对应失电子（被氧化），降低对应得电子（被还原）。歧化、归中反应要注意同一元素既升高又降低。',
      },
      {
        index: 3,
        title: '配守恒',
        goal: '电子守恒 → 原子守恒 → 电荷守恒',
        layer: layer('Ability'),
        analyze:
          '先求失电子总数和得电子总数的最小公倍数，用它除以各自的转移电子数得到变价物质的系数比；再用观察法配平其余物质，最后检查原子守恒。离子反应还要配电荷：酸性介质用 H⁺ 和 H₂O 配平，碱性用 OH⁻ 和 H₂O 配平。',
      },
      {
        index: 4,
        title: '验方向',
        goal: '用强弱律验证反应方向是否合理',
        layer: layer('Ability'),
        analyze:
          '氧化还原反应的方向遵循「强氧化剂 + 强还原剂 → 弱氧化产物 + 弱还原产物」。用金属活动性顺序或电极电势验证：氧化剂的氧化性要强于氧化产物，还原剂的还原性要强于还原产物。注意介质会影响氧化性（如 MnO₄⁻ 在酸性、中性、碱性中的还原产物不同）。',
      },
    ],
    checks: ['变价元素是否全部标出（含过氧根 -1 价）', '失电子总数是否 = 得电子总数', '配平后原子、电荷是否双守恒', '反应方向是否符合强弱律'],
  },

  // 🔋 电极反应 —— 与题型侧「电化学」标题对齐
  'reaction-electrode': {
    kind: 'reaction-electrode',
    name: '电极·半反应模型',
    steps: [
      {
        index: 1,
        title: '判装置',
        goal: '判断是原电池还是电解池，确定电极名称',
        layer: layer('Knowledge'),
        analyze:
          '先看有没有外接电源：有外接电源的是电解池，没有的是原电池。原电池中较活泼金属作负极、发生氧化反应，另一极为正极、发生还原反应；电解池中与电源正极相连的是阳极（氧化），与负极相连的是阴极（还原）。',
      },
      {
        index: 2,
        title: '定电极与放电顺序',
        goal: '确定各电极名称及放电物种',
        layer: layer('Application'),
        analyze:
          '确定电极后判断放电物种：原电池负极是活泼金属失电子；电解池阳极放电顺序为活性电极 > 阴离子（S²⁻>I⁻>Br⁻>Cl⁻>OH⁻>含氧酸根），阴极放电顺序为阳离子得电子（Ag⁺>Fe³⁺>Cu²⁺>H⁺>…）。注意活性电极（如 Cu、Ag 作阳极）自身会溶解。',
      },
      {
        index: 3,
        title: '写半反应',
        goal: '按介质配平两极的电极反应式',
        layer: layer('Application'),
        analyze:
          '负极/阳极写失电子的氧化反应，正极/阴极写得电子的还原反应。配平时先配原子再配电荷：酸性溶液用 H⁺ 和 H₂O 配平，碱性用 OH⁻ 和 H₂O 配平，熔融碳酸盐/氧化物介质写法不同。每个半反应都要写出 e⁻。',
      },
      {
        index: 4,
        title: '合并验守恒',
        goal: '电子守恒配平两电极，写出总反应',
        layer: layer('Ability'),
        analyze:
          '将两个半反应的电子数配成相等（乘以适当系数）后相加，消去电子得到总反应式。检查总反应的原子守恒和电荷守恒，并与实验现象、产物相对应。电子从负极经外电路流向正极，溶液中靠离子定向移动导电。',
      },
    ],
    checks: ['原电池/电解池是否判断正确（有无外接电源）', '电极名称与反应类型（氧/还）是否对应', '半反应配平是否符合介质规则', '合并后电子、原子是否守恒'],
  },

  // 💧 离子平衡（电离/水解/沉淀溶解等可逆平衡）
  'reaction-ion-equilibrium': {
    kind: 'reaction-ion-equilibrium',
    name: '可逆平衡移动模型',
    steps: [
      {
        index: 1,
        title: '认平衡类型',
        goal: '区分电离、水解、沉淀溶解、络合平衡',
        layer: layer('Knowledge'),
        analyze:
          '先判断属于哪类平衡：弱电解质的电离平衡（如 CH₃COOH ⇌ CH₃COO⁻+H⁺）、盐类的水解平衡（如 NH₄⁺+H₂O ⇌ NH₃·H₂O+H⁺）、难溶电解质的沉淀溶解平衡（如 AgCl(s) ⇌ Ag⁺+Cl⁻），还是络合平衡。不同类型的平衡常数写法不同。',
      },
      {
        index: 2,
        title: '写平衡式',
        goal: '用可逆号书写平衡，注意部分电离/水解',
        layer: layer('Application'),
        analyze:
          '用可逆号（⇌）书写平衡方程式。弱电解质部分电离，不拆；多元弱酸分步电离（以第一步为主）；盐类水解一般很微弱，不写 ↑、↓（彻底双水解除外）。沉淀溶解平衡要标明固体状态 (s)。',
      },
      {
        index: 3,
        title: '判移动方向',
        goal: '用勒夏特列原理判浓度/温度/同离子效应的影响',
        layer: layer('Application'),
        analyze:
          '用勒夏特列原理：平衡向「减弱改变」的方向移动。升温促进电离和水解（电离、水解均吸热）；加水稀释促进电离和水解；加入相同离子（同离子效应）抑制电离或水解；加入能与某离子反应的物质促进平衡移动。',
      },
      {
        index: 4,
        title: '写守恒式',
        goal: '写出电荷 / 物料 / 质子守恒关系式',
        layer: layer('Ability'),
        analyze:
          '对溶液体系写出三大守恒式：电荷守恒（阳离子正电荷总数 = 阴离子负电荷总数，浓度乘电荷数）、物料守恒（关键元素在各粒子中的浓度总和 = 起始浓度）、质子守恒（可由电荷守恒减物料守恒得到，也可直接分析水电离的 H⁺ 与 OH⁻ 去向）。',
      },
    ],
    checks: ['平衡类型判断是否正确（电离/水解/沉淀溶解）', '是否用可逆号、是否漏标 (s)', '移动方向是否符合勒夏特列原理', '三大守恒式是否书写正确'],
  },

  // 🌡️ 热化学
  'reaction-thermochemical': {
    kind: 'reaction-thermochemical',
    name: '反应热 ΔH 模型',
    steps: [
      {
        index: 1,
        title: '写配平方程',
        goal: '先写出普通化学方程式并配平',
        layer: layer('Application'),
        analyze:
          '先写出反应的化学方程式并配平。热化学方程式的计量数表示物质的量，可以是分数（如 1/2 O₂），但 ΔH 的数值要与计量数对应成比例。',
      },
      {
        index: 2,
        title: '标状态',
        goal: '标注各物质的状态 g / l / s / aq',
        layer: layer('Application'),
        analyze:
          '在热化学方程式中每种物质都必须标注状态：气态 g、液态 l、固态 s、溶液 aq。同一反应物质状态不同，ΔH 数值不同（如水的 g 和 l 状态，ΔH 相差汽化热）。',
      },
      {
        index: 3,
        title: '算 ΔH',
        goal: '用键能或盖斯定律计算反应热',
        layer: layer('Ability'),
        analyze:
          '计算 ΔH：可由反应物总键能减去生成物总键能（断键吸热、成键放热）；也可用盖斯定律，将已知热化学方程式叠加消去中间产物得到目标反应的 ΔH。注意符号：放热反应 ΔH < 0，吸热 ΔH > 0；计量数加倍时 ΔH 也加倍。可逆反应的 ΔH 指完全反应时的热效应。',
      },
      {
        index: 4,
        title: '验',
        goal: '检查 ΔH 数值、符号、单位与方程对应',
        layer: layer('Ability'),
        analyze:
          '最终检查：ΔH 的正负号是否正确（放热为负）、数值是否与配平计量数对应、单位是否为 kJ/mol、状态是否标注齐全。与教材或题给数据对照，确保一致。',
      },
    ],
    checks: ['方程式是否配平（计量数可为分数）', '各物质状态是否标注（g/l/s/aq）', 'ΔH 符号与数值是否正确（放热为负）', 'ΔH 是否与计量数成比例'],
  },

  // ============ 有机反应子类型（共享前置 2 步 + 子类型专属 3 步） ============

  // 取代反应
  'reaction-organic-substitution': {
    kind: 'reaction-organic-substitution',
    name: '取代反应模型',
    steps: [
      ...ORGANIC_SHARED_STEPS,
      {
        index: 3,
        title: '定取代位点与试剂',
        goal: '确定哪个原子/基团被取代、用什么试剂',
        layer: layer('Application'),
        analyze:
          '根据子类型确定取代位点和试剂：烷烃在光照下与卤素单质发生自由基取代（可逐步取代到 CCl₄）；苯环在 Fe 催化下与液溴卤代、在浓硝酸/浓硫酸下硝化；醇的羟基可被卤素取代（与 HX 反应）。注意反应条件决定取代位置与程度。',
      },
      {
        index: 4,
        title: '写产物',
        goal: '写出取代产物，注意一取代/多取代',
        layer: layer('Application'),
        analyze:
          '写出取代产物：被取代的原子/基团被新基团替换。注意甲烷氯代可逐步生成一氯到四氯甲烷；苯环的取代通常以一取代为主（条件剧烈时可多取代）。取代反应中有机物的不饱和度不变。',
      },
      {
        index: 5,
        title: '验条件与守恒',
        goal: '标注反应条件，配平原子守恒',
        layer: layer('Ability'),
        analyze:
          '标注反应条件（光照、Fe、催化剂、加热等）；配平方程式使原子守恒。验证取代反应的特征——「有进有出」，即一个基团被替换下来，不饱和度保持不变。',
      },
    ],
    checks: ['取代位点与试剂是否匹配', '是否注意一取代/多取代', '条件是否标注正确', '不饱和度是否不变、原子是否守恒'],
  },

  // 加成反应
  'reaction-organic-addition': {
    kind: 'reaction-organic-addition',
    name: '加成反应模型',
    steps: [
      ...ORGANIC_SHARED_STEPS,
      {
        index: 3,
        title: '认不饱和键',
        goal: '识别 C=C / C≡C / C=O 等可加成的位点',
        layer: layer('Application'),
        analyze:
          '找出分子中的不饱和键：碳碳双键 C=C、碳碳三键 C≡C、碳氧双键 C=O（醛、酮）、苯环等。加成反应发生在不饱和键上，反应后不饱和度降低。',
      },
      {
        index: 4,
        title: '定试剂与方向',
        goal: '确定加成试剂与区域选择性',
        layer: layer('Application'),
        analyze:
          '确定加成试剂（H₂、卤素、HX、H₂O、HCN 等）和区域选择性：不对称烯烃与 HX 加成遵循马氏规则（氢加到含氢较多的碳上）；过氧化物存在时反马氏加成；共轭二烯烃可发生 1,2-加成和 1,4-加成。',
      },
      {
        index: 5,
        title: '写产物并验',
        goal: '写出加成产物并配平',
        layer: layer('Ability'),
        analyze:
          '写出加成产物：不饱和键打开，试剂的两部分分别加到两个原子上。加成反应「只进不出」，没有小分子生成。C=C 与 H₂ 加成需 Ni 催化。配平后检查原子守恒。',
      },
    ],
    checks: ['不饱和键是否识别正确', '加成方向（马氏/反马氏）是否正确', '是否为「只进不出」无小分子生成', '产物与配平是否正确'],
  },

  // 消去反应
  'reaction-organic-elimination': {
    kind: 'reaction-organic-elimination',
    name: '消去反应模型',
    steps: [
      ...ORGANIC_SHARED_STEPS,
      {
        index: 3,
        title: '确认消去位点',
        goal: '识别可消去的醇或卤代烃，且必须有 β-H',
        layer: layer('Application'),
        analyze:
          '能发生消去反应的是醇（含 -OH）和卤代烃（含 -X），且与官能团相连碳的邻位碳（β 碳）上必须有氢（β-H），否则不能消去。没有 β-H 的醇/卤代烃只能发生其他反应。',
      },
      {
        index: 4,
        title: '定反应条件',
        goal: '醇用浓硫酸 170℃，卤代烃用 NaOH 醇溶液 △',
        layer: layer('Application'),
        analyze:
          '消去反应的条件是关键：醇的消去用浓硫酸、170℃（140℃ 时生成乙醚，是分子间脱水，不是消去）；卤代烃的消去用 NaOH 的醇溶液、加热（NaOH 水溶液则是水解，不是消去）。',
      },
      {
        index: 5,
        title: '写不饱和产物',
        goal: '脱去小分子生成 C=C，遵循扎伊采夫规则',
        layer: layer('Ability'),
        analyze:
          '消去反应脱去一个小分子（醇脱 H₂O、卤代烃脱 HX），生成含碳碳双键的不饱和化合物。多种消去可能时遵循扎伊采夫规则：优先生成取代基较多（较稳定）的烯烃。配平并标注条件。',
      },
    ],
    checks: ['是否有 β-H（无则不能消去）', '条件是否正确（170℃/醇溶液）', '是否区分消去与分子间脱水/水解', '产物是否遵循扎伊采夫规则'],
  },

  // 氧化反应（有机）
  'reaction-organic-oxidation': {
    kind: 'reaction-organic-oxidation',
    name: '有机氧化模型',
    steps: [
      ...ORGANIC_SHARED_STEPS,
      {
        index: 3,
        title: '认可氧化基团',
        goal: '识别可被氧化的官能团',
        layer: layer('Application'),
        analyze:
          '找出可被氧化的官能团：伯醇 -CH₂OH（可氧化到醛再到酸）、仲醇 -CHOH（氧化到酮）、醛 -CHO（氧化到羧酸）、碳碳双键/三键、苯的同系物侧链。叔醇一般不被氧化。',
      },
      {
        index: 4,
        title: '定氧化剂与条件',
        goal: '确定氧化剂及对应的氧化产物层级',
        layer: layer('Application'),
        analyze:
          '根据氧化剂判断氧化程度：O₂/Cu/△ 把醇氧化为醛或酮；银氨溶液、新制 Cu(OH)₂ 把醛氧化为羧酸（银镜/砖红色沉淀）；酸性 KMnO₄ 氧化碳碳双键、三键以及苯的同系物侧链（生成羧基，要求与苯环相连的碳上有氢）。',
      },
      {
        index: 5,
        title: '写氧化产物并验',
        goal: '写出氧化产物，注意断键位置',
        layer: layer('Ability'),
        analyze:
          '写出氧化产物：伯醇→醛→羧酸，仲醇→酮，醛→羧酸；苯的同系物侧链氧化为 -COOH。注意与苯环直接相连的碳上没有氢时侧链不被氧化。配平并检查产物的官能团是否正确。',
      },
    ],
    checks: ['可氧化官能团是否识别正确', '氧化剂与产物层级是否匹配', '苯同系物侧链碳是否有 α-H', '产物官能团是否正确'],
  },

  // 还原反应（有机）
  'reaction-organic-reduction': {
    kind: 'reaction-organic-reduction',
    name: '有机还原模型',
    steps: [
      ...ORGANIC_SHARED_STEPS,
      {
        index: 3,
        title: '认可还原基团',
        goal: '识别可加氢/脱氧的官能团',
        layer: layer('Application'),
        analyze:
          '找出可被还原的官能团：碳碳双键/三键、碳氧双键（醛、酮）、硝基（-NO₂）、苯环等。这些官能团可通过加氢或脱氧被还原。',
      },
      {
        index: 4,
        title: '定还原剂',
        goal: '确定还原剂与反应条件',
        layer: layer('Application'),
        analyze:
          '常用还原剂：H₂/Ni（催化加氢，可还原 C=C、C≡C、醛酮羰基、苯环、硝基）；硝基（-NO₂）还原为氨基（-NH₂）常用 Fe/HCl。注意高中阶段酯基（-COO-）一般难被 H₂ 还原。',
      },
      {
        index: 5,
        title: '写还原产物并验',
        goal: '写出还原产物',
        layer: layer('Ability'),
        analyze:
          '写出还原产物：醛、酮加氢生成醇；碳碳双键加氢变单键；硝基还原为氨基。配平并检查产物中官能团的变化是否与还原剂的能力匹配（不要把酯还原了）。',
      },
    ],
    checks: ['可还原官能团是否识别正确', '还原剂与还原能力是否匹配（酯不被 H₂ 还原）', '产物官能团是否正确', '配平是否成立'],
  },

  // 酯化反应
  'reaction-organic-esterification': {
    kind: 'reaction-organic-esterification',
    name: '酯化反应模型',
    steps: [
      ...ORGANIC_SHARED_STEPS,
      {
        index: 3,
        title: '认反应物',
        goal: '识别羧酸（或含氧酸）与醇',
        layer: layer('Application'),
        analyze:
          '酯化反应是羧酸（或含氧无机酸）与醇反应生成酯和水。识别反应物中的羧基（-COOH）和羟基（-OH）。酯化反应属于取代反应，本质是「酸脱羟基、醇脱氢」。',
      },
      {
        index: 4,
        title: '定断键方式',
        goal: '明确 -COO- 中哪个氧来自醇',
        layer: layer('Application'),
        analyze:
          '用同位素示踪的思路理解断键：羧酸脱去羟基（-OH），醇脱去氢原子（-H），二者结合生成水；剩下的部分连接成酯（-COO-）。因此酯中 -COO- 的那个与烷基相连的氧来自醇，另一个氧来自羧酸。',
      },
      {
        index: 5,
        title: '写酯与水并验',
        goal: '写出酯和水，标注可逆与条件',
        layer: layer('Ability'),
        analyze:
          '写出产物：酯（羧酸酯，命名为「某酸某酯」）和水。酯化反应是可逆反应，用可逆号 ⇌；条件是浓硫酸、加热（浓硫酸作催化剂和吸水剂）。配平并验证原子守恒。',
      },
    ],
    checks: ['是否识别出羧酸与醇', '断键方式「酸脱羟基醇脱氢」是否正确', '是否标注可逆号与浓硫酸△条件', '产物酯的命名与结构是否正确'],
  },

  // 水解反应
  'reaction-organic-hydrolysis': {
    kind: 'reaction-organic-hydrolysis',
    name: '水解反应模型',
    steps: [
      ...ORGANIC_SHARED_STEPS,
      {
        index: 3,
        title: '认可水解键',
        goal: '识别可水解的化学键',
        layer: layer('Application'),
        analyze:
          '找出可水解的化学键：酯基（-COO-）、卤代烃的碳卤键（-X）、二糖/多糖的糖苷键、蛋白质的肽键、油脂的酯键。水解是这些键在水的作用下断裂，生成相应的小分子。',
      },
      {
        index: 4,
        title: '定反应条件',
        goal: '确定酸性 / 碱性 / 酶催化条件',
        layer: layer('Application'),
        analyze:
          '水解条件决定反应程度和产物：酯在酸性条件下水解可逆（生成羧酸和醇），在碱性条件下水解完全（生成羧酸盐和醇，即皂化反应）；卤代烃水解用 NaOH 水溶液、加热；糖类、蛋白质的水解可用酸、碱或酶催化。',
      },
      {
        index: 5,
        title: '写水解产物并验',
        goal: '写出断裂后的产物',
        layer: layer('Ability'),
        analyze:
          '写出水解产物：酯→羧酸（或羧酸盐）+ 醇；卤代烃→醇（羟基取代卤素）；二糖→单糖（如蔗糖→葡萄糖+果糖）；蛋白质→氨基酸。碱性条件下羧酸变为盐。配平并检查产物是否与条件匹配。',
      },
    ],
    checks: ['可水解键是否识别正确', '酸性（可逆）/碱性（完全）条件是否区分', '碱性条件下酸是否变为盐', '产物是否正确'],
  },

  // 加聚反应
  'reaction-organic-polyaddition': {
    kind: 'reaction-organic-polyaddition',
    name: '加聚反应模型',
    steps: [
      ...ORGANIC_SHARED_STEPS,
      {
        index: 3,
        title: '认单体',
        goal: '识别含碳碳双键的单体',
        layer: layer('Application'),
        analyze:
          '加聚反应的单体必须含有碳碳双键（或三键），如乙烯、氯乙烯、苯乙烯、丙烯腈等。单烯烃发生加聚生成碳链高分子；共轭二烯烃也可加聚。',
      },
      {
        index: 4,
        title: '断双键成链',
        goal: '打开双键连接成高分子链',
        layer: layer('Application'),
        analyze:
          '单体的碳碳双键打开，彼此连接成高分子长链。写出重复单元（链节）：把单体的双键变成单键，两个原本成双键的碳分别向外延伸连接下一个单体。链节的组成与单体相同。',
      },
      {
        index: 5,
        title: '写聚合物并验',
        goal: '写出高聚物结构简式与 n',
        layer: layer('Ability'),
        analyze:
          '写出高聚物的结构简式，在方括号外右下角标 n（聚合度）。加聚反应没有小分子生成，链节与单体的化学组成相同。检查链节是否正确反映单体的结构（取代基位置不变）。',
      },
    ],
    checks: ['单体是否含碳碳双键', '链节是否正确（双键变单键、取代基位置不变）', '是否无小分子生成', '链节与单体组成是否相同'],
  },

  // 缩聚反应
  'reaction-organic-polycondensation': {
    kind: 'reaction-organic-polycondensation',
    name: '缩聚反应模型',
    steps: [
      ...ORGANIC_SHARED_STEPS,
      {
        index: 3,
        title: '认双官能团单体',
        goal: '识别含两个官能团的单体',
        layer: layer('Application'),
        analyze:
          '缩聚反应的单体必须含有两个（或以上）官能团：二元酸与二元醇（生成聚酯）、羟基酸（自身缩聚）、氨基酸（生成多肽/蛋白质）、二元酸与二元胺（生成聚酰胺）。',
      },
      {
        index: 4,
        title: '定脱除小分子',
        goal: '确定缩合时脱去的小分子',
        layer: layer('Application'),
        analyze:
          '缩聚反应在生成高分子的同时脱去小分子：酯化缩聚脱去 H₂O；酰胺缩聚（成肽）脱去 H₂O；酚醛缩聚也脱去 H₂O。明确每形成一个新键脱去几个小分子。',
      },
      {
        index: 5,
        title: '写链节与聚合物并验',
        goal: '写出高聚物与小分子，标 n',
        layer: layer('Ability'),
        analyze:
          '写出高聚物的链节和结构简式，标 n；同时写出脱去的小分子（如 n H₂O）。缩聚反应有小分子生成，因此链节的组成与单体不同（少了脱去的小分子）。检查端基与链节是否正确。',
      },
    ],
    checks: ['单体是否含两个官能团', '脱去的小分子是否正确', '是否有小分子生成（与加聚区分）', '链节组成是否与单体不同'],
  },

  // 🛟 通用降级
  'reaction-generic': {
    kind: 'reaction-generic',
    name: '通用反应分析模型',
    steps: [
      {
        index: 1,
        title: '写方程式',
        goal: '写出并配平反应的化学方程式',
        layer: layer('Application'),
        analyze:
          '先写出反应物和产物的化学式，配平使各元素原子守恒；标注反应条件和必要的状态符号（↑、↓）。若为离子反应，进一步写出离子方程式。',
      },
      {
        index: 2,
        title: '判类型',
        goal: '判断所属反应类型及核心特征',
        layer: layer('Application'),
        analyze:
          '根据反应物和产物的种类与变化判断反应类型：化合（多变一）、分解（一变多）、置换（单+化→单+化）、复分解（交换成分）、氧化还原（有价态变化）等。抓住该类反应的核心特征。',
      },
      {
        index: 3,
        title: '验守恒',
        goal: '检验原子守恒与电荷守恒',
        layer: layer('Ability'),
        analyze:
          '最后检验：方程式两边各元素原子个数相等（原子守恒）；离子反应两边电荷代数和相等（电荷守恒）；若涉及氧化还原，失电子总数等于得电子总数（电子守恒）。',
      },
    ],
    checks: ['方程式是否配平', '反应类型判断是否正确', '原子、电荷（及电子）是否守恒'],
  },
}

/** 获取某反应 kind 的脚手架模型；未知 kind 降级到通用模型 */
export const getReactionModel = (kind: ReactionSolveKind): ReactionSolveModel =>
  REACTION_SOLVE_MODELS[kind] ?? REACTION_SOLVE_MODELS['reaction-generic']
