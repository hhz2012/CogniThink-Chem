// Task 8 A–F 精熟练习真值（教材可溯源，不凭空编造）
// 每条练习给出 A–F 六步的候选项 + 正确答案 + 阶梯释放三档内容（Task 9）：
//   错 1 次 → hint + 辨析选择题（反问）
//   错 2 次 → 正反对比卡片
//   错 3 次 → 规则讲解抽屉
// 校验由 engine/scaffoldValidators 纯函数完成；条件分支结论由 engine/scaffoldBranches 提供参照。

import type { ScaffoldPractice } from '../types/scaffold'
import { GENERATED_SCAFFOLDS } from './scaffolds/generated'

/**
 * 代表例：Cl₂ 通入 FeI₂ 溶液（氧化还原推理，少量 Cl₂）。
 * 串联 Task 7 数量分支：Cl₂ 少量只氧化 I⁻（还原性 I⁻ > Fe²⁺ > Br⁻）。
 * 六步覆盖三种交互形态：multi-check（A/B/E）、single-select（C/F）、order（D）。
 */
const CL2_FEI2_PRACTICE: ScaffoldPractice = {
  id: 'SP-CL2-FEI2',
  title: 'Cl₂ 通入 FeI₂ 溶液（Cl₂ 少量）',
  module: 'redox-reasoning',
  conditions: { quantity: '少量' },
  branchEntryId: 'BR-CL2-FEI2',
  steps: [
    {
      // A 识别体系组分（勾选粒子）
      kind: 'A',
      truth: {
        type: 'multi-check',
        options: [
          { key: 'i', label: 'I⁻' },
          { key: 'fe2', label: 'Fe²⁺' },
          { key: 'cl2', label: 'Cl₂' },
          { key: 'h2o', label: 'H₂O' },
          { key: 'no3', label: 'NO₃⁻' },
          { key: 'oh', label: 'OH⁻' },
        ],
        correct: ['i', 'fe2', 'cl2', 'h2o'],
      },
      releases: {
        hint: '体系是 FeI₂ 溶液中通入 Cl₂，不含 NO₃⁻；酸性 / 中性水溶液中 OH⁻ 不是主要粒子，别多选。',
        probe: {
          stem: '下列哪种粒子在「FeI₂ 溶液中通入 Cl₂」的体系里不应该被列入主要粒子清单？先自己判断，别急着看答案。',
          options: ['NO₃⁻', 'I⁻', 'Fe²⁺', 'Cl₂'],
        },
        scaffold: {
          correct: {
            title: '✅ 正确：只列实际存在的粒子',
            detail: 'FeI₂ 溶液电离出 Fe²⁺ 和 I⁻，通入的 Cl₂ 与溶剂 H₂O 是实际存在的；清单 = I⁻、Fe²⁺、Cl₂、H₂O。',
          },
          wrong: {
            title: '❌ 典型错误：把未出现的离子加进来',
            detail: '误以为溶液里有 NO₃⁻ 或把 OH⁻ 当主要粒子——题干没有硝酸盐，中性水溶液中 OH⁻ 浓度极低，不应列入。',
          },
        },
        reveal: {
          title: '规则：列体系粒子清单的依据',
          body: '只列题干实际给出的物质及其电离产物：强电解质可溶盐拆成离子（FeI₂ → Fe²⁺ + 2I⁻），通入的气体（Cl₂）和溶剂（H₂O）保留分子形式；题干未出现的离子（如 NO₃⁻）一律不加入，弱电解质 / 微量离子（如中性水中的 OH⁻）不算主要粒子。',
        },
      },
    },
    {
      // B 识别反应角色（多勾选变价物种）
      kind: 'B',
      truth: {
        type: 'multi-check',
        options: [
          { key: 'i_red', label: 'I⁻ 是还原剂' },
          { key: 'fe2_red', label: 'Fe²⁺ 是还原剂' },
          { key: 'cl2_ox', label: 'Cl₂ 是氧化剂' },
          { key: 'h2o_red', label: 'H₂O 是还原剂' },
          { key: 'fe2_ox', label: 'Fe²⁺ 是氧化剂' },
        ],
        correct: ['i_red', 'fe2_red', 'cl2_ox'],
      },
      releases: {
        hint: 'Fe²⁺ 和 I⁻ 都能失电子被氧化，都是还原剂；别把 Fe²⁺ 当成氧化剂，H₂O 在此不参与变价。',
        probe: {
          stem: '关于 Fe²⁺ 在该反应中的角色，下列哪个说法更合理？先判断，不看结论。',
          options: ['Fe²⁺ 是还原剂（可失电子变 Fe³⁺）', 'Fe²⁺ 是氧化剂', 'Fe²⁺ 既不被氧化也不被还原'],
        },
        scaffold: {
          correct: {
            title: '✅ 正确：标全所有变价物种',
            detail: 'I⁻ → I₂ 失电子（还原剂），Fe²⁺ → Fe³⁺ 可失电子（还原剂），Cl₂ → Cl⁻ 得电子（氧化剂）；三者都要标。',
          },
          wrong: {
            title: '❌ 典型错误：漏标 Fe²⁺ / 误标 H₂O',
            detail: '只标了 I⁻ 却漏了 Fe²⁺，或把 H₂O 标成还原剂——H₂O 在此条件下不参与变价。',
          },
        },
        reveal: {
          title: '规则：识别氧化剂 / 还原剂',
          body: '化合价升高 → 失电子 → 被氧化 → 是还原剂；化合价降低 → 得电子 → 被还原 → 是氧化剂。本题 I⁻（-1→0）和 Fe²⁺（+2→+3）都升高，都是还原剂；Cl₂（0→-1）降低，是氧化剂。不变价的物质（如本题 H₂O）不标。',
        },
      },
    },
    {
      // C 识别约束条件（单选：用量）
      kind: 'C',
      truth: {
        type: 'single-select',
        options: [
          { key: 'little', label: 'Cl₂ 少量' },
          { key: 'excess', label: 'Cl₂ 过量' },
          { key: 'equal', label: 'Cl₂ 与 FeI₂ 等物质的量' },
          { key: 'none', label: '用量不影响反应' },
        ],
        correct: 'little',
      },
      releases: {
        hint: 'Cl₂ 的用量决定氧化到哪一步：少量只氧化 I⁻，过量则连 Fe²⁺ 一起氧化——这是本题的关键条件。',
        probe: {
          stem: '若把题干改成「Cl₂ 过量」，产物会怎样变化？先想一想。',
          options: ['过量时 Fe²⁺ 也会被氧化成 Fe³⁺', '过量时仍只生成 I₂', '过量时不发生反应'],
        },
        scaffold: {
          correct: {
            title: '✅ 正确：少量只氧化 I⁻',
            detail: 'Cl₂ 少量时，还原性更强的 I⁻ 优先被氧化为 I₂，Fe²⁺ 不被氧化。',
          },
          wrong: {
            title: '❌ 典型错误：忽略用量条件',
            detail: '没区分少量 / 过量，默认 Cl₂ 把 I⁻ 和 Fe²⁺ 都氧化——少量时 Fe²⁺ 还轮不到。',
          },
        },
        reveal: {
          title: '规则：用量（少量 / 过量）改变反应走向',
          body: '多种还原剂共存时，氧化剂优先满足还原性更强的物种。Cl₂ 少量 → 只氧化 I⁻（还原性最强）；Cl₂ 过量 → I⁻ 氧化完后继续氧化 Fe²⁺。用量是数量分支的判定依据，必须先识别。',
        },
      },
    },
    {
      // D 竞争排序（还原性顺序 I⁻ > Fe²⁺ > Br⁻）
      kind: 'D',
      truth: {
        type: 'order',
        options: [
          { key: 'i', label: 'I⁻' },
          { key: 'fe2', label: 'Fe²⁺' },
          { key: 'br', label: 'Br⁻' },
        ],
        correct: ['i', 'fe2', 'br'],
      },
      releases: {
        hint: '还原性顺序 I⁻ > Fe²⁺ > Br⁻，所以 Cl₂ 先氧化 I⁻，再轮到 Fe²⁺，最后才是 Br⁻。',
        probe: {
          stem: '下列还原性强弱排列，哪个是对的？先判断。',
          options: ['I⁻ > Fe²⁺ > Br⁻', 'Fe²⁺ > I⁻ > Br⁻', 'Br⁻ > Fe²⁺ > I⁻'],
        },
        scaffold: {
          correct: {
            title: '✅ 正确：I⁻ > Fe²⁺ > Br⁻',
            detail: '还原性 I⁻ 最强，优先被氧化；Fe²⁺ 次之；Br⁻ 最弱。Cl₂ 按此顺序依次氧化。',
          },
          wrong: {
            title: '❌ 典型错误：记反还原性顺序',
            detail: '误以为 Fe²⁺ 还原性比 I⁻ 强，导致排序错误，进而产物判断全错。',
          },
        },
        reveal: {
          title: '规则：常见还原性顺序（竞争排序依据）',
          body: '水溶液中常见离子的还原性顺序：S²⁻ > SO₃²⁻ > I⁻ > Fe²⁺ > Br⁻ > Cl⁻ > OH⁻。多种还原剂共存时，氧化剂先与还原性最强者反应，这是 D 步排序与 E 步产物的共同依据。',
        },
      },
    },
    {
      // E 推导产物（勾选产物）
      kind: 'E',
      truth: {
        type: 'multi-check',
        options: [
          { key: 'i2', label: '生成 I₂' },
          { key: 'cl', label: '生成 Cl⁻' },
          { key: 'fe3', label: '生成 Fe³⁺' },
          { key: 'br2', label: '生成 Br₂' },
        ],
        correct: ['i2', 'cl'],
      },
      releases: {
        hint: 'Cl₂ 少量时只把 I⁻ 氧化成 I₂，自身被还原为 Cl⁻；Fe²⁺ 还没轮到，不会生成 Fe³⁺。',
        probe: {
          stem: 'Cl₂ 少量时，下列哪种产物不会生成？先判断。',
          options: ['Fe³⁺', 'I₂', 'Cl⁻'],
        },
        scaffold: {
          correct: {
            title: '✅ 正确：产物 I₂ + Cl⁻',
            detail: '少量 Cl₂ 只氧化 I⁻：Cl₂ + 2I⁻ → 2Cl⁻ + I₂；产物为 I₂ 和 Cl⁻，无 Fe³⁺。',
          },
          wrong: {
            title: '❌ 典型错误：提前生成 Fe³⁺',
            detail: '没等到 Cl₂ 过量就写出 Fe³⁺，忽略了「少量」条件下 Fe²⁺ 不被氧化。',
          },
        },
        reveal: {
          title: '规则：产物必须与 A–D 全部条件一致',
          body: '产物由「粒子（A）+ 角色（B）+ 条件（C）+ 排序（D）」共同决定：少量 Cl₂ + 还原性 I⁻ 最强 → 只生成 I₂ 和 Cl⁻。若条件改成过量，才会出现 Fe³⁺。产物与条件矛盾是最常见的错误。',
        },
      },
    },
    {
      // F 匹配选项 / 作答（单选）
      kind: 'F',
      truth: {
        type: 'single-select',
        options: [
          { key: 'a', label: '少量 Cl₂ 只氧化 I⁻，Fe²⁺ 不被氧化' },
          { key: 'b', label: '少量 Cl₂ 同时氧化 I⁻ 和 Fe²⁺' },
          { key: 'c', label: '还原性 Fe²⁺ > I⁻' },
          { key: 'd', label: '产物中有 Fe³⁺' },
        ],
        correct: 'a',
      },
      releases: {
        hint: '抓住还原性 I⁻ > Fe²⁺：少量氧化剂先满足还原性更强的 I⁻，Fe²⁺ 不被氧化。',
        probe: {
          stem: '少量 Cl₂ 通入 FeI₂ 溶液，最终结论是？先判断。',
          options: ['只氧化 I⁻，Fe²⁺ 不被氧化', '同时氧化 I⁻ 和 Fe²⁺', '不发生反应'],
        },
        scaffold: {
          correct: {
            title: '✅ 正确：少量 Cl₂ 只氧化 I⁻',
            detail: '还原性 I⁻ > Fe²⁺，少量氧化剂先满足 I⁻，Fe²⁺ 不被氧化，无 Fe³⁺ 生成。',
          },
          wrong: {
            title: '❌ 典型错误：推理对但选项错',
            detail: '前面步骤都对，却误选了「同时氧化」——结论须与 A–E 推理一致。',
          },
        },
        reveal: {
          title: '规则：结论必须与 A–E 推理链一致',
          body: 'F 步是把 A–E 的全部推理映射回原题选项。检查：粒子清单对吗？角色标全了吗？条件（少量）识别了吗？排序（I⁻ > Fe²⁺）对吗？产物（只有 I₂、Cl⁻）对吗？五者一致才能锁定选项。',
        },
      },
    },
  ],
}

/**
 * 全部 A–F 精熟练习：手工精修题在前（默认选中、不被覆盖），
 * 后面拼接由后端 ScaffoldGenerator 批量生成的题（data/scaffolds/generated/index.ts）。
 */
export const SCAFFOLD_PRACTICES: ScaffoldPractice[] = [CL2_FEI2_PRACTICE, ...GENERATED_SCAFFOLDS]

/** 按 id 取练习 */
export const getScaffoldPractice = (id: string): ScaffoldPractice | undefined =>
  SCAFFOLD_PRACTICES.find((p) => p.id === id)
