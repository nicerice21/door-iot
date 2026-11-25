import { useEffect, useState } from 'react'
import { fetchDoorStats } from '../api/doors'
import { formatDuration } from '../lib/time'
import type { DoorStatsResponse } from '../types/doors'

const RANGES = [
  { days: 7, label: '7日' },
  { days: 30, label: '30日' },
] as const

interface UsageStatsProps {
  deviceId: string
}

interface UsageData {
  days: number
  response: DoorStatsResponse
}

interface UsageFailure {
  days: number
  message: string
}

/** カードを開いたときにだけ開閉の利用統計を取得して表示する。 */
export function UsageStats({ deviceId }: UsageStatsProps) {
  const [days, setDays] = useState<number>(7)
  const [data, setData] = useState<UsageData | null>(null)
  const [failure, setFailure] = useState<UsageFailure | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    fetchDoorStats(deviceId, days, controller.signal)
      .then((response) => setData({ days, response }))
      .catch((cause: unknown) => {
        if (!(cause instanceof DOMException && cause.name === 'AbortError')) {
          setFailure({ days, message: cause instanceof Error ? cause.message : '不明なエラー' })
        }
      })

    return () => controller.abort()
  }, [deviceId, days])

  const stats = data !== null && data.days === days ? data.response : null
  const error = failure !== null && failure.days === days ? failure.message : null

  return (
    <section className="door-usage" aria-label="利用状況">
      <div className="battery-header">
        <h3>利用状況</h3>
        <div className="battery-ranges" role="group" aria-label="集計期間">
          {RANGES.map((range) => (
            <button
              key={range.days}
              type="button"
              aria-pressed={days === range.days}
              className={days === range.days ? 'battery-range selected' : 'battery-range'}
              onClick={() => setDays(range.days)}
            >
              {range.label}
            </button>
          ))}
        </div>
      </div>

      {error !== null && <p className="history-status history-error">利用状況を取得できませんでした: {error}</p>}
      {error === null && stats === null && <p className="history-status">利用状況を読み込み中...</p>}
      {stats !== null && <UsageStatsBody stats={stats} />}
    </section>
  )
}

function UsageStatsBody({ stats }: { stats: DoorStatsResponse }) {
  if (stats.days === 0 || stats.daily.length === 0) {
    return <p className="history-status">集計できる履歴がありません。</p>
  }

  const maxHour = Math.max(1, ...stats.openCountByHour)
  const maxDay = Math.max(1, ...stats.daily.map((day) => day.openCount))

  return (
    <>
      <dl className="battery-summary">
        <div>
          <dt>開いた回数</dt>
          <dd>{stats.openCount}回</dd>
        </div>
        <div>
          <dt>平均</dt>
          <dd>{formatDuration(stats.averageOpenSeconds)}</dd>
        </div>
        <div>
          <dt>最長</dt>
          <dd>{formatDuration(stats.maxOpenSeconds)}</dd>
        </div>
        <div>
          <dt>合計</dt>
          <dd>{formatDuration(stats.totalOpenSeconds)}</dd>
        </div>
      </dl>

      <div className="usage-hours" aria-label="時間帯別の開閉回数">
        {stats.openCountByHour.map((count, hour) => (
          <span key={hour} className="usage-hour" title={`${hour}時: ${count}回`}>
            <span style={{ height: `${(count / maxHour) * 100}%` }} />
          </span>
        ))}
      </div>
      <div className="usage-hours-axis">
        <span>0時</span>
        <span>12時</span>
        <span>23時</span>
      </div>

      <ol className="usage-days">
        {stats.daily.map((day) => (
          <li key={day.date}>
            <span className="usage-date">{formatDay(day.date)}</span>
            <span className="usage-bar">
              <span style={{ width: `${(day.openCount / maxDay) * 100}%` }} />
            </span>
            <span className="usage-count">{day.openCount}回</span>
            <span className="usage-time">{formatDuration(day.openSeconds)}</span>
          </li>
        ))}
      </ol>
    </>
  )
}

/** yyyy-MM-dd を「11/25」の形にする。 */
function formatDay(date: string): string {
  const parsed = new Date(`${date}T00:00:00`)
  if (Number.isNaN(parsed.getTime())) {
    return date
  }

  return parsed.toLocaleDateString('ja-JP', { month: 'numeric', day: 'numeric' })
}
