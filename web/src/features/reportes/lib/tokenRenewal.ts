/** Se pide un token nuevo 5 minutos antes de que venza el actual. */
export const RENOVAR_ANTES_MS = 5 * 60_000
/** Nunca se reintenta más seguido que esto, aunque el token ya esté por vencer (evita ciclos). */
export const RENOVAR_MINIMO_MS = 15_000

/** Milisegundos que faltan para renovar el token. */
export function msHastaRenovar(expiresAtUtc: string, ahora = Date.now()): number {
  const vence = Date.parse(expiresAtUtc)
  if (Number.isNaN(vence)) return RENOVAR_MINIMO_MS
  return Math.max(vence - RENOVAR_ANTES_MS - ahora, RENOVAR_MINIMO_MS)
}

/** true si al token le queda más que el margen de renovación (se puede usar tal cual). */
export function tokenVigente(expiresAtUtc: string, ahora = Date.now()): boolean {
  return Date.parse(expiresAtUtc) - RENOVAR_ANTES_MS > ahora
}
