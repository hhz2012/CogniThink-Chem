import Taro from '@tarojs/taro'
import { View, Text } from '@tarojs/components'
import styles from './index.module.scss'

interface TabBarProps {
  current: string
}

const TABS = [
  { key: 'home', icon: '🏠', label: '首页', path: '/pages/home/index' },
  { key: 'bank', icon: '📕', label: '错题本', path: '/pages/bank/index' },
  { key: 'atlas', icon: '🧪', label: '物质速查', path: '/pages/atlas/index' },
  { key: 'mine', icon: '👤', label: '我的', path: '/pages/mine/index' },
]

export default function TabBar({ current }: TabBarProps) {
  const go = (path: string) => {
    if (path.includes(current.replace('/pages/', '').split('/')[0])) return
    Taro.redirectTo({ url: path })
  }
  return (
    <View className={styles.tabbar}>
      {TABS.map((t) => (
        <View
          key={t.key}
          className={`${styles.tab} ${current === t.key ? styles.active : ''}`}
          onClick={() => go(t.path)}
        >
          <Text className={styles.icon}>{t.icon}</Text>
          <Text className={styles.label}>{t.label}</Text>
        </View>
      ))}
    </View>
  )
}
