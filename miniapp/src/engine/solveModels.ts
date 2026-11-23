import { Challenge, ChemCategory, SolveModelKind } from '../types/chem'
import { chapters } from '../data/challenges'

/** 模型步骤的认知层级 */
export type ModelLayer = 'Knowledge' | 'Application' | 'Ability'

/** 题型标准解题步骤 */
export interface SolveModelStep {
  index: number
  title: string
  goal: string
  layer: ModelLayer
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
  // 🔴 氧化还原：电子守恒配平
  RedoxReaction: {
    category: 'RedoxReaction',
    name: '电子守恒配平模型',
    icon: chapterOf('RedoxReaction')?.icon ?? '⚡',
    color: chapterOf('RedoxReaction')?.color ?? '#dc2626',
    panelKind: 'redox-electron',
    steps: [
      { index: 1, title: '标价态', goal: '标出反应前后各元素化合价，锁定化合价发生变化的元素', layer: 'Knowledge' },
      { index: 2, title: '算电子', goal: '计算每个变价元素的转移电子数 = |价态差| × 变价原子数', layer: 'Application' },
      { index: 3, title: '配守恒', goal: '求得失电子数的最小公倍数，确定氧化剂与还原剂的系数比', layer: 'Ability' },
      { index: 4, title: '观察配平', goal: '用观察法配平其余物质，最后检验原子与电荷守恒', layer: 'Ability' },
    ],
    checks: ['化合价升高总数 = 降低总数', '失电子总数 = 得电子总数', '最小公倍数与系数比是否正确', '配平后原子、电荷是否守恒'],
  },

  // 💧 离子平衡：粒子与三大守恒
  IonicEquilibrium: {
    category: 'IonicEquilibrium',
    name: '粒子·守恒分析模型',
    icon: chapterOf('IonicEquilibrium')?.icon ?? '💧',
    color: chapterOf('IonicEquilibrium')?.color ?? '#2563eb',
    panelKind: 'ionic-conservation',
    steps: [
      { index: 1, title: '找粒子', goal: '写出溶液中所有电离、水解过程，列出全部粒子种类', layer: 'Knowledge' },
      { index: 2, title: '写守恒', goal: '分别写出电荷守恒、物料守恒、质子守恒三个关系式', layer: 'Application' },
      { index: 3, title: '比浓度', goal: '结合电离/水解程度与守恒式，判断离子浓度大小关系', layer: 'Ability' },
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
      { index: 1, title: '写平衡', goal: '写出可逆反应方程式，标注 ΔH 与气体计量数变化', layer: 'Knowledge' },
      { index: 2, title: '判条件', goal: '识别改变的条件：浓度、温度、压强、催化剂等', layer: 'Knowledge' },
      { index: 3, title: '推移动', goal: '用勒夏特列原理判断平衡移动方向，区分速率与平衡', layer: 'Application' },
      { index: 4, title: '看 K 与转化率', goal: '判断 K 是否变化（仅温度）及反应物/产物转化率变化', layer: 'Ability' },
    ],
    checks: ['催化剂只改速率、不影响平衡', 'K 只与温度有关，浓度/压强不改 K', '移动方向是否符合「减弱改变」', '转化率变化与移动方向是否一致'],
  },

  // 🔋 电化学（本轮占位通用面板，预留专属）
  Electrochemistry: {
    category: 'Electrochemistry',
    name: '电极·电路分析模型',
    icon: chapterOf('Electrochemistry')?.icon ?? '🔋',
    color: chapterOf('Electrochemistry')?.color ?? '#7c3aed',
    panelKind: 'generic',
    steps: [
      { index: 1, title: '判装置', goal: '判断是原电池还是电解池，确定电极名称', layer: 'Knowledge' },
      { index: 2, title: '写半反应', goal: '写出两极的电极反应式，标注电子流向', layer: 'Application' },
      { index: 3, title: '析现象', goal: '分析两极现象、离子迁移与产物检验', layer: 'Ability' },
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
      { index: 1, title: '找突破口', goal: '从特征颜色、特征现象、特殊条件锁定关键物质', layer: 'Knowledge' },
      { index: 2, title: '顺逆推', goal: '沿转化关系正向、逆向推导其余物质', layer: 'Application' },
      { index: 3, title: '代回验证', goal: '把推出的物质代回框图，检验每一步转化是否成立', layer: 'Ability' },
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
      { index: 1, title: '认官能团', goal: '识别分子中的官能团，判断所属有机物类别', layer: 'Knowledge' },
      { index: 2, title: '定反应', goal: '根据官能团与条件确定反应类型与断键位置', layer: 'Application' },
      { index: 3, title: '写产物', goal: '推导产物结构，注意同分异构与条件限制', layer: 'Ability' },
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
