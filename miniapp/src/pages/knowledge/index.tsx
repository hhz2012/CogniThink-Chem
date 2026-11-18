import { View, Text, ScrollView } from '@tarojs/components'
import { useState, useEffect } from 'react'
import Taro from '@tarojs/taro'
import { redoxKnowledge, LAYER_META, KnowledgeLayer } from '../../data/knowledge/redox'
import styles from './index.module.scss'

const STORAGE_KEY = 'chem-knowledge-mastered-redox'
const LAYERS: KnowledgeLayer[] = ['Knowledge', 'Application', 'Ability']

export default function KnowledgePage() {
  const [mastered, setMastered] = useState<string[]>([])
  const [openId, setOpenId] = useState<string | null>(null)
  const [answers, setAnswers] = useState<Record<string, number>>({})

  useEffect(() => {
    const saved = Taro.getStorageSync(STORAGE_KEY)
    if (Array.isArray(saved)) setMastered(saved)
  }, [])

  /** 自测判定：答对记为已掌握，答错自动展开讲解 */
  const onPick = (kpId: string, idx: number, correct: number) => {
    if (answers[kpId] !== undefined) return
    setAnswers((s) => ({ ...s, [kpId]: idx }))
    if (idx === correct) {
      if (!mastered.includes(kpId)) {
        const next = [...mastered, kpId]
        setMastered(next)
        Taro.setStorageSync(STORAGE_KEY, next)
      }
      Taro.showToast({ title: '这个知识点你已掌握', icon: 'success' })
    } else {
      Taro.showToast({ title: '别急，看看下面的讲解', icon: 'none' })
    }
  }

  const percent = Math.round((mastered.length / redoxKnowledge.length) * 100)

  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        <View className={styles.header}>
          <Text className={styles.title}>氧化还原 · 知识体系</Text>
          <Text className={styles.subtitle}>
            {redoxKnowledge.length} 个知识点 · 三层进阶 · 先自测，不会就看讲解
          </Text>
          <View className={styles.progressRow}>
            <View className={styles.progressTrack}>
              <View className={styles.progressFill} style={{ width: `${percent}%` }} />
            </View>
            <Text className={styles.progressText}>{mastered.length}/{redoxKnowledge.length}</Text>
          </View>
        </View>

        {LAYERS.map((layer) => {
          const meta = LAYER_META[layer]
          const points = redoxKnowledge.filter((kp) => kp.layer === layer)
          return (
            <View key={layer} className={styles.layerSection}>
              <View className={styles.layerHead} style={{ borderLeftColor: meta.color }}>
                <Text className={styles.layerTitle}>{meta.icon} {meta.label}</Text>
                <Text className={styles.layerDesc}>{meta.desc}</Text>
              </View>

              {points.map((kp) => {
                const open = openId === kp.id
                const isMastered = mastered.includes(kp.id)
                const picked = answers[kp.id]
                return (
                  <View key={kp.id} className={styles.card}>
                    <View
                      className={styles.cardHead}
                      onClick={() => setOpenId(open ? null : kp.id)}
                    >
                      <View className={styles.cardHeadLeft}>
                        <Text
                          className={styles.cardCode}
                          style={{ color: meta.color, background: `${meta.color}12` }}
                        >
                          {kp.id}
                        </Text>
                        <View className={styles.cardTitleWrap}>
                          <Text className={styles.cardName}>{kp.name}</Text>
                          <Text className={styles.cardSummary}>{kp.summary}</Text>
                        </View>
                      </View>
                      <Text className={styles.cardToggle} style={isMastered ? { color: meta.color } : {}}>
                        {isMastered ? '✓' : open ? '−' : '+'}
                      </Text>
                    </View>

                    {open && (
                      <View className={styles.cardBody}>
                        <Text className={styles.label}>核心要点</Text>
                        {kp.keyPoints.map((p, i) => (
                          <Text key={i} className={styles.point}>· {p}</Text>
                        ))}

                        <Text className={styles.label}>易错提醒</Text>
                        {kp.pitfalls.map((p, i) => (
                          <Text key={i} className={styles.pitfall}>⚠ {p}</Text>
                        ))}

                        <Text className={styles.label}>自测：会不会？</Text>
                        <Text className={styles.checkQ}>{kp.check.question}</Text>
                        {kp.check.options.map((opt, idx) => {
                          const showRight = picked !== undefined && idx === kp.check.answerIndex
                          const showWrong = picked === idx && idx !== kp.check.answerIndex
                          return (
                            <View
                              key={idx}
                              className={`${styles.option} ${showRight ? styles.optRight : ''} ${showWrong ? styles.optWrong : ''}`}
                              onClick={() => onPick(kp.id, idx, kp.check.answerIndex)}
                            >
                              <Text className={styles.optLetter}>{String.fromCharCode(65 + idx)}</Text>
                              <Text className={styles.optText}>{opt}</Text>
                            </View>
                          )
                        })}
                        {picked !== undefined && (
                          <Text className={styles.checkExplain}>
                            {picked === kp.check.answerIndex ? '✓ 正确。' : '✗ 再想想。'}
                            {kp.check.explain}
                          </Text>
                        )}

                        <Text className={styles.label}>不会？这样教</Text>
                        {kp.teach.steps.map((s, i) => (
                          <Text key={i} className={styles.teachStep}>{s}</Text>
                        ))}
                        <View className={styles.exampleBox}>
                          <Text className={styles.exampleTitle}>📝 例题</Text>
                          <Text className={styles.exampleText}>{kp.teach.example}</Text>
                        </View>

                        {kp.relatedChallengeIds.length > 0 && (
                          <View>
                            <Text className={styles.label}>对应题目</Text>
                            <View className={styles.chipRow}>
                              {kp.relatedChallengeIds.map((cid) => (
                                <View
                                  key={cid}
                                  className={styles.chip}
                                  onClick={() => Taro.navigateTo({ url: `/pages/challenge/index?id=${cid}` })}
                                >
                                  <Text className={styles.chipText}>{cid}</Text>
                                </View>
                              ))}
                            </View>
                          </View>
                        )}
                      </View>
                    )}
                  </View>
                )
              })}
            </View>
          )
        })}
        <View className={styles.bottomPad} />
      </ScrollView>
    </View>
  )
}
