/** Valores del dominio compartidos con el API (contrato Día 1). Los enums viajan como texto. */

export type Guid = string
/** "YYYY-MM-DD" */
export type FechaOperativa = string
/** ISO 8601 UTC */
export type IsoUtc = string

export const ROLES = ['Admin', 'Supervisor', 'Consulta'] as const
export type Rol = (typeof ROLES)[number]

/** Roles que pueden capturar (abrir turno, producción, paros, cerrar, sincronizar ERP). */
export const ROLES_CAPTURA: readonly Rol[] = ['Admin', 'Supervisor']

export type EstadoTurno = 'Abierto' | 'Cerrado'
export type TipoParo = 'Planificado' | 'NoPlanificado'
export type CategoriaParo =
  | 'Programado'
  | 'Mecanica'
  | 'Electrica'
  | 'Material'
  | 'Calidad'
  | 'CambioModelo'
  | 'Operacion'
export type EstadoOrden = 'Liberada' | 'EnProceso' | 'Terminada'

export const CATEGORIA_PARO_LABEL: Record<CategoriaParo, string> = {
  Programado: 'Programado',
  Mecanica: 'Mecánica',
  Electrica: 'Eléctrica',
  Material: 'Material',
  Calidad: 'Calidad',
  CambioModelo: 'Cambio de modelo',
  Operacion: 'Operación',
}

export const TIPO_PARO_LABEL: Record<TipoParo, string> = {
  Planificado: 'Planificado',
  NoPlanificado: 'No planificado',
}

/** Porcentajes como fracción 0–1 (4 decimales). Tiempos en minutos. */
export interface Oee {
  duracionTurnoMin: number
  parosPlanificadosMin: number
  tiempoPlanificadoMin: number
  parosNoPlanificadosMin: number
  tiempoOperativoMin: number
  piezasTotales: number
  piezasBuenas: number
  scrap: number
  disponibilidad: number
  desempeno: number
  calidad: number
  oee: number
  desempenoExcedido: boolean
}
