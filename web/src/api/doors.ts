import type { BatteryHistoryResponse, DoorHistoryResponse, DoorStatsResponse, DoorsResponse } from '../types/doors'
import { createMockBatteryHistory, createMockHistory, createMockResponse, createMockStats } from './mock'

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
const useMock = import.meta.env.VITE_USE_MOCK === 'true'

/** GET /api/doors を取得する。ベース URL 未設定時は同一オリジン（Vite の proxy）を使う。 */
export async function fetchDoors(signal?: AbortSignal): Promise<DoorsResponse> {
  if (useMock) {
    return createMockResponse()
  }

  const response = await fetch(`${apiBaseUrl}/api/doors`, {
    signal,
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(`API が ${response.status} を返しました`)
  }

  return (await response.json()) as DoorsResponse
}

/** GET /api/doors/{deviceId}/history を取得する。 */
export async function fetchDoorHistory(deviceId: string, signal?: AbortSignal): Promise<DoorHistoryResponse> {
  if (useMock) {
    return createMockHistory(deviceId)
  }

  const response = await fetch(`${apiBaseUrl}/api/doors/${encodeURIComponent(deviceId)}/history`, {
    signal,
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(`API が ${response.status} を返しました`)
  }

  return (await response.json()) as DoorHistoryResponse
}

/** GET /api/doors/{deviceId}/battery?hours= を取得する。期間は時間単位（既定 168 = 7日）。 */
export async function fetchBatteryHistory(
  deviceId: string,
  hours: number,
  signal?: AbortSignal,
): Promise<BatteryHistoryResponse> {
  if (useMock) {
    return createMockBatteryHistory(deviceId, hours)
  }

  const response = await fetch(`${apiBaseUrl}/api/doors/${encodeURIComponent(deviceId)}/battery?hours=${hours}`, {
    signal,
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(`API が ${response.status} を返しました`)
  }

  return (await response.json()) as BatteryHistoryResponse
}

/** GET /api/doors/{deviceId}/stats を取得する。日付の境界はブラウザのタイムゾーンで決める。 */
export async function fetchDoorStats(
  deviceId: string,
  days: number,
  signal?: AbortSignal,
): Promise<DoorStatsResponse> {
  const tzOffsetMinutes = -new Date().getTimezoneOffset()

  if (useMock) {
    return createMockStats(deviceId, days, tzOffsetMinutes)
  }

  const query = `days=${days}&tzOffsetMinutes=${tzOffsetMinutes}`
  const response = await fetch(`${apiBaseUrl}/api/doors/${encodeURIComponent(deviceId)}/stats?${query}`, {
    signal,
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(`API が ${response.status} を返しました`)
  }

  return (await response.json()) as DoorStatsResponse
}
