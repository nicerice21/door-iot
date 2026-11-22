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
