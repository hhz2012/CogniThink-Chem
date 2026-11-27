import { View, Text, ScrollView } from '@tarojs/components'
import { useState } from 'react'
import Taro from '@tarojs/taro'
import classnames from 'classnames'
import Collapse from '../../components/Collapse'
import TabBar from '../../components/TabBar'
import { getChallengesByCategory } from '../../data/challenges'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
import type { ModelIconName } from '../../types/chem'
import { MODEL_ACCENT } from '../../components/SolvePanels'
import { ChemCategory } from '../../types/chem'
import styles from './index.module.scss'

/** 学段选项 */
const STAGES = ['高一', '高二', '高三']

interface ReasonCard {
  key: ChemCategory
  icon: ModelIconName
  title: string
  desc: string
  /** 暂未开放：置灰 + 敬请期待，点击拦截 */
  disabled?: boolean
}

/** 五大推理专项卡片（两行排列：3 + 2）；图标统一青绿线性风格 */
const CARDS: ReasonCard[] = [
  { key: 'RedoxReaction', icon: 'zap', title: '氧化还原推理', desc: '先后顺序、pH依赖、配平、电子转移' },
  { key: 'IonicEquilibrium', icon: 'droplet', title: '离子体系推理', desc: '离子共存、离子方程式、少量过量' },
  { key: 'ChemicalEquilibrium', icon: 'refresh', title: '水溶液平衡', desc: '电离水解、粒子浓度、Ksp沉淀竞争' },
  { key: 'InorganicInference', icon: 'test-tube', title: '连续反应推理', desc: '铝铁硫氯氨，量变引起产物变化', disabled: true },
  { key: 'Electrochemistry', icon: 'battery', title: '电化学推理', desc: '原电池、电解池、放电顺序、膜体系', disabled: true },
]

/** 更多专项 */
const MORE_ITEMS = ['NA选择题专项', '物质的量计算', '元素周期律', '有机化学推理包']

const STAGE_KEY = 'home_stage'

export default function Home() {
  const savedStage = Taro.getStorageSync(STAGE_KEY) as string
  const [stageIndex, setStageIndex] = useState<number>(
    STAGES.indexOf(savedStage) >= 0 ? STAGES.indexOf(savedStage) : 2
  )
  const [moreOpen, setMoreOpen] = useState(false)
  /** 当前正在抽题跳转的卡片 key，null=空闲；用于加载提示与防重复点击 */
  const [loadingKey, setLoadingKey] = useState<string | null>(null)

  /** 切换学段，本地持久化 */
  const selectStage = (idx: number) => {
    if (idx < 0 || idx >= STAGES.length) return
    setStageIndex(idx)
    Taro.setStorageSync(STAGE_KEY, STAGES[idx])
  }

  /** 点击推理卡片：从该专项题库抽一道题，进入答题页（带加载提示与防重复点击） */
  const goReason = (key: ChemCategory) => {
    if (loadingKey) return
    const pool = getChallengesByCategory(key)
    if (pool.length === 0) {
      Taro.showToast({ title: '该专项暂无题目', icon: 'none' })
      return
    }
    const pick = pool[Math.floor(Math.random() * pool.length)]
    setLoadingKey(key)
    Taro.navigateTo({
      url: `/pages/challenge/index?id=${pick.id}`,
      complete: () => setLoadingKey(null),
    })
  }

  /** 拍照识题 / 导入题目：进入求解页 */
  const goImport = () => {
    Taro.navigateTo({ url: '/pages/solve/index' })
  }

  /** 更多专项：进入求解页并携带标签 */
  const goMore = (name: string) => {
    Taro.navigateTo({ url: `/pages/solve/index?tag=${encodeURIComponent(name)}` })
  }

  /** 反应推理卡：进入反应卡列表（分步脚手架讲解） */
  const goReaction = () => {
    Taro.navigateTo({ url: '/pages/reaction/index' })
  }

  /** 反应模拟器：实时调用化学引擎（反应轨迹 / 平衡计算 / 多步推理） */
  const goSimulator = () => {
    Taro.navigateTo({ url: '/pages/simulator/index' })
  }

  /** A–F 精熟练习：进入独立答题页（首页不内嵌作答内容） */
  const goPractice = () => {
    Taro.navigateTo({ url: '/pages/practice/index' })
  }

  return (
    <View className={styles.page}>
      {/* ========== 顶部标题（居中，右侧为微信原生胶囊） ========== */}
      <View className={styles.header}>
        <Text className={styles.title}>化学伴思</Text>
      </View>

      {/* ========== 内容滚动区 ========== */}
      <ScrollView scrollY className={styles.body}>
        {/* 学段切换：分段控件（唯一入口，移除冗余 Picker） */}
        <View className={styles.stagePills}>
          {STAGES.map((s, i) => (
            <View
              key={s}
              className={classnames(styles.pill, { [styles.pillActive]: i === stageIndex })}
              onClick={() => selectStage(i)}
            >
              <Text
                className={classnames(styles.pillText, {
                  [styles.pillTextActive]: i === stageIndex,
                })}
              >
                {s}
              </Text>
            </View>
          ))}
        </View>

        {/* 分区标题 */}
        <View className={styles.sectionHead}>
          <ModelIcon name="test-tube" size={24} color={MODEL_ACCENT} className={styles.sectionIcon} />
          <View className={styles.sectionTitleWrap}>
            <Text className={styles.sectionTitle}>化学分步推理工作台</Text>
            <Text className={styles.sectionSub}>一步一校验，不靠AI直接给答案</Text>
          </View>
        </View>

        {/* 卡片网格 */}
        <View className={styles.grid}>
          {CARDS.map((c) => {
            const isLoading = loadingKey === c.key
            return (
              <View
                key={c.key}
                className={classnames(styles.card, {
                  [styles.cardDisabled]: c.disabled,
                  [styles.cardLoading]: isLoading,
                })}
                onClick={() => {
                  if (c.disabled) {
                    Taro.showToast({ title: '该模块开发中，敬请期待', icon: 'none' })
                    return
                  }
                  goReason(c.key)
                }}
              >
                {isLoading ? (
                  <View className={styles.cardSpinner} />
                ) : (
                  <ModelIcon name={c.icon} size={30} color={MODEL_ACCENT} className={styles.cardIcon} />
                )}
                <Text className={styles.cardTitle}>{c.title}</Text>
                <Text className={styles.cardDesc}>{c.desc}</Text>
                {isLoading && <Text className={styles.cardLoadingText}>正在为你抽题…</Text>}
                {c.disabled && <Text className={styles.disabledTag}>敬请期待</Text>}
              </View>
            )
          })}
        </View>

        {/* 更多专项（可折叠） */}
        <View className={styles.moreCard}>
          <View className={styles.moreHead} onClick={() => setMoreOpen(!moreOpen)}>
            <Text className={styles.moreTitle}>更多专项</Text>
            <Text
              className={classnames(styles.moreChevron, {
                [styles.moreChevronOpen]: moreOpen,
              })}
            >
              ›
            </Text>
          </View>
          <Collapse open={moreOpen}>
            <View className={styles.moreList}>
              {MORE_ITEMS.map((m) => (
                <View key={m} className={styles.moreItem} onClick={() => goMore(m)}>
                  <Text className={styles.moreItemText}>{m}</Text>
                  <Text className={styles.moreItemArrow}>›</Text>
                </View>
              ))}
            </View>
          </Collapse>
        </View>

        {/* 反应推理卡入口：一条反应的分步脚手架讲解 */}
        <View className={styles.reactionEntry} onClick={goReaction}>
          <View className={styles.reactionEntryIcon}>
            <ModelIcon name="hexagon" size={30} color={MODEL_ACCENT} />
          </View>
          <View className={styles.reactionEntryMain}>
            <Text className={styles.reactionEntryTitle}>反应推理卡</Text>
            <Text className={styles.reactionEntryDesc}>一条反应拆成分步脚手架，点提示看实例化讲解</Text>
          </View>
          <Text className={styles.reactionEntryArrow}>›</Text>
        </View>

        {/* 反应模拟器入口：实时调用化学引擎，烧瓶动画与现象同步 */}
        <View className={styles.reactionEntry} onClick={goSimulator}>
          <View className={styles.reactionEntryIcon}>
            <ModelIcon name="test-tube" size={30} color={MODEL_ACCENT} />
          </View>
          <View className={styles.reactionEntryMain}>
            <Text className={styles.reactionEntryTitle}>反应模拟器</Text>
            <Text className={styles.reactionEntryDesc}>反应轨迹 / 平衡计算 / 多步推理，烧瓶动画与现象同步</Text>
          </View>
          <Text className={styles.reactionEntryArrow}>›</Text>
        </View>

        {/* A–F 精熟练习入口：作答内容放在独立页，首页只保留入口 */}
        <View className={styles.practiceEntry} onClick={goPractice}>
          <View className={styles.practiceEntryIcon}>
            <ModelIcon name="zap" size={30} color={MODEL_ACCENT} />
          </View>
          <View className={styles.practiceEntryMain}>
            <Text className={styles.practiceEntryTitle}>A–F 精熟练习</Text>
            <Text className={styles.practiceEntryDesc}>六步推理逐一作答，答对解锁下一步，答错不泄答案</Text>
          </View>
          <Text className={styles.practiceEntryArrow}>›</Text>
        </View>

        {/* 通栏拍照识题按钮 */}
        <View className={styles.photoBtn} onClick={goImport}>
          <ModelIcon name="camera" size={22} color="#ffffff" />
          <Text className={styles.photoBtnText}>拍照识题，导入题目</Text>
        </View>

        <View className={styles.bottomPad} />
      </ScrollView>

      <TabBar current="home" />
    </View>
  )
}
