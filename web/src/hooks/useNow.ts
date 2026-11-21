import { useEffect, useState } from 'react'

/** 経過時間を毎秒描画し直すための現在時刻。ポーリング間隔とは独立に動く。 */
export function useNow(intervalMs = 1000): number {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), intervalMs)
    return () => window.clearInterval(timer)
  }, [intervalMs])

  return now
}
