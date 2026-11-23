import { View, Text } from '@tarojs/components'
import { RedoxSolveData } from '../../types/chem'
import ModelIcon from '../ModelIcon'
import styles from './index.module.scss'

interface Props {
  data: RedoxSolveData
  color: string
}

/** 氧化还原专属：半反应电子转移表 + 电子守恒汇总 */
export default function RedoxElectronPanel({ data, color }: Props) {
  const lost = data.halfReactions
    .filter((h) => h.role === '氧化')
    .reduce((sum, h) => sum + h.electrons, 0)
  const gained = data.halfReactions
    .filter((h) => h.role === '还原')
    .reduce((sum, h) => sum + h.electrons, 0)
  const b = data.balance

  return (
    <View className={styles.wrap}>
      <View className={styles.sectionTitle}>
        <ModelIcon name="zap" size={15} color={color} />
        <Text>电子转移分析</Text>
      </View>

      <View className={styles.table}>
        <View className={`${styles.row} ${styles.head}`}>
          <Text className={styles.col}>物质</Text>
          <Text className={styles.col}>价态变化</Text>
          <Text className={styles.col}>得失</Text>
          <Text className={styles.col}>电子数</Text>
        </View>
        {data.halfReactions.map((h, i) => (
          <View key={i} className={styles.row}>
            <Text className={styles.col}>{h.species}</Text>
            <Text className={styles.col}>
              {h.from} → {h.to}
            </Text>
            <Text
              className={styles.col}
              style={{ color: h.role === '氧化' ? '#dc2626' : '#2563eb' }}
            >
              {h.role}
            </Text>
            <Text className={styles.col}>{h.electrons} e⁻</Text>
          </View>
        ))}
      </View>

      <View className={styles.summary} style={{ borderColor: `${color}40` }}>
        <View className={styles.sumItem}>
          <Text className={styles.sumLabel}>失电子总数</Text>
          <Text className={styles.sumVal}>{lost}</Text>
        </View>
        <Text className={styles.sumEq}>=</Text>
        <View className={styles.sumItem}>
          <Text className={styles.sumLabel}>得电子总数</Text>
          <Text className={styles.sumVal}>{gained}</Text>
        </View>
      </View>

      {b && (
        <View className={styles.balanceRow}>
          <View className={styles.balanceItem}>
            <Text className={styles.balanceLabel}>最小公倍数</Text>
            <Text className={styles.balanceVal}>{b.lcm}</Text>
          </View>
          <View className={styles.balanceItem}>
            <Text className={styles.balanceLabel}>系数比</Text>
            <Text className={styles.balanceVal}>{b.ratio}</Text>
          </View>
        </View>
      )}
    </View>
  )
}
