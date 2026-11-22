// P3-3 导学与求解融合：复用的「标准推理链」DAG 渲染组件。
// 后端已按 conclusion_state 裁剪（Insufficient 不下发），此处只负责展示；nodes 为空时不渲染。
import { View, Text } from '@tarojs/components'
import { ReasoningNodeDto } from '../../services/tutorApi'
import styles from './index.module.scss'

/** 证据强度中文标签（镜像后端 EvidenceLevel） */
const EVIDENCE_TEXT: Record<string, string> = {
  KnowledgeFact: '知识事实',
  EngineDerived: '引擎推导',
  Candidate: '候选',
  Trend: '趋势'
}

export default function ReasoningDag({
  nodes,
  title = '标准推理链（引擎推导，可溯源）'
}: {
  nodes?: ReasoningNodeDto[] | null
  title?: string
}) {
  if (!nodes || nodes.length === 0) return null

  return (
    <View className={styles.chainBlock}>
      {title ? <Text className={styles.chainTitle}>{title}</Text> : null}
      {nodes.map((n) => (
        <View key={n.step} className={styles.chainNode}>
          <Text className={styles.chainStep}>步骤 {n.step}</Text>
          <Text className={styles.chainDesc}>{n.description}</Text>
          {n.premises?.length > 0 && (
            <Text className={styles.chainPremise}>依据：{n.premises.join('，')}</Text>
          )}
          {(n.rule_id || n.knowledge_id) && (
            <Text className={styles.chainRef}>
              规则/知识点：{n.rule_id || n.knowledge_id}
            </Text>
          )}
          {n.conclusion && (
            <Text className={styles.chainConclusion}>
              结论：{n.conclusion}（{EVIDENCE_TEXT[n.evidence_level] ?? n.evidence_level}）
            </Text>
          )}
        </View>
      ))}
    </View>
  )
}
