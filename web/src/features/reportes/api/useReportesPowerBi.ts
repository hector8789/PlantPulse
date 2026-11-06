import { useQuery } from '@tanstack/react-query'
import type { Guid } from '@/types/domain'
import { msHastaRenovar } from '../lib/tokenRenewal'
import { getEmbedInfo, getReportesPowerBi } from './reportesApi'
import { reportesKeys } from './reportesKeys'

export function useReportesPowerBi() {
  return useQuery({
    queryKey: reportesKeys.lista(),
    queryFn: getReportesPowerBi,
    staleTime: 10 * 60_000,
  })
}

/**
 * Token de embed con renovación automática: la query se vuelve a pedir ~5 min antes de expiresAtUtc,
 * también con la pestaña en segundo plano. El componente aplica el token nuevo con report.setAccessToken().
 */
export function useEmbedInfo(reportKey: string | null, plantaId: Guid | null) {
  return useQuery({
    queryKey: reportesKeys.embed(reportKey ?? '', plantaId),
    queryFn: () => getEmbedInfo(reportKey as string, plantaId),
    enabled: reportKey !== null,
    // Un token cacheado deja de servir cuando entra en el margen de renovación.
    staleTime: (query) => (query.state.data ? msHastaRenovar(query.state.data.expiresAtUtc) : 0),
    refetchInterval: (query) => (query.state.data ? msHastaRenovar(query.state.data.expiresAtUtc) : false),
    refetchIntervalInBackground: true,
    // Al cambiar de pestaña se conserva el embed actual mientras llega el token de la otra key,
    // pero NUNCA entre alcances distintos: el token lleva el RLS de la planta.
    placeholderData: (previo, queryPrevia) =>
      queryPrevia?.queryKey[3] === (plantaId ?? 'todas') ? previo : undefined,
  })
}
