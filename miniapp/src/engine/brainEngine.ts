// ============================================================
// 化学智能大脑（Chemistry Brain Engine）
// 能力：
//   1. 题目分析：题型、要求、已知条件、限定条件
//   2. 逐选项分析：每个选项的结论、是否满足题意、关联知识点、错误类型
//   3. 作答诊断：答对/答错 → 错误类型 + 薄弱知识点 + 下一步建议
//   4. 完整讲解：要求 → 选项表 → 知识点 → 解题步骤 → 验证 → 变式
//   5. 学习者画像：跨题汇总薄弱知识点与错误类型
// 与 tutorEngine / 知识注册表协同工作。
// ============================================================

import { Challenge, ChemCategory, Thought } from '../types/chem'
import { getChallengeById } from '../data/challenges'
import {
  KnowledgeRef,
  getFullKnowledgePoint,
  matchKnowledge,
} from '../data/knowledge'
import {
  getTutorScript,
  getOutcomeProbes,
  getStage,
  getValidation,
  TutorProbe,
  TutorStage,
} from './tutorEngine'

export type QuestionType = 'choice' | 'fill' | 'calculation'

export type ErrorType =
  | '概念混淆'
  | '知识点缺口'
  | '审题偏差'
  | '计算失误'
  | '书写不规范'
  | '逻辑跳跃'

export interface RequirementAnalysis {
  askType: '选择正确' | '选择错误' | '计算' | '书写/推断' | '比较/排序' | '未知'
  /** 题目要求得到的结果 */
  target: string
  /** 题干给出的已知条件 */
  givens: string[]
  /** 限定条件：标准状况、过量、离子方程式等 */
  constraints: string[]
}

export interface OptionAnalysis {
  index: number
  letter: string
  text: string
  verdict: 'correct' | 'incorrect'
  /** 是否满足题目要求 */
  meetsRequirement: boolean
  /** 该选项对应哪个知识点 */
  linkedKnowledgeIds: string[]
  /** 这个选项会带来什么结果 / 为什么不符合题意 */
  reasoning: string
  /** 若为干扰项，错误类型 */
  errorType?: ErrorType
}

export interface ChallengeAnalysis {
  challengeId: string
  title: string
  type: QuestionType
  difficulty: Challenge['difficulty']
  thought: Thought
  requirement: RequirementAnalysis
  knowledgePoints: KnowledgeRef[]
  /** 本题真正要考的核心结论 */
  corePoints: string[]
  /** 选项易错点 */
  traps: string[]
  options: OptionAnalysis[]
  /** 解题逻辑链（按阶段） */
  reasoningChain: string[]
}

export interface FullExplanation {
  requirement: RequirementAnalysis
  options: OptionAnalysis[]
  knowledgeDetails: Array<{
    id: string
    name: string
    summary: string
    keyPoints: string[]
    pitfalls: string[]
  }>
  corePoints: string[]
  traps: string[]
  workedSteps: string[]
  verification: string[]
  transferQuestion: string
}

export interface AnswerDiagnosis {
  challengeId: string
  correct: boolean
  selectedIndex: number | null
  chosen: OptionAnalysis | null
  correctOption: OptionAnalysis
  /** 答错时的错误类型（答对为 null） */
  errorType: ErrorType | null
  /** 需要补的知识点（答对为空） */
  weakKnowledgePoints: KnowledgeRef[]
  /** 智能判断的一句话结论 */
  verdict: string
  /** 下一步建议（先做什么） */
  suggestion: string
  /** 逐步探问（来自教学引擎，最多 2 条，避免信息过载） */
  probes: TutorProbe[]
  /** 建议进入的阶段 */
  nextStage: TutorStage | null
  explanation: FullExplanation
}

export interface LearnerAttempt {
  challengeId: string
  selectedIndex: number | null
  correct: boolean
}

export interface LearnerProfile {
  total: number
  correct: number
  accuracy: number
  weakKnowledgePoints: Array<{ id: string; name: string; missCount: number; categories: ChemCategory[] }>
  errorTypeCounts: Record<ErrorType, number>
  recommendations: string[]
}

// ==================== 题目分析 ====================

const CONSTRAINT_WORDS = [
  '标准状况', '常温常压', '无色', '透明', '酸性', '碱性', '中性',
  '过量', '少量', '足量', '等体积', '等浓度', '恒温', '恒容', '恒压',
  '离子方程式', '正确的是', '错误的是', '不考虑立体异构',
]

/** 从题干中提取"数字+单位"形式的已知条件 */
function extractGivens(text: string): string[] {
  const matches = text.match(/\d+(?:\.\d+)?\s*(?:mol\/L|mol|mL|L|g|kg|℃|K|%|mol·L⁻¹)/g) ?? []
  return [...new Set(matches.map((m) => m.trim()))].slice(0, 8)
}

function analyzeRequirement(challenge: Challenge): RequirementAnalysis {
  const text = `${challenge.title} ${challenge.problemText}`
  let askType: RequirementAnalysis['askType'] = '未知'
  if (/错误的是|不正确的是|不符合|不能/.test(text)) askType = '选择错误'
  else if (/正确的是|正确的是|符合/.test(text)) askType = '选择正确'
  else if (/配平|写出|方程式|离子方程式/.test(text)) askType = '书写/推断'
  else if (/由大到小|由小到大|排序|顺序/.test(text)) askType = '比较/排序'
  else if (/计算|多少|物质的量|体积|质量|浓度/.test(text)) askType = '计算'

  const constraints = CONSTRAINT_WORDS.filter((w) => text.includes(w))
  const target =
    askType === '选择正确'
      ? '选出与题干条件完全一致的选项'
      : askType === '选择错误'
        ? '选出与题干条件矛盾（说法错误）的选项'
        : askType === '计算'
          ? '求出题目所问的量'
          : askType === '书写/推断'
            ? '写出符合题意的方程式或推断结论'
            : askType === '比较/排序'
              ? '按题目指定顺序排列'
              : challenge.title

  return {
    askType,
    target,
    givens: extractGivens(challenge.problemText),
    constraints,
  }
}

/** 根据选项文本特征推断干扰项的错误类型 */
function inferErrorType(text: string, kpName?: string): ErrorType {
  if (/\d/.test(text) && /(mol|g|L|mL|%)/.test(text)) return '计算失误'
  if (/一定|都|只能|全部|必然/.test(text)) return '概念混淆'
  if (/↑|↓|电子|方程式|离子/.test(text)) return '书写不规范'
  if (kpName) return '知识点缺口'
  return '概念混淆'
}

function analyzeOption(
  challenge: Challenge,
  index: number,
  fallbackKps: KnowledgeRef[],
): OptionAnalysis {
  const text = challenge.options[index] ?? ''
  const isCorrect = index === challenge.answerIndex
  const matched = matchKnowledge(challenge.category, text)
  const linked = matched.length > 0
    ? matched.map((m) => m.id)
    : fallbackKps.slice(0, 1).map((k) => k.id)

  const correctLetter = String.fromCharCode(65 + challenge.answerIndex)
  const correctText = challenge.options[challenge.answerIndex] ?? ''

  if (isCorrect) {
    return {
      index,
      letter: String.fromCharCode(65 + index),
      text,
      verdict: 'correct',
      meetsRequirement: true,
      linkedKnowledgeIds: linked,
      reasoning: challenge.explanation,
    }
  }

  const kp = linked[0] ? getFullKnowledgePoint(linked[0]) : undefined
  const pitfall = kp?.pitfalls[0]

  return {
    index,
    letter: String.fromCharCode(65 + index),
    text,
    verdict: 'incorrect',
    meetsRequirement: false,
    linkedKnowledgeIds: linked,
    reasoning: pitfall
      ? `代入「${kp!.name}」判断：${pitfall}。正确结论是 ${correctLetter}. ${correctText}，本项与此矛盾。`
      : `本项与正确结论 ${correctLetter}. ${correctText} 不一致，回到题干条件逐条核对即可排除。`,
    errorType: inferErrorType(text, kp?.name),
  }
}

/** 题目分析（选择题逐选项拆解） */
export function analyzeChallenge(challengeId: string): ChallengeAnalysis | null {
  const challenge = getChallengeById(challengeId)
  if (!challenge) return null
  const script = getTutorScript(challengeId)
  const kps = script?.knowledgePoints ?? []
  const requirement = analyzeRequirement(challenge)
  const type: QuestionType =
    challenge.options.length > 0
      ? /计算|多少|物质的量/.test(challenge.problemText) ? 'calculation' : 'choice'
      : 'fill'

  const options =
    challenge.options.length > 0
      ? challenge.options.map((_, i) => analyzeOption(challenge, i, kps))
      : []

  const traps = [...new Set(options.flatMap((o) => (o.errorType ? [o.errorType] : [])))]
  const corePoints = script?.knowledgePoints.map((k) => k.summary) ?? []

  return {
    challengeId,
    title: challenge.title,
    type,
    difficulty: challenge.difficulty,
    thought: challenge.thought,
    requirement,
    knowledgePoints: kps,
    corePoints,
    traps,
    options,
    reasoningChain: script?.stages.map((s) => `${s.index}. ${s.title} — ${s.goal}`) ?? [],
  }
}

/** 单选项分析 */
export function analyzeOptionById(challengeId: string, index: number): OptionAnalysis | null {
  const analysis = analyzeChallenge(challengeId)
  return analysis?.options[index] ?? null
}

// ==================== 作答诊断 ====================

/** 根据错误选项推断薄弱知识点（优先直接关联，其次同类知识点） */
function inferWeakKnowledge(
  _challenge: Challenge,
  chosen: OptionAnalysis,
  allKps: KnowledgeRef[],
): KnowledgeRef[] {
  const direct = chosen.linkedKnowledgeIds
    .map((id) => allKps.find((k) => k.id === id))
    .filter((k): k is KnowledgeRef => Boolean(k))
  if (direct.length > 0) return direct.slice(0, 2)

  // 回退：按错误类型选择层
  const layer =
    chosen.errorType === '计算失误' ? 'Ability' : chosen.errorType === '审题偏差' ? 'Knowledge' : 'Application'
  const fallback = allKps.filter((k) => k.layer === layer)
  return (fallback.length > 0 ? fallback : allKps).slice(0, 2)
}

function buildSuggestion(errorType: ErrorType | null, weak: KnowledgeRef[]): string {
  if (!errorType) return '思路正确。试着说出你判断的关键依据，再做一道同知识点的变式题。'
  const kpName = weak[0]?.name ?? '相关知识点'
  switch (errorType) {
    case '计算失误':
      return `先重算一遍，重点检查单位换算、系数和有效数字；再回到「${kpName}」核对关系式。`
    case '审题偏差':
      return '先把题干拆成"已知、反应、所求"三行，再逐项排除选项，避免漏看限定条件。'
    case '书写不规范':
      return `先按"电子守恒→电荷守恒→原子守恒"的顺序重写一遍，再对照「${kpName}」检查。`
    case '知识点缺口':
      return `先补「${kpName}」的定义和一条核心要点，确认理解后再回原题。`
    case '逻辑跳跃':
      return '把每一步的判断依据写出来，任何一步说不出理由就停下来补知识。'
    default:
      return `回看「${kpName}」的易错点，再对比正确选项与你的选项差在哪里。`
  }
}

/** 作答诊断：答对/答错 → 错误类型、薄弱知识点、建议、探问、讲解 */
export function diagnoseAnswer(
  challengeId: string,
  selectedIndex: number | null,
): AnswerDiagnosis | null {
  const challenge = getChallengeById(challengeId)
  const analysis = analyzeChallenge(challengeId)
  if (!challenge || !analysis) return null

  const correct = selectedIndex === challenge.answerIndex
  const chosen = selectedIndex !== null ? analysis.options[selectedIndex] ?? null : null
  const correctOption = analysis.options[challenge.answerIndex]
  const errorType = correct ? null : chosen?.errorType ?? '概念混淆'
  const weak = correct ? [] : chosen ? inferWeakKnowledge(challenge, chosen, analysis.knowledgePoints) : analysis.knowledgePoints.slice(0, 1)

  const verdict = correct
    ? `答对了。你正确调用了「${analysis.knowledgePoints.map((k) => k.name).join('、') || '本题核心方法'}」。`
    : errorType === '审题偏差'
      ? '结论不匹配题目要求，更像是审题或条件遗漏，而不是完全不会。'
      : `选错项会在「${weak[0]?.name ?? '核心知识点'}」上得出与题意矛盾的结论。`

  const script = getTutorScript(challengeId)
  const probes = getOutcomeProbes(challengeId, correct ? 'correct' : 'wrong').slice(0, 2)
  const nextStage = !correct ? getStage(challengeId, 1) : script?.stages[script.stages.length - 1] ?? null

  return {
    challengeId,
    correct,
    selectedIndex,
    chosen,
    correctOption,
    errorType,
    weakKnowledgePoints: weak,
    verdict,
    suggestion: buildSuggestion(errorType, weak),
    probes,
    nextStage,
    explanation: explainFully(challengeId) ?? {
      requirement: analysis.requirement,
      options: analysis.options,
      knowledgeDetails: [],
      corePoints: analysis.corePoints,
      traps: analysis.traps,
      workedSteps: analysis.reasoningChain,
      verification: [],
      transferQuestion: '换一个条件，结论会变吗？',
    },
  }
}

// ==================== 完整讲解 ====================

/** 把一道题完全讲透：要求 → 选项 → 知识点 → 步骤 → 验证 → 变式 */
export function explainFully(challengeId: string): FullExplanation | null {
  const challenge = getChallengeById(challengeId)
  const analysis = analyzeChallenge(challengeId)
  if (!challenge || !analysis) return null

  const script = getTutorScript(challengeId)
  const validation = getValidation(challengeId)

  const knowledgeDetails = analysis.knowledgePoints
    .map((k) => getFullKnowledgePoint(k.id))
    .filter((k): k is NonNullable<typeof k> => Boolean(k))
    .map((k) => ({
      id: k.id,
      name: k.name,
      summary: k.summary,
      keyPoints: k.keyPoints,
      pitfalls: k.pitfalls,
    }))

  const workedSteps = script?.stages.map(
    (s) => `${s.index}. ${s.title}：${s.goal}${s.scaffold ? `（要点：${s.scaffold}）` : ''}`,
  ) ?? []

  return {
    requirement: analysis.requirement,
    options: analysis.options,
    knowledgeDetails,
    corePoints: analysis.corePoints,
    traps: analysis.traps,
    workedSteps,
    verification: validation?.checks ?? [],
    transferQuestion: script
      ? `如果题目条件改变（如浓度、温度、用量），用「${analysis.knowledgePoints[0]?.name ?? '本题方法'}」重推一遍，结论还成立吗？`
      : '改变一个条件，结论会变吗？',
  }
}

// ==================== 填空题分析 ====================

export interface FillAnalysisInput {
  prompt: string
  answerText: string
  userAnswer: string
  category?: ChemCategory
  explanation?: string
}

export interface FillAnalysisResult {
  type: 'fill'
  normalizedExpected: string
  normalizedUser: string
  correct: boolean
  knowledgePoints: KnowledgeRef[]
  reasoning: string
  suggestion: string
}

/** 化学答案规范化：去空格、全角转半角、统一箭头与上下标、去单位 */
function normalizeChemAnswer(input: string): string {
  return input
    .replace(/\s+/g, '')
    .replace(/[＝=]/g, '=')
    .replace(/→|⟶|—>/g, '→')
    .replace(/[（(]/g, '(')
    .replace(/[）)]/g, ')')
    .replace(/↑|↓/g, '')
    .replace(/[Ａ-Ｚａ-ｚ０-９]/g, (ch) => String.fromCharCode(ch.charCodeAt(0) - 0xfee0))
    .toLowerCase()
}

/**
 * 填空题分析：规范化比对 + 知识点匹配 + 讲透建议。
 * 适用于方程式、化学式、数值等答案；数值答案会做容差提示。
 */
export function analyzeFillAnswer(input: FillAnalysisInput): FillAnalysisResult {
  const normalizedExpected = normalizeChemAnswer(input.answerText)
  const normalizedUser = normalizeChemAnswer(input.userAnswer)
  const correct = normalizedExpected === normalizedUser

  const kps = input.category ? matchKnowledge(input.category, `${input.prompt} ${input.answerText}`) : []

  const reasoning = correct
    ? '你的答案在原子、电荷与化学式书写上与标准答案一致。'
    : normalizedExpected.includes('=')
      ? '答案不一致。先检查三处：反应物/产物是否写全、是否配平（电子/电荷/原子）、条件与状态符号是否遗漏。'
      : '答案不一致。先核对化学式下标、离子电荷与数值单位，再判断是否是表述差异而非错误。'

  return {
    type: 'fill',
    normalizedExpected,
    normalizedUser,
    correct,
    knowledgePoints: kps,
    reasoning,
    suggestion: correct
      ? '做一道同知识点的变式题，确认能稳定复现。'
      : kps[0]
        ? `回到「${kps[0].name}」，先写出关键守恒关系再作答。`
        : '先列出题干条件与所求，再按守恒关系逐步书写。',
  }
}

// ==================== 学习者画像 ====================

const EMPTY_ERROR_COUNTS: Record<ErrorType, number> = {
  概念混淆: 0,
  知识点缺口: 0,
  审题偏差: 0,
  计算失误: 0,
  书写不规范: 0,
  逻辑跳跃: 0,
}

/** 跨题汇总：智能判断薄弱知识点与错误类型 */
export function buildLearnerProfile(attempts: LearnerAttempt[]): LearnerProfile {
  const total = attempts.length
  const correct = attempts.filter((a) => a.correct).length
  const missMap = new Map<string, { id: string; name: string; missCount: number; categories: Set<ChemCategory> }>()
  const errorCounts = { ...EMPTY_ERROR_COUNTS }

  attempts
    .filter((a) => !a.correct)
    .forEach((a) => {
      const diag = diagnoseAnswer(a.challengeId, a.selectedIndex)
      if (!diag) return
      if (diag.errorType) errorCounts[diag.errorType] += 1
      diag.weakKnowledgePoints.forEach((kp) => {
        const challenge = getChallengeById(a.challengeId)
        const prev = missMap.get(kp.id) ?? {
          id: kp.id,
          name: kp.name,
          missCount: 0,
          categories: new Set<ChemCategory>(),
        }
        prev.missCount += 1
        if (challenge) prev.categories.add(challenge.category)
        missMap.set(kp.id, prev)
      })
    })

  const weakKnowledgePoints = [...missMap.values()]
    .sort((a, b) => b.missCount - a.missCount)
    .slice(0, 5)
    .map((v) => ({ id: v.id, name: v.name, missCount: v.missCount, categories: [...v.categories] }))

  const topError = (Object.entries(errorCounts) as Array<[ErrorType, number]>)
    .sort((a, b) => b[1] - a[1])
    .find(([, n]) => n > 0)?.[0]

  const recommendations: string[] = []
  if (weakKnowledgePoints.length > 0) {
    recommendations.push(`优先补「${weakKnowledgePoints[0].name}」，错题中出现 ${weakKnowledgePoints[0].missCount} 次。`)
  }
  if (topError) {
    const map: Record<ErrorType, string> = {
      概念混淆: '做辨析型练习：把相近概念两两对比，各写一条区别。',
      知识点缺口: '先看知识卡片的定义与核心要点，再做同类基础题。',
      审题偏差: '练习前先花 30 秒圈出"限定条件"，再动笔。',
      计算失误: '统一单位后再计算，算完用守恒关系反向验算。',
      书写不规范: '按"电子→电荷→原子"三守恒逐步书写方程式。',
      逻辑跳跃: '每一步判断后写一句依据，说不出依据就停下补知识。',
    }
    recommendations.push(map[topError])
  }
  if (recommendations.length === 0) recommendations.push('保持当前节奏，尝试更高难度的综合题。')

  return {
    total,
    correct,
    accuracy: total > 0 ? Math.round((correct / total) * 100) : 0,
    weakKnowledgePoints,
    errorTypeCounts: errorCounts,
    recommendations,
  }
}

// ==================== 与教学引擎的统一入口 ====================

/**
 * 一步到位：题目分析 + 作答诊断 + 下一步教学动作。
 * 供题目页直接调用，避免前端拼接多个引擎。
 */
export function brainStep(challengeId: string, selectedIndex: number | null): {
  analysis: ChallengeAnalysis
  diagnosis: AnswerDiagnosis
} | null {
  const analysis = analyzeChallenge(challengeId)
  const diagnosis = diagnoseAnswer(challengeId, selectedIndex)
  if (!analysis || !diagnosis) return null
  return { analysis, diagnosis }
}
