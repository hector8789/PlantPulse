import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { Guid } from '@/types/domain'
import { catalogosKeys } from './catalogosKeys'
import { getCausasParo, getLineas, getOrdenes, getPlantas, sincronizarOrdenesErp } from './catalogosApi'

const CATALOGO_STALE = 10 * 60_000

export function usePlantas(enabled = true) {
  return useQuery({
    queryKey: catalogosKeys.plantas(),
    queryFn: getPlantas,
    staleTime: CATALOGO_STALE,
    enabled,
  })
}

export function useLineas(plantaId: Guid | null) {
  return useQuery({
    queryKey: catalogosKeys.lineas(plantaId),
    queryFn: () => getLineas(plantaId as Guid),
    enabled: plantaId !== null,
    staleTime: CATALOGO_STALE,
  })
}

export function useCausasParo() {
  return useQuery({
    queryKey: catalogosKeys.causasParo(),
    queryFn: getCausasParo,
    staleTime: CATALOGO_STALE,
  })
}

export function useOrdenes(lineaId: Guid | null, soloAbiertas = true) {
  return useQuery({
    queryKey: catalogosKeys.ordenes(lineaId, soloAbiertas),
    queryFn: () => getOrdenes(lineaId as Guid, soloAbiertas),
    enabled: lineaId !== null,
  })
}

export function useSincronizarOrdenesErp() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: sincronizarOrdenesErp,
    onSuccess: () => qc.invalidateQueries({ queryKey: catalogosKeys.ordenesAll() }),
  })
}
