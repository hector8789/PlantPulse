import { describe, expect, it } from 'vitest'
import { msHastaRenovar, RENOVAR_MINIMO_MS, tokenVigente } from './tokenRenewal'

const AHORA = Date.parse('2026-11-06T18:00:00Z')

describe('renovación del token de Power BI', () => {
  it('renueva 5 minutos antes de que venza', () => {
    expect(msHastaRenovar('2026-11-06T19:00:00Z', AHORA)).toBe(55 * 60_000)
  })

  it('nunca programa la renovación antes del mínimo, aunque ya esté por vencer o vencido', () => {
    expect(msHastaRenovar('2026-11-06T18:02:00Z', AHORA)).toBe(RENOVAR_MINIMO_MS)
    expect(msHastaRenovar('2026-11-06T17:00:00Z', AHORA)).toBe(RENOVAR_MINIMO_MS)
    expect(msHastaRenovar('no es fecha', AHORA)).toBe(RENOVAR_MINIMO_MS)
  })

  it('acepta el offset +00:00 que devuelve el API', () => {
    expect(msHastaRenovar('2026-11-06T19:00:00.000+00:00', AHORA)).toBe(55 * 60_000)
  })

  it('un token dentro del margen ya no se considera vigente', () => {
    expect(tokenVigente('2026-11-06T19:00:00Z', AHORA)).toBe(true)
    expect(tokenVigente('2026-11-06T18:04:00Z', AHORA)).toBe(false)
  })
})
