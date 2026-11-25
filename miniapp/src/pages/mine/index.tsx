import { View, Text } from '@tarojs/components'
import { useState } from 'react'
import Taro, { useDidShow } from '@tarojs/taro'
import TabBar from '../../components/TabBar'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
import { loadProgress, resetProgress, THOUGHT_META } from '../../utils/storage'
import { UserProgress, Thought } from '../../types/chem'
import { chapters, allChallenges } from '../../data/challenges'
import styles from './index.module.scss'

const THOUGHTS: Thought[] = ['Conservation', 'Structure', 'Equilibrium', 'Classification', 'ModelCognition']

export default function Mine() {
  const [progress, setProgress] = useState<UserProgress>(loadProgress())

  // 切回本页时刷新统计（做题/错题变化后同步）
  useDidShow(() => {
    setProgress(loadProgress())
  })

  const goBank = () => {
    Taro.redirectTo({ url: '/pages/bank/index' })
  }

  const handleReset = () => {
    Taro.showModal({
      title: '重置进度',
      content: '确定要清空所有闯关进度和思想碎片吗？',
      success: (res) => {
        if (res.confirm) {
          resetProgress()
          setProgress(loadProgress())
          Taro.showToast({ title: '已重置', icon: 'success' })
        }
      },
    })
  }

  const total = allChallenges.length
  const solved = progress.solvedIds.length
  const percent = Math.round((solved / total) * 100)

  return (
    <View className={styles.page}>
      <View className={styles.header}>
        <View className={styles.avatar}><ModelIcon name="test-tube" size={34} color="#2EA7C4" /></View>
        <Text className={styles.nickname}>化学探险家</Text>
        <Text className={styles.desc}>用知识驱动，征服化学难题</Text>
        <View className={styles.progressRow}>
          <View className={styles.progressBar}>
            <View className={styles.progressFill} style={{ width: `${percent}%` }} />
          </View>
          <Text className={styles.progressText}>{solved}/{total} ({percent}%)</Text>
        </View>
      </View>

      <View className={styles.body}>
        <View className={styles.statsGrid}>
          <View className={styles.statCard}>
            <ModelIcon name="check-circle" size={24} color="#16a34a" className={styles.statIcon} />
            <Text className={styles.statNum}>{solved}</Text>
            <Text className={styles.statLabel}>已通关</Text>
          </View>
          <View className={styles.statCard} onClick={goBank}>
            <ModelIcon name="book" size={24} color="#2563eb" className={styles.statIcon} />
            <Text className={styles.statNum}>{progress.wrongIds.length}</Text>
            <Text className={styles.statLabel}>错题本</Text>
          </View>
          <View className={styles.statCard}>
            <ModelIcon name="lightbulb" size={24} color="#ea580c" className={styles.statIcon} />
            <Text className={styles.statNum}>{progress.inspirations}</Text>
            <Text className={styles.statLabel}>灵感值</Text>
          </View>
          <View className={styles.statCard}>
            <ModelIcon name="trophy" size={24} color="#d97706" className={styles.statIcon} />
            <Text className={styles.statNum}>{chapters.filter((ch) => ch.challenges.every((c) => progress.solvedIds.includes(c.id))).length}</Text>
            <Text className={styles.statLabel}>通关专项</Text>
          </View>
        </View>

        <View className={styles.section}>
          <Text className={styles.sectionTitle}>思想碎片收集</Text>
          <View className={styles.thoughtList}>
            {THOUGHTS.map((t) => {
              const meta = THOUGHT_META[t]
              const count = progress.thoughtCounts[t]
              return (
                <View key={t} className={styles.thoughtRow}>
                  <ModelIcon name={meta.icon} size={18} color={meta.color} className={styles.thoughtIcon} />
                  <Text className={styles.thoughtLabel}>{meta.label}</Text>
                  <Text className={styles.thoughtCount} style={{ color: count > 0 ? meta.color : '#cbd5e1' }}>
                    {count} 枚
                  </Text>
                </View>
              )
            })}
          </View>
        </View>

        <View className={styles.section}>
          <Text className={styles.sectionTitle}>关于</Text>
          <View className={styles.aboutCard}>
            <Text className={styles.aboutTitle}>化学闯关 · CogniThink-Chem</Text>
            <Text className={styles.aboutDesc}>
              每道题的解题过程均由知识驱动推断引擎生成。五大化学思想：守恒、结构决定性质、动态平衡、分类比较、证据推理·模型认知。
            </Text>
            <View className={styles.btnRow}>
              <View className={styles.resetBtn} onClick={handleReset}>重置进度</View>
            </View>
          </View>
        </View>

        <View className={styles.bottomPad} />
      </View>

      <TabBar current="mine" />
    </View>
  )
}
