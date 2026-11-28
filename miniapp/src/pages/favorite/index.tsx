import { View, Text, ScrollView } from '@tarojs/components'
import { useState, useMemo } from 'react'
import Taro, { useDidShow } from '@tarojs/taro'
import classnames from 'classnames'
import TabBar from '../../components/TabBar'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
import Empty from '../../components/Empty'
import { chapters } from '../../data/challenges'
import { CATEGORY_KNOWLEDGE, KnowledgeRef } from '../../data/knowledge'
import { ChemCategory } from '../../types/chem'
import { loadFavorites, toggleFavorite } from '../../utils/storage'
import styles from './index.module.scss'

/** 认知层级中文标签 */
const LAYER_LABEL: Record<string, string> = {
  Knowledge: '知识',
  Application: '应用',
  Ability: '能力',
}

export default function Favorite() {
  const [favIds, setFavIds] = useState<string[]>(loadFavorites())

  // 切回本页时刷新（在知识页收藏/取消后同步）
  useDidShow(() => {
    setFavIds(loadFavorites())
  })

  /** 从全部知识点中筛出已收藏的，按收藏顺序展示 */
  const list = useMemo(() => {
    const set = new Set(favIds)
    const items: { ref: KnowledgeRef; category: ChemCategory }[] = []
    ;(Object.keys(CATEGORY_KNOWLEDGE) as ChemCategory[]).forEach((cat) => {
      CATEGORY_KNOWLEDGE[cat].forEach((ref) => {
        if (set.has(ref.id)) items.push({ ref, category: cat })
      })
    })
    // 按收藏先后排序
    return items.sort((a, b) => favIds.indexOf(a.ref.id) - favIds.indexOf(b.ref.id))
  }, [favIds])

  const chapterOf = (cat: ChemCategory) => chapters.find((c) => c.category === cat)

  const openKnowledge = (cat: ChemCategory) => {
    Taro.navigateTo({ url: `/pages/knowledge/index?category=${cat}` })
  }

  const goSearch = () => {
    Taro.redirectTo({ url: '/pages/atlas/index' })
  }

  const handleToggle = (e: any, id: string, name: string) => {
    e.stopPropagation()
    const next = toggleFavorite(id)
    setFavIds(next)
    Taro.showToast({
      title: next.includes(id) ? `已收藏「${name}」` : '已取消收藏',
      icon: 'none',
    })
  }

  return (
    <View className={styles.page}>
      {/* ===== 顶部标题 ===== */}
      <View className={styles.header}>
        <Text className={styles.title}>收藏</Text>
        <Text className={styles.subtitle}>
          {list.length > 0 ? `已收藏 ${list.length} 个知识点` : '还没有收藏任何内容'}
        </Text>
      </View>

      <ScrollView scrollY className={styles.body}>
        {list.length === 0 ? (
          <View className={styles.empty}>
            <Empty title="还没有收藏" sub="去搜索页找到知识点，点★即可收藏到这里" icon="star" />
            <View className={styles.goBtn} onClick={goSearch}>
              <Text className={styles.goBtnText}>去搜索</Text>
            </View>
          </View>
        ) : (
          <>
            <Text className={styles.listMeta}>共 {list.length} 个收藏</Text>
            <View className={styles.favList}>
              {list.map(({ ref, category }) => {
                const ch = chapterOf(category)
                const fav = favIds.includes(ref.id)
                return (
                  <View key={ref.id} className={styles.favCard} onClick={() => openKnowledge(category)}>
                    <View className={styles.favTop}>
                      <View
                        className={styles.favIcon}
                        style={{ background: ch ? `${ch.color}15` : '#f1f5f9' }}
                      >
                        {ch && <ModelIcon name={ch.icon} size={28} color={ch.color} />}
                      </View>
                      <View className={styles.favMain}>
                        <Text className={styles.favName}>{ref.name}</Text>
                        <Text className={styles.favSummary}>{ref.summary}</Text>
                      </View>
                      <View
                        className={classnames(styles.favBtn, { [styles.favBtnOn]: fav })}
                        onClick={(e) => handleToggle(e, ref.id, ref.name)}
                      >
                        <ModelIcon name="star" size={20} color={fav ? '#f59e0b' : '#cbd5e1'} />
                      </View>
                    </View>
                    <View className={styles.favFoot}>
                      <Text className={styles.layerTag}>{LAYER_LABEL[ref.layer] ?? ref.layer}</Text>
                      {ch && (
                        <Text className={styles.favCat} style={{ color: ch.color }}>
                          {ch.name}
                        </Text>
                      )}
                    </View>
                  </View>
                )
              })}
            </View>
          </>
        )}
        <View className={styles.bottomPad} />
      </ScrollView>

      <TabBar current="favorite" />
    </View>
  )
}
