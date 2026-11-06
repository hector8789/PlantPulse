import { httpClient } from '@/api/httpClient'
import type { KpiFiltro, OeeKpiResponse } from '../types'

export async function getOeeKpi(filtro: KpiFiltro): Promise<OeeKpiResponse> {
  const { data } = await httpClient.get<OeeKpiResponse>('/kpis/oee', { params: filtro })
  return data
}
