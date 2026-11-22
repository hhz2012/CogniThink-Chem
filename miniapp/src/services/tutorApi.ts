import Taro from '@tarojs/taro'

/**
 * 推理导学后端地址
 * - 微信开发者工具 / 浏览器预览：http://localhost:5080
 * - 真机调试：改为电脑局域网 IP，例如 http://192.168.1.10:5080
 * - 微信小程序需在开发者工具中勾选「不校验合法域名」，或在后台配置 request 域名
 */
export const TUTOR_API_BASE = 'http://localhost:5080'

// ==================== DTO ====================

export interface ProblemStepDto {
  stepIndex: number
  prompt: string
  isKeyStep: boolean
  knowledgeId: string
}

export interface ProblemDto {
  id: string
  subject: string
  category: string
  title: string
  stem: string
  difficulty: number
  totalSteps: number
  knowledgeIds: string[]
  steps: ProblemStepDto[]
  variantOfId?: string | null
}

export interface WeakKnowledgeDto {
  knowledgeId: string
  name: string
  subject: string
  mastery: number
  isPrerequisite: boolean
}

export interface SubmitStepRequest {
  studentId: string
  problemId: string
  stepIndex: number
  stepContent: string
}

export interface ChemicalConflictDto {
  ions: string[]
  type: string
  result: string
  knowledgeIds: string[]
}

export interface ChemicalReactionDto {
  id: string
  equation: string
  conditions: string[]
  phenomena: string[]
  knowledgeIds: string[]
}

export interface ChemicalEvidenceDto {
  queryType: string
  conclusion: string
  keyFacts: string[]
  conflicts: ChemicalConflictDto[]
  relatedReactions: ChemicalReactionDto[]
}

export interface SubmitStepResponse {
  status: string
  verdict: string
  errorTag: string
  hintLevel: number
  recommendedAction: string
  message: string
  relatedKnowledgeIds: string[]
  unlockedNextStep: boolean
  completed: boolean
  nextStepIndex: number
  hintText?: string | null
  mastery?: number | null
  currentStep: number
  totalSteps: number
  weakPrerequisites: WeakKnowledgeDto[]
  chemicalEvidence?: ChemicalEvidenceDto | null
  variant?: ProblemDto | null
  /** P3-3：网关标准推理链（仅 Confirmed 且有可溯源节点时下发；空/缺省表示该题无引擎 DAG） */
  standardReasoningChain?: ReasoningNodeDto[] | null
}

// ==================== 网关求解（P2-4 学生面视图）====================

/** 推理节点（镜像后端 ReasoningNode） */
export interface ReasoningNodeDto {
  step: number
  description: string
  premises: string[]
  rule_id?: string | null
  knowledge_id?: string | null
  conclusion?: string | null
  evidence_level: 'KnowledgeFact' | 'EngineDerived' | 'Candidate' | 'Trend'
}

/**
 * 学生面求解视图（镜像后端 StudentSolveView）。
 * 服务端已按 conclusion_state 裁剪：Insufficient 不下发 final_result / reasoning_nodes / explanation_nodes，
 * 因此前端无需再做防泄露过滤；但展示文案仍需诚实标注。
 */
export interface StudentSolveView {
  conclusion_state: 'Confirmed' | 'CandidateSet' | 'Insufficient'
  task_type: string
  final_result?: string | null
  reasoning_nodes: ReasoningNodeDto[]
  candidates: string[]
  explanation_nodes: ReasoningNodeDto[]
  reply: string
  constraint_note?: string | null
  result_provenance?: string | null
}

// ==================== 请求封装 ====================

async function request<T>(
  path: string,
  method: 'GET' | 'POST' = 'GET',
  data?: unknown
): Promise<T> {
  try {
    const res = await Taro.request({
      url: `${TUTOR_API_BASE}${path}`,
      method,
      data: data as Record<string, unknown> | undefined,
      header: { 'Content-Type': 'application/json' },
      timeout: 10000
    })

    if (res.statusCode >= 200 && res.statusCode < 300) {
      return res.data as T
    }

    const message =
      (res.data as { message?: string } | undefined)?.message ??
      `请求失败（${res.statusCode}）`
    throw new Error(message)
  } catch (err) {
    const message = err instanceof Error ? err.message : '网络异常'
    if (message.includes('request:fail')) {
      throw new Error('无法连接导学服务，请确认后端已启动（localhost:5080）')
    }
    throw new Error(message)
  }
}

// ==================== 对外 API ====================

export const tutorApi = {
  /** 题目列表（可按学科过滤，本项目仅化学） */
  listProblems(subject?: 'Chemistry'): Promise<ProblemDto[]> {
    const query = subject ? `?subject=${subject}` : ''
    return request<ProblemDto[]>(`/api/problem${query}`)
  },

  /** 加载题目（不含标准答案） */
  getProblem(id: string): Promise<ProblemDto> {
    return request<ProblemDto>(`/api/problem/${id}`)
  },

  /** 提交一步思考 */
  submitStep(payload: SubmitStepRequest): Promise<SubmitStepResponse> {
    return request<SubmitStepResponse>('/api/tutor/submit-step', 'POST', payload)
  },

  /** 学生学情 */
  getStudent(id: string): Promise<unknown> {
    return request<unknown>(`/api/student/${id}`)
  },

  /** 题目求解（学生面：按结论状态裁剪，不泄露未采纳结论） */
  solve(text: string): Promise<StudentSolveView> {
    return request<StudentSolveView>('/api/gateway/solve', 'POST', { text })
  }
}

/** 本地持久化的学生 ID（用于学情连续） */
export function getOrCreateStudentId(): string {
  const key = 'tutor_student_id'
  const existing = Taro.getStorageSync(key) as string | undefined
  if (existing) return existing
  const id = `stu-${Date.now().toString(36)}`
  Taro.setStorageSync(key, id)
  return id
}
