// A–F 原子任务的学生作答校验引擎（Task 8，纯函数，确定性，不接 LLM / 后端）
// pass / fail 完全由「学生作答 vs 该步真值」的比较决定，LLM 仅在答错后做反问引导、不影响判定（TR-8.2）。
// 与 engine/scaffoldChains 的门控配合：validateStep.passed 为真 → markPassed 放行下一步，否则 markFailed 锁定当前步。

import type {
  ScaffoldReleaseTier,
  StudentAnswer,
  StepGroundTruth,
  StepOption,
  StepValidation,
} from '../types/scaffold'

/** 把一组 option key 转成展示标签（顿号连接） */
const labelOf = (options: StepOption[], key: string): string =>
  options.find((o) => o.key === key)?.label ?? key

const labelsOf = (options: StepOption[], keys: string[]): string =>
  keys.map((k) => labelOf(options, k)).join('、')

/** 两个字符串集合是否相等（不计顺序） */
const setEq = (a: string[], b: string[]): boolean =>
  a.length === b.length && a.every((x) => b.includes(x))

/**
 * 确定性判定一步作答是否通过。
 * - 作答形态与真值不一致 → 视为不通过（提示按要求作答）。
 * - multi-check：selected 集合与 correct 集合完全相等才通过（漏选 / 多选均不通过）。
 * - single-select：selected === correct 才通过。
 * - order：ordered 与 correct 顺序完全一致才通过。
 */
export const validateStep = (
  answer: StudentAnswer,
  truth: StepGroundTruth,
): StepValidation => {
  if (answer.type !== truth.type) {
    return { passed: false, feedback: '作答形态与该步要求不符，请按题目提示的方式作答。' }
  }

  switch (truth.type) {
    case 'multi-check': {
      const ok = setEq(answer.selected, truth.correct)
      const expected = labelsOf(truth.options, truth.correct)
      return ok
        ? { passed: true, feedback: '目标项已全部正确标出 ✓', expectedLabel: expected }
        : {
            passed: false,
            feedback: `核对一下：正确项应为「${expected}」，检查是否漏选或多选了。`,
            expectedLabel: expected,
          }
    }
    case 'single-select': {
      const ok = answer.selected === truth.correct
      const expected = labelOf(truth.options, truth.correct)
      return ok
        ? { passed: true, feedback: '判断正确 ✓', expectedLabel: expected }
        : {
            passed: false,
            feedback: `该项判断有误，正确应为「${expected}」，再想想它如何影响反应走向。`,
            expectedLabel: expected,
          }
    }
    case 'order': {
      const ok =
        answer.ordered.length === truth.correct.length &&
        answer.ordered.every((k, i) => k === truth.correct[i])
      const expected = labelsOf(truth.options, truth.correct)
      return ok
        ? { passed: true, feedback: '先后顺序正确 ✓', expectedLabel: expected }
        : {
            passed: false,
            feedback: `竞争顺序不对，正确顺序为「${expected}」，回顾还原性 / 溶解度规律。`,
            expectedLabel: expected,
          }
    }
  }
}

/** 取某步真值的正确答案展示标签（用于回顾 / 对照） */
export const correctLabelOf = (truth: StepGroundTruth): string => {
  switch (truth.type) {
    case 'multi-check':
      return labelsOf(truth.options, truth.correct)
    case 'single-select':
      return labelOf(truth.options, truth.correct)
    case 'order':
      return labelsOf(truth.options, truth.correct)
  }
}

/**
 * 阶梯释放触发器（Task 9，纯函数，确定性）：
 * 按学生在某步的「连续错误次数」返回应释放到的档位：
 *   0 → 未释放（还没答错）
 *   1 → 一级：hint + 辨析选择题（反问）
 *   2 → 二级：正反对比卡片
 *   3 → 三级：规则讲解抽屉（再错也不超过此档）
 * 连续正确（通过）后调用方应把计数清零，重新从 0 开始（由组件状态维护）。
 */
export const releaseTier = (consecutiveWrong: number): 0 | ScaffoldReleaseTier => {
  if (consecutiveWrong <= 0) return 0
  if (consecutiveWrong === 1) return 1
  if (consecutiveWrong === 2) return 2
  return 3
}
