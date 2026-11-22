import { useState } from 'react'
import type { Door } from '../types/doors'
import { alertLevel, formatElapsed, formatRelative, formatTime } from '../lib/time'
import { DoorHistory } from './DoorHistory'

interface DoorCardProps {
  door: Door
  now: number
}

export function DoorCard({ door, now }: DoorCardProps) {
  const [showHistory, setShowHistory] = useState(false)
  const isOpen = door.state === 'open'
  const stateClass = !door.isOnline ? 'offline' : isOpen ? 'open' : 'closed'
  const statusLabel = !door.isOnline ? 'オフライン' : isOpen ? '開' : '閉'
  const level = door.isOnline && isOpen
    ? alertLevel(door.openedAt, now, door.meta.alertThresholdSec)
    : 'normal'

  const battery = door.telemetry?.batteryMv == null
    ? '—'
    : `${(door.telemetry.batteryMv / 1000).toFixed(2)} V`
  const lqi = door.telemetry?.lqi ?? '—'

  return (
    <article className={`door-card state-${stateClass} level-${level}`}>
      <header className="door-card-header">
        <div>
          <h2>{door.name}</h2>
          <p className="door-location">{door.meta.floor ?? 'フロア未設定'}</p>
        </div>
        <span className={`state-badge state-${stateClass}`}>{statusLabel}</span>
      </header>

      <div className="door-body">
        {isOpen ? (
          <>
            <p className="elapsed-label">開いてから</p>
            <p className="elapsed-value">{door.isOnline ? formatElapsed(door.openedAt, now) : '—'}</p>
            <p className="opened-at">開放開始 {formatTime(door.openedAt)}</p>
          </>
        ) : (
          <>
            <p className="elapsed-label">最終状態変化</p>
            <p className="elapsed-value muted">{formatTime(door.lastChangedAt)}</p>
          </>
        )}
      </div>

      <footer className="door-card-footer">
        <span title="電池電圧">電池 {battery}</span>
        <span title="電波強度">LQI {lqi}</span>
        <span title="最終受信">最終受信 {formatRelative(door.lastSeenAt, now)}</span>
        <button
          type="button"
          className="history-toggle"
          aria-expanded={showHistory}
          onClick={() => setShowHistory((current) => !current)}
        >
          {showHistory ? '履歴を閉じる' : '履歴'}
        </button>
      </footer>

      {showHistory && <DoorHistory deviceId={door.deviceId} />}
    </article>
  )
}
