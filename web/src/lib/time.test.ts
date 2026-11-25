import { describe, expect, it } from 'vitest'
import { alertLevel, formatDuration, formatElapsed, formatRelative } from './time'

const NOW = Date.parse('2026-11-21T06:00:00Z')
const secondsAgo = (seconds: number) => new Date(NOW - seconds * 1000).toISOString()

describe('formatElapsed', () => {
  it('returns seconds under one minute', () => {
    expect(formatElapsed(secondsAgo(0), NOW)).toBe('0秒')
    expect(formatElapsed(secondsAgo(42), NOW)).toBe('42秒')
  })

  it('returns minutes and seconds under one hour', () => {
    expect(formatElapsed(secondsAgo(65), NOW)).toBe('1分5秒')
    expect(formatElapsed(secondsAgo(59 * 60 + 59), NOW)).toBe('59分59秒')
  })

  it('returns hours above one hour', () => {
    expect(formatElapsed(secondsAgo(3723), NOW)).toBe('1時間2分3秒')
  })

  it('handles missing or invalid timestamps', () => {
    expect(formatElapsed(null, NOW)).toBe('—')
    expect(formatElapsed('not-a-date', NOW)).toBe('—')
  })
})

describe('alertLevel', () => {
  it('warns at 5 minutes and becomes danger at 30 minutes', () => {
    expect(alertLevel(secondsAgo(299), NOW)).toBe('normal')
    expect(alertLevel(secondsAgo(300), NOW)).toBe('warning')
    expect(alertLevel(secondsAgo(1799), NOW)).toBe('warning')
    expect(alertLevel(secondsAgo(1800), NOW)).toBe('danger')
  })

  it('handles missing timestamps', () => {
    expect(alertLevel(null, NOW)).toBe('normal')
  })
})

describe('formatRelative', () => {
  it('formats recent timestamps', () => {
    expect(formatRelative(secondsAgo(3), NOW)).toBe('たった今')
    expect(formatRelative(secondsAgo(30), NOW)).toBe('30秒前')
    expect(formatRelative(secondsAgo(90), NOW)).toBe('1分前')
    expect(formatRelative(secondsAgo(7200), NOW)).toBe('2時間前')
    expect(formatRelative(secondsAgo(172800), NOW)).toBe('2日前')
  })
})

describe('formatDuration', () => {
  it('formats seconds, minutes and hours', () => {
    expect(formatDuration(0)).toBe('0秒')
    expect(formatDuration(45)).toBe('45秒')
    expect(formatDuration(65)).toBe('1分5秒')
    expect(formatDuration(120)).toBe('2分')
    expect(formatDuration(3720)).toBe('1時間2分')
    expect(formatDuration(7200)).toBe('2時間')
  })

  it('clamps invalid values', () => {
    expect(formatDuration(-5)).toBe('0秒')
    expect(formatDuration(Number.NaN)).toBe('—')
  })
})
