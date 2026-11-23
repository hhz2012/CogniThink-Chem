import { View, Text, ScrollView, Input } from '@tarojs/components'
import { useState, useMemo } from 'react'
import Taro from '@tarojs/taro'
import classnames from 'classnames'
import TabBar from '../../components/TabBar'
import { chapters } from '../../data/challenges'
import { CATEGORY_KNOWLEDGE, knowledgeOfCategory, KnowledgeRef } from '../../data/knowledge'
import { ChemCategory } from '../../types/chem'
import styles from './index.module.scss'

/** 认知层级中文标签 */
const LAYER_LABEL: Record<string, string> = {
  Knowledge: '知识',
  Application: '应用',
  Ability: '能力',
}

/** 全部知识点（带所属分类），用于跨分类搜索 */
const ALL_REFS: { ref: KnowledgeRef; category: ChemCategory }[] = (
  Object.keys(CATEGORY_KNOWLEDGE) as ChemCategory[]
).flatMap((cat) => CATEGORY_KNOWLEDGE[cat].map((ref) => ({ ref, category: cat })))

export default function Atlas() {
  const [active, setActive] = useState<ChemCategory>(chapters[0].category)
  const [keyword, setKeyword] = useState('')

  const kw = keyword.trim().toLowerCase()

  /** 搜索时跨分类检索，否则展示当前分类知识点 */
  const list = useMemo(() => {
    if (kw) {
      return ALL_REFS.filter(({ ref }) => {
        const inName = ref.name.toLowerCase().includes(kw)
        const inSummary = ref.summary.toLowerCase().includes(kw)
        const inKeywords = ref.keywords.some((k) => k.toLowerCase().includes(kw))
        return inName || inSummary || inKeywords
      })
    }
    return knowledgeOfCategory(active).map((ref) => ({ ref, category: active }))
  }, [active, kw])

  const chapterOf = (cat: ChemCategory) => chapters.find((c) => c.category === cat)!

  const openKnowledge = (cat: ChemCategory) => {
    Taro.navigateTo({ url: `/pages/knowledge/index?category=${cat}` })
  }

  const selectCategory = (cat: ChemCategory) => {
    setActive(cat)
    setKeyword('')
  }

  return (
    <View className={styles.page}>
      {/* ===== 顶部标题 + 搜索 ===== */}
      <View className={styles.header}>
        <Text className={styles.title}>物质速查</Text>
        <Text className={styles.subtitle}>6 大专项 · 知识点速查</Text>
        <View className={styles.searchBox}>
          <Text className={styles.searchIcon}>🔍</Text>
          <Input
            className={styles.searchInput}
            placeholder="搜索知识点，如 化合价 / 水解 / 原电池"
            placeholderClass={styles.placeholder}
            value={keyword}
            onInput={(e) => setKeyword(e.detail.value)}
            confirmType="search"
          />
          {keyword ? (
            <Text className={styles.searchClear} onClick={() => setKeyword('')}>
              ✕
            </Text>
          ) : null}
        </View>
      </View>

      {/* ===== 分类切换（横向滚动） ===== */}
      <ScrollView scrollX className={styles.catScroll} showScrollbar={false}>
        <View className={styles.catBar}>
          {chapters.map((ch) => {
            const isActive = active === ch.category && !kw
            return (
              <View
                key={ch.id}
                className={classnames(styles.catTab, { [styles.catTabActive]: isActive })}
                onClick={() => selectCategory(ch.category)}
                style={isActive ? { background: ch.color, borderColor: ch.color } : {}}
              >
                <Text className={styles.catTabText}>{ch.icon} {ch.name}</Text>
              </View>
            )
          })}
        </View>
      </ScrollView>

      {/* ===== 知识点列表 ===== */}
      <ScrollView scrollY className={styles.body}>
        {list.length === 0 ? (
          <View className={styles.empty}>
            <Text className={styles.emptyIcon}>🔍</Text>
            <Text className={styles.emptyTitle}>没有找到相关知识点</Text>
            <Text className={styles.emptySub}>换个关键词试试，或切换分类浏览</Text>
          </View>
        ) : (
          <>
            <Text className={styles.listMeta}>
              {kw ? `搜索结果 · 共 ${list.length} 个知识点` : `${chapterOf(active).name} · 共 ${list.length} 个知识点`}
            </Text>
            <View className={styles.kpList}>
              {list.map(({ ref, category }) => {
                const ch = chapterOf(category)
                return (
                  <View key={ref.id} className={styles.kpCard} onClick={() => openKnowledge(category)}>
                    <View className={styles.kpTop}>
                      <View className={styles.kpIcon} style={{ background: `${ch.color}15` }}>
                        <Text style={{ color: ch.color }}>{ch.icon}</Text>
                      </View>
                      <View className={styles.kpMain}>
                        <Text className={styles.kpName}>{ref.name}</Text>
                        <Text className={styles.kpSummary}>{ref.summary}</Text>
                      </View>
                      <Text className={styles.kpArrow}>›</Text>
                    </View>
                    <View className={styles.kpFoot}>
                      <Text className={styles.layerTag}>{LAYER_LABEL[ref.layer] ?? ref.layer}</Text>
                      <Text className={styles.kpCat} style={{ color: ch.color }}>
                        {ch.name}
                      </Text>
                    </View>
                  </View>
                )
              })}
            </View>
          </>
        )}
        <View className={styles.bottomPad} />
      </ScrollView>

      <TabBar current="atlas" />
    </View>
  )
}
