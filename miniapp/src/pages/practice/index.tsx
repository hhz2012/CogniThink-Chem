import { View, Text, ScrollView } from '@tarojs/components'
import Taro from '@tarojs/taro'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
import { MODEL_ACCENT } from '../../components/SolvePanels'
import ScaffoldPractice from '../../components/ScaffoldPractice'
import styles from './index.module.scss'

/** A–F 精熟练习独立页：首页不再内嵌答题内容，统一在此作答 */
export default function Practice() {
  const back = () =>
    Taro.navigateBack({ fail: () => Taro.reLaunch({ url: '/pages/home/index' }) })

  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        <View className={styles.homeBar}>
          <View className={styles.homeBack} onClick={back}>
            <ModelIcon name="home" size={16} color={MODEL_ACCENT} />
            <Text className={styles.homeBackText}>返回首页</Text>
          </View>
        </View>

        <ScaffoldPractice progressTop="72rpx" />

        <View className={styles.bottomPad} />
      </ScrollView>
    </View>
  )
}
