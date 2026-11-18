import { View, Text, ScrollView, Image } from '@tarojs/components'
import { useState, useEffect } from 'react'
import Taro from '@tarojs/taro'
import TabBar from '../../components/TabBar'
import { chapters, allChallenges } from '../../data/challenges'
import { loadProgress } from '../../utils/storage'
import { UserProgress, ChemCategory, Difficulty } from '../../types/chem'
import styles from './index.module.scss'

/** 主题：edu 标准教育模式（默认）/ adventure 秘境探险模式 */
type ThemeMode = 'edu' | 'adventure'

// 专项图标（AI 生成 · 统一白色线条风 + 深青底，双主题通用）
const iconImage = (subject: string) =>
  `https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=${encodeURIComponent(`${subject}, minimalist thin white line art icon, centered, dark teal green gradient background, rounded square app icon style, flat design, elegant, high quality, no text`)}&image_size=square_hd`

// 探险模式卡片底纹（对应专题弱图片，10% 透明度）
const bgImage = (prompt: string) =>
  `https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=${encodeURIComponent(prompt)}&image_size=square_hd`

const CHAPTER_ICON: Record<ChemCategory, string> = {
  RedoxReaction: iconImage('lightning bolt with electron transfer arrows'),
  IonicEquilibrium: iconImage('water drop surrounded by small ion particles'),
  ChemicalEquilibrium: iconImage('laboratory balance scale in equilibrium'),
  Electrochemistry: iconImage('battery cell with positive and negative terminals'),
  InorganicInference: iconImage('crystal gemstone with geometric facets'),
  OrganicChemistry: iconImage('hexagonal benzene ring molecule structure'),
}

const CHAPTER_PATTERN: Record<ChemCategory, string> = {
  RedoxReaction: bgImage('single glowing red-orange spark flame on pure black background, macro photography, cinematic, no text'),
  IonicEquilibrium: bgImage('single laboratory beaker outline with glowing cyan liquid on pure black background, minimalist, cinematic, no text'),
  ChemicalEquilibrium: bgImage('single laboratory balance scale with glowing amber light on pure black background, minimalist, cinematic, no text'),
  Electrochemistry: bgImage('single glowing battery cell with purple energy arcs on pure black background, minimalist, cinematic, no text'),
  InorganicInference: bgImage('single glowing green crystal gemstone on pure black background, minimalist, cinematic, no text'),
  OrganicChemistry: bgImage('single glowing teal hexagonal benzene ring molecule on pure black background, minimalist, cinematic, no text'),
}

const THEME_KEY = 'home_theme_mode'

/** 难度等级（从易到难），用于专题卡片的难度分布展示 */
const DIFF_LEVELS: { key: Difficulty; label: string; color: string }[] = [
  { key: '基础', label: '基础', color: '#16a34a' },
  { key: '进阶', label: '进阶', color: '#2563eb' },
  { key: '综合', label: '综合', color: '#ea580c' },
  { key: '难题', label: '难题', color: '#dc2626' },
  { key: '压轴', label: '压轴', color: '#7c3aed' },
]

export default function Home() {
  const [progress, setProgress] = useState<UserProgress>(loadProgress())
  const [theme, setTheme] = useState<ThemeMode>('edu')

  useEffect(() => {
    setProgress(loadProgress())
    const saved = Taro.getStorageSync(THEME_KEY) as ThemeMode
    if (saved === 'edu' || saved === 'adventure') setTheme(saved)
  }, [])

  /** 风格切换：edu ↔ adventure，本地持久化 */
  const toggleTheme = () => {
    const next: ThemeMode = theme === 'edu' ? 'adventure' : 'edu'
    setTheme(next)
    Taro.setStorageSync(THEME_KEY, next)
  }

  /** 化学闯关按钮：跳转全局第一个可挑战关卡 */
  const goChallenge = () => {
    for (const ch of chapters) {
      const next = ch.challenges.find((c, i) => {
        const solved = progress.solvedIds.includes(c.id)
        const prevSolved = i === 0 || ch.challenges.slice(0, i).every((p) => progress.solvedIds.includes(p.id))
        return !solved && prevSolved
      })
      if (next) {
        Taro.navigateTo({ url: `/pages/challenge/index?id=${next.id}` })
        return
      }
    }
  }

  /** 点击卡片：跳转本专题下一个可挑战的关卡 */
  const goNextChallenge = (
    ch: typeof chapters[number],
    allSolved: boolean
  ) => {
    if (allSolved) return
    const next = ch.challenges.find((c, i) => {
      const solved = progress.solvedIds.includes(c.id)
      const prevSolved = i === 0 || ch.challenges.slice(0, i).every((prev) => progress.solvedIds.includes(prev.id))
      return !solved && prevSolved
    })
    if (next) Taro.navigateTo({ url: `/pages/challenge/index?id=${next.id}` })
  }

  const isEdu = theme === 'edu'

  return (
    <View className={`${styles.page} ${isEdu ? styles.edu : styles.adv}`}>
      {/* ========== 顶部标题区 ========== */}
      <View className={styles.header}>
        <View className={styles.headerRow}>
          <Text className={styles.title}>专项思维扩展</Text>
          <View className={styles.btnGroup}>
            <View className={styles.btnPrimary} onClick={goChallenge}>
              <Text className={styles.btnPrimaryText}>化学闯关</Text>
            </View>
            <View className={styles.btnTheme} onClick={toggleTheme}>
              <Text className={styles.btnThemeText}>风格切换</Text>
            </View>
          </View>
        </View>
        <Text className={styles.descMain}>六大高考化学难题专项 · {allChallenges.length}道精选难题</Text>
        <Text className={styles.descSub}>AI伴思引导层层递进，练透每一条解题思路</Text>
        <View
          className={styles.tutorEntry}
          onClick={() => Taro.navigateTo({ url: '/pages/tutor/index' })}
        >
          <Text className={styles.tutorEntryText}>🧭 伴思导学 · 分步推理</Text>
        </View>
      </View>

      {/* ========== 6个专题卡片（2列 × 3行） ========== */}
      <ScrollView scrollY className={styles.body}>
        <View className={styles.grid}>
          {chapters.map((ch) => {
            const solvedInChapter = ch.challenges.filter((c) =>
              progress.solvedIds.includes(c.id)
            ).length
            const allSolved = solvedInChapter === ch.challenges.length
            const chapterProgress = Math.round((solvedInChapter / ch.challenges.length) * 100)
            // 箭头位置限制在 3%~97%，避免贴边
            const arrowPos = Math.min(Math.max(chapterProgress, 3), 97)
            const diffStats = DIFF_LEVELS.map((d) => ({
              ...d,
              total: ch.challenges.filter((c) => c.difficulty === d.key).length,
            }))
            return (
              <View
                key={ch.id}
                className={styles.card}
                onClick={() => goNextChallenge(ch, allSolved)}
              >
                {/* 探险模式：专题弱底纹（10%） */}
                {!isEdu && (
                  <Image
                    className={styles.cardPattern}
                    src={CHAPTER_PATTERN[ch.category]}
                    mode="aspectFill"
                    lazyLoad
                  />
                )}
                <View className={styles.cardInner}>
                  <View className={styles.cardTop}>
                    <View className={styles.iconBox}>
                      <Image
                        className={styles.iconImg}
                        src={CHAPTER_ICON[ch.category]}
                        mode="aspectFill"
                        lazyLoad
                      />
                    </View>
                    <Text className={styles.cardTitle}>{ch.name}</Text>
                  </View>
                  <View className={styles.cardBottom}>
                    <View className={styles.diffBar}>
                      {diffStats.map((d) => (
                        <View
                          key={d.key}
                          className={styles.diffSeg}
                          style={{ flex: d.total, background: d.color }}
                        />
                      ))}
                    </View>
                    {/* 进度箭头：指示用户在该专项已推进到的位置 */}
                    <View className={styles.progressArrow} style={{ left: `${arrowPos}%` }} />
                  </View>
                </View>
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
