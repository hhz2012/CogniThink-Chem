/**
 * 首页「示例」区数据：数组驱动渲染 3×2 网格。
 * 切换 2 列只需在 home/index.tsx 顶部改 COLS 常量即可。
 *
 * 每个示例附带一套模拟器预设（preset）：点击卡片后跳转到
 * /pages/simulator/index，并通过 query 把 mode / species / cond /
 * plan / scene / problem 传过去，模拟器会自动展示题目卡并预填输入框。
 */

export type ArtKind =
  | 'nail-plate'
  | 'dropper'
  | 'two-tubes'
  | 'cell'
  | 'electrode'
  | 'tube-precip'

/** 模拟器支持的三种推导模式 */
export type SimMode = 'trace' | 'equilibrium' | 'reason'

export interface SimPreset {
  mode: SimMode
  /** 进入模拟器后预填的输入框文本（trace→反应物 / equilibrium→溶质 / reason→初始体系） */
  species: string
  /** 条件开关预设：逗号分隔；前缀 ! 表示关闭 */
  cond?: string
  /** reason 模式专用：推理计划，;; 分隔多步，每步 kind|name|species|numerical(1/0) */
  plan?: string
  /** 场景标题，进入模拟器后在顶部横幅展示 */
  scene: string
  /** 题目文本，进入模拟器后展示，让学生带着问题推导 */
  problem: string
}

export interface HomeExample {
  id: string
  title: string
  desc: string
  /** 反应方程式（卡片与详情展示） */
  eq: string
  stages: number
  dur: number
  tag: string
  art: { kind: ArtKind; liquid: string }
  /** 点击卡片带入模拟器的预设 */
  preset: SimPreset
}

export const EXAMPLES: HomeExample[] = [
  {
    id: 'fe-cu',
    title: '铁铜混合体系',
    desc: '谁先反应，路径推导',
    eq: 'Fe + Cu²⁺ → Fe²⁺ + Cu；Fe + 2H⁺ → Fe²⁺ + H₂↑',
    stages: 4,
    dur: 16,
    tag: '过程推导',
    art: { kind: 'nail-plate', liquid: '#3B82F6' },
    preset: {
      mode: 'trace',
      species: 'Fe:1, CuSO4:1, H2SO4:1',
      cond: 'aqueous,air',
      scene: '混合体系：铁与硫酸铜 / 稀硫酸',
      problem:
        '将铁粉同时加入硫酸铜和稀硫酸的混合溶液中。判断：铁先与哪种物质反应？写出可能发生反应的离子方程式，并结合金属活动性顺序说明判断依据。',
    },
  },
  {
    id: 'ion',
    title: '离子共存推断',
    desc: '逐滴加入，锁定离子',
    eq: 'Ba²⁺ + SO₄²⁻ → BaSO₄↓；Ag⁺ + Cl⁻ → AgCl↓',
    stages: 3,
    dur: 14,
    tag: '推断',
    art: { kind: 'dropper', liquid: '#E2E8F0' },
    preset: {
      mode: 'trace',
      species: 'BaCl2:1, AgNO3:1, Na2SO4:1',
      cond: 'aqueous,air',
      scene: '离子推断：沉淀锁定离子',
      problem:
        '某溶液中可能含有 SO₄²⁻、Cl⁻ 等离子。向其中依次滴加 BaCl₂、AgNO₃ 溶液，均产生白色沉淀。写出相关离子方程式，并判断原溶液中一定存在哪些离子。',
    },
  },
  {
    id: 'order',
    title: '过量与少量',
    desc: '滴加顺序，产物不同',
    eq: 'Na₂CO₃ + HCl（正序 / 反序产物不同）',
    stages: 4,
    dur: 14,
    tag: '过程推导',
    art: { kind: 'two-tubes', liquid: '#DBEAFE' },
    preset: {
      mode: 'reason',
      species: 'Na2CO3:1',
      cond: 'aqueous,air',
      plan: 'AddSpecies|逐滴加稀盐酸|HCl:0.5|1',
      scene: '量变与顺序：碳酸钠与盐酸',
      problem:
        '向碳酸钠溶液中逐滴加入稀盐酸，与向稀盐酸中逐滴加入碳酸钠溶液，现象与产物不同。写出正滴与反滴各步反应的离子方程式，并解释原因。',
    },
  },
  {
    id: 'galvanic',
    title: '原电池推导',
    desc: '正负极与电极反应',
    eq: '负极 Zn−2e⁻ → Zn²⁺；正极 2H⁺+2e⁻ → H₂↑',
    stages: 3,
    dur: 12,
    tag: '电化学',
    art: { kind: 'cell', liquid: '#BFDBFE' },
    preset: {
      mode: 'trace',
      species: 'Zn:1, H2SO4:1, Cu:1',
      cond: 'aqueous,air',
      scene: '原电池：锌铜稀硫酸',
      problem:
        '将锌片和铜片用导线连接后插入稀硫酸中，构成原电池。判断正负极，写出两极的电极反应式和总反应方程式，并解释电子与离子的移动方向。',
    },
  },
  {
    id: 'electrolysis',
    title: '电解池推导',
    desc: '阴阳极产物判断',
    eq: '阴极 Cu²⁺+2e⁻ → Cu；阳极 2Cl⁻−2e⁻ → Cl₂↑',
    stages: 3,
    dur: 12,
    tag: '电化学',
    art: { kind: 'electrode', liquid: '#BAE6FD' },
    preset: {
      mode: 'trace',
      species: 'CuCl2:1',
      cond: 'aqueous,!air',
      scene: '电解池：电解氯化铜溶液',
      problem:
        '用惰性电极电解 CuCl₂ 溶液。判断阴阳极产物，写出两极电极反应式，并说明电解质浓度和 pH 的变化趋势。',
    },
  },
  {
    id: 'precip',
    title: '沉淀转化',
    desc: 'AgCl 转 AgI 判方向',
    eq: 'AgCl + I⁻ → AgI↓ + Cl⁻（Ksp 决定方向）',
    stages: 3,
    dur: 10,
    tag: '平衡',
    art: { kind: 'tube-precip', liquid: '#F8FAFC' },
    preset: {
      mode: 'trace',
      species: 'AgNO3:1, NaCl:1, KI:1',
      cond: 'aqueous,air',
      scene: '沉淀转化：AgCl → AgI',
      problem:
        '向 AgNO₃ 溶液中滴加 NaCl 产生白色沉淀，再滴加 KI 溶液，白色沉淀逐渐变为黄色。写出反应方程式，并从溶度积 Ksp 角度解释沉淀转化的方向。',
    },
  },
]

/** 把示例预设编码成 simulator 页可解析的 query 字符串 */
export function exampleToQuery(p: SimPreset): string {
  const parts: string[] = [`mode=${encodeURIComponent(p.mode)}`]
  parts.push(`scene=${encodeURIComponent(p.scene)}`)
  parts.push(`problem=${encodeURIComponent(p.problem)}`)
  parts.push(`species=${encodeURIComponent(p.species)}`)
  if (p.cond) parts.push(`cond=${encodeURIComponent(p.cond)}`)
  if (p.plan) parts.push(`plan=${encodeURIComponent(p.plan)}`)
  return parts.join('&')
}
