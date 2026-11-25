import { View, Text, Textarea, ScrollView } from '@tarojs/components'
import { useState } from 'react'
import { tutorApi, StudentSolveView, ReasoningNodeDto } from '../../services/tutorApi'
import Collapse from '../../components/Collapse'
import Loading from '../../components/Loading'
import styles from './index.module.scss'

/** 证据强度中文标签（镜像后端 EvidenceLevel） */
const EVIDENCE_LABEL: Record<ReasoningNodeDto['evidence_level'], string> = {
  KnowledgeFact: '知识库事实',
  EngineDerived: '引擎推导',
  Candidate: '候选未唯一',
  Trend: '趋势级（帮助理解，不作判定依据）',
}

export default function Solve() {
  const [text, setText] = useState('')
  const [loading, setLoading] = useState(false)
  const [view, setView] = useState<StudentSolveView | null>(null)
  const [err, setErr] = useState('')
  const [showDag, setShowDag] = useState(false)
  const [showExplain, setShowExplain] = useState(false)

  const run = async () => {
    const q = text.trim()
    if (!q) return
    setLoading(true)
    setErr('')
    setView(null)
    setShowDag(false)
    setShowExplain(false)
    try {
      const v = await tutorApi.solve(q)
      setView(v)
    } catch (e) {
      setErr(e instanceof Error ? e.message : '求解失败，请稍后重试')
    } finally {
      setLoading(false)
    }
  }

  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        {/* ===== 输入区 ===== */}
        <View className={styles.card}>
          <Text className={styles.label}>输入题目（文本）</Text>
          <Textarea
            className={styles.input}
            value={text}
            onInput={(e) => setText(e.detail.value)}
            placeholder='粘贴一道化学题，例如：判断反应 2H₂ + O₂ = 2H₂O 是否配平…'
            maxlength={2000}
            autoHeight
          />
          <View
            className={`${styles.btn} ${loading || !text.trim() ? styles.btnDisabled : ''}`}
            onClick={loading || !text.trim() ? undefined : run}
          >
            <Text className={styles.btnText}>{loading ? '求解中…' : '开始求解'}</Text>
          </View>
          <Text className={styles.hint}>
            结论由化学引擎确定性求解得出；信息不足时不会给出猜测答案。
          </Text>
        </View>

        {err ? (
          <View className={styles.card}>
            <Text className={styles.err}>{err}</Text>
          </View>
        ) : null}

        {loading ? (
          <View className={styles.card}>
            <Loading text="引擎求解中…" />
          </View>
        ) : null}

        {view ? <Result view={view} showDag={showDag} setShowDag={setShowDag} showExplain={showExplain} setShowExplain={setShowExplain} /> : null}
        <View className={styles.bottomPad} />
      </ScrollView>
    </View>
  )
}

// ==================== 结果渲染 ====================

function Result({
  view,
  showDag,
  setShowDag,
  showExplain,
  setShowExplain,
}: {
  view: StudentSolveView
  showDag: boolean
  setShowDag: (v: boolean) => void
  showExplain: boolean
  setShowExplain: (v: boolean) => void
}) {
  const isConfirmed = view.conclusion_state === 'Confirmed'
  const isInsufficient = view.conclusion_state === 'Insufficient'
  const isCandidate = view.conclusion_state === 'CandidateSet'

  return (
    <>
      {/* ===== 结论卡（按状态门控，绝不泄露未采纳结论）===== */}
      <View className={`${styles.card} ${isConfirmed ? styles.cardOk : isInsufficient ? styles.cardInsuf : styles.cardCand}`}>
        <Text className={styles.stateLabel}>
          {isConfirmed ? '✓ 引擎确定结论' : isCandidate ? '? 候选未唯一' : '✕ 信息不足 / 能力缺口'}
        </Text>

        {isConfirmed ? (
          <>
            <Text className={styles.finalResult}>{view.final_result ?? '（无）'}</Text>
            <Text className={styles.disclaimer}>
              已通过引擎确定性校验，仅供参考，不宣称 100% 正确。
            </Text>
          </>
        ) : isInsufficient ? (
          <>
            <Text className={styles.insufficientText}>
              无法给出确定答案。
              {view.constraint_note ? ` ${view.constraint_note}` : ' 题目信息不足或超出当前引擎建模范围。'}
            </Text>
            <Text className={styles.disclaimer}>未采纳结论不会被展示，避免误导。</Text>
          </>
        ) : (
          <>
            <Text className={styles.insufficientText}>存在多个并列可能，暂未唯一确定：</Text>
            {view.candidates.length === 0 ? (
              <Text className={styles.disclaimer}>（当前无候选输出）</Text>
            ) : (
              view.candidates.map((c, i) => (
                <Text key={i} className={styles.candidateItem}>· {c}</Text>
              ))
            )}
          </>
        )}

        <Text className={styles.meta}>
          task_type: {view.task_type}
          {view.result_provenance ? ` · 来源: ${view.result_provenance}` : ''}
        </Text>
      </View>

      {/* ===== 教学话术（分级提示）===== */}
      {view.reply ? (
        <View className={styles.card}>
          <Text className={styles.sectionTitle}>💬 讲解</Text>
          <Text className={styles.reply}>{view.reply}</Text>
        </View>
      ) : null}

      {/* ===== 推理 DAG（渐进揭示；仅 Confirmed 下发）===== */}
      {isConfirmed && view.reasoning_nodes.length > 0 ? (
        <View className={styles.card}>
          <View className={styles.collapseHead} onClick={() => setShowDag(!showDag)}>
            <Text className={styles.sectionTitle}>🔗 推理过程（溯源链）</Text>
            <Text className={`${styles.collapseArrow} ${showDag ? styles.collapseArrowOpen : ''}`}>▸</Text>
          </View>
          <Collapse open={showDag}>
            <View className={styles.dag}>
              {view.reasoning_nodes.map((n) => (
                <View key={n.step} className={styles.dagNode}>
                  <Text className={styles.dagStep}>步骤 {n.step}</Text>
                  <Text className={styles.dagDesc}>{n.description}</Text>
                  {n.premises && n.premises.length > 0 ? (
                    <Text className={styles.dagPremise}>依据：{n.premises.join('；')}</Text>
                  ) : null}
                  {n.conclusion ? <Text className={styles.dagConclusion}>→ {n.conclusion}</Text> : null}
                  <Text className={styles.dagEvidence}>证据强度：{EVIDENCE_LABEL[n.evidence_level]}</Text>
                  {(n.rule_id || n.knowledge_id) ? (
                    <Text className={styles.dagRef}>
                      溯源：{n.rule_id ? `规则 ${n.rule_id}` : ''}
                      {n.rule_id && n.knowledge_id ? ' / ' : ''}
                      {n.knowledge_id ? `知识 ${n.knowledge_id}` : ''}
                    </Text>
                  ) : null}
                </View>
              ))}
            </View>
          </Collapse>
        </View>
      ) : null}

      {/* ===== 趋势级解释（标注不作判定依据；Confirmed/Candidate 下发）===== */}
      {!isInsufficient && view.explanation_nodes.length > 0 ? (
        <View className={styles.card}>
          <View className={styles.collapseHead} onClick={() => setShowExplain(!showExplain)}>
            <Text className={styles.sectionTitle}>🌱 微观解释</Text>
            <Text className={`${styles.collapseArrow} ${showExplain ? styles.collapseArrowOpen : ''}`}>▸</Text>
          </View>
          <Collapse open={showExplain}>
            <View className={styles.dag}>
              <Text className={styles.explainWarn}>趋势级，帮助理解，不作判定依据。</Text>
              {view.explanation_nodes.map((n) => (
                <View key={n.step} className={styles.dagNode}>
                  <Text className={styles.dagStep}>解释 {n.step}</Text>
                  <Text className={styles.dagDesc}>{n.description}</Text>
                  {n.premises && n.premises.length > 0 ? (
                    <Text className={styles.dagPremise}>依据：{n.premises.join('；')}</Text>
                  ) : null}
                </View>
              ))}
            </View>
          </Collapse>
        </View>
      ) : null}
    </>
  )
}
