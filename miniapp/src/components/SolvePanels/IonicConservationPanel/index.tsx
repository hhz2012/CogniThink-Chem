import { View, Text } from '@tarojs/components'
import { IonicSolveData } from '../../types/chem'
import ModelIcon from '../ModelIcon'
import styles from './index.module.scss'

interface Props {
  data: IonicSolveData
  color: string
}

const EQ_COLOR: Record<string, string> = {
  电荷: '#dc2626',
  物料: '#2563eb',
  质子: '#16a34a',
}

/** 离子平衡专属：粒子清单 + 三大守恒式卡片 */
export default function IonicConservationPanel({ data, color }: Props) {
  return (
    <View className={styles.wrap}>
      <View className={styles.sectionTitle}>
        <ModelIcon name="droplet" size={15} color={color} />
        <Text>溶液粒子清单</Text>
      </View>
      <View className={styles.species}>
        {data.species.map((s, i) => (
          <View key={i} className={styles.speciesItem} style={{ borderColor: `${color}40` }}>
            <Text className={styles.speciesFormula}>{s.formula}</Text>
            <Text className={styles.speciesSource}>{s.source}</Text>
          </View>
        ))}
      </View>

      <View className={styles.sectionTitle}>
        <ModelIcon name="link" size={15} color={color} />
        <Text>三大守恒关系</Text>
      </View>
      <View className={styles.equations}>
        {data.equations.map((e, i) => {
          const c = EQ_COLOR[e.type] ?? color
          return (
            <View key={i} className={styles.eqCard} style={{ borderLeftColor: c }}>
              <Text className={styles.eqType} style={{ color: c }}>
                {e.type}守恒
              </Text>
              <Text className={styles.eqExpr}>{e.expression}</Text>
            </View>
          )
        })}
      </View>
    </View>
  )
}
