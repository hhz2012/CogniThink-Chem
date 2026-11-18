import { KnowledgePoint } from './redox'

// ============================================================
// 化学平衡 · 知识体系（EQ1-EQ8）
// ============================================================
export const equilibriumKnowledge: KnowledgePoint[] = [
  {
    id: 'EQ1',
    name: '化学反应速率',
    layer: 'Knowledge',
    summary: 'v=Δc/Δt，速率比等于化学计量数比。',
    keyPoints: ['v=Δc/Δt', 'v(A):v(B):v(C)=计量数之比', '速率只取正值'],
    pitfalls: ['忘记速率之比等于计量数之比而直接比较浓度变化'],
    relatedChallengeIds: ['E9'],
    check: {
      question: '2 s 内 A 的浓度由 2 mol/L 降至 0.8 mol/L，v(A) 为？',
      options: ['0.4 mol/(L·s)', '0.6 mol/(L·s)', '1.2 mol/(L·s)', '2.4 mol/(L·s)'],
      answerIndex: 1,
      explain: 'Δc=1.2 mol/L，v=1.2/2=0.6 mol/(L·s)。',
    },
    teach: {
      steps: ['第一步：求浓度变化量 Δc。', '第二步：除以时间 Δt。', '第三步：若要换物质，用计量数之比换算。'],
      example: '2A+B→3C，v(A)=0.6 时 v(B)=0.3，v(C)=0.9。',
    },
  },
  {
    id: 'EQ2',
    name: '平衡移动与勒夏特列原理',
    layer: 'Application',
    summary: '改变条件，平衡向减弱这种改变的方向移动。',
    keyPoints: ['升温向吸热方向移动', '加压向气体分子数减少方向移动', '催化剂不移动平衡'],
    pitfalls: ['把"催化剂加快反应"误当成"使平衡正向移动"'],
    relatedChallengeIds: ['E1', 'E10', 'E14'],
    check: {
      question: 'N₂+3H₂⇌2NH₃（ΔH<0）已达平衡，能使平衡正向移动的是？',
      options: ['升高温度', '减小压强', '移走 NH₃', '加入催化剂'],
      answerIndex: 2,
      explain: '移走生成物使生成物浓度减小，平衡正向移动。',
    },
    teach: {
      steps: ['第一步：明确正反应的方向特征（吸放热、气体分子数变化）。', '第二步：判断条件改变使哪一侧"变弱"。', '第三步：按减弱方向确定移动方向。'],
      example: '2SO₂+O₂⇌2SO₃ 加压，向气体分子数减少的正反应方向移动。',
    },
  },
  {
    id: 'EQ3',
    name: '平衡常数 K',
    layer: 'Application',
    summary: 'K=生成物浓度幂之积/反应物浓度幂之积，只与温度有关。',
    keyPoints: ['计量数作为指数', 'K 只受温度影响', '正逆反应的 K 互为倒数'],
    pitfalls: ['把纯固体/纯液体写进 K 表达式'],
    relatedChallengeIds: ['E2'],
    check: {
      question: 'H₂+I₂⇌2HI 的 K=49，起始 c(H₂)=c(I₂)=1 mol/L，平衡 c(HI) 约为？',
      options: ['0.78 mol/L', '1.56 mol/L', '1.43 mol/L', '2.00 mol/L'],
      answerIndex: 1,
      explain: '设 HI=2x，K=4x²/(1-x)²=49，得 x=7/9，c(HI)=2x≈1.56 mol/L。',
    },
    teach: {
      steps: ['第一步：列三段式（起始、变化、平衡）。', '第二步：把平衡浓度代入 K 表达式。', '第三步：开方或解方程，取合理解。'],
      example: 'K=49 时 4x²/(1-x)²=49 → 2x/(1-x)=7 → x=7/9。',
    },
  },
  {
    id: 'EQ4',
    name: '转化率与三段式',
    layer: 'Application',
    summary: 'α=转化量/起始量×100%。',
    keyPoints: ['转化量之比等于化学计量数之比', '平衡浓度=平衡物质的量/体积', '两种反应物起始比等于计量比时转化率相等'],
    pitfalls: ['把"生成量"直接当作"转化量"（需按计量数换算）'],
    relatedChallengeIds: ['E6', 'E11'],
    check: {
      question: '充入 2 mol SO₂ 和 1 mol O₂，平衡时生成 1 mol SO₃，SO₂ 的转化率为？',
      options: ['25%', '50%', '75%', '100%'],
      answerIndex: 1,
      explain: '生成 1 mol SO₃ 消耗 1 mol SO₂（2:2），转化率=1/2=50%。',
    },
    teach: {
      steps: ['第一步：写出计量数关系。', '第二步：由生成量反推各物质的变化量。', '第三步：用变化量/起始量求转化率。'],
      example: '2SO₂+O₂⇌2SO₃ 生成 1 mol SO₃ → SO₂ 变化 1 mol、O₂ 变化 0.5 mol。',
    },
  },
  {
    id: 'EQ5',
    name: '浓度商 Q 与反应方向',
    layer: 'Application',
    summary: 'Q<K 正向，Q>K 逆向，Q=K 平衡。',
    keyPoints: ['Q 用任意时刻浓度计算', 'Q 与 K 形式相同', 'Q<K 说明产物不足'],
    pitfalls: ['用平衡浓度计算 Q 导致误判'],
    relatedChallengeIds: ['E7'],
    check: {
      question: '某时刻 Q=0.25，该温度下 K=1，反应将？',
      options: ['向正反应方向进行', '向逆反应方向进行', '已达平衡', '无法判断'],
      answerIndex: 0,
      explain: 'Q<K，正向进行使 Q 增大至 K。',
    },
    teach: {
      steps: ['第一步：按 K 的形式写出 Q 并代入当前浓度。', '第二步：比较 Q 与 K。', '第三步：Q<K 正向、Q>K 逆向。'],
      example: 'Q=0.25、K=1 → 正向；若 Q=4 → 逆向。',
    },
  },
  {
    id: 'EQ6',
    name: '等效平衡',
    layer: 'Ability',
    summary: '一边倒后相同（恒容）或成比例（恒压）即等效。',
    keyPoints: ['恒温恒容：一边倒完全相同则等效', '恒温恒压：一边倒成比例则等效', '等效平衡时各物质浓度（或百分含量）相同'],
    pitfalls: ['恒容与恒压的判定条件混淆'],
    relatedChallengeIds: ['E4', 'E12'],
    check: {
      question: '恒温恒容，2SO₂+O₂⇌2SO₃。甲投 2 mol SO₂+1 mol O₂，乙投 2 mol SO₃，达平衡后？',
      options: ['两容器浓度不等', '两容器各物质浓度相同', '甲中转化率更大', '两容器 K 不同'],
      answerIndex: 1,
      explain: '乙一边倒后为 2 mol SO₂+1 mol O₂，与甲相同，属等效平衡。',
    },
    teach: {
      steps: ['第一步：把各投料"一边倒"折算到同一侧。', '第二步：判断是否完全相同（恒容）或成比例（恒压）。', '第三步：等效则浓度/百分含量相同，转化率按等效关系比较。'],
      example: '恒容下 2 mol SO₃ 等价于 2 mol SO₂+1 mol O₂。',
    },
  },
  {
    id: 'EQ7',
    name: '化学平衡图像',
    layer: 'Ability',
    summary: '先拐先平数值大，定一议二。',
    keyPoints: ['先达到平衡的曲线对应温度更高或压强更大', '固定一个变量讨论另一个', '注意起点、拐点、平台'],
    pitfalls: ['把"先拐"误读为先达到平衡的反而是低温'],
    relatedChallengeIds: ['E3', 'E14'],
    check: {
      question: '放热反应升高温度，产物 C 的百分含量如何变化？',
      options: ['增大', '减小', '不变', '先增后减'],
      answerIndex: 1,
      explain: '放热反应升温平衡逆向移动，C% 减小。',
    },
    teach: {
      steps: ['第一步：看曲线谁先拐平，判断温度或压强高低。', '第二步：固定另一变量，看平衡位置变化。', '第三步：结合勒夏特列原理判断吸放热与分子数变化。'],
      example: '两条曲线 T₁ 先拐 → T₁>T₂；若 C% 随 T 降低，则正反应放热。',
    },
  },
  {
    id: 'EQ8',
    name: '溶度积与分步沉淀',
    layer: 'Ability',
    summary: '开始沉淀所需沉淀剂浓度小的先沉淀。',
    keyPoints: ['Ksp=c(M)·c(A) 形式按化学式书写', '同类型难溶物 Ksp 小的先沉淀', '不同类型须计算所需离子浓度再比较'],
    pitfalls: ['不同类型（AB 与 A₂B）直接比较 Ksp 大小'],
    relatedChallengeIds: ['E5', 'E8', 'E13'],
    check: {
      question: 'Ksp(AgCl)=1.8×10⁻¹⁰，Ksp(AgI)=8.5×10⁻¹⁷，向等浓度 Cl⁻、I⁻ 混合液滴加 AgNO₃，先沉淀的是？',
      options: ['AgCl', 'AgI', '同时沉淀', '无法判断'],
      answerIndex: 1,
      explain: '同类型难溶物，Ksp 越小所需 Ag⁺ 越低，AgI 先沉淀。',
    },
    teach: {
      steps: ['第一步：写出各自的 Ksp 表达式。', '第二步：计算开始沉淀所需的沉淀剂离子浓度。', '第三步：所需浓度小的先沉淀，据此判断顺序。'],
      example: 'AgCl 需 c(Ag⁺)=1.8×10⁻⁸；Ag₂CrO₄ 需 c(Ag⁺)=√(Ksp/0.01)≈1.05×10⁻⁵，故 AgCl 先沉淀。',
    },
  },
]
