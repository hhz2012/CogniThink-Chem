import type { ModelIconName } from '../types/chem'

/**
 * 推导场景：首页点击后「带预设」进入反应模拟器，让学生自己加料、调条件、观察现象来推导，
 * 而不是抽题作答。预设参数通过 URL query 传给 simulator 页，由其解析并预填。
 *
 * 设计原则：
 * - 每个场景只预填一个「起点」+ 关键条件，剩下由学生在模拟器里动手推导；
 * - 优先选用引擎已能正确处理的体系（trace / equilibrium / reason 三模式均已验证）；
 * - 刻意安排「默认不发生 → 改条件后发生」的场景，训练条件意识。
 */

export type SimMode = 'trace' | 'equilibrium' | 'reason'

export interface DeriveScene {
  id: string
  title: string
  desc: string
  hint: string
  icon: ModelIconName
  accent: string
  mode: SimMode
  /** 该模式对应的输入框预填文本（trace→反应物 / equilibrium→溶质浓度 / reason→初始体系） */
  species: string
  /** 条件开关预设：逗号分隔；前缀 ! 表示关闭（用于覆盖 aqueous/air 的默认 true） */
  cond: string
  /** reason 模式专用：推理计划，;; 分隔多步，每步 kind|name|species|numerical(1/0) */
  plan?: string
  /** 场景标题，进入模拟器后在顶部展示 */
  scene: string
  /** 题目文本：进入模拟器后展示，让学生带着问题推导 */
  problem: string
  /** 首页示例卡片展示：现象名称 */
  shortName: string
  /** 首页示例卡片展示：一句话现象描述 */
  phenomenon: string
  /** 首页示例卡片展示：推导阶段数 */
  stages: number
  /** 首页示例卡片展示：演示时长文案 */
  duration: string
  /** 首页示例卡片展示：真实实验照片缩略图 URL */
  thumb: string
}

/** 生成真实质感实验照片缩略图（平台文生图接口） */
const THUMB = (prompt: string) =>
  `https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=${encodeURIComponent(
    prompt
  )}&image_size=landscape_4_3`

export const DERIVE_SCENES: DeriveScene[] = [
  {
    id: 'redox-fe-cuso4',
    title: '氧化还原推导',
    desc: '铁与硫酸铜，观察置换与电子转移',
    hint: '观察溶液颜色与固体变化，判断谁被氧化、谁被还原',
    icon: 'zap',
    accent: '#dc2626',
    mode: 'trace',
    species: 'Fe:1, CuSO4:1',
    cond: 'aqueous,air',
    scene: '氧化还原：铁置换铜',
    problem: '将铁钉放入硫酸铜溶液中，充分反应后观察现象。判断：反应中哪种物质被氧化、哪种被还原？写出反应的离子方程式。',
    shortName: '铁与硫酸铜',
    phenomenon: '铁表面析出红色铜',
    stages: 3,
    duration: '12s',
    thumb: THUMB(
      'realistic close-up photo of an iron nail immersed in blue copper sulfate solution in a glass beaker, reddish copper metal depositing on the nail surface, chemistry laboratory, soft daylight, high detail'
    ),
  },
  {
    id: 'ionic-precip-agcl',
    title: '离子与沉淀',
    desc: '硝酸银与氯化钠，看沉淀如何生成',
    hint: '哪些离子真正参与了反应？写出离子方程式',
    icon: 'droplet',
    accent: '#2563eb',
    mode: 'trace',
    species: 'AgNO3:1, NaCl:1',
    cond: 'aqueous,air',
    scene: '离子反应：氯化银沉淀',
    problem: '向氯化钠溶液中滴加硝酸银溶液，立即产生白色沉淀。写出该反应的离子方程式，并指出实际参与反应的离子。',
    shortName: '氯离子检验',
    phenomenon: '滴加硝酸银，产生白色沉淀',
    stages: 2,
    duration: '10s',
    thumb: THUMB(
      'realistic close-up photo of white silver chloride precipitate forming in a clear test tube after adding silver nitrate solution, cloudy white solid suspended in transparent liquid, chemistry laboratory'
    ),
  },
  {
    id: 'equilibrium-ac',
    title: '水溶液平衡',
    desc: '醋酸钠溶液，推算 pH 与粒子浓度',
    hint: '为什么醋酸钠溶液呈碱性？比较各粒子浓度大小',
    icon: 'refresh',
    accent: '#ea580c',
    mode: 'equilibrium',
    species: 'CH3COONa:0.1',
    cond: '',
    scene: '平衡计算：醋酸钠水解',
    problem: '常温下，0.1 mol/L 醋酸钠（CH₃COONa）溶液的 pH 约为多少？比较溶液中 CH₃COO⁻、CH₃COOH、Na⁺、H⁺、OH⁻ 的浓度大小，并说明溶液呈碱性的原因。',
    shortName: '醋酸钠水解',
    phenomenon: '弱酸根水解，溶液呈碱性',
    stages: 3,
    duration: '12s',
    thumb: THUMB(
      'realistic close-up photo of a colorless salt solution in a beaker turning bright pink after adding phenolphthalein indicator, demonstrating alkaline hydrolysis, chemistry laboratory, soft lighting'
    ),
  },
  {
    id: 'continuous-alcl3-naoh',
    title: '量变引起质变',
    desc: '向氯化铝中逐滴加 NaOH，看产物如何分步变化',
    hint: '先沉淀后溶解，每一步的反应与现象是什么',
    icon: 'test-tube',
    accent: '#16a34a',
    mode: 'reason',
    species: 'AlCl3:1',
    cond: 'aqueous,air',
    plan: 'AddSpecies|加过量 NaOH|NaOH:0.5|1',
    scene: '连续反应：铝三角的量变',
    problem: '向含 1 mol AlCl₃ 的溶液中逐滴加入 NaOH 溶液直至过量。描述实验现象的变化，并写出每一步反应的化学方程式。',
    shortName: '铝盐与碱',
    phenomenon: '先生成沉淀，后沉淀溶解',
    stages: 3,
    duration: '14s',
    thumb: THUMB(
      'realistic close-up photo of white gelatinous aluminum hydroxide precipitate suspended in a test tube, white flocculent solid in clear solution, chemistry laboratory'
    ),
  },
  {
    id: 'condition-gating-cu-h2so4',
    title: '条件决定反应',
    desc: '铜与浓硫酸，默认不加热——观察「不发生」的原因',
    hint: '先看为什么不反应，再勾选「加热」观察反应发生',
    icon: 'wrench',
    accent: '#7c3aed',
    mode: 'trace',
    species: 'Cu:1, H2SO4:1',
    cond: 'concentrated,!aqueous,!air',
    scene: '条件门控：铜与浓硫酸',
    problem: '常温下将铜片放入浓硫酸中，观察不到明显现象。试分析其原因；若对体系加热，预测会发生什么反应？写出相应的化学方程式。',
    shortName: '铜与浓硫酸',
    phenomenon: '加热才反应，放出刺激气味',
    stages: 3,
    duration: '12s',
    thumb: THUMB(
      'realistic close-up photo of copper metal reacting with concentrated sulfuric acid in a heated test tube, gas bubbles rising from the metal, chemistry laboratory'
    ),
  },
  {
    id: 'solvent-bromoethane',
    title: '溶剂决定产物',
    desc: '溴乙烷与 NaOH，水溶液 vs 醇溶液，产物不同',
    hint: '水溶液生成乙醇；勾选「醇溶液」看乙烯消去',
    icon: 'hexagon',
    accent: '#0891b2',
    mode: 'trace',
    species: 'C2H5Br:1, NaOH:1',
    cond: 'aqueous,air',
    scene: '溶剂效应：溴乙烷的水解与消去',
    problem: '溴乙烷（C₂H₅Br）与 NaOH 在水溶液中和在醇溶液中反应，产物不同。分别写出两种条件下的反应方程式，并指出所属反应类型。',
    shortName: '溴乙烷反应',
    phenomenon: '溶剂不同，产物不同',
    stages: 2,
    duration: '10s',
    thumb: THUMB(
      'realistic close-up photo of two separate immiscible liquid layers in a test tube, colorless organic layer on top of aqueous solution, chemistry laboratory'
    ),
  },
]

/** 把场景编码成 simulator 页可解析的 query 字符串 */
export function sceneToQuery(s: DeriveScene): string {
  const parts: string[] = [`mode=${encodeURIComponent(s.mode)}`]
  parts.push(`scene=${encodeURIComponent(s.scene)}`)
  parts.push(`problem=${encodeURIComponent(s.problem)}`)
  parts.push(`species=${encodeURIComponent(s.species)}`)
  if (s.cond) parts.push(`cond=${encodeURIComponent(s.cond)}`)
  if (s.plan) parts.push(`plan=${encodeURIComponent(s.plan)}`)
  return parts.join('&')
}
