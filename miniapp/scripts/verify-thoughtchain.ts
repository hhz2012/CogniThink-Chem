// R4 全题型思维链 DAG 统一规范 — 可复跑验证脚本（满足 FR-9 单测覆盖，零新增依赖，复用已有 ts-node）。
// 运行（在 miniapp/ 目录下）：
//   npx ts-node --project scripts/tsconfig.verify.json --transpile-only scripts/verify-thoughtchain.ts
// 覆盖 AC：AC-1/2/3/4/5/6/7（结构 / 校验 / 门控 / 实例化 / 真实题库绑定通路）。
// 退出码 0 = 全部通过，非 0 = 存在 FAIL。
import { CATEGORY_DAGS, REACTION_DAGS } from '../src/engine/thoughtChainDAGs'
import { validateDAG } from '../src/engine/thoughtChainValidation'
import { MODULE_CHAINS } from '../src/engine/scaffoldChains'
import {
  initialDagGateStates,
  markDagPassed,
  markDagFailed,
  resetDagStep,
  isDagUnlocked,
  getDagGateState,
} from '../src/engine/thoughtGate'
import {
  CATEGORY_REACTION_SCAFFOLD_MAP,
  getReactionKinds,
  getScaffoldModules,
  getCategoryForReactionKind,
} from '../src/engine/thoughtChainMapping'
import { resolveNodeConclusion, hasNodeConclusion } from '../src/engine/thoughtChainBindings'
import { chapters } from '../src/data/challenges'
import type { Challenge, ChemCategory } from '../src/types/chem'
import type { ScaffoldModuleKind } from '../src/types/scaffold'
import type { ThoughtChainDAG, ThoughtChainNode } from '../src/types/thoughtChain'
import { NO3_FE2_ENTRY, CL2_FEI2_ENTRY } from '../src/data/branchExamples'
import {
  resolveDagNodeBranch,
  deriveBranchConditions,
  branchKeyConditionKeys,
  instantiateDagNode,
} from '../src/engine/thoughtChainBranchResolver'
import {
  META_DAG,
  getMetaDAG,
  groupNodesByMetaPhase,
  getMetaPhaseLabel,
  getQuestionTarget,
  resolveQuestionTargetNodes,
} from '../src/engine/thoughtChainMetaDAG'
import type { MetaPhase } from '../src/types/thoughtChain'
import { META_PHASE_ORDER } from '../src/types/thoughtChain'

let failures = 0
const check = (name: string, cond: boolean, detail = '') => {
  if (!cond) { failures++; console.log('  FAIL ' + name + (detail ? ' -> ' + detail : '')) }
  else console.log('  ok   ' + name)
}

console.log('=== 6 题型 DAG 结构校验 ===')
const cats = Object.keys(CATEGORY_DAGS) as (keyof typeof CATEGORY_DAGS)[]
let totalNodes = 0
for (const cat of cats) {
  const dag = CATEGORY_DAGS[cat]
  totalNodes += dag.nodes.length
  const r = validateDAG(dag)
  console.log('\n[' + cat + '] ' + dag.name + ' — ' + dag.nodes.length + ' 节点')
  check(cat + ' 校验通过', r.ok, r.errors.join('; '))
  const roots = dag.nodes.filter((n) => n.dependsOn.length === 0)
  check(cat + ' 有根节点', roots.length >= 1, 'roots=' + roots.length)
  const withSkip = dag.nodes.filter((n) => n.skipWhen)
  const withBranch = dag.nodes.filter((n) => n.branchKey)
  console.log(
    '    roots=' + roots.map((n) => n.id).join(',') +
    ' | skipWhen=' + (withSkip.map((n) => n.id + ':' + n.skipWhen).join(',') || '-') +
    ' | branch=' + (withBranch.map((n) => n.id + ':' + n.branchKey).join(',') || '-'),
  )
  for (const n of dag.nodes) {
    console.log(
      '    ' + n.index + '. [' + n.layer + '] ' + n.id + ' "' + n.title + '"' +
      ' <- ' + (n.dependsOn.join(',') || '(root)') +
      (n.metaPhase ? ' [' + n.metaPhase + ']' : '') +
      (n.stage ? ' #' + n.stage : '') +
      (n.skipWhen ? ' skip=' + n.skipWhen : '') +
      (n.branchKey ? ' branch=' + n.branchKey : ''),
    )
  }
}
console.log('\n6 题型总节点数: ' + totalNodes)

console.log('\n=== 反应侧 DAG（Task 4）===')
const rkKeys = Object.keys(REACTION_DAGS)
console.log('反应 kind 总数: ' + rkKeys.length)
check('反应侧 DAG 覆盖 19 kind', rkKeys.length === 19, '实际=' + rkKeys.length)
let rxnValid = 0
for (const k of rkKeys) {
  const r = validateDAG(REACTION_DAGS[k as keyof typeof REACTION_DAGS])
  if (r.ok) rxnValid++
  else { console.log('  FAIL ' + k + ': ' + r.errors.join(';')); failures++ }
}
check('全部反应 DAG 校验通过', rxnValid === rkKeys.length, '通过=' + rxnValid + '/' + rkKeys.length)
check('reaction-generic 存在', !!REACTION_DAGS['reaction-generic'])
const org = REACTION_DAGS['reaction-organic-addition']
check('有机 step1=认官能团', org.nodes[0].title === '认官能团', org.nodes[0].title)
check('有机 step2=定反应类型', org.nodes[1].title === '定反应类型', org.nodes[1].title)
check('有机 step2 dependsOn step1', org.nodes[1].dependsOn.includes(org.nodes[0].id), 'deps=' + org.nodes[1].dependsOn.join(','))
check('有机子类型步骤数=5', org.nodes.length === 5, '实际=' + org.nodes.length)

console.log('\n=== 映射表 + 脚手架模块（Task 5）===')
const cats2 = Object.keys(CATEGORY_REACTION_SCAFFOLD_MAP) as ChemCategory[]
check('映射表覆盖 6 category', cats2.length === 6, '实际=' + cats2.length)
let mapOk = true
for (const c of cats2) {
  const a = CATEGORY_REACTION_SCAFFOLD_MAP[c]
  if (a.reactionKinds.length < 1 || a.scaffoldModules.length < 1) { mapOk = false; console.log('  FAIL ' + c) }
}
check('每个 category 映射 ≥1 reactionKind 且 ≥1 scaffoldModule', mapOk)
check('无机推断含 sequential-reaction 模块', getScaffoldModules('InorganicInference').includes('sequential-reaction'))
check('有机对齐 9 个 reaction kind', getReactionKinds('OrganicChemistry').length === 9, '实际=' + getReactionKinds('OrganicChemistry').length)
const expectedModules: ScaffoldModuleKind[] = [
  'redox-reasoning', 'ionic-system', 'aqueous-equilibrium', 'sequential-reaction',
  'electrochem-reasoning', 'chemical-equilibrium', 'inorganic-inference', 'organic-reaction',
]
let modOk = true
for (const m of expectedModules) {
  const chain = MODULE_CHAINS[m]
  if (!chain || !chain.tasks || chain.tasks.length < 5) { modOk = false; console.log('  FAIL module ' + m + ' tasks=' + (chain?.tasks?.length ?? 0)) }
}
check('8 模块均有非空 A–F 链(≥5步)', modOk)
check('MODULE_CHAINS 共 8 模块', Object.keys(MODULE_CHAINS).length === 8, '实际=' + Object.keys(MODULE_CHAINS).length)
check('反向查询 reaction-electrode → Electrochemistry', getCategoryForReactionKind('reaction-electrode') === 'Electrochemistry')
check('反向查询未知 kind → undefined', getCategoryForReactionKind('reaction-unknown' as never) === undefined)

console.log('\n=== DAG 门控（Task 6）===')
const redox = CATEGORY_DAGS.RedoxReaction
const s0 = initialDagGateStates(redox)
check('初始：S1 根节点 redox.read-problem unlocked', s0['redox.read-problem'] === 'unlocked')
check('初始：原 S2 根 list-particles 依赖 S1 → locked', s0['redox.list-particles'] === 'locked')
check('初始：非根 locked', s0['redox.mark-valence-species'] === 'locked' && s0['redox.infer-products'] === 'locked')
let s = s0
const order = ['redox.read-problem', 'redox.list-particles', 'redox.mark-valence-species', 'redox.judge-medium', 'redox.judge-order', 'redox.infer-products']
for (const id of order) s = markDagPassed(s, redox, id)
check('通过 stage1 后 stage2 根 redox.mark-valence 解锁', isDagUnlocked(redox, s, 'redox.mark-valence'))
check('未触及的末端仍 locked', s['redox.verify'] === 'locked')
check('未知节点 getDagGateState → locked', getDagGateState(s, 'nonexist') === 'locked')
check('未知节点 markDagPassed 不抛错', typeof markDagPassed(s, redox, 'nonexist') === 'object')
// skipWhen：跳过 judge-order(single-pair) 时，infer-products 不必先过 judge-order 即解锁
const skipSinglePair = (n: { skipWhen?: string }) => n.skipWhen === 'single-pair'
let s2 = initialDagGateStates(redox, skipSinglePair)
s2 = markDagPassed(s2, redox, 'redox.read-problem', skipSinglePair)
s2 = markDagPassed(s2, redox, 'redox.list-particles', skipSinglePair)
s2 = markDagPassed(s2, redox, 'redox.mark-valence-species', skipSinglePair)
s2 = markDagPassed(s2, redox, 'redox.judge-medium', skipSinglePair)
check('skip judge-order 后 infer-products 解锁', isDagUnlocked(redox, s2, 'redox.infer-products', skipSinglePair))
check('被 skip 的 judge-order 状态为 passed', getDagGateState(s2, 'redox.judge-order') === 'passed')
// failed / reset 语义
let s3 = markDagFailed(s0, redox, 'redox.read-problem')
check('unlocked → failed', s3['redox.read-problem'] === 'failed')
s3 = resetDagStep(s3, redox, 'redox.read-problem')
check('failed → unlocked(reset)', s3['redox.read-problem'] === 'unlocked')
check('locked 节点 markFailed 不改变', markDagFailed(s0, redox, 'redox.verify')['redox.verify'] === 'locked')

console.log('\n=== 实例化解析 + 缺失降级（Task 7）===')
const redoxCh = { category: 'RedoxReaction', solveData: { kind: 'redox-electron', halfReactions: [], balance: { lcm: 2 }, stage1: { particles: [{ formula: 'Fe2+' }] } } } as unknown as Challenge
check('redox.list-particles 解析非空', resolveNodeConclusion(redoxCh, 'redox.list-particles') !== null)
check('redox.balance 解析非空', resolveNodeConclusion(redoxCh, 'redox.balance') !== null)
check('redox 未知节点 → null', resolveNodeConclusion(redoxCh, 'redox.nope') === null)
check('previewSafe 恒 null(不泄答案)', resolveNodeConclusion(redoxCh, 'redox.list-particles', true) === null)
check('hasNodeConclusion 一致', hasNodeConclusion(redoxCh, 'redox.list-particles') === true)
const emptyCh = { category: 'IonicEquilibrium' } as unknown as Challenge
check('无 solveData：节点全 null', resolveNodeConclusion(emptyCh, 'ionic.list-species') === null && resolveNodeConclusion(emptyCh, 'ionic.write-conservation') === null)
const ionicCh = { category: 'IonicEquilibrium', solveData: { kind: 'ionic-conservation', species: [{ formula: 'Na+' }] } } as unknown as Challenge
check('ionic.list-species 解析', resolveNodeConclusion(ionicCh, 'ionic.list-species') !== null)
const eqCh = { category: 'ChemicalEquilibrium', solveData: { kind: 'equilibrium-shift', factors: [{ factor: '温度', shift: '正向', kChanges: '增大' }] } } as unknown as Challenge
check('eq.infer-shift 解析', resolveNodeConclusion(eqCh, 'eq.infer-shift') !== null)
const ecCh = { category: 'Electrochemistry', solveData: { kind: 'electrochem-cell', device: { type: '原电池' }, electrodes: {}, halfReactions: [] } } as unknown as Challenge
check('ec.identify-device 解析', resolveNodeConclusion(ecCh, 'ec.identify-device') !== null)
check('ec 无 calc → ec.compute null(安全降级)', resolveNodeConclusion(ecCh, 'ec.compute') === null)
const infCh = { category: 'InorganicInference', solveData: { kind: 'inorganic-inference', breakthrough: { clue: 'c', conclusion: 'O' } } } as unknown as Challenge
check('inf.breakthrough 解析', resolveNodeConclusion(infCh, 'inf.breakthrough') !== null)
const orgCh = { category: 'OrganicChemistry', solveData: { kind: 'organic-chemistry', functionalGroups: [{ name: '碳碳双键' }] } } as unknown as Challenge
check('org.identify-fg 解析', resolveNodeConclusion(orgCh, 'org.identify-fg') !== null)
check('org.verify 无专属字段 → null(安全降级)', resolveNodeConclusion(orgCh, 'org.verify') === null)

console.log('\n=== 真实题目绑定通路（Task 9 代表题抽样）===')
const allC = chapters.flatMap((c) => c.challenges)
const catOrder = Object.keys(CATEGORY_DAGS) as ChemCategory[]
let totalResolved = 0
for (const cat of catOrder) {
  const list = allC.filter((c) => c.category === cat)
  const withData = list.filter((c) => !!c.solveData)
  const nodeIds = CATEGORY_DAGS[cat].nodes.map((n) => n.id)
  let catResolved = 0
  let sample = ''
  for (const ch of withData) {
    for (const nid of nodeIds) {
      if (resolveNodeConclusion(ch, nid) !== null) { catResolved++; if (!sample) sample = ch.id + '::' + nid }
    }
  }
  totalResolved += catResolved
  console.log('  [' + cat + '] 题数=' + list.length + ' 有solveData=' + withData.length + ' 节点命中=' + catResolved + ' 样例=' + (sample || '(无)'))
  check(cat + ' 有数据则至少一节点可解析(无数据则跳过)', withData.length === 0 || catResolved > 0, 'withData=' + withData.length + ' resolved=' + catResolved)
}
console.log('真实题目节点命中总数: ' + totalResolved)

console.log('\n=== branchKey 运行时接线（resolveDagNodeBranch）===')
// 无 branchKey 的节点 → branch 为 null
check('无 branchKey 节点 → branch null', resolveDagNodeBranch(redoxCh, 'redox.balance') === null)
// previewSafe 恒 null（分支结论含答案，不得在 preview 泄露）
check(
  'previewSafe → branch null(不泄答案)',
  resolveDagNodeBranch(redoxCh, 'redox.infer-products', {
    previewSafe: true,
    branchEntry: NO3_FE2_ENTRY,
    conditions: { medium: '酸性' },
  }) === null,
)
// 真实分支数据：NO3_FE2 medium 酸性 → 命中酸分支（E 结论含 NO）
const acid = resolveDagNodeBranch(redoxCh, 'redox.infer-products', {
  branchEntry: NO3_FE2_ENTRY,
  conditions: { medium: '酸性' },
})
check('medium=酸性 命中酸分支', !!acid && acid.matched && /NO/.test(acid.conclusion.E ?? ''), acid?.conclusion.E ?? '')
// medium 中性 → 命中中性分支（E 结论含「不发生」）
const neutral = resolveDagNodeBranch(redoxCh, 'redox.infer-products', {
  branchEntry: NO3_FE2_ENTRY,
  conditions: { medium: '中性' },
})
check('medium=中性 命中中性分支', !!neutral && neutral.matched && /不发生/.test(neutral.conclusion.E ?? ''), neutral?.conclusion.E ?? '')
// medium 碱性 → 无匹配（安全降级：matched:false，conclusion 空）
const nomatch = resolveDagNodeBranch(redoxCh, 'redox.infer-products', {
  branchEntry: NO3_FE2_ENTRY,
  conditions: { medium: '碱性' },
})
check('medium=碱性 无匹配(安全降级)', !!nomatch && !nomatch.matched && Object.keys(nomatch.conclusion).length === 0)
// 无 branchEntry → null（Challenge 本身不携带 branches）
check('无 branchEntry → null', resolveDagNodeBranch(redoxCh, 'redox.infer-products', { conditions: { medium: '酸性' } }) === null)
// 数量分支：CL2_FEI2 quantity=少量 → 命中少量分支（E 结论含「只生成 I₂」）
const little = resolveDagNodeBranch(redoxCh, 'redox.infer-products', {
  branchEntry: CL2_FEI2_ENTRY,
  conditions: { quantity: '少量' },
})
check('quantity=少量 命中少量分支', !!little && little.matched && /只生成 I₂/.test(little.conclusion.E ?? ''), little?.conclusion.E ?? '')
// 多键 branchKey ['device-type','medium'] 归一化到 ['medium']（device-type 为原生轴）
check('多键 branchKey 归一化含 medium', branchKeyConditionKeys(['device-type', 'medium']).includes('medium'))
const ecNode = CATEGORY_DAGS.Electrochemistry.nodes.find((n) => n.id === 'ec.write-half-reactions')
check('ec.write-half-reactions branchKey 为数组', Array.isArray(ecNode?.branchKey))
const ecAcid = resolveDagNodeBranch(ecCh, 'ec.write-half-reactions', {
  branchEntry: NO3_FE2_ENTRY,
  conditions: { medium: '酸性' },
})
check('ec 多键节点经 medium 命中分支', !!ecAcid && ecAcid.matched)
// 题型原生轴 branchKey（subtype 等）→ condKeys 空 → 不走 resolveBranch（结论由 solveData 给出）
check('原生轴 subtype 不走 resolveBranch', resolveDagNodeBranch(infCh, 'inf.breakthrough', { branchEntry: NO3_FE2_ENTRY }) === null)
// 真实题库：deriveBranchConditions 从 solveData 提取条件（证明路径在真实数据上生效且不崩）
const eqWithTemp = allC.filter((c) => !!deriveBranchConditions(c).temperature).length
check('真实平衡题可提取 temperature 条件', eqWithTemp > 0, 'eqWithTemp=' + eqWithTemp)
const redoxWithMedium = allC.filter((c) => !!deriveBranchConditions(c).medium).length
check('真实 redox 题提取 medium 不崩(≥0)', redoxWithMedium >= 0, 'redoxWithMedium=' + redoxWithMedium)
// instantiateDagNode：conclusion 来自 solveData，branch 来自分支引擎
const inst = instantiateDagNode(redoxCh, 'redox.list-particles')
check('instantiateDagNode conclusion 非空', inst.conclusion !== null)
check('instantiateDagNode 无 branchKey → branch null', inst.branch === null)
const instBranch = instantiateDagNode(redoxCh, 'redox.infer-products', {
  branchEntry: NO3_FE2_ENTRY,
  conditions: { medium: '酸性' },
})
check('instantiateDagNode branch 命中', !!instBranch.branch && instBranch.branch.matched)
const instPreview = instantiateDagNode(redoxCh, 'redox.infer-products', { previewSafe: true })
check('instantiateDagNode preview 双空(不泄答案)', instPreview.conclusion === null && instPreview.branch === null)

console.log('\n=== 元 DAG + metaPhase 打标 ===')
// 元 DAG 结构：线性骨架 S1→S2→…→S6
const metaR = validateDAG(META_DAG)
check('META_DAG 校验通过', metaR.ok, metaR.errors.join('; '))
check("META_DAG scope.kind = 'meta'", META_DAG.scope.kind === 'meta')
check('META_DAG 节点数 = 6', META_DAG.nodes.length === 6, '实际=' + META_DAG.nodes.length)
const metaIds = META_DAG.nodes.map((n) => n.id)
check(
  'META_DAG id 依次为 meta.S1..meta.S6',
  JSON.stringify(metaIds) === JSON.stringify(['meta.S1', 'meta.S2', 'meta.S3', 'meta.S4', 'meta.S5', 'meta.S6']),
  metaIds.join(','),
)
const metaIdx = META_DAG.nodes.map((n) => n.index)
check('META_DAG index 1..6', JSON.stringify(metaIdx) === JSON.stringify([1, 2, 3, 4, 5, 6]), metaIdx.join(','))
let metaSelfLabel = true
for (const n of META_DAG.nodes) {
  if (n.metaPhase !== n.id.replace('meta.', '')) { metaSelfLabel = false; break }
}
check('每节点 metaPhase 等于自身阶段', metaSelfLabel)
check('S1 为根(无依赖)', META_DAG.nodes[0].dependsOn.length === 0, 'deps=' + META_DAG.nodes[0].dependsOn.join(','))
let linearOk = META_DAG.nodes.length === 6
for (let i = 1; i < META_DAG.nodes.length && linearOk; i++) {
  const expected = 'meta.' + META_PHASE_ORDER[i - 1]
  if (META_DAG.nodes[i].dependsOn.length !== 1 || META_DAG.nodes[i].dependsOn[0] !== expected) linearOk = false
}
check('依赖链线性(S_i 仅依赖 S_{i-1})', linearOk)

// 题型节点全覆盖打标：6 张题型 DAG 共 42 节点全部有 metaPhase（S1/S5 已闭合）
const allCatNodes = cats.flatMap((c) => CATEGORY_DAGS[c].nodes)
check('6 题型总节点 = 42', allCatNodes.length === 42, '实际=' + allCatNodes.length)
const unassigned = allCatNodes.filter((n) => !n.metaPhase)
check('42 节点全部有 metaPhase(无漏标)', unassigned.length === 0, '漏标=' + unassigned.map((n) => n.id).join(','))
const invalidPhase = allCatNodes.filter((n) => !!n.metaPhase && !META_PHASE_ORDER.includes(n.metaPhase))
check('所有 metaPhase ∈ META_PHASE_ORDER', invalidPhase.length === 0, '非法=' + invalidPhase.map((n) => n.id + ':' + n.metaPhase).join(','))

// 每张题型 DAG 都含 S1/S5 实例节点，且依赖闭合（S1 是唯一根，其余节点间接依赖 S1）
for (const cat of cats) {
  const dag = CATEGORY_DAGS[cat]
  const s1 = dag.nodes.find((n) => n.metaPhase === 'S1')
  const s5 = dag.nodes.find((n) => n.metaPhase === 'S5')
  check(cat + ' 含 S1 实例节点', !!s1)
  check(cat + ' 含 S5 实例节点', !!s5)
  check(cat + ' S1 为根(无依赖)', !!s1 && s1.dependsOn.length === 0, s1?.dependsOn.join(','))
  const roots = dag.nodes.filter((n) => n.dependsOn.length === 0)
  check(cat + ' 唯一根是 S1', roots.length === 1 && roots[0].metaPhase === 'S1', '根=' + roots.map((n) => n.id).join(','))
}

// 分布断言（单一真值源 NODE_META_PHASE；S1–S6 全阶段闭合）
const dist: Record<string, number> = {}
for (const n of allCatNodes) { const p = n.metaPhase ?? 'none'; dist[p] = (dist[p] ?? 0) + 1 }
check('S1 实例数 = 6', (dist['S1'] ?? 0) === 6, 'S1=' + (dist['S1'] ?? 0))
check('S2 实例数 = 7', (dist['S2'] ?? 0) === 7, 'S2=' + (dist['S2'] ?? 0))
check('S3 实例数 = 8', (dist['S3'] ?? 0) === 8, 'S3=' + (dist['S3'] ?? 0))
check('S4 实例数 = 11', (dist['S4'] ?? 0) === 11, 'S4=' + (dist['S4'] ?? 0))
check('S5 实例数 = 6', (dist['S5'] ?? 0) === 6, 'S5=' + (dist['S5'] ?? 0))
check('S6 实例数 = 4', (dist['S6'] ?? 0) === 4, 'S6=' + (dist['S6'] ?? 0))
check('分布总数 = 42', Object.values(dist).reduce((a, b) => a + b, 0) === 42)

// 辅助函数
check('getMetaDAG() === META_DAG(单例)', getMetaDAG() === META_DAG)
check("getMetaPhaseLabel('S1') === '读题定向'", getMetaPhaseLabel('S1') === '读题定向', getMetaPhaseLabel('S1'))
const redoxGroups = groupNodesByMetaPhase(CATEGORY_DAGS.RedoxReaction)
check('redox 分组含 S1 非空', (redoxGroups.S1?.length ?? 0) > 0)
check('redox 分组含 S2 非空', (redoxGroups.S2?.length ?? 0) > 0)
check('redox 分组含 S3 非空', (redoxGroups.S3?.length ?? 0) > 0)
check('redox 分组含 S4 非空', (redoxGroups.S4?.length ?? 0) > 0)
check('redox 分组含 S5 非空', (redoxGroups.S5?.length ?? 0) > 0)
check('redox 分组含 S6 非空', (redoxGroups.S6?.length ?? 0) > 0)
check('groupNodesByMetaPhase 空输入安全', Object.keys(groupNodesByMetaPhase(null)).length === 0)

// questionTarget / problemConditions 全覆盖 + S1/S5 真实数据下实例解析与映射命中
let realTargets = 0
let realConds = 0
let realHit = 0
for (const ch of allC) {
  if (getQuestionTarget(ch) !== undefined) realTargets++
  if (Array.isArray(ch.problemConditions) && ch.problemConditions.length > 0) realConds++
  if (resolveQuestionTargetNodes(ch, null).length > 0) realHit++
}
check('真实题库 questionTarget 全覆盖', realTargets === allC.length, '已填=' + realTargets + '/' + allC.length)
check('真实题库 problemConditions 全覆盖', realConds === allC.length, '已填=' + realConds + '/' + allC.length)
check('真实题库 questionTarget 映射全部命中', realHit === allC.length, '命中=' + realHit + '/' + allC.length)
// 84 题真实数据下 S1/S5 实例结论均非空
let s1ok = 0
let s5ok = 0
for (const ch of allC) {
  const dag = CATEGORY_DAGS[ch.category]
  const s1 = dag.nodes.find((n) => n.metaPhase === 'S1')
  const s5 = dag.nodes.find((n) => n.metaPhase === 'S5')
  if (s1 && resolveNodeConclusion(ch, s1.id) !== null) s1ok++
  if (s5 && resolveNodeConclusion(ch, s5.id) !== null) s5ok++
}
check('84 题 S1 实例结论非空', s1ok === allC.length, 's1=' + s1ok)
check('84 题 S5 实例结论非空', s5ok === allC.length, 's5=' + s5ok)
// S1 解析：problemConditions 非空 → 结论非空；preview 恒 null（守纪律）
const withConds = { ...redoxCh, problemConditions: ['Na 与 Cl₂ 反应', '求氧化剂还原剂'] } as Challenge
check('S1 problemConditions 解析非空', resolveNodeConclusion(withConds, 'redox.read-problem') !== null)
check('S1 preview 恒 null(守纪律)', resolveNodeConclusion(withConds, 'redox.read-problem', true) === null)
// S5 映射命中：questionTarget='配平系数比' → 命中 redox.balance / redox.lcm
const withTarget = { ...redoxCh, questionTarget: '产物' } as Challenge
check("questionTarget='产物' 可读取", getQuestionTarget(withTarget) === '产物', getQuestionTarget(withTarget) ?? 'undefined')
const withTarget2 = { ...redoxCh, questionTarget: '配平系数比' } as Challenge
const hit = resolveQuestionTargetNodes(withTarget2, META_DAG)
check('S5 映射：配平系数比 命中 balance+lcm', hit.includes('redox.balance') && hit.includes('redox.lcm'), '命中=' + hit.join(','))
const s5Conc = resolveNodeConclusion(withTarget2, 'redox.match-answer')
check('S5 match-answer 实例结论非空', s5Conc !== null)
check('S5 match-answer preview 恒 null', resolveNodeConclusion(withTarget2, 'redox.match-answer', true) === null)
// S5 无关键词命中 → []（安全降级）
check('S5 无命中关键词 → []', resolveQuestionTargetNodes({ ...redoxCh, questionTarget: '颜色' } as Challenge, META_DAG).length === 0)
check('空串 questionTarget → undefined(降级)', getQuestionTarget({ ...redoxCh, questionTarget: '' } as Challenge) === undefined)
check('null challenge → undefined(降级)', getQuestionTarget(null) === undefined)
check(
  'resolveQuestionTargetNodes null 安全',
  Array.isArray(resolveQuestionTargetNodes(null, null)) && resolveQuestionTargetNodes(null, null).length === 0,
)

console.log('\n=== 校验函数负样本 ===')
const mk = (nodes: ThoughtChainNode[]): ThoughtChainDAG => ({ scope: { kind: 'category', category: 'RedoxReaction' }, name: 't', nodes })
const base = (id: string, index: number, dependsOn: string[]): ThoughtChainNode =>
  ({ id, index, title: id, goal: '', analyze: '', layer: 'Knowledge', dependsOn })

check('合法DAG通过', validateDAG(mk([base('a', 1, []), base('b', 2, ['a'])])).ok)
check('缺依赖引用被检出', !validateDAG(mk([base('a', 1, []), base('b', 2, ['nonexist'])])).ok)
check('有环被检出', !validateDAG(mk([base('a', 1, ['b']), base('b', 2, ['a'])])).ok)
check('无根被检出', !validateDAG(mk([base('a', 1, ['b']), base('b', 2, ['a'])])).ok)
check('index破坏依赖序被检出', !validateDAG(mk([base('a', 1, ['b']), base('b', 2, [])])).ok)
check('空输入安全(null)', !validateDAG(null).ok)
check('空输入安全(undefined)', !validateDAG(undefined).ok)
check('空nodes安全', !validateDAG(mk([])).ok)

console.log('\n=== 结果: ' + (failures === 0 ? '全部通过 PASS' : failures + ' 项失败 FAIL') + ' ===')
process.exit(failures === 0 ? 0 : 1)
