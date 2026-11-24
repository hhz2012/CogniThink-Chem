import { useState, useEffect, useCallback, useRef } from 'react'

interface UseSpeechReturn {
  /** 当前正在朗读的标识；null 表示空闲 */
  speakingKey: string | null
  /** 当前环境是否支持语音合成（H5 浏览器可用，小程序端通常不支持） */
  supported: boolean
  /** 当前朗读到的字符索引（相对本次朗读文本的偏移）；未朗读时为 -1 */
  boundaryCharIndex: number
  /** 朗读指定文本（会先停止正在进行的朗读）；返回是否成功开始 */
  speak: (key: string, text: string) => boolean
  /** 停止朗读 */
  stop: () => void
}

/**
 * 语音朗读 Hook（基于浏览器 Web Speech API）。
 *
 * - H5 预览环境可用；微信/抖音等小程序端无 window.speechSynthesis 时 supported=false，
 *   调用方应降级为 toast 提示。
 * - 任意时刻只有一个 key 在朗读，切换 key 会自动停掉上一段，避免语音叠加。
 * - 组件卸载或页面隐藏时自动停止，避免副作用残留（跨题/跨页面继续朗读）。
 */
export function useSpeech(): UseSpeechReturn {
  const supported =
    typeof window !== 'undefined' && typeof window.speechSynthesis !== 'undefined'

  const [speakingKey, setSpeakingKey] = useState<string | null>(null)
  const [boundaryCharIndex, setBoundaryCharIndex] = useState(-1)
  const utterRef = useRef<SpeechSynthesisUtterance | null>(null)

  const stop = useCallback(() => {
    if (supported) {
      try {
        window.speechSynthesis.cancel()
      } catch (err) {
        console.error('[useSpeech] cancel error', err)
      }
    }
    utterRef.current = null
    setSpeakingKey(null)
    setBoundaryCharIndex(-1)
  }, [supported])

  const speak = useCallback(
    (key: string, text: string): boolean => {
      if (!supported || !text) return false
      try {
        window.speechSynthesis.cancel()
        setBoundaryCharIndex(-1)
        const utter = new SpeechSynthesisUtterance(text)
        utter.lang = 'zh-CN'
        utter.rate = 1
        utter.pitch = 1
        // 朗读到的字符边界（H5 中文下 Chrome 会触发，Safari/小程序可能不触发 → 降级为不高亮）
        utter.onboundary = (event) => {
          if (typeof event.charIndex === 'number') {
            setBoundaryCharIndex(event.charIndex)
          }
        }
        utter.onend = () => {
          setSpeakingKey((cur) => (cur === key ? null : cur))
          setBoundaryCharIndex(-1)
        }
        utter.onerror = (event) => {
          // 主动 cancel / 切换触发的中断不算故障
          if (event.error !== 'canceled' && event.error !== 'interrupted') {
            console.error('[useSpeech] utterance error', event.error)
          }
          setSpeakingKey((cur) => (cur === key ? null : cur))
          setBoundaryCharIndex(-1)
        }
        utterRef.current = utter
        setSpeakingKey(key)
        window.speechSynthesis.speak(utter)
        return true
      } catch (err) {
        console.error('[useSpeech] speak error', err)
        setSpeakingKey(null)
        setBoundaryCharIndex(-1)
        return false
      }
    },
    [supported],
  )

  // 页面隐藏 / 卸载时停止朗读
  useEffect(() => {
    const onHide = () => stop()
    if (typeof window !== 'undefined') {
      window.addEventListener('pagehide', onHide)
      window.addEventListener('beforeunload', onHide)
    }
    return () => {
      if (typeof window !== 'undefined') {
        window.removeEventListener('pagehide', onHide)
        window.removeEventListener('beforeunload', onHide)
      }
      stop()
    }
  }, [stop])

  return { speakingKey, speak, stop, supported, boundaryCharIndex }
}
