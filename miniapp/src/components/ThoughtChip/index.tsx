import { View, Text } from '@tarojs/components'
import { Thought } from '../../types/chem'
import { THOUGHT_META } from '../../utils/storage'
import ModelIcon from '../SolvePanels/ModelIcon'
import type { ModelIconName } from '../../types/chem'
import styles from './index.module.scss'

interface Props {
  thought: Thought
  size?: 'sm' | 'md'
}

/** 思维标签 → 青绿线性图标（统一色系，替代多色 emoji） */
export const THOUGHT_ICON: Record<Thought, ModelIconName> = {
  Conservation: 'scale',
  Structure: 'atom',
  Equilibrium: 'refresh',
  Classification: 'layers',
  ModelCognition: 'puzzle',
}

export default function ThoughtChip({ thought, size = 'md' }: Props) {
  const meta = THOUGHT_META[thought]
  return (
    <View
      className={`${styles.chip} ${size === 'sm' ? styles.sm : styles.md}`}
      style={{ background: `${meta.color}15`, color: meta.color, borderColor: `${meta.color}40` }}
    >
      <ModelIcon name={THOUGHT_ICON[thought]} size={size === 'sm' ? 13 : 15} color={meta.color} />
      <Text>{meta.label}</Text>
    </View>
  )
}
