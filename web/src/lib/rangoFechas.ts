import { diasEntre } from './dates'

/** Valida un rango desde/hasta con un máximo de días (62 para turnos, 92 para KPIs). Devuelve el error o null. */
export function validarRango(desde: string, hasta: string, maxDias: number): string | null {
  if (!desde || !hasta) return 'Selecciona las fechas desde y hasta.'
  if (desde > hasta) return 'La fecha "desde" debe ser anterior o igual a "hasta".'
  if (diasEntre(desde, hasta) + 1 > maxDias) return `El rango máximo es de ${maxDias} días.`
  return null
}
