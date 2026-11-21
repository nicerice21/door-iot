export type AlertLevel = 'normal' | 'warning' | 'danger'

/** 警告閾値の6倍を危険域とする（既定 5分 → 30分）。 */
const DANGER_MULTIPLIER = 6

export function formatElapsed(openedAt: string | null, now: number): string {
  const opened = openedAt === null ? Number.NaN : Date.parse(openedAt)
  if (Number.isNaN(opened)) {
    return '—'
  }

  const totalSeconds = Math.max(0, Math.floor((now - opened) / 1000))
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  if (hours > 0) {
    return `${hours}時間${minutes}分${seconds}秒`
  }

  if (minutes > 0) {
    return `${minutes}分${seconds}秒`
  }

  return `${seconds}秒`
}

export function alertLevel(openedAt: string | null, now: number, thresholdSec: number): AlertLevel {
  const opened = openedAt === null ? Number.NaN : Date.parse(openedAt)
  if (Number.isNaN(opened)) {
    return 'normal'
  }

  const threshold = thresholdSec > 0 ? thresholdSec : 300
  const elapsedSeconds = Math.max(0, (now - opened) / 1000)

  if (elapsedSeconds >= threshold * DANGER_MULTIPLIER) {
    return 'danger'
  }

  if (elapsedSeconds >= threshold) {
    return 'warning'
  }

  return 'normal'
}

export function formatRelative(iso: string | null, now: number): string {
  const timestamp = iso === null ? Number.NaN : Date.parse(iso)
  if (Number.isNaN(timestamp)) {
    return '—'
  }

  const seconds = Math.max(0, Math.floor((now - timestamp) / 1000))

  if (seconds < 5) {
    return 'たった今'
  }

  if (seconds < 60) {
    return `${seconds}秒前`
  }

  if (seconds < 3600) {
    return `${Math.floor(seconds / 60)}分前`
  }

  if (seconds < 86400) {
    return `${Math.floor(seconds / 3600)}時間前`
  }

  return `${Math.floor(seconds / 86400)}日前`
}

export function formatTime(iso: string | null): string {
  const timestamp = iso === null ? Number.NaN : Date.parse(iso)
  if (Number.isNaN(timestamp)) {
    return '—'
  }

  return new Date(timestamp).toLocaleString('ja-JP', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}
