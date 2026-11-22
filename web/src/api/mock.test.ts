import { describe, expect, it } from 'vitest'
import { createMockHistory } from './mock'

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
