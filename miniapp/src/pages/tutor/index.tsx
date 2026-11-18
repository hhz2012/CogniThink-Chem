import { useState } from 'react'
import { View, Text, Textarea } from '@tarojs/components'
import Taro, { useLoad } from '@tarojs/taro'
import {
  tutorApi,
  getOrCreateStudentId,
  TUTOR_API_BASE,
  ProblemDto,
  SubmitStepResponse
} from '../../services/tutorApi'
import styles from './index.module.scss'

const STATUS_TEXT: Record<string, string> = {
  AwaitingStep: '等待作答',
  StepAccepted: '步骤正确',
  LocalMistake: '局部待完善',
  KeyMistake: '关键步骤出错',
  EscalatingHint: '连续卡壳',
  Completed: '已通关'
}

const ACTION_TEXT: Record<string, string> = {
  Proceed: '继续下一步',
  SelfCheck: '先自查',
  AskSubQuestion: '回答子问题',
  GiveClue: '查看线索',
  GiveScaffold: '使用脚手架',
  RetryStep: '重新作答',
  FinishLevel: '通关',
  ReviewKnowledge: '复习知识点'
}

const QUERY_TYPE_TEXT: Record<string, string> = {
  Coexistence: '离子共存',
  Identification: '物质检验',
  Inference: '反应推断',
  Equation: '反应方程式',
  Color: '颜色',
  Solubility: '溶解性'
}

const INTERACTION_TYPE_TEXT: Record<string, string> = {
  Precipitation: '生成沉淀',
  GasEvolution: '生成气体',
  WeakElectrolyte: '生成弱电解质',
  Redox: '氧化还原',
  Complexation: '络合反应'
}

const DIFFICULTY_TEXT = ['', '入门', '基础', '进阶', '挑战', '压轴']

export default function TutorPage() {
  const [studentId] = useState(getOrCreateStudentId)
  const [problems, setProblems] = useState<ProblemDto[]>([])
  const [problem, setProblem] = useState<ProblemDto | null>(null)
  const [stepIndex, setStepIndex] = useState(1)
  const [content, setContent] = useState('')
  const [reply, setReply] = useState<SubmitStepResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const loadList = async () => {
    setLoading(true)
    setError('')
    try {
      setProblems(await tutorApi.listProblems())
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载失败')
    } finally {
      setLoading(false)
    }
  }

  const loadProblem = async (id: string) => {
    setLoading(true)
    setError('')
    try {
      const p = await tutorApi.getProblem(id)
      setProblem(p)
      setStepIndex(1)
      setContent('')
      setReply(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : '加载题目失败')
    } finally {
      setLoading(false)
    }
  }

  useLoad((query) => {
    if (query?.id) {
      void loadProblem(query.id)
    } else {
      void loadList()
    }
  })

  const currentStep =
    problem?.steps.find((s) => s.stepIndex === stepIndex) ?? null
  const completed = reply?.completed === true
  const evidence = reply?.chemicalEvidence ?? null
  const evidenceFacts = evidence?.keyFacts.filter((fact) =>
    !evidence.conflicts.some((conflict) => conflict.result === fact) &&
    !evidence.relatedReactions.some((reaction) => fact.includes(reaction.equation))
  ) ?? []

  const onSubmit = async () => {
    if (!problem || loading) return
    const text = content.trim()
    if (!text) {
      Taro.showToast({ title: '先写下你的推理', icon: 'none' })
      return
    }

    setLoading(true)
    try {
      const res = await tutorApi.submitStep({
        studentId,
        problemId: problem.id,
        stepIndex,
        stepContent: text
      })
      setReply(res)

      if (res.completed) {
        setStepIndex(res.currentStep)
      } else if (res.unlockedNextStep) {
        setStepIndex(res.nextStepIndex)
        setContent('')
      }
    } catch (e) {
      Taro.showToast({
        title: e instanceof Error ? e.message : '提交失败',
        icon: 'none'
      })
    } finally {
      setLoading(false)
    }
  }

  // ==================== 题目列表 ====================
  if (!problem) {
    return (
      <View className={styles.page}>
        <View className={styles.header}>
          <Text className={styles.title}>伴思导学</Text>
          <Text className={styles.subtitle}>一次一步，先想再说结论</Text>
        </View>

        {error && <Text className={styles.errorText}>{error}</Text>}
        {loading && <Text className={styles.loadingText}>加载中…</Text>}

        <View className={styles.listWrap}>
          {problems.map((p) => (
            <View key={p.id} className={styles.problemCard} onClick={() => void loadProblem(p.id)}>
              <View className={styles.problemTop}>
                <Text className={styles.problemTitle}>{p.title}</Text>
                <Text className={styles.difficulty}>
                  {DIFFICULTY_TEXT[p.difficulty] ?? `难度 ${p.difficulty}`}
                </Text>
              </View>
              <Text className={styles.problemMeta}>
                化学推断 · {p.totalSteps} 步
                {p.variantOfId ? ' · 变式' : ''}
              </Text>
            </View>
          ))}
        </View>

        <Text className={styles.apiHint}>服务地址：{TUTOR_API_BASE}</Text>
      </View>
    )
  }

  // ==================== 导学作答 ====================
  const progress =
    problem.totalSteps > 0
      ? Math.round(((completed ? problem.totalSteps : stepIndex - 1) / problem.totalSteps) * 100)
      : 0

  return (
    <View className={styles.page}>
      <View className={styles.header}>
        <View className={styles.backBtn} onClick={() => setProblem(null)}>
          <Text className={styles.backIcon}>←</Text>
        </View>
        <View className={styles.headerMain}>
          <Text className={styles.title}>{problem.title}</Text>
          <Text className={styles.subtitle}>
            化学推断 · 共 {problem.totalSteps} 步
          </Text>
        </View>
      </View>

      {/* 题干 */}
      <View className={styles.stemCard}>
        <Text className={styles.stemLabel}>题目</Text>
        <Text className={styles.stemText}>{problem.stem}</Text>
        <View className={styles.kpRow}>
          {problem.knowledgeIds.map((id) => (
            <Text key={id} className={styles.kpTag}>{id}</Text>
          ))}
        </View>
      </View>

      {/* 进度 */}
      <View className={styles.progressRow}>
        <View className={styles.progressBar}>
          <View className={styles.progressFill} style={{ width: `${progress}%` }} />
        </View>
        <Text className={styles.progressText}>
          {completed ? problem.totalSteps : stepIndex}/{problem.totalSteps}
        </Text>
      </View>

      {/* 当前步骤 */}
      {!completed && currentStep && (
        <View className={styles.stepCard}>
          <View className={styles.stepHead}>
            <Text className={styles.stepTitle}>
              第 {currentStep.stepIndex} 步
              {currentStep.isKeyStep && <Text className={styles.keyBadge}>关键</Text>}
            </Text>
            <Text className={styles.stepKp}>{currentStep.knowledgeId}</Text>
          </View>
          <Text className={styles.stepPrompt}>{currentStep.prompt}</Text>

          <Textarea
            className={styles.textarea}
            value={content}
            placeholder="写出你的推理过程，不要只写一个词…"
            maxlength={500}
            onInput={(e) => setContent(e.detail.value)}
          />

          <View
            className={`${styles.submitBtn} ${loading ? styles.submitBtnDisabled : ''}`}
            onClick={() => void onSubmit()}
          >
            {loading ? '判定中…' : '提交这一步'}
          </View>
        </View>
      )}

      {/* 反馈 */}
      {reply && (
        <View className={styles.feedback}>
          <View className={styles.feedbackHead}>
            <Text
              className={`${styles.statusBadge} ${
                reply.status === 'StepAccepted'
                  ? styles.statusOk
                  : reply.status === 'EscalatingHint' || reply.status === 'Completed'
                    ? styles.statusDone
                    : styles.statusWarn
              }`}
            >
              {STATUS_TEXT[reply.status] ?? reply.status}
            </Text>
            <Text className={styles.actionText}>
              {ACTION_TEXT[reply.recommendedAction] ?? reply.recommendedAction}
            </Text>
          </View>

          <Text className={styles.messageText}>{reply.message}</Text>

          {reply.hintText && (
            <View className={styles.hintBox}>
              <Text className={styles.hintLabel}>
                线索 · Lv.{reply.hintLevel}
              </Text>
              <Text className={styles.hintText}>{reply.hintText}</Text>
            </View>
          )}

          {evidence && (
            <View className={styles.evidenceBox}>
              <View className={styles.evidenceHead}>
                <Text className={styles.evidenceTitle}>知识库证据</Text>
                <Text className={styles.evidenceType}>
                  {QUERY_TYPE_TEXT[evidence.queryType] ?? evidence.queryType}
                </Text>
              </View>
              <Text className={styles.evidenceConclusion}>{evidence.conclusion}</Text>

              {evidence.conflicts.map((conflict) => (
                <View key={`${conflict.ions.join('-')}-${conflict.result}`} className={styles.conflictItem}>
                  <Text className={styles.conflictTitle}>
                    {conflict.ions.join(' + ')} · {INTERACTION_TYPE_TEXT[conflict.type] ?? conflict.type}
                  </Text>
                  <Text className={styles.conflictText}>{conflict.result}</Text>
                </View>
              ))}

              {evidenceFacts.map((fact) => (
                <Text key={fact} className={styles.evidenceFact}>· {fact}</Text>
              ))}

              {evidence.relatedReactions.map((reaction) => (
                <View key={reaction.id} className={styles.reactionCard}>
                  <Text className={styles.reactionEquation}>{reaction.equation}</Text>
                  {reaction.conditions.length > 0 && (
                    <Text className={styles.reactionMeta}>条件：{reaction.conditions.join('、')}</Text>
                  )}
                  {reaction.phenomena.length > 0 && (
                    <Text className={styles.reactionMeta}>现象：{reaction.phenomena.join('、')}</Text>
                  )}
                </View>
              ))}
            </View>
          )}

          <View className={styles.metaRow}>
            {reply.errorTag && reply.errorTag !== '无' && (
              <Text className={styles.errorTag}>错因：{reply.errorTag}</Text>
            )}
            {typeof reply.mastery === 'number' && (
              <Text className={styles.mastery}>
                掌握度 {Math.round(reply.mastery * 100)}%
              </Text>
            )}
          </View>

          {reply.weakPrerequisites.length > 0 && (
            <View className={styles.weakBox}>
              <Text className={styles.weakTitle}>建议先补前置</Text>
              {reply.weakPrerequisites.map((w) => (
                <Text key={w.knowledgeId} className={styles.weakItem}>
                  · {w.name}（{Math.round(w.mastery * 100)}%）
                </Text>
              ))}
            </View>
          )}
        </View>
      )}

      {/* 通关 */}
      {completed && reply && (
        <View className={styles.completeCard}>
          <Text className={styles.completeTitle}>🎉 关卡通过</Text>
          <Text className={styles.completeText}>
            你已经走通整条推理链。下面进入同知识点的变式复测。
          </Text>
          {reply.variant && (
            <View className={styles.variantBtn} onClick={() => void loadProblem(reply.variant!.id)}>
              进入变式题：{reply.variant.title}
            </View>
          )}
        </View>
      )}

      <Text className={styles.apiHint}>服务地址：{TUTOR_API_BASE}</Text>
      <View className={styles.bottomPad} />
    </View>
  )
}
