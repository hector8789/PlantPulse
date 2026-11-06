import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import { describe, expect, it, vi } from 'vitest'
import { applyServerErrors } from './forms'

interface Valores {
  piezasTotales: number
  piezasBuenas: number
}

function error422(errors: Record<string, string[]>) {
  const response = {
    status: 422,
    data: { title: 'Validación', errors },
    statusText: '',
    headers: {},
    config: { headers: new AxiosHeaders() },
  } as AxiosResponse
  return new AxiosError('fail', 'ERR', response.config, null, response)
}

describe('applyServerErrors', () => {
  it('mapea campos PascalCase a setError y junta los desconocidos en root.server', () => {
    const setError = vi.fn()
    const ok = applyServerErrors<Valores>(
      error422({ PiezasBuenas: ['Mayor que las totales.'], Otro: ['Algo más.'] }),
      setError,
      ['piezasTotales', 'piezasBuenas'],
    )
    expect(ok).toBe(true)
    expect(setError).toHaveBeenCalledWith(
      'piezasBuenas',
      { type: 'server', message: 'Mayor que las totales.' },
      { shouldFocus: true },
    )
    expect(setError).toHaveBeenCalledWith('root.server', { type: 'server', message: 'Algo más.' })
  })

  it('ignora errores que no son 422', () => {
    const setError = vi.fn()
    expect(applyServerErrors<Valores>(new Error('x'), setError, ['piezasBuenas'])).toBe(false)
    expect(setError).not.toHaveBeenCalled()
  })
})
