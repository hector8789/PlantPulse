import { httpClient } from '@/api/httpClient'
import type { TurnoLinea } from '@/features/produccion/types'
import type { Guid } from '@/types/domain'
import type { RegistrarParoRequest } from '../types'

export async function registrarParo(turnoId: Guid, body: RegistrarParoRequest): Promise<TurnoLinea> {
  const { data } = await httpClient.post<TurnoLinea>(`/turnos-linea/${turnoId}/paros`, body)
  return data
}

export async function eliminarParo(turnoId: Guid, paroId: Guid): Promise<TurnoLinea> {
  const { data } = await httpClient.delete<TurnoLinea>(`/turnos-linea/${turnoId}/paros/${paroId}`)
  return data
}
