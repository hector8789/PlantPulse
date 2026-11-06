import { describe, expect, it } from 'vitest'
import { abrirTurnoSchema, registrarProduccionSchema } from './produccionSchemas'

describe('registrarProduccionSchema', () => {
  it('rechaza piezas buenas mayores que las totales en el campo piezasBuenas', () => {
    const r = registrarProduccionSchema.safeParse({ ordenProduccionId: 'x', piezasTotales: 10, piezasBuenas: 11 })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toEqual(['piezasBuenas'])
  })

  it('rechaza decimales, negativos y campos vacíos (NaN)', () => {
    expect(registrarProduccionSchema.safeParse({ ordenProduccionId: 'x', piezasTotales: 1.5, piezasBuenas: 1 }).success).toBe(false)
    expect(registrarProduccionSchema.safeParse({ ordenProduccionId: 'x', piezasTotales: -1, piezasBuenas: 0 }).success).toBe(false)
    expect(registrarProduccionSchema.safeParse({ ordenProduccionId: 'x', piezasTotales: Number.NaN, piezasBuenas: 0 }).success).toBe(false)
  })

  it('acepta una captura válida', () => {
    expect(registrarProduccionSchema.safeParse({ ordenProduccionId: 'x', piezasTotales: 100, piezasBuenas: 97 }).success).toBe(true)
  })
})

describe('abrirTurnoSchema', () => {
  const schema = abrirTurnoSchema('2026-11-04', '2026-10-28')
  const base = { lineaId: 'l', turnoId: 't' }

  it('no permite fechas futuras ni más de 7 días atrás', () => {
    expect(schema.safeParse({ ...base, fechaOperativa: '2026-11-05' }).success).toBe(false)
    expect(schema.safeParse({ ...base, fechaOperativa: '2026-10-27' }).success).toBe(false)
    expect(schema.safeParse({ ...base, fechaOperativa: '2026-10-28' }).success).toBe(true)
  })
})
