import { useMutation } from '@tanstack/react-query'
import { useTurnoLineaCache } from '@/features/produccion/api/useTurnoLineaCache'
import type { Guid } from '@/types/domain'
import type { RegistrarParoRequest } from '../types'
import { eliminarParo, registrarParo } from './parosApi'

export function useRegistrarParo(turnoId: Guid) {
  const onTurno = useTurnoLineaCache()
  return useMutation({
    mutationFn: (body: RegistrarParoRequest) => registrarParo(turnoId, body),
    onSuccess: onTurno,
  })
}

export function useEliminarParo(turnoId: Guid) {
  const onTurno = useTurnoLineaCache()
  return useMutation({
    mutationFn: (paroId: Guid) => eliminarParo(turnoId, paroId),
    onSuccess: onTurno,
  })
}
