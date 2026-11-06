import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { Guid } from '@/types/domain'
import type {
  AbrirTurnoRequest,
  CorregirProduccionRequest,
  RegistrarProduccionRequest,
  TurnosLineaFiltro,
} from '../types'
import {
  abrirTurno,
  cerrarTurno,
  corregirProduccion,
  getTurnoLinea,
  getTurnosLinea,
  reabrirTurno,
  registrarProduccion,
} from './produccionApi'
import { turnosLineaKeys } from './produccionKeys'
import { useTurnoLineaCache } from './useTurnoLineaCache'

export function useTurnosLinea(filtro: TurnosLineaFiltro | null) {
  return useQuery({
    queryKey: filtro ? turnosLineaKeys.list(filtro) : turnosLineaKeys.lists(),
    queryFn: () => getTurnosLinea(filtro as TurnosLineaFiltro),
    enabled: filtro !== null,
    placeholderData: keepPreviousData,
  })
}

export function useTurnoLinea(id: Guid | undefined) {
  return useQuery({
    queryKey: turnosLineaKeys.detail(id ?? ''),
    queryFn: () => getTurnoLinea(id as Guid),
    enabled: Boolean(id),
  })
}

export function useAbrirTurno() {
  const onTurno = useTurnoLineaCache()
  // El formulario maneja los errores: un 409 turno_linea.ya_existe lleva al turno existente.
  return useMutation({ mutationFn: abrirTurno, onSuccess: onTurno, meta: { skipGlobalError: true } })
}

/** Busca el turno ya abierto para línea + turno + fecha (flujo sugerido ante 409 turno_linea.ya_existe). */
export function useBuscarTurnoExistente() {
  const qc = useQueryClient()
  return async (plantaId: Guid, req: AbrirTurnoRequest) => {
    const filtro: TurnosLineaFiltro = {
      plantaId,
      lineaId: req.lineaId,
      desde: req.fechaOperativa,
      hasta: req.fechaOperativa,
    }
    const turnos = await qc.fetchQuery({
      queryKey: turnosLineaKeys.list(filtro),
      queryFn: () => getTurnosLinea(filtro),
      staleTime: 0,
    })
    return turnos.find((t) => t.turnoId === req.turnoId) ?? null
  }
}

export function useRegistrarProduccion(turnoId: Guid) {
  const onTurno = useTurnoLineaCache()
  return useMutation({
    mutationFn: (body: RegistrarProduccionRequest) => registrarProduccion(turnoId, body),
    onSuccess: onTurno,
  })
}

export function useCorregirProduccion(turnoId: Guid) {
  const onTurno = useTurnoLineaCache()
  return useMutation({
    mutationFn: ({ registroId, body }: { registroId: Guid; body: CorregirProduccionRequest }) =>
      corregirProduccion(turnoId, registroId, body),
    onSuccess: onTurno,
  })
}

export function useCerrarTurno(turnoId: Guid) {
  const onTurno = useTurnoLineaCache()
  return useMutation({ mutationFn: () => cerrarTurno(turnoId), onSuccess: onTurno })
}

export function useReabrirTurno(turnoId: Guid) {
  const onTurno = useTurnoLineaCache()
  return useMutation({ mutationFn: () => reabrirTurno(turnoId), onSuccess: onTurno })
}
