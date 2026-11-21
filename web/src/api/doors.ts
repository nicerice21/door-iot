import type { DoorsResponse } from '../types/doors'
import { createMockResponse } from './mock'

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
