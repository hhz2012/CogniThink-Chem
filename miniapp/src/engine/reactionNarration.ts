// 反应侧实例化引擎：与题型侧 engine/solveNarration.ts 的 narrateStep 同构。
// 把一条反应的 ReactionSolveData 真值，拼成「结合本条反应具体内容」的实例化讲解文字。
//
// 设计原则：
// 1. 数据驱动——所有真实取值均来自 entry.solveData，不硬编码任何具体反应的静态文案（经验 533966）。
// 2. 优雅降级——无 solveData 或该步暂无对应真值时返回 null，调用方回退 step.analyze（永不崩）。
// 3. 复用——氧化还原的价态/电子格式化复用 solveNarration 导出的 fmtVal / fmtChange，避免双份维护漂移。

import { SolveModelStep } from './solveModels'
import { fmtVal, fmtChange } from './solveNarration'
import { ReactionEntry, ReactionSolveData } from '../types/reaction'

/** 有机 9 子类型共享同一套真值字段（OrganicTruthBase），合并为一个类型便于统一叙述 */
type OrganicData = Extract<ReactionSolveData, { kind: `reaction-organic-${string}` }>

export function narrateReaction(step: SolveModelStep, entry: ReactionEntry): string | null {
  const sd = entry.solveData
  if (!sd) return null
  switch (sd.kind) {
    case 'reaction-combination':
      return narrateCombination(step, sd)
    case 'reaction-decomposition':
      return narrateDecomposition(step, sd)
    case 'reaction-displacement':
      return narrateDisplacement(step, sd)
    case 'reaction-double-displacement':
      return narrateDoubleDisplacement(step, sd)
    case 'reaction-combustion':
      return narrateCombustion(step, sd)
    case 'reaction-redox':
      return narrateRedox(step, sd)
    case 'reaction-electrode':
      return narrateElectrode(step, sd)
    case 'reaction-ion-equilibrium':
      return narrateIonEquilibrium(step, sd)
    case 'reaction-thermochemical':
      return narrateThermochemical(step, sd)
    case 'reaction-organic-substitution':
    case 'reaction-organic-addition':
    case 'reaction-organic-elimination':
    case 'reaction-organic-oxidation':
    case 'reaction-organic-reduction':
    case 'reaction-organic-esterification':
    case 'reaction-organic-hydrolysis':
    case 'reaction-organic-polyaddition':
    case 'reaction-organic-polycondensation':
      return narrateOrganic(step, sd)
    case 'reaction-generic':
      return narrateGeneric(step, sd)
    default:
      return null
  }
}

// ============ 化合反应：认特征 / 判条件 / 析价态 / 验守恒 ============
function narrateCombination(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-combination' }>,
): string | null {
  switch (step.title) {
    case '认特征': {
      if (sd.reactantCount == null && sd.productCount == null) return null
      const parts: string[] = []
      if (sd.reactantCount != null) parts.push(`反应物有 ${sd.reactantCount} 种`)
      if (sd.productCount != null) parts.push(`产物有 ${sd.productCount} 种`)
      const tail = sd.productCount === 1 ? '，符合「多变一」的化合反应特征' : ''
      return `${parts.join('，')}${tail}。`
    }
    case '判条件':
      return sd.conditions?.length ? `本条反应条件：${sd.conditions.join('、')}。条件不同产物可能不同，先看准条件再定产物。` : null
    case '析价态': {
      if (sd.isRedox == null && !sd.valenceChanges?.length) return null
      const parts: string[] = []
      if (sd.isRedox != null) parts.push(sd.isRedox ? '本反应有价态变化，属于氧化还原反应' : '本反应各元素价态不变，不属于氧化还原反应')
      if (sd.valenceChanges?.length) parts.push(`价态变化要点：${sd.valenceChanges.join('；')}`)
      return `${parts.join('；')}。`
    }
    case '验守恒': {
      const parts: string[] = []
      if (sd.equation) parts.push(`配平方程式：${sd.equation}`)
      if (sd.phenomenon) parts.push(`现象：${sd.phenomenon}`)
      if (!parts.length) return null
      return `${parts.join('；')}。最后复核原子守恒与系数最简。`
    }
    default:
      return null
  }
}

// ============ 分解反应：认特征 / 判条件 / 定产物 / 验守恒 ============
function narrateDecomposition(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-decomposition' }>,
): string | null {
  switch (step.title) {
    case '认特征':
      return sd.products?.length ? `分解产物有 ${sd.products.length} 种，符合「一变多」的分解反应特征。` : null
    case '判条件':
      return sd.energyType
        ? `分解所需外加能量类型：${sd.energyType}。`
        : sd.conditions?.length
          ? `反应条件：${sd.conditions.join('、')}。`
          : null
    case '定产物':
      return sd.products?.length ? `分解产物：${sd.products.join('、')}。注意变价元素在产物中的价态。` : null
    case '验守恒':
      return sd.equation ? `配平方程式：${sd.equation}；单质气体标 ↑、沉淀标 ↓，复核原子守恒。` : null
    default:
      return null
  }
}

// ============ 置换反应：认特征 / 判活动性 / 写产物 / 验方向与守恒 ============
function narrateDisplacement(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-displacement' }>,
): string | null {
  switch (step.title) {
    case '认特征':
      return sd.equation ? `通式符合「单质 + 化合物 → 新单质 + 新化合物」：${sd.equation}。` : null
    case '判活动性':
      return sd.activityBasis
        ? `活动性判据：${sd.activityBasis}。排在前面的金属能置换后面的金属或氢；K、Ca、Na 会先与水反应。`
        : null
    case '写产物': {
      const parts: string[] = []
      if (sd.displacedElement) parts.push(`被置换出的单质：${sd.displacedElement}`)
      if (sd.valenceNote) parts.push(`价态注意：${sd.valenceNote}`)
      return parts.length ? `${parts.join('；')}。` : null
    }
    case '验方向与守恒':
      return sd.equation ? `验证方向符合「强置换弱」；配平 ${sd.equation}，检查原子与电子守恒。` : null
    default:
      return null
  }
}

// ============ 复分解反应：认特征 / 判发生条件 / 拆离子写式子 / 验守恒 ============
function narrateDoubleDisplacement(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-double-displacement' }>,
): string | null {
  switch (step.title) {
    case '认特征':
      return sd.equation ? `两化合物互相交换成分：${sd.equation}；反应中各元素化合价不变，属非氧化还原反应。` : null
    case '判发生条件':
      return sd.drivingForce ? `复分解发生条件满足：生成${sd.drivingForce}。` : null
    case '拆离子写式子': {
      const parts: string[] = []
      if (sd.ionicEquation) parts.push(`离子方程式：${sd.ionicEquation}`)
      if (sd.solubilityNote) parts.push(`拆写说明：${sd.solubilityNote}`)
      return parts.length ? `${parts.join('；')}。` : null
    }
    case '验守恒':
      return sd.ionicEquation || sd.equation
        ? `配平后检查电荷守恒与原子守恒：${sd.ionicEquation ?? sd.equation}。`
        : null
    default:
      return null
  }
}

// ============ 燃烧：认可燃物与助燃物 / 判完全不完全 / 析价态与能量 / 配平燃烧式 ============
function narrateCombustion(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-combustion' }>,
): string | null {
  switch (step.title) {
    case '认可燃物与助燃物': {
      const parts: string[] = []
      if (sd.combustible) parts.push(`可燃物：${sd.combustible}`)
      if (sd.oxidizer) parts.push(`助燃物：${sd.oxidizer}`)
      return parts.length ? `${parts.join('；')}。` : null
    }
    case '判完全/不完全': {
      const parts: string[] = []
      if (sd.complete != null) parts.push(sd.complete ? '完全燃烧' : '不完全燃烧')
      if (sd.products?.length) parts.push(`产物：${sd.products.join('、')}`)
      return parts.length ? `${parts.join('，')}。氧气是否充足决定氧化产物。` : null
    }
    case '析价态与能量':
      return sd.phenomenon
        ? `燃烧为氧化还原反应且放热（ΔH < 0）；现象：${sd.phenomenon}。`
        : sd.equation
          ? `燃烧方程式：${sd.equation}，属放热氧化还原反应。`
          : null
    case '配平燃烧式':
      return sd.equation ? `配平：${sd.equation}。按 C → H → O 顺序配平，标注点燃条件。` : null
    default:
      return null
  }
}

// ============ 氧化还原：标价态 / 算电子 / 配守恒 / 验方向 —— 复用题型侧格式化 ============
function narrateRedox(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-redox' }>,
): string | null {
  const hs = sd.halfReactions ?? []
  switch (step.title) {
    case '标价态': {
      if (!hs.length) return null
      const head = sd.equation ? `本反应方程式：${sd.equation}。` : ''
      const parts = hs.map(fmtChange)
      const changed = hs.filter((h) => h.from !== h.to)
      const tail = changed.length ? `化合价发生变化的元素是 ${changed.map((h) => h.species).join('、')}。` : ''
      return `${head}${parts.join('；')}。${tail}`
    }
    case '算电子': {
      if (!hs.length) return null
      const parts = hs.map((h) => {
        const per = Math.abs(h.to - h.from)
        const verb = h.role === '氧化' ? '失' : '得'
        return `${h.species}：${fmtVal(h.from)}→${fmtVal(h.to)}，每个 ${h.species} ${verb} ${per}e⁻（被${h.role}）`
      })
      let agent = ''
      if (sd.oxidants?.length || sd.reductants?.length) {
        agent = `氧化剂：${(sd.oxidants ?? []).join('、') || '—'}；还原剂：${(sd.reductants ?? []).join('、') || '—'}。`
      }
      return `${parts.join('；')}。${agent}`
    }
    case '配守恒': {
      const bal = sd.balance
      if (!bal) return null
      let text = `失电子总数 ${bal.oxidizedCount} = 得电子总数 ${bal.reducedCount}，最小公倍数 ${bal.lcm}。`
      if (bal.ratio) text += `变价物质系数比：${bal.ratio}。`
      if (bal.coefficients?.length) {
        text += `配平系数：${bal.coefficients.map((c) => `${c.formula} ${c.value}`).join('，')}。`
      }
      return text
    }
    case '验方向': {
      if (sd.directionBasis) {
        return `用强弱律验证方向：${sd.directionBasis}。氧化剂的氧化性强于氧化产物、还原剂的还原性强于还原产物，故方向合理。`
      }
      if (sd.oxidants?.length || sd.reductants?.length) {
        return `氧化剂：${(sd.oxidants ?? []).join('、') || '—'}；还原剂：${(sd.reductants ?? []).join('、') || '—'}。按「强生弱」规律验证反应方向。`
      }
      return null
    }
    default:
      return null
  }
}

// ============ 电极反应：判装置 / 定电极与放电顺序 / 写半反应 / 合并验守恒 ============
function narrateElectrode(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-electrode' }>,
): string | null {
  const hs = sd.halfReactions ?? []
  switch (step.title) {
    case '判装置':
      return sd.deviceType ? `本装置为${sd.deviceType}${sd.equation ? `，总反应：${sd.equation}` : ''}。` : null
    case '定电极与放电顺序': {
      if (!hs.length) return null
      const list = hs.map((h) => `${h.electrode}（${h.role}）`).join('、')
      return `各电极反应类型：${list}。负极/阳极失电子（氧化），正极/阴极得电子（还原）；按放电顺序确定放电物种。`
    }
    case '写半反应': {
      if (!hs.length) return null
      const list = hs.map((h) => `${h.electrode}：${h.equation}（${h.role}）`).join('；')
      let text = `电极反应：${list}。`
      if (sd.totalEquation) text += `总反应：${sd.totalEquation}。`
      return text
    }
    case '合并验守恒':
      if (sd.totalEquation) return `两电极电子数配平后相加，总反应：${sd.totalEquation}。检验总反应的原子守恒与电荷守恒。`
      if (sd.equation) return `总反应：${sd.equation}。检验电子守恒（两电极转移电子数相等）与原子守恒。`
      return null
    default:
      return null
  }
}

// ============ 离子平衡：认平衡类型 / 写平衡式 / 判移动方向 / 写守恒式 ============
function narrateIonEquilibrium(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-ion-equilibrium' }>,
): string | null {
  switch (step.title) {
    case '认平衡类型':
      return sd.equilibriumType ? `本平衡属于${sd.equilibriumType}平衡。` : null
    case '写平衡式':
      return sd.equilibriumEquation
        ? `平衡方程式：${sd.equilibriumEquation}。用可逆号；弱电解质部分电离、多元弱酸分步电离。`
        : null
    case '判移动方向': {
      if (!sd.shiftFactors?.length) return null
      const list = sd.shiftFactors.map((f) => `${f.factor}（${f.change}）→ 平衡${f.direction}移动`).join('；')
      return `逐条件判断：${list}。依据勒夏特列原理，平衡向减弱改变的方向移动。`
    }
    case '写守恒式': {
      if (!sd.conservations?.length) return null
      const list = sd.conservations.map((c) => `${c.type}守恒：${c.expression}`).join('；')
      return `可列出的守恒式：${list}。质子守恒可由电荷守恒减物料守恒得到。`
    }
    default:
      return null
  }
}

// ============ 热化学：写配平方程 / 标状态 / 算 ΔH / 验 ============
function narrateThermochemical(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-thermochemical' }>,
): string | null {
  switch (step.title) {
    case '写配平方程':
      return sd.equation ? `配平后的化学方程式：${sd.equation}。热化学方程式计量数表示物质的量，可为分数。` : null
    case '标状态': {
      if (!sd.states || !Object.keys(sd.states).length) return null
      const list = Object.entries(sd.states)
        .map(([k, v]) => `${k}(${v})`)
        .join('、')
      return `各物质状态：${list}。状态不同，ΔH 数值不同。`
    }
    case '算 ΔH': {
      if (!sd.deltaH && !sd.calcSteps?.length) return null
      const parts: string[] = []
      if (sd.calcSteps?.length) parts.push(sd.calcSteps.join('；'))
      if (sd.deltaH) parts.push(`ΔH = ${sd.deltaH}`)
      return `${parts.join('；')}。注意放热 ΔH < 0，且数值与计量数成正比。`
    }
    case '验':
      return sd.deltaH ? `复核：ΔH = ${sd.deltaH}，符号（放热为负）、数值与计量数对应、单位 kJ/mol。` : null
    default:
      return null
  }
}

// ============ 有机反应：共享前置（认官能团 / 定反应类型）+ 各子类型专属 ============
function narrateOrganic(step: SolveModelStep, sd: OrganicData): string | null {
  const fg = sd.functionalGroups
  const rx = sd.reaction
  const prod = sd.product
  switch (step.title) {
    // ---- 共享前置 2 步 ----
    case '认官能团': {
      if (!fg?.length) return null
      const list = fg
        .map((g) => {
          let s = g.name
          if (g.structure) s += `（${g.structure}）`
          if (g.property) s += `：${g.property}`
          return s
        })
        .join('；')
      return `该反应物含有的官能团：${list}。`
    }
    case '定反应类型': {
      if (!rx) return null
      const parts: string[] = []
      if (rx.type) parts.push(`反应子类型：${rx.type}`)
      if (rx.reagents) parts.push(`试剂：${rx.reagents}`)
      if (rx.conditions) parts.push(`条件：${rx.conditions}`)
      if (rx.conclusion) parts.push(rx.conclusion)
      return parts.length ? `${parts.join('；')}。` : null
    }

    // ---- 子类型专属：试剂 / 位点 / 单体等 ----
    case '定取代位点与试剂':
      return rx?.reagents ? `取代试剂：${rx.reagents}${rx.conclusion ? `；${rx.conclusion}` : ''}。` : null
    case '认不饱和键':
      return fg?.length ? `可加成的不饱和键：${fg.map((g) => g.name).join('、')}；加成后不饱和度降低。` : null
    case '定试剂与方向': {
      let text = ''
      if (rx?.reagents) text += `加成试剂：${rx.reagents}。`
      if (sd.kind === 'reaction-organic-addition' && sd.regioselectivity) text += `区域选择性：${sd.regioselectivity}。`
      return text || null
    }
    case '确认消去位点':
      return rx?.conclusion ?? null
    case '定反应条件':
      return rx?.conditions ? `反应条件：${rx.conditions}。` : null
    case '认可氧化基团':
    case '认可还原基团':
    case '认可水解键':
      return fg?.length ? `关键官能团 / 化学键：${fg.map((g) => g.name).join('、')}。` : null
    case '定氧化剂与条件':
    case '定还原剂':
      return rx?.reagents ? `试剂：${rx.reagents}${rx.conditions ? `（${rx.conditions}）` : ''}。` : null
    case '认反应物':
      return rx?.reagents ? `反应物：${rx.reagents}。酯化本质是「酸脱羟基、醇脱氢」。` : null
    case '认单体':
    case '认双官能团单体':
      if ((sd.kind === 'reaction-organic-polyaddition' || sd.kind === 'reaction-organic-polycondensation') && sd.monomer) {
        return `单体：${sd.monomer}。`
      }
      return rx?.reagents ? `单体 / 反应物：${rx.reagents}。` : null
    case '定断键方式':
      return rx?.conclusion ?? null
    case '定脱除小分子':
      return sd.kind === 'reaction-organic-polycondensation' && sd.byproduct ? `缩合脱去小分子：${sd.byproduct}。` : null
    case '断双键成链':
      return sd.kind === 'reaction-organic-polyaddition' && sd.repeatUnit
        ? `重复单元（链节）：${sd.repeatUnit}；加聚链节与单体组成相同。`
        : null

    // ---- 产物类（多个子类型标题共享同一套产物真值） ----
    case '写产物':
    case '写产物并验':
    case '写不饱和产物':
    case '写氧化产物并验':
    case '写还原产物并验':
    case '写酯与水并验':
    case '写水解产物并验':
    case '写聚合物并验':
    case '写链节与聚合物并验': {
      if (!prod) return null
      const parts: string[] = []
      if (prod.formula) parts.push(`产物为 ${prod.formula}`)
      if (prod.equation) parts.push(`方程式：${prod.equation}`)
      if (sd.kind === 'reaction-organic-polyaddition' && sd.repeatUnit) parts.push(`链节：${sd.repeatUnit}`)
      if (sd.kind === 'reaction-organic-polycondensation' && sd.byproduct) parts.push(`脱去小分子 ${sd.byproduct}`)
      if (prod.reasoning) parts.push(prod.reasoning)
      if (prod.conclusion) parts.push(prod.conclusion)
      return parts.length ? `${parts.join('；')}。` : null
    }
    case '验条件与守恒':
      return rx?.conditions
        ? `标注条件：${rx.conditions}；配平并检查原子守恒。`
        : sd.equation
          ? `配平：${sd.equation}，复核原子守恒。`
          : null
    default:
      return null
  }
}

// ============ 通用降级：写方程式 / 判类型 / 验守恒 ============
function narrateGeneric(
  step: SolveModelStep,
  sd: Extract<ReactionSolveData, { kind: 'reaction-generic' }>,
): string | null {
  switch (step.title) {
    case '写方程式':
      return sd.equation
        ? `方程式：${sd.equation}${sd.conditions?.length ? `（条件：${sd.conditions.join('、')}）` : ''}。`
        : null
    case '判类型':
      return null // 通用模型无类型真值，回退 step.analyze
    case '验守恒':
      return sd.equation ? `检验 ${sd.equation} 的原子守恒（离子反应还要电荷守恒）。` : null
    default:
      return null
  }
}
