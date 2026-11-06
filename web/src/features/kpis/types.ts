import type { FechaOperativa, Guid, Oee } from '@/types/domain'

export interface KpiFiltro {
  plantaId: Guid
  lineaId?: Guid
  desde: FechaOperativa
  hasta: FechaOperativa
}

export interface OeePorLinea {
  lineaId: Guid
  lineaCodigo: string
  turnos: number
  oee: Oee
}

export interface OeePorLineaTurno extends OeePorLinea {
  turnoNumero: number
}

export interface OeeKpiResponse {
  desde: FechaOperativa
  hasta: FechaOperativa
  /** Número de turnos-línea agregados */
  turnos: number
  total: Oee
  porLinea: OeePorLinea[]
  porLineaTurno: OeePorLineaTurno[]
}
