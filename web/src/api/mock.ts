import type {
  BatteryHistoryResponse,
  BatterySample,
  DailyUsage,
  DoorEvent,
  DoorHistoryResponse,
  DoorStatsResponse,
  DoorsResponse,
} from '../types/doors'

/**
 * 開発用のダミーデータ。Azure Functions を起動せずに画面を確認するときは
 * .env.local に VITE_USE_MOCK=true を設定する。
 */
export function createMockResponse(): DoorsResponse {
  const now = Date.now()

  return {
    gateway: {
      id: 'gw-pc-01',
      status: 'online',
      lastSeenAt: new Date(now - 3_000).toISOString(),
    },
    doors: [
      {
        deviceId: '0x8201015A',
        logicalId: 1,
        name: 'ドア 1',
        state: 'open',
        openedAt: new Date(now - 6 * 60 * 1000).toISOString(),
        lastChangedAt: new Date(now - 6 * 60 * 1000).toISOString(),
        lastSeenAt: new Date(now - 8_000).toISOString(),
        isOnline: true,
        telemetry: { batteryMv: 2950, lqi: 135 },
      },
    ],
  }
}

/**
 * 開発用のダミー履歴。現在のモック状態と矛盾しないよう、ドアが開いていれば
 * 「開」から始まる並びを返す。並びは常に新しい順。
 */
export function createMockHistory(deviceId: string): DoorHistoryResponse {
  const door = createMockResponse().doors.find((item) => item.deviceId === deviceId)
  const lastChanged = door === undefined ? Date.now() - 45 * 60_000 : Date.parse(door.lastChangedAt)
  const isOpen = door?.state === 'open'

  const events: DoorEvent[] = []

  if (isOpen) {
    events.push({ state: 'open', changedAt: new Date(lastChanged).toISOString(), openedAt: null })
    events.push({
      state: 'closed',
      changedAt: new Date(lastChanged - 8 * 60_000).toISOString(),
      openedAt: new Date(lastChanged - 21 * 60_000).toISOString(),
    })
    events.push({
      state: 'open',
      changedAt: new Date(lastChanged - 21 * 60_000).toISOString(),
      openedAt: null,
    })
  } else {
    events.push({
      state: 'closed',
      changedAt: new Date(lastChanged).toISOString(),
      openedAt: new Date(lastChanged - 7 * 60_000).toISOString(),
    })
    events.push({
      state: 'open',
      changedAt: new Date(lastChanged - 7 * 60_000).toISOString(),
      openedAt: null,
    })
  }

  return { deviceId, events }
}

/**
 * 開発用のダミー電池電圧。1 時間に 1 件で、ゆるやかに降下させてグラフを確認できるようにする。
 * 直近の値は createMockResponse の現在状態（2950mV 前後）と大きくずれないようにする。
 */
export function createMockBatteryHistory(deviceId: string, hours: number): BatteryHistoryResponse {
  const bucketNow = new Date()
  bucketNow.setMinutes(0, 0, 0)

  const samples: BatterySample[] = []
  for (let offset = hours - 1; offset >= 0; offset--) {
    const sampledAt = new Date(bucketNow.getTime() - offset * 3_600_000)
    const progress = hours <= 1 ? 1 : (hours - 1 - offset) / (hours - 1)
    const batteryMv = Math.round(3050 - 100 * progress + 6 * Math.sin(offset / 7))
    samples.push({ sampledAt: sampledAt.toISOString(), batteryMv })
  }

  const values = samples.map((sample) => sample.batteryMv)
  const latest = values[values.length - 1]
  const elapsedDays = (samples.length - 1) / 24
  const mvPerDay = elapsedDays > 0
    ? Math.round(((latest - values[0]) / elapsedDays) * 10) / 10
    : null

  return {
    deviceId,
    from: samples[0].sampledAt,
    to: new Date().toISOString(),
    samples,
    summary: {
      sampleCount: samples.length,
      latestMv: latest,
      minMv: Math.min(...values),
      averageMv: Math.round(values.reduce((sum, value) => sum + value, 0) / values.length),
    },
    trend: {
      mvPerDay,
      estimatedDaysRemaining: mvPerDay !== null && mvPerDay < 0
        ? Math.max(0, Math.round((latest - 2300) / -mvPerDay))
        : null,
      thresholdMv: 2300,
    },
  }
}

/** 開発用のダミー利用統計。開いた回数と開放時間が日ごとにばらつく。 */
export function createMockStats(deviceId: string, days: number, tzOffsetMinutes: number): DoorStatsResponse {
  const hourPattern = [8, 12, 18, 21, 7, 9, 15, 23]
  const now = new Date()
  const openCountByHour = Array.from({ length: 24 }, () => 0)
  const daily: DailyUsage[] = []

  let openCount = 0
  let totalOpenSeconds = 0
  let maxOpenSeconds = 0

  for (let offset = days - 1; offset >= 0; offset--) {
    const date = new Date(now.getTime() - offset * 86_400_000)
    const opens = (offset * 7 + 3) % 6
    let openSeconds = 0
    let dayMax = 0

    for (let index = 0; index < opens; index++) {
      const duration = 45 + ((offset * 13 + index * 37) % 180)
      openSeconds += duration
      dayMax = Math.max(dayMax, duration)
      openCountByHour[hourPattern[(offset + index) % hourPattern.length]]++
    }

    openCount += opens
    totalOpenSeconds += openSeconds
    maxOpenSeconds = Math.max(maxOpenSeconds, dayMax)
    daily.push({
      date: localDate(date, tzOffsetMinutes),
      openCount: opens,
      openSeconds,
      maxOpenSeconds: dayMax,
    })
  }

  return {
    deviceId,
    from: new Date(now.getTime() - (days - 1) * 86_400_000).toISOString(),
    to: now.toISOString(),
    tzOffsetMinutes,
    days,
    openCount,
    closeCount: openCount,
    totalOpenSeconds,
    averageOpenSeconds: openCount === 0 ? 0 : Math.round(totalOpenSeconds / openCount),
    maxOpenSeconds,
    daily,
    openCountByHour,
  }
}

/** UTC の時刻を指定したオフセットの日付（yyyy-MM-dd）にする。 */
function localDate(date: Date, tzOffsetMinutes: number): string {
  return new Date(date.getTime() + tzOffsetMinutes * 60_000).toISOString().slice(0, 10)
}
