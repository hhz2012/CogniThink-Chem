import { View, Text } from '@tarojs/components'
import { Thought } from '../../types/chem'
import { THOUGHT_META } from '../../utils/storage'
import styles from './index.module.scss'

interface Props {
  thought: Thought
  size?: 'sm' | 'md'
}

export default function ThoughtChip({ thought, size = 'md' }: Props) {
  const meta = THOUGHT_META[thought]
  return (
    <View
      className={`${styles.chip} ${size === 'sm' ? styles.sm : styles.md}`}
      style={{ background: `${meta.color}15`, color: meta.color, borderColor: `${meta.color}40` }}
    >
      <Text>{meta.icon}</Text>
      <Text>{meta.label}</Text>
    </View>
  )
}
