import { Challenge, ChemCategory, SolveModelKind } from '../types/chem'
import { chapters } from '../data/challenges'

/** 模型步骤的认知层级 */
export type ModelLayer = 'Knowledge' | 'Application' | 'Ability'

/** 题型标准解题步骤 */
export interface SolveModelStep {
  index: number
  /** 稳定标识：叙述层（narrateReaction）按此分发，不随 title 文案变动；反应侧必填，题型侧可选 */
  key?: string
  title: string
  goal: string
  layer: ModelLayer
  /** 这一步的通用分析方法（讲题文案，不包含具体题目的答案） */
  analyze: string
  /** 所属阶段（氧化还原两阶段模型用）：stage1=反应推理主链路，stage2=电子守恒配平子专项；缺省不分组 */
  stage?: 'stage1' | 'stage2'
}

/** 题型解题模型（引擎层共享资产） */
export interface SolveModel {
  category: ChemCategory
  /** 模型名，如「电子守恒配平模型」 */
  name: string
  icon: string
  color: string
  /** 专属可视化面板类型 */
  panelKind: SolveModelKind
  /** 该题型的标准解题步骤（思考骨架） */
  steps: SolveModelStep[]
  /** 该题型的专属校验检查项 */
  checks: string[]
}

const chapterOf = (cat: ChemCategory) => chapters.find((c) => c.category === cat)

export const SOLVE_MODELS: Record<ChemCategory, SolveModel> = {
  // 🔴 氧化还原：两阶段——阶段一反应推理（主链路）+ 阶段二电子守恒配平（可展开子专项）
  RedoxReaction: {
    category: 'RedoxReaction',
    name: '氧还反应推理模型',
    icon: chapterOf('RedoxReaction')?.icon ?? '⚡',
    color: chapterOf('RedoxReaction')?.color ?? '#dc2626',
    panelKind: 'redox-electron',
    steps: [
      // —— 阶段一：反应可行性 & 产物推理（主链路，默认可见）——
      {
        index: 1,
        title: '列粒子',
        goal: '拆开电离，列出体系全部粒子，区分大量与微量',
        layer: 'Knowledge',
        stage: 'stage1',
        analyze:
          '把加入的强电解质拆成离子，写出弱电解质的电离与水解，别忘了水的微弱电离；把体系中所有存在的分子和离子全部列出来，按「大量」「微量」分类——大量粒子是反应的主角，微量粒子（如水电离的 H⁺/OH⁻）通常只在配平时才考虑。',
      },
      {
        index: 2,
        title: '标变价物种',
        goal: '标出各元素价态，选出候选氧化剂、还原剂（允许多个）',
        layer: 'Application',
        stage: 'stage1',
        analyze:
          '逐一标出粒子中各元素的化合价，找出化合价能改变的物种：含可降价元素的粒子是候选氧化剂，含可升价元素的粒子是候选还原剂；可能同时有多个氧化剂或多个还原剂，先全部列出来，不急于下结论。',
      },
      {
        index: 3,
        title: '判介质',
        goal: '判断酸 / 碱 / 中性，介质直接决定氧化剂的还原产物',
        layer: 'Application',
        stage: 'stage1',
        analyze:
          '看题目是否给出酸性（如 H₂SO₄、H⁺）、碱性（如 NaOH、OH⁻）或中性条件；介质会改变氧化剂的还原产物，例如 MnO₄⁻：酸性→Mn²⁺、中性→MnO₂、碱性→MnO₄²⁻；Cr₂O₇²⁻ 酸性→Cr³⁺；NO₃⁻ 酸性→NO（或 NO₂），中性 / 碱性一般不表现强氧化性。',
      },
      {
        index: 4,
        title: '判先后',
        goal: '多种氧化剂 / 还原剂时，判断反应的先后顺序',
        layer: 'Ability',
        stage: 'stage1',
        analyze:
          '若体系中有多个还原剂，氧化性最强的氧化剂先与还原性最强的还原剂反应；反之亦然。依据已知的氧化性 / 还原性强弱顺序，确定谁先反应、谁后反应，避免漏掉分步反应或搞错产物。本步为可选，只有一个氧化剂和一个还原剂时可跳过。',
      },
      {
        index: 5,
        title: '推产物',
        goal: '结合价态与介质推导变价产物，判断是否生成沉淀、能否共存',
        layer: 'Ability',
        stage: 'stage1',
        analyze:
          '根据氧化剂 / 还原剂的价态变化趋势和介质，写出变价后的产物；再检查产物之间、产物与体系原有粒子能否大量共存（是否生成沉淀、气体或弱电解质），如 Ba²⁺ 与 SO₄²⁻ 生成 BaSO₄ 沉淀、Fe³⁺ 与 S²⁻ 不能共存。至此得到一套确认的反应物 + 生成物，才能进入配平。',
      },
      // —— 阶段二：电子守恒配平（子专项，可展开，不强制）——
      {
        index: 6,
        title: '标价态',
        goal: '标出反应前后变价元素的化合价',
        layer: 'Knowledge',
        stage: 'stage2',
        analyze:
          '在已确认的反应物和生成物上，标出各变价元素反应前后的化合价；没变价的元素先放一边，只盯住化合价发生变化的元素。',
      },
      {
        index: 7,
        title: '算电子',
        goal: '单独看 1 个原子，计算每个原子的得失电子数',
        layer: 'Application',
        stage: 'stage2',
        analyze:
          '对每个变价元素，算 1 个原子化合价的变化值：升高几价就失几个电子（被氧化），降低几价就得几个电子（被还原）；再乘以一个分子中该变价元素的原子数，得到一个分子转移的电子数。',
      },
      {
        index: 8,
        title: '求公倍数',
        goal: '求最小公倍数，使失电子总数 = 得电子总数，确定基础系数',
        layer: 'Ability',
        stage: 'stage2',
        analyze:
          '分别汇总所有失电子数和得电子数，求它们的最小公倍数；用最小公倍数除以各自一个分子的转移电子数，得到氧化剂和还原剂的基础系数比。',
      },
      {
        index: 9,
        title: '配变价',
        goal: '用最小公倍数配平变价物质的系数',
        layer: 'Ability',
        stage: 'stage2',
        analyze:
          '把上一步得到的系数写定到氧化剂和还原剂（及其对应的变价产物）上，先把电子守恒关系落实，再处理其余物质。',
      },
      {
        index: 10,
        title: '补氢氧',
        goal: '先用电荷守恒补 H⁺ / OH⁻，再用原子守恒补 H₂O',
        layer: 'Ability',
        stage: 'stage2',
        analyze:
          '离子方程式配平时，先看两边电荷是否守恒：酸性溶液用 H⁺ 配电荷，碱性溶液用 OH⁻ 配电荷；电荷配平后，再用 H、O 原子守恒补上 H₂O，最后检查其余元素。',
      },
      {
        index: 11,
        title: '全局校验',
        goal: '原子、电荷、得失电子三重校验',
        layer: 'Ability',
        stage: 'stage2',
        analyze:
          '配平完成后逐项校验：①各元素原子个数左右相等；②离子方程式左右电荷总数相等；③化合价升高总数 = 降低总数（失电子总数 = 得电子总数）。三项都通过，方程式才配平正确。',
      },
    ],
    checks: [
      '阶段一：有无变价物种、反应是否真能发生',
      '阶段一：介质与氧化剂还原产物是否匹配（如 MnO₄⁻ 酸性→Mn²⁺）',
      '阶段一：推导产物能否共存、是否生成沉淀',
      '阶段二：化合价升高总数 = 降低总数',
      '阶段二：失电子总数 = 得电子总数，最小公倍数与系数比正确',
      '阶段二：配平后原子、电荷是否守恒',
    ],
  },

  // 💧 离子平衡：粒子与三大守恒
  IonicEquilibrium: {
    category: 'IonicEquilibrium',
    name: '粒子·守恒分析模型',
    icon: chapterOf('IonicEquilibrium')?.icon ?? '💧',
    color: chapterOf('IonicEquilibrium')?.color ?? '#2563eb',
    panelKind: 'ionic-conservation',
    steps: [
      {
        index: 1,
        title: '找粒子',
        goal: '写出溶液中所有电离、水解过程，列出全部粒子种类',
        layer: 'Knowledge',
        analyze:
          '先判断溶液中的溶质是强电解质还是弱电解质，写出全部电离方程式；再看有无离子会发生水解，写出水解方程式；别忘了水本身也会微弱电离出 H⁺ 和 OH⁻，把溶液中所有存在的粒子（分子和离子）全部列出来，避免漏列。',
      },
      {
        index: 2,
        title: '写守恒',
        goal: '分别写出电荷守恒、物料守恒、质子守恒三个关系式',
        layer: 'Application',
        analyze:
          '电荷守恒：溶液中所有阳离子所带正电荷总数等于所有阴离子所带负电荷总数，注意离子浓度要乘以它的电荷数；物料守恒：抓住某一关键元素，写出它在各种粒子中的浓度总和等于起始浓度；质子守恒可由电荷守恒减去物料守恒得到，也可直接分析水电离的 H⁺ 和 OH⁻ 去向。',
      },
      {
        index: 3,
        title: '比浓度',
        goal: '结合电离/水解程度与守恒式，判断离子浓度大小关系',
        layer: 'Ability',
        analyze:
          '先看溶质本身电离出的离子，它们浓度最大；再比较弱电解质的电离程度和离子的水解程度谁占上风，结合三大守恒关系式判断离子浓度的相对大小；出现等式时优先用守恒式去验证是否成立。',
      },
    ],
    checks: ['是否漏列溶液中的粒子（含水电离）', '电荷守恒是否配平（电荷数×浓度）', '物料守恒是否抓住关键元素', '质子守恒可用电荷−物料验证'],
  },

  // 🔄 化学平衡：条件-移动-K 推演
  ChemicalEquilibrium: {
    category: 'ChemicalEquilibrium',
    name: '平衡移动推演模型',
    icon: chapterOf('ChemicalEquilibrium')?.icon ?? '🔄',
    color: chapterOf('ChemicalEquilibrium')?.color ?? '#ea580c',
    panelKind: 'equilibrium-shift',
    steps: [
      {
        index: 1,
        title: '写平衡',
        goal: '写出可逆反应方程式，标注 ΔH 与气体计量数变化',
        layer: 'Knowledge',
        analyze:
          '先写出可逆反应的化学方程式，注明各物质状态；标出反应的热效应 ΔH（吸热为正、放热为负），并数清楚方程式左右气体物质的化学计量数之和，判断气体分子数是增大、减小还是不变。',
      },
      {
        index: 2,
        title: '判条件',
        goal: '识别改变的条件：浓度、温度、压强、催化剂等',
        layer: 'Knowledge',
        analyze:
          '逐一审题找出改变的条件：是改变了某种反应物或产物的浓度、升高或降低温度、增大或减小压强（或体积），还是加入了催化剂；把每个改变的条件单独列出来，多个条件叠加时分别判断再综合。',
      },
      {
        index: 3,
        title: '推移动',
        goal: '用勒夏特列原理判断平衡移动方向，区分速率与平衡',
        layer: 'Application',
        analyze:
          '用勒夏特列原理：平衡总是向「减弱这种改变」的方向移动——升温向吸热方向移动，加压向气体分子数减少的方向移动，增加反应物浓度向正向移动；注意区分：条件改变的瞬间是速率变化，随后才是平衡移动，不要把速率和平衡混为一谈。',
      },
      {
        index: 4,
        title: '看 K 与转化率',
        goal: '判断 K 是否变化（仅温度）及反应物/产物转化率变化',
        layer: 'Ability',
        analyze:
          '平衡常数 K 只与温度有关，温度不变则 K 不变（浓度、压强、催化剂都不改变 K）；升温时 K 增大还是减小取决于正反应吸热还是放热；转化率的变化与平衡移动方向一致：平衡向正方向移动，反应物转化率通常增大。',
      },
    ],
    checks: ['催化剂只改速率、不影响平衡', 'K 只与温度有关，浓度/压强不改 K', '移动方向是否符合「减弱改变」', '转化率变化与移动方向是否一致'],
  },

  // 🔋 电化学：装置·半反应·现象分析（专属面板 electrochem-cell）
  Electrochemistry: {
    category: 'Electrochemistry',
    name: '电极·电路分析模型',
    icon: chapterOf('Electrochemistry')?.icon ?? '🔋',
    color: chapterOf('Electrochemistry')?.color ?? '#7c3aed',
    panelKind: 'electrochem-cell',
    steps: [
      {
        index: 1,
        title: '判装置',
        goal: '判断是原电池还是电解池，确定电极名称',
        layer: 'Knowledge',
        analyze:
          '先看有没有外接电源：有外接电源的是电解池，没有的是原电池；原电池中较活泼金属做负极、发生氧化反应，另一极为正极、发生还原反应；电解池中与电源正极相连的是阳极（氧化），与负极相连的是阴极（还原）。',
      },
      {
        index: 2,
        title: '写半反应',
        goal: '写出两极的电极反应式，标注电子流向',
        layer: 'Application',
        analyze:
          '分别写出两极的电极反应式：负极/阳极写失电子的氧化反应，正极/阴极写得电子的还原反应；配平时注意电荷守恒和原子守恒，酸性溶液用 H⁺ 和 H₂O 配平，碱性溶液用 OH⁻ 和 H₂O 配平；电子从负极经外电路流向正极，溶液中靠离子定向移动导电。',
      },
      {
        index: 3,
        title: '析现象',
        goal: '分析两极现象、离子迁移与产物检验',
        layer: 'Ability',
        analyze:
          '根据电极反应判断两极现象：有气体放出（如 H₂、O₂、Cl₂）、有金属析出、溶液颜色变化、pH 变化等；判断离子迁移方向——阳离子向阴极移动、阴离子向阳极移动；产物可用特征反应检验。',
      },
    ],
    checks: ['负极/阳极发生氧化，正极/阴极发生还原', '电子不下水、离子不上岸', '电解池电极与电源正负极对应是否正确'],
  },

  // ⚗️ 无机推断（本轮占位通用面板，预留专属）
  InorganicInference: {
    category: 'InorganicInference',
    name: '突破口·转化链推断模型',
    icon: chapterOf('InorganicInference')?.icon ?? '⚗️',
    color: chapterOf('InorganicInference')?.color ?? '#16a34a',
    panelKind: 'generic',
    steps: [
      {
        index: 1,
        title: '找突破口',
        goal: '从特征颜色、特征现象、特殊条件锁定关键物质',
        layer: 'Knowledge',
        analyze:
          '先扫一遍框图和文字，找特征信息：特殊颜色（如淡黄色固体、红褐色沉淀、蓝色溶液）、特征现象（如使品红褪色的气体、焰色反应）、特殊反应条件（如高温、催化剂、电解）、特征数据；从最确定的那个物质入手锁定它。',
      },
      {
        index: 2,
        title: '顺逆推',
        goal: '沿转化关系正向、逆向推导其余物质',
        layer: 'Application',
        analyze:
          '以突破口物质为中心，沿转化箭头正向推导后面的产物，再逆向推导前面的反应物；遇到分支或不确定处先做假设，把可能的物质都列出来，结合条件逐步排除。',
      },
      {
        index: 3,
        title: '代回验证',
        goal: '把推出的物质代回框图，检验每一步转化是否成立',
        layer: 'Ability',
        analyze:
          '把最终推出的所有物质代回到整个框图中，逐一检验每一步转化是否都成立、是否符合反应规律和题目条件；只要有一步对不上，就回退到上一个分叉点换一种假设重试。',
      },
    ],
    checks: ['特征现象对应物质是否唯一', '每一步转化是否符合反应规律', '推出的物质代回框图是否自洽'],
  },

  // 🧬 有机化学（本轮占位通用面板，预留专属）
  OrganicChemistry: {
    category: 'OrganicChemistry',
    name: '官能团·反应类型模型',
    icon: chapterOf('OrganicChemistry')?.icon ?? '🧬',
    color: chapterOf('OrganicChemistry')?.color ?? '#0891b2',
    panelKind: 'generic',
    steps: [
      {
        index: 1,
        title: '认官能团',
        goal: '识别分子中的官能团，判断所属有机物类别',
        layer: 'Knowledge',
        analyze:
          '先看结构简式或分子式，识别其中的官能团（如碳碳双键、羟基、羧基、酯基、醛基、氨基等）；根据官能团判断有机物所属类别，并回忆该官能团的特征反应和性质。',
      },
      {
        index: 2,
        title: '定反应',
        goal: '根据官能团与条件确定反应类型与断键位置',
        layer: 'Application',
        analyze:
          '根据反应条件和官能团确定反应类型：如光照→烷烃取代、Ni/H₂→加成、浓硫酸/加热→酯化或消去、Cu/O₂→醇氧化等；明确断键位置——反应发生在哪个键上，生成什么新键。',
      },
      {
        index: 3,
        title: '写产物',
        goal: '推导产物结构，注意同分异构与条件限制',
        layer: 'Ability',
        analyze:
          '根据反应类型和断键位置推导产物结构，注意取代基的位置和数目；若是写同分异构体，先算不饱和度，再按官能团异构→碳链异构→位置异构的顺序有序书写，最后检查是否满足价键规则和题目限制条件。',
      },
    ],
    checks: ['官能团识别是否正确', '反应类型与条件是否匹配', '产物是否满足不饱和度与价键规则'],
  },
}

/** 获取某道题所属题型的解题模型 */
export const getSolveModel = (challenge: Challenge): SolveModel =>
  SOLVE_MODELS[challenge.category]

/** 获取该题应渲染的面板类型（以题型模型为准；题目 solveData 需与之匹配才走富可视化） */
export const getPanelKind = (challenge: Challenge): SolveModelKind =>
  SOLVE_MODELS[challenge.category].panelKind

/** 获取该题型的专属校验检查项 */
export const getModelChecks = (challenge: Challenge): string[] =>
  SOLVE_MODELS[challenge.category].checks
