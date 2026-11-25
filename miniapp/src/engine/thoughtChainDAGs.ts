// 6 题型标准推导 DAG（R4 Task 3，核心交付物）
// 设计原则：title / goal / analyze / layer / stage 复用现有 engine/solveModels.ts 的 SOLVE_MODELS（单一真值源，不重写、不编造），
//   仅新增结构字段 id / dependsOn / skipWhen / branchKey；设计 B 中比现有步骤多出的节点（离子守恒校验 / 电化学计算 / 有机价键校验）在此补全。
// 文件无 import 副作用；结构合法性由 engine/thoughtChainValidation.ts 校验（见 verify 脚本 / 单测）。

import type { ChemCategory } from '../types/chem'
import type { AtomicTaskInteraction, ScaffoldChain } from '../types/scaffold'
import type { MetaPhase, ThoughtChainDAG, ThoughtChainNode } from '../types/thoughtChain'
import { SOLVE_MODELS, type SolveModelStep } from './solveModels'

/** 单个步骤的结构字段 overlay（id 与依赖边等；文案来自 SolveModelStep） */
interface NodeOverlay {
  id: string
  dependsOn: string[]
  skipWhen?: string
  branchKey?: string | string[]
  interaction?: AtomicTaskInteraction
  pitfall?: string
}

/** 由现有 SolveModelStep + overlay 构造一个 ThoughtChainNode */
const fromStep = (s: SolveModelStep, ov: NodeOverlay): ThoughtChainNode => ({
  id: ov.id,
  index: s.index,
  title: s.title,
  goal: s.goal,
  analyze: s.analyze,
  layer: s.layer,
  stage: s.stage,
  dependsOn: ov.dependsOn,
  skipWhen: ov.skipWhen,
  branchKey: ov.branchKey,
  interaction: ov.interaction,
  pitfall: ov.pitfall,
})

/**
 * 题型节点 → 元骨架阶段（S1–S6）打标表（单一真值源）。
 * 语义见 types/thoughtChain.ts MetaPhase 注释。覆盖全部题型节点（含 S1/S5）。
 */
const NODE_META_PHASE: Record<string, MetaPhase> = {
  // S1 读题定向（每题一个，新根）
  'redox.read-problem': 'S1',
  'ionic.read-problem': 'S1',
  'eq.read-problem': 'S1',
  'ec.read-problem': 'S1',
  'inf.read-problem': 'S1',
  'org.read-problem': 'S1',
  // 氧化还原：S2 条件识别 / S3 反应建模 / S4 推导结论 / S6 校验
  'redox.list-particles': 'S2',
  'redox.judge-medium': 'S2',
  'redox.mark-valence-species': 'S3',
  'redox.judge-order': 'S3',
  'redox.infer-products': 'S3',
  'redox.mark-valence': 'S4',
  'redox.count-electrons': 'S4',
  'redox.lcm': 'S4',
  'redox.balance': 'S4',
  'redox.patch-ho': 'S4',
  'redox.verify': 'S6',
  // 离子平衡：S2 找粒子 / S3 建守恒 / S4 比浓度 / S6 校验
  'ionic.list-species': 'S2',
  'ionic.write-conservation': 'S3',
  'ionic.compare-concentration': 'S4',
  'ionic.verify': 'S6',
  // 化学平衡：S2 判条件 / S3 写平衡 / S4 推移动·看 K
  'eq.identify-condition': 'S2',
  'eq.write-reaction': 'S3',
  'eq.infer-shift': 'S4',
  'eq.analyze-k': 'S4',
  // 电化学：S2 判装置 / S3 写半反应 / S4 析现象·计算
  'ec.identify-device': 'S2',
  'ec.write-half-reactions': 'S3',
  'ec.analyze-phenomena': 'S4',
  'ec.compute': 'S4',
  // 无机推断：S2 找突破口 / S3 顺逆推（建模转化链，逆向）/ S6 代回验证
  'inf.breakthrough': 'S2',
  'inf.forward-backward': 'S3',
  'inf.verify': 'S6',
  // 有机：S2 认官能团 / S3 定反应 / S4 写产物 / S6 价键校验
  'org.identify-fg': 'S2',
  'org.determine-reaction': 'S3',
  'org.write-product': 'S4',
  'org.verify': 'S6',
  // S5 问题关联 / 定答案（每题一个，插在推导结论之后、校验之前或末端）
  'redox.match-answer': 'S5',
  'ionic.match-answer': 'S5',
  'eq.match-answer': 'S5',
  'ec.match-answer': 'S5',
  'inf.match-answer': 'S5',
  'org.match-answer': 'S5',
}

/** 给节点列表打上 metaPhase（命中映射表则标注，否则保留 undefined；不就地修改） */
const withMetaPhase = (nodes: ThoughtChainNode[]): ThoughtChainNode[] =>
  nodes.map((n) => {
    const phase = NODE_META_PHASE[n.id]
    return phase ? { ...n, metaPhase: phase } : n
  })

/** category → 节点 id 前缀（与现有节点 id 约定一致） */
const CATEGORY_PREFIX: Record<ChemCategory, string> = {
  RedoxReaction: 'redox',
  IonicEquilibrium: 'ionic',
  ChemicalEquilibrium: 'eq',
  Electrochemistry: 'ec',
  InorganicInference: 'inf',
  OrganicChemistry: 'org',
}

/** S1 读题定向节点（题型通用方法论文案，不含题目答案；实例结论取自 Challenge.problemConditions） */
const s1Node = (prefix: string): ThoughtChainNode => ({
  id: `${prefix}.read-problem`,
  index: 0,
  title: '读题定向',
  goal: '明确题面条件与所求，锁定解题目标',
  analyze:
    '通读题干，分清三件事：题面给出了哪些物质和条件（浓度、温度、装置、现象等）、体系中可能涉及哪些反应或平衡、问题最终要求什么（产物、浓度、装置判断还是结构推断）。把条件逐条列出、把所求记为目标，后续推导都围绕目标展开，避免算到无关量上。',
  layer: 'Knowledge',
  stage: '元骨架',
  dependsOn: [],
})

/** S5 问题关联节点（把问题所求映射到已推结论；实例结论取自 Challenge.questionTarget + 映射表） */
const s5Node = (prefix: string, depId: string): ThoughtChainNode => ({
  id: `${prefix}.match-answer`,
  index: 0,
  title: '定答案',
  goal: '把问题所求映射到已推导的结论，确定最终答案',
  analyze:
    '回到题目所求的目标：在前面推导出的中间结论里找到对应的量——问产物就取推产物结论，问浓度就取守恒/浓度关系，问装置就取装置判断，问系数比就取配平结果。若目标量未直接出现，看它能否由已推结论进一步得出；若推不出来，说明前面缺步或方向错了，需回退。',
  layer: 'Ability',
  stage: '元骨架',
  dependsOn: [depId],
})

/**
 * 稳定拓扑排序并重排 index 为 1..N（保证 index 不破坏依赖序，且连续无跳号）。
 * 有环时防御降级：按原顺序赋 index（交由 validateDAG 检出环）。
 */
const topoIndex = (nodes: ThoughtChainNode[]): ThoughtChainNode[] => {
  const byId = new Map(nodes.map((n) => [n.id, n]))
  const indeg = new Map<string, number>()
  for (const n of nodes) indeg.set(n.id, n.dependsOn.filter((d) => byId.has(d)).length)
  const result: ThoughtChainNode[] = []
  const remaining = [...nodes]
  while (remaining.length > 0) {
    const pickIdx = remaining.findIndex((n) => (indeg.get(n.id) ?? 0) === 0)
    if (pickIdx === -1) {
      // 有环：降级为原序（不重排，交由 validateDAG 检出）
      return nodes.map((n, i) => ({ ...n, index: i + 1 }))
    }
    const picked = remaining.splice(pickIdx, 1)[0]
    result.push(picked)
    for (const m of remaining) {
      if (m.dependsOn.includes(picked.id)) indeg.set(m.id, (indeg.get(m.id) ?? 0) - 1)
    }
  }
  return result.map((n, i) => ({ ...n, index: i + 1 }))
}

/**
 * 由题型现有 steps + overlay 映射 + 额外节点 构造一张 category DAG。
 * overlay 必须覆盖该题型的全部 step.index（id 必填）；extra 为设计 B 中新增的节点（带完整文案）。
 * 自动前置 S1 读题定向节点（作为新根，原根节点改依赖 S1），并在 s5DepId 之后追加 S5 定答案节点。
 */
const buildCategoryDAG = (
  category: ChemCategory,
  name: string,
  overlays: Record<number, NodeOverlay>,
  s5DepId: string,
  extra: ThoughtChainNode[] = [],
): ThoughtChainDAG => {
  const prefix = CATEGORY_PREFIX[category]
  const model = SOLVE_MODELS[category]
  const nodes = model.steps.map((s) => {
    const ov = overlays[s.index]
    if (!ov) {
      // 防御：若某步未配 overlay，给一个兜底线性 id（仅前序依赖），保证不崩、不丢节点
      return fromStep(s, { id: `${prefix}.step-${s.index}`, dependsOn: s.index > 1 ? [`${prefix}.step-${s.index - 1}`] : [] })
    }
    return fromStep(s, ov)
  })
  // S1：作为新根前置；原根节点（dependsOn 为空）改依赖 S1（不就地修改，map 新建）
  const s1 = s1Node(prefix)
  const rooted = nodes.map((n) => (n.dependsOn.length === 0 ? { ...n, dependsOn: [s1.id] } : n))
  // S5：插在推导结论节点 s5DepId 之后；S6 校验节点的 dependsOn 由调用方在 overlay/extra 中指定接 S5
  const s5 = s5Node(prefix, s5DepId)
  const all = [s1, ...rooted, ...extra, s5]
  return { scope: { kind: 'category', category }, name, nodes: topoIndex(withMetaPhase(all)) }
}

// ============ 6 题型 DAG ============

// DAG-1 氧化还原（两阶段，沿用 R3 骨架 + 补依赖边与分支）
const redoxDAG = buildCategoryDAG('RedoxReaction', '氧还反应推理 DAG', {
  1: { id: 'redox.list-particles', dependsOn: [] },
  2: { id: 'redox.mark-valence-species', dependsOn: ['redox.list-particles'] },
  3: { id: 'redox.judge-medium', dependsOn: ['redox.list-particles'], branchKey: 'medium' },
  4: { id: 'redox.judge-order', dependsOn: ['redox.mark-valence-species', 'redox.judge-medium'], skipWhen: 'single-pair' },
  5: { id: 'redox.infer-products', dependsOn: ['redox.mark-valence-species', 'redox.judge-medium', 'redox.judge-order'], branchKey: 'medium' },
  6: { id: 'redox.mark-valence', dependsOn: ['redox.infer-products'] },
  7: { id: 'redox.count-electrons', dependsOn: ['redox.mark-valence'] },
  8: { id: 'redox.lcm', dependsOn: ['redox.count-electrons'] },
  9: { id: 'redox.balance', dependsOn: ['redox.lcm'] },
  10: { id: 'redox.patch-ho', dependsOn: ['redox.balance'], skipWhen: 'no-ionic-equation', branchKey: 'medium' },
  11: { id: 'redox.verify', dependsOn: ['redox.match-answer'] },
}, 'redox.patch-ho')

// DAG-2 离子平衡（现有 3 步 + 新增「守恒校验」）
const ionicDAG = buildCategoryDAG(
  'IonicEquilibrium',
  '粒子·守恒分析 DAG',
  {
    1: { id: 'ionic.list-species', dependsOn: [] },
    2: { id: 'ionic.write-conservation', dependsOn: ['ionic.list-species'], branchKey: 'solute-type' },
    3: { id: 'ionic.compare-concentration', dependsOn: ['ionic.write-conservation'], skipWhen: 'only-equation-check', branchKey: 'solute-type' },
  },
  'ionic.compare-concentration',
  [
    {
      id: 'ionic.verify',
      index: 4,
      title: '守恒校验',
      goal: '把守恒式代回粒子清单核对，确保三式自洽',
      analyze:
        '把写出的守恒式代回粒子清单逐项核对：电荷守恒两边正负电荷代数和是否相等（注意离子浓度要乘电荷数）、物料守恒关键元素在各粒子中的浓度总和是否等于起始浓度、质子守恒能否由电荷守恒减去物料守恒推出；任一式不成立说明前面列粒子或写守恒有误，需回退检查。',
      layer: 'Ability',
      dependsOn: ['ionic.match-answer'],
      skipWhen: 'only-concentration-rank',
    },
  ],
)

// DAG-3 化学平衡
const equilibriumDAG = buildCategoryDAG('ChemicalEquilibrium', '平衡移动推演 DAG', {
  1: { id: 'eq.write-reaction', dependsOn: [] },
  2: { id: 'eq.identify-condition', dependsOn: ['eq.write-reaction'] },
  3: { id: 'eq.infer-shift', dependsOn: ['eq.write-reaction', 'eq.identify-condition'], branchKey: 'condition-type' },
  4: { id: 'eq.analyze-k', dependsOn: ['eq.infer-shift'], branchKey: 'temperature-changed' },
}, 'eq.analyze-k')

// DAG-4 电化学（现有 3 步 + 新增「计算」）
const electrochemDAG = buildCategoryDAG(
  'Electrochemistry',
  '电极·电路分析 DAG',
  {
    1: { id: 'ec.identify-device', dependsOn: [], branchKey: 'device-type' },
    2: { id: 'ec.write-half-reactions', dependsOn: ['ec.identify-device'], branchKey: ['device-type', 'medium'] },
    3: { id: 'ec.analyze-phenomena', dependsOn: ['ec.write-half-reactions'] },
  },
  'ec.compute',
  [
    {
      id: 'ec.compute',
      index: 4,
      title: '计算',
      goal: '结合电极反应的电子转移关系求解数值（电量 / 产物量 / pH 等）',
      analyze:
        '若题目涉及计算，先把已知量换算成物质的量；抓住电子转移关系：串联电路中各电极转移电子数相等，n(e⁻) 与产物量由电极反应式的计量比决定；电量 Q = It = n(e⁻) × F（F ≈ 96500 C/mol）。求出结果后代回单位与数量级校验，确认与电极反应和现象一致。',
      layer: 'Ability',
      dependsOn: ['ec.write-half-reactions'],
      skipWhen: 'no-calc',
    },
  ],
)

// DAG-5 无机推断（从 generic 占位提升为完整 DAG）
const inorganicDAG = buildCategoryDAG('InorganicInference', '突破口·转化链推断 DAG', {
  1: { id: 'inf.breakthrough', dependsOn: [], branchKey: 'subtype' },
  2: { id: 'inf.forward-backward', dependsOn: ['inf.breakthrough'], branchKey: 'subtype' },
  3: { id: 'inf.verify', dependsOn: ['inf.match-answer'] },
}, 'inf.forward-backward')

// DAG-6 有机化学（从 generic 占位提升，对齐反应侧有机子类型 + 新增「价键校验」）
const organicDAG = buildCategoryDAG(
  'OrganicChemistry',
  '官能团·反应类型 DAG',
  {
    1: { id: 'org.identify-fg', dependsOn: [] },
    2: { id: 'org.determine-reaction', dependsOn: ['org.identify-fg'], branchKey: 'organic-subtype' },
    3: { id: 'org.write-product', dependsOn: ['org.determine-reaction'], branchKey: 'organic-subtype' },
  },
  'org.write-product',
  [
    {
      id: 'org.verify',
      index: 4,
      title: '价键 / 不饱和度校验',
      goal: '核对产物满足价键规则，且不饱和度与反应类型一致',
      analyze:
        '检查产物结构是否满足价键规则：碳四价、氮三价、氧二价、氢一价、卤素一价；再核对不饱和度 Ω = (2C + 2 + N − H − X) / 2（O、S 不计）与反应类型一致——加成反应后不饱和度降低、取代反应不饱和度不变、消去反应不饱和度升高。不满足说明产物结构或反应类型判断有误，需回退检查。',
      layer: 'Ability',
      dependsOn: ['org.match-answer'],
    },
  ],
)

/** 6 题型标准推导 DAG 注册表 */
export const CATEGORY_DAGS: Record<ChemCategory, ThoughtChainDAG> = {
  RedoxReaction: redoxDAG,
  IonicEquilibrium: ionicDAG,
  ChemicalEquilibrium: equilibriumDAG,
  Electrochemistry: electrochemDAG,
  InorganicInference: inorganicDAG,
  OrganicChemistry: organicDAG,
}

/** 取某题型的标准推导 DAG */
export const getCategoryDAG = (category: ChemCategory): ThoughtChainDAG => CATEGORY_DAGS[category]

// ============ 脚手架链 → 推导 DAG（P0 接入）============
// 把脚手架 A–F（或反应侧 A–E）链转成 ThoughtChainDAG，供 ThoughtDagFlow 单列垂直渲染。
// 不编造化学事实：节点文案取自 AtomicTask（title/goal/interaction/checks/pitfall），
//   边取自 markPassed 的真实线性解锁依赖（前一步 passed 才解锁后一步）；
//   AtomicTask 没有的字段（analyze/layer/metaPhase/skipWhen/branchKey/stage）一律省略。
// 空链 → nodes: []，ThoughtDagFlow 对空节点返回 null，自然降级不崩。

/** 脚手架链 → 推导 DAG */
export const scaffoldChainToDAG = (chain: ScaffoldChain): ThoughtChainDAG => {
  const nodes: ThoughtChainNode[] = chain.tasks.map((task, i) => ({
    id: `${chain.module}.${task.kind}`,
    index: task.index,
    title: task.title,
    goal: task.goal,
    dependsOn: i === 0 ? [] : [`${chain.module}.${chain.tasks[i - 1].kind}`],
    interaction: task.interaction,
    checks: task.checks,
    pitfall: task.pitfall,
  }))
  return { scope: { kind: 'scaffold', module: chain.module }, name: chain.name, nodes }
}
