import Taro from '@tarojs/taro'
import { View, Text } from '@tarojs/components'
import ModelIcon from '../SolvePanels/ModelIcon'
import type { ModelIconName } from '../../types/chem'
import styles from './index.module.scss'

interface TabBarProps {
  current: string
}

const TABS: { key: string; icon: ModelIconName; label: string; path: string }[] = [
  { key: 'home', icon: 'home', label: '首页', path: '/pages/home/index' },
  { key: 'bank', icon: 'book', label: '错题本', path: '/pages/bank/index' },
  { key: 'atlas', icon: 'test-tube', label: '物质速查', path: '/pages/atlas/index' },
  { key: 'mine', icon: 'user', label: '我的', path: '/pages/mine/index' },
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
          <ModelIcon name={t.icon} size={20} className={styles.icon} />
          <Text className={styles.label}>{t.label}</Text>
        </View>
      ))}
    </View>
  )
}
