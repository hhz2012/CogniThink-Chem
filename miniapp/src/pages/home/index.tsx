import { View, Text } from '@tarojs/components'
import Taro, { usePullDownRefresh } from '@tarojs/taro'
import TabBar from '../../components/TabBar'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
import ExampleArt from '../../components/ExampleArt'
import { EXAMPLES, exampleToQuery, type HomeExample } from '../../data/homeExamples'
import type { ModelIconName } from '../../types/chem'
import styles from './index.module.scss'

/** 切换列数：改这里 + 下方 cardWidth 计算即可在 2/3 列间切换 */
const COLS = 3

/** 两张主功能大卡 */
const ENTRIES: {
  key: string
  title: string
  desc: string
  bg: string
  smallTitle?: boolean
  icon?: ModelIconName
}[] = [
  {
    key: 'camera',
    icon: 'camera',
    title: '拍照识题',
    desc: '拍照识题，秒出解题思路',
    bg: 'linear-gradient(135deg,#FF9A3C,#FF5A2D)',
  },
  {
    key: 'derive',
    title: '反应过程推导',
    desc: '多物质混合，推反应路径',
    bg: 'linear-gradient(135deg,#22C5E0,#2F6BFF)',
    smallTitle: true,
  },
]

export default function Home() {
  // 3 列：(总宽 - 2 个 16rpx 间距) / 3
  const cardWidth = `calc((100% - ${(COLS - 1) * 16}rpx) / ${COLS})`

  /** 顶部「+」按钮：预留 */
  const addTool = () => {
    // TODO: 打开添加工具
  }

  /** 左卡：拍照识题，预留 chooseMedia */
  const onCamera = () => {
    // TODO: Taro.chooseMedia 拍照识题
  }

  /** 右卡：反应过程推导，预留跳转推导页 */
  const onDerive = () => {
    // TODO: 跳转到推导页
  }

  /** 点击示例卡 → 模拟器，并带入预设自动填充题目与输入框 */
  const goSimulator = (ex: HomeExample) => {
    Taro.navigateTo({ url: `/pages/simulator/index?${exampleToQuery(ex.preset)}` })
  }

  /** 下拉刷新：预留 */
  usePullDownRefresh(() => {
    // TODO: 重新拉取示例数据
    Taro.stopPullDownRefresh()
  })

  return (
    <View className={styles.page}>
      {/* ===== 顶部标题区 ===== */}
      <View className={styles.header}>
        <View className={styles.headerRow}>
          <View className={styles.avatar}>
            <BeakerIcon />
          </View>
          <View className={styles.addBtn} onClick={addTool}>
            <PlusIcon />
          </View>
        </View>
        <Text className={styles.brandTitle}>化学伴思</Text>
        <Text className={styles.brandSubtitle}>让化学看得见</Text>
      </View>

      {/* ===== 两张主功能大卡 ===== */}
      <View className={styles.entryGrid}>
        {ENTRIES.map((e) => (
          <View
            key={e.key}
            className={styles.entryCard}
            style={{ background: e.bg }}
            onClick={e.key === 'camera' ? onCamera : onDerive}
          >
            <View className={styles.entryGlow} />
            <View className={styles.entryIconCircle}>
              {e.icon ? <ModelIcon name={e.icon} size={22} color="#ffffff" /> : <DeriveIcon />}
            </View>
            <Text className={`${styles.entryTitle} ${e.smallTitle ? styles.entryTitleSmall : ''}`}>
              {e.title}
            </Text>
            <Text className={styles.entryDesc}>{e.desc}</Text>
          </View>
        ))}
      </View>

      {/* ===== 示例区标题 ===== */}
      <View className={styles.sectionHead}>
        <View className={styles.sectionBar} />
        <Text className={styles.sectionTitle}>示例</Text>
        <View className={styles.sectionLink}>
          <Text className={styles.sectionLinkText}>点开看推导过程</Text>
          <ChevronRight />
        </View>
      </View>

      {/* ===== 示例网格 ===== */}
      <View className={styles.exampleGrid}>
        {EXAMPLES.map((ex, i) => (
          <View
            key={ex.id}
            className={styles.exampleCard}
            style={{ width: cardWidth, animationDelay: `${i * 60}ms` }}
            onClick={() => goSimulator(ex)}
          >
            <View className={styles.thumb}>
              <ExampleArt kind={ex.art.kind} liquid={ex.art.liquid} />
              <View className={styles.thumbPlay}>
                <PlayIcon />
              </View>
            </View>
            <Text className={styles.exampleTitle}>{ex.title}</Text>
            <Text className={styles.exampleDesc}>{ex.desc}</Text>
            <Text className={styles.exampleInfo}>
              {ex.stages} 阶段 · {ex.dur}s
            </Text>
          </View>
        ))}
      </View>

      <View className={styles.bottomPad} />

      <TabBar current="home" />
    </View>
  )
}

/* ===================== 行内 SVG 图标（全部代码绘制，无外部资源） ===================== */

function svgProps(size: number, color = '#ffffff', strokeWidth = 2) {
  return {
    width: size,
    height: size,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: color,
    strokeWidth,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    style: { display: 'block' },
  }
}

/** 烧杯线框（头像） */
function BeakerIcon() {
  return (
    <svg {...svgProps(30, '#ffffff')}>
      <path d="M9 3h6v5l4 9a2 2 0 0 1-1.8 3H6.8A2 2 0 0 1 5 17l4-9V3z" />
      <path d="M7.5 9h9" />
    </svg>
  )
}

/** 加号（右上添加按钮） */
function PlusIcon() {
  return (
    <svg {...svgProps(22, '#94A3B8')}>
      <path d="M12 5v14M5 12h14" />
    </svg>
  )
}

/** 三个圆点由箭头串联成 Y 形分叉（右卡图标） */
function DeriveIcon() {
  return (
    <svg {...svgProps(22, '#ffffff')}>
      <circle cx="6" cy="7" r="2.2" fill="#ffffff" stroke="none" />
      <circle cx="18" cy="7" r="2.2" fill="#ffffff" stroke="none" />
      <circle cx="12" cy="18" r="2.2" fill="#ffffff" stroke="none" />
      <path d="M12 18v-7" />
      <path d="M12 11L7.6 7.6" />
      <path d="M12 11l4.4-3.4" />
    </svg>
  )
}

/** 播放三角（缩略图右上） */
function PlayIcon() {
  return (
    <svg width={12} height={12} viewBox="0 0 24 24" style={{ display: 'block' }}>
      <path d="M6 4l14 8-14 8z" fill="#1F2937" />
    </svg>
  )
}

/** 右尖括号（示例区标题右侧） */
function ChevronRight() {
  return (
    <svg {...svgProps(12, '#94A3B8')}>
      <polyline points="9 6 15 12 9 18" />
    </svg>
  )
}
