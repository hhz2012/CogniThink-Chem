import { View } from '@tarojs/components'
import type { ArtKind } from '../../data/homeExamples'

/**
 * 示例卡缩略图：纯 SVG 绘制，无外部图片资源。
 * 统一 218×180 画布、同一套烧杯/试管基线，保证 6 张卡视觉整齐。
 * 玻璃描边用浅灰（在浅灰底上比纯白更易辨认），液体色由数据驱动。
 */

const GLASS = '#CBD5E1'

interface Props {
  kind: ArtKind
  liquid: string
}

export default function ExampleArt({ kind, liquid }: Props) {
  return (
    <View style={{ width: '100%', height: '100%' }}>
      <svg
        viewBox="0 0 218 180"
        width="100%"
        height="100%"
        preserveAspectRatio="xMidYMid meet"
        style={{ display: 'block' }}
      >
        {kind === 'nail-plate' && <NailPlate liquid={liquid} />}
        {kind === 'dropper' && <Dropper liquid={liquid} />}
        {kind === 'two-tubes' && <TwoTubes liquid={liquid} />}
        {kind === 'cell' && <Cell liquid={liquid} />}
        {kind === 'electrode' && <Electrode liquid={liquid} />}
        {kind === 'tube-precip' && <TubePrecip liquid={liquid} />}
      </svg>
    </View>
  )
}

/** 烧杯轮廓（共用）：开口朝上、底部圆角 */
function Beaker({ x = 62, top = 44, bottom = 150, w = 94 }: { x?: number; top?: number; bottom?: number; w?: number }) {
  const r = 18
  const right = x + w
  const d = `M${x} ${top} H${right} V${bottom - r} Q${right} ${bottom} ${right - r} ${bottom} H${x + r} Q${x} ${bottom} ${x} ${
    bottom - r
  } Z`
  return <path d={d} fill="none" stroke={GLASS} strokeWidth={2} strokeLinejoin="round" />
}

/** 铁与铜片：烧杯内两条竖矩形，铜片下半段红棕 */
function NailPlate({ liquid }: { liquid: string }) {
  return (
    <>
      {/* 液体（下半部分） */}
      <path d="M66 82 H152 V132 Q152 150 134 150 H84 Q66 150 66 132 Z" fill={liquid} />
      {/* 铁片（灰） */}
      <rect x={90} y={62} width={10} height={74} rx={2} fill="#94A3B8" />
      {/* 铜片：上半铜色、下半红棕 */}
      <rect x={118} y={62} width={10} height={36} rx={2} fill="#D97706" />
      <rect x={118} y={98} width={10} height={38} rx={2} fill="#B91C1C" />
      <Beaker />
    </>
  )
}

/** 滴管 + 杯底白色沉淀，液体接近无色 */
function Dropper({ liquid }: { liquid: string }) {
  return (
    <>
      <path d="M66 70 H152 V132 Q152 150 134 150 H84 Q66 150 66 132 Z" fill={liquid} />
      {/* 杯底白色沉淀 */}
      <ellipse cx={109} cy={142} rx={30} ry={7} fill="#FFFFFF" />
      <circle cx={96} cy={138} r={2.5} fill="#FFFFFF" />
      <circle cx={122} cy={139} r={2} fill="#FFFFFF" />
      <Beaker />
      {/* 滴管 */}
      <line x1={109} y1={24} x2={109} y2={64} stroke="#94A3B8" strokeWidth={3} strokeLinecap="round" />
      <circle cx={109} cy={22} r={5} fill="#CBD5E1" />
    </>
  )
}

/** 两支细试管并列，左澄清右浑浊，中间浅灰分隔线 */
function TwoTubes({ liquid }: { liquid: string }) {
  const tube = (cx: number, fill: string, cloudy: boolean) => {
    const x = cx - 13
    const right = cx + 13
    const d = `M${x} 44 H${right} V128 Q${right} 146 ${cx} 146 Q${x} 146 ${x} 128 Z`
    return (
      <g key={cx}>
        <path d={d} fill={fill} stroke={GLASS} strokeWidth={2} strokeLinejoin="round" />
        {cloudy && (
          <>
            <circle cx={cx - 5} cy={120} r={2.5} fill="#FFFFFF" />
            <circle cx={cx + 4} cy={128} r={2} fill="#FFFFFF" />
            <circle cx={cx - 2} cy={134} r={2.2} fill="#FFFFFF" />
            <circle cx={cx + 6} cy={116} r={1.8} fill="#FFFFFF" />
          </>
        )}
      </g>
    )
  }
  return (
    <>
      {tube(84, liquid, false)}
      {tube(134, '#F1F5F9', true)}
      {/* 中间分隔线 */}
      <line x1={109} y1={44} x2={109} y2={146} stroke="#E2E8F0" strokeWidth={2} />
    </>
  )
}

/** 原电池：两电极 + 导线 + 顶部暖黄小灯泡 */
function Cell({ liquid }: { liquid: string }) {
  return (
    <>
      <path d="M66 82 H152 V132 Q152 150 134 150 H84 Q66 150 66 132 Z" fill={liquid} />
      {/* 左锌电极（深灰） */}
      <rect x={82} y={54} width={8} height={74} rx={1.5} fill="#64748B" />
      {/* 右铜电极 */}
      <rect x={128} y={54} width={8} height={74} rx={1.5} fill="#B45309" />
      {/* 导线 */}
      <path
        d="M86 54 V28 H104 M114 28 H130 V54"
        fill="none"
        stroke="#475569"
        strokeWidth={1.6}
        strokeLinecap="round"
      />
      {/* 灯泡光晕 + 灯泡 */}
      <circle cx={109} cy={28} r={11} fill="#FDE68A" opacity={0.45} />
      <circle cx={109} cy={28} r={7} fill="#FBBF24" />
      <Beaker />
    </>
  )
}

/** 电解池：两电极 + 上方电池符号 + 两极表面白色小气泡 */
function Electrode({ liquid }: { liquid: string }) {
  const bubbles = (cx: number) => (
    <>
      <circle cx={cx - 4} cy={62} r={2.6} fill="#FFFFFF" opacity={0.9} />
      <circle cx={cx + 4} cy={70} r={2.2} fill="#FFFFFF" opacity={0.9} />
      <circle cx={cx - 2} cy={78} r={2.4} fill="#FFFFFF" opacity={0.9} />
    </>
  )
  return (
    <>
      <path d="M66 82 H152 V132 Q152 150 134 150 H84 Q66 150 66 132 Z" fill={liquid} />
      {/* 两电极 */}
      <rect x={82} y={54} width={8} height={74} rx={1.5} fill="#64748B" />
      <rect x={128} y={54} width={8} height={74} rx={1.5} fill="#64748B" />
      {bubbles(86)}
      {bubbles(132)}
      {/* 电池符号：短粗竖线 + 长细竖线 */}
      <line x1={104} y1={16} x2={104} y2={32} stroke="#1F2937" strokeWidth={4} strokeLinecap="round" />
      <line x1={114} y1={18} x2={114} y2={30} stroke="#1F2937" strokeWidth={1.8} strokeLinecap="round" />
      {/* 引线 */}
      <path d="M104 32 V46 H86 V54 M114 32 V46 H132 V54" fill="none" stroke="#475569" strokeWidth={1.6} />
      <Beaker />
    </>
  )
}

/** 细长试管：底部白色沉淀 + 中段黄色沉淀，液面在上部 */
function TubePrecip({ liquid }: { liquid: string }) {
  const x = 91
  const right = 127
  const cx = 109
  const d = `M${x} 34 H${right} V132 Q${right} 150 ${cx} 150 Q${x} 150 ${x} 132 Z`
  return (
    <>
      {/* 上层清液 */}
      <path d={d} fill={liquid} />
      {/* 中段黄色沉淀层 */}
      <path d={`M${x} 98 H${right} V120 H${x} Z`} fill="#FDE68A" opacity={0.9} />
      {/* 底部白色沉淀层（贴合试管底圆角） */}
      <path d={`M${x} 120 H${right} V132 Q${right} 150 ${cx} 150 Q${x} 150 ${x} 132 Z`} fill="#FFFFFF" />
      {/* 试管轮廓 */}
      <path d={d} fill="none" stroke={GLASS} strokeWidth={2} strokeLinejoin="round" />
      {/* 管口 */}
      <line x1={x - 3} y1={34} x2={right + 3} y2={34} stroke={GLASS} strokeWidth={2} strokeLinecap="round" />
    </>
  )
}
