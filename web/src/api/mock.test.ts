import { describe, expect, it } from 'vitest'
import { createMockBatteryHistory, createMockHistory } from './mock'

describe('createMockHistory', () => {
  it('returns events newest first', () => {
    const history = createMockHistory('0x8201015A')
    const times = history.events.map((event) => Date.parse(event.changedAt))

    expect(times.length).toBeGreaterThan(0)
    expect(times).toEqual([...times].sort((a, b) => b - a))
  })

  it('sets openedAt only for closed events', () => {
    for (const event of createMockHistory('0x8201015A').events) {
      if (event.state === 'closed') {
        expect(event.openedAt).not.toBeNull()
        expect(Date.parse(event.openedAt!)).toBeLessThanOrEqual(Date.parse(event.changedAt))
      } else {
        expect(event.openedAt).toBeNull()
      }
    }
  })
})

describe('createMockBatteryHistory', () => {
  it('returns hourly samples oldest first', () => {
    const history = createMockBatteryHistory('0x8201015A', 24)
    const times = history.samples.map((sample) => Date.parse(sample.sampledAt))

    expect(history.samples).toHaveLength(24)
    expect(times).toEqual([...times].sort((a, b) => a - b))
    expect(history.summary.sampleCount).toBe(24)
    expect(history.summary.latestMv).toBe(history.samples[23].batteryMv)
  })

  it('agrees with the summary calculated from the samples', () => {
    const history = createMockBatteryHistory('0x8201015A', 168)
    const values = history.samples.map((sample) => sample.batteryMv)

    expect(history.summary.minMv).toBe(Math.min(...values))
    expect(history.summary.averageMv).toBe(Math.round(values.reduce((sum, value) => sum + value, 0) / values.length))
  })
})
