import { View, Text, ScrollView, Input, Textarea, Picker } from '@tarojs/components'
import { useEffect, useRef, useState } from 'react'
import Taro from '@tarojs/taro'
import classnames from 'classnames'
import styles from './index.module.scss'

/* ===================== 类型（镜像 /api/chemistry 响应，camelCase） ===================== */
interface SimSpecies {
  id?: string; name?: string; formula?: string
  initialMoles?: number; finalMoles?: number; amountText?: string; color?: string; role?: string
}
interface SimEvent {
  equation?: string; type?: string; extent?: number
  consumed?: Record<string, number>; produced?: Record<string, number>
  phenomena?: string[]; solutionColorAfter?: string
}
interface SystemState {
  species?: SimSpecies[]; solutionColor?: string
  precipitates?: string[]; phenomena?: string[]; summary?: string
}
interface EquilibriumResult {
  status?: string; ph?: number | null; acidityVerdict?: string
  concentrationOrdering?: string[]; conservationEquations?: string[]
  ionConcentrations?: Record<string, number>; conversionConstantK?: string
  conversionProceeds?: boolean; derivationSteps?: string[]; warnings?: string[]
}
interface StateSnapshot {
  stepIndex?: number; stageName?: string
  state?: SystemState; events?: SimEvent[]; numerical?: EquilibriumResult | null
}
interface TraceResult {
  initialSpecies?: SimSpecies[]; events?: SimEvent[]
  finalState?: SystemState; converged?: boolean; warnings?: string[]
}
interface ReasonResult {
  trajectory?: StateSnapshot[]; finalState?: SystemState
  evidence?: string[]; warnings?: string[]
}
type Mode = 'trace' | 'equilibrium' | 'reason'
interface PlanStep { kind: 'AddSpecies' | 'SetConditions' | 'Assert'; name: string; species: string; numerical: boolean }

/* ===================== 常量与工具 ===================== */
const DEFAULT_BASE = 'http://localhost:5099'
const BASE_KEY = 'sim_api_base'
const NUM_QUANTITIES = ['Ph', 'IonConcentrations', 'ConcentrationOrdering', 'Conservations', 'AcidityQualitative']
const KIND_VALUES: PlanStep['kind'][] = ['AddSpecies', 'SetConditions', 'Assert']
const KIND_LABELS = ['加料', '切换条件', '断言']

const COLORS: Record<string, string> = {
  '蓝色': '#3b82f6', '浅蓝色': '#60a5fa', '蓝绿色': '#2dd4bf', '深蓝色': '#1d4ed8',
  '浅绿色': '#86efac', '绿色': '#22c55e', '黄绿色': '#a3e635', '墨绿色': '#15803d',
  '黄色': '#eab308', '浅黄色': '#fde047', '橙黄色': '#fbbf24', '棕黄色': '#ca8a04',
  '红色': '#ef4444', '橙红色': '#f97316', '橙色': '#f97316', '血红色': '#be123c',
  '紫红色': '#be123c', '红褐色': '#b45309', '棕色': '#92400e',
  '黑色': '#1f2937', '白色': '#ffffff', '无色': '#ffffff',
  '银白色': '#cbd5e1', '灰色': '#94a3b8', '紫色': '#a855f7',
}
const colorOf = (name?: string) => (name && COLORS[name]) || '#94a3b8'

/* ===================== 烧瓶图标：化学状态数据驱动（镜像 web simInferIconState） ===================== */
interface IconState { solutionColor?: string; precipColor?: string; gas: boolean; solid: boolean }
interface IconSource {
  state?: SystemState; type?: string; phenomena?: string[]
  solutionColor?: string; solutionColorAfter?: string
  precipitates?: string[]; species?: SimSpecies[]; events?: SimEvent[]
}
// 沉淀文本 → 颜色：取其中出现的颜色词（长词优先）
const PRECIP_COLOR_ORDER = Object.keys(COLORS).sort((a, b) => b.length - a.length)
function precipColorOf(text?: string): string {
  if (!text) return '#ffffff'
  for (const name of PRECIP_COLOR_ORDER) if (text.indexOf(name) >= 0) return COLORS[name]
  return '#ffffff'
}
// 「无色/白色」视为清水；多色描述（如「黄色、血红色」）也算有色
const isClearColor = (name?: string) => {
  if (!name) return true
  const tokens = name.split(/[、\/,，]/).map((t) => t.trim()).filter(Boolean)
  return tokens.length > 0 && tokens.every((t) => t === '无色' || t === '白色')
}
// 溶液颜色文本 → 色值：精确匹配 → 多色取最后命中（结果色优先）→ 子串最长匹配兜底
function solColorOf(name?: string): string | null {
  if (!name) return null
  if (COLORS[name]) return COLORS[name]
  const tokens = name.split(/[、\/,，]/).map((t) => t.trim()).filter(Boolean)
  for (let i = tokens.length - 1; i >= 0; i--) if (COLORS[tokens[i]]) return COLORS[tokens[i]]
  for (const cname of PRECIP_COLOR_ORDER) if (name.indexOf(cname) >= 0) return COLORS[cname]
  return null
}
const isColoredSolution = (name?: string) => !!solColorOf(name) && !isClearColor(name)
// 从「事件 / 状态 / 快照」推断烧瓶应呈现的化学状态
function inferIconState(source?: IconSource | null): IconState {
  const st: IconState = { gas: false, solid: false }
  if (!source) return st
  const state: SystemState & { solutionColorAfter?: string } = (source.state as SystemState & { solutionColorAfter?: string }) || (source as unknown as SystemState & { solutionColorAfter?: string })
  st.solutionColor = state.solutionColor || state.solutionColorAfter
  const precips = state.precipitates
  if (precips && precips.length) st.precipColor = precipColorOf(precips[0])
  const evs: SimEvent[] = source.events || (source.type ? [source as unknown as SimEvent] : [])
  for (const ev of evs) {
    if (!ev) continue
    if (ev.type === 'GasEvolution') st.gas = true
    const phen = ev.phenomena || []
    if (phen.some((p) => /气体|气泡|冒泡|逸出|放出/.test(p))) st.gas = true
    if (!st.precipColor && phen.some((p) => /沉淀|析出|浑浊|生成.*固体/.test(p))) {
      st.precipColor = precipColorOf(phen.join(' '))
    }
  }
  if (!st.solutionColor && !st.precipColor) {
    const sp = state.species || []
    if (sp.some((s) => /固体|粉末|片|粒|块状/.test(`${s.amountText || ''} ${s.name || ''}`))) st.solid = true
  }
  return st
}

function parseReactants(text: string): { substance: string; moles: number }[] {
  return text.split(/[,，、\n]+/).map((p) => p.trim()).filter(Boolean).map((p) => {
    const parts = p.split(/[:：]/)
    const substance = parts[0].trim()
    const moles = parts[1] != null ? parseFloat(parts[1]) : 1
    return { substance, moles: isNaN(moles) ? 1 : moles }
  }).filter((s) => s.substance)
}
function parseSolute(text: string): { substance: string; concentration: number; volume: number }[] {
  return text.split(/[,，、\n]+/).map((p) => p.trim()).filter(Boolean).map((p) => {
    const parts = p.split(/[:：]/)
    const substance = parts[0].trim()
    const c = parts[1] != null ? parseFloat(parts[1]) : 0.1
    return { substance, concentration: isNaN(c) ? 0.1 : c, volume: 1.0 }
  }).filter((s) => s.substance)
}
function joinEntries(obj?: Record<string, number>, sign = '') {
  return obj ? Object.entries(obj).map(([k, v]) => `${k} ${sign}${v}`).join('，') : ''
}

// 反应类型 → 配色 + 中文标签（纯展示，镜像 web 端）
const EVENT_TYPE_META: Record<string, { color: string; label: string }> = {
  Precipitation:   { color: '#dc2626', label: '沉淀' },
  GasEvolution:    { color: '#ea580c', label: '气体' },
  Neutralization:  { color: '#db2777', label: '中和' },
  Displacement:    { color: '#2563eb', label: '置换' },
  Redox:           { color: '#4f46e5', label: '氧化还原' },
  Oxidation:       { color: '#7c3aed', label: '氧化' },
  Complexation:    { color: '#0891b2', label: '络合' },
  Metathesis:      { color: '#0d9488', label: '复分解' },
  Combination:     { color: '#16a34a', label: '化合' },
  Decomposition:   { color: '#d97706', label: '分解' },
  Hydrolysis:      { color: '#475569', label: '水解' },
  WeakElectrolyte: { color: '#be185d', label: '弱电解质' },
  Esterification:  { color: '#9333ea', label: '酯化' },
  Addition:        { color: '#0284c7', label: '加成' },
  Substitution:    { color: '#059669', label: '取代' },
  NoReaction:      { color: '#94a3b8', label: '无反应' },
}
const eventTypeMeta = (type?: string) => (type && EVENT_TYPE_META[type]) || { color: '#64748b', label: type || '' }
const hexA = (hex: string, a: number) => {
  const h = hex.replace('#', '')
  const r = parseInt(h.substring(0, 2), 16)
  const g = parseInt(h.substring(2, 4), 16)
  const b = parseInt(h.substring(4, 6), 16)
  return `rgba(${r}, ${g}, ${b}, ${a})`
}
const fmtExtent = (v: unknown): string => (typeof v === 'number' ? v.toFixed(3).replace(/\.?0+$/, '') : String(v ?? '-'))

async function postJson<T>(base: string, path: string, body: unknown): Promise<T> {
  const res = await Taro.request({
    url: `${base.replace(/\/+$/, '')}${path}`,
    method: 'POST',
    data: body as Record<string, unknown>,
    header: { 'Content-Type': 'application/json' },
    timeout: 20000,
  })
  if (res.statusCode < 200 || res.statusCode >= 300) throw new Error(`HTTP ${res.statusCode}`)
  return res.data as T
}

/* ===================== 烧瓶图标（CSS 绘制；running 转动，state 驱动变色/沉淀/冒泡/固态） ===================== */
function FlaskIcon({ running, state, large }: { running: boolean; state: IconState; large?: boolean }) {
  const solColor = solColorOf(state.solutionColor)
  const colored = isColoredSolution(state.solutionColor) && !state.solid
  const sol = colored && solColor ? solColor : 'rgba(186, 230, 253, 0.22)'
  const solOpacity = state.solid ? 0 : (colored ? 0.85 : 0.55)
  return (
    <View className={classnames(styles.simIconWrap, running && styles.simIconWrapRun, large && styles.simIconLarge)}>
      <View className={classnames(
        styles.simIcon,
        running && styles.simIconRun,
        !running && styles.simIconIdle,
        colored && styles.simIconColored,
        !!state.precipColor && styles.simIconHasPrecip,
        state.gas && styles.simIconBubbling,
        state.solid && styles.simIconSolid,
      )}>
        <View className={styles.simRing} />
        <View className={styles.simFlask}>
          <View className={styles.simRim}>
            <View className={styles.simMouth} />
          </View>
          <View className={styles.simNeck} />
          <View className={styles.simBody}>
            <View className={styles.simLiquid} style={{ background: sol, opacity: solOpacity }} />
            <View className={styles.simLiquidWave} />
            <View className={styles.simSolids}>
              <View className={classnames(styles.simGranule, styles.simG1)} />
              <View className={classnames(styles.simGranule, styles.simG2)} />
              <View className={classnames(styles.simGranule, styles.simG3)} />
              <View className={classnames(styles.simGranule, styles.simG4)} />
            </View>
            <View className={styles.simPrecip} style={{ background: state.precipColor || '#ffffff' }} />
            <View className={classnames(styles.simBubble, styles.simB1)} />
            <View className={classnames(styles.simBubble, styles.simB2)} />
            <View className={classnames(styles.simBubble, styles.simB3)} />
            <View className={styles.simShine} />
            <View className={styles.simScale}>
              <View className={styles.simTick} />
              <View className={styles.simTick} />
              <View className={styles.simTick} />
            </View>
          </View>
        </View>
      </View>
    </View>
  )
}

/* ===================== 渲染片段 ===================== */
function Check({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <View className={styles.checkRow} onClick={() => onChange(!checked)}>
      <View className={classnames(styles.checkBox, checked && styles.checkBoxOn)}>
        {checked ? <Text className={styles.checkTick}>✓</Text> : null}
      </View>
      <Text>{label}</Text>
    </View>
  )
}

function SpeciesChip({ sp }: { sp: SimSpecies }) {
  return (
    <Text className={styles.chip}>
      <Text className={styles.dot} style={{ background: colorOf(sp.color) }} />
      {sp.formula || sp.name || sp.id || ''}
      {sp.finalMoles != null ? `：${sp.finalMoles} mol` : ''}
      {sp.amountText ? `（${sp.amountText}）` : ''}
    </Text>
  )
}

function EventItem({ ev, index, animate }: { ev: SimEvent; index: number; animate?: boolean }) {
  const consumed = joinEntries(ev.consumed, '-')
  const produced = joinEntries(ev.produced, '+')
  const tm = eventTypeMeta(ev.type)
  return (
    <View
      className={classnames(styles.event, animate ? styles.eventAnim : styles.eventOn)}
      style={{ ...(animate ? { animationDelay: `${index * 0.12 + 0.08}s` } : {}), borderLeftColor: tm.color }}
    >
      <Text className={styles.eventEq}>{index + 1}. {ev.equation ?? ''}</Text>
      <Text className={styles.eventMeta}>
        <Text className={styles.typeTag} style={{ background: hexA(tm.color, 0.1), color: tm.color, borderColor: hexA(tm.color, 0.2) }}>{tm.label}</Text>
        {ev.type ?? ''} · 进度 {fmtExtent(ev.extent)}
      </Text>
      {consumed ? <Text className={styles.delta}>消耗：<Text className={styles.mono}>{consumed}</Text></Text> : null}
      {produced ? <Text className={styles.delta}>生成：<Text className={styles.mono}>{produced}</Text></Text> : null}
      {ev.phenomena && ev.phenomena.length > 0 ? (
        <View className={styles.phen}>
          {ev.phenomena.map((p, i) => <Text key={i} className={classnames(styles.chip, styles.chipSoft)}>{p}</Text>)}
        </View>
      ) : null}
      {ev.solutionColorAfter ? (
        <Text className={styles.colorLine}>
          溶液颜色 → <Text className={styles.dot} style={{ background: colorOf(ev.solutionColorAfter) }} />{ev.solutionColorAfter}
        </Text>
      ) : null}
    </View>
  )
}

function NumericalBlock({ d }: { d: EquilibriumResult }) {
  return (
    <View className={styles.numerical}>
      <Text>状态：{d.status ?? ''}</Text>
      {d.ph != null ? <Text>；pH ≈ {d.ph}</Text> : null}
      {d.acidityVerdict ? <Text>；{d.acidityVerdict}</Text> : null}
      {d.concentrationOrdering && d.concentrationOrdering.length > 0 ? (
        <Text>；浓度排序：{d.concentrationOrdering.join(' > ')}</Text>
      ) : null}
      {d.conversionConstantK ? (
        <Text>；沉淀转化 K：{d.conversionConstantK}（自发：{d.conversionProceeds ? '是' : '否'}）</Text>
      ) : null}
      {d.ionConcentrations && Object.keys(d.ionConcentrations).length > 0 ? (
        <View style={{ marginTop: 4 }}>
          {Object.entries(d.ionConcentrations).map(([k, v]) => (
            <Text key={k} className={styles.mono}> · {k} = {v}</Text>
          ))}
        </View>
      ) : null}
      {d.conservationEquations && d.conservationEquations.length > 0 ? (
        <View style={{ marginTop: 4 }}>
          {d.conservationEquations.map((e, i) => <Text key={i} className={styles.mono}> · {e}</Text>)}
        </View>
      ) : null}
      {d.warnings && d.warnings.length > 0 ? (
        <View style={{ marginTop: 4 }}>
          {d.warnings.map((w, i) => <Text key={i} className={styles.note}> · {w}</Text>)}
        </View>
      ) : null}
    </View>
  )
}

function FinalState({ fin }: { fin: SystemState }) {
  return (
    <View className={classnames(styles.final, styles.finalShow)}>
      <View className={styles.finalLabelRow}>
        <Text className={styles.finalCheck}>✓</Text>
        <Text className={styles.finalLabel}>终态</Text>
      </View>
      {fin.solutionColor ? (
        <Text className={styles.colorLine}>
          溶液颜色：<Text className={styles.dot} style={{ background: colorOf(fin.solutionColor) }} />{fin.solutionColor}
        </Text>
      ) : null}
      <View className={styles.chips}>
        {(fin.species || []).map((sp, i) => <SpeciesChip key={i} sp={sp} />)}
      </View>
      {fin.precipitates && fin.precipitates.length > 0 ? (
        <View className={styles.phen}>
          {fin.precipitates.map((p, i) => <Text key={i} className={classnames(styles.chip, styles.chipSoft)}>沉淀：{p}</Text>)}
        </View>
      ) : null}
      {fin.summary ? <Text className={styles.summary}>{fin.summary}</Text> : null}
    </View>
  )
}

/* ===================== 页面 ===================== */
export default function SimulatorPage() {
  const [mode, setMode] = useState<Mode>('trace')
  const [base, setBase] = useState<string>(() => (Taro.getStorageSync(BASE_KEY) as string) || DEFAULT_BASE)
  const [traceInput, setTraceInput] = useState('Fe:1, CuSO4:1')
  const [eqInput, setEqInput] = useState('CH3COONa:0.1')
  const [reasonInitial, setReasonInitial] = useState('AlCl3:1')
  const [aqueous, setAqueous] = useState(true)
  const [plan, setPlan] = useState<PlanStep[]>([
    { kind: 'AddSpecies', name: '加过量 NaOH', species: 'NaOH:0.5', numerical: true },
  ])

  const [running, setRunning] = useState(false)
  const [status, setStatus] = useState('')
  const [trace, setTrace] = useState<TraceResult | null>(null)
  const [eq, setEq] = useState<EquilibriumResult | null>(null)
  const [reason, setReason] = useState<ReasonResult | null>(null)
  const [revealed, setRevealed] = useState(0)
  const [showFinal, setShowFinal] = useState(false)
  const [revealedSnaps, setRevealedSnaps] = useState(0)
  const [reasonShowFinal, setReasonShowFinal] = useState(false)

  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const finalTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const clearTimers = () => {
    if (timerRef.current != null) { clearInterval(timerRef.current); timerRef.current = null }
    if (finalTimerRef.current != null) { clearTimeout(finalTimerRef.current); finalTimerRef.current = null }
  }
  useEffect(() => () => clearTimers(), [])

  const resetAnim = () => {
    clearTimers()
    setRunning(false)
    setRevealed(0)
    setShowFinal(false)
    setRevealedSnaps(0)
    setReasonShowFinal(false)
  }

  const persistBase = (v: string) => {
    setBase(v)
    try { Taro.setStorageSync(BASE_KEY, v) } catch { /* ignore */ }
  }

  const updatePlan = (i: number, patch: Partial<PlanStep>) =>
    setPlan((prev) => prev.map((s, idx) => (idx === i ? { ...s, ...patch } : s)))
  const addStep = () => setPlan((prev) => [...prev, { kind: 'AddSpecies', name: '', species: '', numerical: false }])
  const removeStep = (i: number) => setPlan((prev) => prev.filter((_, idx) => idx !== i))

  /* ---------- trace ---------- */
  const runTrace = async () => {
    const species = parseReactants(traceInput)
    if (species.length === 0) { Taro.showToast({ title: '请输入反应物', icon: 'none' }); return }
    resetAnim(); setTrace(null); setEq(null); setReason(null)
    setRunning(true); setStatus('正在模拟…')
    try {
      const data = await postJson<TraceResult>(base, '/api/chemistry/trace', { species, conditions: { aqueous } })
      setTrace(data)
      const total = data.events?.length ?? 0
      if (total === 0) {
        setRevealed(0)
        finalTimerRef.current = setTimeout(() => { setRunning(false); setShowFinal(true); setStatus('模拟完成') }, 600)
        return
      }
      setRevealed(1)
      let step = 1
      timerRef.current = setInterval(() => {
        step += 1
        if (step >= total) {
          if (timerRef.current != null) { clearInterval(timerRef.current); timerRef.current = null }
          setRevealed(total)
          finalTimerRef.current = setTimeout(() => {
            setRunning(false); setShowFinal(true)
            setStatus(`模拟完成（收敛：${data.converged ? '是' : '否'}）`)
          }, 500)
        } else {
          setRevealed(step)
        }
      }, 1300)
    } catch (err) {
      resetAnim()
      setStatus('调用失败：' + (err instanceof Error ? err.message : '网络异常'))
    }
  }

  /* ---------- equilibrium ---------- */
  const runEquilibrium = async () => {
    const species = parseSolute(eqInput)
    if (species.length === 0) { Taro.showToast({ title: '请输入溶质与浓度', icon: 'none' }); return }
    resetAnim(); setTrace(null); setEq(null); setReason(null)
    setRunning(true); setStatus('正在计算…')
    try {
      const data = await postJson<EquilibriumResult>(base, '/api/chemistry/equilibrium', {
        input: { species }, query: { quantities: NUM_QUANTITIES },
      })
      setEq(data)
      setRunning(false)
      setStatus(`计算完成（${data.status ?? ''}）`)
    } catch (err) {
      resetAnim()
      setStatus('调用失败：' + (err instanceof Error ? err.message : '网络异常'))
    }
  }

  /* ---------- reason ---------- */
  const runReason = async () => {
    const initial = parseReactants(reasonInitial)
    if (initial.length === 0) { Taro.showToast({ title: '请输入初始体系', icon: 'none' }); return }
    if (plan.length === 0) { Taro.showToast({ title: '请至少添加一个推理步骤', icon: 'none' }); return }
    const steps = plan.map((s) => {
      const step: Record<string, unknown> = { kind: s.kind, name: s.name || `步骤` }
      if (s.kind === 'AddSpecies') {
        const sp = parseReactants(s.species)
        if (sp.length) step.species = sp
      }
      if (s.numerical) step.numerical = { quantities: NUM_QUANTITIES }
      return step
    })
    resetAnim(); setTrace(null); setEq(null); setReason(null)
    setRunning(true); setStatus('正在推理…')
    try {
      const data = await postJson<ReasonResult>(base, '/api/chemistry/reason', {
        initial: { species: initial, conditions: { aqueous } }, plan: { steps },
      })
      setReason(data)
      const total = data.trajectory?.length ?? 0
      if (total === 0) {
        setRevealedSnaps(0)
        finalTimerRef.current = setTimeout(() => { setRunning(false); setReasonShowFinal(true); setStatus('推理完成') }, 600)
        return
      }
      setRevealedSnaps(1)
      let step = 1
      timerRef.current = setInterval(() => {
        step += 1
        if (step >= total) {
          if (timerRef.current != null) { clearInterval(timerRef.current); timerRef.current = null }
          setRevealedSnaps(total)
          finalTimerRef.current = setTimeout(() => {
            setRunning(false); setReasonShowFinal(true)
            setStatus(`推理完成，共 ${total} 个状态快照`)
          }, 650)
        } else {
          setRevealedSnaps(step)
        }
      }, 1500)
    } catch (err) {
      resetAnim()
      setStatus('调用失败：' + (err instanceof Error ? err.message : '网络异常'))
    }
  }

  const onRun = () => {
    if (mode === 'trace') return runTrace()
    if (mode === 'equilibrium') return runEquilibrium()
    return runReason()
  }

  // 专属「模拟」按钮：直接执行多步推理并播放动画，不依赖先切模式标签
  const quickReason = () => {
    if (running) return
    setMode('reason')
    runReason()
  }

  const traceEvents = trace?.events ?? []
  const tracePct = traceEvents.length ? Math.round((revealed / traceEvents.length) * 100) : 0
  const reasonTraj = reason?.trajectory ?? []
  const reasonPct = reasonTraj.length ? Math.round((revealedSnaps / reasonTraj.length) * 100) : 0

  // 烧瓶图标状态：取当前已揭示的事件/快照，或终态（trace 与 reason 互斥）
  let iconSource: IconSource | null = null
  if (trace) {
    if (showFinal && trace.finalState) iconSource = trace.finalState
    else if (revealed > 0) iconSource = traceEvents[revealed - 1] ?? null
  } else if (reason) {
    if (reasonShowFinal && reason.finalState) iconSource = reason.finalState
    else if (revealedSnaps > 0) iconSource = reasonTraj[revealedSnaps - 1] ?? null
  }
  const iconState = inferIconState(iconSource)

  return (
    <View className={styles.page}>
      <ScrollView scrollY className={styles.body}>
        <View className={styles.panel}>
          <View className={styles.simHead}>
            <View className={styles.simTitle}>
              <Text className={styles.simTitleMain}>⚗️ 反应模拟器</Text>
              <Text className={styles.simTitleSub}>输入物质与条件，调用化学引擎实时模拟；烧瓶随运行转动，与现象同步</Text>
            </View>
          </View>

          <View className={styles.baseRow}>
            <Text className={styles.baseLabel}>API 地址：</Text>
            <Input className={styles.baseInput} value={base} onInput={(e) => persistBase(e.detail.value)} />
          </View>

          <View className={styles.tabs}>
            {(['trace', 'equilibrium', 'reason'] as Mode[]).map((m) => (
              <View
                key={m}
                className={classnames(styles.tab, mode === m && styles.tabActive)}
                onClick={() => { setMode(m); resetAnim() }}
              >
                <Text>{m === 'trace' ? '反应轨迹' : m === 'equilibrium' ? '平衡计算' : '多步推理'}</Text>
              </View>
            ))}
          </View>

          {mode === 'trace' ? (
            <>
              <Textarea className={styles.textarea} value={traceInput} onInput={(e) => setTraceInput(e.detail.value)}
                placeholder={'反应物，格式 物质:物质的量，如：Fe:1, CuSO4:1'} />
              <Check checked={aqueous} onChange={setAqueous} label="水溶液条件" />
            </>
          ) : null}

          {mode === 'equilibrium' ? (
            <Textarea className={styles.textarea} value={eqInput} onInput={(e) => setEqInput(e.detail.value)}
              placeholder={'溶质与浓度，格式 物质:浓度(mol/L)，如：CH3COONa:0.1'} />
          ) : null}

          {mode === 'reason' ? (
            <>
              <Textarea className={styles.textarea} value={reasonInitial} onInput={(e) => setReasonInitial(e.detail.value)}
                placeholder={'初始体系，格式 物质:物质的量，如：AlCl3:1'} />
              <Text className={styles.planLabel}>推理计划（按顺序执行）</Text>
              <View className={styles.planList}>
                {plan.map((s, i) => (
                  <View key={i} className={styles.planRow}>
                    <Picker
                      mode="selector"
                      range={KIND_LABELS}
                      value={KIND_VALUES.indexOf(s.kind)}
                      onChange={(e) => updatePlan(i, { kind: KIND_VALUES[Number(e.detail.value)] })}
                    >
                      <View className={styles.planKind}>{KIND_LABELS[KIND_VALUES.indexOf(s.kind)]}</View>
                    </Picker>
                    <Input className={styles.planName} value={s.name} placeholder="步骤名，如 加过量 NaOH"
                      onInput={(e) => updatePlan(i, { name: e.detail.value })} />
                    <Input className={styles.planSpec} value={s.species} placeholder="NaOH:0.5"
                      onInput={(e) => updatePlan(i, { species: e.detail.value })} />
                    <Check checked={s.numerical} onChange={(v) => updatePlan(i, { numerical: v })} label="查数值" />
                    <View className={styles.planRm} onClick={() => removeStep(i)}><Text>✕</Text></View>
                  </View>
                ))}
                <View className={styles.planAdd} onClick={addStep}><Text>＋ 添加步骤</Text></View>
              </View>
              <Check checked={aqueous} onChange={setAqueous} label="水溶液条件" />
            </>
          ) : null}

        </View>

        {/* ===== 效果舞台：大烧瓶图标 + 模拟按钮 + 出现的过程，聚合在同一块 ===== */}
        <View className={styles.stage}>
          <View className={styles.stageIcon}>
            <FlaskIcon running={running} state={iconState} large />
          </View>
          <View className={classnames(styles.actions, styles.stageActions)}>
            <View className={classnames(styles.btn, styles.icoBtn, running && styles.icoBtnRunning)} onClick={running ? undefined : onRun}>
              <Text className={styles.ico}>{running ? '⏳' : '▶'}</Text>
              <Text className={styles.icoLabel}>{running ? '运行中…' : '开始模拟'}</Text>
            </View>
            <View className={classnames(styles.btn, styles.btnGhost, styles.icoBtn)} onClick={resetAnim}>
              <Text className={styles.ico}>■</Text>
              <Text className={styles.icoLabel}>停止</Text>
            </View>
            <View className={classnames(styles.quickBtn, styles.icoBtn, running && { opacity: 0.6 })} onClick={quickReason}>
              <Text className={styles.ico}>⚡</Text>
              <Text className={styles.icoLabel}>模拟</Text>
            </View>
            <Text className={styles.status}>{status}</Text>
          </View>
          <View className={styles.stageHint}>
            <Text className={styles.note}>「模拟」一键执行多步推理计划，烧瓶随状态轨迹转动并同步现象</Text>
          </View>

          {/* ===== 空状态占位 ===== */}
        {!trace && !eq && !reason ? (
          <View className={styles.panel}>
            <View className={styles.empty}>
              <Text className={styles.emptyDesc}>输入物质后点「开始模拟」，烧瓶会随反应同步变色、沉淀与冒泡。</Text>
            </View>
          </View>
        ) : null}

        {/* ===== trace 结果 ===== */}
        {trace ? (
          <View className={styles.panel}>
            <View className={styles.progress}><View className={styles.progressBar} style={{ width: `${tracePct}%` }} /></View>
            {trace.initialSpecies && trace.initialSpecies.length > 0 ? (
              <View style={{ marginTop: 10 }}>
                <Text className={styles.note}>初始态：</Text>
                <View className={styles.chips}>{trace.initialSpecies.map((sp, i) => <SpeciesChip key={i} sp={sp} />)}</View>
              </View>
            ) : null}
            <View className={styles.events}>
              {traceEvents.map((ev, i) => (
                <EventItem key={i} ev={ev} index={i} />
              ))}
            </View>
            {showFinal && trace.finalState ? <FinalState fin={trace.finalState} /> : null}
            {trace.warnings && trace.warnings.length > 0 ? (
              <View style={{ marginTop: 8 }}>{trace.warnings.map((w, i) => <Text key={i} className={styles.note}>⚠ {w}</Text>)}</View>
            ) : null}
          </View>
        ) : null}

        {/* ===== equilibrium 结果 ===== */}
        {eq ? (
          <View className={styles.panel}>
            <NumericalBlock d={eq} />
            {eq.derivationSteps && eq.derivationSteps.length > 0 ? (
              <View style={{ marginTop: 8 }}>
                <Text className={styles.note}>推导：</Text>
                {eq.derivationSteps.map((s, i) => <Text key={i} className={styles.note}>  {i + 1}. {s}</Text>)}
              </View>
            ) : null}
          </View>
        ) : null}

        {/* ===== reason 结果（时间线逐步展开） ===== */}
        {reason ? (
          <View className={styles.panel}>
            <View className={styles.progress}><View className={styles.progressBar} style={{ width: `${reasonPct}%` }} /></View>
            <View className={styles.timeline}>
              {reasonTraj.map((snap, i) => (
                i < revealedSnaps ? (
                  <View key={i} className={styles.snap}>
                    <View className={styles.snapHead}>
                      <View className={styles.stepBadge}>{snap.stepIndex ?? i}</View>
                      <Text>{snap.stageName ?? `步骤 ${i}`}</Text>
                    </View>
                    <Text className={styles.snapLabel}>体系状态</Text>
                    <View className={styles.chips}>{(snap.state?.species ?? []).map((sp, k) => <SpeciesChip key={k} sp={sp} />)}</View>
                    {snap.state?.solutionColor ? (
                      <Text className={styles.colorLine}>
                        溶液颜色：<Text className={styles.dot} style={{ background: colorOf(snap.state.solutionColor) }} />{snap.state.solutionColor}
                      </Text>
                    ) : null}
                    {snap.state?.precipitates && snap.state.precipitates.length > 0 ? (
                      <View className={styles.phen}>
                        {snap.state.precipitates.map((p, k) => <Text key={k} className={classnames(styles.chip, styles.chipSoft)}>沉淀：{p}</Text>)}
                      </View>
                    ) : null}
                    {snap.events && snap.events.length > 0 ? (
                      <>
                        <Text className={styles.snapLabel}>反应事件</Text>
                        <View className={styles.events}>
                          {snap.events.map((ev, k) => <EventItem key={k} ev={ev} index={k} animate />)}
                        </View>
                      </>
                    ) : null}
                    {snap.numerical ? (
                      <>
                        <Text className={styles.snapLabel}>该步平衡数值</Text>
                        <NumericalBlock d={snap.numerical} />
                      </>
                    ) : null}
                  </View>
                ) : null
              ))}
            </View>
            {reasonShowFinal && reason.finalState ? <FinalState fin={reason.finalState} /> : null}
            {reason.evidence && reason.evidence.length > 0 ? (
              <View className={styles.evidence}>
                <Text className={styles.note}>依据：</Text>
                {reason.evidence.map((e, i) => <Text key={i} className={styles.note}>· {e}</Text>)}
              </View>
            ) : null}
            {reason.warnings && reason.warnings.length > 0 ? (
              <View style={{ marginTop: 8 }}>{reason.warnings.map((w, i) => <Text key={i} className={styles.note}>⚠ {w}</Text>)}</View>
            ) : null}
          </View>
        ) : null}

        </View>

        <View className={styles.bottomPad} />
      </ScrollView>
    </View>
  )
}
