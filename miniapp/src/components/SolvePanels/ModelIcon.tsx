import type { ReactNode } from 'react'
import { View } from '@tarojs/components'

/**
 * 线性单色图标（currentColor 驱动，颜色由外层 color 控制）。
 * 用于题型解题模型面板，统一图标色系，避免 emoji 多色无法统一的问题。
 * 图标风格：24x24 线性（stroke=currentColor, fill=none），与参考图的清新线性风一致。
 */
export type ModelIconName =
  | 'zap'
  | 'droplet'
  | 'refresh'
  | 'battery'
  | 'test-tube'
  | 'hexagon'
  | 'link'
  | 'check-circle'
  | 'lightbulb'
  | 'layout'
  | 'camera'

const ICONS: Record<ModelIconName, ReactNode> = {
  // 氧化还原 / 电子转移
  zap: <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />,
  // 离子 / 粒子
  droplet: <path d="M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z" />,
  // 化学平衡 / 推演
  refresh: (
    <>
      <polyline points="21 12 21 8 17 8" />
      <path d="M21 8a9 9 0 0 0-15-6.7L3 8" />
      <polyline points="3 12 3 16 7 16" />
      <path d="M3 16a9 9 0 0 0 15 6.7L21 16" />
    </>
  ),
  // 电化学
  battery: (
    <>
      <rect x="2" y="7" width="16" height="10" rx="2" ry="2" />
      <line x1="22" y1="11" x2="22" y2="13" />
    </>
  ),
  // 无机推断
  'test-tube': (
    <>
      <path d="M14.5 2v17.5a3.5 3.5 0 0 1-7 0V2" />
      <path d="M9 2h6" />
      <path d="M9 16h6" />
    </>
  ),
  // 有机化学（分子/六元环）
  hexagon: (
    <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
  ),
  // 守恒关系
  link: (
    <>
      <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
      <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
    </>
  ),
  // 校验要点
  'check-circle': (
    <>
      <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
      <polyline points="22 4 12 14.01 9 11.01" />
    </>
  ),
  // 这类题怎么想（思考）
  lightbulb: (
    <>
      <path d="M9 18h6" />
      <path d="M10 22h4" />
      <path d="M15.09 14c.18-.98.65-1.74 1.41-2.5A4.65 4.65 0 0 0 18 8 6 6 0 0 0 6 8c0 1 .23 2.23 1.5 3.5A4.61 4.61 0 0 1 8.91 14" />
    </>
  ),
  // 完整解题模型
  layout: (
    <>
      <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
      <path d="M3 9h18" />
      <path d="M9 21V9" />
    </>
  ),
  // 拍照识题
  camera: (
    <>
      <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
      <circle cx="12" cy="13" r="4" />
    </>
  ),
}

interface Props {
  name: ModelIconName
  /** 图标边长（px），默认 20 */
  size?: number
  /** 图标颜色，默认 currentColor（由外层 color 控制） */
  color?: string
  className?: string
}

export default function ModelIcon({ name, size = 20, color = 'currentColor', className }: Props) {
  const inner = ICONS[name] ?? ICONS.zap
  return (
    <View
      className={className}
      style={{
        width: `${size}px`,
        height: `${size}px`,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        color,
        lineHeight: 0,
        flexShrink: 0,
      }}
    >
      <svg
        viewBox="0 0 24 24"
        width={size}
        height={size}
        fill="none"
        stroke="currentColor"
        strokeWidth={2}
        strokeLinecap="round"
        strokeLinejoin="round"
        style={{ display: 'block' }}
      >
        {inner}
      </svg>
    </View>
  )
}
