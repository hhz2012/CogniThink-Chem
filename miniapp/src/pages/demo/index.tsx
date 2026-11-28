import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import ExampleArt from '../../components/ExampleArt'
import { EXAMPLES } from '../../data/homeExamples'
import styles from './index.module.scss'

/** H5 下 router.params 不会自动 decode，自行安全解码 */
function safeDecode(v?: string): string {
  if (v === undefined || v === null) return ''
  const s = String(v)
  if (!s.includes('%')) return s
  try {
    return decodeURIComponent(s)
  } catch {
    return s
  }
}

export default function Demo() {
  const raw = (() => {
    try {
      return (Taro.getCurrentInstance().router?.params as Record<string, string>) || {}
    } catch {
      return {} as Record<string, string>
    }
  })()
  const id = safeDecode(raw.id)
  const ex = EXAMPLES.find((e) => e.id === id)

  if (!ex) {
    return (
      <View className={styles.page}>
        <Text className={styles.empty}>未找到该示例</Text>
      </View>
    )
  }

  return (
    <View className={styles.page}>
      <View className={styles.hero}>
        <ExampleArt kind={ex.art.kind} liquid={ex.art.liquid} />
      </View>

      <View className={styles.head}>
        <Text className={styles.title}>{ex.title}</Text>
        <View className={styles.tag}>
          <Text className={styles.tagText}>{ex.tag}</Text>
        </View>
      </View>
      <Text className={styles.desc}>{ex.desc}</Text>

      <View className={styles.metaRow}>
        <Text className={styles.meta}>{ex.stages} 阶段</Text>
        <Text className={styles.metaDot}>·</Text>
        <Text className={styles.meta}>{ex.dur}s 演示</Text>
      </View>

      <View className={styles.card}>
        <Text className={styles.cardLabel}>反应方程式</Text>
        <Text className={styles.eq}>{ex.eq}</Text>
      </View>
    </View>
  )
}
