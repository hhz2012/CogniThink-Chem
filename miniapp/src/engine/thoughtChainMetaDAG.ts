// 跨题型统一求解元骨架（R4 后继：把 6 张题型 DAG 提升到一张公共超结构）。
//
// 元骨架是「导航层」抽象，不替换任何题型 DAG：6 张题型 DAG 是它在各自条件轴与反应规则下
// 的实例化投影。题型节点通过 ThoughtChainNode.metaPhase 标注其在本骨架中的阶段（S1–S6）。
//
// 纯加性：不修改现有类型签名、不改现有消费方；现有运行时按构造零触碰。
// 纯函数：不就地修改、不抛错；越界 / 缺数据返回安全空值。
//
// 已知缺口（诚实记录，非缺陷）：
//   - S1 读题定向、S5 问题关联目前在题型 DAG 中大多无对应实例节点——题型 solveData 直接从
//     S2 起步；这两个阶段的结构化输入（题面条件清单 / questionTarget）有待数据附着。
//   - resolveQuestionTargetNodes 是 S5 的接入桩：当前无 target→节点 映射表，恒返回 [] 安全降级。

import type { Challenge, ChemCategory } from '../types/chem'
import type { MetaPhase, ThoughtChainDAG, ThoughtChainNode } from '../types/thoughtChain'
import { META_PHASE_ORDER } from '../types/thoughtChain'

/** 元阶段的展示标题 / 目标 / 认知层级（单一真值源，元 DAG 节点文案由此派生） */
export const META_PHASE_INFO: Record<MetaPhase, { title: string; goal: string; layer: ThoughtChainNode['layer']; analyze: string }> = {
  S1: {
    title: '读题定向',
    goal: '明确题面给出的条件与问题所求，锁定解题目标',
    layer: 'Knowledge',
    analyze:
      '先通读题干，分清三件事：题面给出了哪些物质和条件（浓度、温度、装置、现象等）、体系中可能涉及哪些反应或平衡、问题最终要求什么（产物、浓度、装置判断还是结构推断）。把问题所求记为目标，后续推导都围绕它展开，避免算到无关量上。',
  },
  S2: {
    title: '条件识别',
    goal: '识别影响反应 / 平衡的关键条件轴',
    layer: 'Application',
    analyze:
      '把题面条件按轴归类：氧化还原的介质（酸/碱/中性）与氧化剂还原剂用量；离子平衡的溶质类型与浓度；化学平衡的温度/压强/浓度/催化剂；电化学的装置类型（原电池/电解池）与电极介质；有机的官能团与反应条件；无机推断的特征现象。条件决定后续走哪条分支。',
  },
  S3: {
    title: '反应建模',
    goal: '判断是否发生反应、单步还是多步，或建立守恒 / 平衡关系',
    layer: 'Application',
    analyze:
      '分三类处理：顺向反应——判断是否发生反应、是单步还是多步（多步时上一步产物是下一步输入），推出产物；既有体系——不写新反应，而是建立守恒关系（离子）或写出平衡式（平衡）；逆向推断——由现象/产物反推反应物与转化链。这一步输出「反应/关系模型」，是后续推导的骨架。',
  },
  S4: {
    title: '推导结论',
    goal: '按规则一步步推出中间量',
    layer: 'Ability',
    analyze:
      '在模型骨架上按规则逐步推导：氧化还原标价态→算电子→求公倍数→配平→补氢氧；离子由守恒式推浓度关系；平衡由条件推移动方向与 K/转化率变化；电化学写半反应→析现象→计算；有机写产物；无机顺逆推转化链。每一步只推一个中间量，依赖前一步结果。',
  },
  S5: {
    title: '问题关联',
    goal: '把问题指向的量映射到已推导的结论节点',
    layer: 'Ability',
    analyze:
      '回到 S1 锁定的目标：问题问的是哪个量？在 S4 推导出的中间结论里找到对应节点——问产物就取推产物结论，问浓度就取守恒/浓度关系，问装置就取装置判断。若目标量未直接出现，看它能否由已推结论进一步得出；若推不出来，说明前面缺步或方向错了，需回退。',
  },
  S6: {
    title: '校验作答',
    goal: '用守恒 / 价键 / 代回验证结论，确定最终答案',
    layer: 'Ability',
    analyze:
      '对结论做最后校验：氧化还原查电子守恒与原子守恒；离子查三大守恒自洽；电化学查电子转移数与现象一致；有机查价键规则与不饱和度；无机组代回原题条件验证。校验通过才落笔作答；不通过则回退到出错的阶段。',
  },
}

/** 取元阶段的展示标题 */
export const getMetaPhaseLabel = (phase: MetaPhase): string => META_PHASE_INFO[phase].title

/** 6 阶段元 DAG（线性骨架 S1→S2→S3→S4→S5→S6；多步回环是运行时迭代，不构成图上的环） */
export const META_DAG: ThoughtChainDAG = (() => {
  const nodes: ThoughtChainNode[] = META_PHASE_ORDER.map((phase, i) => {
    const info = META_PHASE_INFO[phase]
    const index = i + 1
    const dependsOn = i === 0 ? [] : [`meta.${META_PHASE_ORDER[i - 1]}`]
    return {
      id: `meta.${phase}`,
      index,
      title: info.title,
      goal: info.goal,
      analyze: info.analyze,
      layer: info.layer,
      metaPhase: phase,
      stage: '元骨架',
      dependsOn,
    }
  })
  return { scope: { kind: 'meta' }, name: '跨题型统一求解元骨架', nodes }
})()

/** 取元 DAG（单例） */
export const getMetaDAG = (): ThoughtChainDAG => META_DAG

/** 把一张 DAG 的节点按 metaPhase 分组（未打标的节点归入 'unassigned' 之外的空结果，不报错） */
export const groupNodesByMetaPhase = (dag: ThoughtChainDAG | null | undefined): Partial<Record<MetaPhase, ThoughtChainNode[]>> => {
  const out: Partial<Record<MetaPhase, ThoughtChainNode[]>> = {}
  if (!dag?.nodes) return out
  for (const n of dag.nodes) {
    if (!n.metaPhase) continue
    if (!out[n.metaPhase]) out[n.metaPhase] = []
    out[n.metaPhase]!.push(n)
  }
  return out
}

/** 读取 Challenge 的 questionTarget（空串 / 非字符串 → undefined，安全降级） */
export const getQuestionTarget = (challenge: Challenge | null | undefined): string | undefined => {
  if (!challenge) return undefined
  const t = (challenge as Challenge).questionTarget
  return typeof t === 'string' && t.length > 0 ? t : undefined
}

/** questionTarget 关键词 → 命中的结论节点 id（按 category 分表；target 包含关键词即命中，合并去重） */
interface TargetRule {
  keyword: string
  nodeIds: string[]
}

/**
 * S5「问题关联」映射表（单一真值源）。
 * 题库 Challenge.questionTarget 标注为描述「题目所求」的短语；本按 category 查表，
 * 短语包含某关键词即命中对应节点，帮助学生把「问题问什么」定位到「推导到哪一步」。
 * 无 target / 无命中 → []，不抛错、不影响解题。
 */
const QUESTION_TARGET_MAP: Record<ChemCategory, TargetRule[]> = {
  RedoxReaction: [
    { keyword: '氧化剂', nodeIds: ['redox.mark-valence-species'] },
    { keyword: '还原剂', nodeIds: ['redox.mark-valence-species'] },
    { keyword: '氧化产物', nodeIds: ['redox.infer-products'] },
    { keyword: '还原产物', nodeIds: ['redox.infer-products'] },
    { keyword: '产物', nodeIds: ['redox.infer-products'] },
    { keyword: '配平', nodeIds: ['redox.balance', 'redox.lcm'] },
    { keyword: '系数', nodeIds: ['redox.balance', 'redox.lcm'] },
    { keyword: '电子', nodeIds: ['redox.count-electrons'] },
    { keyword: '价态', nodeIds: ['redox.mark-valence'] },
    { keyword: '化合价', nodeIds: ['redox.mark-valence'] },
    { keyword: '先后', nodeIds: ['redox.judge-order'] },
    { keyword: '顺序', nodeIds: ['redox.judge-order'] },
    { keyword: '介质', nodeIds: ['redox.infer-products', 'redox.judge-medium'] },
    { keyword: '离子方程', nodeIds: ['redox.patch-ho', 'redox.balance'] },
  ],
  IonicEquilibrium: [
    { keyword: '粒子', nodeIds: ['ionic.list-species'] },
    { keyword: '守恒', nodeIds: ['ionic.write-conservation'] },
    { keyword: '浓度', nodeIds: ['ionic.compare-concentration'] },
    { keyword: 'pH', nodeIds: ['ionic.compare-concentration'] },
    { keyword: '排序', nodeIds: ['ionic.compare-concentration'] },
    { keyword: '大小', nodeIds: ['ionic.compare-concentration'] },
  ],
  ChemicalEquilibrium: [
    { keyword: '移动', nodeIds: ['eq.infer-shift'] },
    { keyword: '平衡常数', nodeIds: ['eq.analyze-k'] },
    { keyword: '转化率', nodeIds: ['eq.infer-shift', 'eq.analyze-k'] },
    { keyword: '产量', nodeIds: ['eq.infer-shift'] },
    { keyword: '条件', nodeIds: ['eq.identify-condition', 'eq.infer-shift'] },
  ],
  Electrochemistry: [
    { keyword: '装置', nodeIds: ['ec.identify-device'] },
    { keyword: '原电池', nodeIds: ['ec.identify-device'] },
    { keyword: '电解池', nodeIds: ['ec.identify-device'] },
    { keyword: '电极反应', nodeIds: ['ec.write-half-reactions'] },
    { keyword: '半反应', nodeIds: ['ec.write-half-reactions'] },
    { keyword: '正极', nodeIds: ['ec.identify-device', 'ec.analyze-phenomena'] },
    { keyword: '负极', nodeIds: ['ec.identify-device', 'ec.analyze-phenomena'] },
    { keyword: '阴极', nodeIds: ['ec.identify-device', 'ec.analyze-phenomena'] },
    { keyword: '阳极', nodeIds: ['ec.identify-device', 'ec.analyze-phenomena'] },
    { keyword: '现象', nodeIds: ['ec.analyze-phenomena'] },
    { keyword: '计算', nodeIds: ['ec.compute'] },
    { keyword: '电量', nodeIds: ['ec.compute'] },
  ],
  InorganicInference: [
    { keyword: '推断', nodeIds: ['inf.breakthrough', 'inf.forward-backward'] },
    { keyword: '化学式', nodeIds: ['inf.breakthrough', 'inf.forward-backward'] },
    { keyword: '检验', nodeIds: ['inf.breakthrough'] },
    { keyword: '鉴别', nodeIds: ['inf.breakthrough'] },
    { keyword: '转化', nodeIds: ['inf.forward-backward'] },
    { keyword: '用途', nodeIds: ['inf.breakthrough'] },
    { keyword: '性质', nodeIds: ['inf.breakthrough'] },
  ],
  OrganicChemistry: [
    { keyword: '官能团', nodeIds: ['org.identify-fg'] },
    { keyword: '反应类型', nodeIds: ['org.determine-reaction'] },
    { keyword: '取代', nodeIds: ['org.determine-reaction'] },
    { keyword: '加成', nodeIds: ['org.determine-reaction'] },
    { keyword: '消去', nodeIds: ['org.determine-reaction'] },
    { keyword: '产物', nodeIds: ['org.write-product'] },
    { keyword: '结构', nodeIds: ['org.write-product'] },
    { keyword: '同分异构', nodeIds: ['org.write-product', 'org.verify'] },
  ],
}

/**
 * S5「问题关联」反向索引：questionTarget → 该题需要的节点 id 列表。
 * 按 challenge.category 查 QUESTION_TARGET_MAP，target 包含关键词即命中（合并去重）；
 * 无 target / 无 category 映射 / 无命中 → []，不抛错、不影响解题。
 */
export const resolveQuestionTargetNodes = (
  challenge: Challenge | null | undefined,
  _dag: ThoughtChainDAG | null | undefined,
): string[] => {
  const target = getQuestionTarget(challenge)
  if (!target || !challenge) return []
  const rules = QUESTION_TARGET_MAP[challenge.category]
  if (!rules || rules.length === 0) return []
  const hit = new Set<string>()
  for (const r of rules) {
    if (target.includes(r.keyword)) {
      for (const id of r.nodeIds) hit.add(id)
    }
  }
  return Array.from(hit)
}
