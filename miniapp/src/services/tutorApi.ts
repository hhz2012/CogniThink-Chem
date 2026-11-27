import Taro from '@tarojs/taro'

/**
 * 推理导学后端地址
 * - H5 浏览器预览：用空串走同源，由 dev server（config/dev.ts）把 /api 代理到 5080，
 *   避免浏览器跨域 / 沙箱端口不可达
 * - 微信开发者工具：http://localhost:5080（需在开发者工具勾选「不校验合法域名」）
 * - 真机调试：改为电脑局域网 IP，例如 http://192.168.1.10:5080
 */
export const TUTOR_API_BASE = Taro.getEnv() === Taro.ENV_TYPE.WEB ? '' : 'http://localhost:5080'

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

/* ===== 结构化辅助数据（镜像后端 StudentSolveStructured，加性，不参与判分）===== */
export interface NumericSnapshotDto {
  value?: number | null
  unit?: string | null
  species?: string | null
  formula?: string | null
}
export interface AqueousSnapshotDto {
  ordering: string[]
  conservation_equations: string[]
  ph?: number | null
  acidity_verdict?: string | null
  ion_concentrations?: Record<string, number>
}
export interface EquilibriumSnapshotDto {
  ksp_residual_concentration?: number | null
  ksp_residual_ion?: string | null
  conversion_constant_k?: string | null
  conversion_proceeds?: boolean | null
}
export interface TraceEventDto {
  equation?: string | null
  stage_index: number
  extent: number
  basis_knowledge_ids: string[]
  priority_rank?: number | null
}
export interface SpeciesAmountDto {
  substance?: string | null
  moles?: number | null
}
export interface ReactionTraceSnapshotDto {
  events: TraceEventDto[]
  final_species: SpeciesAmountDto[]
  converged: boolean
}
export interface StudentSolveStructuredDto {
  numeric?: NumericSnapshotDto | null
  aqueous?: AqueousSnapshotDto | null
  equilibrium?: EquilibriumSnapshotDto | null
  reaction_trace?: ReactionTraceSnapshotDto | null
  warnings?: string[]
}

/**
 * 学生面求解视图（镜像后端 StudentSolveView）。
 * 服务端已按 conclusion_state 裁剪：Insufficient 不下发 final_result / reasoning_nodes / explanation_nodes，
 * 因此前端无需再做防泄露过滤；但展示文案仍需诚实标注。
 * structured 为加性辅助数据（数值/守恒/pH/反应轨迹），与判分无关；非 Confirmed 时须标注“不作最终判定结论”。
 */
export interface StudentSolveView {
  conclusion_state: 'Confirmed' | 'CandidateSet' | 'Insufficient'
  task_type: string
  final_result?: string | null
  /** 引擎逐步推导过程（文字步骤），仅 Confirmed 时有值；空/缺省表示该题无逐步推导 */
  derivation_steps?: string[]
  reasoning_nodes: ReasoningNodeDto[]
  candidates: string[]
  explanation_nodes: ReasoningNodeDto[]
  reply: string
  constraint_note?: string | null
  result_provenance?: string | null
  structured?: StudentSolveStructuredDto | null
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
  },

  /** 拍照 OCR：上传题目图片，返回视觉模型抽取的题干文本（供前端编辑后再求解） */
  async ocrPhoto(tempFilePath: string): Promise<{ text: string; available: boolean }> {
    const res = await Taro.uploadFile({
      url: `${TUTOR_API_BASE}/api/gateway/ocr-photo`,
      filePath: tempFilePath,
      name: 'file',
      timeout: 60000
    })
    if (res.statusCode < 200 || res.statusCode >= 300) {
      let msg = `识别失败（${res.statusCode}）`
      try { const d = JSON.parse(res.data); if (d?.message) msg = d.message } catch { /* ignore */ }
      throw new Error(msg)
    }
    const data = JSON.parse(res.data) as { text?: string; available?: boolean }
    return { text: data.text ?? '', available: !!data.available }
  },

  /** 拍照一键解题：OCR 抽题干 → 网关求解 → 返回学生面视图 */
  async solvePhoto(
    tempFilePath: string
  ): Promise<{ ocrText: string; view: StudentSolveView; available: boolean; note?: string }> {
    const res = await Taro.uploadFile({
      url: `${TUTOR_API_BASE}/api/gateway/solve-photo`,
      filePath: tempFilePath,
      name: 'file',
      timeout: 120000
    })
    if (res.statusCode < 200 || res.statusCode >= 300) {
      let msg = `求解失败（${res.statusCode}）`
      try { const d = JSON.parse(res.data); if (d?.message) msg = d.message } catch { /* ignore */ }
      throw new Error(msg)
    }
    const data = JSON.parse(res.data) as {
      ocrText?: string; view?: StudentSolveView; available?: boolean; note?: string
    }
    if (!data.view) throw new Error(data.note || '未能从图片中识别出可解的题目，请改用拍照识别后手动核对')
    return { ocrText: data.ocrText ?? '', view: data.view, available: !!data.available, note: data.note }
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
