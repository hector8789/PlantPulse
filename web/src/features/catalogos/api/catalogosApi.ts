import { httpClient } from '@/api/httpClient'
import type { Guid } from '@/types/domain'
import type {
  CausaParo,
  Linea,
  OrdenProduccion,
  Planta,
  SincronizacionErpRequest,
  SincronizacionErpResult,
} from '../types'

export async function getPlantas(): Promise<Planta[]> {
  const { data } = await httpClient.get<Planta[]>('/catalogos/plantas')
  return data
}

export async function getLineas(plantaId: Guid): Promise<Linea[]> {
  const { data } = await httpClient.get<Linea[]>('/catalogos/lineas', { params: { plantaId } })
  return data
}

export async function getCausasParo(): Promise<CausaParo[]> {
  const { data } = await httpClient.get<CausaParo[]>('/catalogos/causas-paro')
  return data
}

export async function getOrdenes(lineaId: Guid, soloAbiertas: boolean): Promise<OrdenProduccion[]> {
  const { data } = await httpClient.get<OrdenProduccion[]>(`/catalogos/lineas/${lineaId}/ordenes`, {
    params: { soloAbiertas },
  })
  return data
}

export async function sincronizarOrdenesErp(body: SincronizacionErpRequest): Promise<SincronizacionErpResult> {
  const { data } = await httpClient.post<SincronizacionErpResult>('/erp/ordenes/sincronizar', body)
  return data
}
