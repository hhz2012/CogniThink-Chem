import { View, Text, ScrollView } from '@tarojs/components'
import { useState, useEffect } from 'react'
import Taro, { useRouter } from '@tarojs/taro'
import ThoughtChip from '../../components/ThoughtChip'
import { chapters, getChallengeById } from '../../data/challenges'
import {
  loadProgress, markSolved, markWrong, unmarkWrong, consumeInspiration, THOUGHT_META,
} from '../../utils/storage'
import { Challenge, GuideLevel, UserProgress, Difficulty } from '../../types/chem'
import { getTutorScript } from '../../engine/tutorEngine'
import { diagnoseAnswer, AnswerDiagnosis } from '../../engine/brainEngine'
import { getModelChecks } from '../../engine/solveModels'
import { getFullKnowledgePoint, KnowledgeRef } from '../../data/knowledge'
import SolveModelPanel, { MODEL_ACCENT } from '../../components/SolvePanels'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
import styles from './index.module.scss'

type ResultState = 'pending' | 'correct' | 'wrong'

/** 难度从易到难，用于题目页的难度切换导航 */
const DIFF_ORDER: Difficulty[] = ['基础', '进阶', '综合', '难题', '压轴']

const LAYER_META = {
  Knowledge: { label: '知识点', icon: '📖', cls: 'knowledge' },
  Application: { label: '知识运用', icon: '🛠️', cls: 'application' },
  Ability: { label: '能力', icon: '🧠', cls: 'ability' },
}

/** 三级提示按钮文案：hint → scaffold → reveal */
const HINT_LABELS = ['💡 给我提示（-1）', '🧩 再具体一点（-1）', '🔓 直接看关键（-1）']

type ChatKind = 'intro' | 'question' | 'hint' | 'knowledge' | 'action' | 'summary'

interface ChatMsg {
  id: number
  role: 'tutor' | 'student'
  kind: ChatKind
  text: string
  level: number
  layer?: GuideLevel['cognitiveLayer']
  knowledge?: { name: string; summary?: string; textbookRef?: string }
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

export default function ChallengePage() {
  const router = useRouter()
  const id = router.params.id as string
  const challenge = getChallengeById(id)

  const [selected, setSelected] = useState<number | null>(null)
  const [result, setResult] = useState<ResultState>('pending')
  const [progress, setProgress] = useState<UserProgress>(loadProgress())
  const [messages, setMessages] = useState<ChatMsg[]>([])
  const [currentLevel, setCurrentLevel] = useState(0) // 0=未开始, 1..6=进行中, 7=已完成
  const [hintLevels, setHintLevels] = useState<Record<number, number>>({}) // 每步已解锁的提示级数
  const [showConservation, setShowConservation] = useState(false)
  const [showModel, setShowModel] = useState(true) // 题型解题模型（思考骨架）折叠
  // 逐步诊断状态
  const [diagMsgs, setDiagMsgs] = useState<DiagMsg[]>([])
  const [diagStep, setDiagStep] = useState<DiagStep>('idle')
  const [diagKpId, setDiagKpId] = useState<string | null>(null)
  const [brainDiag, setBrainDiag] = useState<AnswerDiagnosis | null>(null)

  useEffect(() => {
    if (!challenge) {
      Taro.showToast({ title: '题目不存在', icon: 'none' })
      setTimeout(() => Taro.navigateBack(), 800)
    }
  }, [challenge])

  if (!challenge) return <View className={styles.page} />

  const chapter = chapters.find((c) => c.challenges.some((ch) => ch.id === challenge.id))
  const thoughtMeta = THOUGHT_META[challenge.thought]
  const correctLetter = String.fromCharCode(65 + challenge.answerIndex)

  // 难度导航：在本专项内切换到相邻难度（优先最近的一档）
  const diffIdx = DIFF_ORDER.indexOf(challenge.difficulty)
  const findNeighbor = (dir: -1 | 1) => {
    if (!chapter) return undefined
    const levels = dir < 0
      ? [...DIFF_ORDER.slice(0, diffIdx)].reverse()
      : DIFF_ORDER.slice(diffIdx + 1)
    for (const d of levels) {
      const target = chapter.challenges.find((c) => c.difficulty === d)
      if (target) return target
    }
    return undefined
  }
  const easier = findNeighbor(-1)
  const harder = findNeighbor(1)
  const goDifficulty = (cid: string) =>
    Taro.redirectTo({ url: `/pages/challenge/index?id=${cid}` })

  // 教学引擎：知识点、阶段、探问、验证
  const tutor = getTutorScript(challenge.id)
  const relatedKps = tutor?.knowledgePoints ?? []

  /** 当前阶段的提示链：hint → scaffold → reveal */
  const stageTips =
    tutor && currentLevel >= 1 && currentLevel <= 6
      ? [tutor.stages[currentLevel - 1]?.hint, tutor.stages[currentLevel - 1]?.scaffold, tutor.stages[currentLevel - 1]?.reveal]
          .filter((t): t is string => Boolean(t))
      : []
  const usedHintCount = hintLevels[currentLevel] ?? 0
  // 答对时只给 2 条延伸追问；答错不再堆信息，改走逐步诊断
  const outcomeProbes = result === 'correct' ? (tutor?.outcomes.correct ?? []).slice(0, 2) : []
  const lastDiag = diagMsgs[diagMsgs.length - 1]

  const pushMsg = (msg: Omit<ChatMsg, 'id'>) => {
    setMessages((prev) => [...prev, { ...msg, id: prev.length }])
  }

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
    setMessages([])
    setCurrentLevel(0)
    setHintLevels({})
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

  const startGuide = (fromLevel = 1) => {
    if (currentLevel > 0) return
    const lv = challenge.guide[fromLevel - 1]
    if (!lv) return
    pushMsg({ role: 'tutor', kind: 'intro', text: `第 ${lv.level} 步 · ${lv.title}`, level: lv.level, layer: lv.cognitiveLayer })
    pushMsg({ role: 'tutor', kind: 'question', text: lv.prompts.join('\n'), level: lv.level, layer: lv.cognitiveLayer, knowledge: lv.knowledgeCard })
    setCurrentLevel(lv.level)
  }

  const onHint = () => {
    if (currentLevel < 1 || currentLevel > 6) return
    if (usedHintCount >= stageTips.length) return
    const next = consumeInspiration(progress)
    if (!next) {
      Taro.showToast({ title: '灵感值不足，答对题目可获得', icon: 'none' })
      return
    }
    setProgress(next)
    setHintLevels((s) => ({ ...s, [currentLevel]: usedHintCount + 1 }))
    pushMsg({ role: 'student', kind: 'action', text: HINT_LABELS[usedHintCount] ?? '💡 再提示一下', level: currentLevel })
    pushMsg({ role: 'tutor', kind: 'hint', text: stageTips[usedHintCount], level: currentLevel })
  }

  const onNext = () => {
    if (currentLevel < 1 || currentLevel > 6) return
    const lv = challenge.guide[currentLevel - 1]
    pushMsg({ role: 'student', kind: 'action', text: currentLevel === 6 ? '明白了 ✓' : '明白了，继续 →', level: lv.level })
    const nextLv = challenge.guide[currentLevel]
    if (!nextLv) {
      pushMsg({ role: 'tutor', kind: 'summary', text: '🎉 6 步思维引导已完成！回顾整条解题路径，你已掌握本题的核心思维方法。', level: 6 })
      setCurrentLevel(7)
      return
    }
    pushMsg({ role: 'tutor', kind: 'intro', text: `第 ${nextLv.level} 步 · ${nextLv.title}`, level: nextLv.level, layer: nextLv.cognitiveLayer })
    pushMsg({ role: 'tutor', kind: 'question', text: nextLv.prompts.join('\n'), level: nextLv.level, layer: nextLv.cognitiveLayer, knowledge: nextLv.knowledgeCard })
    setCurrentLevel(nextLv.level)
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

  const guideDone = currentLevel > 6

  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        {/* 顶部信息栏 */}
        {chapter && (
          <View className={styles.metaBar} style={{ borderLeftColor: chapter.color }}>
            <View className={styles.metaLeft}>
              <Text className={styles.metaIcon}>{chapter.icon}</Text>
              <View className={styles.metaText}>
                <Text className={styles.metaChapter}>{chapter.name}</Text>
                <Text className={styles.metaId}>题目 {challenge.id} · {challenge.difficulty}</Text>
              </View>
            </View>
            <ThoughtChip thought={challenge.thought} size="sm" />
          </View>
        )}

        {/* 难度导航：更简单 / 更难 */}
        <View className={styles.diffNav}>
          <View
            className={`${styles.diffNavBtn} ${easier ? '' : styles.diffNavDisabled}`}
            onClick={() => easier && goDifficulty(easier.id)}
          >
            <Text className={styles.diffNavArrow}>←</Text>
            <Text className={styles.diffNavText}>
              更简单{easier ? ` · ${easier.difficulty}` : ''}
            </Text>
          </View>
          <View
            className={`${styles.diffNavBtn} ${harder ? '' : styles.diffNavDisabled}`}
            onClick={() => harder && goDifficulty(harder.id)}
          >
            <Text className={styles.diffNavText}>
              更难{harder ? ` · ${harder.difficulty}` : ''}
            </Text>
            <Text className={styles.diffNavArrow}>→</Text>
          </View>
        </View>

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
              <Text className={styles.kpTitle}>🧩 本题知识点</Text>
              <Text className={styles.kpNames}>
                {relatedKps.map((k) => `${k.id} ${k.name}`).join(' · ')}
              </Text>
            </View>
            <Text className={styles.kpArrow}>→</Text>
          </View>
        )}

        {/* 题干卡片 */}
        <View className={styles.problemCard}>
          <View className={styles.problemHead}>
            <View className={styles.problemTitleRow}>
              <Text className={styles.problemTitle}>{challenge.title}</Text>
              <Text className={styles.problemDiff}>{challenge.difficulty}</Text>
            </View>
          </View>
          <Text className={styles.problemText}>{challenge.problemText}</Text>
        </View>

        {/* 题型解题模型：思考骨架（答题前可见，不泄答案） */}
        <View className={styles.modelBox}>
          <View className={styles.modelBoxHead} onClick={() => setShowModel((v) => !v)}>
            <View className={styles.modelBoxTitle}>
              <ModelIcon name="lightbulb" size={18} color={MODEL_ACCENT} />
              <Text>这类题怎么想</Text>
            </View>
            <Text className={styles.modelBoxToggle}>{showModel ? '收起' : '展开'}</Text>
          </View>
          {showModel && (
            <View className={styles.modelBoxBody}>
              <SolveModelPanel challenge={challenge} mode="preview" />
            </View>
          )}
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
            <Text className={styles.resultTitle}>
              {result === 'correct' ? '🎉 回答正确！' : '💪 别灰心，答案是 ' + correctLetter}
            </Text>
            <Text className={styles.explanation}>{challenge.explanation}</Text>
            <View className={styles.thoughtReward}>
              <Text>获得思想碎片：</Text>
              <Text style={{ color: thoughtMeta.color }}>{thoughtMeta.icon} {thoughtMeta.label}</Text>
            </View>
            <View className={styles.thoughtExplain}>
              <Text className={styles.thoughtExplainTitle}>{thoughtMeta.icon} {thoughtMeta.label}思维</Text>
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
            <SolveModelPanel challenge={challenge} mode="full" />
          </View>
        )}

        {/* 逐步诊断：一次只问一个问题，确认后再讲解 */}
        {result === 'wrong' && diagMsgs.length > 0 && (
          <View className={styles.diagBox}>
            <Text className={styles.diagTitle}>🧭 一步一步找到你的问题</Text>
            <View className={styles.diagList}>
              {diagMsgs.map((m) => (
                <View
                  key={m.id}
                  className={`${styles.diagMsg} ${m.role === 'student' ? styles.diagStudent : ''}`}
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
                🔄 回到原题，再试一次
              </View>
            )}
          </View>
        )}

        {/* 答对后的延伸追问（只保留 2 条） */}
        {result !== 'pending' && outcomeProbes.length > 0 && (
          <View className={styles.reviewBox}>
            <Text className={styles.reviewTitle}>
              {result === 'correct' ? '🧠 再进一步：说出你的判断依据' : '🧭 先定位卡点，再补知识'}
            </Text>
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
              <Text>🔍 守恒校验（自查）</Text>
              <Text>{showConservation ? '收起' : '展开'}</Text>
            </View>
            {showConservation && (
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
            )}
          </View>
        )}

        {/* 伴思引导 - 对话式递进 */}
        <View className={styles.guideSection}>
          <View className={styles.guideHeader}>
            <Text className={styles.guideTitle}>🤔 伴思引导 · 对话式</Text>
            <Text className={styles.guideSub}>灵感值：{progress.inspirations}</Text>
          </View>
          <View className={styles.chatBox}>
            {messages.length === 0 ? (
              <View className={styles.chatEmpty} onClick={() => startGuide(1)}>
                <Text className={styles.chatEmptyIcon}>💬</Text>
                <Text className={styles.chatEmptyTitle}>点击开始伴思引导</Text>
                <Text className={styles.chatEmptySub}>跟着 AI 导师一步步思考，不直接给答案</Text>
              </View>
            ) : (
              <View className={styles.chatList}>
                {messages.map((m) => {
                  const isTutor = m.role === 'tutor'
                  return (
                    <View key={m.id} className={`${styles.msg} ${isTutor ? styles.tutor : styles.student}`}>
                      {isTutor && <View className={styles.avatar}>🤖</View>}
                      <View className={styles.bubbleWrap}>
                        {isTutor && m.kind === 'intro' && m.layer && (
                          <Text className={`${styles.layerTag} ${styles['lt-' + LAYER_META[m.layer].cls]}`}>
                            {LAYER_META[m.layer].icon} {LAYER_META[m.layer].label}
                          </Text>
                        )}
                        <View className={`${styles.bubble} ${isTutor ? styles.tutorBubble : styles.studentBubble} ${styles['bk-' + m.kind]}`}>
                          {m.kind === 'intro' && <Text className={styles.introText}>{m.text}</Text>}
                          {m.kind === 'question' && (
                            <>
                              <Text className={styles.questionText}>{m.text}</Text>
                              {m.knowledge && (
                                <View className={styles.knowledgeCard}>
                                  <View className={styles.kcardHead}>
                                    <Text className={styles.kcardName}>📖 {m.knowledge.name}</Text>
                                    {m.knowledge.textbookRef && (
                                      <Text className={styles.kcardRef}>{m.knowledge.textbookRef}</Text>
                                    )}
                                  </View>
                                  {m.knowledge.summary && (
                                    <Text className={styles.kcardSummary}>{m.knowledge.summary}</Text>
                                  )}
                                </View>
                              )}
                            </>
                          )}
                          {m.kind === 'hint' && <Text className={styles.hintText}>💡 {m.text}</Text>}
                          {m.kind === 'summary' && <Text className={styles.summaryText}>{m.text}</Text>}
                          {m.kind === 'action' && <Text className={styles.actionText}>{m.text}</Text>}
                        </View>
                      </View>
                      {!isTutor && <View className={styles.avatar}>🧑‍🎓</View>}
                    </View>
                  )
                })}
              </View>
            )}

            {/* 当前步骤的操作按钮 */}
            {currentLevel >= 1 && currentLevel <= 6 && (
              <View className={styles.chatActions}>
                {usedHintCount < stageTips.length && (
                  <View className={styles.hintBtn} onClick={onHint}>
                    {HINT_LABELS[usedHintCount] ?? '💡 再提示一下'}
                  </View>
                )}
                <View className={styles.nextBtn} onClick={onNext}>
                  {currentLevel === 6 ? '完成引导 ✓' : '继续下一步 →'}
                </View>
              </View>
            )}

            {/* 引导完成后展示完整解题路径回顾 */}
            {guideDone && (
              <View className={styles.pathRecap}>
                <Text className={styles.pathRecapTitle}>🧭 解题路径回顾</Text>
                {challenge.guide.map((lv) => (
                  <View key={lv.level} className={styles.pathItem}>
                    <Text className={styles.pathItemNum}>{lv.level}</Text>
                    <View className={styles.pathItemBody}>
                      <Text className={styles.pathItemTitle}>{lv.title}</Text>
                      {lv.hint && <Text className={styles.pathItemHint}>{lv.hint}</Text>}
                    </View>
                  </View>
                ))}
              </View>
            )}
          </View>
        </View>

        <View className={styles.bottomPad} />
      </ScrollView>
    </View>
  )
}
