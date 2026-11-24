import { View, Text } from '@tarojs/components'
import { ElectrochemSolveData } from '../../types/chem'
import ModelIcon from '../ModelIcon'
import styles from './index.module.scss'

interface Props {
  data: ElectrochemSolveData
  color: string
}

/** 装置类型 → 徽标强调色（与章节主题呼应） */
const TYPE_COLOR: Record<string, string> = {
  原电池: '#16a34a',
  电解池: '#ea580c',
  可充电电池: '#7c3aed',
  燃料电池: '#2563eb',
  金属腐蚀: '#6b7280',
}

/**
 * 电化学专属面板：装置判别卡 → 电极反应表 → 现象与结论 →（计算类）计算过程。
 * 精准映射「判装置 → 写半反应 → 析现象」的思考骨架；所有可选字段缺失时优雅降级。
 */
export default function ElectrochemCellPanel({ data, color }: Props) {
  const typeColor = TYPE_COLOR[data.device.type] ?? color
  const neg = data.electrodes?.negative
  const pos = data.electrodes?.positive

  return (
    <View className={styles.wrap}>
      {/* ① 装置判别 */}
      <View className={styles.deviceCard} style={{ borderColor: `${typeColor}40` }}>
        <View className={styles.deviceTop}>
          <ModelIcon name="battery" size={15} color={typeColor} />
          <Text className={styles.deviceTitle}>装置判别</Text>
          <Text
            className={styles.badge}
            style={{ background: `${typeColor}18`, color: typeColor, borderColor: `${typeColor}55` }}
          >
            {data.device.type}
          </Text>
        </View>
        {data.device.electrolyte && (
          <View className={styles.infoRow}>
            <Text className={styles.infoLabel}>电解质</Text>
            <Text className={styles.infoVal}>{data.device.electrolyte}</Text>
          </View>
        )}
        {data.device.electronFlow && (
          <View className={styles.infoRow}>
            <Text className={styles.infoLabel}>电子流向</Text>
            <Text className={styles.infoVal}>{data.device.electronFlow}</Text>
          </View>
        )}
        {(neg || pos) && (
          <View className={styles.electrodeRow}>
            {neg && (
              <View className={styles.chip} style={{ borderColor: '#dc262655', background: '#dc262612' }}>
                <Text className={styles.chipText} style={{ color: '#dc2626' }}>{neg}</Text>
              </View>
            )}
            {pos && (
              <View className={styles.chip} style={{ borderColor: '#2563eb55', background: '#2563eb12' }}>
                <Text className={styles.chipText} style={{ color: '#2563eb' }}>{pos}</Text>
              </View>
            )}
          </View>
        )}
      </View>

      {/* ② 电极反应表 */}
      {data.halfReactions.length > 0 && (
        <>
          <View className={styles.sectionTitle}>
            <ModelIcon name="zap" size={15} color={color} />
            <Text>电极反应</Text>
          </View>
          <View className={styles.table}>
            <View className={`${styles.row} ${styles.head}`}>
              <Text className={`${styles.col} ${styles.colElec}`}>电极</Text>
              <Text className={`${styles.col} ${styles.colEq}`}>电极反应式</Text>
              <Text className={`${styles.col} ${styles.colRole}`}>类型</Text>
            </View>
            {data.halfReactions.map((h, i) => (
              <View key={i} className={styles.row}>
                <Text className={`${styles.col} ${styles.colElec}`}>{h.electrode}</Text>
                <Text className={`${styles.col} ${styles.colEq}`}>{h.equation}</Text>
                <Text
                  className={`${styles.col} ${styles.colRole}`}
                  style={{ color: h.role === '氧化' ? '#dc2626' : '#2563eb' }}
                >
                  {h.role}
                </Text>
              </View>
            ))}
            {data.totalEquation && (
              <View className={styles.totalRow}>
                <Text className={styles.totalLabel}>总反应</Text>
                <Text className={styles.totalEq}>{data.totalEquation}</Text>
              </View>
            )}
          </View>
        </>
      )}

      {/* ③ 现象与结论 */}
      {((data.phenomena && data.phenomena.length > 0) || data.conclusion) && (
        <View className={styles.sectionTitle}>
          <ModelIcon name="check-circle" size={15} color={color} />
          <Text>现象与结论</Text>
        </View>
      )}
      {data.phenomena && data.phenomena.length > 0 && (
        <View className={styles.phenomena}>
          {data.phenomena.map((p, i) => (
            <View key={i} className={styles.phenomItem}>
              <Text className={styles.phenomDot} style={{ background: color }} />
              <Text className={styles.phenomText}>{p}</Text>
            </View>
          ))}
        </View>
      )}
      {data.conclusion && (
        <View className={styles.conclusion} style={{ borderColor: `${color}55`, background: `${color}10` }}>
          <Text className={styles.conclusionText} style={{ color }}>{data.conclusion}</Text>
        </View>
      )}

      {/* ④ 计算过程（仅计算类题） */}
      {data.calc && (
        <>
          <View className={styles.sectionTitle}>
            <ModelIcon name="layout" size={15} color={color} />
            <Text>计算过程</Text>
          </View>
          <View className={styles.calcBox}>
            <View className={styles.calcRow}>
              <Text className={styles.calcLabel}>已知</Text>
              <Text className={styles.calcVal}>{data.calc.given}</Text>
            </View>
            <View className={styles.calcRow}>
              <Text className={styles.calcLabel}>求解</Text>
              <Text className={styles.calcVal}>{data.calc.target}</Text>
            </View>
            {data.calc.steps.map((s, i) => (
              <View key={i} className={styles.calcStep}>
                <Text className={styles.stepIdx} style={{ background: color }}>{i + 1}</Text>
                <Text className={styles.stepText}>{s}</Text>
              </View>
            ))}
            <View className={styles.calcResult} style={{ borderColor: `${color}55` }}>
              <Text className={styles.resultLabel}>结果</Text>
              <Text className={styles.resultVal} style={{ color }}>{data.calc.result}</Text>
            </View>
          </View>
        </>
      )}
    </View>
  )
}
