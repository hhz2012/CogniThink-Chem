import { View, Text } from '@tarojs/components'
import ModelIcon from '../SolvePanels/ModelIcon'
import type { ModelIconName } from '../../types/chem'
import styles from './index.module.scss'

interface Props {
  /** 主文案，必填 */
  title: string
  /** 副文案，可选 */
  sub?: string
  /** 线性图标（ModelIcon 已有图标集），默认 search */
  icon?: ModelIconName
  className?: string
}

/**
 * 统一空状态：线性图标 + 标题 + 副标题，居中展示。
 * 视觉对齐 pages/atlas 既有的本地空态，抽出为共享组件供各页复用。
 */
export default function Empty({ title, sub, icon = 'search', className }: Props) {
  return (
    <View className={`${styles.wrap} ${className ?? ''}`}>
      <View className={styles.icon}>
        <ModelIcon name={icon} size={48} />
      </View>
      <Text className={styles.title}>{title}</Text>
      {sub ? <Text className={styles.sub}>{sub}</Text> : null}
    </View>
  )
}
