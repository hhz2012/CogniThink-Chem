// 统一思维链 DAG 契约（R4：全题型思维链 DAG 统一规范）
// 统摄题型侧（SolveModelStep）、反应侧（ReactionSolveModel.steps）、脚手架侧（AtomicTask）三套资产：
//   把「线性步骤串」升级为「有依赖边、有条件分支、可门控」的推导图。
// 纯加性文件：不修改 chem.ts / scaffold.ts / reaction.ts 的现有类型签名；
//   ThoughtChainNode 是 SolveModelStep 的超集（讲题骨架）+ AtomicTask 的作答子集（interaction）。

import type { ChemCategory } from './chem'
import type { AtomicTaskInteraction, ScaffoldModuleKind } from './scaffold'

/** 认知层级：与 engine/solveModels.ModelLayer 保持一致 */
export type ThoughtLayer = 'Knowledge' | 'Application' | 'Ability'

/**
 * 元求解阶段（跨题型统一骨架，见 engine/thoughtChainMetaDAG.ts）：
 *   S1 读题定向 — 题面给什么、问什么，锁定解题目标
 *   S2 条件识别 — 介质 / 用量 / 浓度 / 温度 / 装置 / 官能团等外部条件与体系特征
 *   S3 反应建模 — 是否反应、单步/多步、推产物；或建立守恒/平衡关系、逆向推断转化链
 *   S4 推导结论 — 按规则一步步推中间量（配平 / 浓度关系 / 移动方向 / 现象 / 计算）
 *   S5 问题关联 — 把问题指向的量映射到已推导结论（收敛到答案）
 *   S6 校验作答 — 守恒 / 价键 / 代回验证 → 确定答案
 * 题型 DAG 节点通过 metaPhase 标注其在元骨架中的位置；S1/S5 当前多为缺口（无对应实例节点）。
 */
export type MetaPhase = 'S1' | 'S2' | 'S3' | 'S4' | 'S5' | 'S6'

/** 元阶段的线性顺序（供导航 / 分组使用） */
export const META_PHASE_ORDER: MetaPhase[] = ['S1', 'S2', 'S3', 'S4', 'S5', 'S6']

/**
 * 推导图节点：
 * - title / goal / analyze / layer / stage 复用 SolveModelStep（讲题骨架，题型通用、不含本题答案）；
 * - dependsOn 显式声明前置节点 id（DAG 依赖边）；空数组 = 根节点；
 * - skipWhen：可跳过条件键（如 'single-pair'），由调用方条件求值决定是否渲染/门控（跳过视为已过）；
 * - branchKey：该节点结论受条件分支控制，复用 ScaffoldBranch / resolveBranch 解析；
 *   单键（string）或多键数组（string[]，结论同时受多个分支控制，如电化学半反应 device-type + medium）；
 * - metaPhase：该节点在跨题型统一元骨架中的阶段（S1–S6）；缺省 = 未归类（元 DAG 节点必带）。
 * - interaction：该步是否可门控作答（复用 AtomicTaskInteraction）；缺省为只读推导节点。
 */
export interface ThoughtChainNode {
  /** 稳定 id，如 'redox.infer-products'（跨版本稳定，供 dependsOn 引用） */
  id: string
  /** 展示序号（线性化视图 / 门控初始态用） */
  index: number
  /** 步骤标题 */
  title: string
  /** 这一步的目标（通用讲题文案，不含具体题目答案） */
  goal: string
  /** 通用分析方法（讲题文案，不包含具体题目的答案）；脚手架侧 AtomicTask 无此字段，缺省 */
  analyze?: string
  /** 认知层级；脚手架侧 AtomicTask 无此字段，缺省 */
  layer?: ThoughtLayer
  /** 阶段分组（泛化 redox 的 'stage1'|'stage2'），如 '主链路' / '配平子专项'；缺省不分组 */
  stage?: string
  /** 前置节点 id（DAG 依赖边）；空数组 = 根节点 */
  dependsOn: string[]
  /** 可选：跳过条件键（如 'single-pair'）；命中时该节点视为 passed，不渲染/不门控 */
  skipWhen?: string
  /** 可选：该节点结论受条件分支控制（复用 ScaffoldBranch 解析）；单键或多键数组 */
  branchKey?: string | string[]
  /** 可选：该节点在跨题型元骨架中的阶段（S1–S6）；元 DAG 节点必带，题型节点按需打标 */
  metaPhase?: MetaPhase
  /** 可选：该步可门控作答的交互形态；缺省 = 只读推导节点 */
  interaction?: AtomicTaskInteraction
  /** 该步的校验检查项 / 判断项要点 */
  checks?: string[]
  /** 常见坑（供答错后的阶梯释放使用） */
  pitfall?: string
}

/** 推导图作用域：题型视角 / 跨题型元骨架 / 脚手架链（模块或反应侧） */
export type ThoughtChainScope =
  | { kind: 'category'; category: ChemCategory }
  | { kind: 'meta' }
  | { kind: 'scaffold'; module: ScaffoldModuleKind | 'reaction' }

/** 一张标准推导 DAG */
export interface ThoughtChainDAG {
  /** 作用域：题型视角 或 跨题型元骨架 */
  scope: ThoughtChainScope
  /** 图名，如「氧还反应推理 DAG」 */
  name: string
  /** 节点集合 */
  nodes: ThoughtChainNode[]
}

/** DAG 不变量校验结果（见 engine/thoughtChainValidation.ts） */
export interface DAGValidationResult {
  ok: boolean
  errors: string[]
}
