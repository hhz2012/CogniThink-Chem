// 通用 A–F 原子任务链注册表 + 门控状态机（纯数据 / 逻辑层，不接 UI）
// 轴 2（解题侧）：MODULE_CHAINS 为 5 个首页模块各定义一条 A–F 完整链。
// 轴 1（反应讲解侧）：REACTION_SCAFFOLD_CHAIN 导出 A–E 子集链（F「匹配选项作答」省略，
//   首期反应卡降级为揭示文本，无作答门控，见 spec Open Questions 裁决）。
// 门控：前序未 passed 则后续 locked；访问 locked / 越界步返回安全空值（null），不抛错。

import type {
  AtomicTask,
  AtomicTaskKind,
  ScaffoldChain,
  ScaffoldModuleKind,
  StepGateState,
} from '../types/scaffold'

// ========== A–F 通用任务模板（spec「A–F 原子任务定义」表） ==========
// 各模块共享 title / goal / interaction / pitfall；仅 checks 按模块微调（spec「5 模块 × 每步判断项」表）。
type TaskTemplate = Omit<AtomicTask, 'index' | 'checks'>

const TASK_TEMPLATES: Record<AtomicTaskKind, TaskTemplate> = {
  A: {
    kind: 'A',
    title: '识别体系组分（微观粒子清单）',
    goal: '列出体系中的主要粒子与物质。',
    interaction: 'multi-check',
    pitfall: '跳过这步凭感觉写反应。',
  },
  B: {
    kind: 'B',
    title: '识别反应角色（变价 / 平衡角色）',
    goal: '标出氧化剂、还原剂及全部变价物种。',
    interaction: 'multi-check',
    pitfall: '漏标变价物种。',
  },
  C: {
    kind: 'C',
    title: '识别约束条件（最核心、引擎最大价值点）',
    goal: '识别改变反应走向的约束条件（酸碱 / 用量 / 温度等）。',
    interaction: 'single-select',
    pitfall: '漏条件导致整套结论错。',
  },
  D: {
    kind: 'D',
    title: '竞争排序（多物质共存时）',
    goal: '判断多物种竞争反应的先后顺序。',
    interaction: 'order',
    pitfall: '忽略竞争直接写主反应。',
  },
  E: {
    kind: 'E',
    title: '推导产物 / 平衡结果',
    goal: '结合全部条件推导产物或平衡结果。',
    interaction: 'multi-check',
    pitfall: '产物与条件矛盾。',
  },
  F: {
    kind: 'F',
    title: '回到原题，匹配选项 / 作答',
    goal: '用推理结论匹配原题选项并作答。',
    interaction: 'single-select',
    pitfall: '推理对但选错。',
  },
}

/** 由模板 + 序号 + 模块专属 checks 构造一个原子任务 */
const task = (kind: AtomicTaskKind, index: number, checks: string[]): AtomicTask => ({
  ...TASK_TEMPLATES[kind],
  index,
  checks,
})

// ========== 轴 2：5 模块 A–F 链（spec「5 模块 × 每步判断项」表） ==========
export const MODULE_CHAINS: Record<ScaffoldModuleKind, ScaffoldChain> = {
  // ⚡ 氧化还原推理
  'redox-reasoning': {
    module: 'redox-reasoning',
    name: '氧化还原推理链',
    tasks: [
      task('A', 1, ['不漏粒子', '不把不会电离的物质拆成离子', '把题干涉及的全部氧化剂 / 还原剂物种列入']),
      task('B', 2, ['化合价不看错', '不把不变价物质（如 H⁺）当变价', '标出全部氧化剂与还原剂（可多种）']),
      task('C', 3, ['NO₃⁻ 须酸性、ClO⁻ 酸碱产物不同、Ksp 看浓度积、电解看放电顺序', '酸 / 碱性、浓度、温度是否改变产物']),
      task('D', 4, ['还原性 I⁻ > Fe²⁺ > Br⁻', '多种还原剂 / 氧化剂判断反应先后', '无竞争则跳过']),
      task('E', 5, ['酸性不生成氢氧化物沉淀', '过量改变产物', '产物价态与条件一致']),
      task('F', 6, ['结论与选项一致']),
    ],
  },

  // 🧪 离子体系推理（共存 / 离子方程式）
  'ionic-system': {
    module: 'ionic-system',
    name: '离子体系推理链',
    tasks: [
      task('A', 1, ['不漏粒子', '不把不会电离的物质拆成离子', '列出全部离子与分子']),
      task('B', 2, ['化合价不看错', '不把不变价物质（如 H⁺）当变价', '区分哪些离子发生复分解、哪些发生氧还（两类竞争）']),
      task('C', 3, ['NO₃⁻ 须酸性、ClO⁻ 酸碱产物不同、Ksp 看浓度积', '环境条件 + 试剂用量（少量 / 过量）']),
      task('D', 4, ['还原性 I⁻ > Fe²⁺ > Br⁻', '沉淀 Ksp 小者优先沉淀', '多种可沉淀离子判断沉淀先后']),
      task('E', 5, ['酸性不生成氢氧化物沉淀', '过量改变产物', '判断能否共存并写出离子方程式']),
      task('F', 6, ['结论与选项一致']),
    ],
  },

  // 💧 水溶液平衡（电离水解 / 粒子浓度排序）
  'aqueous-equilibrium': {
    module: 'aqueous-equilibrium',
    name: '水溶液平衡推理链',
    tasks: [
      task('A', 1, ['不漏粒子', '不把不会电离的物质拆成离子', '列全电离与水解产生的全部粒子（含水电离）']),
      task('B', 2, ['化合价不看错', '不把不变价物质（如 H⁺）当变价', '区分哪些粒子来自电离、哪些来自水解']),
      task('C', 3, ['酸 / 碱性环境决定电离 / 水解方向', '温度、浓度、同离子效应的影响']),
      task('D', 4, ['电离 / 水解谁占主导', '多元弱酸分步电离以第一步为主', '无主次之分则跳过']),
      task('E', 5, ['酸性不生成氢氧化物沉淀', '过量改变产物', '结合守恒式排出粒子浓度大小']),
      task('F', 6, ['结论与选项一致']),
    ],
  },

  // 🔁 连续反应推理（铝铁硫氮氯）
  'sequential-reaction': {
    module: 'sequential-reaction',
    name: '连续反应推理链',
    tasks: [
      task('A', 1, ['不漏粒子', '列出初始体系的全部粒子']),
      task('B', 2, ['判断加入试剂是少量还是过量', '标记每一步的反应物角色']),
      task('C', 3, ['每一步的反应条件（温度、用量、介质）是否改变产物', '分步反应的先后条件']),
      task('D', 4, ['按加入顺序确定每步的中间产物与终产物', '前步产物作为后步反应物', '无多步竞争则跳过']),
      task('E', 5, ['酸性不生成氢氧化物沉淀', '过量改变产物', '解读图像（沉淀量 / 气体量 / pH）各节点含义']),
      task('F', 6, ['结论与选项一致']),
    ],
  },

  // 🔋 电化学推理
  'electrochem-reasoning': {
    module: 'electrochem-reasoning',
    name: '电化学推理链',
    tasks: [
      task('A', 1, ['不漏粒子', '列出体系粒子与电极材料']),
      task('B', 2, ['判断电极名称（阴 / 阳极、正 / 负极）与反应类型（氧化 / 还原）', '不把电极名称搞反', '不把不变价物种当变价']),
      task('C', 3, ['介质环境（质子膜 / 阴离子膜）', '是否通电、离子迁移方向', '酸 / 碱性介质改变电极产物']),
      task('D', 4, ['阳极 / 阴极放电顺序', '阳离子向阴极、阴离子向阳极迁移', '无多种放电物种则跳过']),
      task('E', 5, ['酸性不生成氢氧化物沉淀', '过量改变产物', '写出各电极产物与现象']),
      task('F', 6, ['结论与选项一致']),
    ],
  },

  // ⚖️ 化学平衡移动（R4 新增）
  'chemical-equilibrium': {
    module: 'chemical-equilibrium',
    name: '化学平衡移动推理链',
    tasks: [
      task('A', 1, ['写出可逆反应方程式与平衡状态', '不漏反应物 / 产物', '标注各物质状态（气 / 液 / 固）']),
      task('B', 2, ['判断改变的条件属于浓度 / 温度 / 压强 / 催化剂中的哪一类', '不把催化剂当成改变平衡的条件', '识别条件变化的方向（增大 / 减小、升温 / 降温）']),
      task('C', 3, ['温度对吸热 / 放热方向的影响', '压强对气体分子数变化方向的影响', '浓度对反应物 / 产物方向的影响', '催化剂只改速率不改平衡']),
      task('D', 4, ['多条件同时改变时判断主次', '稀有气体充入看是否改变分压', '无竞争则跳过']),
      task('E', 5, ['平衡移动方向与 K 变化一致（只有温度变 K）', '转化率与产率的变化方向', '结合条件推出新平衡态']),
      task('F', 6, ['结论与选项一致']),
    ],
  },

  // 🧩 无机推断（R4 新增）
  'inorganic-inference': {
    module: 'inorganic-inference',
    name: '无机推断推理链',
    tasks: [
      task('A', 1, ['不漏粒子 / 物质', '把题干现象、特征颜色、特殊反应全部列出', '标注已知物与未知物']),
      task('B', 2, ['识别特征突破口（颜色 / 气味 / 沉淀 / 气体 / 焰色 / 电解 / 高温）', '不止一个突破口时全部标记', '不把无关现象当突破口']),
      task('C', 3, ['推断子类型（框图 / 周期律 / 工业流程 / 离子检验 / 物质鉴别 / 离子共存 / 一步转化）', '该子类型的关键约束条件']),
      task('D', 4, ['从突破口顺推与逆推结合', '每一步转化都要有反应或性质依据', '无多分支则跳过']),
      task('E', 5, ['把推出的物质代回原题验证现象与转化', '检查是否满足全部条件', '产物与条件不矛盾']),
      task('F', 6, ['结论与选项一致']),
    ],
  },

  // 🧬 有机反应（R4 新增）
  'organic-reaction': {
    module: 'organic-reaction',
    name: '有机反应推理链',
    tasks: [
      task('A', 1, ['列出全部官能团与结构特征', '不漏官能团（羟基 / 羧基 / 醛基 / 碳碳双键等）', '标注碳骨架与不饱和度线索']),
      task('B', 2, ['识别反应物与试剂角色', '判断反应类型（取代 / 加成 / 消去 / 氧化 / 还原 / 酯化 / 水解 / 聚合）', '不把反应类型搞反']),
      task('C', 3, ['反应条件（温度 / 催化剂 / 介质 / 试剂）对产物的影响', '官能团的反应活性顺序', '定位反应发生在哪个官能团 / 哪个碳位']),
      task('D', 4, ['多官能团时判断反应主次与先后', '聚合反应区分加聚与缩聚', '无竞争则跳过']),
      task('E', 5, ['写出产物结构并核对价键规则（碳四价等）', '不饱和度与反应类型一致', '产物与条件不矛盾']),
      task('F', 6, ['结论与选项一致']),
    ],
  },
}

// ========== 轴 1：反应侧 A–E 子集链（F 省略，首期降级为揭示文本无门控） ==========
export const REACTION_SCAFFOLD_CHAIN: ScaffoldChain = {
  module: 'reaction',
  name: '反应推理链（A–E 子集）',
  tasks: [
    task('A', 1, ['不漏粒子', '不把不会电离的物质拆成离子', '列出反应涉及的全部反应物与产物物种']),
    task('B', 2, ['化合价不看错', '不把不变价物质（如 H⁺）当变价', '标出氧化剂 / 还原剂或电离 / 水解角色']),
    task('C', 3, ['NO₃⁻ 须酸性、ClO⁻ 酸碱产物不同、Ksp 看浓度积、电解看放电顺序', '介质 / 浓度 / 温度 / 通电 / 膜是否改变走向']),
    task('D', 4, ['还原性 I⁻ > Fe²⁺ > Br⁻', '沉淀 Ksp 小者优先沉淀', '无竞争则跳过']),
    task('E', 5, ['酸性不生成氢氧化物沉淀', '过量改变产物', '结合 A–D 推导产物 / 平衡结果']),
  ],
}

/** 取某模块的 A–F 链 */
export const getModuleChain = (module: ScaffoldModuleKind): ScaffoldChain => MODULE_CHAINS[module]

// ========== 门控状态机（纯函数，不就地修改、不抛错） ==========

/**
 * 初始门控状态：第 0 步 unlocked，其余 locked。
 * n ≤ 0 返回空数组（安全空值）。
 */
export const initialGateStates = (n: number): StepGateState[] => {
  if (n <= 0) return []
  const states: StepGateState[] = new Array(n).fill('locked')
  states[0] = 'unlocked'
  return states
}

/** 安全读取某步状态；越界或非法下标一律返回 'locked'（不抛错） */
export const getGateState = (states: StepGateState[], i: number): StepGateState =>
  i < 0 || i >= states.length ? 'locked' : states[i]

/**
 * 标记第 i 步 passed 并解锁下一步。
 * 允许该步当前为 'unlocked' 或 'failed'（答错后重试答对）；
 * 已 passed / locked 或越界时返回不变的副本（不抛错）。
 */
export const markPassed = (states: StepGateState[], i: number): StepGateState[] => {
  const next = states.slice()
  if (i < 0 || i >= next.length) return next
  if (next[i] === 'locked' || next[i] === 'passed') return next
  next[i] = 'passed'
  if (i + 1 < next.length) next[i + 1] = 'unlocked'
  return next
}

/**
 * 重试：将 failed 步重置为 unlocked（清空上次失败态），便于重新作答；
 * unlocked / passed / locked 或越界时返回不变的副本（不抛错）。
 */
export const resetStep = (states: StepGateState[], i: number): StepGateState[] => {
  const next = states.slice()
  if (i < 0 || i >= next.length) return next
  if (next[i] === 'failed') next[i] = 'unlocked'
  return next
}

/**
 * 标记第 i 步 failed（答错但仍锁定在当前步可重试）。
 * 仅当该步当前为 'unlocked' 时生效；其余情况返回不变的副本（不抛错）。
 */
export const markFailed = (states: StepGateState[], i: number): StepGateState[] => {
  const next = states.slice()
  if (i < 0 || i >= next.length) return next
  if (next[i] !== 'unlocked') return next
  next[i] = 'failed'
  return next
}

/**
 * 访问某步任务：locked 或越界返回 null（安全空值，不抛错）；
 * unlocked / failed / passed 均返回任务（passed 可回顾，failed 可重试）。
 */
export const resolveStep = (
  chain: ScaffoldChain,
  states: StepGateState[],
  i: number,
): AtomicTask | null => {
  if (i < 0 || i >= chain.tasks.length) return null
  if (getGateState(states, i) === 'locked') return null
  return chain.tasks[i]
}
