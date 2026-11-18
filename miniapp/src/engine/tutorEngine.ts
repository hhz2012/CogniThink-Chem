// ============================================================
// 教学引擎（Tutor Engine）
// 能力：
//   1. 任意题目 → 对应知识点
//   2. 任意题目 → 分阶段（6 阶段）提问 / 提示 / 揭示结构
//   3. 学生做对 / 做错 → 不同的探问路径
//   4. 任意题目 → 结构化验证（答案 + 守恒/边界检查项）
//   5. 按题目分类批量获取全部脚本
// ============================================================

import { Challenge, ChemCategory, Difficulty, Thought } from '../types/chem'
import { allChallenges, getChallengeById } from '../data/challenges'
import {
  KnowledgeRef,
  knowledgeOfCategory,
  matchKnowledge,
} from '../data/knowledge'
import { knowledgeForChallenge } from '../data/knowledge/redox'

/** 学生作答结果 */
export type Outcome = 'correct' | 'wrong'

/** 一条结构化探问 */
export interface TutorProbe {
  id: string
  question: string
  /** 提问目的（为什么问这个） */
  purpose: string
  /** 期望学生表现（判定是否通过的参照） */
  expect: string
}

/** 题目的一个解题阶段 */
export interface TutorStage {
  id: string
  index: number
  title: string
  layer: 'Knowledge' | 'Application' | 'Ability'
  /** 本阶段要达成的目标 */
  goal: string
  /** 本阶段的引导提问 */
  probes: string[]
  /** 一级提示 */
  hint?: string
  /** 二级提示（支架：给出知识点要点） */
  scaffold: string
  /** 三级提示（揭示：直接给结论/依据） */
  reveal: string
  knowledgePointIds: string[]
}

/** 结构性验证 */
export interface TutorValidation {
  answer: { letter: string; text: string }
  /** 该题型必须通过的检查项 */
  checks: string[]
  explanation: string
  /** 常见错误落点 */
  errorTraps: string[]
}

/** 一道题的完整教学脚本 */
export interface TutorScript {
  challengeId: string
  title: string
  category: ChemCategory
  difficulty: Difficulty
  thought: Thought
  knowledgePoints: KnowledgeRef[]
  stages: TutorStage[]
  outcomes: Record<Outcome, TutorProbe[]>
  validation: TutorValidation
}

// ==================== 常量 ====================

/** 难度 → 认知层，用于知识点兜底匹配 */
const DIFF_LAYER: Record<Difficulty, KnowledgeRef['layer']> = {
  基础: 'Knowledge',
  进阶: 'Application',
  综合: 'Ability',
  难题: 'Ability',
  压轴: 'Ability',
}

/** 六阶段通用目标 */
const STAGE_GOALS = [
  '读懂题意：圈出条件、数据与所求',
  '建立模型：判断题目属于哪类反应/平衡/结构问题',
  '调动知识：找到对应的概念、规律或守恒关系',
  '应用方法：按规律推进，写出关键中间结论',
  '精确求解：完成计算或方程式书写',
  '结果校验：用守恒或边界条件验证答案',
]

/** 做对时的分类追问 */
const CORRECT_PROBES: Record<ChemCategory, [string, string]> = {
  RedoxReaction: [
    '你是用哪种守恒关系确定答案的？得失电子数具体怎么算的？',
    '如果把稀硝酸换成浓硝酸，产物和结论会怎么变？',
  ],
  IonicEquilibrium: [
    '你最先判断的是酸碱性还是离子浓度？依据是什么？',
    '若把溶液稀释 10 倍，你的结论会变吗？为什么？',
  ],
  ChemicalEquilibrium: [
    '你用的是 Q 与 K 比较，还是勒夏特列原理？为什么选它？',
    '如果只改变温度，K 和转化率分别怎么变？',
  ],
  Electrochemistry: [
    '你是怎么判断正负极（或阴阳极）的？',
    '如果换成惰性电极或改变介质，产物会变吗？',
  ],
  InorganicInference: [
    '你的突破口是哪个特征现象或性质？',
    '还有没有其他物质能同时满足题干所有条件？',
  ],
  OrganicChemistry: [
    '你是由哪个官能团推出这条性质的？',
    '你列举的同分异构体，如何保证不重不漏？',
  ],
}

/** 做错时的分类诊断提问 */
const WRONG_PROBES: Record<ChemCategory, [string, string]> = {
  RedoxReaction: [
    '先告诉我：这道题里哪些元素化合价发生了变化？分别变成多少？',
    '你算的失电子总数和得电子总数相等吗？差在哪一边？',
  ],
  IonicEquilibrium: [
    '这题主要考电离还是水解？谁强谁弱？',
    '你写电荷守恒时，多价离子的系数乘上了吗？',
  ],
  ChemicalEquilibrium: [
    '你是改变了浓度、压强还是温度？对应平衡向哪边移动？',
    'K 只与温度有关，你的表达式里有没有混进固体或纯液体？',
  ],
  Electrochemistry: [
    '先确定：这是原电池还是电解池？负极/阳极分别是谁？',
    '你写的电极反应式，两边电荷和原子数都配平了吗？',
  ],
  InorganicInference: [
    '你的突破口是颜色、状态还是某一步反应现象？',
    '你得到的结论能同时满足题干所有条件吗？逐条核对。',
  ],
  OrganicChemistry: [
    '你圈出了哪些官能团？每个官能团对应什么性质？',
    '你写的结构式，每个碳的价键数都是 4 吗？',
  ],
}

/** 思维类型 → 结构化验证检查项 */
const THOUGHT_CHECKS: Record<Thought, string[]> = {
  Conservation: [
    '电子守恒：失电子总数 = 得电子总数',
    '原子守恒：方程式两边各元素原子数相等',
    '电荷守恒：离子方程式两边电荷代数和相等',
  ],
  Equilibrium: [
    'Q 与 K 的比较方向是否正确',
    '是否只改变了一个条件，其余条件保持不变',
    '固体与纯液体未写入平衡常数表达式',
  ],
  Structure: [
    '结构决定性质：官能团/化合价判断与结构一致',
    '电子式与结构式的价键数是否饱和',
    '同分异构体是否不重不漏',
  ],
  Classification: [
    '分类标准是否唯一且前后一致',
    '每个选项是否都被逐一排除或确认',
    '是否存在"都符合"或"都不符合"的遗漏',
  ],
  ModelCognition: [
    '模型假设是否与题干条件一致',
    '结论是否能解释题干中的全部现象',
    '边界条件（少量/过量/恰好反应）是否考虑',
  ],
}

/** 思维类型 → 常见错误落点 */
const THOUGHT_TRAPS: Record<Thought, string[]> = {
  Conservation: ['漏乘变价原子个数', '把转移电子数与得失电子数混用', '忽略部分变价'],
  Equilibrium: ['把催化剂当作平衡移动因素', '固体计入 K 表达式', '混淆转化率与产率'],
  Structure: ['官能团漏判', '价键数不饱和', '同分异构体重复计数'],
  Classification: ['分类标准中途变化', '漏排某个选项', '忽略隐含条件'],
  ModelCognition: ['模型套用错位', '忽略少量/过量条件', '以偏概全'],
}

// ==================== 知识点解析 ====================

function toRef(kp: { id: string; name: string; layer: KnowledgeRef['layer']; summary: string }): KnowledgeRef {
  return { id: kp.id, name: kp.name, layer: kp.layer, summary: kp.summary, keywords: [] }
}

/**
 * 题目 → 知识点
 * 1) 氧化还原：使用精确映射
 * 2) 其他分类：题干关键词匹配
 * 3) 兜底：按难度对应认知层取同层知识点
 */
export function resolveKnowledgePoints(challenge: Challenge): KnowledgeRef[] {
  if (challenge.category === 'RedoxReaction') {
    const exact = knowledgeForChallenge(challenge.id)
    if (exact.length > 0) return exact.map(toRef)
  }
  const text = `${challenge.title} ${challenge.problemText} ${challenge.explanation}`
  const matched = matchKnowledge(challenge.category, text)
  if (matched.length > 0) return matched

  const layer = DIFF_LAYER[challenge.difficulty]
  const sameLayer = knowledgeOfCategory(challenge.category).filter((k) => k.layer === layer)
  return sameLayer.slice(0, 2)
}

// ==================== 脚本构建 ====================

function buildStages(challenge: Challenge, kps: KnowledgeRef[]): TutorStage[] {
  return challenge.guide.map((g, i) => {
    const stageKps = kps.filter((k) => k.name === g.knowledgeCard?.name)
    const kpIds = stageKps.length > 0
      ? stageKps.map((k) => k.id)
      : i === challenge.guide.length - 1 && kps[0]
        ? [kps[0].id]
        : []

    return {
      id: `${challenge.id}-S${i + 1}`,
      index: i + 1,
      title: g.title,
      layer: g.cognitiveLayer,
      goal: STAGE_GOALS[i] ?? g.title,
      probes: g.prompts,
      hint: g.hint,
      scaffold:
        g.knowledgeCard?.summary ??
        (stageKps[0]?.summary ?? '回到本阶段的核心概念，先说出它的定义，再动手。'),
      reveal: g.knowledgeCard
        ? `${g.knowledgeCard.name}：${g.knowledgeCard.summary ?? ''}${
            g.knowledgeCard.textbookRef ? `（${g.knowledgeCard.textbookRef}）` : ''
          }`
        : '回到题干，把已知条件与所求逐条列成清单，再对照本阶段目标检查遗漏。',
      knowledgePointIds: kpIds,
    }
  })
}

function buildOutcomeProbes(challenge: Challenge, outcome: Outcome): TutorProbe[] {
  const common: Array<[string, string, string]> =
    outcome === 'correct'
      ? [
          ['你是怎么快速锁定切入点的？', '提取可复用的解题策略', '能说出一条明确的判断依据'],
          ['如果题干某个条件改变，答案会怎么变？', '检验迁移与条件敏感性', '能指出受影响的条件及变化方向'],
          ['这题最关键的一步，能用一句话概括吗？', '检验结构化理解', '能概括出核心关系而非复述步骤'],
        ]
      : [
          ['先不急着算，你卡在读题、找关系还是计算？', '定位卡点阶段', '能明确指出卡住的阶段'],
          ['题干给了哪些数据？你打算先处理哪一个？', '重建解题起点', '能列出已知量、未知量与所求'],
          ['你的答案和选项对得上吗？哪一步最没把握？', '暴露错误步骤', '能指出具体不确定的步骤'],
        ]

  const categoryProbes = outcome === 'correct' ? CORRECT_PROBES[challenge.category] : WRONG_PROBES[challenge.category]

  const probes: TutorProbe[] = [
    ...common.map(([question, purpose, expect], i) => ({
      id: `${challenge.id}-${outcome}-C${i + 1}`,
      question,
      purpose,
      expect,
    })),
    ...categoryProbes.map((question, i) => ({
      id: `${challenge.id}-${outcome}-K${i + 1}`,
      question,
      purpose: outcome === 'correct' ? '验证知识点是否真正内化' : '诊断知识点缺口',
      expect: outcome === 'correct' ? '能解释原因并迁移到变式' : '能回到对应知识点重新表述',
    })),
  ]

  if (outcome === 'wrong') {
    probes.push({
      id: `${challenge.id}-wrong-S`,
      question: `回顾一下：这题的第几步（${STAGE_GOALS.map((s, i) => `${i + 1}.${s.split('：')[0]}`).join(' / ')}）出了问题？`,
      purpose: '把错误定位到具体阶段，便于按阶段补救',
      expect: '能选出一个具体阶段并说明原因',
    })
  }

  return probes
}

function buildValidation(challenge: Challenge): TutorValidation {
  const letter = String.fromCharCode(65 + challenge.answerIndex)
  return {
    answer: { letter, text: challenge.options[challenge.answerIndex] ?? '' },
    checks: THOUGHT_CHECKS[challenge.thought] ?? [],
    explanation: challenge.explanation,
    errorTraps: THOUGHT_TRAPS[challenge.thought] ?? [],
  }
}

// ==================== 缓存 & 对外 API ====================

const cache = new Map<string, TutorScript>()

/** 获取单题完整教学脚本 */
export function getTutorScript(challengeId: string): TutorScript | null {
  const cached = cache.get(challengeId)
  if (cached) return cached

  const challenge = getChallengeById(challengeId)
  if (!challenge) return null

  const knowledgePoints = resolveKnowledgePoints(challenge)
  const script: TutorScript = {
    challengeId: challenge.id,
    title: challenge.title,
    category: challenge.category,
    difficulty: challenge.difficulty,
    thought: challenge.thought,
    knowledgePoints,
    stages: buildStages(challenge, knowledgePoints),
    outcomes: {
      correct: buildOutcomeProbes(challenge, 'correct'),
      wrong: buildOutcomeProbes(challenge, 'wrong'),
    },
    validation: buildValidation(challenge),
  }
  cache.set(challengeId, script)
  return script
}

/** 按分类（或全部）批量获取教学脚本 */
export function listTutorScripts(category?: ChemCategory): TutorScript[] {
  const source = category ? allChallenges.filter((c) => c.category === category) : allChallenges
  return source.map((c) => getTutorScript(c.id)).filter((s): s is TutorScript => s !== null)
}

/** 获取某题的指定阶段（index 从 1 开始） */
export function getStage(challengeId: string, index: number): TutorStage | null {
  const script = getTutorScript(challengeId)
  return script?.stages[index - 1] ?? null
}

/** 获取某阶段的结构化提问 */
export function getStageProbes(challengeId: string, stageIndex: number): TutorProbe[] {
  const stage = getStage(challengeId, stageIndex)
  if (!stage) return []
  return stage.probes.map((q, i) => ({
    id: `${stage.id}-P${i + 1}`,
    question: q,
    purpose: `推进第 ${stage.index} 阶段：${stage.goal}`,
    expect: '学生能给出判断依据或中间结论',
  }))
}

/** 获取做对 / 做错时的探问路径 */
export function getOutcomeProbes(challengeId: string, outcome: Outcome): TutorProbe[] {
  return getTutorScript(challengeId)?.outcomes[outcome] ?? []
}

/** 获取某题对应的知识点 */
export function getKnowledgePointsForChallenge(challengeId: string): KnowledgeRef[] {
  return getTutorScript(challengeId)?.knowledgePoints ?? []
}

/** 获取某题的结构化验证 */
export function getValidation(challengeId: string): TutorValidation | null {
  return getTutorScript(challengeId)?.validation ?? null
}

/** 按分类汇总：题目数、知识点覆盖 */
export function summarizeCategory(category: ChemCategory): {
  total: number
  knowledgePointIds: string[]
} {
  const scripts = listTutorScripts(category)
  const ids = new Set<string>()
  scripts.forEach((s) => s.knowledgePoints.forEach((k) => ids.add(k.id)))
  return { total: scripts.length, knowledgePointIds: [...ids] }
}
