import type { Guid, IsoUtc } from '@/types/domain'

export interface RegistrarParoRequest {
  causaParoId: Guid
  inicioUtc: IsoUtc
  finUtc: IsoUtc
  comentario?: string
}
