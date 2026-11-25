import type { BatterySample, BatterySummary } from '../types/doors'

/** 電池電圧の警告しきい値。TWELITE の動作下限 2.3V に余裕を持たせた 2.5V。 */
export const LOW_BATTERY_THRESHOLD_MV = 2500

/** mV を「2.95 V」の形にする。値が無ければ「—」。 */
export function formatVoltage(mv: number | null): string {
  return mv === null ? '—' : `${(mv / 1000).toFixed(2)} V`
}

/** 警告しきい値を下回っているか。 */
export function isBatteryLow(mv: number | null): boolean {
  return mv !== null && mv < LOW_BATTERY_THRESHOLD_MV
}

/** 1 日あたりの変化量を「-14.3 mV/日」の形にする。 */
export function formatTrendRate(mvPerDay: number): string {
  return `${mvPerDay > 0 ? '+' : ''}${mvPerDay.toFixed(1)} mV/日`
}

export interface ChartGeometry {
  /** SVG の polyline に渡す座標列。 */
  points: string
  lastX: number
  lastY: number
  minMv: number
  maxMv: number
}

/**
 * サンプルを width × height の矩形へ収めた折れ線の座標を作る。
 * 値の幅が小さいときも線が潰れないよう、上下に最低 20mV の余白を取る。
 */
export function buildChartGeometry(samples: BatterySample[], width: number, height: number): ChartGeometry | null {
  if (samples.length < 2 || width <= 0 || height <= 0) {
    return null
  }

  const values = samples.map((sample) => sample.batteryMv)
  const minMv = Math.min(...values)
  const maxMv = Math.max(...values)
  const margin = Math.max(20, Math.round((maxMv - minMv) * 0.1))
  const lower = minMv - margin
  const span = maxMv + margin - lower

  const stepX = width / (samples.length - 1)
  let lastX = 0
  let lastY = 0
  const points = samples
    .map((sample, index) => {
      const x = index * stepX
      const y = height - ((sample.batteryMv - lower) / span) * height
      lastX = x
      lastY = y
      return `${x.toFixed(2)},${y.toFixed(2)}`
    })
    .join(' ')

  return { points, lastX, lastY, minMv, maxMv }
}

/** 横軸のラベル。24時間表示なら時刻、それ以上なら日付にする。 */
export function formatAxisTime(iso: string, hours: number): string {
  const date = new Date(Date.parse(iso))
  if (Number.isNaN(date.getTime())) {
    return '—'
  }

  return hours <= 24
    ? date.toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit' })
    : date.toLocaleDateString('ja-JP', { month: 'numeric', day: 'numeric' })
}

/** モックなど、サーバーの要約が無いときにサンプルから計算する。 */
export function summarize(samples: BatterySample[]): BatterySummary {
  if (samples.length === 0) {
    return { sampleCount: 0, latestMv: null, minMv: null, averageMv: null }
  }

  const values = samples.map((sample) => sample.batteryMv)
  const total = values.reduce((sum, value) => sum + value, 0)
  return {
    sampleCount: samples.length,
    latestMv: values[values.length - 1],
    minMv: Math.min(...values),
    averageMv: Math.round(total / values.length),
  }
}
