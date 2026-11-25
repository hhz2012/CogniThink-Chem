import { View, Text } from '@tarojs/components'
import styles from './index.module.scss'

interface Props {
  /** 加载文案，默认“加载中…”；传空字符串则只显示旋转环 */
  text?: string
  className?: string
}

/**
 * 统一加载态：纯 CSS 旋转环 + 文案，居中展示。
 * 无外部依赖，尊重 prefers-reduced-motion（减少动效时停止旋转）。
 */
export default function Loading({ text = '加载中…', className }: Props) {
  return (
    <View className={`${styles.wrap} ${className ?? ''}`}>
      <View className={styles.spinner} />
      {text ? <Text className={styles.text}>{text}</Text> : null}
    </View>
  )
}
