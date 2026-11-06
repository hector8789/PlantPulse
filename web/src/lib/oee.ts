/** Umbrales del semáforo de OEE (fracción 0–1). 85 % es la referencia "clase mundial". */
export const OEE_UMBRALES = { verde: 0.85, amarillo: 0.6 } as const

export type NivelOee = 'verde' | 'amarillo' | 'rojo'

export function nivelOee(oee: number): NivelOee {
  if (oee >= OEE_UMBRALES.verde) return 'verde'
  if (oee >= OEE_UMBRALES.amarillo) return 'amarillo'
  return 'rojo'
}

/** Un turno o periodo sin piezas capturadas se muestra como "Sin producción", no como 0 % en rojo. */
export function sinProduccion(oee: { piezasTotales: number; tiempoPlanificadoMin: number }): boolean {
  return oee.piezasTotales === 0 || oee.tiempoPlanificadoMin === 0
}
