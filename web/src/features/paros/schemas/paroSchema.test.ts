import { describe, expect, it } from 'vitest'
import { paroSchema } from './paroSchema'

const schema = paroSchema({ inicioTurno: '2026-11-04T06:00', finTurno: '2026-11-04T14:00', ahora: '2026-11-04T16:00' })
const base = { causaParoId: 'c', comentario: '' }

function errorEn(valores: { inicio: string; fin: string }) {
  const r = schema.safeParse({ ...base, ...valores })
  return r.success ? null : r.error.issues.map((i) => i.path.join('.'))
}

describe('paroSchema', () => {
  it('acepta un paro dentro del turno', () => {
    expect(errorEn({ inicio: '2026-11-04T08:00', fin: '2026-11-04T08:30' })).toBeNull()
  })

  it('rechaza un paro que cruza el fin del turno', () => {
    expect(errorEn({ inicio: '2026-11-04T13:30', fin: '2026-11-04T14:30' })).toEqual(['fin'])
  })

  it('rechaza fin anterior o igual al inicio', () => {
    expect(errorEn({ inicio: '2026-11-04T09:00', fin: '2026-11-04T09:00' })).toEqual(['fin'])
  })

  it('rechaza un paro en el futuro', () => {
    const enCurso = paroSchema({ inicioTurno: '2026-11-04T06:00', finTurno: '2026-11-04T14:00', ahora: '2026-11-04T10:00' })
    const r = enCurso.safeParse({ ...base, inicio: '2026-11-04T09:00', fin: '2026-11-04T11:00' })
    expect(r.success).toBe(false)
  })
})
