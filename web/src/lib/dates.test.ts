import { describe, expect, it } from 'vitest'
import { diasEntre, hoyEnZona, localAUtcIso, sumarDias, utcIsoALocal } from './dates'
import { nivelOee } from './oee'
import { validarRango } from './rangoFechas'

const MX = 'America/Mexico_City' // UTC-6 sin horario de verano

describe('fechas', () => {
  it('convierte hora local de planta a UTC y de regreso', () => {
    expect(localAUtcIso('2026-11-04T22:30', MX)).toBe('2026-11-05T04:30:00.000Z')
    expect(utcIsoALocal('2026-11-05T04:30:00Z', MX)).toBe('2026-11-04T22:30')
  })

  it('la fecha de hoy depende de la zona de la planta', () => {
    const casiMedianocheMx = new Date('2026-11-05T05:30:00Z') // 23:30 del día 4 en CDMX
    expect(hoyEnZona(MX, casiMedianocheMx)).toBe('2026-11-04')
  })

  it('suma días y mide rangos', () => {
    expect(sumarDias('2026-11-04', -7)).toBe('2026-10-28')
    expect(sumarDias('2026-12-31', 1)).toBe('2027-01-01')
    expect(diasEntre('2026-11-01', '2026-11-04')).toBe(3)
  })

  it('valida el rango máximo', () => {
    expect(validarRango('2026-11-05', '2026-11-04', 62)).toMatch(/anterior/)
    expect(validarRango('2026-01-01', '2026-11-04', 92)).toMatch(/92/)
    expect(validarRango('2026-10-29', '2026-11-04', 62)).toBeNull()
  })
})

describe('semáforo OEE', () => {
  it('usa los umbrales 85 % y 60 %', () => {
    expect(nivelOee(0.85)).toBe('verde')
    expect(nivelOee(0.7)).toBe('amarillo')
    expect(nivelOee(0.59)).toBe('rojo')
  })
})
