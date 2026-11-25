import { useEffect, useState } from 'react'
import { fetchBatteryHistory } from '../api/doors'
import { buildChartGeometry, formatAxisTime, formatTrendRate, formatVoltage } from '../lib/battery'
import type { BatteryHistoryResponse } from '../types/doors'

const CHART_WIDTH = 276
const CHART_HEIGHT = 100
const PAD_LEFT = 38
const PAD_TOP = 6

const RANGES = [
  { hours: 24, label: '24時間' },
  { hours: 168, label: '7日' },
  { hours: 720, label: '30日' },
] as const

interface BatteryChartProps {
  deviceId: string
}

interface BatteryData {
  hours: number
  response: BatteryHistoryResponse
}

interface BatteryFailure {
  hours: number
  message: string
}

/** カードを開いたときにだけ電池電圧の履歴を取得し、折れ線（SVG）で表示する。 */
export function BatteryChart({ deviceId }: BatteryChartProps) {
  const [hours, setHours] = useState<number>(168)
  const [data, setData] = useState<BatteryData | null>(null)
  const [failure, setFailure] = useState<BatteryFailure | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    fetchBatteryHistory(deviceId, hours, controller.signal)
      .then((response) => setData({ hours, response }))
      .catch((cause: unknown) => {
        if (!(cause instanceof DOMException && cause.name === 'AbortError')) {
          setFailure({ hours, message: cause instanceof Error ? cause.message : '不明なエラー' })
        }
      })

    return () => controller.abort()
  }, [deviceId, hours])

  // 期間を切り替えた直後は、前の期間の結果を出さずに読み込み中として扱う。
  const history = data !== null && data.hours === hours ? data.response : null
  const error = failure !== null && failure.hours === hours ? failure.message : null

  return (
    <section className="door-battery" aria-label="電池電圧の履歴">
      <div className="battery-header">
        <h3>電池電圧</h3>
        <div className="battery-ranges" role="group" aria-label="表示期間">
          {RANGES.map((range) => (
            <button
              key={range.hours}
              type="button"
              aria-pressed={hours === range.hours}
              className={hours === range.hours ? 'battery-range selected' : 'battery-range'}
              onClick={() => setHours(range.hours)}
            >
              {range.label}
            </button>
          ))}
        </div>
      </div>

      {error !== null && <p className="history-status history-error">電圧の履歴を取得できませんでした: {error}</p>}
      {error === null && history === null && <p className="history-status">電圧の履歴を読み込み中...</p>}
      {history !== null && <BatteryChartBody history={history} hours={hours} />}
    </section>
  )
}

function BatteryChartBody({ history, hours }: { history: BatteryHistoryResponse; hours: number }) {
  const geometry = buildChartGeometry(history.samples, CHART_WIDTH, CHART_HEIGHT)
  if (geometry === null) {
    return <p className="history-status">グラフを描くには 2 点以上の履歴が必要です。</p>
  }

  const { summary } = history
  const { trend } = history
  const first = history.samples[0]
  const last = history.samples[history.samples.length - 1]

  return (
    <>
      <dl className="battery-summary">
        <div>
          <dt>最新</dt>
          <dd>{formatVoltage(summary.latestMv)}</dd>
        </div>
        <div>
          <dt>最小</dt>
          <dd>{formatVoltage(summary.minMv)}</dd>
        </div>
        <div>
          <dt>平均</dt>
          <dd>{formatVoltage(summary.averageMv)}</dd>
        </div>
        <div>
          <dt>件数</dt>
          <dd>{summary.sampleCount}</dd>
        </div>
      </dl>

      <svg
        className="battery-chart"
        viewBox={`0 0 ${PAD_LEFT + CHART_WIDTH} ${PAD_TOP + CHART_HEIGHT + 6}`}
        role="img"
        aria-label={`電池電圧の推移。${summary.sampleCount} 点、最新 ${formatVoltage(summary.latestMv)}`}
      >
        <g transform={`translate(${PAD_LEFT}, ${PAD_TOP})`}>
          <line x1="0" y1={CHART_HEIGHT} x2={CHART_WIDTH} y2={CHART_HEIGHT} className="chart-grid" />
          <line x1="0" y1="0" x2={CHART_WIDTH} y2="0" className="chart-grid" />
          <polyline points={geometry.points} className="chart-line" />
          <circle cx={geometry.lastX} cy={geometry.lastY} r="3" className="chart-point" />
        </g>
        <text x="2" y={PAD_TOP + 8} className="chart-axis">
          {formatVoltage(geometry.maxMv)}
        </text>
        <text x="2" y={PAD_TOP + CHART_HEIGHT} className="chart-axis">
          {formatVoltage(geometry.minMv)}
        </text>
      </svg>

      <div className="battery-axis-range">
        <span>{formatAxisTime(first.sampledAt, hours)}</span>
        <span>{formatAxisTime(last.sampledAt, hours)}</span>
      </div>

      {trend.estimatedDaysRemaining !== null ? (
        <p className="battery-trend">
          動作下限 {formatVoltage(trend.thresholdMv)} まで約 {trend.estimatedDaysRemaining} 日
          {trend.mvPerDay !== null && `（${formatTrendRate(trend.mvPerDay)}）`}
        </p>
      ) : trend.mvPerDay !== null ? (
        <p className="battery-trend muted">低下傾向は見られません（{formatTrendRate(trend.mvPerDay)}）</p>
      ) : (
        <p className="battery-trend muted">推定には 24 時間以上の履歴が必要です。</p>
      )}
    </>
  )
}
