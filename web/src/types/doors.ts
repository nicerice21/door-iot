export type DoorState = 'open' | 'closed'

export type GatewayStatus = 'online' | 'offline'

export interface DoorTelemetry {
  batteryMv: number | null
  lqi: number | null
}

export interface Door {
  deviceId: string
  logicalId: number | null
  name: string
  state: DoorState
  openedAt: string | null
  lastChangedAt: string
  lastSeenAt: string
  isOnline: boolean
  telemetry: DoorTelemetry | null
}

export interface Gateway {
  id: string
  status: GatewayStatus
  lastSeenAt: string | null
}

export interface DoorsResponse {
  gateway: Gateway
  doors: Door[]
}

export interface DoorEvent {
  state: DoorState
  changedAt: string
  openedAt: string | null
}

export interface DoorHistoryResponse {
  deviceId: string
  events: DoorEvent[]
}

export interface BatterySample {
  sampledAt: string
  batteryMv: number
}

export interface BatterySummary {
  sampleCount: number
  latestMv: number | null
  minMv: number | null
  averageMv: number | null
}

export interface BatteryTrend {
  mvPerDay: number | null
  estimatedDaysRemaining: number | null
  thresholdMv: number
}

export interface BatteryHistoryResponse {
  deviceId: string
  from: string
  to: string
  samples: BatterySample[]
  summary: BatterySummary
  trend: BatteryTrend
}

export interface DailyUsage {
  date: string
  openCount: number
  openSeconds: number
  maxOpenSeconds: number
}

export interface DoorStatsResponse {
  deviceId: string
  from: string
  to: string
  tzOffsetMinutes: number
  days: number
  openCount: number
  closeCount: number
  totalOpenSeconds: number
  averageOpenSeconds: number
  maxOpenSeconds: number
  daily: DailyUsage[]
  openCountByHour: number[]
}
