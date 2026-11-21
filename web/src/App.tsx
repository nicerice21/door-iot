import './App.css'
import { DoorCard } from './components/DoorCard'
import { useDoors } from './hooks/useDoors'
import { useNow } from './hooks/useNow'
import { formatRelative } from './lib/time'

const configuredPollInterval = Number(import.meta.env.VITE_POLL_INTERVAL_MS ?? 5000)
const pollIntervalMs = Number.isFinite(configuredPollInterval) && configuredPollInterval >= 1000
  ? configuredPollInterval
  : 5000

function App() {
  const { data, error, isLoading } = useDoors(pollIntervalMs)
  const now = useNow()
  const gateway = data?.gateway

  return (
    <div className="app">
      <header className="app-header">
        <div>
          <h1>ドア開閉モニター</h1>
          <p className="subtitle">ドアの開閉状態と、開いてからの経過時間を表示します。</p>
        </div>

        <div className={`gateway-status ${gateway?.status ?? 'unknown'}`}>
          <span className="gateway-label">ゲートウェイ</span>
          <strong>{gateway ? (gateway.status === 'online' ? 'オンライン' : 'オフライン') : '取得中'}</strong>
          {gateway?.lastSeenAt && (
            <span className="gateway-last-seen">最終通信 {formatRelative(gateway.lastSeenAt, now)}</span>
          )}
        </div>
      </header>

      {error && (
        <div className="error-banner" role="alert">
          最新の状態を取得できませんでした: {error}
        </div>
      )}

      {isLoading && !data && <p className="loading">読み込み中...</p>}

      {data && data.doors.length === 0 && (
        <p className="empty">登録されたドアがありません。</p>
      )}

      <main className="door-grid">
        {data?.doors.map((door) => (
          <DoorCard key={door.deviceId} door={door} now={now} />
        ))}
      </main>

      <footer className="app-footer">
        <span>{pollIntervalMs / 1000} 秒ごとに自動更新</span>
        <span>経過時間はブラウザ側で毎秒更新</span>
      </footer>
    </div>
  )
}

export default App
