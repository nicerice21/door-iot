import { useEffect, useState } from 'react'
import { fetchDoorHistory } from '../api/doors'
import { formatElapsed, formatTime } from '../lib/time'
import type { DoorEvent } from '../types/doors'

interface DoorHistoryProps {
  deviceId: string
}

/** カードを開いたときにだけ履歴を取得して表示する。 */
export function DoorHistory({ deviceId }: DoorHistoryProps) {
  const [events, setEvents] = useState<DoorEvent[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    fetchDoorHistory(deviceId, controller.signal)
      .then((response) => setEvents(response.events))
      .catch((cause: unknown) => {
        if (!(cause instanceof DOMException && cause.name === 'AbortError')) {
          setError(cause instanceof Error ? cause.message : '不明なエラー')
        }
      })

    return () => controller.abort()
  }, [deviceId])

  if (error !== null) {
    return <p className="history-status history-error">履歴を取得できませんでした: {error}</p>
  }

  if (events === null) {
    return <p className="history-status">履歴を読み込み中...</p>
  }

  if (events.length === 0) {
    return <p className="history-status">履歴はまだありません。</p>
  }

  return (
    <section className="door-history" aria-label="開閉履歴">
      <h3>最近の履歴</h3>
      <ol className="history-list">
        {events.map((event) => (
          <li key={`${event.changedAt}-${event.state}`} className="history-item">
            <span className={`history-badge state-${event.state}`}>{event.state === 'open' ? '開' : '閉'}</span>
            <span className="history-time">{formatTime(event.changedAt)}</span>
            {event.state === 'closed' && event.openedAt !== null && (
              <span className="history-duration">
                開放 {formatElapsed(event.openedAt, Date.parse(event.changedAt))}
              </span>
            )}
          </li>
        ))}
      </ol>
    </section>
  )
}
