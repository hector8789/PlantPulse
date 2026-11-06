import type { CategoriaParo, EstadoOrden, Guid, IsoUtc, TipoParo } from '@/types/domain'

export interface Turno {
  id: Guid
  numero: number
  nombre: string
  /** "06:00" en hora local de la planta */
  horaInicio: string
  horaFin: string
}

export interface Planta {
  id: Guid
  codigo: string
  nombre: string
  /** Zona IANA, p. ej. "America/Mexico_City" */
  zonaHoraria: string
  turnos: Turno[]
}

export interface Linea {
  id: Guid
  plantaId: Guid
  codigo: string
  nombre: string
  tiempoCicloEstandarSeg: number
  activa: boolean
}

export interface CausaParo {
  id: Guid
  codigo: string
  descripcion: string
  tipo: TipoParo
  categoria: CategoriaParo
  activa: boolean
}

export interface OrdenProduccion {
  id: Guid
  numero: string
  lineaId: Guid
  materialCodigo: string
  materialDescripcion: string
  cantidadPlaneada: number
  fechaInicioPlan: IsoUtc
  fechaFinPlan: IsoUtc
  tiempoCicloIdealSeg: number
  /** true si la orden sobrescribe el tiempo de ciclo estándar de la línea */
  tiempoCicloPropio: boolean
  costoUnitarioMxn: number | null
  estado: EstadoOrden
}

export interface SincronizacionErpRequest {
  plantaId: Guid
}

export interface SincronizacionErpResult {
  plantaId: Guid
  plantaCodigo: string
  recibidas: number
  creadas: number
  actualizadas: number
  sinCambios: number
  omitidas: { numero: string; motivo: string }[]
  sincronizadoEnUtc: IsoUtc
}
