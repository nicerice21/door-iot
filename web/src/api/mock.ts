import type { DoorsResponse } from '../types/doors'

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
        name: '研究棟 3F 北側ドア',
        state: 'open',
        openedAt: new Date(now - 6 * 60 * 1000).toISOString(),
        lastChangedAt: new Date(now - 6 * 60 * 1000).toISOString(),
        lastSeenAt: new Date(now - 8_000).toISOString(),
        isOnline: true,
        telemetry: { batteryMv: 2950, lqi: 135 },
        meta: {
          floor: '3F',
          mapId: 'building-a-3f',
          mapX: 42.5,
          mapY: 18.0,
          alertThresholdSec: 300,
        },
      },
      {
        deviceId: '0x8201016B',
        logicalId: 2,
        name: '研究棟 2F 実験室',
        state: 'closed',
        openedAt: null,
        lastChangedAt: new Date(now - 25 * 60 * 1000).toISOString(),
        lastSeenAt: new Date(now - 12_000).toISOString(),
        isOnline: true,
        telemetry: { batteryMv: 3010, lqi: 168 },
        meta: {
          floor: '2F',
          mapId: 'building-a-2f',
          mapX: 70.0,
          mapY: 55.0,
          alertThresholdSec: 600,
        },
      },
      {
        deviceId: '0x8201017C',
        logicalId: 3,
        name: '研究棟 1F 倉庫',
        state: 'open',
        openedAt: new Date(now - 47 * 60 * 1000).toISOString(),
        lastChangedAt: new Date(now - 47 * 60 * 1000).toISOString(),
        lastSeenAt: new Date(now - 20 * 60 * 1000).toISOString(),
        isOnline: false,
        telemetry: { batteryMv: 2410, lqi: 42 },
        meta: {
          floor: '1F',
          mapId: 'building-a-1f',
          mapX: 15.0,
          mapY: 80.0,
          alertThresholdSec: 300,
        },
      },
    ],
  }
}
