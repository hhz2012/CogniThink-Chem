import { View, Text, ScrollView } from '@tarojs/components'
import { useState } from 'react'
import Taro, { useDidShow } from '@tarojs/taro'
import classnames from 'classnames'
import TabBar from '../../components/TabBar'
import ThoughtChip from '../../components/ThoughtChip'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
import { chapters, allChallenges, getChallengeById } from '../../data/challenges'
import { loadProgress, unmarkWrong } from '../../utils/storage'
import { UserProgress, Chapter } from '../../types/chem'
import styles from './index.module.scss'

export default function Bank() {
  const [progress, setProgress] = useState<UserProgress>(loadProgress())
  const [allOpen, setAllOpen] = useState(false)

  // Tab 页切回时刷新（从 challenge 页做完题返回后）
  useDidShow(() => {
    setProgress(loadProgress())
  })

  const wrongList = progress.wrongIds
    .map((id) => getChallengeById(id))
    .filter((c): c is NonNullable<ReturnType<typeof getChallengeById>> => !!c)

  const chapterOf = (category: Chapter['category']) => chapters.find((c) => c.category === category)

  const redo = (id: string) => {
    Taro.navigateTo({ url: `/pages/challenge/index?id=${id}` })
  }

  const remove = (id: string) => {
    Taro.showModal({
      title: '移出错题本',
      content: '确定移除这道题吗？重做正确后也会自动移除。',
      success: (res) => {
        if (res.confirm) {
          setProgress((prev) => unmarkWrong(prev, id))
          Taro.showToast({ title: '已移除', icon: 'none' })
        }
      },
    })
  }

  const goHome = () => {
    Taro.redirectTo({ url: '/pages/home/index' })
  }

  return (
    <View className={styles.page}>
      {/* ===== 顶部标题 ===== */}
      <View className={styles.header}>
        <Text className={styles.title}>错题本</Text>
        <Text className={styles.subtitle}>
          {wrongList.length > 0
            ? `共 ${wrongList.length} 道错题，去攻克它们`
            : '还没有错题，去做题吧'}
        </Text>
      </View>

      <ScrollView scrollY className={styles.body}>
        {/* ===== 错题列表 / 空状态 ===== */}
        {wrongList.length === 0 ? (
          <View className={styles.empty}>
            <Text className={styles.emptyIcon}>🎉</Text>
            <Text className={styles.emptyTitle}>太棒了，暂无错题</Text>
            <Text className={styles.emptySub}>去挑战几道题，错题会自动收录到这里</Text>
            <View className={styles.goBtn} onClick={goHome}>
              <Text className={styles.goBtnText}>去做题</Text>
            </View>
          </View>
        ) : (
          <View className={styles.wrongList}>
            {wrongList.map((c) => {
              const ch = chapterOf(c.category)
              return (
                <View key={c.id} className={styles.wrongCard}>
                  <View className={styles.wrongHead}>
                    <View className={styles.chipRow}>
                      {ch && (
                        <View
                          className={styles.chapterTag}
                          style={{ background: `${ch.color}15`, color: ch.color }}
                        >
                          <ModelIcon name={ch.icon} size={12} color={ch.color} />
                          <Text>{ch.name}</Text>
                        </View>
                      )}
                      <Text className={styles.diffTag}>{c.difficulty}</Text>
                    </View>
                    <Text className={styles.removeBtn} onClick={() => remove(c.id)}>
                      移除
                    </Text>
                  </View>
                  <View className={styles.wrongBody} onClick={() => redo(c.id)}>
                    <Text className={styles.wrongTitle}>{c.title}</Text>
                    <Text className={styles.wrongText}>{c.problemText}</Text>
                    <View className={styles.wrongFoot}>
                      <ThoughtChip thought={c.thought} size="sm" />
                      <Text className={styles.redoText}>去重做 →</Text>
                    </View>
                  </View>
                </View>
              )
            })}
          </View>
        )}

        {/* ===== 题库总览（可折叠） ===== */}
        <View className={styles.allCard}>
          <View className={styles.allHead} onClick={() => setAllOpen(!allOpen)}>
            <Text className={styles.allTitle}>题库总览</Text>
            <Text className={styles.allMeta}>共 {allChallenges.length} 题</Text>
            <Text
              className={classnames(styles.allChevron, { [styles.allChevronOpen]: allOpen })}
            >
              ›
            </Text>
          </View>
          {allOpen && (
            <View className={styles.allList}>
              {chapters.map((ch) => (
                <View key={ch.id} className={styles.allChapter}>
                  <View className={styles.allChapterHead} style={{ background: `${ch.color}10` }}>
                    <View className={styles.allChapterName} style={{ color: ch.color }}>
                      <ModelIcon name={ch.icon} size={15} color={ch.color} />
                      <Text>{ch.name}</Text>
                    </View>
                    <Text className={styles.allChapterCount}>{ch.challenges.length} 题</Text>
                  </View>
                  <View className={styles.allItems}>
                    {ch.challenges.map((c) => {
                      const solved = progress.solvedIds.includes(c.id)
                      return (
                        <View
                          key={c.id}
                          className={styles.allItem}
                          onClick={() => redo(c.id)}
                        >
                          <View className={styles.allItemMain}>
                            <Text className={styles.allItemTitle}>{c.title}</Text>
                            <Text className={styles.allItemDiff}>{c.difficulty}</Text>
                          </View>
                          {solved ? (
                            <Text className={styles.solvedBadge}>✓</Text>
                          ) : (
                            <Text className={styles.allItemArrow}>›</Text>
                          )}
                        </View>
                      )
                    })}
                  </View>
                </View>
              ))}
            </View>
          )}
        </View>

        <View className={styles.bottomPad} />
      </ScrollView>

      <TabBar current="bank" />
    </View>
  )
}
