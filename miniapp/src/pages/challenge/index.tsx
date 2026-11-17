import { View, Text, ScrollView } from '@tarojs/components'
import { useState, useEffect } from 'react'
import Taro, { useRouter } from '@tarojs/taro'
import ThoughtChip from '../../components/ThoughtChip'
import { getChallengeById } from '../../data/challenges'
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

export default function ChallengePage() {
  const router = useRouter()
  const id = router.params.id as string
  const challenge = getChallengeById(id)

  const [selected, setSelected] = useState<number | null>(null)
  const [result, setResult] = useState<ResultState>('pending')
  const [progress, setProgress] = useState<UserProgress>(loadProgress())
  const [revealedLevels, setRevealedLevels] = useState<Record<number, boolean>>({})
  const [hintLevels, setHintLevels] = useState<Record<number, boolean>>({})
  const [showConservation, setShowConservation] = useState(false)

  useEffect(() => {
    if (!challenge) {
      Taro.showToast({ title: '题目不存在', icon: 'none' })
      setTimeout(() => Taro.navigateBack(), 800)
    }
  }, [challenge])

  if (!challenge) return <View className={styles.page} />

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
      Taro.showToast({ title: '再想想，可以点下方「伴思引导」', icon: 'none' })
    }
  }

  const revealLevel = (lv: GuideLevel) => {
    // 先展开本层（反问问题）
    setRevealedLevels((s) => ({ ...s, [lv.level]: true }))
  }

  const askHint = (lv: GuideLevel) => {
    // 提示需要消耗 1 灵感值
    if (!lv.hint) return
    const next = consumeInspiration(progress)
    if (!next) {
      Taro.showToast({ title: '灵感值不足，答对题目可获得', icon: 'none' })
      return
    }
    setProgress(next)
    setHintLevels((s) => ({ ...s, [lv.level]: true }))
    Taro.showToast({ title: '-1 灵感值', icon: 'none' })
  }

  const thoughtMeta = THOUGHT_META[challenge.thought]

  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        {/* 题干卡片 */}
        <View className={styles.problemCard}>
          <View className={styles.problemHead}>
            <Text className={styles.problemTitle}>{challenge.title}</Text>
            <ThoughtChip thought={challenge.thought} size="sm" />
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

        {/* 提交按钮 */}
        {result === 'pending' ? (
          <View className={styles.submitBtn} onClick={submit}>提交答案</View>
        ) : (
          <View className={styles.resultBox} style={{ borderColor: result === 'correct' ? '#16a34a' : '#dc2626' }}>
            <Text className={styles.resultTitle}>
              {result === 'correct' ? '🎉 回答正确！' : '💪 别灰心，看看解析'}
            </Text>
            <Text className={styles.explanation}>{challenge.explanation}</Text>
            <View className={styles.thoughtReward}>
              <Text>获得思想碎片：</Text>
              <Text style={{ color: thoughtMeta.color }}>{thoughtMeta.icon} {thoughtMeta.label}</Text>
            </View>
          </View>
        )}

        {/* 守恒校验 */}
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

        {/* 伴思引导 */}
        <View className={styles.guideSection}>
          <View className={styles.guideHeader}>
            <Text className={styles.guideTitle}>🤔 伴思引导（逐级解锁）</Text>
            <Text className={styles.guideSub}>灵感值：{progress.inspirations}</Text>
          </View>
          <View className={styles.guideLegend}>
            <View className={`${styles.legend} ly-knowledge`}><Text>📖 知识点</Text></View>
            <View className={`${styles.legend} ly-application`}><Text>🛠️ 知识运用</Text></View>
            <View className={`${styles.legend} ly-ability`}><Text>🧠 能力</Text></View>
          </View>
          <View className={styles.guideList}>
            {challenge.guide.map((lv) => {
              const lm = LAYER_META[lv.cognitiveLayer]
              const isRevealed = revealedLevels[lv.level]
              const isHintShown = hintLevels[lv.level]
              return (
                <View key={lv.level} className={`${styles.guideLevel} ${styles['ly-' + lm.cls]}`}>
                  <View className={styles.guideLevelHead} onClick={() => revealLevel(lv)}>
                    <Text className={styles.levelNo}>{lv.level}</Text>
                    <Text className={styles.levelTitle}>{lv.title}</Text>
                    <Text className={`${styles.layerBadge} ${styles['lb-' + lm.cls]}`}>{lm.icon} {lm.label}</Text>
                  </View>
                  {isRevealed && (
                    <View className={styles.guideBody}>
                      <View className={styles.promptList}>
                        {lv.prompts.map((p, i) => (
                          <Text key={i} className={styles.prompt}>• {p}</Text>
                        ))}
                      </View>
                      {lv.knowledgeCard && (
                        <View className={styles.knowledgeCard}>
                          <View className={styles.kcardHead}>
                            <Text className={styles.kcardName}>📖 {lv.knowledgeCard.name}</Text>
                            {lv.knowledgeCard.textbookRef && (
                              <Text className={styles.kcardRef}>{lv.knowledgeCard.textbookRef}</Text>
                            )}
                          </View>
                          {lv.knowledgeCard.summary && (
                            <Text className={styles.kcardSummary}>{lv.knowledgeCard.summary}</Text>
                          )}
                        </View>
                      )}
                      {lv.hint && (
                        <View className={styles.hintArea}>
                          {isHintShown ? (
                            <Text className={styles.hintText}>💡 {lv.hint}</Text>
                          ) : (
                            <View className={styles.hintBtn} onClick={() => askHint(lv)}>
                              💡 给我提示（-1 灵感值）
                            </View>
                          )}
                        </View>
                      )}
                    </View>
                  )}
                </View>
              )
            })}
          </View>
        </View>

        <View className={styles.bottomPad} />
      </ScrollView>
    </View>
  )
}
