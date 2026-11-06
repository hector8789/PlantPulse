import { httpClient } from '@/api/httpClient'
import type { Guid } from '@/types/domain'
import type {
  AbrirTurnoRequest,
  CorregirProduccionRequest,
  RegistrarProduccionRequest,
  TurnoLinea,
  TurnoLineaResumen,
  TurnosLineaFiltro,
} from '../types'

export async function getTurnosLinea(filtro: TurnosLineaFiltro): Promise<TurnoLineaResumen[]> {
  const { data } = await httpClient.get<TurnoLineaResumen[]>('/turnos-linea', { params: filtro })
  return data
}

export async function getTurnoLinea(id: Guid): Promise<TurnoLinea> {
  const { data } = await httpClient.get<TurnoLinea>(`/turnos-linea/${id}`)
  return data
}

export async function abrirTurno(body: AbrirTurnoRequest): Promise<TurnoLinea> {
  const { data } = await httpClient.post<TurnoLinea>('/turnos-linea', body)
  return data
}

export async function registrarProduccion(turnoId: Guid, body: RegistrarProduccionRequest): Promise<TurnoLinea> {
  const { data } = await httpClient.post<TurnoLinea>(`/turnos-linea/${turnoId}/produccion`, body)
  return data
}

export async function corregirProduccion(
  turnoId: Guid,
  registroId: Guid,
  body: CorregirProduccionRequest,
): Promise<TurnoLinea> {
  const { data } = await httpClient.put<TurnoLinea>(`/turnos-linea/${turnoId}/produccion/${registroId}`, body)
  return data
}

export async function cerrarTurno(turnoId: Guid): Promise<TurnoLinea> {
  const { data } = await httpClient.post<TurnoLinea>(`/turnos-linea/${turnoId}/cerrar`)
  return data
}

export async function reabrirTurno(turnoId: Guid): Promise<TurnoLinea> {
  const { data } = await httpClient.post<TurnoLinea>(`/turnos-linea/${turnoId}/reabrir`)
  return data
}
