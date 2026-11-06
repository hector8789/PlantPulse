import { useQueryClient } from '@tanstack/react-query'
import { catalogosKeys } from '@/features/catalogos/api/catalogosKeys'
import { kpisKeys } from '@/features/kpis/api/kpisKeys'
import type { TurnoLinea } from '../types'
import { turnosLineaKeys } from './produccionKeys'

/**
 * Todas las mutaciones del turno devuelven el TurnoLinea actualizado:
 * se escribe en la caché del detalle y se invalidan la lista, los KPIs y las órdenes
 * (una orden pasa a EnProceso con su primer registro).
 */
export function useTurnoLineaCache() {
  const qc = useQueryClient()
  return (turno: TurnoLinea) => {
    qc.setQueryData(turnosLineaKeys.detail(turno.id), turno)
    void qc.invalidateQueries({ queryKey: turnosLineaKeys.lists() })
    void qc.invalidateQueries({ queryKey: kpisKeys.all })
    void qc.invalidateQueries({ queryKey: catalogosKeys.ordenesAll() })
  }
}
