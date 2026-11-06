import { describe, expect, it, vi } from 'vitest'
import { exportarExcel, hoja, nombreHojaValido } from './excel'

const writeFile = vi.fn()
vi.mock('xlsx', async (importOriginal) => {
  const real = await importOriginal<typeof import('xlsx')>()
  return { ...real, writeFile: (...args: unknown[]) => writeFile(...args) }
})

interface Fila {
  linea: string
  oee: number
  piezas: number
}

describe('exportación a Excel', () => {
  it('arma la matriz con encabezados y valores', () => {
    const h = hoja<Fila>(
      'OEE',
      [
        { header: 'Línea', valor: (f) => f.linea },
        { header: 'OEE', valor: (f) => f.oee, formato: 'porcentaje' },
      ],
      [{ linea: 'LEO-L01', oee: 0.7547, piezas: 10 }],
    )
    expect(h.matriz).toEqual([
      ['Línea', 'OEE'],
      ['LEO-L01', 0.7547],
    ])
  })

  it('limpia los nombres de hoja que Excel no acepta', () => {
    expect(nombreHojaValido('Paros [turno 1/3]')).toBe('Paros  turno 1 3 ')
    expect(nombreHojaValido('x'.repeat(40))).toHaveLength(31)
  })

  it('escribe el libro con formato de porcentaje y extensión .xlsx', async () => {
    await exportarExcel('PlantPulse_OEE', [
      hoja<Fila>(
        'Por línea',
        [
          { header: 'Línea', valor: (f) => f.linea },
          { header: 'OEE', valor: (f) => f.oee, formato: 'porcentaje' },
          { header: 'Piezas', valor: (f) => f.piezas, formato: 'entero' },
        ],
        [{ linea: 'LEO-L01', oee: 0.7547, piezas: 1200 }],
      ),
    ])
    expect(writeFile).toHaveBeenCalledTimes(1)
    const [libro, nombre] = writeFile.mock.calls[0] as [
      { SheetNames: string[]; Sheets: Record<string, Record<string, { v: unknown; z?: string }>> },
      string,
    ]
    expect(nombre).toBe('PlantPulse_OEE.xlsx')
    expect(libro.SheetNames).toEqual(['Por línea'])
    const ws = libro.Sheets['Por línea']
    expect(ws.B2).toMatchObject({ v: 0.7547, z: '0.0%' })
    expect(ws.C2).toMatchObject({ v: 1200, z: '#,##0' })
  })
})
