import { View, Text, Textarea, ScrollView, Image } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { useState, useRef, useEffect } from 'react'
import { tutorApi, StudentSolveView, ReasoningNodeDto, ReactionTraceSnapshotDto } from '../../services/tutorApi'
import Collapse from '../../components/Collapse'
import Loading from '../../components/Loading'
import ModelIcon from '../../components/SolvePanels/ModelIcon'
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
  const [photoPath, setPhotoPath] = useState('')
  const [ocrLoading, setOcrLoading] = useState(false)

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

  // 拍照/选图 → OCR 抽题干 → 回填可编辑文本框（用户核对后再点“开始求解”）
  const pickAndOcr = async () => {
    try {
      const res = await Taro.chooseImage({
        count: 1,
        sourceType: ['album', 'camera'],
        sizeType: ['compressed']
      })
      const path = res.tempFilePaths?.[0]
      if (!path) return
      setPhotoPath(path)
      setOcrLoading(true)
      setErr('')
      try {
        const { text: extracted, available } = await tutorApi.ocrPhoto(path)
        if (!available) setErr('后端未配置视觉模型，无法识别图片')
        if (extracted) setText(extracted)
      } catch (e) {
        setErr(e instanceof Error ? e.message : '图片识别失败，请重试')
      } finally {
        setOcrLoading(false)
      }
    } catch {
      /* 用户取消选图，忽略 */
    }
  }

  // 拍照/选图 → 一键 OCR + 求解（快速尝鲜，建议核对 OCR 文本）
  const pickAndSolve = async () => {
    try {
      const res = await Taro.chooseImage({
        count: 1,
        sourceType: ['album', 'camera'],
        sizeType: ['compressed']
      })
      const path = res.tempFilePaths?.[0]
      if (!path) return
      setPhotoPath(path)
      setLoading(true)
      setErr('')
      setView(null)
      setShowDag(false)
      setShowExplain(false)
      try {
        const { view: v, ocrText } = await tutorApi.solvePhoto(path)
        setView(v)
        if (ocrText) setText(ocrText)
      } catch (e) {
        setErr(e instanceof Error ? e.message : '拍照求解失败，请稍后重试')
      } finally {
        setLoading(false)
      }
    } catch {
      /* 用户取消选图，忽略 */
    }
  }

  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        {/* ===== 输入区 ===== */}
        <View className={styles.card}>
          <Text className={styles.label}>输入题目（文本 / 拍照）</Text>
          <View
            className={`${styles.photoPrimary} ${loading ? styles.photoBtnDisabled : ''}`}
            onClick={loading ? undefined : pickAndSolve}
          >
            <ModelIcon name="camera" size={20} color="#fff" />
            <Text className={styles.photoPrimaryText}>{loading ? '求解中…' : '拍照讲题'}</Text>
          </View>
          {photoPath ? (
            <View className={styles.preview}>
              <Image className={styles.previewImg} src={photoPath} mode="aspectFit" />
              <Text
                className={styles.previewClear}
                onClick={() => { setPhotoPath(''); setErr('') }}
              >清除图片</Text>
            </View>
          ) : null}
          <Textarea
            className={styles.input}
            value={text}
            onInput={(e) => setText(e.detail.value)}
            placeholder='粘贴或输入一道化学题…'
            maxlength={2000}
            autoHeight
          />
          <Text
            className={`${styles.ocrLink} ${ocrLoading || loading ? styles.ocrLinkDisabled : ''}`}
            onClick={ocrLoading || loading ? undefined : pickAndOcr}
          >{ocrLoading ? '识别中…' : '先拍照识别，再编辑文本 ›'}</Text>
          <View
            className={`${styles.btn} ${loading || !text.trim() ? styles.btnDisabled : ''}`}
            onClick={loading || !text.trim() ? undefined : run}
          >
            <Text className={styles.btnText}>{loading ? '求解中…' : '开始求解'}</Text>
          </View>
          <Text className={styles.hint}>结论由引擎确定性求解，信息不足时不会猜测，仅供参考。</Text>
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
  const trace = view.structured?.reaction_trace ?? null

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

      </View>

      {/* ===== 解题过程 + 讲解（合并为一张卡）===== */}
      {(isConfirmed && view.derivation_steps && view.derivation_steps.length > 0) || view.reply ? (
        <View className={styles.card}>
          <View className={styles.sectionTitleRow}>
            <ModelIcon name="layers" size={18} color="#2EA7C4" />
            <Text className={styles.sectionTitle}>解题过程</Text>
          </View>
          {isConfirmed && view.derivation_steps && view.derivation_steps.length > 0 ? (
            <View className={styles.steps}>
              {view.derivation_steps.map((s, i) => (
                <View key={i} className={styles.stepItem}>
                  <Text className={styles.stepIdx}>{i + 1}</Text>
                  <Text className={styles.stepText}>{s}</Text>
                </View>
              ))}
            </View>
          ) : null}
          {view.reply ? (
            <Text className={`${styles.reply} ${isConfirmed && view.derivation_steps?.length ? styles.replyDivider : ''}`}>{view.reply}</Text>
          ) : null}
        </View>
      ) : null}

      {/* ===== 推理 DAG（渐进揭示；仅 Confirmed 下发）===== */}
      {isConfirmed && view.reasoning_nodes.length > 0 ? (
        <View className={styles.card}>
          <View className={styles.collapseHead} onClick={() => setShowDag(!showDag)}>
            <View className={styles.sectionTitleRow}>
              <ModelIcon name="link" size={18} color="#2EA7C4" />
              <Text className={styles.sectionTitle}>推理过程（溯源链）</Text>
            </View>
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
            <View className={styles.sectionTitleRow}>
              <ModelIcon name="atom" size={18} color="#2EA7C4" />
              <Text className={styles.sectionTitle}>微观解释</Text>
            </View>
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

      {/* ===== 结构化辅助数据（数值/守恒/pH；加性，与判分无关；反应轨迹由下方模拟器卡片承载）===== */}
      {view.structured &&
      (view.structured.numeric || view.structured.aqueous || view.structured.equilibrium) ? (
        <View className={styles.card}>
          <View className={styles.sectionTitleRow}>
            <ModelIcon name="wrench" size={18} color="#2EA7C4" />
            <Text className={styles.sectionTitle}>结构化辅助数据</Text>
          </View>
          {!isConfirmed ? (
            <Text className={styles.disclaimer}>
              以下为引擎对题给数据的辅助计算结果，不作最终判定结论，仅供参考。
            </Text>
          ) : null}
          <View className={styles.dag}>
            {view.structured.numeric ? (
              <View className={styles.dagNode}>
                <Text className={styles.dagStep}>定量计算</Text>
                <Text className={styles.dagDesc}>
                  {view.structured.numeric.formula ? `${view.structured.numeric.formula} → ` : ''}
                  {view.structured.numeric.value ?? '-'}
                  {view.structured.numeric.unit ? ` ${view.structured.numeric.unit}` : ''}
                  {view.structured.numeric.species ? `（${view.structured.numeric.species}）` : ''}
                </Text>
              </View>
            ) : null}
            {view.structured.aqueous ? (
              <View className={styles.dagNode}>
                <Text className={styles.dagStep}>水相离子平衡</Text>
                {view.structured.aqueous.ph != null ? (
                  <Text className={styles.dagDesc}>pH ≈ {view.structured.aqueous.ph}</Text>
                ) : null}
                {view.structured.aqueous.acidity_verdict ? (
                  <Text className={styles.dagDesc}>{view.structured.aqueous.acidity_verdict}</Text>
                ) : null}
                {view.structured.aqueous.ordering.length > 0 ? (
                  <Text className={styles.dagDesc}>排序：{view.structured.aqueous.ordering.join(' > ')}</Text>
                ) : null}
                {view.structured.aqueous.conservation_equations.length > 0
                  ? view.structured.aqueous.conservation_equations.map((e, i) => (
                      <Text key={i} className={styles.dagDesc}>守恒：{e}</Text>
                    ))
                  : null}
              </View>
            ) : null}
            {view.structured.equilibrium &&
            (view.structured.equilibrium.ksp_residual_concentration != null ||
              view.structured.equilibrium.conversion_constant_k ||
              view.structured.equilibrium.conversion_proceeds != null) ? (
              <View className={styles.dagNode}>
                <Text className={styles.dagStep}>沉淀溶解平衡</Text>
                {view.structured.equilibrium.ksp_residual_concentration != null ? (
                  <Text className={styles.dagDesc}>
                    Ksp 残留浓度：{view.structured.equilibrium.ksp_residual_concentration}
                    {view.structured.equilibrium.ksp_residual_ion
                      ? `（${view.structured.equilibrium.ksp_residual_ion}）`
                      : ''}
                  </Text>
                ) : null}
                {view.structured.equilibrium.conversion_constant_k ? (
                  <Text className={styles.dagDesc}>
                    转化常数 K：{view.structured.equilibrium.conversion_constant_k}
                  </Text>
                ) : null}
                {view.structured.equilibrium.conversion_proceeds != null ? (
                  <Text className={styles.dagDesc}>
                    沉淀转化自发：{view.structured.equilibrium.conversion_proceeds ? '是' : '否'}
                  </Text>
                ) : null}
              </View>
            ) : null}
            {view.structured.warnings && view.structured.warnings.length > 0
              ? view.structured.warnings.map((w, i) => (
                  <Text key={i} className={styles.disclaimer}>⚠ {w}</Text>
                ))
              : null}
          </View>
        </View>
      ) : null}

      {/* ===== 反应模拟器（前端控件；reaction_trace 存在时出现，加性可视化，与判分无关）===== */}
      {trace && (trace.events.length > 0 || trace.final_species.length > 0) ? (
        <SimPlayer trace={trace} isConfirmed={isConfirmed} />
      ) : null}
    </>
  )
}

// ==================== 反应模拟器（嵌入式播放器，镜像 web /solve 的 sim-panel）====================
/**
 * 特有标志：CSS 绘制的烧瓶图标（瓶身+液体+气泡+轨道环）。
 * 运行/停止：.simIconRun 触发关键帧；状态用 ref 存定时器避免闭包旧值；
 * 图标动画与反应事件逐步点亮、终态揭示严格同步。
 */
function SimPlayer({ trace, isConfirmed }: { trace: ReactionTraceSnapshotDto; isConfirmed: boolean }) {
  const [running, setRunning] = useState(false)
  const [revealed, setRevealed] = useState(0) // 已点亮的事件数
  const [showFinal, setShowFinal] = useState(false)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const finTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const events = trace.events || []
  const total = events.length

  const clearTimers = () => {
    if (timerRef.current != null) { clearInterval(timerRef.current); timerRef.current = null }
    if (finTimerRef.current != null) { clearTimeout(finTimerRef.current); finTimerRef.current = null }
  }
  useEffect(() => () => clearTimers(), [])

  const stop = () => {
    clearTimers()
    setRunning(false)
    setRevealed(0)
    setShowFinal(false)
  }
  const play = () => {
    clearTimers()
    setRunning(true)
    setShowFinal(false)
    setRevealed(total > 0 ? 1 : 0)
    if (total === 0) {
      finTimerRef.current = setTimeout(() => {
        setRunning(false)
        setShowFinal(true)
      }, 600)
      return
    }
    let step = 1
    timerRef.current = setInterval(() => {
      step += 1
      if (step >= total) {
        if (timerRef.current != null) { clearInterval(timerRef.current); timerRef.current = null }
        setRevealed(total)
        finTimerRef.current = setTimeout(() => {
          setRunning(false)
          setShowFinal(true)
        }, 500)
      } else {
        setRevealed(step)
      }
    }, 1100)
  }

  const pct = total ? Math.round((revealed / total) * 100) : 0

  return (
    <View className={styles.simCard}>
      <View className={styles.simHead}>
        <View className={`${styles.simIcon} ${running ? styles.simIconRun : ''}`}>
          <View className={styles.simRing} />
          <View className={styles.simFlask}>
            <View className={styles.simNeck} />
            <View className={styles.simBody}>
              <View className={styles.simLiquid} />
              <View className={`${styles.simBubble} ${styles.simB1}`} />
              <View className={`${styles.simBubble} ${styles.simB2}`} />
            </View>
          </View>
        </View>
        <View className={styles.simTitle}>
          <Text className={styles.simTitleMain}>反应模拟器</Text>
          <Text className={styles.simTitleSub}>点击播放，烧瓶转动与反应步骤同步展开</Text>
        </View>
      </View>

      <View className={styles.simActions}>
        <View className={`${styles.simBtn} ${running ? styles.simBtnDisabled : ''}`} onClick={running ? undefined : play}>
          <Text className={styles.simBtnText}>{running ? '播放中…' : '▶ 播放'}</Text>
        </View>
        <View className={`${styles.simBtn} ${styles.simBtnGhost}`} onClick={stop}>
          <Text className={styles.simBtnTextGhost}>■ 停止</Text>
        </View>
        <Text className={styles.simConv}>收敛：{trace.converged ? '是' : '否'}</Text>
      </View>

      {!isConfirmed ? (
        <Text className={styles.disclaimer}>辅助可视化，不作最终判定结论，仅供参考。</Text>
      ) : null}

      <View className={styles.simProgress}>
        <View className={styles.simProgressBar} style={{ width: `${pct}%` }} />
      </View>

      <View className={styles.simEvents}>
        {events.map((ev, i) => (
          <View key={i} className={`${styles.simEvent} ${i < revealed ? styles.simEventOn : ''}`}>
            <Text className={styles.simEventEq}>
              {i + 1}. {ev.equation ?? ''}
            </Text>
            <Text className={styles.simEventMeta}>
              阶段 {ev.stage_index} · 进度 {ev.extent}
              {ev.basis_knowledge_ids && ev.basis_knowledge_ids.length > 0
                ? ` · 依据：${ev.basis_knowledge_ids.join(', ')}`
                : ''}
            </Text>
          </View>
        ))}
      </View>

      {showFinal && trace.final_species.length > 0 ? (
        <View className={`${styles.simFinal} ${styles.simFinalShow}`}>
          <Text className={styles.simFinalLabel}>最终物种</Text>
          <View className={styles.simChips}>
            {trace.final_species.map((sp, i) => (
              <Text key={i} className={styles.simChip}>
                {sp.substance ?? ''}
                {sp.moles != null ? `：${sp.moles} mol` : ''}
              </Text>
            ))}
          </View>
        </View>
      ) : null}
    </View>
  )
}
