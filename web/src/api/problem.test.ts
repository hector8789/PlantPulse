import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import { describe, expect, it } from 'vitest'
import { getApiProblem, normalizeFieldName, problemMessage } from './problem'

function axiosError(status: number, data: unknown): AxiosError {
  const response = {
    status,
    data,
    statusText: '',
    headers: {},
    config: { headers: new AxiosHeaders() },
  } as AxiosResponse
  return new AxiosError('fail', 'ERR', response.config, null, response)
}

describe('normalizeFieldName', () => {
  it('pasa PascalCase a camelCase y respeta rutas anidadas', () => {
    expect(normalizeFieldName('PiezasBuenas')).toBe('piezasBuenas')
    expect(normalizeFieldName('Registros[0].PiezasBuenas')).toBe('registros.0.piezasBuenas')
    expect(normalizeFieldName('email')).toBe('email')
  })
})

describe('getApiProblem', () => {
  it('lee ProblemDetails con errors en PascalCase', () => {
    const p = getApiProblem(
      axiosError(422, { title: 'Validación', status: 422, errors: { PiezasBuenas: ['No puede ser mayor.'] } }),
    )
    expect(p?.status).toBe(422)
    expect(p?.errors).toEqual({ piezasBuenas: ['No puede ser mayor.'] })
  })

  it('traduce el código 409 a un mensaje de negocio', () => {
    const p = getApiProblem(axiosError(409, { title: 'Conflicto', codigo: 'turno_linea.paro_traslapado' }))
    expect(problemMessage(p)).toMatch(/traslapa/)
  })

  it('identifica errores de red', () => {
    const p = getApiProblem(new AxiosError('Network Error', 'ERR_NETWORK'))
    expect(p?.isNetworkError).toBe(true)
  })

  it('regresa null para errores que no son de Axios', () => {
    expect(getApiProblem(new Error('x'))).toBeNull()
  })
})
