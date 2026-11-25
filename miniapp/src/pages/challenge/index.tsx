import { View, Text, ScrollView } from '@tarojs/components'
import { useState, useEffect, useMemo } from 'react'
import Taro, { useRouter } from '@tarojs/taro'
import classnames from 'classnames'
import { chapters, getChallengeById } from '../../data/challenges'
import {
  loadProgress, markSolved, markWrong, unmarkWrong, THOUGHT_META,
} from '../../utils/storage'
import { UserProgress } from '../../types/chem'
import { getTutorScript } from '../../engine/tutorEngine'
import { diagnoseAnswer, AnswerDiagnosis } from '../../engine/brainEngine'
import { getSolveModel, getModelChecks, SolveModelStep } from '../../engine/solveModels'
import { narrateStep } from '../../engine/solveNarration'
import { getFullKnowledgePoint, KnowledgeRef } from '../../data/knowledge'
import SolveModelPanel, { MODEL_ACCENT, CATEGORY_ICON } from '../../components/SolvePanels'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
import ThoughtChip, { THOUGHT_ICON } from '../../components/ThoughtChip'
import ThoughtDagFlow from '../../components/ThoughtDagFlow'
import Collapse from '../../components/Collapse'
import { useSpeech } from '../../hooks/useSpeech'
import styles from './index.module.scss'

type ResultState = 'pending' | 'correct' | 'wrong'

const LAYER_META = {
  Knowledge: { label: '知识点', cls: 'knowledge' },
  Application: { label: '知识运用', cls: 'application' },
  Ability: { label: '能力', cls: 'ability' },
}

/** 两阶段模型分组标题（与 StepsView 保持一致）；仅当步骤带 stage 时渲染 */
const STAGE_LABEL: Record<'stage1' | 'stage2', string> = {
  stage1: '阶段一 · 主链路推导',
  stage2: '阶段二 · 选做推导',
}

/** 逐步诊断中的一条消息 */
interface DiagMsg {
  id: number
  role: 'tutor' | 'student'
  text: string
  tone?: 'ask' | 'tip' | 'teach'
  choices?: { label: string; value: string }[]
}

/** 诊断阶段：定位卡点 → 确认知识点 → 确认理解 → 回题重试 */
type DiagStep = 'idle' | 'locate' | 'check' | 'confirm' | 'done'

/** 朗读按钮：active 时高亮为「停止」态；阻止冒泡，避免触发外层卡片的展开/收起 */
function ReadAloud({ active, onClick }: { active: boolean; onClick: () => void }) {
  return (
    <View
      className={classnames(styles.readAloud, active && styles.readAloudActive)}
      onClick={(e) => {
        e.stopPropagation()
        onClick()
      }}
    >
      <ModelIcon
        name="volume"
        size={15}
        color={active ? '#ffffff' : MODEL_ACCENT}
        className={styles.readAloudIcon}
      />
      <Text className={styles.readAloudText}>{active ? '停止' : '朗读'}</Text>
    </View>
  )
}

/** 按句末标点把文本切成短句，返回每段在原文本中的 [start, end) 偏移 */
function splitSegments(text: string): { text: string; start: number; end: number }[] {
  const tokens = text.split(/([。！？!?；;，,、])/)
  const segs: { text: string; start: number; end: number }[] = []
  let cursor = 0
  let buf = ''
  let bufStart = 0
  const flush = () => {
    if (buf) {
      segs.push({ text: buf, start: bufStart, end: bufStart + buf.length })
      buf = ''
    }
  }
  for (let i = 0; i < tokens.length; i++) {
    const t = tokens[i]
    if (!t) continue
    if (buf === '') bufStart = cursor
    buf += t
    cursor += t.length
    // 捕获的标点位于奇数位，附在前段末尾作为段尾
    if (i % 2 === 1) flush()
  }
  flush()
  return segs
}

interface SpeakingTextProps {
  text: string
  /** 是否处于朗读态（仅本段对应 key 朗读时为 true） */
  active: boolean
  /** useSpeech 返回的当前朗读字符索引 */
  boundaryIndex: number
  /** 本段文本在完整朗读串中的起始偏移（用于跨段拼接的朗读对齐） */
  baseOffset?: number
  className?: string
}

/**
 * 可同步高亮的朗读文本：按标点切成短句，朗读时高亮当前句。
 * 不支持语音 / 未朗读时原样渲染，等价于普通 Text。
 */
function SpeakingText({ text, active, boundaryIndex, baseOffset = 0, className }: SpeakingTextProps) {
  const segments = useMemo(() => splitSegments(text), [text])
  return (
    <Text className={className}>
      {segments.map((seg, i) => {
        const on =
          active &&
          boundaryIndex >= baseOffset + seg.start &&
          boundaryIndex < baseOffset + seg.end
        return (
          <Text key={i} className={on ? styles.hlOn : undefined}>
            {seg.text}
          </Text>
        )
      })}
    </Text>
  )
}

export default function ChallengePage() {
  const router = useRouter()
  const id = router.params.id as string
  const challenge = getChallengeById(id)

  const [selected, setSelected] = useState<number | null>(null)
  const [result, setResult] = useState<ResultState>('pending')
  const [progress, setProgress] = useState<UserProgress>(loadProgress())
  const [showConservation, setShowConservation] = useState(false)
  // 逐步诊断状态
  const [diagMsgs, setDiagMsgs] = useState<DiagMsg[]>([])
  const [diagStep, setDiagStep] = useState<DiagStep>('idle')
  const [diagKpId, setDiagKpId] = useState<string | null>(null)
  const [brainDiag, setBrainDiag] = useState<AnswerDiagnosis | null>(null)
  // 每步「怎么分析」关键思考内容是否已揭示（默认隐藏，点提示展开，再点收起可重考）
  const [revealed, setRevealed] = useState<Record<number, boolean>>({})
  // 思考过程启发：阶段二（电子守恒配平）选做区是否展开（默认收起，不干扰选择题）
  const [openStage2Guide, setOpenStage2Guide] = useState(false)
  // 答题前「思路递进图」预览是否展开（默认收起，先看分步再看全景；不泄答案）
  const [showDagPreview, setShowDagPreview] = useState(false)
  const toggleReveal = (idx: number) => {
    stop()
    setRevealed((prev) => ({ ...prev, [idx]: !prev[idx] }))
  }
  // 语音朗读
  const { speakingKey, speak, stop, supported, boundaryCharIndex } = useSpeech()

  useEffect(() => {
    if (!challenge) {
      Taro.showToast({ title: '题目不存在', icon: 'none' })
      setTimeout(() => Taro.navigateBack(), 800)
    }
    // 题目切换时停止朗读，避免上一题语音残留
    return () => stop()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [challenge?.id])

  if (!challenge) return <View className={styles.page} />

  const chapter = chapters.find((c) => c.challenges.some((ch) => ch.id === challenge.id))
  const thoughtMeta = THOUGHT_META[challenge.thought]
  const correctLetter = String.fromCharCode(65 + challenge.answerIndex)
  const model = getSolveModel(challenge)
  // 思考过程启发分组：阶段一（主链路，默认展开引导）+ 阶段二（配平选做，折叠收起）
  const guideSteps = model.steps.filter((s) => s.stage !== 'stage2')
  const stage2GuideSteps = model.steps.filter((s) => s.stage === 'stage2')

  // 返回首页（清空页面栈，直达首页）
  const onBackHome = () => {
    stop()
    Taro.reLaunch({ url: '/pages/home/index' })
  }

  // 朗读：同一段再次点击则停止；不支持时提示
  const toggleSpeak = (key: string, text: string) => {
    if (speakingKey === key) {
      stop()
      return
    }
    const ok = speak(key, text)
    if (!ok) {
      Taro.showToast({
        title: supported ? '朗读失败，请重试' : '当前环境不支持语音朗读',
        icon: 'none',
      })
    }
  }
  const readText = `${challenge.title}。${challenge.problemText}`
  const overviewText =
    `${model.name}。共 ${model.steps.length} 步：` +
    model.steps.map((s) => `第 ${s.index} 步，${s.title}：${s.goal}`).join('；')
  // 未揭示时只读 goal（不泄答案），揭示后读全量；须与 stepOffsets 偏移模板保持一致
  // analyzeText 为结合本题真值的实例化讲解（无数据时回退通用 analyze），与渲染保持同源
  const stepSpeakText = (s: SolveModelStep, isRevealed: boolean, analyzeText: string) =>
    isRevealed
      ? `第 ${s.index} 步，${s.title}。这一步分析什么：${s.goal}。怎么分析：${analyzeText}`
      : `第 ${s.index} 步，${s.title}。这一步分析什么：${s.goal}`
  // 每步朗读高亮偏移：须与 stepSpeakText 的拼接模板保持一致
  const stepOffsets = (s: SolveModelStep) => {
    const head = `第 ${s.index} 步，${s.title}。这一步分析什么：`
    return {
      key: `step-${s.index}`,
      goalOffset: head.length,
      analyzeOffset: head.length + s.goal.length + '。怎么分析：'.length,
    }
  }
  const readBodyOffset = challenge.title.length + 1 // 题干正文在 readText 中的起点（标题 + 句号）

  // 教学引擎：知识点、阶段、探问、验证
  const tutor = getTutorScript(challenge.id)
  const relatedKps = tutor?.knowledgePoints ?? []

  // 答对时只给 2 条延伸追问；答错不再堆信息，改走逐步诊断
  const outcomeProbes = result === 'correct' ? (tutor?.outcomes.correct ?? []).slice(0, 2) : []
  const lastDiag = diagMsgs[diagMsgs.length - 1]

  // ==================== 逐步诊断（答错后） ====================
  const pushDiag = (m: Omit<DiagMsg, 'id'>) =>
    setDiagMsgs((prev) => [...prev, { ...m, id: prev.length }])

  const finishDiagnosis = () => {
    if (brainDiag?.suggestion) {
      pushDiag({ role: 'tutor', tone: 'tip', text: `下一步：${brainDiag.suggestion}` })
    }
    pushDiag({ role: 'tutor', tone: 'tip', text: '现在回到原题，用刚才的思路重新选一次。' })
    setDiagStep('done')
  }

  /** 只问一个最小确认题，判断该知识点会不会 */
  const askKnowledgeCheck = (target: KnowledgeRef) => {
    const full = getFullKnowledgePoint(target.id)
    if (!full) {
      pushDiag({ role: 'tutor', tone: 'tip', text: '这个知识点暂无自测题，我们回到解题步骤：先把条件逐条列出来。' })
      finishDiagnosis()
      return
    }
    setDiagKpId(target.id)
    setDiagStep('check')
    pushDiag({
      role: 'tutor',
      tone: 'ask',
      text: `先只确认一个问题（${target.name}）：${full.check.question}`,
      choices: full.check.options.map((o, i) => ({
        label: `${String.fromCharCode(65 + i)}. ${o}`,
        value: `check:${i}`,
      })),
    })
  }

  const startDiagnosis = () => {
    setDiagMsgs([])
    setDiagKpId(null)
    setDiagStep('locate')
    pushDiag({
      role: 'tutor',
      tone: 'ask',
      text: '先不看解析。你自己感觉，这题最卡的是哪一步？',
      choices: [
        { label: '读不懂题 / 找不到条件', value: 'read' },
        { label: '知识点没记牢', value: 'knowledge' },
        { label: '知道知识点但不会用', value: 'method' },
        { label: '计算或书写出错', value: 'calc' },
        { label: '说不上来，就是选错了', value: 'unsure' },
      ],
    })
  }

  const retryChallenge = () => {
    setSelected(null)
    setResult('pending')
    setDiagMsgs([])
    setDiagStep('idle')
    setDiagKpId(null)
    setBrainDiag(null)
    setShowConservation(false)
  }

  const onDiagChoice = (value: string, label: string) => {
    const kps = tutor?.knowledgePoints ?? []
    const firstStage = tutor?.stages[0]
    pushDiag({ role: 'student', text: label })

    // 1) 定位卡点：一次只问一个问题
    if (diagStep === 'locate') {
      if (value === 'read') {
        pushDiag({
          role: 'tutor',
          tone: 'ask',
          text: '读题只做一件事：把"已知条件"和"要求什么"分开。你现在能说出本题要求的结果吗？',
          choices: [
            { label: '能，我找到了', value: 'read-ok' },
            { label: '不能，题干太绕', value: 'read-stuck' },
          ],
        })
        return
      }
      if (value === 'calc') {
        pushDiag({
          role: 'tutor',
          tone: 'ask',
          text: '先分清是"不会列式"还是"算错"：这题需要用的关系式，你能写出来吗？',
          choices: [
            { label: '能写出关系式', value: 'calc-formula' },
            { label: '不知道用哪个关系式', value: 'calc-concept' },
          ],
        })
        return
      }
      const weak = brainDiag?.weakKnowledgePoints ?? []
      if (value === 'method') {
        const target =
          weak.find((k) => k.layer === 'Ability') ??
          [...kps].reverse().find((k) => k.layer === 'Ability') ??
          kps[kps.length - 1]
        if (target) askKnowledgeCheck(target)
        else finishDiagnosis()
        return
      }
      // knowledge / unsure：优先使用智能诊断定位的薄弱知识点
      const target = weak[0] ?? kps[0]
      if (target) askKnowledgeCheck(target)
      else finishDiagnosis()
      return
    }

    // 2) 知识点最小确认
    if (diagStep === 'check' && value.startsWith('check:')) {
      const idx = Number(value.slice(6))
      const full = diagKpId ? getFullKnowledgePoint(diagKpId) : null
      if (!full) return
      if (idx === full.check.answerIndex) {
        pushDiag({ role: 'tutor', tone: 'tip', text: `✓ ${full.name} 你是懂的，那问题更可能在解题步骤上。` })
        if (firstStage) {
          pushDiag({
            role: 'tutor',
            tone: 'ask',
            text: `回到第 1 步「${firstStage.title}」：${firstStage.probes[0] ?? '这一步你会怎么做？'}`,
          })
        }
        finishDiagnosis()
      } else {
        pushDiag({ role: 'tutor', tone: 'teach', text: `找到问题了：${full.name}。${full.summary}` })
        pushDiag({ role: 'tutor', tone: 'tip', text: `先记这一条：${full.keyPoints[0]}` })
        pushDiag({
          role: 'tutor',
          tone: 'ask',
          text: '这一条能理解吗？',
          choices: [
            { label: '能，继续', value: 'understood' },
            { label: '还是不太懂', value: 'notyet' },
          ],
        })
        setDiagStep('confirm')
      }
      return
    }

    // 3) 学生确认后再给讲解
    if (diagStep === 'confirm') {
      const full = diagKpId ? getFullKnowledgePoint(diagKpId) : null
      if (!full) return
      if (value === 'understood') {
        pushDiag({ role: 'tutor', tone: 'teach', text: full.teach.steps.join('\n') })
        pushDiag({ role: 'tutor', tone: 'tip', text: `例：${full.teach.example}` })
      } else {
        pushDiag({ role: 'tutor', tone: 'teach', text: `换个说法：${full.teach.steps[0]}` })
        pushDiag({ role: 'tutor', tone: 'tip', text: `再看这个例子：${full.teach.example}` })
      }
      finishDiagnosis()
      return
    }

    // 4) 读题 / 计算分支
    if (value === 'read-ok') {
      pushDiag({
        role: 'tutor',
        tone: 'ask',
        text: firstStage
          ? `很好。第 1 步「${firstStage.title}」：${firstStage.probes[0] ?? '按这一步继续推进。'}`
          : '很好，继续按步骤推进。',
      })
      finishDiagnosis()
      return
    }
    if (value === 'read-stuck') {
      pushDiag({
        role: 'tutor',
        tone: 'tip',
        text: firstStage?.hint ?? '题干先抓两样：每个数字代表什么、最后问的是什么量。',
      })
      finishDiagnosis()
      return
    }
    if (value === 'calc-formula') {
      pushDiag({
        role: 'tutor',
        tone: 'tip',
        text: '关系式会写，那问题在细节。检查三处：单位是否统一、系数是否乘对、结果是否按要求取整。',
      })
      finishDiagnosis()
      return
    }
    if (value === 'calc-concept') {
      const target = [...kps].reverse().find((k) => k.layer === 'Ability') ?? kps[kps.length - 1]
      if (target) askKnowledgeCheck(target)
      else finishDiagnosis()
      return
    }
    if (value === 'retry') {
      retryChallenge()
    }
  }

  const submit = () => {
    if (selected === null) {
      Taro.showToast({ title: '请先选择答案', icon: 'none' })
      return
    }
    const correct = selected === challenge.answerIndex
    setResult(correct ? 'correct' : 'wrong')
    if (correct) {
      let next = progress
      if (!progress.solvedIds.includes(challenge.id)) {
        next = markSolved(progress, challenge.id, challenge.thought)
        Taro.showToast({ title: '答对了！+2 灵感值', icon: 'success' })
      }
      // 重做做对：从错题本移除
      if (next.wrongIds.includes(challenge.id)) {
        next = unmarkWrong(next, challenge.id)
      }
      setProgress(next)
    } else {
      // 答错：入错题本，再进入逐步诊断
      setProgress(markWrong(progress, challenge.id))
      setBrainDiag(diagnoseAnswer(challenge.id, selected))
      setTimeout(() => startDiagnosis(), 400)
      Taro.showToast({ title: '已加入错题本，一步步找原因', icon: 'none' })
    }
  }

  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        {/* 返回首页 */}
        <View className={styles.homeBar}>
          <View className={styles.homeBack} onClick={onBackHome}>
            <ModelIcon name="home" size={16} color={MODEL_ACCENT} />
            <Text className={styles.homeBackText}>返回首页</Text>
          </View>
        </View>

        {/* 顶部信息栏 */}
        {chapter && (
          <View className={styles.metaBar} style={{ borderLeftColor: chapter.color }}>
            <View className={styles.metaLeft}>
              <ModelIcon name={CATEGORY_ICON[chapter.category] ?? 'zap'} size={22} color={chapter.color} className={styles.metaIcon} />
              <View className={styles.metaText}>
                <Text className={styles.metaChapter}>{chapter.name}</Text>
                <Text className={styles.metaId}>题目 {challenge.id} · {challenge.difficulty}</Text>
              </View>
            </View>
            <ThoughtChip thought={challenge.thought} size="sm" />
          </View>
        )}

        {/* 知识体系入口：展示引擎解析出的本题知识点 */}
        {relatedKps.length > 0 && (
          <View
            className={styles.kpBar}
            onClick={() => {
              if (chapter?.category) {
                Taro.navigateTo({ url: `/pages/knowledge/index?category=${chapter.category}` })
              }
            }}
          >
            <View className={styles.kpLeft}>
              <View className={styles.kpTitleRow}>
                <ModelIcon name="lightbulb" size={13} color={MODEL_ACCENT} />
                <Text className={styles.kpTitle}>本题知识点</Text>
              </View>
              <Text className={styles.kpNames}>
                {relatedKps.map((k) => `${k.id} ${k.name}`).join(' · ')}
              </Text>
            </View>
            <Text className={styles.kpArrow}>→</Text>
          </View>
        )}

        {/* 一、读题：题干 + 语音朗读 */}
        <View className={styles.sectionLabel}>一、读题</View>
        <View className={styles.problemCard}>
          <View className={styles.problemHead}>
            <View className={styles.problemTitleRow}>
              <Text className={styles.problemTitle}>{challenge.title}</Text>
              <Text className={styles.problemDiff}>{challenge.difficulty}</Text>
            </View>
            <ReadAloud
              active={speakingKey === 'read'}
              onClick={() => toggleSpeak('read', readText)}
            />
          </View>
          <SpeakingText
            text={challenge.problemText}
            active={speakingKey === 'read'}
            boundaryIndex={boundaryCharIndex}
            baseOffset={readBodyOffset}
            className={styles.problemText}
          />
          {challenge.questionTarget && (
            <View className={styles.targetRow}>
              <ModelIcon name="lightbulb" size={14} color={MODEL_ACCENT} />
              <Text className={styles.targetLabel}>问题：</Text>
              <Text className={styles.targetText}>{challenge.questionTarget}</Text>
            </View>
          )}
          {challenge.problemConditions && challenge.problemConditions.length > 0 && (
            <View className={styles.condBox}>
              <View className={styles.condTitle}>
                <ModelIcon name="check-circle" size={14} color={MODEL_ACCENT} />
                <Text>题目条件</Text>
              </View>
              {challenge.problemConditions.map((c, i) => (
                <Text key={i} className={styles.condItem}>• {c}</Text>
              ))}
            </View>
          )}
        </View>

        {/* 二、思考过程启发：每步先想再看，点提示揭示关键思考内容 */}
        <View className={styles.sectionLabelRow}>
          <View className={styles.sectionLabel}>二、思考过程启发 · {model.name}</View>
          <ReadAloud
            active={speakingKey === 'overview'}
            onClick={() => toggleSpeak('overview', overviewText)}
          />
        </View>

        {/* 思路递进图（答题前预览：题型通用 DAG 骨架，不读 solveData、不泄答案；默认收起） */}
        <View
          className={styles.dagPreviewToggle}
          onClick={() => setShowDagPreview((v) => !v)}
        >
          <ModelIcon name="link" size={15} color={MODEL_ACCENT} />
          <Text className={styles.dagPreviewToggleText}>
            {showDagPreview ? '收起思路递进图' : '展开思路递进图（这类题怎么想）'}
          </Text>
          <Text className={styles.stage2Arrow}>{showDagPreview ? '▲' : '▼'}</Text>
        </View>
        <Collapse open={showDagPreview}>
          <View className={styles.dagPreviewBody}>
            <ThoughtDagFlow challenge={challenge} mode="preview" />
          </View>
        </Collapse>

        <View className={styles.stepSections}>
          {/* 阶段一（主链路）：每步默认展开，先想再点「提示」看实例化讲解 */}
          {guideSteps.map((s, i) => {
            const layerMeta = LAYER_META[s.layer]
            const isRevealed = !!revealed[s.index]
            const analyzeText = narrateStep(s, challenge) ?? s.analyze
            const off = stepOffsets(s)
            const prevStage = i > 0 ? guideSteps[i - 1].stage : undefined
            const showStageHead = !!s.stage && s.stage !== prevStage
            return (
              <View key={s.index}>
                {showStageHead && s.stage && (
                  <View
                    className={styles.stepStageHead}
                    style={{ borderLeftColor: MODEL_ACCENT, color: MODEL_ACCENT }}
                  >
                    <Text>{STAGE_LABEL[s.stage]}</Text>
                  </View>
                )}
                <View className={styles.stepSection}>
                  <View className={classnames(styles.stepSectionHead, styles.stepSectionHeadOpen)}>
                    <View className={styles.stepSectionIndex}>{s.index}</View>
                    <View className={styles.stepSectionMain}>
                      <Text className={styles.stepSectionTitle}>{s.title}</Text>
                      <Text className={styles.stepSectionLayer}>{layerMeta.label}</Text>
                    </View>
                  </View>
                  <View className={styles.stepSectionBody}>
                    <View className={styles.stepSectionBodyHead}>
                      <View className={styles.stepSectionBodyTitle}>
                        <Text className={styles.stepSectionNo}>第 {s.index} 步</Text>
                        <Text className={styles.stepSectionName}>{s.title}</Text>
                      </View>
                      <ReadAloud
                        active={speakingKey === off.key}
                        onClick={() => toggleSpeak(off.key, stepSpeakText(s, isRevealed, analyzeText))}
                      />
                    </View>
                    <View className={styles.analyzeBlock}>
                      <Text className={styles.analyzeLabel}>这一步分析什么</Text>
                      <SpeakingText
                        text={s.goal}
                        active={speakingKey === off.key}
                        boundaryIndex={boundaryCharIndex}
                        baseOffset={off.goalOffset}
                        className={styles.analyzeText}
                      />
                    </View>
                    <View className={styles.analyzeBlock}>
                      <View className={styles.analyzeLabelRow}>
                        <Text className={styles.analyzeLabel}>怎么分析</Text>
                        <View
                          className={styles.hintBtn}
                          onClick={() => toggleReveal(s.index)}
                        >
                          <Text className={styles.hintBtnText}>
                            {isRevealed ? '收起' : '提示'}
                          </Text>
                        </View>
                      </View>
                      {isRevealed ? (
                        <SpeakingText
                          text={analyzeText}
                          active={speakingKey === off.key}
                          boundaryIndex={boundaryCharIndex}
                          baseOffset={off.analyzeOffset}
                          className={styles.analyzeText}
                        />
                      ) : (
                        <View className={styles.hintPrompt}>
                          <View className={styles.hintPromptInner}>
                            <ModelIcon name="lightbulb" size={14} color={MODEL_ACCENT} />
                            <Text className={styles.hintPromptText}>
                              先自己想一想：这一步你会怎么分析？点「提示」查看。
                            </Text>
                          </View>
                        </View>
                      )}
                    </View>
                  </View>
                </View>
              </View>
            )
          })}

          {/* 阶段二（电子守恒配平）：选做，默认折叠收起，不干扰选择题 */}
          {stage2GuideSteps.length > 0 && (
            <View
              className={styles.stage2Toggle}
              onClick={() => setOpenStage2Guide((v) => !v)}
            >
              <ModelIcon name="zap" size={15} color={MODEL_ACCENT} />
              <Text className={styles.stage2ToggleText}>
                {openStage2Guide ? '收起选做推导（选做）' : '展开选做推导（选做）'}
              </Text>
              <Text className={styles.stage2Arrow}>{openStage2Guide ? '▲' : '▼'}</Text>
            </View>
          )}
          <Collapse open={openStage2Guide}>
            <View>
              {stage2GuideSteps.map((s) => {
                const layerMeta = LAYER_META[s.layer]
                return (
                  <View key={s.index} className={styles.stepSection}>
                    <View className={classnames(styles.stepSectionHead, styles.stepSectionHeadOpen)}>
                      <View className={styles.stepSectionIndex}>{s.index}</View>
                      <View className={styles.stepSectionMain}>
                        <Text className={styles.stepSectionTitle}>{s.title}</Text>
                        <Text className={styles.stepSectionLayer}>{layerMeta.label}</Text>
                      </View>
                    </View>
                    <View className={styles.stepSectionBody}>
                      <View className={styles.analyzeBlock}>
                        <Text className={styles.analyzeLabel}>这一步分析什么</Text>
                        <Text className={styles.analyzeText}>{s.goal}</Text>
                      </View>
                    </View>
                  </View>
                )
              })}
            </View>
          </Collapse>
        </View>

        {/* 选项 */}
        <View className={styles.optionList}>
          {challenge.options.map((opt, idx) => {
            const isSelected = selected === idx
            const showCorrect = result !== 'pending' && idx === challenge.answerIndex
            const showWrong = result === 'wrong' && isSelected
            return (
              <View
                key={idx}
                className={`${styles.option} ${isSelected ? styles.selected : ''} ${showCorrect ? styles.correct : ''} ${showWrong ? styles.wrongOpt : ''}`}
                onClick={() => result === 'pending' && setSelected(idx)}
              >
                <View className={styles.optionCircle}>{String.fromCharCode(65 + idx)}</View>
                <Text className={styles.optionText}>{opt}</Text>
                {showCorrect && <Text className={styles.optionMark}>✓</Text>}
                {showWrong && <Text className={styles.optionMark}>✗</Text>}
              </View>
            )
          })}
        </View>

        {/* 提交 / 结果 */}
        {result === 'pending' ? (
          <View className={styles.submitBtn} onClick={submit}>提交答案</View>
        ) : (
          <View className={styles.resultBox} style={{ borderColor: result === 'correct' ? '#16a34a' : '#dc2626' }}>
            <View className={styles.resultTitleRow}>
              <ModelIcon
                name={result === 'correct' ? 'check-circle' : 'refresh'}
                size={20}
                color={result === 'correct' ? '#16a34a' : '#dc2626'}
              />
              <Text className={styles.resultTitle}>
                {result === 'correct' ? '回答正确！' : '别灰心，答案是 ' + correctLetter}
              </Text>
            </View>
            <Text className={styles.explanation}>{challenge.explanation}</Text>
            <View className={styles.thoughtReward}>
              <Text>获得思想碎片：</Text>
              <ModelIcon name={THOUGHT_ICON[challenge.thought]} size={15} color={thoughtMeta.color} />
              <Text style={{ color: thoughtMeta.color }}>{thoughtMeta.label}</Text>
            </View>
            <View className={styles.thoughtExplain}>
              <Text className={styles.thoughtExplainTitle}>{thoughtMeta.label}思维</Text>
              <Text className={styles.thoughtExplainText}>{thoughtMeta.desc}</Text>
            </View>
          </View>
        )}

        {/* 完整解题模型（答题后展示题型专属可视化） */}
        {result !== 'pending' && (
          <View className={styles.solveModelFull}>
            <View className={styles.solveModelFullTitle}>
              <ModelIcon name="layout" size={18} color={MODEL_ACCENT} />
              <Text>完整解题模型</Text>
            </View>
            <View className={styles.dagFull}>
              <ThoughtDagFlow challenge={challenge} mode="full" />
            </View>
            <SolveModelPanel challenge={challenge} mode="full" />
          </View>
        )}

        {/* 逐步诊断：一次只问一个问题，确认后再讲解 */}
        {result === 'wrong' && diagMsgs.length > 0 && (
          <View className={styles.diagBox}>
            <View className={styles.diagTitleRow}>
              <ModelIcon name="lightbulb" size={16} color={MODEL_ACCENT} />
              <Text className={styles.diagTitle}>一步一步找到你的问题</Text>
            </View>
            <View className={styles.diagList}>
              {diagMsgs.map((m) => (
                <View
                  key={m.id}
                  className={`${styles.diagMsg} ${m.role === 'student' ? styles.diagStudent : ''}`}
                  style={{ animationDelay: `${m.id * 0.06}s` }}
                >
                  <View
                    className={`${styles.diagBubble} ${
                      m.role === 'student' ? styles.diagStuBubble : styles.diagTutorBubble
                    }`}
                  >
                    <Text className={styles.diagText}>{m.text}</Text>
                  </View>
                </View>
              ))}
            </View>
            {lastDiag?.choices && diagStep !== 'done' && (
              <View className={styles.diagChoices}>
                {lastDiag.choices.map((c) => (
                  <View
                    key={c.value}
                    className={styles.diagChoice}
                    onClick={() => onDiagChoice(c.value, c.label)}
                  >
                    <Text className={styles.diagChoiceText}>{c.label}</Text>
                  </View>
                ))}
              </View>
            )}
            {diagStep === 'done' && (
              <View className={styles.diagRetry} onClick={retryChallenge}>
                <View className={styles.diagRetryInner}>
                  <ModelIcon name="refresh" size={15} color="#fff" />
                  <Text>回到原题，再试一次</Text>
                </View>
              </View>
            )}
          </View>
        )}

        {/* 答对后的延伸追问（只保留 2 条） */}
        {result !== 'pending' && outcomeProbes.length > 0 && (
          <View className={styles.reviewBox}>
            <View className={styles.reviewTitleRow}>
              <ModelIcon name={result === 'correct' ? 'lightbulb' : 'layout'} size={16} color="#075985" />
              <Text className={styles.reviewTitle}>
                {result === 'correct' ? '再进一步：说出你的判断依据' : '先定位卡点，再补知识'}
              </Text>
            </View>
            {outcomeProbes.map((p) => (
              <View key={p.id} className={styles.reviewItem}>
                <Text className={styles.reviewQ}>{p.question}</Text>
                <Text className={styles.reviewMeta}>{p.purpose} · 期望：{p.expect}</Text>
              </View>
            ))}
          </View>
        )}

        {/* 守恒校验（答完后展示） */}
        {result !== 'pending' && (
          <View className={styles.conservation}>
            <View className={styles.conservationHead} onClick={() => setShowConservation((v) => !v)}>
              <View className={styles.conservationHeadLeft}>
                <ModelIcon name="check-circle" size={16} color={MODEL_ACCENT} />
                <Text>守恒校验（自查）</Text>
              </View>
              <Text>{showConservation ? '收起' : '展开'}</Text>
            </View>
            <Collapse open={showConservation}>
              <View className={styles.conservationBody}>
                {tutor?.validation.checks.map((c) => (
                  <Text key={c} className={styles.conservationItem}>✓ {c}</Text>
                ))}
                {tutor && tutor.validation.errorTraps.length > 0 && (
                  <Text className={styles.conservationTip}>常见错误落点：{tutor.validation.errorTraps.join('；')}</Text>
                )}
                <Text className={styles.conservationSubHead}>题型专属校验</Text>
                {getModelChecks(challenge).map((c) => (
                  <Text key={c} className={styles.conservationItem}>✓ {c}</Text>
                ))}
                <Text className={styles.conservationTip}>本题关键思维：{thoughtMeta.label}。逐条核对上述检查项再确认答案。</Text>
              </View>
            </Collapse>
          </View>
        )}

        <View className={styles.bottomPad} />
      </ScrollView>
    </View>
  )
}
