import { View, Text, ScrollView } from '@tarojs/components'
import { useState, useEffect } from 'react'
import TabBar from '../../components/TabBar'
import ThoughtChip from '../../components/ThoughtChip'
import { loadProgress, THOUGHT_META } from '../../utils/storage'
import { UserProgress, Thought } from '../../types/chem'
import { chapters, allChallenges } from '../../data/challenges'
import styles from './index.module.scss'

const THOUGHTS: Thought[] = ['Conservation', 'Structure', 'Equilibrium', 'Classification', 'ModelCognition']

// 成就定义
const ACHIEVEMENTS = [
  { id: 'first', name: '初露锋芒', desc: '完成第一道题', icon: '🌱', check: (p: UserProgress) => p.solvedIds.length >= 1 },
  { id: 'five', name: '渐入佳境', desc: '完成 5 道题', icon: '⚡', check: (p: UserProgress) => p.solvedIds.length >= 5 },
  { id: 'ten', name: '勤学不辍', desc: '完成 10 道题', icon: '🔥', check: (p: UserProgress) => p.solvedIds.length >= 10 },
  { id: 'all', name: '通关达人', desc: '完成全部题目', icon: '🏆', check: (p: UserProgress) => p.solvedIds.length >= allChallenges.length },
  { id: 'thought3', name: '思想收集者', desc: '集齐 3 种化学思想碎片', icon: '💎', check: (p: UserProgress) => THOUGHTS.filter((t) => p.thoughtCounts[t] > 0).length >= 3 },
  { id: 'thought5', name: '五思俱全', desc: '集齐全部 5 种化学思想碎片', icon: '🌈', check: (p: UserProgress) => THOUGHTS.every((t) => p.thoughtCounts[t] > 0) },
  { id: 'chapter1', name: '初探奥秘', desc: '通关一个章节', icon: '🎯', check: (p: UserProgress) => chapters.some((ch) => ch.challenges.every((c) => p.solvedIds.includes(c.id))) },
  { id: 'chapter3', name: '探索不止', desc: '通关 3 个章节', icon: '🧭', check: (p: UserProgress) => chapters.filter((ch) => ch.challenges.every((c) => p.solvedIds.includes(c.id))).length >= 3 },
]

export default function Atlas() {
  const [progress, setProgress] = useState<UserProgress>(loadProgress())

  useEffect(() => {
    setProgress(loadProgress())
  }, [])

  const unlockedAchievements = ACHIEVEMENTS.filter((a) => a.check(progress)).length
  const thoughtCollected = THOUGHTS.filter((t) => progress.thoughtCounts[t] > 0).length

  return (
    <View className={styles.page}>
      <View className={styles.header}>
        <Text className={styles.title}>化学思想图鉴</Text>
        <Text className={styles.subtitle}>集齐 5 大化学思想碎片，解锁成就</Text>
      </View>

      <ScrollView scrollY className={styles.body}>
        {/* 思想碎片区 */}
        <View className={styles.section}>
          <Text className={styles.sectionTitle}>思想碎片（{thoughtCollected}/5）</Text>
          <View className={styles.thoughtGrid}>
            {THOUGHTS.map((t) => {
              const meta = THOUGHT_META[t]
              const count = progress.thoughtCounts[t]
              const unlocked = count > 0
              return (
                <View
                  key={t}
                  className={`${styles.thoughtCard} ${unlocked ? '' : styles.locked}`}
                  style={unlocked ? { borderColor: meta.color } : {}}
                >
                  <View className={styles.thoughtIcon} style={unlocked ? { background: `${meta.color}15` } : {}}>
                    <Text style={unlocked ? { color: meta.color } : {}}>{unlocked ? meta.icon : '❓'}</Text>
                  </View>
                  <Text className={styles.thoughtName}>{unlocked ? meta.label : '???'}</Text>
                  <Text className={styles.thoughtCount}>{unlocked ? `${count} 枚` : '未解锁'}</Text>
                </View>
              )
            })}
          </View>
        </View>

        {/* 成就墙 */}
        <View className={styles.section}>
          <Text className={styles.sectionTitle}>成就墙（{unlockedAchievements}/{ACHIEVEMENTS.length}）</Text>
          <View className={styles.achievementGrid}>
            {ACHIEVEMENTS.map((a) => {
              const unlocked = a.check(progress)
              return (
                <View key={a.id} className={`${styles.achievementCard} ${unlocked ? '' : styles.locked}`}>
                  <Text className={styles.achievementIcon}>{unlocked ? a.icon : '🔒'}</Text>
                  <Text className={styles.achievementName}>{a.name}</Text>
                  <Text className={styles.achievementDesc}>{a.desc}</Text>
                </View>
              )
            })}
          </View>
        </View>

        <View className={styles.bottomPad} />
      </ScrollView>

      <TabBar current="atlas" />
    </View>
  )
}
