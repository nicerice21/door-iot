import { useState } from 'react'
import type { Door } from '../types/doors'
import { alertLevel, formatElapsed, formatRelative, formatTime } from '../lib/time'
import { BatteryChart } from './BatteryChart'
import { DoorHistory } from './DoorHistory'

interface DoorCardProps {
  door: Door
  now: number
}

export function DoorCard({ door, now }: DoorCardProps) {
  const [showHistory, setShowHistory] = useState(false)
  const [showBattery, setShowBattery] = useState(false)
  const isOpen = door.state === 'open'
  const stateClass = !door.isOnline ? 'offline' : isOpen ? 'open' : 'closed'
  const statusLabel = !door.isOnline ? 'オフライン' : isOpen ? '開' : '閉'
  const level = door.isOnline && isOpen
    ? alertLevel(door.openedAt, now)
    : 'normal'

  const battery = door.telemetry?.batteryMv == null
    ? '—'
    : `${(door.telemetry.batteryMv / 1000).toFixed(2)} V`
  const lqi = door.telemetry?.lqi ?? '—'

  return (
    <article className={`door-card state-${stateClass} level-${level}`}>
      <header className="door-card-header">
        <h2>{door.name}</h2>
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
        <div className="door-actions">
          <button
            type="button"
            className="panel-toggle"
            aria-expanded={showBattery}
            onClick={() => setShowBattery((current) => !current)}
          >
            {showBattery ? '電池を閉じる' : '電池'}
          </button>
          <button
            type="button"
            className="panel-toggle"
            aria-expanded={showHistory}
            onClick={() => setShowHistory((current) => !current)}
          >
            {showHistory ? '履歴を閉じる' : '履歴'}
          </button>
        </div>
      </footer>

      {showHistory && <DoorHistory deviceId={door.deviceId} />}
      {showBattery && <BatteryChart deviceId={door.deviceId} />}
    </article>
  )
}
