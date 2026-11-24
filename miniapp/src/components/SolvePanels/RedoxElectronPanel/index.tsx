import { useState } from 'react'
import { View, Text } from '@tarojs/components'
import { RedoxSolveData } from '../../types/chem'
import ModelIcon, { ModelIconName } from '../ModelIcon'
import styles from './index.module.scss'

interface Props {
  data: RedoxSolveData
  color: string
}

function SectionTitle({ icon, text, color }: { icon: ModelIconName; text: string; color: string }) {
  return (
    <View className={styles.sectionTitle}>
      <ModelIcon name={icon} size={15} color={color} />
      <Text>{text}</Text>
    </View>
  )
}

/**
 * 氧化还原专属面板（两阶段 + 动态 section 路由）：
 * 阶段一「反应可行性 & 产物推理」默认可见；阶段二「电子守恒配平」折叠在按钮后（不强制）；
 * 其余 subtype 专属段（反应信息 / 四剂四产物 / 计算 / 比较 / 先后 / 价态 / 多反应）按字段存在性渲染。
 * 任一字段缺失即跳过该 section，不崩溃。
 */
export default function RedoxElectronPanel({ data, color }: Props) {
  const s1 = data.stage1
  const [openStage2, setOpenStage2] = useState(false)

  const hasStage1 = !!(
    s1 &&
    (s1.particles?.length ||
      s1.medium ||
      s1.mediumRules?.length ||
      s1.candidates ||
      s1.order ||
      s1.products?.length ||
      s1.coexistence)
  )
  const hasStage2 = data.halfReactions.length > 0
  const b = data.balance

  const lost = data.halfReactions.filter((h) => h.role === '氧化').reduce((sum, h) => sum + h.electrons, 0)
  const gained = data.halfReactions.filter((h) => h.role === '还原').reduce((sum, h) => sum + h.electrons, 0)

  return (
    <View className={styles.wrap}>
      {/* ============ 阶段一：反应可行性 & 产物推理（默认可见） ============ */}
      {hasStage1 && s1 && (
        <View className={styles.stageBlock}>
          <View className={styles.stageBanner} style={{ borderColor: `${color}40`, color }}>
            <Text>阶段一 · 反应可行性 &amp; 产物推理</Text>
          </View>

          {s1.particles && s1.particles.length > 0 && (
            <View className={styles.block}>
              <SectionTitle icon="droplet" text="① 体系粒子清单" color={color} />
              <View className={styles.table}>
                <View className={`${styles.row} ${styles.head}`}>
                  <Text className={styles.col}>粒子</Text>
                  <Text className={styles.col}>来源</Text>
                  <Text className={styles.col}>丰度</Text>
                </View>
                {s1.particles.map((p, i) => (
                  <View key={i} className={styles.row}>
                    <Text className={styles.col}>{p.formula}</Text>
                    <Text className={styles.col}>{p.source}</Text>
                    <Text className={styles.col} style={{ color: p.abundance === '大量' ? '#dc2626' : '#6b7280' }}>
                      {p.abundance}
                    </Text>
                  </View>
                ))}
              </View>
            </View>
          )}

          {s1.candidates && (
            <View className={styles.block}>
              <SectionTitle icon="zap" text="② 候选氧化剂 / 还原剂" color={color} />
              <View className={styles.chipGroup}>
                <Text className={styles.chipLabel}>氧化剂</Text>
                <View className={styles.chipRow}>
                  {s1.candidates.oxidants.map((o, i) => (
                    <Text key={i} className={`${styles.chip} ${styles.oxidant}`}>
                      {o}
                    </Text>
                  ))}
                </View>
              </View>
              <View className={styles.chipGroup}>
                <Text className={styles.chipLabel}>还原剂</Text>
                <View className={styles.chipRow}>
                  {s1.candidates.reductants.map((r, i) => (
                    <Text key={i} className={`${styles.chip} ${styles.reductant}`}>
                      {r}
                    </Text>
                  ))}
                </View>
              </View>
            </View>
          )}

          {s1.medium && (
            <View className={styles.block}>
              <SectionTitle icon="test-tube" text="③ 介质判定" color={color} />
              <View className={styles.mediumBadge} style={{ background: `${color}15`, color }}>
                <Text>本题介质：{s1.medium}</Text>
              </View>
              {s1.mediumRules && s1.mediumRules.length > 0 && (
                <>
                  <Text className={styles.ruleHint}>介质直接决定氧化剂的还原产物：</Text>
                  <View className={styles.table}>
                    <View className={`${styles.row} ${styles.head}`}>
                      <Text className={styles.col}>氧化剂</Text>
                      <Text className={styles.col}>酸性</Text>
                      <Text className={styles.col}>中性</Text>
                      <Text className={styles.col}>碱性</Text>
                    </View>
                    {s1.mediumRules.map((r, i) => (
                      <View key={i} className={styles.row}>
                        <Text className={styles.col}>{r.species}</Text>
                        <Text className={`${styles.col} ${s1.medium === '酸性' ? styles.hlCol : ''}`}>{r.acid}</Text>
                        <Text className={`${styles.col} ${s1.medium === '中性' ? styles.hlCol : ''}`}>{r.neutral}</Text>
                        <Text className={`${styles.col} ${s1.medium === '碱性' ? styles.hlCol : ''}`}>{r.base}</Text>
                      </View>
                    ))}
                  </View>
                </>
              )}
            </View>
          )}

          {s1.order && (
            <View className={styles.block}>
              <SectionTitle icon="refresh" text="④ 反应先后顺序" color={color} />
              <View className={styles.noteCard}>
                <Text className={styles.noteText}>
                  {s1.order.property}性由强到弱：{s1.order.strongerFirst.join(' > ')}
                </Text>
                <Text className={styles.noteText}>{s1.order.conclusion}</Text>
              </View>
            </View>
          )}

          {s1.products && s1.products.length > 0 && (
            <View className={styles.block}>
              <SectionTitle icon="check-circle" text="⑤ 推导产物" color={color} />
              <View className={styles.table}>
                <View className={`${styles.row} ${styles.head}`}>
                  <Text className={styles.col}>产物</Text>
                  <Text className={styles.col}>推导依据</Text>
                </View>
                {s1.products.map((p, i) => (
                  <View key={i} className={styles.row}>
                    <Text className={styles.col}>{p.species}</Text>
                    <Text className={styles.col}>{p.basis}</Text>
                  </View>
                ))}
              </View>
            </View>
          )}

          {s1.coexistence && (
            <View className={styles.block}>
              <SectionTitle icon="check-circle" text="共存 / 沉淀判断" color={color} />
              <View className={`${styles.noteCard} ${s1.coexistence.ok ? styles.ok : styles.no}`}>
                <Text className={styles.noteText}>
                  {s1.coexistence.ok ? '产物可大量共存' : '产物不能大量共存'}
                  {s1.coexistence.precipitates && s1.coexistence.precipitates.length > 0
                    ? `；生成沉淀：${s1.coexistence.precipitates.join('、')}`
                    : ''}
                </Text>
                <Text className={styles.noteText}>{s1.coexistence.note}</Text>
              </View>
            </View>
          )}
        </View>
      )}

      {/* ============ 反应信息 & 四剂四产物 ============ */}
      {data.reaction && (data.reaction.equation || data.reaction.ionicEquation) && (
        <View className={styles.block}>
          <SectionTitle icon="link" text="反应信息" color={color} />
          <View className={styles.eqCard}>
            <View className={styles.eqTagRow}>
              {data.reaction.type && <Text className={styles.eqTag}>{data.reaction.type}</Text>}
              {data.reaction.subtype && <Text className={styles.eqTag}>{data.reaction.subtype}</Text>}
            </View>
            {data.reaction.equation && <Text className={styles.eqText}>{data.reaction.equation}</Text>}
            {data.reaction.ionicEquation && (
              <Text className={styles.eqText}>离子方程式：{data.reaction.ionicEquation}</Text>
            )}
          </View>
        </View>
      )}

      {data.agents && (
        <View className={styles.block}>
          <SectionTitle icon="zap" text="四剂 / 四产物" color={color} />
          <View className={styles.agentGrid}>
            <View className={styles.agentCell}>
              <Text className={styles.agentLabel}>氧化剂</Text>
              <Text className={styles.agentVal}>{data.agents.oxidants.join('、')}</Text>
            </View>
            <View className={styles.agentCell}>
              <Text className={styles.agentLabel}>还原剂</Text>
              <Text className={styles.agentVal}>{data.agents.reductants.join('、')}</Text>
            </View>
            <View className={styles.agentCell}>
              <Text className={styles.agentLabel}>氧化产物</Text>
              <Text className={styles.agentVal}>{data.agents.oxidizedProducts.join('、')}</Text>
            </View>
            <View className={styles.agentCell}>
              <Text className={styles.agentLabel}>还原产物</Text>
              <Text className={styles.agentVal}>{data.agents.reducedProducts.join('、')}</Text>
            </View>
          </View>
        </View>
      )}

      {/* ============ 阶段二：电子守恒配平（折叠在按钮后，不强制） ============ */}
      {hasStage2 && (
        <View className={styles.stageBlock}>
          <View
            className={`${styles.expandBtn} ${openStage2 ? styles.expandBtnOpen : ''}`}
            style={{ borderColor: `${color}40` }}
            onClick={() => setOpenStage2((v) => !v)}
          >
            <ModelIcon name="zap" size={16} color={color} />
            <Text style={{ color }}>
              {openStage2 ? '收起电子守恒配平推导' : b ? '展开电子守恒配平推导' : '展开电子转移分析'}
            </Text>
            <Text className={styles.expandArrow} style={{ color }}>
              {openStage2 ? '▲' : '▼'}
            </Text>
          </View>

          {openStage2 && (
            <View className={styles.stage2Body}>
              <View className={styles.sectionTitle}>
                <ModelIcon name="zap" size={15} color={color} />
                <Text>电子转移分析</Text>
              </View>
              <View className={styles.table}>
                <View className={`${styles.row} ${styles.head}`}>
                  <Text className={styles.col}>物质</Text>
                  <Text className={styles.col}>价态变化</Text>
                  <Text className={styles.col}>得失</Text>
                  <Text className={styles.col}>电子数</Text>
                </View>
                {data.halfReactions.map((h, i) => (
                  <View key={i} className={styles.row}>
                    <Text className={styles.col}>{h.species}</Text>
                    <Text className={styles.col}>
                      {h.from} → {h.to}
                    </Text>
                    <Text className={styles.col} style={{ color: h.role === '氧化' ? '#dc2626' : '#2563eb' }}>
                      {h.role}
                    </Text>
                    <Text className={styles.col}>{h.electrons} e⁻</Text>
                  </View>
                ))}
              </View>

              <View className={styles.summary} style={{ borderColor: `${color}40` }}>
                <View className={styles.sumItem}>
                  <Text className={styles.sumLabel}>失电子总数</Text>
                  <Text className={styles.sumVal}>{lost}</Text>
                </View>
                <Text className={styles.sumEq}>=</Text>
                <View className={styles.sumItem}>
                  <Text className={styles.sumLabel}>得电子总数</Text>
                  <Text className={styles.sumVal}>{gained}</Text>
                </View>
              </View>

              {b && (
                <View className={styles.balanceRow}>
                  <View className={styles.balanceItem}>
                    <Text className={styles.balanceLabel}>最小公倍数</Text>
                    <Text className={styles.balanceVal}>{b.lcm}</Text>
                  </View>
                  <View className={styles.balanceItem}>
                    <Text className={styles.balanceLabel}>系数比</Text>
                    <Text className={styles.balanceVal}>{b.ratio}</Text>
                  </View>
                </View>
              )}
            </View>
          )}
        </View>
      )}

      {/* ============ 题型专属段（按字段存在性动态渲染） ============ */}
      {data.calc && (
        <View className={styles.block}>
          <SectionTitle icon="lightbulb" text="计算过程" color={color} />
          <View className={styles.calcBox}>
            <View className={styles.calcRow}>
              <Text className={styles.calcLabel}>已知</Text>
              <Text className={styles.calcVal}>{data.calc.given}</Text>
            </View>
            <View className={styles.calcRow}>
              <Text className={styles.calcLabel}>求解</Text>
              <Text className={styles.calcVal}>{data.calc.target}</Text>
            </View>
            <Text className={styles.calcStepsTitle}>推导步骤</Text>
            {data.calc.steps.map((s, i) => (
              <Text key={i} className={styles.calcStep}>
                {i + 1}. {s}
              </Text>
            ))}
            <View className={styles.calcResult} style={{ borderColor: `${color}40` }}>
              <Text className={styles.calcResultLabel}>结果</Text>
              <Text className={styles.calcResultVal} style={{ color }}>
                {data.calc.result}
              </Text>
            </View>
          </View>
        </View>
      )}

      {data.comparison && (
        <View className={styles.block}>
          <SectionTitle icon="refresh" text={`${data.comparison.property}强弱比较`} color={color} />
          <View className={styles.cmpBox}>
            <Text className={styles.cmpOrder} style={{ color }}>
              {data.comparison.order}
            </Text>
            <Text className={styles.cmpBasis}>依据：{data.comparison.basis}</Text>
          </View>
        </View>
      )}

      {data.order && (
        <View className={styles.block}>
          <SectionTitle icon="refresh" text="反应先后" color={color} />
          <View className={styles.noteCard}>
            <Text className={styles.noteText}>
              {data.order.property}性由强到弱：{data.order.strongerFirst.join(' > ')}
            </Text>
            <Text className={styles.noteText}>{data.order.conclusion}</Text>
          </View>
        </View>
      )}

      {data.valenceRule && data.valenceRule.items.length > 0 && (
        <View className={styles.block}>
          <SectionTitle icon="zap" text="价态规律" color={color} />
          <View className={styles.table}>
            <View className={`${styles.row} ${styles.head}`}>
              <Text className={styles.col}>粒子</Text>
              <Text className={styles.col}>元素</Text>
              <Text className={styles.col}>价态</Text>
              <Text className={styles.col}>位置</Text>
            </View>
            {data.valenceRule.items.map((it, i) => (
              <View key={i} className={styles.row}>
                <Text className={styles.col}>{it.species}</Text>
                <Text className={styles.col}>{it.element}</Text>
                <Text className={styles.col}>{it.valence}</Text>
                <Text className={styles.col}>{it.position}</Text>
              </View>
            ))}
          </View>
          <Text className={styles.ruleHint}>
            规律：最低价只作还原剂，最高价只作氧化剂，中间价态既可作氧化剂又可作还原剂。
          </Text>
        </View>
      )}

      {data.reactions && data.reactions.length > 0 && (
        <View className={styles.block}>
          <SectionTitle icon="link" text="涉及反应" color={color} />
          <View className={styles.reactionList}>
            {data.reactions.map((r, i) => (
              <View key={i} className={styles.reactionItem}>
                <Text className={`${styles.redoxBadge} ${r.isRedox ? styles.redoxYes : styles.redoxNo}`}>
                  {r.isRedox ? '氧化还原' : '非氧化还原'}
                </Text>
                <Text className={styles.reactionEq}>{r.equation}</Text>
                {r.type && <Text className={styles.reactionType}>{r.type}</Text>}
              </View>
            ))}
          </View>
        </View>
      )}
    </View>
  )
}
