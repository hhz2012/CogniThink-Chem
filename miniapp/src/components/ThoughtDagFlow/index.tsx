import { useMemo } from 'react'
import { View, Text } from '@tarojs/components'
import type { Challenge } from '../../types/chem'
import { getCategoryDAG } from '../../engine/thoughtChainDAGs'
import { resolveSkippedNodeIds } from '../../engine/thoughtChainSkip'
import {
  META_PHASE_ORDER,
  type MetaPhase,
  type ThoughtChainDAG,
  type ThoughtChainNode,
  type ThoughtChainScope,
} from '../../types/thoughtChain'
import styles from './index.module.scss'

interface Props {
  /** 题型：取 category DAG，并在 full 模式下按本题实例结论判定跳过 */
  challenge?: Challenge
  /** 直接传入 DAG（优先级最高） */
  dag?: ThoughtChainDAG
  /** full=高亮实际执行 / 已解锁路径；preview=展示题型骨架（不读实例数据、不泄答案） */
  mode?: 'preview' | 'full'
  /** 调用方显式指定置灰节点（如脚手架未解锁步）；full 模式优先于 challenge 派生 */
  dimmedIds?: Set<string>
  /** 图例文案覆盖；缺省沿用 challenge 侧文案 */
  legendLabels?: { active: string; dimmed: string }
}

const NODE_W = 172
const NODE_H = 46
const LAYER_GAP = 56 // 层间垂直间距（节点外）
const NODE_GAP = 18 // 同层节点水平间距
const PAD_X = 24
const PAD_Y = 20

/** 把 scope 转成稳定字符串，用于 SVG marker id 去重（同一页面可能渲染多实例） */
const scopeToKey = (scope: ThoughtChainScope): string => {
  switch (scope.kind) {
    case 'category':
      return `cat-${scope.category}`
    case 'meta':
      return 'meta'
    case 'scaffold':
      return `scaf-${scope.module}`
  }
}

/** 从矩形中心向 (dx,dy) 方向走到矩形边缘的交点 */
const boundaryPoint = (
  cx: number,
  cy: number,
  dx: number,
  dy: number,
  hw: number,
  hh: number,
) => {
  const len = Math.hypot(dx, dy) || 1
  const ux = dx / len
  const uy = dy / len
  const tx = ux !== 0 ? hw / Math.abs(ux) : Infinity
  const ty = uy !== 0 ? hh / Math.abs(uy) : Infinity
  const t = Math.min(tx, ty)
  return { x: cx + ux * t, y: cy + uy * t }
}

export default function ThoughtDagFlow({
  challenge,
  dag,
  mode = 'full',
  dimmedIds,
  legendLabels,
}: Props) {
  // 取数优先级：dag > challenge
  const resolvedDag = useMemo<ThoughtChainDAG | null>(
    () => dag ?? (challenge ? getCategoryDAG(challenge.category) : null),
    [dag, challenge],
  )

  const scopeKey = resolvedDag ? scopeToKey(resolvedDag.scope) : 'x'
  const arrowId = `tdf-arrow-${scopeKey}-${mode}`
  const arrowSkipId = `tdf-arrow-skip-${scopeKey}-${mode}`

  const { layers, width, height, nodePos, edges, skipped } = useMemo(() => {
    const empty = {
      layers: [] as ThoughtChainNode[][],
      width: 0,
      height: 0,
      nodePos: new Map<string, { x: number; y: number }>(),
      edges: [] as { from: string; to: string; skipped: boolean }[],
      skipped: new Set<string>(),
    }
    if (!resolvedDag) return empty
    const nodes = resolvedDag.nodes ?? []
    if (nodes.length === 0) return empty

    // full 模式置灰：调用方显式 dimmedIds 优先，否则按 challenge 实例结论派生；preview 恒空
    const sk =
      mode === 'full'
        ? dimmedIds ?? (challenge ? resolveSkippedNodeIds(challenge) : new Set<string>())
        : new Set<string>()

    // 布局自适应：
    //   有 metaPhase → 按阶段横向分层（题型侧：每层节点并排，层间垂直）；
    //   无 metaPhase → 纯线性链（外部传入的 DAG），每节点独占一行 → 单列垂直堆叠。
    let layers: ThoughtChainNode[][]
    if (nodes.some((n) => n.metaPhase)) {
      const layerMap = new Map<MetaPhase | 'none', ThoughtChainNode[]>()
      for (const n of nodes) {
        const key = (n.metaPhase ?? 'none') as MetaPhase | 'none'
        if (!layerMap.has(key)) layerMap.set(key, [])
        layerMap.get(key)!.push(n)
      }
      const order: (MetaPhase | 'none')[] = [...META_PHASE_ORDER, 'none']
      layers = order.filter((k) => layerMap.has(k)).map((k) => layerMap.get(k)!)
    } else {
      layers = nodes.map((n) => [n])
    }

    // 计算布局坐标（两种布局共用：每层居中、层间垂直堆叠）
    let maxRowW = 0
    const nodePos = new Map<string, { x: number; y: number }>()
    let cursorY = PAD_Y
    for (const row of layers) {
      const rowW = row.length * NODE_W + Math.max(0, row.length - 1) * NODE_GAP
      if (rowW > maxRowW) maxRowW = rowW
      const y = cursorY + NODE_H / 2
      row.forEach((n, j) => {
        const x =
          PAD_X +
          rowW / 2 +
          (j - (row.length - 1) / 2) * (NODE_W + NODE_GAP)
        nodePos.set(n.id, { x, y })
      })
      cursorY += NODE_H + LAYER_GAP
    }
    const width = PAD_X * 2 + maxRowW
    const height = cursorY - LAYER_GAP + PAD_Y

    // 边：跳过 / 未解锁节点相关的边整体灰虚
    const byId = new Map(nodes.map((n) => [n.id, n]))
    const edges: { from: string; to: string; skipped: boolean }[] = []
    for (const n of nodes) {
      for (const dep of n.dependsOn) {
        if (!byId.has(dep)) continue
        edges.push({ from: dep, to: n.id, skipped: sk.has(n.id) || sk.has(dep) })
      }
    }
    return { layers, width, height, nodePos, edges, skipped: sk }
  }, [resolvedDag, mode, challenge, dimmedIds])

  if (layers.length === 0 || !resolvedDag) return null

  // preview：带 skipWhen 的节点统一标为"视情况触发"（不读实例数据）
  const isDimmed = (n: ThoughtChainNode) =>
    mode === 'full' ? skipped.has(n.id) : !!n.skipWhen

  const activeLabel = legendLabels?.active ?? (mode === 'full' ? '本题执行' : '常规步骤')
  const dimmedLabel = legendLabels?.dimmed ?? (mode === 'full' ? '本题跳过' : '视情况触发')

  return (
    <View className={styles.wrap}>
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className={styles.svg}
        style={{
          aspectRatio: `${width} / ${height}`,
          width: 'auto',
          height: 'auto',
          maxHeight: '600px',
          flex: 'none',
        }}
        preserveAspectRatio="xMidYMid meet"
        role="img"
        aria-label={`${resolvedDag.name} · 思路递进图`}
      >
        <defs>
          <marker
            id={arrowId}
            viewBox="0 0 10 10"
            refX="8"
            refY="5"
            markerWidth="6"
            markerHeight="6"
            orient="auto-start-reverse"
          >
            <path d="M0,0 L10,5 L0,10 z" className={styles.arrowActive} />
          </marker>
          <marker
            id={arrowSkipId}
            viewBox="0 0 10 10"
            refX="8"
            refY="5"
            markerWidth="6"
            markerHeight="6"
            orient="auto-start-reverse"
          >
            <path d="M0,0 L10,5 L0,10 z" className={styles.arrowSkip} />
          </marker>
        </defs>

        {edges.map((e, i) => {
          const a = nodePos.get(e.from)
          const b = nodePos.get(e.to)
          if (!a || !b) return null
          const dx = b.x - a.x
          const dy = b.y - a.y
          const p1 = boundaryPoint(a.x, a.y, dx, dy, NODE_W / 2, NODE_H / 2)
          const p2 = boundaryPoint(b.x, b.y, -dx, -dy, NODE_W / 2, NODE_H / 2)
          return (
            <line
              key={i}
              x1={p1.x}
              y1={p1.y}
              x2={p2.x}
              y2={p2.y}
              className={e.skipped ? styles.edgeSkip : styles.edgeActive}
              markerEnd={e.skipped ? `url(#${arrowSkipId})` : `url(#${arrowId})`}
            />
          )
        })}

        {layers.flat().map((n) => {
          const p = nodePos.get(n.id)
          if (!p) return null
          const dimmed = isDimmed(n)
          const isMeta = n.stage === '元骨架'
          return (
            <g key={n.id}>
              <rect
                x={p.x - NODE_W / 2}
                y={p.y - NODE_H / 2}
                width={NODE_W}
                height={NODE_H}
                rx={isMeta ? NODE_H / 2 : 9}
                className={
                  dimmed ? styles.nodeSkip : isMeta ? styles.nodeMeta : styles.nodeActive
                }
              />
              <text
                x={p.x}
                y={p.y + 1}
                textAnchor="middle"
                dominantBaseline="middle"
                className={
                  dimmed
                    ? styles.nodeLabelSkip
                    : isMeta
                      ? styles.nodeLabelMeta
                      : styles.nodeLabelActive
                }
              >
                {n.title}
              </text>
            </g>
          )
        })}
      </svg>

      <View className={styles.legend}>
        <View className={styles.legendItem}>
          <View className={`${styles.legendSwatch} ${styles.legendActive}`} />
          <Text>{activeLabel}</Text>
        </View>
        <View className={styles.legendItem}>
          <View className={`${styles.legendSwatch} ${styles.legendSkip}`} />
          <Text>{dimmedLabel}</Text>
        </View>
      </View>
    </View>
  )
}
