import type { Guid } from '@/types/domain'

export const catalogosKeys = {
  all: ['catalogos'] as const,
  plantas: () => [...catalogosKeys.all, 'plantas'] as const,
  lineas: (plantaId: Guid | null) => [...catalogosKeys.all, 'lineas', plantaId] as const,
  causasParo: () => [...catalogosKeys.all, 'causas-paro'] as const,
  ordenesAll: () => [...catalogosKeys.all, 'ordenes'] as const,
  ordenes: (lineaId: Guid | null, soloAbiertas: boolean) =>
    [...catalogosKeys.ordenesAll(), lineaId, { soloAbiertas }] as const,
}
