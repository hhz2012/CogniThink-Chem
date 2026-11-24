import { View, Text } from '@tarojs/components'
import { SolveModelStep } from '../../engine/solveModels'
import styles from './index.module.scss'

interface Props {
  steps: SolveModelStep[]
  color: string
  compact?: boolean
}

const STAGE_LABEL: Record<string, string> = {
  stage1: '阶段一 · 反应推理',
  stage2: '阶段二 · 电子守恒配平',
}

/** 题型标准解题步骤（思考骨架），preview 模式与通用面板共用；步骤带 stage 时按阶段分组渲染 */
export default function StepsView({ steps, color, compact }: Props) {
  return (
    <View className={styles.steps}>
      {steps.map((s, i) => {
        const prevStage = i > 0 ? steps[i - 1].stage : undefined
        const showStageHead = !!s.stage && s.stage !== prevStage
        return (
          <View key={s.index}>
            {showStageHead && s.stage && (
              <View className={styles.stageHead} style={{ borderLeftColor: color, color }}>
                <Text>{STAGE_LABEL[s.stage]}</Text>
              </View>
            )}
            <View className={styles.step}>
              <View className={styles.stepIndex} style={{ background: `${color}15`, color }}>
                <Text>{s.index}</Text>
              </View>
              <View className={styles.stepBody}>
                <Text className={styles.stepTitle}>{s.title}</Text>
                {!compact && <Text className={styles.stepGoal}>{s.goal}</Text>}
              </View>
            </View>
          </View>
        )
      })}
    </View>
  )
}
