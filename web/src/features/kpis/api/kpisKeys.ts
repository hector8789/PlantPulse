import type { KpiFiltro } from '../types'

export const kpisKeys = {
  all: ['kpis'] as const,
  oee: (filtro: KpiFiltro) => [...kpisKeys.all, 'oee', filtro] as const,
}
