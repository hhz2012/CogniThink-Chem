import { View, Text, ScrollView } from '@tarojs/components'
import { useEffect, useState } from 'react'
import Taro, { useRouter } from '@tarojs/taro'
import classnames from 'classnames'
import {
  REACTIONS_BY_KIND,
  REACTION_KIND_LABEL,
  getReactionById,
} from '../../data/reactions'
import { ReactionEntry, ReactionSolveKind } from '../../types/reaction'
import { getReactionModel } from '../../engine/reactionSolveModels'
import { narrateReaction } from '../../engine/reactionNarration'
import { SolveModelStep } from '../../engine/solveModels'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
import { MODEL_ACCENT } from '../../components/SolvePanels'
import Collapse from '../../components/Collapse'
import StepProgress from '../../components/StepProgress'
import ThoughtDagFlow from '../../components/ThoughtDagFlow'
import { REACTION_SCAFFOLD_CHAIN } from '../../engine/scaffoldChains'
import { scaffoldChainToDAG } from '../../engine/thoughtChainDAGs'
import styles from './index.module.scss'

/** 列表分组顺序：无机基本 → 氧还 → 电极 → 离子平衡 → 热化学 → 有机 */
const KIND_ORDER: ReactionSolveKind[] = [
  'reaction-combination',
  'reaction-decomposition',
  'reaction-displacement',
  'reaction-double-displacement',
  'reaction-combustion',
  'reaction-redox',
  'reaction-electrode',
  'reaction-ion-equilibrium',
  'reaction-thermochemical',
  'reaction-organic-substitution',
  'reaction-organic-addition',
  'reaction-organic-elimination',
  'reaction-organic-oxidation',
  'reaction-organic-reduction',
  'reaction-organic-esterification',
  'reaction-organic-hydrolysis',
  'reaction-organic-polyaddition',
  'reaction-organic-polycondensation',
]

/** 返回首页（清空页面栈，直达首页） */
const backHome = () => Taro.reLaunch({ url: '/pages/home/index' })

/** 返回列表：优先退栈，退不掉则重进列表 */
const backToList = () =>
  Taro.navigateBack({ fail: () => Taro.reLaunch({ url: '/pages/reaction/index' }) })

/* ===================== 列表视图 ===================== */
function ReactionListView() {
  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        <View className={styles.homeBar}>
          <View className={styles.homeBack} onClick={backHome}>
            <ModelIcon name="home" size={16} color={MODEL_ACCENT} />
            <Text className={styles.homeBackText}>返回首页</Text>
          </View>
        </View>

        <View className={styles.sectionHead}>
          <ModelIcon name="hexagon" size={24} color={MODEL_ACCENT} className={styles.sectionIcon} />
          <View className={styles.sectionTitleWrap}>
            <Text className={styles.sectionTitle}>反应推理卡</Text>
            <Text className={styles.sectionSub}>一条反应，拆成分步脚手架；点「提示」看实例化讲解</Text>
          </View>
        </View>

        {KIND_ORDER.map((kind) => {
          const list = REACTIONS_BY_KIND.get(kind) ?? []
          if (list.length === 0) return null
          return (
            <View key={kind} className={styles.group}>
              <View className={styles.groupHead}>
                <Text className={styles.groupTitle}>{REACTION_KIND_LABEL[kind]}</Text>
                <Text className={styles.groupCount}>{list.length} 条</Text>
              </View>
              <View className={styles.groupList}>
                {list.map((e) => (
                  <View
                    key={e.id}
                    className={styles.reactionCard}
                    onClick={() => Taro.navigateTo({ url: `/pages/reaction/index?id=${e.id}` })}
                  >
                    <Text className={styles.reactionEq}>{e.equation}</Text>
                    <View className={styles.reactionMeta}>
                      <Text className={styles.reactionName}>{e.id}</Text>
                      {e.conditions && e.conditions.length > 0 && (
                        <Text className={styles.reactionCond}>{e.conditions.join(' · ')}</Text>
                      )}
                    </View>
                    <Text className={styles.reactionArrow}>›</Text>
                  </View>
                ))}
              </View>
            </View>
          )
        })}

        <View className={styles.bottomPad} />
      </ScrollView>
    </View>
  )
}

/* ===================== 详情视图 ===================== */
function ReactionDetailView({ entry }: { entry: ReactionEntry }) {
  const [expanded, setExpanded] = useState<Record<number, boolean>>({ 1: true })
  const [revealed, setRevealed] = useState<Record<number, boolean>>({})
  const toggleExpand = (idx: number) =>
    setExpanded((prev) => ({ ...prev, [idx]: !prev[idx] }))
  const toggleReveal = (idx: number) =>
    setRevealed((prev) => ({ ...prev, [idx]: !prev[idx] }))

  const solveData = entry.solveData
  // 数据层保证每条都带 solveData；这里仅作类型收窄与兜底
  const reactionKind = (solveData?.kind ?? 'reaction-generic') as ReactionSolveKind
  const model = getReactionModel(reactionKind)
  const kindLabel = REACTION_KIND_LABEL[reactionKind]

  // 紧凑浮动迷你节点条：步骤序号作节点标识，标题作短标签；高亮当前展开步
  const progressSteps = model.steps.map((s) => ({ kind: String(s.index), label: s.title }))
  const activeIdx = model.steps.findIndex((s) => expanded[s.index])

  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        <View className={styles.homeBar}>
          <View className={styles.homeBack} onClick={backToList}>
            <ModelIcon name="home" size={16} color={MODEL_ACCENT} />
            <Text className={styles.homeBackText}>返回列表</Text>
          </View>
        </View>

        {/* 反应信息卡 */}
        <View className={styles.problemCard}>
          <View className={styles.problemHead}>
            <Text className={styles.problemTitle}>{kindLabel}</Text>
            <Text className={styles.problemDiff}>{entry.id}</Text>
          </View>
          <Text className={styles.equation}>{entry.equation}</Text>
          {entry.conditions && entry.conditions.length > 0 && (
            <View className={styles.condRow}>
              {entry.conditions.map((c) => (
                <Text key={c} className={styles.condChip}>{c}</Text>
              ))}
            </View>
          )}
          {entry.phenomenon && (
            <Text className={styles.phenomenon}>现象：{entry.phenomenon}</Text>
          )}
        </View>

        {/* 思路递进：紧凑浮动迷你节点条（只读预览，高亮当前展开步） */}
        <View className={styles.progressWrap}>
          <StepProgress
            steps={progressSteps}
            activeIndex={activeIdx}
            title={`思路递进图 · ${model.name}`}
            preview
          />
        </View>

        {/* 思路递进图：反应推理通用 A–E 脚手架骨架（preview，反应无门控故全高亮） */}
        <View className={styles.dagBlock}>
          <ThoughtDagFlow dag={scaffoldChainToDAG(REACTION_SCAFFOLD_CHAIN)} mode="preview" />
        </View>

        {/* 思考过程启发：每步先想再看，点提示揭示实例化讲解 */}
        <View className={styles.sectionLabelRow}>
          <Text className={styles.sectionLabel}>分步脚手架 · {model.name}</Text>
        </View>

        <View className={styles.stepSections}>
          {model.steps.map((s: SolveModelStep) => {
            const isOpen = !!expanded[s.index]
            const isRevealed = !!revealed[s.index]
            const analyzeText = solveData ? narrateReaction(s, entry) ?? s.analyze : s.analyze
            return (
              <View key={s.index} className={styles.stepSection}>
                <View
                  className={classnames(styles.stepSectionHead, isOpen && styles.stepSectionHeadOpen)}
                  onClick={() => toggleExpand(s.index)}
                >
                  <View className={styles.stepSectionIndex}>{s.index}</View>
                  <View className={styles.stepSectionMain}>
                    <Text className={styles.stepSectionTitle}>{s.title}</Text>
                    <Text className={styles.stepSectionLayer}>{s.layer}</Text>
                  </View>
                  <Text className={classnames(styles.stepSectionChev, isOpen && styles.stepSectionChevOpen)}>▸</Text>
                </View>
                <Collapse open={isOpen}>
                  <View className={styles.stepSectionBody}>
                    <View className={styles.analyzeBlock}>
                      <Text className={styles.analyzeLabel}>这一步分析什么</Text>
                      <Text className={styles.analyzeText}>{s.goal}</Text>
                    </View>
                    <View className={styles.analyzeBlock}>
                      <View className={styles.analyzeLabelRow}>
                        <Text className={styles.analyzeLabel}>怎么分析</Text>
                        <View className={styles.hintBtn} onClick={() => toggleReveal(s.index)}>
                          <Text className={styles.hintBtnText}>{isRevealed ? '收起' : '提示'}</Text>
                        </View>
                      </View>
                      {isRevealed ? (
                        <Text className={styles.analyzeText}>{analyzeText}</Text>
                      ) : (
                        <View className={styles.hintPrompt}>
                          <ModelIcon name="lightbulb" size={16} color="#1C86A1" />
                          <Text className={styles.hintPromptText}>
                            先自己想一想：这一步你会怎么分析这条反应？点右上角「提示」查看实例化讲解。
                          </Text>
                        </View>
                      )}
                    </View>
                  </View>
                </Collapse>
              </View>
            )
          })}
        </View>

        {/* 本类反应自查清单：来自 model.checks（每类反应的校验要点） */}
        {model.checks.length > 0 && (
          <View className={styles.checksBlock}>
            <View className={styles.sectionLabelRow}>
              <Text className={styles.sectionLabel}>本类反应自查清单</Text>
            </View>
            <View className={styles.checksWrap}>
              {model.checks.map((c, i) => (
                <View key={i} className={styles.checkItem}>
                  <Text className={styles.checkItemIdx}>{i + 1}</Text>
                  <Text className={styles.checkItemText}>{c}</Text>
                </View>
              ))}
            </View>
          </View>
        )}

        <View className={styles.bottomPad} />
      </ScrollView>
    </View>
  )
}

/* ===================== 页面入口：按路由参数分发 ===================== */
export default function ReactionPage() {
  const router = useRouter()
  const id = router.params.id as string | undefined
  const entry = id ? getReactionById(id) : undefined

  useEffect(() => {
    if (id && !entry) {
      Taro.showToast({ title: '反应不存在', icon: 'none' })
      const t = setTimeout(() => backToList(), 800)
      return () => clearTimeout(t)
    }
  }, [id, entry])

  // 无效 id：等待 toast 后返回，期间不渲染列表避免闪烁
  if (id && !entry) return <View className={styles.page} />
  if (!entry) return <ReactionListView />
  return <ReactionDetailView entry={entry} />
}
