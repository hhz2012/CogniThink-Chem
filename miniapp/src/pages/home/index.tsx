import { View, Text, Picker, ScrollView } from '@tarojs/components'
import { useState } from 'react'
import Taro from '@tarojs/taro'
import classnames from 'classnames'
import TabBar from '../../components/TabBar'
import ModelIcon, { ModelIconName } from '../../components/SolvePanels/ModelIcon'
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

  /** 切换学段，本地持久化 */
  const selectStage = (idx: number) => {
    if (idx < 0 || idx >= STAGES.length) return
    setStageIndex(idx)
    Taro.setStorageSync(STAGE_KEY, STAGES[idx])
  }

  const onStageChange = (e: { detail: { value: number | string } }) => {
    selectStage(Number(e.detail.value))
  }

  /** 点击推理卡片：进入题目求解（引擎推理链），携带分类提示 */
  const goReason = (key: ChemCategory) => {
    Taro.navigateTo({ url: `/pages/solve/index?category=${key}` })
  }

  /** 拍照识题 / 导入题目：进入求解页 */
  const goImport = () => {
    Taro.navigateTo({ url: '/pages/solve/index' })
  }

  /** 更多专项：进入求解页并携带标签 */
  const goMore = (name: string) => {
    Taro.navigateTo({ url: `/pages/solve/index?tag=${encodeURIComponent(name)}` })
  }

  return (
    <View className={styles.page}>
      {/* ========== 顶部标题（居中，右侧为微信原生胶囊） ========== */}
      <View className={styles.header}>
        <Text className={styles.title}>化学伴思</Text>
      </View>

      {/* ========== 内容滚动区 ========== */}
      <ScrollView scrollY className={styles.body}>
        {/* 学段选择 */}
        <View className={styles.stageRow}>
          <Picker
            mode="selector"
            range={STAGES}
            value={stageIndex}
            onChange={onStageChange}
          >
            <View className={styles.stageLabel}>
              <Text className={styles.stageLabelText}>当前学段：{STAGES[stageIndex]}</Text>
              <Text className={styles.stageArrow}>▾</Text>
            </View>
          </Picker>
        </View>

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
          {CARDS.map((c) => (
            <View
              key={c.key}
              className={classnames(styles.card, { [styles.cardDisabled]: c.disabled })}
              onClick={() => {
                if (c.disabled) {
                  Taro.showToast({ title: '该模块开发中，敬请期待', icon: 'none' })
                  return
                }
                goReason(c.key)
              }}
            >
              {/* 图标：青绿线性 SVG，无背景无边框，直接贴在白卡上 */}
              <ModelIcon name={c.icon} size={30} color={MODEL_ACCENT} className={styles.cardIcon} />
              <Text className={styles.cardTitle}>{c.title}</Text>
              <Text className={styles.cardDesc}>{c.desc}</Text>
              {c.disabled && <Text className={styles.disabledTag}>敬请期待</Text>}
            </View>
          ))}
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
          {moreOpen && (
            <View className={styles.moreList}>
              {MORE_ITEMS.map((m) => (
                <View key={m} className={styles.moreItem} onClick={() => goMore(m)}>
                  <Text className={styles.moreItemText}>{m}</Text>
                  <Text className={styles.moreItemArrow}>›</Text>
                </View>
              ))}
            </View>
          )}
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
