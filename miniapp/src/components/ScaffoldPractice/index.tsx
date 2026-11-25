// A–F 精熟练习组件（Task 8，轴 2 解题侧）
// 学生在 A–F 每步作答 → 确定性引擎校验 → pass 解锁下一步 / fail 锁定当前步并释放提示。
// 门控复用 engine/scaffoldChains（Task 6），校验复用 engine/scaffoldValidators（Task 8），
// 分支结论复用 engine/scaffoldBranches（Task 7）。纯前端，不依赖后端，预览即可走通。

import { useMemo, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import classnames from 'classnames'
import StepProgress from '../StepProgress'
import ThoughtDagFlow from '../ThoughtDagFlow'
import {
  MODULE_CHAINS,
  initialGateStates,
  markPassed,
  markFailed,
  getGateState,
} from '../../engine/scaffoldChains'
import { validateStep, releaseTier } from '../../engine/scaffoldValidators'
import { scaffoldChainToDAG } from '../../engine/thoughtChainDAGs'
import { resolveBranch } from '../../engine/scaffoldBranches'
import { SCAFFOLD_PRACTICES, getScaffoldPractice } from '../../data/scaffoldPractices'
import { BRANCH_EXAMPLES } from '../../data/branchExamples'
import type {
  ScaffoldPractice as Practice,
  ScaffoldReleaseTier,
  StepGateState,
  StudentAnswer,
  StepValidation,
} from '../../types/scaffold'
import styles from './index.module.scss'

interface Props {
  /** 指定练习 id；缺省取第一条 */
  practiceId?: string
  /** 浮动进度条的 sticky top 偏移（避让页面顶部固定栏）；缺省 0 */
  progressTop?: string
}

const STATE_LABEL: Record<StepGateState, string> = {
  locked: '未解锁',
  unlocked: '进行中',
  passed: '已通过',
  failed: '需重试',
}

const KIND_LABEL: Record<string, string> = {
  A: '粒子',
  B: '角色',
  C: '条件',
  D: '排序',
  E: '产物',
  F: '作答',
}

/** 稳定种子化哈希（FNV-1a）：同字符串 → 同数字，不依赖 Math.random，保证幂等 */
const hashSeed = (s: string): number => {
  let h = 2166136261
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i)
    h = Math.imul(h, 16777619)
  }
  return h >>> 0
}

/** 稳定种子化 Fisher–Yates 打乱：同一种子 → 同一排列（切换 practice / 重渲染不串扰、不丢作答） */
const seededShuffle = <T,>(arr: T[], seed: string): T[] => {
  const a = arr.slice()
  let state = hashSeed(seed) || 0x9e3779b9
  const rand = () => {
    state ^= state << 13
    state ^= state >>> 17
    state ^= state << 5
    return (state >>> 0) / 0x100000000
  }
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(rand() * (i + 1))
    ;[a[i], a[j]] = [a[j], a[i]]
  }
  return a
}

/** order 步初始作答：打乱选项源序，并保证初始序 ≠ 正确序，杜绝「不交互直接提交即判对」 */
const initOrderedAnswer = (
  options: { key: string }[],
  correct: string[],
  seed: string,
): string[] => {
  const keys = options.map((o) => o.key)
  if (keys.length <= 1) return keys
  let ordered = seededShuffle(keys, seed)
  // 恰好等于正确序 → 反转；length=2 反转即正确序时 → 手动交换首位（保证至少错位一位）
  const equalsCorrect = (a: string[]) =>
    a.length === correct.length && a.every((k, i) => k === correct[i])
  if (equalsCorrect(ordered)) {
    ordered = ordered.slice().reverse()
    if (equalsCorrect(ordered)) {
      ;[ordered[0], ordered[1]] = [ordered[1], ordered[0]]
    }
  }
  return ordered
}

/** 生成某步的空作答（按真值形态）；降级步 truth 为 null 时返回安全空作答，仅防 getAnswer 解引用崩溃。
 *  order 步用 seed 稳定打乱初始序，杜绝「选项源序 = 正确序 → 不交互即判对」。 */
const emptyAnswer = (
  truth: Practice['steps'][number]['truth'],
  seed = 'order-default',
): StudentAnswer => {
  if (!truth) return { type: 'multi-check', selected: [] }
  if (truth.type === 'multi-check') return { type: 'multi-check', selected: [] }
  if (truth.type === 'single-select') return { type: 'single-select', selected: '' }
  // 此处 truth 已被 TS 收窄为 order 变体：options / correct 均为 string[] 形态
  return { type: 'order', ordered: initOrderedAnswer(truth.options, truth.correct, seed) }
}

export default function ScaffoldPractice({ practiceId, progressTop = '0' }: Props) {
  const [id, setId] = useState<string>(practiceId ?? SCAFFOLD_PRACTICES[0].id)
  const practice = getScaffoldPractice(id) ?? SCAFFOLD_PRACTICES[0]
  const chain = MODULE_CHAINS[practice.module]

  const [gate, setGate] = useState<StepGateState[]>(() => initialGateStates(chain.tasks.length))
  const [answers, setAnswers] = useState<Record<number, StudentAnswer>>({})
  const [validations, setValidations] = useState<Record<number, StepValidation>>({})
  // Task 9：每步「连续错误次数」→ 决定释放档位；通过后清零。
  const [wrongCounts, setWrongCounts] = useState<Record<number, number>>({})
  // Task 9：每步已被学生关闭（渐撤）的档位，关闭后不再渲染该档。
  const [dismissed, setDismissed] = useState<Record<number, ScaffoldReleaseTier[]>>({})
  // Task 9：三级规则抽屉的展开 / 收起状态，缺省展开。
  const [drawerOpen, setDrawerOpen] = useState<Record<number, boolean>>({})
  // 答题前思路递进图（A–F 骨架预览）的展开 / 收起，缺省收起，避免干扰先看步骤。
  const [dagOpen, setDagOpen] = useState(false)

  /** 当前进行中的步：第一个 unlocked 或 failed（failed 仍锁定在当前步，可改答案重试）；-1 表示全部通过 */
  const cur = gate.findIndex((s) => s === 'unlocked' || s === 'failed')
  const allPassed = cur === -1 && gate.every((s) => s === 'passed')

  /** 切换练习时重置门控与作答 */
  const switchPractice = (nextId: string) => {
    if (nextId === id) return
    setId(nextId)
    const p = getScaffoldPractice(nextId) ?? SCAFFOLD_PRACTICES[0]
    const c = MODULE_CHAINS[p.module]
    setGate(initialGateStates(c.tasks.length))
    setAnswers({})
    setValidations({})
    setWrongCounts({})
    setDismissed({})
    setDrawerOpen({})
  }

  const resetAll = () => {
    setGate(initialGateStates(chain.tasks.length))
    setAnswers({})
    setValidations({})
    setWrongCounts({})
    setDismissed({})
    setDrawerOpen({})
  }

  const getAnswer = (i: number): StudentAnswer =>
    answers[i] ?? emptyAnswer(practice.steps[i].truth, `${practice.id}-${practice.steps[i].kind}`)
  const setAnswer = (i: number, ans: StudentAnswer) =>
    setAnswers((prev) => ({ ...prev, [i]: ans }))

  // ---- 作答交互 ----
  const toggleMulti = (i: number, key: string) => {
    const ans = getAnswer(i)
    if (ans.type !== 'multi-check') return
    const has = ans.selected.includes(key)
    setAnswer(i, { type: 'multi-check', selected: has ? ans.selected.filter((k) => k !== key) : [...ans.selected, key] })
  }
  const selectSingle = (i: number, key: string) => {
    setAnswer(i, { type: 'single-select', selected: key })
  }
  const moveOrder = (i: number, idx: number, dir: -1 | 1) => {
    const ans = getAnswer(i)
    if (ans.type !== 'order') return
    const j = idx + dir
    if (j < 0 || j >= ans.ordered.length) return
    const next = ans.ordered.slice()
    ;[next[idx], next[j]] = [next[j], next[idx]]
    setAnswer(i, { type: 'order', ordered: next })
  }

  // ---- 阶梯释放（Task 9）：关闭某档 / 切换规则抽屉展开 ----
  const dismissTier = (i: number, tier: ScaffoldReleaseTier) => {
    setDismissed((prev) => {
      const cur = prev[i] ?? []
      if (cur.includes(tier)) return prev
      return { ...prev, [i]: [...cur, tier] }
    })
  }
  const toggleDrawer = (i: number) => {
    setDrawerOpen((prev) => ({ ...prev, [i]: !(prev[i] ?? true) }))
  }

  // ---- 提交校验 ----
  const onSubmit = (i: number) => {
    // 用 getAnswer 而非 answers[i]：排序题初始顺序可能已正确，用户未交互（没点 ▲/▼）
    // 时 answers[i] 仍为 undefined，直接提交会被误判为「未作答」。getAnswer 会回退到 emptyAnswer 初始值。
    const ans = getAnswer(i)
    const truth = practice.steps[i].truth
    // 降级步无真值：不渲染提交控件，此处防御性返回
    if (!truth) return
    if (!ans) {
      Taro.showToast({ title: '请先作答', icon: 'none' })
      return
    }
    if (ans.type === 'multi-check' && ans.selected.length === 0) {
      Taro.showToast({ title: '请至少勾选一项', icon: 'none' })
      return
    }
    if (ans.type === 'single-select' && !ans.selected) {
      Taro.showToast({ title: '请选择一项', icon: 'none' })
      return
    }
    const v = validateStep(ans, truth)
    setValidations((prev) => ({ ...prev, [i]: v }))
    setGate((prev) => (v.passed ? markPassed(prev, i) : markFailed(prev, i)))
    // Task 9：失败 → 该步连续错误计数 +1（触发下一档释放）；通过 → 清零。
    setWrongCounts((prev) => ({ ...prev, [i]: v.passed ? 0 : (prev[i] ?? 0) + 1 }))
  }

  // ---- 降级步（truth 为 null）：引擎未生成可校验真值，只读揭示规则，点「已理解」解锁下一步 ----
  const markDegradedPassed = (i: number) => {
    setGate((prev) => markPassed(prev, i))
  }

  // 分支结论参照（Task 7）：有关联分支条目时取该分支 A–E 结论，仅在全部通过后展示
  const branchRef = useMemo(() => {
    if (!practice.conditions || !practice.branchEntryId) return null
    const entry = BRANCH_EXAMPLES.find((e) => e.id === practice.branchEntryId)
    return entry ? resolveBranch(entry, practice.conditions) : null
  }, [practice])

  // 思路递进图：A–F 链转 DAG（答题前预览骨架 / 通过后展示执行路径）
  const dag = useMemo(() => scaffoldChainToDAG(chain), [chain])
  // full 模式置灰：gate 中 locked 的步（id 与转换层约定一致：${module}.${kind}）
  const lockedIds = useMemo(
    () =>
      new Set(
        chain.tasks.filter((_, i) => gate[i] === 'locked').map((t) => `${chain.module}.${t.kind}`),
      ),
    [chain, gate],
  )

  // 浮动紧凑进度条的节点：A–F 字母 + 短标签
  const progressItems = chain.tasks.map((t) => ({
    kind: t.kind,
    label: KIND_LABEL[t.kind] ?? t.title,
  }))

  return (
    <View className={styles.wrap}>
      <View className={styles.head}>
        <Text className={styles.title}>A–F 精熟练习</Text>
        <Text className={styles.sub}>一步一校验，答对解锁下一步 · 答错不泄答案</Text>
      </View>

      {/* 练习切换 */}
      {SCAFFOLD_PRACTICES.length > 1 && (
        <View className={styles.practiceTabs}>
          {SCAFFOLD_PRACTICES.map((p) => (
            <View
              key={p.id}
              className={classnames(styles.tab, { [styles.tabActive]: p.id === id })}
              onClick={() => switchPractice(p.id)}
            >
              <Text>{p.title}</Text>
            </View>
          ))}
        </View>
      )}

      <View className={styles.practiceTitle}>
        <Text className={styles.practiceTitleText}>{practice.title}</Text>
        <Text className={styles.practiceModule}>{chain.name}</Text>
      </View>

      {/* 思路递进图：答题前预览 A–F 推导骨架（不泄答案），默认收起 */}
      <View className={styles.dagToggle} onClick={() => setDagOpen((v) => !v)}>
        <Text className={styles.dagToggleText}>{dagOpen ? '收起思路图' : '展开思路图 · A–F 推导骨架'}</Text>
        <Text className={styles.dagToggleCaret}>{dagOpen ? '▲' : '▼'}</Text>
      </View>
      {dagOpen && (
        <View className={styles.dagBlock}>
          <ThoughtDagFlow dag={dag} mode="preview" legendLabels={{ active: '常规步骤', dimmed: '视情况触发' }} />
        </View>
      )}

      {/* 浮动紧凑进度：A–F 迷你节点 + 连接线，答题中常驻导航；与上方思路图并存（职责不同） */}
      <View className={styles.progressWrap} style={{ top: progressTop }}>
        <StepProgress
          steps={progressItems}
          states={gate}
          activeIndex={cur}
          title={chain.name}
        />
      </View>

      {/* 步骤流 */}
      {chain.tasks.map((task, i) => {
        const state = getGateState(gate, i)
        const isActive = i === cur
        const isLocked = state === 'locked'
        const sp = practice.steps[i]
        const truth = sp.truth
        const val = validations[i]
        const tier = releaseTier(wrongCounts[i] ?? 0)
        const dismissedTiers = dismissed[i] ?? []
        return (
          <View
            key={task.kind}
            className={classnames(styles.step, {
              [styles.stepActive]: isActive,
              [styles.stepLocked]: isLocked,
              [styles.stepPassed]: state === 'passed',
              [styles.stepFailed]: state === 'failed',
            })}
          >
            <View className={styles.stepHead}>
              <View
                className={classnames(styles.badge, {
                  [styles.badgeActive]: isActive,
                  [styles.badgePassed]: state === 'passed',
                  [styles.badgeFailed]: state === 'failed',
                  [styles.badgeLocked]: isLocked,
                })}
              >
                <Text className={styles.badgeText}>
                  {state === 'passed' ? '✓' : state === 'failed' ? '✗' : isLocked ? '🔒' : task.kind}
                </Text>
              </View>
              <View className={styles.stepMeta}>
                <Text className={styles.stepTitle}>
                  {task.kind} · {KIND_LABEL[task.kind] ?? task.title}
                </Text>
                <Text className={styles.stepState}>{STATE_LABEL[state]}</Text>
              </View>
            </View>

            {state === 'passed' ? (
              <View className={styles.passedRow}>
                <Text className={styles.passedSummary}>
                  {KIND_LABEL[task.kind] ?? task.title}
                  {val?.expectedLabel ? ` · 正确项：${val.expectedLabel}` : ' · 已通过'}
                </Text>
              </View>
            ) : !isLocked ? (
              <>
                <Text className={styles.stepGoal}>{task.goal}</Text>

                {truth ? (
                  <>
                    {/* 作答控件：仅当前步可交互；passed/failed 展示已选 + 反馈 */}
                <View className={styles.options}>
                  {truth.type === 'multi-check' &&
                    truth.options.map((o) => {
                      const sel = getAnswer(i).type === 'multi-check' ? (getAnswer(i) as { selected: string[] }).selected : []
                      const on = sel.includes(o.key)
                      return (
                        <View
                          key={o.key}
                          className={classnames(styles.option, styles.optionCheck, {
                            [styles.optionOn]: on,
                            [styles.optionDisabled]: !isActive,
                          })}
                          onClick={() => isActive && toggleMulti(i, o.key)}
                        >
                          <Text className={styles.optionBox}>{on ? '✓' : ''}</Text>
                          <Text className={styles.optionLabel}>{o.label}</Text>
                        </View>
                      )
                    })}

                  {truth.type === 'single-select' &&
                    truth.options.map((o) => {
                      const sel = getAnswer(i).type === 'single-select' ? (getAnswer(i) as { selected: string }).selected : ''
                      const on = sel === o.key
                      return (
                        <View
                          key={o.key}
                          className={classnames(styles.option, {
                            [styles.optionOn]: on,
                            [styles.optionDisabled]: !isActive,
                          })}
                          onClick={() => isActive && selectSingle(i, o.key)}
                        >
                          <Text className={styles.optionRadio}>{on ? '●' : '○'}</Text>
                          <Text className={styles.optionLabel}>{o.label}</Text>
                        </View>
                      )
                    })}

                  {truth.type === 'order' && (() => {
                    const ans = getAnswer(i)
                    const ordered = ans.type === 'order' ? ans.ordered : []
                    return ordered.map((key, idx) => {
                      const o = truth.options.find((x) => x.key === key)
                      return (
                        <View key={key} className={classnames(styles.option, styles.optionOrder)}>
                          <Text className={styles.orderRank}>{idx + 1}</Text>
                          <Text className={styles.optionLabel}>{o?.label ?? key}</Text>
                          {isActive && (
                            <View className={styles.orderBtns}>
                              <Text
                                className={classnames(styles.orderBtn, { [styles.orderBtnOff]: idx === 0 })}
                                onClick={() => moveOrder(i, idx, -1)}
                              >
                                ▲
                              </Text>
                              <Text
                                className={classnames(styles.orderBtn, { [styles.orderBtnOff]: idx === ordered.length - 1 })}
                                onClick={() => moveOrder(i, idx, 1)}
                              >
                                ▼
                              </Text>
                            </View>
                          )}
                        </View>
                      )
                    })
                  })()}
                </View>

                {/* 反馈：通过 / 失败；失败时按连续错误次数阶梯释放三档（Task 9），每档可关闭 */}
                {val && (
                  <View
                    className={classnames(styles.feedback, {
                      [styles.feedbackPass]: val.passed,
                      [styles.feedbackFail]: !val.passed,
                    })}
                  >
                    <Text className={styles.feedbackText}>{val.feedback}</Text>

                    {!val.passed && (
                      <View className={styles.releases}>
                        {/* 一级：hint + 辨析选择题（反问，不给结论） */}
                        {tier >= 1 && !dismissedTiers.includes(1) && (
                          <View className={styles.releaseBlock}>
                            <View className={styles.releaseHead}>
                              <Text className={styles.releaseTag}>一级提示 · 错 {wrongCounts[i] ?? 0} 次</Text>
                              <Text className={styles.releaseClose} onClick={() => dismissTier(i, 1)}>关闭</Text>
                            </View>
                            <Text className={styles.releaseHint}>💡 {sp.releases.hint}</Text>
                            <View className={styles.probe}>
                              <Text className={styles.probeStem}>🤔 {sp.releases.probe.stem}</Text>
                              {sp.releases.probe.options.map((opt, oi) => (
                                <Text key={oi} className={styles.probeOpt}>· {opt}</Text>
                              ))}
                            </View>
                          </View>
                        )}

                        {/* 二级：正反对比卡片 */}
                        {tier >= 2 && !dismissedTiers.includes(2) && (
                          <View className={styles.releaseBlock}>
                            <View className={styles.releaseHead}>
                              <Text className={styles.releaseTag}>二级支架 · 正反对比</Text>
                              <Text className={styles.releaseClose} onClick={() => dismissTier(i, 2)}>关闭</Text>
                            </View>
                            <View className={styles.contrast}>
                              <View className={classnames(styles.contrastCol, styles.contrastCorrect)}>
                                <Text className={styles.contrastTitle}>{sp.releases.scaffold.correct.title}</Text>
                                <Text className={styles.contrastDetail}>{sp.releases.scaffold.correct.detail}</Text>
                              </View>
                              <View className={classnames(styles.contrastCol, styles.contrastWrong)}>
                                <Text className={styles.contrastTitle}>{sp.releases.scaffold.wrong.title}</Text>
                                <Text className={styles.contrastDetail}>{sp.releases.scaffold.wrong.detail}</Text>
                              </View>
                            </View>
                          </View>
                        )}

                        {/* 三级：规则讲解抽屉（可展开 / 收起） */}
                        {tier >= 3 && !dismissedTiers.includes(3) && (
                          <View className={styles.releaseBlock}>
                            <View className={styles.releaseHead}>
                              <Text className={styles.releaseTag}>三级揭示 · 规则依据</Text>
                              <Text className={styles.releaseClose} onClick={() => dismissTier(i, 3)}>关闭</Text>
                            </View>
                            <View className={styles.drawer} onClick={() => toggleDrawer(i)}>
                              <Text className={styles.drawerTitle}>📖 {sp.releases.reveal.title}</Text>
                              <Text className={styles.drawerCaret}>
                                {(drawerOpen[i] ?? true) ? '收起 ▲' : '展开 ▼'}
                              </Text>
                            </View>
                            {(drawerOpen[i] ?? true) && (
                              <Text className={styles.drawerBody}>{sp.releases.reveal.body}</Text>
                            )}
                          </View>
                        )}
                      </View>
                    )}

                    {val.passed && val.expectedLabel && (
                      <Text className={styles.expectedText}>正确项：{val.expectedLabel}</Text>
                    )}
                  </View>
                )}

                    {isActive && (
                      <View className={styles.actions}>
                        <View className={styles.submitBtn} onClick={() => onSubmit(i)}>
                          <Text className={styles.submitText}>提交校验</Text>
                        </View>
                      </View>
                    )}
                  </>
                ) : (
                  // 降级步：引擎未生成可校验 correct，只读揭示规则，点「已理解」解锁下一步
                  <View className={styles.releaseBlock}>
                    <Text className={styles.releaseTag}>📖 本步为只读揭示（引擎未生成可校验真值）</Text>
                    <Text className={styles.drawerTitle}>📖 {sp.releases.reveal.title}</Text>
                    <Text className={styles.drawerBody}>{sp.releases.reveal.body}</Text>
                    {isActive && (
                      <View className={styles.actions}>
                        <View className={styles.submitBtn} onClick={() => markDegradedPassed(i)}>
                          <Text className={styles.submitText}>已理解，继续</Text>
                        </View>
                      </View>
                    )}
                  </View>
                )}
              </>
            ) : null}
          </View>
        )
      })}

      {/* 全部通过：展示完整执行路径 + 总结 + 分支结论参照（Task 7） */}
      {allPassed && (
        <View className={styles.complete}>
          <View className={styles.dagBlock}>
            <ThoughtDagFlow dag={dag} mode="full" dimmedIds={lockedIds} legendLabels={{ active: '已通过', dimmed: '未解锁' }} />
          </View>
          <Text className={styles.completeTitle}>🎉 全部通过！</Text>
          <Text className={styles.completeText}>你已独立完成 A–F 六步推理，引擎全程判定，未直接给答案。</Text>
          {branchRef?.matched && branchRef.conclusion.E && (
            <Text className={styles.completeBranch}>本题分支结论（{branchRef.branch?.label}）：{branchRef.conclusion.E}</Text>
          )}
        </View>
      )}

      <View className={styles.footer}>
        <View className={styles.resetBtn} onClick={resetAll}>
          <Text className={styles.resetText}>重新练习</Text>
        </View>
      </View>
    </View>
  )
}
