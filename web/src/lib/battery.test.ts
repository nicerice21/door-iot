import { describe, expect, it } from 'vitest'
import { buildChartGeometry, formatAxisTime, formatTrendRate, formatVoltage, isBatteryLow, summarize } from './battery'
import type { BatterySample } from '../types/doors'

function sample(batteryMv: number, minutes = 0): BatterySample {
  return {
    sampledAt: new Date(Date.UTC(2026, 10, 25, 9, minutes)).toISOString(),
    batteryMv,
  }
}

describe('formatVoltage', () => {
  it('mV を V 表記にする', () => {
    expect(formatVoltage(2950)).toBe('2.95 V')
  })

  it('値が無ければダッシュ', () => {
    expect(formatVoltage(null)).toBe('—')
  })
})

describe('isBatteryLow', () => {
  it('警告しきい値（2.5V）未満を低下とみなす', () => {
    expect(isBatteryLow(2500)).toBe(false)
    expect(isBatteryLow(2499)).toBe(true)
    expect(isBatteryLow(null)).toBe(false)
  })
})

describe('formatTrendRate', () => {
  it('符号付きの mV/日 にする', () => {
    expect(formatTrendRate(-14.25)).toBe('-14.3 mV/日')
    expect(formatTrendRate(3)).toBe('+3.0 mV/日')
  })
})

describe('buildChartGeometry', () => {
  it('2 点未満では描画しない', () => {
    expect(buildChartGeometry([sample(3000)], 100, 50)).toBeNull()
    expect(buildChartGeometry([], 100, 50)).toBeNull()
  })

  it('最初と最後の点を両端に置き、電圧が低いほど下になる', () => {
    const geometry = buildChartGeometry([sample(3000), sample(2900)], 100, 50)

    expect(geometry).not.toBeNull()
    const [first, last] = geometry!.points.split(' ')
    expect(first.split(',')[0]).toBe('0.00')
    expect(last.split(',')[0]).toBe('100.00')
    expect(Number(last.split(',')[1])).toBeGreaterThan(Number(first.split(',')[1]))
    expect(geometry!.lastX).toBe(100)
    expect(geometry!.minMv).toBe(2900)
    expect(geometry!.maxMv).toBe(3000)
  })

  it('値がすべて同じでも線が潰れない', () => {
    const geometry = buildChartGeometry([sample(3000), sample(3000)], 100, 50)

    const ys = geometry!.points.split(' ').map((point) => point.split(',')[1])
    expect(ys[0]).toBe(ys[1])
  })
})

describe('summarize', () => {
  it('最新・最小・平均を計算する', () => {
    const summary = summarize([sample(3100), sample(2950), sample(3000)])

    expect(summary.sampleCount).toBe(3)
    expect(summary.latestMv).toBe(3000)
    expect(summary.minMv).toBe(2950)
    expect(summary.averageMv).toBe(3017)
  })

  it('空なら null を返す', () => {
    expect(summarize([])).toEqual({ sampleCount: 0, latestMv: null, minMv: null, averageMv: null })
  })
})

describe('formatAxisTime', () => {
  it('24時間以内は時刻、それ以上は日付で表す', () => {
    const iso = new Date(Date.UTC(2026, 10, 25, 9, 30)).toISOString()

    expect(formatAxisTime(iso, 24)).toMatch(/^\d{1,2}:\d{2}$/)
    expect(formatAxisTime(iso, 168)).toMatch(/\d{1,2}\/\d{1,2}/)
    expect(formatAxisTime('broken', 24)).toBe('—')
  })
})
