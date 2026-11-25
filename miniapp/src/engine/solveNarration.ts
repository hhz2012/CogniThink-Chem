import {
  Challenge,
  SolveData,
  RedoxSolveData,
  RedoxHalfReaction,
  IonicSolveData,
  EquilibriumSolveData,
  ElectrochemSolveData,
  InferenceSolveData,
  OrganicSolveData,
} from '../types/chem'
import { SolveModelStep } from '../engine/solveModels'

/**
 * 把题目结构化真值（solveData）拼成「结合本题具体内容」的实例化讲解文字。
 *
 * 设计原则：
 * 1. 数据驱动 —— 所有真实取值（价态、电子数、粒子、守恒式、移动方向等）均来自 challenge.solveData，
 *    不写死任何题目的静态文案（经验 533966：禁止与数据脱钩的硬编码）。
 * 2. 优雅降级 —— 题目无 solveData，或该步骤暂无对应真值时，返回 null，调用方回退到题型通用 analyze。
 * 3. 只做「怎么分析」的实例化，goal（方法目标）仍由 solveModels 提供通用方法骨架。
 */

/** 价态数字格式：正数带 +，0 不加号，负数带 - */
export const fmtVal = (n: number): string => (n > 0 ? `+${n}` : `${n}`)

export function narrateStep(step: SolveModelStep, challenge: Challenge): string | null {
  const sd: SolveData | undefined = challenge.solveData
  if (!sd) return null
  switch (sd.kind) {
    case 'redox-electron':
      return narrateRedox(step, sd)
    case 'ionic-conservation':
      return narrateIonic(step, sd)
    case 'equilibrium-shift':
      return narrateEquilibrium(step, sd)
    case 'electrochem-cell':
      return narrateElectrochem(step, sd)
    case 'inorganic-inference':
      return narrateInference(step, sd)
    case 'organic-chemistry':
      return narrateOrganic(step, sd)
    default:
      return null
  }
}

// ============ 氧化还原：标价态 / 算电子 / 配守恒 / 观察配平 ============
function narrateRedox(step: SolveModelStep, sd: RedoxSolveData): string | null {
  const hs = sd.halfReactions ?? []
  // 1) 有变价明细：按步骤生成带方程式 / 四剂 / 系数的实例化讲解
  const byTitle = redoxNarrationByTitle(step, sd, hs)
  if (byTitle) return byTitle
  // 2) 无变价明细或该步骤不匹配：用专属段（价态规律 / 性质比较 / 多反应判断）兜底
  return redoxNarrationBySection(step, sd)
}

/** 单个变价元素的价态变化描述：优先带物质，否则仅元素 */
export function fmtChange(h: RedoxHalfReaction): string {
  const dir = h.to > h.from ? '升到' : h.to < h.from ? '降到' : '不变'
  if (h.reactantFormula && h.productFormula) {
    return `${h.species} 在 ${h.reactantFormula} 中为 ${fmtVal(h.from)}，反应后在 ${h.productFormula} 中变为 ${fmtVal(h.to)}（${dir}）`
  }
  return `${h.species} 由 ${fmtVal(h.from)} ${dir} ${fmtVal(h.to)}`
}

/** 有变价明细时，按步骤标题生成实例化讲解 */
function redoxNarrationByTitle(
  step: SolveModelStep,
  sd: RedoxSolveData,
  hs: RedoxHalfReaction[],
): string | null {
  const bal = sd.balance
  const eq = sd.reaction?.equation
  const ionic = sd.reaction?.ionicEquation

  switch (step.title) {
    case '标价态': {
      if (hs.length === 0) return null
      const head = eq ? `本题方程式：${eq}。` : ionic ? `本题离子方程式：${ionic}。` : ''
      const parts = hs.map(fmtChange)
      const changed = hs.filter((h) => h.from !== h.to)
      const tail = changed.length > 0 ? `化合价发生变化的元素是 ${changed.map((h) => h.species).join('、')}。` : ''
      const typeLine = sd.reaction?.type ? `该反应属于${sd.reaction.type}。` : ''
      return `${head}${parts.join('；')}。${tail}${typeLine}`
    }
    case '算电子': {
      if (hs.length === 0) return null
      const parts = hs.map((h) => {
        const perAtom = Math.abs(h.to - h.from)
        const verb = h.role === '氧化' ? '失' : '得'
        return `${h.species}：${fmtVal(h.from)}→${fmtVal(h.to)}，每个 ${h.species} ${verb} ${perAtom}e⁻（被${h.role}）`
      })
      const ag = sd.agents
      const agentLine = ag
        ? `氧化剂：${ag.oxidants.join('、')}；还原剂：${ag.reductants.join('、')}。`
        : ''
      return `${parts.join('；')}。${agentLine}`
    }
    case '配守恒': {
      if (!bal && !sd.calc) return null
      let text = ''
      if (bal) {
        const pair = hs.length >= 2 ? `${hs[0].species} 与 ${hs[1].species} 所在物质的系数比为 ${bal.ratio}；` : ''
        text += `失电子总数 ${bal.oxidizedCount} = 得电子总数 ${bal.reducedCount}，最小公倍数 ${bal.lcm}；${pair}`
        if (bal.coefficients && bal.coefficients.length) {
          text += `配平系数：${bal.coefficients.map((c) => `${c.formula} ${c.value}`).join('，')}。`
        }
      }
      if (sd.calc) {
        text += `已知：${sd.calc.given}；求：${sd.calc.target}。`
      }
      return text || null
    }
    case '观察配平': {
      let text = ''
      if (eq) text += `配平后方程式：${eq}。`
      if (sd.agents) {
        text += `氧化产物：${sd.agents.oxidizedProducts.join('、')}；还原产物：${sd.agents.reducedProducts.join('、')}。`
      }
      if (sd.order) text += `${sd.order.conclusion}。`
      if (sd.calc) {
        text += `${sd.calc.steps.join('；')}。结论：${sd.calc.result}。`
      } else if (text) {
        text += '最后检验原子与电荷是否守恒。'
      }
      return text || null
    }
    default:
      return null
  }
}

/** 无变价明细时，按专属段生成实例化讲解（概念判断 / 性质比较 / 价态规律 / 多反应判断） */
function redoxNarrationBySection(step: SolveModelStep, sd: RedoxSolveData): string | null {
  // 价态规律（如 R14：只能作还原剂的判断）
  if (sd.valenceRule && sd.valenceRule.items.length) {
    const items = sd.valenceRule.items
    const list = items
      .map((i) => `${i.species} 中 ${i.element} 为 ${fmtVal(i.valence)}（${i.position}价态）`)
      .join('；')
    const lowest = items.find((i) => i.position === '最低')
    if (step.title === '标价态') return `本题各粒子核心元素价态：${list}。`
    if (step.title === '算电子')
      return '规律：元素处于最低价只能失电子作还原剂，处于最高价只能得电子作氧化剂，处于中间价态既可作氧化剂又可作还原剂。'
    if (step.title === '配守恒' || step.title === '观察配平') {
      return lowest
        ? `${lowest.species} 中 ${lowest.element} 为 ${fmtVal(lowest.valence)}（最低价），化合价只能升高，故只能作还原剂；其余粒子均为中间价态，既可作氧化剂又可作还原剂。`
        : null
    }
    return null
  }
  // 性质比较（如 R10：氧化性强弱顺序）
  if (sd.comparison) {
    const c = sd.comparison
    if (step.title === '标价态') {
      if (!sd.reactions || sd.reactions.length === 0) return null
      const list = sd.reactions
        .map((r) => `${r.equation}（${r.oxidant ? `氧化剂 ${r.oxidant}，氧化产物 ${r.oxidizedProduct}` : r.type}）`)
        .join('；')
      return `本题涉及反应：${list}。`
    }
    if (step.title === '算电子') return `比较依据：${c.basis}。`
    if (step.title === '配守恒' || step.title === '观察配平')
      return `${c.property}由强到弱：${c.order}。`
    return null
  }
  // 反应先后（如 R11：少量 Cl₂ 先氧化 Fe²⁺）—— halfReactions 非空时通常已由上面处理，此处兜底结论
  if (sd.order && (step.title === '配守恒' || step.title === '观察配平')) {
    return `${sd.order.property}由强到弱：${sd.order.strongerFirst.join(' > ')}。${sd.order.conclusion}。`
  }
  // 多反应判断（如 R9：选出非氧化还原反应）
  if (sd.reactions && sd.reactions.length) {
    const rs = sd.reactions
    const list = rs
      .map((r) => `${r.equation}（${r.type}，${r.isRedox ? '是' : '不是'}氧化还原反应）`)
      .join('；')
    const nonRedox = rs.find((r) => !r.isRedox)
    if (step.title === '标价态') return `逐项分析：${list}。`
    if (step.title === '算电子')
      return '判断依据：反应前后元素化合价是否变化。复分解反应各元素化合价均不变，一定不是氧化还原反应。'
    if (step.title === '配守恒' || step.title === '观察配平') {
      return nonRedox
        ? `${nonRedox.equation} 是${nonRedox.type}，各元素化合价不变，故不属于氧化还原反应。`
        : null
    }
    return null
  }
  return null
}

// ============ 离子平衡：找粒子 / 写守恒 / 比浓度 ============
function narrateIonic(step: SolveModelStep, sd: IonicSolveData): string | null {
  switch (step.title) {
    case '找粒子': {
      if (sd.species.length === 0) return null
      const list = sd.species.map((sp) => `${sp.formula}（${sp.source}）`).join('、')
      return `本题溶液中存在的粒子有：${list}。注意不要漏列水电离出的 H⁺ 和 OH⁻。`
    }
    case '写守恒': {
      const eqs = sd.equations ?? []
      if (eqs.length === 0) return null
      const list = eqs.map((eq) => `${eq.type}守恒：${eq.expression}`).join('；')
      return `可列出的守恒关系式为：${list}。` + (eqs.some((e) => e.type === '质子')
          ? ''
          : '质子守恒可由电荷守恒减去物料守恒得到。')
    }
    case '比浓度': {
      if (sd.comparison) {
        const c = sd.comparison
        const head = c.order ? `正确顺序：${c.order}。` : c.conclusion ? `${c.conclusion}。` : ''
        const basis = c.basis ? `判断依据：${c.basis}。` : ''
        return `${head}${basis}` || null
      }
      if (sd.calc) {
        return `${sd.calc.steps.join('；')}。结论：${sd.calc.result}。`
      }
      return null
    }
    default:
      return null
  }
}

// ============ 化学平衡：写平衡 / 判条件 / 推移动 / 看 K 与转化率 ============
function narrateEquilibrium(step: SolveModelStep, sd: EquilibriumSolveData): string | null {
  const fs = sd.factors ?? []
  const r = sd.reaction

  switch (step.title) {
    case '写平衡': {
      if (!r) return null
      let text = `反应方程式：${r.equation}。`
      if (r.deltaH) text += `ΔH：${r.deltaH}。`
      if (r.gasChange) text += `正反应${r.gasChange}。`
      return text
    }
    case '判条件': {
      if (fs.length === 0) return null
      const list = fs.map((f) => `${f.factor}（${f.change}）`).join('、')
      return `本题改变的条件有：${list}。把每个条件单独列出，再分别判断平衡如何移动。`
    }
    case '推移动': {
      if (fs.length === 0) return null
      const list = fs
        .map((f) => `${f.change} → 平衡${f.shift}移动，K ${f.kChanges}`)
        .join('；')
      return `逐条件判断：${list}。注意催化剂只改变速率、不影响平衡，K 只与温度有关。`
    }
    case '看 K 与转化率': {
      if (sd.result) {
        const res = sd.result
        let text = `求解类型：${res.type}。`
        if (res.expression) text += `${res.expression}。`
        if (res.steps && res.steps.length) text += `${res.steps.join('；')}。`
        if (res.value) text += `计算结果：${res.value}。`
        if (res.conclusion) text += `${res.conclusion}。`
        return text
      }
      if (fs.length === 0) return null
      const kOnlyTemp = fs.some((f) => f.kChanges !== '不变')
        ? '本题中温度改变会使 K 变化，其余条件（浓度/压强/催化剂）下 K 均不变。'
        : '本题未改变温度，故 K 不变；浓度、压强、催化剂都不改变 K。'
      const conv = fs
        .filter((f) => f.conversion)
        .map((f) => `${f.change}：${f.conversion}`)
        .join('；')
      return conv ? `${kOnlyTemp} 转化率与产量变化：${conv}。` : kOnlyTemp
    }
    default:
      return null
  }
}

// ============ 电化学：判装置 / 写半反应 / 析现象 ============
function narrateElectrochem(step: SolveModelStep, sd: ElectrochemSolveData): string | null {
  switch (step.title) {
    case '判装置': {
      const d = sd.device
      let text = `本题装置为${d.type}`
      if (d.electrolyte) text += `（电解质：${d.electrolyte}）`
      text += '。'
      const negs = sd.electrodes.negative
      const poss = sd.electrodes.positive
      if (negs && poss) {
        const isElec = d.type === '电解池'
        text += `${negs} 为${isElec ? '阳极' : '负极'}（氧化），${poss} 为${isElec ? '阴极' : '正极'}（还原）。`
      }
      if (d.electronFlow) text += `电子流向：${d.electronFlow}。`
      return text
    }
    case '写半反应': {
      if (!sd.halfReactions.length) return null
      const list = sd.halfReactions.map((h) => `${h.electrode}：${h.equation}（${h.role}）`).join('；')
      let text = `电极反应：${list}。`
      if (sd.totalEquation) text += `总反应：${sd.totalEquation}。`
      return text
    }
    case '析现象': {
      if (sd.calc) {
        return `${sd.calc.steps.join('；')}。结论：${sd.calc.result}。`
      }
      if (sd.phenomena && sd.phenomena.length) {
        return `现象：${sd.phenomena.join('；')}。`
      }
      return null
    }
    default:
      return null
  }
}

// ============ 无机推断：找突破口 / 顺逆推 / 代回验证 ============
function narrateInference(step: SolveModelStep, sd: InferenceSolveData): string | null {
  switch (step.title) {
    case '找突破口': {
      const b = sd.breakthrough
      if (!b) return null
      let text = `本题突破口：由「${b.clue}」可推出${b.conclusion}。`
      if (sd.subtype) text = `（${sd.subtype}）${text}`
      return text
    }
    case '顺逆推': {
      const r = sd.reasoning
      if (!r || r.steps.length === 0) return null
      let text = `推断过程：${r.steps.join(' → ')}。`
      if (r.conclusion) text += ` 由此确定：${r.conclusion}。`
      return text
    }
    case '代回验证': {
      const v = sd.verification
      if (!v) return null
      return `代回验证：${v.basis}。${v.result}。`
    }
    default:
      return null
  }
}

// ============ 有机化学：认官能团 / 定反应 / 写产物 ============
function narrateOrganic(step: SolveModelStep, sd: OrganicSolveData): string | null {
  switch (step.title) {
    case '认官能团': {
      const gs = sd.functionalGroups ?? []
      if (gs.length === 0) return null
      const list = gs
        .map((g) => {
          let s = g.name
          if (g.structure) s += `（${g.structure}）`
          if (g.property) s += `：${g.property}`
          return s
        })
        .join('；')
      return `该有机物含有的官能团：${list}。`
    }
    case '定反应': {
      const rs = sd.reactions ?? []
      if (rs.length === 0) return null
      const list = rs
        .map((r) => {
          let s = ''
          if (r.reactants) s += `${r.reactants}：`
          if (r.type) s += `${r.type}`
          if (r.conclusion) s += r.type ? `，${r.conclusion}` : r.conclusion
          return s
        })
        .filter(Boolean)
        .join('；')
      return list ? `反应分析：${list}。` : null
    }
    case '写产物': {
      const p = sd.product
      if (!p) return null
      const parts: string[] = []
      if (p.formula) parts.push(`产物/目标结构为 ${p.formula}`)
      if (p.equation) parts.push(`方程式：${p.equation}`)
      if (p.count) parts.push(`同分异构体共 ${p.count}`)
      if (p.reasoning) parts.push(p.reasoning)
      if (p.conclusion) parts.push(p.conclusion)
      return parts.length ? `${parts.join('；')}。` : null
    }
    default:
      return null
  }
}
