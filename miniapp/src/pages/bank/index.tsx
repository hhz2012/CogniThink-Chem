import { View, Text, ScrollView } from '@tarojs/components'
import { useState, useEffect } from 'react'
import Taro from '@tarojs/taro'
import TabBar from '../../components/TabBar'
import ThoughtChip from '../../components/ThoughtChip'
import { chapters, allChallenges } from '../../data/challenges'
import { loadProgress } from '../../utils/storage'
import { UserProgress, Chapter } from '../../types/chem'
import styles from './index.module.scss'

export default function Bank() {
  const [progress, setProgress] = useState<UserProgress>(loadProgress())
  const [activeChapter, setActiveChapter] = useState<string>('all')

  useEffect(() => {
    setProgress(loadProgress())
  }, [])

  const list: Chapter[] = activeChapter === 'all'
    ? chapters
    : chapters.filter((c) => c.id === activeChapter)

  const startChallenge = (id: string) => {
    Taro.navigateTo({ url: `/pages/challenge/index?id=${id}` })
  }

  return (
    <View className={styles.page}>
      <View className={styles.header}>
        <Text className={styles.title}>题库</Text>
        <Text className={styles.subtitle}>共 {allChallenges.length} 道题，已解 {progress.solvedIds.length} 道</Text>
      </View>

      <ScrollView scrollX className={styles.catScroll}>
        <View className={styles.catBar}>
          <View
            className={`${styles.catTab} ${activeChapter === 'all' ? styles.active : ''}`}
            onClick={() => setActiveChapter('all')}
          >
            全部
          </View>
          {chapters.map((ch) => (
            <View
              key={ch.id}
              className={`${styles.catTab} ${activeChapter === ch.id ? styles.active : ''}`}
              onClick={() => setActiveChapter(ch.id)}
              style={activeChapter === ch.id ? { background: ch.color, borderColor: ch.color } : {}}
            >
              {ch.icon} {ch.name}
            </View>
          ))}
        </View>
      </ScrollView>

      <ScrollView scrollY className={styles.body}>
        {list.map((ch) => (
          <View key={ch.id} className={styles.chapterGroup}>
            <View className={styles.chapterHeader} style={{ background: `${ch.color}10` }}>
              <Text className={styles.chapterIcon}>{ch.icon}</Text>
              <Text className={styles.chapterName} style={{ color: ch.color }}>{ch.name}</Text>
            </View>
            <View className={styles.challengeList}>
              {ch.challenges.map((c) => {
                const solved = progress.solvedIds.includes(c.id)
                return (
                  <View key={c.id} className={styles.challengeCard} onClick={() => startChallenge(c.id)}>
                    <View className={styles.challengeTop}>
                      <Text className={styles.challengeTitle}>{c.title}</Text>
                      {solved && <Text className={styles.solvedTag}>✓ 已通关</Text>}
                    </View>
                    <Text className={styles.challengeText}>{c.problemText}</Text>
                    <View className={styles.challengeFoot}>
                      <ThoughtChip thought={c.thought} size="sm" />
                      <Text className={styles.more}>挑战 →</Text>
                    </View>
                  </View>
                )
              })}
            </View>
          </View>
        ))}
        <View className={styles.bottomPad} />
      </ScrollView>

      <TabBar current="bank" />
    </View>
  )
}
