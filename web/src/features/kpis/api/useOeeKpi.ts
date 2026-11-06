import { keepPreviousData, useQuery } from '@tanstack/react-query'
import type { KpiFiltro } from '../types'
import { getOeeKpi } from './kpisApi'
import { kpisKeys } from './kpisKeys'

export function useOeeKpi(filtro: KpiFiltro | null) {
  return useQuery({
    queryKey: filtro ? kpisKeys.oee(filtro) : kpisKeys.all,
    queryFn: () => getOeeKpi(filtro as KpiFiltro),
    enabled: filtro !== null,
    placeholderData: keepPreviousData,
  })
}
