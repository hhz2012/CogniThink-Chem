import { View, Text, ScrollView } from '@tarojs/components'
import { useState, useEffect } from 'react'
import Taro, { useRouter } from '@tarojs/taro'
import ThoughtChip from '../../components/ThoughtChip'
import { chapters, getChallengeById } from '../../data/challenges'
import {
  loadProgress, markSolved, consumeInspiration, THOUGHT_META,
} from '../../utils/storage'
import { Challenge, GuideLevel, UserProgress } from '../../types/chem'
import styles from './index.module.scss'

type ResultState = 'pending' | 'correct' | 'wrong'

const LAYER_META = {
  Knowledge: { label: '知识点', icon: '📖', cls: 'knowledge' },
  Application: { label: '知识运用', icon: '🛠️', cls: 'application' },
  Ability: { label: '能力', icon: '🧠', cls: 'ability' },
}

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

export default function ChallengePage() {
  const router = useRouter()
  const id = router.params.id as string
  const challenge = getChallengeById(id)

  const [selected, setSelected] = useState<number | null>(null)
  const [result, setResult] = useState<ResultState>('pending')
  const [progress, setProgress] = useState<UserProgress>(loadProgress())
  const [messages, setMessages] = useState<ChatMsg[]>([])
  const [currentLevel, setCurrentLevel] = useState(0) // 0=未开始, 1..6=进行中, 7=已完成
  const [hintLevels, setHintLevels] = useState<Record<number, boolean>>({})
  const [showConservation, setShowConservation] = useState(false)

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

  const pushMsg = (msg: Omit<ChatMsg, 'id'>) => {
    setMessages((prev) => [...prev, { ...msg, id: prev.length }])
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
    const lv = challenge.guide[currentLevel - 1]
    if (!lv.hint || hintLevels[lv.level]) return
    const next = consumeInspiration(progress)
    if (!next) {
      Taro.showToast({ title: '灵感值不足，答对题目可获得', icon: 'none' })
      return
    }
    setProgress(next)
    setHintLevels((s) => ({ ...s, [lv.level]: true }))
    pushMsg({ role: 'student', kind: 'action', text: '💡 给我提示', level: lv.level })
    pushMsg({ role: 'tutor', kind: 'hint', text: lv.hint, level: lv.level })
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
    if (correct && !progress.solvedIds.includes(challenge.id)) {
      const next = markSolved(progress, challenge.id, challenge.thought)
      setProgress(next)
      Taro.showToast({ title: '答对了！+2 灵感值', icon: 'success' })
    } else if (!correct) {
      // 答错时自动开始第一步引导
      setTimeout(() => startGuide(1), 600)
      Taro.showToast({ title: '答错了，引导已开启', icon: 'none' })
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

        {/* 守恒校验（答完后展示） */}
        {result !== 'pending' && (
          <View className={styles.conservation}>
            <View className={styles.conservationHead} onClick={() => setShowConservation((v) => !v)}>
              <Text>🔍 守恒校验（自查）</Text>
              <Text>{showConservation ? '收起' : '展开'}</Text>
            </View>
            {showConservation && (
              <View className={styles.conservationBody}>
                <Text className={styles.conservationItem}>⚖️ 原子守恒：方程式两侧各元素原子数相等</Text>
                <Text className={styles.conservationItem}>⚡ 电子守恒：氧化反应失电子数 = 还原反应得电子数</Text>
                <Text className={styles.conservationItem}>🔋 电荷守恒：离子方程式两侧电荷总数相等</Text>
                <Text className={styles.conservationTip}>本题关键守恒：{thoughtMeta.label}。检查每一步是否满足对应守恒关系。</Text>
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
                {challenge.guide[currentLevel - 1].hint && !hintLevels[currentLevel] && (
                  <View className={styles.hintBtn} onClick={onHint}>💡 给我提示（-1）</View>
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
