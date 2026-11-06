import { httpClient } from '@/api/httpClient'
import type { Guid } from '@/types/domain'
import type { EmbedInfo, ReportePowerBi } from '../types'

export async function getReportesPowerBi(): Promise<ReportePowerBi[]> {
  const { data } = await httpClient.get<ReportePowerBi[]>('/powerbi/reportes')
  return data
}

/** Sin plantaId, el RLS incluye todas las plantas del usuario; con plantaId, solo esa (403 si no es suya). */
export async function getEmbedInfo(reportKey: string, plantaId: Guid | null): Promise<EmbedInfo> {
  const { data } = await httpClient.get<EmbedInfo>(`/powerbi/embed/${encodeURIComponent(reportKey)}`, {
    params: plantaId ? { plantaId } : undefined,
  })
  return data
}
