import { View, Text, ScrollView } from '@tarojs/components'
import { useState, useEffect } from 'react'
import Taro from '@tarojs/taro'
import TabBar from '../../components/TabBar'
import { chapters } from '../../data/challenges'
import { loadProgress } from '../../utils/storage'
import { UserProgress } from '../../types/chem'
import styles from './index.module.scss'

export default function Home() {
  const [progress, setProgress] = useState<UserProgress>(loadProgress())

  useEffect(() => {
    setProgress(loadProgress())
  }, [])

  const totalSolved = progress.solvedIds.length

  const startChallenge = (challengeId: string) => {
    Taro.navigateTo({ url: `/pages/challenge/index?id=${challengeId}` })
  }

  return (
    <View className={styles.page}>
      {/* Hero 区 */}
      <View className={styles.hero}>
        <View className={styles.heroBg}>
          <Text className={styles.float1}>⚗️</Text>
          <Text className={styles.float2}>🧪</Text>
          <Text className={styles.float3}>🔬</Text>
        </View>
        <View className={styles.heroInner}>
          <Text className={styles.heroEyebrow}>CogniThink · 知识驱动</Text>
          <Text className={styles.heroTitle}>化学闯关</Text>
          <Text className={styles.heroSub}>用 5 大化学思想，征服 6 章关卡</Text>
          <View className={styles.stats}>
            <View className={styles.stat}>
              <Text className={styles.statNum}>{totalSolved}</Text>
              <Text className={styles.statLabel}>已通关</Text>
            </View>
            <View className={styles.stat}>
              <Text className={styles.statNum}>{progress.inspirations}</Text>
              <Text className={styles.statLabel}>灵感值</Text>
            </View>
            <View className={styles.stat}>
              <Text className={styles.statNum}>{chapters.length}</Text>
              <Text className={styles.statLabel}>章节</Text>
            </View>
          </View>
        </View>
      </View>

      <ScrollView scrollY className={styles.body}>
        <View className={styles.chapterList}>
          {chapters.map((ch, idx) => {
            const solvedInChapter = ch.challenges.filter((c) =>
              progress.solvedIds.includes(c.id)
            ).length
            const allSolved = solvedInChapter === ch.challenges.length
            const chapterProgress = Math.round((solvedInChapter / ch.challenges.length) * 100)
            return (
              <View key={ch.id} className={styles.chapterCard} style={{ borderLeftColor: ch.color }}>
                <View className={styles.chapterHead}>
                  <View className={styles.chapterIcon} style={{ background: `${ch.color}15`, color: ch.color }}>
                    <Text>{ch.icon}</Text>
                  </View>
                  <View className={styles.chapterInfo}>
                    <Text className={styles.chapterName}>第{idx + 1}章 · {ch.name}</Text>
                    <Text className={styles.chapterDesc}>{ch.desc}</Text>
                  </View>
                  <View className={styles.chapterBadge}>
                    <Text style={{ color: ch.color }}>{solvedInChapter}/{ch.challenges.length}</Text>
                  </View>
                </View>
                <View className={styles.progressBar}>
                  <View className={styles.progressFill} style={{ width: `${chapterProgress}%`, background: ch.color }} />
                </View>
                <View className={styles.levelGrid}>
                  {ch.challenges.map((c, i) => {
                    const solved = progress.solvedIds.includes(c.id)
                    return (
                      <View
                        key={c.id}
                        className={`${styles.levelBtn} ${solved ? styles.solved : ''}`}
                        onClick={() => startChallenge(c.id)}
                      >
                        <Text className={styles.levelNum}>{solved ? '✓' : i + 1}</Text>
                        <Text className={styles.levelTitle}>{c.title}</Text>
                      </View>
                    )
                  })}
                </View>
                {allSolved && <View className={styles.chapterDone}>🏆 章节通关</View>}
              </View>
            )
          })}
        </View>
        <View className={styles.bottomPad} />
      </ScrollView>

      <TabBar current="home" />
    </View>
  )
}
