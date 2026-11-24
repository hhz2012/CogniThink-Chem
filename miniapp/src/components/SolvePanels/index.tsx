import { View, Text } from '@tarojs/components'
import { Challenge } from '../../types/chem'
import { getSolveModel, getPanelKind } from '../../engine/solveModels'
import StepsView from './StepsView'
import RedoxElectronPanel from './RedoxElectronPanel'
import IonicConservationPanel from './IonicConservationPanel'
import EquilibriumShiftPanel from './EquilibriumShiftPanel'
import ElectrochemCellPanel from './ElectrochemCellPanel'
import GenericSolvePanel from './GenericSolvePanel'
import ModelIcon, { ModelIconName } from './ModelIcon'
import styles from './index.module.scss'

/** 解题模型面板统一强调色：品牌青蓝，与 $color-primary 保持一致 */
export const MODEL_ACCENT = '#2EA7C4'

/** 题型 category → 线性图标名（统一青绿色系，替代多色 emoji） */
export const CATEGORY_ICON: Record<string, ModelIconName> = {
  RedoxReaction: 'zap',
  IonicEquilibrium: 'droplet',
  ChemicalEquilibrium: 'refresh',
  Electrochemistry: 'battery',
  InorganicInference: 'test-tube',
  OrganicChemistry: 'hexagon',
}

interface Props {
  challenge: Challenge
  /** preview=答题前只给思考骨架不泄答案；full=答题后给完整可视化 */
  mode: 'preview' | 'full'
}

/**
 * 题型解题模型调度面板：
 * - 按 challenge.category 取模型；
 * - preview 模式仅渲染题型标准步骤（思考骨架），不读 solveData 结论；
 * - full 模式优先渲染题型专属可视化面板（需 solveData 匹配），否则降级通用面板。
 */
export default function SolveModelPanel({ challenge, mode }: Props) {
  const model = getSolveModel(challenge)
  const iconName = CATEGORY_ICON[model.category] ?? 'zap'

  // 答题前：只展示思考骨架，绝不泄露答案
  if (mode === 'preview') {
    return (
      <View className={styles.wrap}>
        <View className={styles.modelHead}>
          <ModelIcon name={iconName} size={22} color={MODEL_ACCENT} />
          <Text className={styles.modelName} style={{ color: MODEL_ACCENT }}>
            {model.name}
          </Text>
          <Text className={styles.modelTag} style={{ background: `${MODEL_ACCENT}15`, color: MODEL_ACCENT }}>
            思考骨架
          </Text>
        </View>
        <StepsView steps={model.steps} color={MODEL_ACCENT} />
      </View>
    )
  }

  // 答题后：尝试渲染专属面板
  const panelKind = getPanelKind(challenge)
  const sd = challenge.solveData

  let specialized = null
  if (panelKind === 'redox-electron' && sd?.kind === 'redox-electron') {
    specialized = <RedoxElectronPanel data={sd} color={MODEL_ACCENT} />
  } else if (panelKind === 'ionic-conservation' && sd?.kind === 'ionic-conservation') {
    specialized = <IonicConservationPanel data={sd} color={MODEL_ACCENT} />
  } else if (panelKind === 'equilibrium-shift' && sd?.kind === 'equilibrium-shift') {
    specialized = <EquilibriumShiftPanel data={sd} color={MODEL_ACCENT} />
  } else if (panelKind === 'electrochem-cell' && sd?.kind === 'electrochem-cell') {
    specialized = <ElectrochemCellPanel data={sd} color={MODEL_ACCENT} />
  }

  return (
    <View className={styles.wrap}>
      <View className={styles.modelHead}>
        <ModelIcon name={iconName} size={22} color={MODEL_ACCENT} />
        <Text className={styles.modelName} style={{ color: MODEL_ACCENT }}>
          {model.name}
        </Text>
      </View>
      {/* 答题前已引导过思考骨架，答后不再重复 StepsView，只展示专属可视化 */}
      {specialized ? (
        specialized
      ) : (
        <GenericSolvePanel model={model} color={MODEL_ACCENT} />
      )}
    </View>
  )
}
