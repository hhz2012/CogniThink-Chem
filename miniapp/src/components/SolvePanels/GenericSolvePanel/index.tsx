import { View, Text } from '@tarojs/components'
import { SolveModel } from '../../engine/solveModels'
import StepsView from '../StepsView'
import ModelIcon from '../ModelIcon'
import styles from './index.module.scss'

interface Props {
  model: SolveModel
  color?: string
}

/** 通用降级面板：展示题型步骤骨架 + 校验要点（无 solveData 或未实现专属面板时使用） */
export default function GenericSolvePanel({ model, color = '#2EA7C4' }: Props) {
  return (
    <View className={styles.wrap}>
      <StepsView steps={model.steps} color={color} />
      <View className={styles.checks}>
        <View className={styles.checksTitle}>
          <ModelIcon name="check-circle" size={15} color={color} />
          <Text>校验要点</Text>
        </View>
        {model.checks.map((c, i) => (
          <View key={i} className={styles.checkItem}>
            <Text className={styles.checkDot} style={{ background: color }} />
            <Text className={styles.checkText}>{c}</Text>
          </View>
        ))}
      </View>
    </View>
  )
}
