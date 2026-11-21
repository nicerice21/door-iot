import { useEffect, useState } from 'react'
import { fetchDoors } from '../api/doors'
import type { DoorsResponse } from '../types/doors'

interface UseDoorsResult {
  data: DoorsResponse | null
  error: string | null
  isLoading: boolean
}

/** 数秒おきに GET /api/doors をポーリングする。 */
export function useDoors(pollIntervalMs: number): UseDoorsResult {
  const [data, setData] = useState<DoorsResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let cancelled = false
    let inFlight = false
    const controller = new AbortController()

    const load = async () => {
      if (inFlight) {
        return
      }

      inFlight = true
      try {
        const response = await fetchDoors(controller.signal)
        if (!cancelled) {
          setData(response)
          setError(null)
        }
      } catch (cause) {
        if (!cancelled && !(cause instanceof DOMException && cause.name === 'AbortError')) {
          setError(cause instanceof Error ? cause.message : '不明なエラー')
        }
      } finally {
        inFlight = false
        if (!cancelled) {
          setIsLoading(false)
        }
      }
    }

    void load()
    const timer = window.setInterval(() => void load(), pollIntervalMs)

    return () => {
      cancelled = true
      controller.abort()
      window.clearInterval(timer)
    }
  }, [pollIntervalMs])

  return { data, error, isLoading }
}
