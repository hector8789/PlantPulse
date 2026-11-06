import type { Guid } from '@/types/domain'
import type { TurnosLineaFiltro } from '../types'

export const turnosLineaKeys = {
  all: ['turnos-linea'] as const,
  lists: () => [...turnosLineaKeys.all, 'list'] as const,
  list: (filtro: TurnosLineaFiltro) => [...turnosLineaKeys.lists(), filtro] as const,
  details: () => [...turnosLineaKeys.all, 'detail'] as const,
  detail: (id: Guid) => [...turnosLineaKeys.details(), id] as const,
}
