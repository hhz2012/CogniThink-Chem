import { View } from '@tarojs/components'
import type { ReactNode } from 'react'
import { useEffect, useRef, useState } from 'react'
import styles from './index.module.scss'

/** 默认缓动曲线：easeOutQuint，末端利落顺滑 */
const DEFAULT_EASING = 'cubic-bezier(0.22, 1, 0.36, 1)'

interface CollapseProps {
  /** 是否展开 */
  open: boolean
  children: ReactNode
  /** 过渡时长（ms），默认 300 */
  duration?: number
  /** 缓动曲线，默认 easeOutQuint；可传 'linear' | 'ease' 或任意 cubic-bezier */
  easing?: string
  /** 内容是否叠加淡入（在高度卷帘之上），默认 false 保持纯卷帘 */
  fade?: boolean
  /** 自定义类名，附加到动画外壳 */
  className?: string
}

/**
 * 通用折叠容器：用 grid-template-rows 0fr→1fr 实现高度过渡动画，
 * 无需 JS 测量内容高度，跨 h5/小程序 webview 兼容。
 *
 * 用法：用 <Collapse open={flag}> 包裹原有内容，替代 `{flag && <View>}`，
 * 保留内部原 View 结构与 className，仅在外层提供动画外壳（最小侵入）。
 *
 * 可访问性：收起时内容区 aria-hidden（全端）+ inert（h5），读屏与键盘跳过隐藏内容；
 * 尊重系统 prefers-reduced-motion（见 index.module.scss）。
 */
export default function Collapse({
  open,
  children,
  duration = 300,
  easing = DEFAULT_EASING,
  fade = false,
  className = '',
}: CollapseProps) {
  // 首次挂载时禁用过渡：若初始 open，直接展开不播放入场动画
  const [ready, setReady] = useState(false)
  const bodyRef = useRef<any>(null)

  useEffect(() => {
    const timer = setTimeout(() => setReady(true), 0)
    return () => clearTimeout(timer)
  }, [])

  // 收起时标记 inert（仅 h5）：阻止键盘聚焦隐藏内容；读屏跳过由 aria-hidden 全端承担
  useEffect(() => {
    if (process.env.TARO_ENV !== 'h5') return
    const el = bodyRef.current
    if (!el) return
    if (!open) el.setAttribute('inert', '')
    else el.removeAttribute('inert')
  }, [open])

  const shellStyle = {
    transitionDuration: ready ? `${duration}ms` : '0ms',
    transitionTimingFunction: easing,
  }

  return (
    <View
      className={`${styles.collapse} ${open ? styles.open : ''} ${className}`.trim()}
      style={shellStyle}
    >
      <View
        ref={bodyRef}
        className={styles.body}
        aria-hidden={open ? undefined : 'true'}
      >
        {fade ? (
          <View
            className={styles.fadeContent}
            style={{
              opacity: open ? 1 : 0,
              transitionProperty: 'opacity',
              transitionDuration: ready ? `${Math.min(duration, 220)}ms` : '0ms',
              transitionTimingFunction: easing,
              // 展开时略延迟，让高度卷帘先启动再淡入；收起时立即淡出
              transitionDelay: open && ready ? `${Math.round(duration * 0.15)}ms` : '0ms',
            }}
          >
            {children}
          </View>
        ) : (
          children
        )}
      </View>
    </View>
  )
}
