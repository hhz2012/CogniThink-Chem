import { View, Text } from '@tarojs/components'
import { SolveModelStep } from '../../engine/solveModels'
import styles from './index.module.scss'

interface Props {
  steps: SolveModelStep[]
  color: string
  compact?: boolean
}

/** 题型标准解题步骤（思考骨架），preview 模式与通用面板共用 */
export default function StepsView({ steps, color, compact }: Props) {
  return (
    <View className={styles.steps}>
      {steps.map((s) => (
        <View key={s.index} className={styles.step}>
          <View className={styles.stepIndex} style={{ background: `${color}15`, color }}>
            <Text>{s.index}</Text>
          </View>
          <View className={styles.stepBody}>
            <Text className={styles.stepTitle}>{s.title}</Text>
            {!compact && <Text className={styles.stepGoal}>{s.goal}</Text>}
          </View>
        </View>
      ))}
    </View>
  )
}
