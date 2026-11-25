import type { BatteryHistoryResponse, BatterySample, DoorEvent, DoorHistoryResponse, DoorsResponse } from '../types/doors'

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
  return {
    deviceId,
    from: samples[0].sampledAt,
    to: new Date().toISOString(),
    samples,
    summary: {
      sampleCount: samples.length,
      latestMv: values[values.length - 1],
      minMv: Math.min(...values),
      averageMv: Math.round(values.reduce((sum, value) => sum + value, 0) / values.length),
    },
  }
}
