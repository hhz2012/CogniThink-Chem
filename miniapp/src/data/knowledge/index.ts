// ============================================================
// 知识点注册表：按题目分类组织，供教学引擎做题目↔知识点匹配
// 六大专项均已有完整知识点（要点/易错/自测/教学）
// ============================================================

import { ChemCategory } from '../../types/chem'
import { redoxKnowledge, KnowledgePoint } from './redox'
import { ionicKnowledge } from './ionic'
import { equilibriumKnowledge } from './equilibrium'
import { electrochemistryKnowledge } from './electrochemistry'
import { inorganicKnowledge } from './inorganic'
import { organicKnowledge } from './organic'

export type KnowledgeLayer = 'Knowledge' | 'Application' | 'Ability'

export interface KnowledgeRef {
  id: string
  name: string
  layer: KnowledgeLayer
  summary: string
  /** 关键词用于从题干文本自动匹配知识点 */
  keywords: string[]
}

/** 关键词表：id → 检索词 */
const KEYWORDS: Record<string, string[]> = {
  // 氧化还原
  K1: ['化合价', '价态', 'Na₂O₂', '过氧化', 'Fe₃O₄', '标价'],
  K2: ['判断', '是否属于', '不属于', '复分解', '置换'],
  K3: ['本质', '特征', '电子转移'],
  K4: ['氧化剂', '还原剂', '氧化产物', '还原产物'],
  K5: ['双线桥', '单线桥', '电子转移表示'],
  K6: ['电子转移数', '转移电子', '转移'],
  K7: ['氧化性', '还原性', '强弱', '顺序'],
  K8: ['先后', '优先', '少量', '过量', '通入'],
  K9: ['KMnO₄', '高锰酸钾', '硝酸', '浓硫酸', 'Cl₂', 'Fe³⁺', '常见氧化剂'],
  K10: ['歧化', '归中', 'NO₂', 'H₂S'],
  K11: ['配平', '系数', '化合价升降法'],
  K12: ['离子方程式', '电荷守恒', 'H⁺'],
  K13: ['电子守恒', '计算', '转移电子'],
  K14: ['守恒', 'NaOH', '物质的量', '沉淀'],
  K15: ['信息', '陌生', '新反应', '写出'],
  K16: ['滴定', '标定', '终点', '消耗体积'],
  // 离子平衡
  IE1: ['电离', '弱酸', '弱碱', '醋酸', '电离平衡'],
  IE2: ['pH', '水电离', 'Kw', 'c(H⁺)', 'c(OH⁻)'],
  IE3: ['水解', '显碱性', '显酸性', 'Na₂CO₃', 'NH₄Cl', 'NaHCO₃'],
  IE4: ['浓度大小', '离子浓度', '大小关系', '排序'],
  IE5: ['守恒', '物料', '质子', '电荷守恒', '关系式'],
  IE6: ['混合', '缓冲', '等体积', 'pH=', 'Ka'],
  IE7: ['稀释', '电离度', '稀释倍数'],
  IE8: ['滴定', '突跃', '指示剂', '终点'],
  // 化学平衡
  EQ1: ['速率', 'v=', '变化量'],
  EQ2: ['平衡移动', '勒夏特列', '升温', '加压', '正向移动'],
  EQ3: ['平衡常数', 'K=', 'Ksp'],
  EQ4: ['转化率', '三段式', '起始', '平衡量'],
  EQ5: ['浓度商', 'Q', '反应方向'],
  EQ6: ['等效平衡', '一边倒', '恒温恒容', '恒温恒压'],
  EQ7: ['图像', '百分含量', '先拐先平'],
  EQ8: ['Ksp', '沉淀', '溶度积', '分步沉淀'],
  // 电化学
  EC1: ['原电池', '负极', '正极', '电子流向'],
  EC2: ['电极反应', '正极反应式', '负极反应式', '燃料电池'],
  EC3: ['电解', '惰性电极', '放电顺序', '阳极', '阴极'],
  EC4: ['转移电子', '计算', '增重', '气体体积', '串联'],
  EC5: ['复原', '恢复到', '加入'],
  EC6: ['铅蓄电池', '充电', '放电', '可充电'],
  EC7: ['腐蚀', '生锈', '吸氧腐蚀', '防护', '镀锌'],
  EC8: ['电解饱和', 'NaCl', '氯碱', '电镀'],
  // 无机推断
  IN1: ['用途', '性质', '材料', '对应关系'],
  IN2: ['检验', '鉴别', '试剂', '沉淀', 'SO₄²⁻', 'Cl⁻'],
  IN3: ['共存', '大量共存', '离子'],
  IN4: ['周期', '主族', '原子半径', '最外层', '短周期'],
  IN5: ['推断', '框图', '转化', 'A→B', '淡黄色'],
  IN6: ['制备', '除杂', '干燥', '饱和食盐水', '收集'],
  IN7: ['保存', '试剂瓶', '棕色瓶', '煤油'],
  IN8: ['工业', '流程', '铝土矿', '海水', '滤液', '滤渣'],
  // 有机
  OR1: ['官能团', '结构简式', '含有的', '羟基', '羧基', '双键'],
  OR2: ['反应类型', '取代', '加成', '属于', '酯化'],
  OR3: ['同分异构', '几种', '不考虑立体异构'],
  OR4: ['同系物', '互为同系'],
  OR5: ['推断', '分子式', '银镜', '水解生成'],
  OR6: ['合成', '路线', '原料', '以…为原料'],
  OR7: ['燃烧', 'CO₂', 'H₂O', '物质的量之比'],
  OR8: ['除杂', '提纯', '分离', '除去'],
}

const toRefs = (points: KnowledgePoint[]): KnowledgeRef[] =>
  points.map((p) => ({
    id: p.id,
    name: p.name,
    layer: p.layer,
    summary: p.summary,
    keywords: KEYWORDS[p.id] ?? [p.name],
  }))

export const CATEGORY_KNOWLEDGE: Record<ChemCategory, KnowledgeRef[]> = {
  RedoxReaction: toRefs(redoxKnowledge),
  IonicEquilibrium: toRefs(ionicKnowledge),
  ChemicalEquilibrium: toRefs(equilibriumKnowledge),
  Electrochemistry: toRefs(electrochemistryKnowledge),
  InorganicInference: toRefs(inorganicKnowledge),
  OrganicChemistry: toRefs(organicKnowledge),
}

const ALL_REFS: Record<string, KnowledgeRef> = Object.values(CATEGORY_KNOWLEDGE)
  .flat()
  .reduce((acc, k) => {
    acc[k.id] = k
    return acc
  }, {} as Record<string, KnowledgeRef>)

/** 全部完整知识点（含自测题与教学步骤） */
const ALL_POINTS: Record<string, KnowledgePoint> = [
  ...redoxKnowledge,
  ...ionicKnowledge,
  ...equilibriumKnowledge,
  ...electrochemistryKnowledge,
  ...inorganicKnowledge,
  ...organicKnowledge,
].reduce((acc, k) => {
  acc[k.id] = k
  return acc
}, {} as Record<string, KnowledgePoint>)

export function knowledgeById(id: string): KnowledgeRef | undefined {
  return ALL_REFS[id]
}

/** 获取知识点完整数据（自测题 + 教学步骤），用于逐步诊断 */
export function getFullKnowledgePoint(id: string): KnowledgePoint | undefined {
  return ALL_POINTS[id]
}

export function knowledgeOfCategory(category: ChemCategory): KnowledgeRef[] {
  return CATEGORY_KNOWLEDGE[category] ?? []
}

/**
 * 从题干文本自动匹配知识点（按关键词命中数排序）
 * 命中为空时由调用方决定回退策略。
 */
export function matchKnowledge(category: ChemCategory, text: string): KnowledgeRef[] {
  const pool = knowledgeOfCategory(category)
  const scored = pool
    .map((kp) => {
      const hits = kp.keywords.filter((kw) => text.includes(kw)).length
      return { kp, hits }
    })
    .filter((x) => x.hits > 0)
    .sort((a, b) => b.hits - a.hits)
  return scored.slice(0, 3).map((x) => x.kp)
}
