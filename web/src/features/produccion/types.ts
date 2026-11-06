import type { EstadoTurno, FechaOperativa, Guid, IsoUtc, Oee, TipoParo } from '@/types/domain'

export interface RegistroProduccion {
  id: Guid
  ordenProduccionId: Guid
  ordenNumero: string
  materialCodigo: string
  tiempoCicloIdealSeg: number
  piezasTotales: number
  piezasBuenas: number
  scrap: number
  registradoEnUtc: IsoUtc
  modificadoEnUtc: IsoUtc | null
}

export interface ParoRegistrado {
  id: Guid
  causaParoId: Guid
  causaCodigo: string
  causaDescripcion: string
  tipo: TipoParo
  inicioUtc: IsoUtc
  finUtc: IsoUtc
  duracionMin: number
  comentario: string | null
}

export interface TurnoLinea {
  id: Guid
  plantaId: Guid
  lineaId: Guid
  lineaCodigo: string
  turnoId: Guid
  turnoNumero: number
  turnoNombre: string
  fechaOperativa: FechaOperativa
  inicioUtc: IsoUtc
  finUtc: IsoUtc
  estado: EstadoTurno
  cerradoEnUtc: IsoUtc | null
  registros: RegistroProduccion[]
  paros: ParoRegistrado[]
  oee: Oee
}

/**
 * Elemento de GET /api/turnos-linea. El contrato no detalla el tipo de `paros` en la lista:
 * se acepta un conteo o el arreglo completo (pendiente de confirmar con backend).
 */
export interface TurnoLineaResumen {
  id: Guid
  plantaId: Guid
  lineaId: Guid
  lineaCodigo: string
  turnoId: Guid
  turnoNumero: number
  fechaOperativa: FechaOperativa
  estado: EstadoTurno
  inicioUtc: IsoUtc
  finUtc: IsoUtc
  paros: number | ParoRegistrado[]
  oee: Oee
}

export interface TurnosLineaFiltro {
  plantaId: Guid
  lineaId?: Guid
  desde: FechaOperativa
  hasta: FechaOperativa
}

export interface AbrirTurnoRequest {
  lineaId: Guid
  turnoId: Guid
  fechaOperativa: FechaOperativa
}

export interface RegistrarProduccionRequest {
  ordenProduccionId: Guid
  piezasTotales: number
  piezasBuenas: number
}

export interface CorregirProduccionRequest {
  piezasTotales: number
  piezasBuenas: number
}
