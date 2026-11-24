import { View, Text } from '@tarojs/components'
import classnames from 'classnames'
import type { StepGateState } from '../../types/scaffold'
import styles from './index.module.scss'

export interface StepProgressItem {
  /** 节点内标识：练习页用 A–F 字母，反应详情页用步骤序号 */
  kind: string
  /** 节点下方短标签，如 粒子 / 认官能团 */
  label: string
}

interface Props {
  steps: StepProgressItem[]
  /** 门控状态（练习页用）；preview 模式可省略 */
  states?: StepGateState[]
  /** 当前进行中 / 展开的步下标；无则传 -1 */
  activeIndex: number
  /** 标题（如「氧化还原推理链」），留空则只显示节点 */
  title?: string
  /** 只读预览模式（反应详情页）：全部节点可用态、不显示 ✓/✗，仅高亮当前步 */
  preview?: boolean
}

/**
 * 紧凑浮动进度条：步骤以横向迷你节点 + 连接线展示，
 * 替代大块思路递进图，节省垂直空间；由父容器设 position:sticky 使其滚动时常驻。
 */
export default function StepProgress({
  steps,
  states = [],
  activeIndex,
  title,
  preview = false,
}: Props) {
  const passedCount = states.filter((s) => s === 'passed').length
  const countText = preview ? `共 ${steps.length} 步` : `${passedCount}/${steps.length}`

  return (
    <View className={styles.bar}>
      {(title || steps.length > 0) && (
        <View className={styles.cap}>
          {title ? <Text className={styles.capTitle}>{title}</Text> : null}
          <Text className={styles.capCount}>{countText}</Text>
        </View>
      )}
      <View className={styles.row}>
        {steps.map((s, i) => {
          const st = states[i] ?? 'locked'
          const isActive = i === activeIndex
          const hasConn = i < steps.length - 1

          // 节点外观：preview 全部"可用"，仅当前步高亮；非 preview 按门控态
          const nodeClass = preview
            ? isActive
              ? styles.nodeActive
              : styles.nodeAvail
            : classnames({
                [styles.nodePassed]: st === 'passed',
                [styles.nodeFailed]: st === 'failed',
                [styles.nodeActive]: isActive,
                [styles.nodeLocked]: st === 'locked' && !isActive,
              })
          const textLight = preview
            ? isActive
            : st === 'passed' || st === 'failed' || isActive
          const labelClass = preview
            ? isActive
              ? styles.nodeLabelOn
              : styles.nodeLabelAvail
            : classnames({
                [styles.nodeLabelOn]: st === 'passed' || isActive,
                [styles.nodeLabelDim]: st === 'locked' && !isActive,
              })
          const icon = preview ? s.kind : st === 'passed' ? '✓' : st === 'failed' ? '✗' : s.kind
          const connClass = preview
            ? styles.connAvail
            : st === 'passed' && states[i + 1] === 'passed'
              ? styles.connPassed
              : undefined

          return (
            <View key={s.kind} className={styles.seg}>
              <View className={styles.cell}>
                <View className={classnames(styles.node, nodeClass)}>
                  <Text
                    className={classnames(styles.nodeText, {
                      [styles.nodeTextLight]: textLight,
                    })}
                  >
                    {icon}
                  </Text>
                </View>
                <Text className={classnames(styles.nodeLabel, labelClass)}>{s.label}</Text>
              </View>
              {hasConn && <View className={classnames(styles.conn, connClass)} />}
            </View>
          )
        })}
      </View>
    </View>
  )
}
