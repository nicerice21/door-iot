export type AlertLevel = 'normal' | 'warning' | 'danger'

/** プロトタイプ固定の警告閾値（5分）。危険域はその6倍（30分）。 */
export const ALERT_THRESHOLD_SEC = 300
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

export function alertLevel(openedAt: string | null, now: number): AlertLevel {
  const opened = openedAt === null ? Number.NaN : Date.parse(openedAt)
  if (Number.isNaN(opened)) {
    return 'normal'
  }

  const elapsedSeconds = Math.max(0, (now - opened) / 1000)

  if (elapsedSeconds >= ALERT_THRESHOLD_SEC * DANGER_MULTIPLIER) {
    return 'danger'
  }

  if (elapsedSeconds >= ALERT_THRESHOLD_SEC) {
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

/** 秒数を「1分5秒」「2時間3分」の形にする。 */
export function formatDuration(totalSeconds: number): string {
  if (!Number.isFinite(totalSeconds)) {
    return '—'
  }

  const seconds = Math.max(0, Math.floor(totalSeconds))
  if (seconds < 60) {
    return `${seconds}秒`
  }

  const minutes = Math.floor(seconds / 60)
  const remainingSeconds = seconds % 60
  if (minutes < 60) {
    return remainingSeconds > 0 ? `${minutes}分${remainingSeconds}秒` : `${minutes}分`
  }

  const hours = Math.floor(minutes / 60)
  const remainingMinutes = minutes % 60
  return remainingMinutes > 0 ? `${hours}時間${remainingMinutes}分` : `${hours}時間`
}
