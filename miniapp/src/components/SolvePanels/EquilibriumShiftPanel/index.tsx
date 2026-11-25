import { View, Text } from '@tarojs/components'
import { EquilibriumSolveData } from '../../../types/chem'
import ModelIcon from '../ModelIcon'
import styles from './index.module.scss'

interface Props {
  data: EquilibriumSolveData
  color: string
}

const shiftColor = (s: string) =>
  s === '正向' ? '#16a34a' : s === '逆向' ? '#dc2626' : '#64748b'

/** 化学平衡专属：条件-移动-K-转化率 推演表 */
export default function EquilibriumShiftPanel({ data, color }: Props) {
  return (
    <View className={styles.wrap}>
      <View className={styles.sectionTitle}>
        <ModelIcon name="refresh" size={15} color={color} />
        <Text>平衡移动推演</Text>
      </View>
      <View className={styles.table}>
        <View className={`${styles.row} ${styles.head}`}>
          <Text className={styles.col}>影响因素</Text>
          <Text className={styles.col}>改变方式</Text>
          <Text className={styles.col}>移动方向</Text>
          <Text className={styles.col}>K 变化</Text>
          <Text className={styles.col}>转化率</Text>
        </View>
        {data.factors?.map((f, i) => (
          <View key={i} className={styles.row}>
            <Text className={styles.col}>{f.factor}</Text>
            <Text className={styles.col}>{f.change}</Text>
            <Text className={styles.col} style={{ color: shiftColor(f.shift) }}>
              {f.shift}
            </Text>
            <Text className={styles.col}>{f.kChanges}</Text>
            <Text className={styles.col}>{f.conversion ?? '—'}</Text>
          </View>
        ))}
      </View>
    </View>
  )
}
